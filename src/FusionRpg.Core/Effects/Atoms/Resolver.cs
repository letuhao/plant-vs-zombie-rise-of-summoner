using System.Text.Json;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>One atom resolved through all five steps — a slot substituted (or not), a tier chosen
/// (or already fixed by a concrete ref), and its magnitudes rolled. <b>Not</b> an
/// <see cref="InstanceAtomRow"/>: instance persistence is `instance-producer`'s job (module 4, T3.6),
/// not this resolver's — <see cref="Resolver.Resolve"/>'s own signature carries no `thetaContent`/
/// `tuning`. <see cref="Resolver.Resolve"/>'s optional <c>contentScaleMilli</c> parameter (T3.6, added
/// without breaking any T3.3 call site — default 1000 = ×1.000, unchanged) lets a caller that HAS
/// already derived the ratio apply it inline at roll time, the same one-call-site discipline
/// `Instantiator.Freeze` already established, rather than re-parsing this record's JSON after the
/// fact to scale it a second time.</summary>
public sealed record ResolvedAtom(string AtomId, string ValuesJson);

/// <summary>The pool half of one container instantiation, resolved through the five-step order.</summary>
public sealed record ResolvedDraw(IReadOnlyList<ResolvedAtom> Atoms);

/// <summary>
/// `resolution-order` (T3.3, `spec-resolution-order.md`): the affix-aware replacement for
/// <see cref="Instantiator.Draw"/>'s single-stream, atom-only draw. Implements the five-step order
/// `definitions.md:204-212` makes normative — <c>slots → affixes → atoms → tiers → values</c> — each
/// step on its <b>own</b> named RNG stream, so a future sixth step never shifts an existing layer's
/// draws (`definitions.md:231-236`).
///
/// <para><see cref="Instantiator.Draw"/> is <b>not deleted</b> — it stays the single-concrete-ref-only
/// entry point every existing caller (<c>ActionSeeder</c>, etc.) already depends on. This is a
/// parallel, affix-aware entry point; wiring it into <c>Instantiator.TryInstantiate</c>/
/// <c>InstanceRow</c> is `instance-producer`'s job (module 4, T3.6), not this module's.</para>
/// </summary>
public static class Resolver
{
    /// <summary>
    /// Resolve one container's pool half. <paramref name="domainMembers"/> mirrors
    /// <see cref="AffixValidator"/>'s own shape — the caller owns the real vocabularies
    /// (<c>ElementRoster</c>, etc.), this resolver stays free of I/O and of a hardcoded domain list.
    /// <paramref name="variant"/> is a resolution <b>parameter</b>, never a container — <c>null</c>
    /// for "normal"/"shiny" (both have zero resolution effect from this resolver's own perspective).
    /// </summary>
    public static ResolvedDraw Resolve(
        ContainerRow container,
        Func<string, AtomRow?> lookupAtom,
        Func<string, AffixRow?> lookupAffix,
        Func<string, IReadOnlyList<string>> domainMembers,
        long rollSeed,
        VariantShift? variant = null,
        long contentScaleMilli = 1000)
    {
        if (container.Pool.Count == 0) return new ResolvedDraw(Array.Empty<ResolvedAtom>());

        // Step 1 — slots. Resolved for EVERY affix in the WHOLE pool, drawn or not, so the number of
        // slot draws never depends on step 2's outcome — the exact independence the four-stream
        // design exists to guarantee (definitions.md:231-236).
        var slotRng = new AtomRandom(unchecked((ulong)rollSeed), "affix.slot." + container.ContainerId);
        var slots = ResolveSlots(container, lookupAffix, domainMembers, slotRng, variant);

        // Step 2 — affixes. One shared stream for both budgets (unlike Instantiator.Draw's own
        // per-budget streams, T3.2) — this module's contract names exactly four streams total, not
        // six, so the prefix and suffix draws share `affix.draw` and run in a fixed prefix-then-
        // suffix order for reproducibility.
        var affixRng = new AtomRandom(unchecked((ulong)rollSeed), "affix.draw." + container.ContainerId);
        var prefixRolls = variant?.ShiftPrefixRolls(container.PrefixRolls) ?? container.PrefixRolls;
        var suffixRolls = variant?.ShiftSuffixRolls(container.SuffixRolls) ?? container.SuffixRolls;
        var drawn = new List<string>();
        drawn.AddRange(DrawFromPool(container.Pool, lookupAffix, lookupAtom, affixRng, prefixRolls,
            a => a.Class is AffixClass.Prefix or AffixClass.Mixed));
        drawn.AddRange(DrawFromPool(container.Pool, lookupAffix, lookupAtom, affixRng, suffixRolls,
            a => a.Class is AffixClass.Suffix or AffixClass.Mixed));

        // Step 3 — atoms. Deterministic given steps 1-2's output; no RNG of its own (definitions.md
        // lists it without a stream).
        var expanded = ExpandRefs(drawn, lookupAffix, slots);

        // Step 4 — tiers. Only refs that came from a slot need one (a concrete ref's id already bakes
        // its tier in) — drawing for the others would waste a roll on a choice that was never made.
        var tierRng = new AtomRandom(unchecked((ulong)rollSeed), "affix.tier." + container.ContainerId);
        var (minTier, maxTier) = variant?.ShiftTierWindow(container.MinTier, container.MaxTier)
                                  ?? (container.MinTier, container.MaxTier);
        var tiered = ResolveTiers(expanded, minTier, maxTier, tierRng);

        // Step 5 — values. One shared stream, consumed in the tiered list's own fixed order.
        var valueRng = new AtomRandom(unchecked((ulong)rollSeed), "atom.value." + container.ContainerId);
        return RollValues(tiered, lookupAtom, valueRng, contentScaleMilli);
    }

