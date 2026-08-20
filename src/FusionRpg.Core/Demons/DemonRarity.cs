namespace FusionRpg.Core.Demons;

public enum DemonRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>How a species can enter the roster. A species with None is a catalog error.</summary>
[Flags]
public enum DemonAcquisition
{
    None = 0,
    Summonable = 1,
    CaptureOnly = 2,
    EventOnly = 4
}

/// <summary>How a species expresses in a PvZ run (resolved decision 1; deploy modules consume this later).</summary>
public enum DemonDeployMode
{
    PlantAvatar,
    HypnoAlly
}

public static class DemonRarityIds
{
    public static string ToId(this DemonRarity rarity) => rarity switch
    {
        DemonRarity.Common => "common",
        DemonRarity.Rare => "rare",
        DemonRarity.Epic => "epic",
        DemonRarity.Legendary => "legendary",
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null)
    };

    public static bool TryParse(string? value, out DemonRarity rarity)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "common": rarity = DemonRarity.Common; return true;
            case "rare": rarity = DemonRarity.Rare; return true;
            case "epic": rarity = DemonRarity.Epic; return true;
            case "legendary": rarity = DemonRarity.Legendary; return true;
            default: rarity = default; return false;
        }
    }
}
