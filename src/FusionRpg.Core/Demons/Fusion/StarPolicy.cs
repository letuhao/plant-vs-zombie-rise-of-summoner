namespace FusionRpg.Core.Demons.Fusion;

/// <summary>
/// Star-merge rules (spec-demon-fusion.md, owner locks 2026-08-21). Numbers are spec-locked;
/// tuning is ask-first (balance boundary). Per-star bonuses reach battles ONLY as squad-build
/// ChannelMods — never engine changes (battle goldens stay byte-identical).
/// </summary>
public static class StarPolicy
{
    static FusionTuning? _tuning;

    /// <summary>Host-only (Injector/Server startup, or a test's inline construction) — also
    /// configures <see cref="FusionCostTable"/>, which reads the same tuning file.</summary>
    public static void Configure(FusionTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    internal static FusionTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "StarPolicy.Configure(...) has not run. Every fusion rule reads data/tuning/fusion.v{n}.json " +
        "(tunables-ssot.md T5) — there is no built-in default to fall back to.");

    public static int PerStarPowerMilli => Tuning.PerStarPowerMilli;
    public static int PerStarDefenseMilli => Tuning.PerStarDefenseMilli;

    public static int StarCap(DemonRarity rarity) =>
        Tuning.StarCap.TryGetValue(rarity, out var v) ? v : Tuning.StarCap[DemonRarity.Almanac];

    /// <summary>Same-rarity sacrifices consumed to reach star n (n+1 curve).</summary>
    public static int SacrificesForStar(int targetStar)
    {
        if (targetStar is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(targetStar), targetStar, "stars run 1..5");
        return targetStar + 1;
    }

    /// <summary>Promotion: once per specimen, only at the star cap, never above the top rung (Almanac).</summary>
    public static bool CanPromote(DemonRarity rarity, int star, bool promoted) =>
        !promoted && !DemonRarityLadder.IsTopRung(rarity) && star >= StarCap(rarity);
}

/// <summary>One fusion's price: Souls fee + rarity-banded shards + result-element essences.</summary>
public sealed record FusionCost(long Souls, DemonRarity ShardRarity, int ShardCount, int EssenceCount);

public static class FusionCostTable
{
    public static FusionCost StarMerge(DemonRarity baseRarity)
    {
        var c = StarPolicy.Tuning.StarMergeCost;
        return new FusionCost(c.Souls, baseRarity, c.ShardCount, c.EssenceCount);
    }

    public static FusionCost Promotion(DemonRarity newRarity)
    {
        var c = StarPolicy.Tuning.PromotionCost;
        return new FusionCost(c.Souls, newRarity, c.ShardCount, c.EssenceCount);
    }

    public static FusionCost Recipe(DemonRarity resultRarity)
    {
        if (!StarPolicy.Tuning.RecipeCost.TryGetValue(resultRarity, out var c))
            throw new ArgumentOutOfRangeException(nameof(resultRarity), resultRarity,
                "recipes only produce rare and above");
        return new FusionCost(c.Souls, c.ShardRarity, c.ShardCount, c.EssenceCount);
    }
}
