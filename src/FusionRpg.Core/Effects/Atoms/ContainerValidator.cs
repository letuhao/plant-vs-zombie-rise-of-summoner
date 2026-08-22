using System.Text.Json;
using System.Text.RegularExpressions;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// Judges one <see cref="ContainerRow"/> before it reaches the tables.
///
/// <para>Same law as E4 — a bad container is rejected <b>whole</b>, with its id and reason. The rule
/// carrying the most weight is <c>pool_rolls ≤ distinct <b>drawable</b> groups</c>: a pool that
/// cannot satisfy the one-per-group rule under-fills the instance silently, which is exactly the
/// failure mode this program exists to remove.</para>
/// </summary>
public static class ContainerValidator
{
    static readonly Regex ContainerIdRe =
        new(@"^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Validate. <paramref name="lookupAtom"/> resolves an atom id against the loaded catalog —
    /// supplied by the store, so this stays free of I/O.
    /// </summary>
    public static AtomRejection Validate(ContainerRow c, Func<string, AtomRow?> lookupAtom)
    {
        if (c is null) return AtomRejection.Fail(AtomRejectionReason.BadParamValue, "null container");

        // ---- identity --------------------------------------------------------------------------
        if (!ContainerIdRe.IsMatch(c.ContainerId ?? ""))
            return Fail(AtomRejectionReason.BadParamValue,
                $"container_id '{c.ContainerId}' does not match the grammar");

        var prefix = ContainerRow.PrefixOf(c.Kind);
        if (!c.ContainerId!.StartsWith(prefix + ".", StringComparison.Ordinal))
            return Fail(AtomRejectionReason.BadParamValue,
                $"container_id '{c.ContainerId}' does not carry the '{prefix}.' prefix its kind requires");

        if (c.MinTier is { } lo && c.MaxTier is { } hi && lo > hi)
            return Fail(AtomRejectionReason.BadParamValue, $"tier window [{lo}, {hi}] is inverted");

        // ---- the fixed core ---------------------------------------------------------------------
        var seqs = new HashSet<int>();
        var coreAtoms = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in c.Atoms)
        {
            if (!seqs.Add(entry.Seq))
                return Fail(AtomRejectionReason.DuplicateSeq, $"seq {entry.Seq} appears twice");

            var atom = lookupAtom(entry.AtomId);
            if (atom is null)
                return Fail(AtomRejectionReason.UnknownAtom, entry.AtomId);

            coreAtoms.Add(entry.AtomId);

            var ov = ValidateOverrides(atom, entry.OverridesJson);
            if (!ov.IsOk) return Fail(ov.Reason, $"{entry.AtomId}: {ov.Detail}");
        }

        // ---- the pool ----------------------------------------------------------------------------
        if (c.PoolRolls < 0)
            return Fail(AtomRejectionReason.BadParamValue, $"pool_rolls {c.PoolRolls} is negative");

        if (c.PoolRolls > 0 && c.Pool.Count == 0)
            return Fail(AtomRejectionReason.UnsatisfiablePool,
                $"pool_rolls is {c.PoolRolls} but the pool is empty");

        // Group -> whether ANY row in it can actually be drawn. A group whose every row is weight 0
        // exists in the table and is unreachable in a draw; counting it is how a container passes
        // validation and then hands back fewer atoms than it promised.
        var drawable = new Dictionary<string, bool>(StringComparer.Ordinal);
        var anyDrawable = false;

        foreach (var row in c.Pool)
        {
            if (row.Weight < 0)
                return Fail(AtomRejectionReason.BadParamValue,
                    $"{row.AtomId}: weight {row.Weight} is negative — rejected, never clamped");

            var atom = lookupAtom(row.AtomId);
            if (atom is null)
                return Fail(AtomRejectionReason.UnknownAtom, row.AtomId);

            if (coreAtoms.Contains(row.AtomId))
                return Fail(AtomRejectionReason.DuplicateAtomInContainer,
                    $"{row.AtomId} is in both the fixed core and the pool");

            // The window governs what the POOL may offer; a fixed core says what the thing is.
            if (c.MinTier is { } min && atom.Tier < min)
                return Fail(AtomRejectionReason.TierOutOfWindow,
                    $"{row.AtomId} is tier {atom.Tier}, below the window minimum {min}");
            if (c.MaxTier is { } max && atom.Tier > max)
                return Fail(AtomRejectionReason.TierOutOfWindow,
                    $"{row.AtomId} is tier {atom.Tier}, above the window maximum {max}");

            var group = GroupOf(row, atom);
            drawable[group] = drawable.TryGetValue(group, out var had) && had || row.Weight > 0;
            anyDrawable |= row.Weight > 0;
        }

        if (c.Pool.Count > 0 && !anyDrawable)
            return Fail(AtomRejectionReason.UnsatisfiablePool,
                "every pool row has weight 0 — the draw would return nothing");

        var drawableGroups = drawable.Values.Count(v => v);
        if (c.PoolRolls > drawableGroups)
            return Fail(AtomRejectionReason.PoolRollsExceedGroups,
                $"pool_rolls {c.PoolRolls} exceeds {drawableGroups} drawable group(s) — " +
                "one atom per group per draw cannot be satisfied");

        return AtomRejection.Ok;

        AtomRejection Fail(AtomRejectionReason reason, string detail) =>
            AtomRejection.Fail(reason, $"{c.ContainerId}: {detail}");
    }

