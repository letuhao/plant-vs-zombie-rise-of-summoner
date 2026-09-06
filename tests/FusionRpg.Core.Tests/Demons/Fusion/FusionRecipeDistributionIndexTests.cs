using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using Xunit;

namespace FusionRpg.Core.Tests.Demons.Fusion;

/// <summary>
/// T8.1 (`demon-seed` module 17 `fusion-recipe-generator`, spec-fusion-recipe-generator.md §1
/// `distribution-index`, tasks/seed-to-concrete-todo.md T8.1) — proves
/// <see cref="FusionRecipeDistributionIndex.Compute"/> against both the real committed corpus and
/// small synthetic fixtures, in-process, avoiding the slow/fragile CLI-subprocess pattern
/// `DemonSpeciesImportCliTests` already suffered from (observed 12-15 minutes, sometimes hanging):
/// `tools/DemonRecipeDistributionIndex/Program.cs` is a thin wrapper around the exact same
/// `Compute()` this file calls directly. Real-corpus cases share <see cref="RealCorpusFixture"/>
/// with `FusionRecipeReconcileTests` (T8.3) rather than importing the ~829-species corpus twice.
/// </summary>
public class FusionRecipeDistributionIndexTests
{
    [Fact]
    public void Real_corpus_finds_exactly_the_one_known_Almanac_shortfall_and_no_others()
    {
        using (DemonSpeciesCatalog.UseScoped(RealCorpusFixture.Snapshot))
        {
            var rows = FusionRecipeDistributionIndex.Compute();

            var shortfalls = rows.Where(r => r.Shortfall).ToList();
            var almanac = Assert.Single(shortfalls);
            Assert.Equal(DemonRarity.Almanac, almanac.Rarity);

            // Pinned to the exact numbers verified live 2026-09-06 and committed in
            // demon-seed-map.md / spec-fusion-recipe-generator.md / seed-to-concrete-plan.md /
            // seed-to-concrete-todo.md. If the real corpus legitimately changes and this breaks,
            // re-run `dotnet run --project tools/DemonRecipeDistributionIndex` and update this
            // assertion AND those four documents together — never one without the other.
            Assert.Equal(DemonRarity.Sunwoven, almanac.NearestBelow);
            Assert.Equal(4, almanac.BelowCount);
            Assert.Equal(6, almanac.MaxPairs);
            Assert.Equal(14, almanac.Deficit);

            // OutputCount is cross-checked against an independent recomputation (not just the bare
            // literal) so a genuinely new Almanac species moves this via the SAME rule Compute()
            // itself uses, rather than the test silently going stale against a magic number.
            var expectedAlmanacOutputs = DemonSpeciesCatalog.All.Count(s =>
                s.BaseRarity == DemonRarity.Almanac && s.Acquisition != DemonAcquisition.CaptureOnly);
            Assert.Equal(expectedAlmanacOutputs, almanac.OutputCount);
            Assert.Equal(20, almanac.OutputCount); // today's real number — see comment above

            // Every OTHER populated rung at/above the output floor has ample headroom — this isn't
            // the coincidence of one lucky rung, every other rung is nowhere near its own ceiling.
            foreach (var row in rows.Where(r => r.Rarity != DemonRarity.Almanac))
            {
                Assert.False(row.Shortfall, $"{row.Rarity} unexpectedly hit its pairing ceiling ({row.OutputCount} outputs vs {row.MaxPairs} max pairs)");
                Assert.True(row.MaxPairs > row.OutputCount, $"{row.Rarity} has no headroom left ({row.OutputCount}/{row.MaxPairs})");
            }
        }
    }

    [Fact]
    public void Real_corpus_rows_never_disagree_with_DemonRecipeCatalogs_own_rung_search()
    {
        // The "not a parallel reimplementation that could drift" acceptance criterion: every row's
        // NearestBelow/BelowCount must equal calling DemonRecipeCatalog.NearestPopulatedRungBelow
        // directly for that same rarity — true by construction today (Compute() calls that method,
        // never a second walk-down), but this pins the wiring so a future "optimization" that inlines
        // a different search would be caught here instead of silently drifting.
        using (DemonSpeciesCatalog.UseScoped(RealCorpusFixture.Snapshot))
        {
            var rows = FusionRecipeDistributionIndex.Compute();
            Assert.NotEmpty(rows);

            foreach (var row in rows)
            {
                var directPool = DemonRecipeCatalog.NearestPopulatedRungBelow(row.Rarity);
                var directNearestBelow = directPool.Count > 0 ? directPool[0].BaseRarity : (DemonRarity?)null;

                Assert.Equal(directNearestBelow, row.NearestBelow);
                Assert.Equal(directPool.Count, row.BelowCount);
                Assert.Equal((long)directPool.Count * (directPool.Count - 1) / 2, row.MaxPairs);
            }
        }
    }

