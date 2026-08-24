using System.Text;
using System.Text.Json;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>Where an instance came from. Recorded so a support question has an answer.</summary>
public enum InstanceOrigin
{
    Drop = 0,
    Craft,
    Grant,
    Migration,
}

/// <summary>One atom inside an instance, with its <c>OnInstantiate</c> rolls already frozen.</summary>
/// <param name="ValuesJson">
/// Frozen results. <c>Fixed</c> values are copied verbatim; <c>OnApply</c> values are
/// <b>left unresolved</b> — they belong to the hit, not to the item.
/// </param>
/// <param name="PowerJson">Nullable — E9 owns power, lands later, and backfills.</param>
public sealed record InstanceAtomRow(int Seq, string AtomId, string ValuesJson, string? PowerJson = null);

/// <summary>A container template turned into a specific owned thing.</summary>
public sealed record InstanceRow
{
    public string InstanceId { get; init; } = "";
    public string ContainerId { get; init; } = "";

    /// <summary>Replays the drop exactly. The whole reason an instance is reproducible.</summary>
    public long RollSeed { get; init; }

    /// <summary>
    /// The catalog the rolls were taken against. Reproducibility is claimed over
    /// <c>(container_id, catalog_revision, roll_seed)</c> — without this column there is nothing to
    /// compare, and a content edit silently changes what an already-owned item means.
    /// </summary>
    public long CatalogRevision { get; init; }

    public string CreatedUtc { get; init; } = "";
    public InstanceOrigin Origin { get; init; } = InstanceOrigin.Drop;
    public IReadOnlyList<InstanceAtomRow> Atoms { get; init; } = Array.Empty<InstanceAtomRow>();

    /// <summary>
    /// content-scale (T3.4): the Θ_content this instance rolled at, and the exact per-mille ratio
    /// applied to every <c>OnInstantiate</c>/<c>Fixed</c> magnitude in <see cref="Atoms"/> — recorded
    /// so any value can be divided back to its relative roll and audited (spec-content-scale.md
    /// §2.2). 1000 = ×1.000, the pin (Θ_content = 20).
    /// </summary>
    public int ThetaContent { get; init; }
    public long ContentScaleMilli { get; init; }

    /// <summary>
    /// The comparison used for reproducibility, deliberately <b>excluding</b> `instance_id` and
    /// `created_utc`, which are generated. Without that exclusion the test could never pass.
    /// <see cref="ThetaContent"/> is included on purpose — a different drop depth is a different
    /// instance for reproducibility purposes, even if the relative roll happens to match.
    /// </summary>
    public string ContentFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append(ContainerId).Append('\n');
        sb.Append(ThetaContent).Append('|').Append(ContentScaleMilli).Append('\n');
        foreach (var a in Atoms)
            sb.Append(a.Seq).Append('|').Append(a.AtomId).Append('|')
              .Append(a.ValuesJson).Append('|').Append(a.PowerJson ?? "").Append('\n');
        return sb.ToString();
    }
}

/// <summary>
/// Roll moment 2: draw the pool, append the fixed core, and freeze every <c>OnInstantiate</c> value.
///
/// <para>Moment 3 (bind) deliberately rolls nothing — if a value would change at equip, it is
/// <c>OnApply</c>, and this never touches it.</para>
/// </summary>
public static class Instantiator
{
    /// <summary>
    /// Instantiate. Re-running with the same <c>(container, catalogRevision, rollSeed, thetaContent)</c>
    /// reproduces the instance byte-identically over <see cref="InstanceRow.ContentFingerprint"/>.
    ///
    /// <para><paramref name="thetaContent"/> and <paramref name="tuning"/> are required, not
    /// defaulted (spec-content-scale.md §2.4: "Absence is a rejection, not a default of 1.0" — a
    /// silent 1.0 is a drop that quietly ignores depth). Making them non-optional parameters is that
    /// rejection: there is no call this compiles without a real Θ_content and a loaded tuning, so
    /// there is no runtime "missing" state left to reject, and no need to grow
    /// <see cref="AtomRejectionReason"/>'s closed, guard-tested 33-member list for a case that C#'s
    /// own type system already forecloses. Every caller that has no depth to supply cannot reach this
    /// method at all — that decision belongs one layer up, at whoever resolves Θ_content from a wave,
    /// expedition or world context.</para>
    /// </summary>
    public static AtomRejection TryInstantiate(
        ContainerRow container,
        Func<string, AtomRow?> lookupAtom,
        long rollSeed,
        int thetaContent,
        FusionRpg.Core.Power.PowerTuning tuning,
        out InstanceRow? instance,
        InstanceOrigin origin = InstanceOrigin.Drop,
        long catalogRevision = 0)
    {
        instance = null;

        var check = ContainerValidator.Validate(container, lookupAtom);
        if (!check.IsOk) return check;

        // Computed once (spec §2.2: "One call site") — never re-derived per atom, so every value in
        // this instance scales by the exact same ratio.
        var contentScaleMilli = FusionRpg.Core.Power.ContentScale.Milli(thetaContent, tuning);

        var rows = new List<InstanceAtomRow>();

        // The fixed core keeps its authored seq. Determinism comes first, literally: a trait always
        // contains what it says, and the drawn half is appended after it.
        foreach (var entry in container.Atoms.OrderBy(a => a.Seq))
        {
            var atom = lookupAtom(entry.AtomId)!;
            var freeze = Freeze(atom, entry.OverridesJson, rollSeed, entry.Seq, contentScaleMilli, out var valuesJson);
            if (!freeze.IsOk) return freeze;

            rows.Add(new InstanceAtomRow(entry.Seq, entry.AtomId, valuesJson));
        }

        var drawn = Draw(container, lookupAtom, rollSeed);
        var nextSeq = rows.Count == 0 ? 1 : rows.Max(r => r.Seq) + 1;

        foreach (var atomId in drawn)
        {
            var atom = lookupAtom(atomId)!;
            var freeze = Freeze(atom, null, rollSeed, nextSeq, contentScaleMilli, out var valuesJson);
            if (!freeze.IsOk) return freeze;

            rows.Add(new InstanceAtomRow(nextSeq, atomId, valuesJson));
            nextSeq++;
        }

        instance = new InstanceRow
        {
            InstanceId = "",       // generated by the store; excluded from the fingerprint
            ContainerId = container.ContainerId,
            RollSeed = rollSeed,
            CatalogRevision = catalogRevision,
            Origin = origin,
            Atoms = rows,
            ThetaContent = thetaContent,
            ContentScaleMilli = contentScaleMilli,
        };
        return AtomRejection.Ok;
    }