    /// <summary>
    /// Default group is <c>(family_id, variant)</c>, not <c>family_id</c>: a container may roll fire
    /// power and ice power — two variants of one family, normal itemisation — while never rolling two
    /// tiers of the same variant.
    /// </summary>
    static string GroupOf(ContainerPoolRow row, AtomRow atom) =>
        string.IsNullOrWhiteSpace(row.Group)
            // Separated, not concatenated: family "atom.a" + variant "bc" and family "atom.ab" + variant
            // "c" would otherwise both key as "atom.abc", silently merging two families into one
            // group. '|' cannot occur in either — both are kebab-case per definitions §1.
            ? atom.FamilyId + "|" + atom.Variant
            : row.Group!;

    /// <summary>
    /// An override replaces a value spec on the referenced atom, so it obeys the same schema and the
    /// same E2 validation as the original. It may tighten a range; it may not invent a param the kind
    /// does not declare, and it may not change what the atom <i>is</i>.
    /// </summary>
    static AtomRejection ValidateOverrides(AtomRow atom, string? overridesJson)
    {
        if (string.IsNullOrWhiteSpace(overridesJson)) return AtomRejection.Ok;

        var kind = AtomKindRegistry.Get(atom.KindId);
        if (kind is null) return AtomRejection.Fail(AtomRejectionReason.UnknownKind, atom.KindId);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(overridesJson!); }
        catch (JsonException ex)
        {
            return AtomRejection.Fail(AtomRejectionReason.BadParamValue, $"overrides_json: {ex.Message}");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return AtomRejection.Fail(AtomRejectionReason.BadParamValue,
                    "overrides_json must be an object");

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "kind_id", StringComparison.OrdinalIgnoreCase))
                    return AtomRejection.Fail(AtomRejectionReason.OverrideChangesKind,
                        "an override tunes a value; it does not rewrite what the atom is");

                var def = kind.Params.Defs.FirstOrDefault(d =>
                    string.Equals(d.Name, prop.Name, StringComparison.OrdinalIgnoreCase));

                if (def is null)
                    return AtomRejection.Fail(AtomRejectionReason.UnknownParam,
                        $"'{prop.Name}' is not declared by {atom.KindId}");

                if (def.Kind != ParamKind.Value) continue;

                var spec = AtomJson.TryReadValueSpec(prop.Value, out _);
                if (!spec.IsOk)
                    return AtomRejection.Fail(spec.Reason, $"{prop.Name}: {spec.Detail}");
            }
        }

        return AtomRejection.Ok;
    }
}
