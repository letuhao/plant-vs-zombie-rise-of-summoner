using System.Text.Json;
using System.Text.RegularExpressions;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// Judges one <see cref="AtomRow"/> before it reaches the table.
///
/// <para><b>Whole-row rejection.</b> There is no disabled-on-error state: a row is loadable or it is
/// refused with a typed reason an author can act on. A partially-applied atom is the silent no-op
/// this whole layer exists to remove.</para>
///
/// <para>Grammars are the ones pinned in definitions.md §1 — this is the only place they are
/// enforced, so drift shows up here rather than three modules later.</para>
/// </summary>
public static class AtomRowValidator
{
    // definitions.md §1. Compiled once: validation runs over whole seed files.
    static readonly Regex FamilyIdRe = new(@"^atom\.[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);
    static readonly Regex VariantRe = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);
    static readonly Regex AtomIdRe = new(@"^atom\.[a-z0-9-]+(\.[a-z0-9-]+)?\.t[1-9][0-9]*$", RegexOptions.Compiled);
    static readonly Regex IcdKeyRe = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>
    /// Validate one row. <paramref name="curveInput"/> resolves a curve id to the axis it reads;
    /// the store supplies one backed by <c>effect_curve</c>, which is how <b>D9</b> is enforced
    /// without Core ever touching SQL. Pass null to skip curve checks.
    /// </summary>
    public static AtomRejection Validate(AtomRow row, Func<string, CurveInput?>? curveInput = null)
    {
        if (row is null) return AtomRejection.Fail(AtomRejectionReason.BadParamValue, "null row");

        // ---- identity ------------------------------------------------------------------------
        if (!FamilyIdRe.IsMatch(row.FamilyId ?? ""))
            return Fail(AtomRejectionReason.BadParamValue, $"family_id '{row.FamilyId}' is not kebab-case with an atom. prefix");

        if (!string.IsNullOrEmpty(row.Variant) && !VariantRe.IsMatch(row.Variant))
            return Fail(AtomRejectionReason.BadParamValue, $"variant '{row.Variant}' is not kebab-case");

        if (row.Variant is null)
            return Fail(AtomRejectionReason.BadParamValue, "variant is NULL; use '' — NULL breaks the unique key");

        if (row.Tier < 1)
            return Fail(AtomRejectionReason.BadParamValue, $"tier {row.Tier} is below 1; there is no tier-0 parking spot");

        if (!AtomIdRe.IsMatch(row.AtomId ?? ""))
            return Fail(AtomRejectionReason.BadParamValue, $"atom_id '{row.AtomId}' does not match the grammar");

        // atom_id is DERIVED. Storing it is a denormalisation for lookup speed, so it must agree
        // with the columns it was derived from, or two sources of truth exist.
        var derived = row.DerivedId();
        if (!string.Equals(row.AtomId, derived, StringComparison.Ordinal))
            return Fail(AtomRejectionReason.IdMismatch, $"atom_id '{row.AtomId}' but columns imply '{derived}'");

        if (!string.IsNullOrWhiteSpace(row.IcdKey) && !IcdKeyRe.IsMatch(row.IcdKey!))
            return Fail(AtomRejectionReason.BadParamValue, $"icd_key '{row.IcdKey}' is not kebab-case");

        // ---- kind and params -------------------------------------------------------------------
        var kind = AtomKindRegistry.Get(row.KindId);
        if (kind is null)
            return Fail(AtomRejectionReason.UnknownKind, row.KindId ?? "(null)");

        if (!TryParseObject(row.ParamsJson, out var pars, out var parsErr))
            return Fail(AtomRejectionReason.BadParamValue, $"params_json: {parsErr}");

        var paramCheck = kind.Params.Validate(pars);
        if (!paramCheck.IsOk) return paramCheck;

        // E2 wiring: a param the kind declares as a Value carries a value spec, and a spec whose
        // range runs backwards or whose roll policy does not exist must never reach the table.
        foreach (var def in kind.Params.Defs)
        {
            if (def.Kind != ParamKind.Value) continue;
            if (!pars.TryGetValue(def.Name, out var raw) || raw is not JsonElement el) continue;

            var specCheck = AtomJson.TryReadValueSpec(el, out var spec);
            if (!specCheck.IsOk) return Fail(specCheck.Reason, $"{def.Name}: {specCheck.Detail}");

            var curveCheck = ValidateCurve(def.Name, spec, curveInput);
            if (!curveCheck.IsOk) return curveCheck;
        }

        // ---- when ------------------------------------------------------------------------------
        if (!TryParseObject(row.WhenJson, out var when, out var whenErr))
            return Fail(AtomRejectionReason.BadParamValue, $"when_json: {whenErr}");

        var whenCheck = ValidateWhen(kind, when);
        if (!whenCheck.IsOk) return whenCheck;

        // E3 wiring: the predicate tree is validated HERE, at load. A tree nine levels deep or over
        // an unknown leaf is a row that must not land, not a runtime surprise to survive.
        if (when.TryGetValue("predicate", out var predRaw) && predRaw is JsonElement predEl)
        {
            var read = AtomJson.TryReadPredicate(predEl, out var tree);
            if (!read.IsOk) return Fail(read.Reason, read.Detail);

            var compiled = PredicateCompiler.TryCompile(tree, statusBit: null, out _);
            if (!compiled.IsOk) return Fail(compiled.Reason, compiled.Detail);
        }

        if (!TryParseObject(row.TagsJson, out _, out var tagsErr))
            return Fail(AtomRejectionReason.BadParamValue, $"tags_json: {tagsErr}");

        // ---- power ------------------------------------------------------------------------------
        // A stored override without a note is a magic number nobody can defend later.
        if (!string.IsNullOrWhiteSpace(row.PowerOverrideJson) && string.IsNullOrWhiteSpace(row.PowerNote))
            return Fail(AtomRejectionReason.MissingPowerNote, row.AtomId ?? "(no id)");

        return AtomRejection.Ok;

        AtomRejection Fail(AtomRejectionReason reason, string detail) =>
            AtomRejection.Fail(reason, $"{row.AtomId}: {detail}");
    }