    /// <summary>Every distinct (affix, slot name) pair across the WHOLE pool, resolved once each, in
    /// pool-then-seq order — the draw order must be stable so the same seed reproduces identically.</summary>
    static Dictionary<(string AffixId, string SlotName), string> ResolveSlots(
        ContainerRow container, Func<string, AffixRow?> lookupAffix,
        Func<string, IReadOnlyList<string>> domainMembers, AtomRandom slotRng, VariantShift? variant)
    {
        var resolved = new Dictionary<(string, string), string>();
        var corruptedRerollSpent = false;

        foreach (var poolRow in container.Pool)
        {
            var affix = lookupAffix(poolRow.AffixId);
            if (affix is null) continue; // ContainerValidator already refuses this at load

            foreach (var r in affix.Refs.Where(r => r.IsSlot).OrderBy(r => r.Seq))
            {
                var key = (poolRow.AffixId, r.SlotName!);
                if (resolved.ContainsKey(key)) continue;

                var members = domainMembers(r.SlotDomain!);
                var pick = members[slotRng.NextInclusive(0, members.Count - 1)];

                // `corrupted`: burns a SECOND draw on the first element-domain slot the resolve
                // encounters, keeping the second pick and discarding the first — a literal reroll,
                // spent at most once per resolve regardless of how many element slots exist.
                if (!corruptedRerollSpent && variant?.RerollsOneElementSlot == true
                    && string.Equals(r.SlotDomain, "element", StringComparison.Ordinal))
                {
                    pick = members[slotRng.NextInclusive(0, members.Count - 1)];
                    corruptedRerollSpent = true;
                }

                resolved[key] = pick;
            }
        }

        return resolved;
    }

    /// <summary>Weighted draw, at most one affix per group, <paramref name="rolls"/> times, restricted
    /// to affixes whose <see cref="AffixClass"/> satisfies <paramref name="eligible"/> — the same
    /// weighted-pick-with-group-exclusion shape <see cref="Instantiator.Draw"/> uses, but drawing
    /// AFFIX ids (not yet expanded) since step 3 needs the unexpanded bundle to apply resolved slots.</summary>
    static List<string> DrawFromPool(
        IReadOnlyList<ContainerPoolRow> pool, Func<string, AffixRow?> lookupAffix,
        Func<string, AtomRow?> lookupAtom, AtomRandom rng, int rolls, Func<AffixRow, bool> eligible)
    {
        var picked = new List<string>();
        if (rolls <= 0) return picked;

        var remaining = pool
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

            picked.Add(chosen.Row.AffixId);
            remaining.RemoveAll(c => string.Equals(c.Group, chosen.Group, StringComparison.Ordinal));
        }

