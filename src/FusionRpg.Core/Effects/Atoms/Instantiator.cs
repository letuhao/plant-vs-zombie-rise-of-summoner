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
        Func<string, AffixRow?> lookupAffix,
        long rollSeed,
        int thetaContent,
        FusionRpg.Core.Power.PowerTuning tuning,
        out InstanceRow? instance,
        InstanceOrigin origin = InstanceOrigin.Drop,
        long catalogRevision = 0)
    {
        instance = null;

        var check = ContainerValidator.Validate(container, lookupAtom, lookupAffix);
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

        var drawn = Draw(container, lookupAtom, lookupAffix, rollSeed);
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
    /// Weighted draw, <b>at most one affix per group</b>, <c>prefix_rolls</c> + <c>suffix_rolls</c>
    /// times across two separately-budgeted draws, then each drawn affix expands to its concrete
    /// atom id(s) — returned flat, matching every existing caller's atom-id-list contract.
    ///
    /// <para><b>Public since T31 (action program, A13):</b> "the generator already exists" — a
    /// container's own weighted pool-plus-group-exclusion roll is exactly what the action-seeding
    /// runtime generator needs for "which atoms", and it must not be reinvented. Visibility widened,
    /// no behavior changed at the time.</para>
    ///
    /// <para><b>T3.1 (affix-schema):</b> the pool now draws affix ids, not bare atom ids
    /// (`definitions.md` §4a). This method still returns atom ids — expansion happens inside it — so
    /// <c>ActionSeeder</c> and every other existing caller are unaffected. <b>Only single-concrete-
    /// ref affixes expand here.</b> A multi-ref or slot-bearing bundle needs the full five-step
    /// resolver (`resolution-order`, module 2, not yet built) to correlate its refs and resolve its
    /// slots — drawing one throws rather than silently returning a partial or wrong expansion.</para>
    ///
    /// <para><b>T3.2 (prefix/suffix split):</b> the single <c>pool_rolls</c> budget is now two —
    /// <c>Prefix</c>-eligible rows (class <see cref="AffixClass.Prefix"/> or <see cref="AffixClass.Mixed"/>)
    /// draw against <c>prefix_rolls</c>; <c>Suffix</c>-eligible rows (<see cref="AffixClass.Suffix"/> or
    /// <see cref="AffixClass.Mixed"/>) draw against <c>suffix_rolls</c> — each its own independent
    /// weighted draw with its own group-exclusion and its own named RNG stream, so one budget's rolls
    /// never shift the other's. A <see cref="AffixClass.Mixed"/> affix is eligible in both draws, and
    /// today's two-independent-draws model can therefore pick it in one, both, or neither — the exact
    /// "one draw consumes both budgets simultaneously" semantics A1 describes belongs to the full
    /// resolver (module 2, not yet built); this is an interim, honestly-documented simplification, not
    /// the final resolution order.</para>
    /// </summary>
    public static List<string> Draw(
        ContainerRow container, Func<string, AtomRow?> lookupAtom, Func<string, AffixRow?> lookupAffix,
        long rollSeed)
    {
        var picked = new List<string>();
        if (container.Pool.Count == 0) return picked;

        if (container.PrefixRolls > 0)
            DrawBudget(container, lookupAtom, lookupAffix, rollSeed, "prefix", container.PrefixRolls,
                a => a.Class is AffixClass.Prefix or AffixClass.Mixed, picked);
        if (container.SuffixRolls > 0)
            DrawBudget(container, lookupAtom, lookupAffix, rollSeed, "suffix", container.SuffixRolls,
                a => a.Class is AffixClass.Suffix or AffixClass.Mixed, picked);

        return picked;
    }

    static void DrawBudget(
        ContainerRow container, Func<string, AtomRow?> lookupAtom, Func<string, AffixRow?> lookupAffix,
        long rollSeed, string budgetName, int rolls, Func<AffixRow, bool> eligible, List<string> picked)
    {
        // A stream named for the container AND the budget so the prefix draw and the suffix draw
        // never share a sequence, and the same container always replays identically.
        var rng = new AtomRandom(unchecked((ulong)rollSeed),
            AtomStreams.Pool + "." + budgetName + "." + container.ContainerId);

        var remaining = container.Pool
            .Where(p => p.Weight > 0 && eligible(lookupAffix(p.AffixId)!))
            .Select(p => (Row: p, Group: GroupOf(p, lookupAffix(p.AffixId)!, lookupAtom)))
            .ToList();

        for (var roll = 0; roll < rolls && remaining.Count > 0; roll++)
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

            picked.AddRange(ExpandSingleRefAffix(chosen.Row.AffixId, lookupAffix));
            // One per group: drop the whole group, not just the row, or a second tier of the same
            // variant could still come up.
            remaining.RemoveAll(c => string.Equals(c.Group, chosen.Group, StringComparison.Ordinal));
        }
    }

    /// <summary>The T3.1-scoped expansion: a bundle of exactly one concrete ref resolves to that one
    /// atom id. Anything else — a slot ref, or more than one ref — is not expressible without the
    /// resolver `resolution-order` (module 2) is building, and this throws rather than guessing.</summary>
    static IEnumerable<string> ExpandSingleRefAffix(string affixId, Func<string, AffixRow?> lookupAffix)
    {
        var affix = lookupAffix(affixId)
            ?? throw new InvalidOperationException($"drawn affix '{affixId}' is not in the catalog — validation should have caught this");
        if (affix.Refs.Count != 1 || affix.Refs[0].AtomId is null)
            throw new NotSupportedException(
                $"affix '{affixId}' is a multi-ref or slot-bearing bundle — expanding it needs the " +
                "resolution-order resolver (module 2), not yet built. Draw() only expands single-ref affixes.");
        yield return affix.Refs[0].AtomId!;
    }

    /// <summary>Group for a drawn affix: a single-ref affix defaults to that atom's own
    /// <c>(family_id, variant)</c>, matching the pre-affix default exactly; anything else must have
    /// declared an explicit <see cref="ContainerPoolRow.Group"/> — <see cref="ContainerValidator"/>
    /// already refuses to load a container where one didn't.</summary>
    static string GroupOf(ContainerPoolRow row, AffixRow affix, Func<string, AtomRow?> lookupAtom)
    {
        if (!string.IsNullOrWhiteSpace(row.Group)) return row.Group!;
        var atom = lookupAtom(affix.Refs[0].AtomId!)!;
        return atom.FamilyId + "|" + atom.Variant;
    }

    /// <summary>
    /// Resolve every <c>OnInstantiate</c> value spec; copy <c>Fixed</c> ones; leave <c>OnApply</c>
    /// alone. Non-value params pass through untouched.
    ///
    /// <para>content-scale (T3.4) multiplies both <c>OnInstantiate</c> and <c>Fixed</c> results —
    /// both are magnitudes that end up on the item, so both are worth more when the item drops
    /// deeper. <c>OnApply</c> is exempt for an unrelated, pre-existing reason (it belongs to the hit,
    /// not the item) and stays exempt here too — content-scale never touches it.</para>
    ///
    /// <para><c>internal</c> (T3.6, `instance-producer`): <see cref="InstanceProducer"/> reuses this
    /// to freeze a container's FIXED core the same way <see cref="TryInstantiate"/> already does —
    /// <see cref="Resolver"/> owns the pool half (its own five-step order), this owns the core half,
    /// and both must freeze identically or the same instance would carry two different rounding
    /// rules depending on which half an atom came from.</para>
    /// </summary>
    internal static AtomRejection Freeze(
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