    /// <summary>
    /// D9: an <c>OnApply</c> value scaled by a <b>level</b> curve cannot be rolled on the injector —
    /// it would need the curve's points and the actor's level, i.e. a content lookup at trigger time,
    /// which E19 forbids outright. E7 bakes pre-multiplied bounds instead, so this shape is refused
    /// at authoring rather than discovered when the compile/run split leaks.
    /// </summary>
    static AtomRejection ValidateCurve(
        string paramName, ValueSpec spec, Func<string, CurveInput?>? curveInput)
    {
        if (string.IsNullOrWhiteSpace(spec.CurveId) || curveInput is null) return AtomRejection.Ok;

        var input = curveInput(spec.CurveId!);
        if (input is null)
            return AtomRejection.Fail(AtomRejectionReason.BadCurve,
                $"{paramName}: unknown curve '{spec.CurveId}'");

        if (spec.Roll == RollPolicy.OnApply && input == CurveInput.Level)
            return AtomRejection.Fail(AtomRejectionReason.BadValueSpec,
                $"{paramName}: a level curve cannot scale an onApply value — the injector would need " +
                "the curve rows and the actor level per hit (D9). Use onInstantiate, or let E7 bake bounds.");

        return AtomRejection.Ok;
    }

    static AtomRejection ValidateWhen(AtomKind kind, IReadOnlyDictionary<string, object?> when)
    {
        // The trigger key is simply ABSENT for permanent modifiers — there is no "None" trigger name,
        // which is what keeps the closed count at 7 (definitions §14.2).
        var hasTrigger = when.TryGetValue("trigger", out var raw) && raw is not null;
        var trigger = hasTrigger ? raw!.ToString() : null;

        if (hasTrigger)
        {
            var t = AtomKindRegistry.ValidateTrigger(kind.KindId, trigger);
            if (!t.IsOk) return t;
        }
        else if (kind.Triggers.Count > 0)
        {
            // The mirror case: a kind that fires on events must say which.
            return AtomRejection.Fail(AtomRejectionReason.MissingParam,
                $"{kind.KindId} requires a trigger");
        }

        if (when.TryGetValue("chance", out var chance) && chance is not null)
        {
            if (!TryInt(chance, out var permille) || permille is < 0 or > 1000)
                return AtomRejection.Fail(AtomRejectionReason.BadParamValue,
                    $"chance must be per-mille in [0, 1000], got '{chance}'");
        }

        if (when.TryGetValue("icd_ms", out var icd) && icd is not null)
        {
            if (!TryInt(icd, out var ms) || ms < 0)
                return AtomRejection.Fail(AtomRejectionReason.BadParamValue,
                    $"icd_ms must be a non-negative integer, got '{icd}'");
        }

        return AtomRejection.Ok;
    }

    static bool TryInt(object? v, out int result)
    {
        switch (v)
        {
            case int i: result = i; return true;
            case long l when l is >= int.MinValue and <= int.MaxValue: result = (int)l; return true;
            case JsonElement { ValueKind: JsonValueKind.Number } je: return je.TryGetInt32(out result);
            default: result = 0; return false;
        }
    }

    static bool TryParseObject(
        string? json, out IReadOnlyDictionary<string, object?> map, out string error)
    {
        map = new Dictionary<string, object?>();
        error = "";

        if (string.IsNullOrWhiteSpace(json)) return true; // absent is legal; "{}" and null agree

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = $"expected an object, got {doc.RootElement.ValueKind}";
                return false;
            }

            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
                d[prop.Name] = prop.Value.Clone();
            map = d;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