        return picked;
    }

    /// <summary>Group for a pool row's affix — mirrors <c>Instantiator.GroupOf</c> exactly: an explicit
    /// <see cref="ContainerPoolRow.Group"/> wins; a single-concrete-ref affix defaults to that atom's
    /// own <c>(family_id, variant)</c>. <see cref="ContainerValidator"/> already refuses to load a
    /// container where a multi-ref or slot-bearing affix omits an explicit group, so the fallback path
    /// here is only ever reached for the single-concrete-ref case it was written for.</summary>
    static string GroupOf(ContainerPoolRow row, AffixRow affix, Func<string, AtomRow?> lookupAtom)
    {
        if (!string.IsNullOrWhiteSpace(row.Group)) return row.Group!;
        var atom = lookupAtom(affix.Refs[0].AtomId!)!;
        return atom.FamilyId + "|" + atom.Variant;
    }

    /// <summary>One expanded ref, mid-resolution — either already a concrete atom id (its tier is
    /// baked in), or a family/variant pair still waiting on step 4's tier pick.</summary>
    readonly record struct ExpandedRef(string? ConcreteAtomId, string? Family, string? Variant);

    static List<ExpandedRef> ExpandRefs(
        IReadOnlyList<string> drawnAffixIds, Func<string, AffixRow?> lookupAffix,
        IReadOnlyDictionary<(string AffixId, string SlotName), string> slots)
    {
        var expanded = new List<ExpandedRef>();

        foreach (var affixId in drawnAffixIds)
        {
            var affix = lookupAffix(affixId)
                ?? throw new InvalidOperationException(
                    $"drawn affix '{affixId}' is not in the catalog — validation should have caught this");

            foreach (var r in affix.Refs.OrderBy(r => r.Seq))
            {
                if (!r.IsSlot)
                {
                    expanded.Add(new ExpandedRef(ConcreteAtomId: r.AtomId, Family: null, Variant: null));
                    continue;
                }

                var member = slots[(affixId, r.SlotName!)];
                var family = SubstitutePatternFamily(r.SlotAtomPattern!, r.SlotName!);
                expanded.Add(new ExpandedRef(ConcreteAtomId: null, Family: family, Variant: member));
            }
        }

        return expanded;
    }

    /// <summary>Mirrors <c>AffixValidator.SubstitutePattern</c>'s own family-extraction exactly — kept
    /// local rather than widening that method's visibility, since this is the only piece of it this
    /// module needs (the same "kept local" precedent T3.1's <c>AffixClassOfAtom</c> set).</summary>
    static string SubstitutePatternFamily(string pattern, string slotName)
    {
        var placeholder = "$" + slotName;
        var idx = pattern.IndexOf(placeholder, StringComparison.Ordinal);
        return pattern[..idx].TrimEnd('.');
    }

    static List<string> ResolveTiers(
        IReadOnlyList<ExpandedRef> expanded, int? minTier, int? maxTier, AtomRandom tierRng)
    {
        var lo = minTier ?? 1;
        var hi = maxTier ?? 1;

        var atomIds = new List<string>(expanded.Count);
        foreach (var e in expanded)
        {
            if (e.ConcreteAtomId is not null)
            {
                atomIds.Add(e.ConcreteAtomId);
                continue;
            }

            var tier = tierRng.NextInclusive(lo, hi);
            atomIds.Add(AtomRow.DeriveId(e.Family!, e.Variant!, tier));
        }

        return atomIds;
    }

    /// <summary>
    /// Resolves every <c>OnInstantiate</c>/<c>Fixed</c> value spec; leaves <c>OnApply</c> alone —
    /// the same three-roll-moment split <see cref="Instantiator.Freeze"/> uses, scaled by
    /// <paramref name="contentScaleMilli"/> the same way (default 1000 = ×1.000, i.e. unscaled, for
    /// every T3.3 call site that predates T3.6 and never supplies one).
    /// </summary>
    static ResolvedDraw RollValues(
        IReadOnlyList<string> atomIds, Func<string, AtomRow?> lookupAtom, AtomRandom valueRng,
        long contentScaleMilli)
    {
        var rows = new List<ResolvedAtom>(atomIds.Count);

        foreach (var atomId in atomIds)
        {
            var atom = lookupAtom(atomId)
                ?? throw new InvalidOperationException(
                    $"resolved atom '{atomId}' is not in the catalog — validation should have caught this");

            var kind = AtomKindRegistry.Get(atom.KindId)
                ?? throw new InvalidOperationException($"'{atom.KindId}' is not a known kind");

            var frozen = new Dictionary<string, object?>(StringComparer.Ordinal);
            using var doc = JsonDocument.Parse(atom.ParamsJson);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var def = kind.Params.Defs.FirstOrDefault(d =>
                    string.Equals(d.Name, prop.Name, StringComparison.OrdinalIgnoreCase));

                if (def is null || def.Kind != ParamKind.Value)
                {
                    frozen[prop.Name] = prop.Value.Clone();
                    continue;
                }

                var read = AtomJson.TryReadValueSpec(prop.Value, out var spec);
                if (!read.IsOk)
                    throw new InvalidOperationException(
                        $"{atomId}.{prop.Name}: {read.Detail} — validation should have caught this");

                frozen[prop.Name] = spec.Roll switch
                {
                    RollPolicy.OnInstantiate => FusionRpg.Core.Power.ContentScale.Apply(spec.Resolve(valueRng), contentScaleMilli),
                    RollPolicy.Fixed => FusionRpg.Core.Power.ContentScale.Apply(spec.Min, contentScaleMilli),
                    _ => prop.Value.Clone(), // OnApply — belongs to the hit, not the item
                };
            }

            rows.Add(new ResolvedAtom(atomId, JsonSerializer.Serialize(frozen, JsonOpts)));
        }

        return new ResolvedDraw(rows);
    }

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
}
