using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>F1: star caps, sacrifice curve, per-star bonuses, promotion gate, cost table —
/// the locked numbers from spec-demon-fusion.md (tuning is ask-first).</summary>
public class StarPolicyTests
{
    [Theory]
    [InlineData(DemonRarity.Chaff, 3)]
    [InlineData(DemonRarity.Cultivated, 4)]
    [InlineData(DemonRarity.Heirloom, 5)]
    [InlineData(DemonRarity.Sunwoven, 5)]
    public void Star_caps_by_rarity(DemonRarity rarity, int cap) =>
        Assert.Equal(cap, StarPolicy.StarCap(rarity));

    [Fact]
    public void Sacrifice_curve_is_n_plus_one()
    {
        Assert.Equal(2, StarPolicy.SacrificesForStar(1));
        Assert.Equal(4, StarPolicy.SacrificesForStar(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => StarPolicy.SacrificesForStar(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StarPolicy.SacrificesForStar(6));
    }

    [Fact]
    public void Per_star_bonuses_are_locked()
    {
        Assert.Equal(30, StarPolicy.PerStarPowerMilli);
        Assert.Equal(30, StarPolicy.PerStarDefenseMilli);
    }

    [Fact]
    public void Promotion_gates_on_max_star_once_below_the_top_rung()
    {
        // "The top rung" is Almanac (ordinal 100, the TRUE top of the ten-rung ladder) — not
        // Sunwoven, which is merely where Legendary's species happened to migrate to
        // (seed-to-concrete T4.1). A Sunwoven demon CAN now promote further, to Almanac; only
        // Almanac itself has no ceiling above it.
        Assert.True(StarPolicy.CanPromote(DemonRarity.Chaff, 3, promoted: false));
        Assert.False(StarPolicy.CanPromote(DemonRarity.Chaff, 2, promoted: false)); // not maxed
        Assert.False(StarPolicy.CanPromote(DemonRarity.Chaff, 3, promoted: true));  // once only
        Assert.True(StarPolicy.CanPromote(DemonRarity.Sunwoven, 5, promoted: false)); // can still promote to Almanac
        Assert.False(StarPolicy.CanPromote(DemonRarity.Almanac, 5, promoted: false)); // no ceiling above the true top
        Assert.True(StarPolicy.CanPromote(DemonRarity.Heirloom, 5, promoted: false));
    }

    [Fact]
    public void Cost_table_matches_the_spec()
    {
        var merge = FusionCostTable.StarMerge(DemonRarity.Cultivated);
        Assert.Equal((50L, DemonRarity.Cultivated, 1, 1), (merge.Souls, merge.ShardRarity, merge.ShardCount, merge.EssenceCount));

        var promo = FusionCostTable.Promotion(DemonRarity.Heirloom);
        Assert.Equal((200L, DemonRarity.Heirloom, 3, 3), (promo.Souls, promo.ShardRarity, promo.ShardCount, promo.EssenceCount));

        Assert.Equal((150L, DemonRarity.Chaff, 2, 2), Tuple(FusionCostTable.Recipe(DemonRarity.Cultivated)));
        Assert.Equal((400L, DemonRarity.Cultivated, 3, 4), Tuple(FusionCostTable.Recipe(DemonRarity.Heirloom)));
        Assert.Equal((1000L, DemonRarity.Heirloom, 4, 8), Tuple(FusionCostTable.Recipe(DemonRarity.Sunwoven)));
        Assert.Throws<ArgumentOutOfRangeException>(() => FusionCostTable.Recipe(DemonRarity.Chaff));

        static (long, DemonRarity, int, int) Tuple(FusionCost c) =>
            (c.Souls, c.ShardRarity, c.ShardCount, c.EssenceCount);
    }

    [Fact]
    public void Shard_ids_round_trip_the_material_catalog()
    {
        foreach (var rarity in new[] { DemonRarity.Chaff, DemonRarity.Cultivated, DemonRarity.Heirloom, DemonRarity.Sunwoven })
            Assert.True(DemonMaterialCatalog.IsKnown("shard." + rarity.ToId()));
    }
}