    /// <summary>
    /// Weighted draw, <b>at most one atom per group</b>, <c>pool_rolls</c> times. Zero-weight rows are
    /// present in the pool and never drawn — validation has already proven enough groups are drawable.
    /// </summary>
    static List<string> Draw(ContainerRow container, Func<string, AtomRow?> lookupAtom, long rollSeed)
    {
        var picked = new List<string>();
        if (container.PoolRolls <= 0 || container.Pool.Count == 0) return picked;

        // A stream named for the container so two containers rolled from one seed do not share a
        // sequence, and the same container always replays identically.
        var rng = new AtomRandom(unchecked((ulong)rollSeed), AtomStreams.Pool + "." + container.ContainerId);

        var remaining = container.Pool
            .Where(p => p.Weight > 0)
            .Select(p => (Row: p, Group: GroupOf(p, lookupAtom(p.AtomId)!)))
            .ToList();

        for (var roll = 0; roll < container.PoolRolls && remaining.Count > 0; roll++)
        {
            var total = remaining.Sum(c => c.Row.Weight);
            var target = rng.NextInclusive(1, total);

            var running = 0;
            var chosen = remaining[^1];
            foreach (var candidate in remaining)
            {
                running += candidate.Row.Weight;
                if (running < target) continue;
                chosen = candidate;
                break;
            }

            picked.Add(chosen.Row.AtomId);
            // One per group: drop the whole group, not just the row, or a second tier of the same
            // variant could still come up.
            remaining.RemoveAll(c => string.Equals(c.Group, chosen.Group, StringComparison.Ordinal));
        }

        return picked;
    }

    static string GroupOf(ContainerPoolRow row, AtomRow atom) =>
        string.IsNullOrWhiteSpace(row.Group) ? atom.FamilyId + "|" + atom.Variant : row.Group!;

    /// <summary>
    /// Resolve every <c>OnInstantiate</c> value spec; copy <c>Fixed</c> ones; leave <c>OnApply</c>
    /// alone. Non-value params pass through untouched.
    ///
    /// <para>content-scale (T3.4) multiplies both <c>OnInstantiate</c> and <c>Fixed</c> results —
    /// both are magnitudes that end up on the item, so both are worth more when the item drops
    /// deeper. <c>OnApply</c> is exempt for an unrelated, pre-existing reason (it belongs to the hit,
    /// not the item) and stays exempt here too — content-scale never touches it.</para>
    /// </summary>
    static AtomRejection Freeze(
        AtomRow atom, string? overridesJson, long rollSeed, int seq, long contentScaleMilli, out string valuesJson)
    {
        valuesJson = "{}";

        var kind = AtomKindRegistry.Get(atom.KindId);
        if (kind is null) return AtomRejection.Fail(AtomRejectionReason.UnknownKind, atom.KindId);

        var merged = ReadObject(atom.ParamsJson);
        foreach (var (k, v) in ReadObject(overridesJson)) merged[k] = v;

        // Per (atom, seq) so two copies of one atom in a container do not freeze to the same number.
        var rng = new AtomRandom(unchecked((ulong)rollSeed),
            AtomStreams.Pool + ".freeze." + atom.AtomId + "." + seq);

        var frozen = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (key, raw) in merged)
        {
            var def = kind.Params.Defs.FirstOrDefault(d =>
                string.Equals(d.Name, key, StringComparison.OrdinalIgnoreCase));

            if (def is null || def.Kind != ParamKind.Value)
            {
                frozen[key] = raw;
                continue;
            }

            var read = AtomJson.TryReadValueSpec(raw, out var spec);
            if (!read.IsOk) return AtomRejection.Fail(read.Reason, $"{atom.AtomId}.{key}: {read.Detail}");

            frozen[key] = spec.Roll switch
            {
                RollPolicy.OnInstantiate => FusionRpg.Core.Power.ContentScale.Apply(spec.Resolve(rng), contentScaleMilli),
                RollPolicy.Fixed => FusionRpg.Core.Power.ContentScale.Apply(spec.Min, contentScaleMilli),
                // Left as authored: an OnApply range belongs to the hit, not the item — content-scale
                // never touches it, same reasoning that already exempted it from freezing at all.
                _ => raw,
            };
        }

        valuesJson = JsonSerializer.Serialize(frozen, JsonOpts);
        return AtomRejection.Ok;
    }

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    static Dictionary<string, JsonElement> ReadObject(string? json)
    {
        var d = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return d;

        try
        {
            using var doc = JsonDocument.Parse(json!);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return d;
            foreach (var p in doc.RootElement.EnumerateObject()) d[p.Name] = p.Value.Clone();
        }
        catch (JsonException) { /* already refused upstream */ }

        return d;
    }
}
