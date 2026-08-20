using FusionRpg.Core.Battle;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

public class SummonRollerTests
{
    static SummonBannerDef Standard => SummonBannerCatalog.TryGet(SummonBannerCatalog.StandardRift)!;
    static SummonBannerDef Focus => SummonBannerCatalog.TryGet(SummonBannerCatalog.ElementFocus)!;

    static SeededRng Rng(ulong seed) => SeededRng.DeriveStream(seed, "gacha");

    [Fact]
    public void Roller_is_deterministic()
    {
        var (a, pa) = SummonRoller.Roll(Standard, null, 10, PityState.Fresh, Rng(7));
        var (b, pb) = SummonRoller.Roll(Standard, null, 10, PityState.Fresh, Rng(7));
        Assert.Equal(pa, pb);
        Assert.Equal(a.Select(x => (x.SpeciesId, x.Rarity, x.Variant, string.Join(',', x.TraitIds))),
                     b.Select(x => (x.SpeciesId, x.Rarity, x.Variant, string.Join(',', x.TraitIds))));
    }

    [Fact]
    public void Epic_hard_pity_fires_on_pull_25()
    {
        // 24 pulls without epic+ → the 25th must be epic-or-better regardless of the roll.
        var pity = new PityState(PullsSinceEpic: 24, PullsSinceLegendary: 24);
        for (ulong seed = 0; seed < 20; seed++)
        {
            var (results, _) = SummonRoller.Roll(Standard, null, 1, pity, Rng(seed));
            Assert.True(results[0].Rarity >= DemonRarity.Epic, $"seed {seed} gave {results[0].Rarity}");
        }
    }

    [Fact]
    public void Legendary_hard_pity_fires_on_pull_55()
    {
        var pity = new PityState(PullsSinceEpic: 0, PullsSinceLegendary: 54);
        for (ulong seed = 0; seed < 20; seed++)
        {
            var (results, newPity) = SummonRoller.Roll(Standard, null, 1, pity, Rng(seed));
            Assert.Equal(DemonRarity.Legendary, results[0].Rarity);
            Assert.Equal(0, newPity.PullsSinceLegendary);
            Assert.Equal(0, newPity.PullsSinceEpic); // legendary resets the epic counter too
        }
    }

    [Fact]
    public void Legendary_soft_ramp_raises_odds_after_pull_40()
    {
        // At counter 50 (pull 51): 10 + 11×60 = 670‰. Over many seeds most pulls are legendary.
        var ramped = new PityState(0, 50);
        var hits = 0;
        for (ulong seed = 0; seed < 200; seed++)
            if (SummonRoller.Roll(Standard, null, 1, ramped, Rng(seed)).Results[0].Rarity == DemonRarity.Legendary)
                hits++;
        Assert.InRange(hits, 110, 200); // ~67% expected; far above the 1% base

        var fresh = PityState.Fresh;
        var freshHits = 0;
        for (ulong seed = 0; seed < 200; seed++)
            if (SummonRoller.Roll(Standard, null, 1, fresh, Rng(seed)).Results[0].Rarity == DemonRarity.Legendary)
                freshHits++;
        Assert.InRange(freshHits, 0, 15); // ~1%
    }

    [Fact]
    public void Ten_pull_guarantees_rare_or_better()
    {
        for (ulong seed = 0; seed < 100; seed++)
        {
            var (results, _) = SummonRoller.Roll(Standard, null, 10, PityState.Fresh, Rng(seed));
            Assert.Equal(10, results.Count);
            Assert.Contains(results, r => r.Rarity >= DemonRarity.Rare);
        }
    }

