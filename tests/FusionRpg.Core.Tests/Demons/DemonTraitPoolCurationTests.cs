using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

public class DemonTraitPoolCurationTests
{
    [Fact]
    public void A_species_still_present_in_the_legacy_compiled_catalog_ports_its_trait_pool_forward_verbatim()
    {
        DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        var legacy = DemonSpeciesCatalog.Get("peashooter").TraitPool;

        // Rarity/gameTypeId are deliberately wrong here — the legacy port-forward path keys ONLY on
        // speciesId, so a mismatched rarity/gameTypeId must not change the result.
        var curated = DemonTraitPoolCuration.PickFor("peashooter", DemonRarity.Almanac, gameTypeId: -1);

        Assert.Equal(legacy, curated);
        Assert.NotEmpty(curated);
    }

    [Fact]
    public void Species_id_matching_is_case_and_whitespace_insensitive_like_the_rest_of_the_pipeline()
    {
        DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        var legacy = DemonSpeciesCatalog.Get("peashooter").TraitPool;

        var curated = DemonTraitPoolCuration.PickFor(" Peashooter ", DemonRarity.Cultivated, gameTypeId: 0);

        Assert.Equal(legacy, curated);
    }

    [Fact]
    public void A_species_absent_from_the_legacy_catalog_gets_a_deterministic_non_empty_pick()
    {
        var first = DemonTraitPoolCuration.PickFor("not-a-real-legacy-species", DemonRarity.Cultivated, gameTypeId: 4242);
        var second = DemonTraitPoolCuration.PickFor("not-a-real-legacy-species", DemonRarity.Cultivated, gameTypeId: 4242);

        Assert.NotEmpty(first);
        Assert.Equal(first, second); // same inputs, same pick — required for --check byte-identical regen
    }

    [Theory]
    [InlineData(DemonRarity.Chaff)]
    [InlineData(DemonRarity.Cultivated)]
    [InlineData(DemonRarity.Heirloom)]
    [InlineData(DemonRarity.Sunwoven)]
    [InlineData(DemonRarity.Almanac)]
    public void Every_hash_picked_trait_is_a_known_DemonTraitCatalog_member(DemonRarity rarity)
    {
        for (var gameTypeId = 0; gameTypeId < 25; gameTypeId++)
        {
            var picked = DemonTraitPoolCuration.PickFor($"synthetic-{gameTypeId}", rarity, gameTypeId);
            foreach (var trait in picked)
                Assert.True(DemonTraitCatalog.IsKnown(trait), $"'{trait}' (rarity={rarity}, gameTypeId={gameTypeId})");
        }
    }

    [Fact]
    public void Rarity_at_least_Heirloom_adds_an_essence_trait_below_Heirloom_does_not()
    {
        var below = DemonTraitPoolCuration.PickFor("synthetic-below", DemonRarity.Cultivated, gameTypeId: 7);
        var atHeirloom = DemonTraitPoolCuration.PickFor("synthetic-at", DemonRarity.Heirloom, gameTypeId: 7);

        Assert.DoesNotContain("void-touched", below);
        Assert.DoesNotContain("chaos-marked", below);
        Assert.True(atHeirloom.Contains("void-touched") || atHeirloom.Contains("chaos-marked"));
    }

    [Fact]
    public void Only_the_top_rung_rarity_adds_immortal()
    {
        var sunwoven = DemonTraitPoolCuration.PickFor("synthetic-sw", DemonRarity.Sunwoven, gameTypeId: 3);
        var almanac = DemonTraitPoolCuration.PickFor("synthetic-al", DemonRarity.Almanac, gameTypeId: 3);

        Assert.DoesNotContain("immortal", sunwoven);
        Assert.Contains("immortal", almanac);
    }
}
