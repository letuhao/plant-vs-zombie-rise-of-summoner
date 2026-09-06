using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Items.Uniques;

/// <summary>Everything <see cref="UniqueContainerBuild.From"/> needs beyond the seed itself — the real
/// atom catalog this module holds no copy of (the "read model owned elsewhere" idiom this whole
/// program already uses everywhere else). <see cref="AtomsInFamily"/> exists because nothing in the
/// codebase indexes atoms by family today (confirmed by direct search) — the caller, who has the real
/// catalog loaded, is the only one who can answer it.</summary>
public sealed record UniqueContainerLookups(
    Func<string, AtomRow?> LookupAtom,
    Func<string, IReadOnlyList<AtomRow>> AtomsInFamily);

/// <summary>
/// D4.24 (spec-unique-pipeline.md §2) — "`UniqueContainerBuild.From(anchor, rollSeed, tuning, lookups)`
/// is a pure function producing the `ContainerRow`; the instance is `TryInstantiate` on it — nothing
/// else." This build takes <paramref name="rung"/> instead of a raw `rollSeed`/`tuning` pair: nothing
/// here rolls anything (the seed contract forbids a seed from authoring numbers, and the ONE roll this
/// container ever takes happens later, inside `TryInstantiate` itself) — only <see cref="UniqueBudget.ReferenceTier"/>
/// needs the rung's own tier window, so that is the one live fact this function actually reads.
///
/// <para><b>Fixed core</b> = <c>fixedAtoms[]</c>, one <see cref="ContainerAtomRow"/> each: `family ×
/// powerBand` → tier (<see cref="UniqueBudget.TierOfPowerBand"/>) → atom id
/// (<see cref="AtomRow.DeriveId"/>) — the two real, already-shipped halves confirmed by direct search;
/// composing them is this function's own new, small work (spec §2, verbatim).</para>
///
/// <para><b>Pool = the variance slot</b> (spec §2: "`PrefixRolls + SuffixRolls = 1` (or 0), `MinTier ==
/// MaxTier` at the authored tier, 3-6 `ContainerPoolRow`s from the affix library"). <b>Confirmed by a
/// dedicated search: no existing code resolves "a family → N weighted pool candidates" anywhere in
/// this codebase</b> — the shared affix generator wraps EVERY atom in the catalog 1:1
/// (<see cref="AffixLibraryGenerator.Generate"/>) with no family filter, and the one sibling
/// container-with-a-pool build (`EliteAffix.Apply`, encounter-generator) takes its own pool as an
/// ALREADY-FILTERED parameter rather than deriving one — this is genuinely new work, not a mirror.
/// "The authored tier" is <see cref="UniqueBudget.ReferenceTier"/> (already shipped, used identically
/// for pricing: "at the rung's reference tier", `UniqueValidator.cs`'s own `UniquePricing` doc comment)
/// — the rung's own tier window midpoint, rounded down. <b>Every atom in the variance family AT that
/// tier becomes one pool candidate</b> (equal weight — nothing in the spec or the shipped tuning names
/// a per-candidate weighting rule for this pool specifically), wrapped as a single-atom affix via
/// <see cref="AffixLibraryGenerator.SingleAtomAffix"/> — never re-deriving that wrap.</para>
///
/// <para><b>The one roll's own side (prefix vs. suffix), named explicitly:</b> a container's `PrefixRolls`/
/// `SuffixRolls` are budgets `Instantiator`'s own `Draw` spends against affixes of the MATCHING
/// <see cref="AffixClass"/> only (`Instantiator.cs`'s `DrawBudget` calls, confirmed by direct read) —
/// class is derived from whether the underlying atom carries a trigger
/// (<see cref="AffixValidator.AffixClassOfAtom"/>), never authored. This build reads the FIRST
/// resolved candidate's own class and spends the container's one roll on that side; every OTHER
/// candidate of the SAME class joins the pool, and a candidate of the OTHER class is dropped from the
/// pool with a report line — it would sit in `Pool` unreachable by the container's own single roll
/// budget otherwise, which is strictly worse than a smaller, fully-reachable pool.</para>
/// </summary>
public static class UniqueContainerBuild
{
    /// <summary>One dropped variance candidate, named so a caller can see the corpus is under-using a
    /// family rather than silently shrinking it — never a hard refusal (the fixed core and the rest of
    /// the pool are still perfectly valid), matching <see cref="UniqueCorpusReport"/>'s own "measure
    /// and say so" posture rather than <see cref="UniqueCorpusValidator"/>'s "refuse".</summary>
    public readonly record struct DroppedCandidate(string SeedId, string AtomId, string Reason);