    [Fact]
    public void Rarity_distribution_is_roughly_on_spec()
    {
        var common = 0; var rare = 0; var epicPlus = 0; var total = 0;
        var pity = PityState.Fresh;
        var rng = Rng(123);
        for (var batch = 0; batch < 300; batch++)
        {
            // Reset pity each batch so hard pity doesn't skew the base-rate measurement.
            var (results, _) = SummonRoller.Roll(Standard, null, 10, PityState.Fresh, rng);
            foreach (var r in results)
            {
                total++;
                if (r.Rarity == DemonRarity.Common) common++;
                else if (r.Rarity == DemonRarity.Rare) rare++;
                else epicPlus++;
            }
        }

        Assert.InRange(common / (double)total, 0.66, 0.80);   // 74% base minus the floor's nudge
        Assert.InRange(rare / (double)total, 0.16, 0.28);
        Assert.InRange(epicPlus / (double)total, 0.03, 0.10); // 6% combined epic+legendary
        _ = pity;
    }

    [Fact]
    public void Focus_banner_triples_focus_element_share_within_band()
    {
        var focusElement = ElementTypeId.Fire;
        var focusCount = 0; var total = 0;
        var rng = Rng(55);
        for (var i = 0; i < 400; i++)
        {
            var (results, _) = SummonRoller.Roll(Focus, focusElement, 1, PityState.Fresh, rng);
            if (results[0].Rarity != DemonRarity.Common) continue; // measure within one band
            total++;
            var species = DemonSpeciesCatalog.Get(results[0].SpeciesId);
            if (species.ElementPrimary == focusElement) focusCount++;
        }

        var commons = DemonSpeciesCatalog.All.Count(s => s.BaseRarity == DemonRarity.Common && s.Acquisition.HasFlag(DemonAcquisition.Summonable));
        var fireCommons = DemonSpeciesCatalog.All.Count(s => s.BaseRarity == DemonRarity.Common && s.Acquisition.HasFlag(DemonAcquisition.Summonable) && s.ElementPrimary == focusElement);
        Assert.True(fireCommons >= 1, "test needs at least one fire common in the catalog");
        var expected = fireCommons * 3.0 / (fireCommons * 3.0 + (commons - fireCommons));
        Assert.InRange(focusCount / (double)total, expected - 0.12, expected + 0.12);
    }

    [Fact]
    public void Capture_only_species_never_appear_in_pulls()
    {
        var rng = Rng(9);
        for (var i = 0; i < 100; i++)
        {
            var (results, _) = SummonRoller.Roll(Standard, null, 10, PityState.Fresh, rng);
            foreach (var r in results)
            {
                var s = DemonSpeciesCatalog.Get(r.SpeciesId);
                Assert.True(s.Acquisition.HasFlag(DemonAcquisition.Summonable), $"{s.SpeciesId} is not summonable");
            }
        }
    }

    [Fact]
    public void Trait_counts_follow_rarity()
    {
        var rng = Rng(77);
        for (var i = 0; i < 50; i++)
        {
            var (results, _) = SummonRoller.Roll(Standard, null, 10, PityState.Fresh, rng);
            foreach (var r in results)
            {
                var expected = r.Rarity switch
                {
                    DemonRarity.Common => 1,
                    DemonRarity.Rare or DemonRarity.Epic => 2,
                    _ => 3
                };
                var poolSize = DemonSpeciesCatalog.Get(r.SpeciesId).TraitPool.Count;
                Assert.Equal(Math.Min(expected, poolSize), r.TraitIds.Count);
                Assert.Equal(r.TraitIds.Count, r.TraitIds.Distinct().Count());
            }
        }
    }

    [Fact]
    public void Focus_rotation_is_a_pure_function_of_date()
    {
        var d = new DateOnly(2026, 8, 21);
        Assert.Equal(SummonBannerCatalog.FocusFor(d), SummonBannerCatalog.FocusFor(d));
        // Rotation changes across weeks and cycles the full roster.
        var seen = new HashSet<ElementTypeId>();
        for (var w = 0; w < 6; w++)
            seen.Add(SummonBannerCatalog.FocusFor(d.AddDays(w * 7)));
        Assert.Equal(6, seen.Count);
    }
}
