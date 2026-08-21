namespace FusionRpg.Core.Demons.Fusion;

/// <summary>
/// Star-merge rules (spec-demon-fusion.md, owner locks 2026-08-21). Numbers are spec-locked;
/// tuning is ask-first (balance boundary). Per-star bonuses reach battles ONLY as squad-build
/// ChannelMods — never engine changes (battle goldens stay byte-identical).
/// </summary>
public static class StarPolicy
{
    public const int PerStarPowerMilli = 30;
    public const int PerStarDefenseMilli = 30;

    public static int StarCap(DemonRarity rarity) => rarity switch
    {
        DemonRarity.Common => 3,
        DemonRarity.Rare => 4,
        _ => 5
    };

    /// <summary>Same-rarity sacrifices consumed to reach star n (n+1 curve).</summary>
    public static int SacrificesForStar(int targetStar)
    {
        if (targetStar is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(targetStar), targetStar, "stars run 1..5");
        return targetStar + 1;
    }

    /// <summary>Promotion: once per specimen, only at the star cap, never above legendary.</summary>
    public static bool CanPromote(DemonRarity rarity, int star, bool promoted) =>
        !promoted && rarity != DemonRarity.Legendary && star >= StarCap(rarity);
}

/// <summary>One fusion's price: Souls fee + rarity-banded shards + result-element essences.</summary>
public sealed record FusionCost(long Souls, DemonRarity ShardRarity, int ShardCount, int EssenceCount);

public static class FusionCostTable
{
    public static FusionCost StarMerge(DemonRarity baseRarity) => new(50, baseRarity, 1, 1);

    public static FusionCost Promotion(DemonRarity newRarity) => new(200, newRarity, 3, 3);

    public static FusionCost Recipe(DemonRarity resultRarity) => resultRarity switch
    {
        DemonRarity.Rare => new FusionCost(150, DemonRarity.Common, 2, 2),
        DemonRarity.Epic => new FusionCost(400, DemonRarity.Rare, 3, 4),
        DemonRarity.Legendary => new FusionCost(1000, DemonRarity.Epic, 4, 8),
        _ => throw new ArgumentOutOfRangeException(nameof(resultRarity), resultRarity,
            "recipes only produce rare and above")
    };
}
