using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>F1: star caps, sacrifice curve, per-star bonuses, promotion gate, cost table —
/// the locked numbers from spec-demon-fusion.md (tuning is ask-first).</summary>
public class StarPolicyTests
{
    [Theory]
    [InlineData(DemonRarity.Common, 3)]
    [InlineData(DemonRarity.Rare, 4)]
    [InlineData(DemonRarity.Epic, 5)]
    [InlineData(DemonRarity.Legendary, 5)]
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
    public void Promotion_gates_on_max_star_once_below_legendary()
    {
        Assert.True(StarPolicy.CanPromote(DemonRarity.Common, 3, promoted: false));
        Assert.False(StarPolicy.CanPromote(DemonRarity.Common, 2, promoted: false)); // not maxed
        Assert.False(StarPolicy.CanPromote(DemonRarity.Common, 3, promoted: true));  // once only
        Assert.False(StarPolicy.CanPromote(DemonRarity.Legendary, 5, promoted: false)); // no ceiling above
        Assert.True(StarPolicy.CanPromote(DemonRarity.Epic, 5, promoted: false));
    }

    [Fact]
    public void Cost_table_matches_the_spec()
    {
        var merge = FusionCostTable.StarMerge(DemonRarity.Rare);
        Assert.Equal((50L, DemonRarity.Rare, 1, 1), (merge.Souls, merge.ShardRarity, merge.ShardCount, merge.EssenceCount));

        var promo = FusionCostTable.Promotion(DemonRarity.Epic);
        Assert.Equal((200L, DemonRarity.Epic, 3, 3), (promo.Souls, promo.ShardRarity, promo.ShardCount, promo.EssenceCount));

        Assert.Equal((150L, DemonRarity.Common, 2, 2), Tuple(FusionCostTable.Recipe(DemonRarity.Rare)));
        Assert.Equal((400L, DemonRarity.Rare, 3, 4), Tuple(FusionCostTable.Recipe(DemonRarity.Epic)));
        Assert.Equal((1000L, DemonRarity.Epic, 4, 8), Tuple(FusionCostTable.Recipe(DemonRarity.Legendary)));
        Assert.Throws<ArgumentOutOfRangeException>(() => FusionCostTable.Recipe(DemonRarity.Common));

        static (long, DemonRarity, int, int) Tuple(FusionCost c) =>
            (c.Souls, c.ShardRarity, c.ShardCount, c.EssenceCount);
    }

    [Fact]
    public void Shard_ids_round_trip_the_material_catalog()
    {
        foreach (var rarity in new[] { DemonRarity.Common, DemonRarity.Rare, DemonRarity.Epic, DemonRarity.Legendary })
            Assert.True(DemonMaterialCatalog.IsKnown("shard." + rarity.ToId()));
    }
}