    [Fact]
    public void Synthetic_fixture_with_headroom_at_every_rung_reports_zero_shortfalls()
    {
        // Mirrors the real corpus's own shape at a size a human can check by hand: 3 eligible species
        // at Grafted (one CaptureOnly, excluded from the input pool exactly the way
        // DemonRecipeCatalog.InputPoolBelow excludes it) support C(3,2)=3 pairs; Cultivated has
        // exactly 3 eligible outputs (also one CaptureOnly, excluded the same way) — sitting AT the
        // ceiling (3 == 3), the boundary case for Shortfall's strict ">" comparison, not comfortably
        // under it.
        var roster = new[]
        {
            SyntheticSpecies.Make("chaff-a", DemonRarity.Grafted, DemonAcquisition.Summonable, 10_001),
            SyntheticSpecies.Make("chaff-b", DemonRarity.Grafted, DemonAcquisition.Summonable, 10_002),
            SyntheticSpecies.Make("chaff-c", DemonRarity.Grafted, DemonAcquisition.Summonable, 10_003),
            SyntheticSpecies.Make("chaff-locked", DemonRarity.Grafted, DemonAcquisition.CaptureOnly, 10_004),
            SyntheticSpecies.Make("out-a", DemonRarity.Cultivated, DemonAcquisition.Summonable, 10_005),
            SyntheticSpecies.Make("out-b", DemonRarity.Cultivated, DemonAcquisition.Summonable, 10_006),
            SyntheticSpecies.Make("out-c", DemonRarity.Cultivated, DemonAcquisition.Summonable, 10_007),
            SyntheticSpecies.Make("out-locked", DemonRarity.Cultivated, DemonAcquisition.CaptureOnly, 10_008),
        };

        using (DemonSpeciesCatalog.UseScoped(roster))
        {
            var rows = FusionRecipeDistributionIndex.Compute();

            var cultivated = Assert.Single(rows); // Fused/Chimeric/.../Almanac all have zero species here -> skipped entirely
            Assert.Equal(DemonRarity.Cultivated, cultivated.Rarity);
            Assert.Equal(3, cultivated.OutputCount); // out-locked excluded
            Assert.Equal(DemonRarity.Grafted, cultivated.NearestBelow);
            Assert.Equal(3, cultivated.BelowCount); // chaff-locked excluded
            Assert.Equal(3, cultivated.MaxPairs); // C(3,2)
            Assert.False(cultivated.Shortfall); // 3 outputs == 3 max pairs, not > -> no shortfall at the boundary
            Assert.Equal(0, cultivated.Deficit);

            Assert.DoesNotContain(rows, r => r.Shortfall); // "reports nothing to fill" — the closed-loop-first case
        }
    }

    [Fact]
    public void Synthetic_fixture_with_more_outputs_than_pairs_reports_the_real_shortfall_shape()
    {
        // The general mechanism, proven at a size independent of whatever the real corpus happens to
        // look like today: 2 inputs support only C(2,2)=1 pair, but 3 outputs need distinct pairs —
        // the same shape as the real Almanac/Sunwoven ceiling, small enough to verify by hand.
        var roster = new[]
        {
            SyntheticSpecies.Make("below-a", DemonRarity.Grafted, DemonAcquisition.Summonable, 10_001),
            SyntheticSpecies.Make("below-b", DemonRarity.Grafted, DemonAcquisition.Summonable, 10_002),
            SyntheticSpecies.Make("top-a", DemonRarity.Cultivated, DemonAcquisition.Summonable, 10_003),
            SyntheticSpecies.Make("top-b", DemonRarity.Cultivated, DemonAcquisition.Summonable, 10_004),
            SyntheticSpecies.Make("top-c", DemonRarity.Cultivated, DemonAcquisition.Summonable, 10_005),
        };

        using (DemonSpeciesCatalog.UseScoped(roster))
        {
            var rows = FusionRecipeDistributionIndex.Compute();
            var cultivated = Assert.Single(rows);

            Assert.Equal(3, cultivated.OutputCount);
            Assert.Equal(2, cultivated.BelowCount);
            Assert.Equal(1, cultivated.MaxPairs); // C(2,2)
            Assert.True(cultivated.Shortfall);
            Assert.Equal(2, cultivated.Deficit); // 3 - 1
        }
    }
}