    public sealed record BuildResult(ContainerRow Container, IReadOnlyList<DroppedCandidate> Dropped);

    public static BuildResult From(UniqueSeed anchor, RarityRungWindow rung, UniqueContainerLookups lookups)
    {
        if (anchor is null) throw new ArgumentNullException(nameof(anchor));
        if (lookups is null) throw new ArgumentNullException(nameof(lookups));
        if (!string.Equals(rung.RarityId, anchor.RarityId, StringComparison.Ordinal))
            throw new ArgumentException(
                $"unique '{anchor.SeedId}' authors rarity '{anchor.RarityId}' but the supplied window is for '{rung.RarityId}'",
                nameof(rung));

        var atoms = new List<ContainerAtomRow>();
        var seq = 0;
        foreach (var fa in anchor.FixedAtoms)
        {
            var tier = UniqueBudget.TierOfPowerBand(fa.PowerBand);
            var atomId = AtomRow.DeriveId(fa.Family, "", tier);
            if (lookups.LookupAtom(atomId) is null)
                throw new UniqueCorpusRejection(UniqueRules.CorpusMalformed,
                    $"unique '{anchor.SeedId}' fixed atom '{atomId}' (family '{fa.Family}', band '{fa.PowerBand}') is not in the atom catalog");
            atoms.Add(new ContainerAtomRow(seq++, atomId));
        }

        var pool = new List<ContainerPoolRow>();
        var dropped = new List<DroppedCandidate>();
        int? minTier = null, maxTier = null;
        var prefixRolls = 0;
        var suffixRolls = 0;

        if (anchor.VarianceSlot is { } variance)
        {
            var tier = UniqueBudget.ReferenceTier(rung);
            var candidates = lookups.AtomsInFamily(variance.Family).Where(a => a.Tier == tier).ToList();
            if (candidates.Count == 0)
                throw new UniqueCorpusRejection(UniqueRules.CorpusMalformed,
                    $"unique '{anchor.SeedId}' variance family '{variance.Family}' has no atom at reference tier {tier}");

            var rollClass = AffixValidator.AffixClassOfAtom(candidates[0]);
            if (rollClass == AffixClass.Prefix) prefixRolls = 1; else suffixRolls = 1;

            foreach (var atom in candidates)
            {
                if (AffixValidator.AffixClassOfAtom(atom) != rollClass)
                {
                    dropped.Add(new DroppedCandidate(anchor.SeedId, atom.AtomId,
                        $"family '{variance.Family}' mixes {rollClass} and {AffixValidator.AffixClassOfAtom(atom)} atoms; " +
                        $"the container's one roll is {rollClass}-side, so this candidate would never be reachable"));
                    continue;
                }
                pool.Add(new ContainerPoolRow(AffixLibraryGenerator.SingleAtomAffix(atom).AffixId, Weight: 1));
            }

            minTier = tier;
            maxTier = tier;
        }

        var container = new ContainerRow
        {
            ContainerId = anchor.ContainerId,
            Kind = ContainerKind.Item,
            Rarity = anchor.RarityId,
            MinTier = minTier,
            MaxTier = maxTier,
            PrefixRolls = prefixRolls,
            SuffixRolls = suffixRolls,
            Atoms = atoms,
            Pool = pool,
        };
        return new BuildResult(container, dropped);
    }
}
