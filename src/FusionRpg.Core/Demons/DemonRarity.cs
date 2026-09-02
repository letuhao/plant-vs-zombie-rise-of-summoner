namespace FusionRpg.Core.Demons;

/// <summary>
/// The item ladder's ten rungs, adopted by demons 2026-09-01 (`seed-to-concrete` T4.1, owner Q4/Q24 —
/// <see href="../../../docs/architecture/item/ssot-rarity.md">ssot-rarity.md</see> §4.3, reversed the
/// same day it was first decided). Ordinal IS rank: <c>Chaff=0</c> is weakest, <c>Almanac=9</c> is
/// strongest, and every rung in between (<c>Sprout..Sunwoven</c>) is now addressable where only four
/// existed before.
///
/// ⛔ <b>Never cast to/from <see cref="int"/> or compare with <c>&lt;</c>/<c>&gt;</c>/<c>&lt;=</c>/
/// <c>&gt;=</c> against a named member directly</b> — both silently changed meaning the day this enum
/// widened from four values to ten (spec-rarity-migration.md §3: a bare <c>(int)r-1</c> meant "one
/// rung of four" before and "one rung of ten" after, with no compiler error either way). Use
/// <see cref="DemonRarityLadder"/>'s named helpers instead; a guard test forbids the bare forms.
/// </summary>
public enum DemonRarity
{
    Chaff,
    Sprout,
    Grafted,
    Cultivated,
    Fused,
    Chimeric,
    Heirloom,
    Firstseed,
    Sunwoven,
    Almanac,
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
    /// <summary>The item ladder's own lower-case ids (ssot-rarity.md §3.3) — never the legacy
    /// `common`/`rare`/`epic`/`legendary` names. Legacy ids still resolve, but only through
    /// <see cref="LegacyDemonRarityIds"/>'s one-way forward map (spec-rarity-migration.md §4).</summary>
    public static string ToId(this DemonRarity rarity) => rarity switch
    {
        DemonRarity.Chaff => "chaff",
        DemonRarity.Sprout => "sprout",
        DemonRarity.Grafted => "grafted",
        DemonRarity.Cultivated => "cultivated",
        DemonRarity.Fused => "fused",
        DemonRarity.Chimeric => "chimeric",
        DemonRarity.Heirloom => "heirloom",
        DemonRarity.Firstseed => "firstseed",
        DemonRarity.Sunwoven => "sunwoven",
        DemonRarity.Almanac => "almanac",
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null)
    };

    public static bool TryParse(string? value, out DemonRarity rarity)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "chaff": rarity = DemonRarity.Chaff; return true;
            case "sprout": rarity = DemonRarity.Sprout; return true;
            case "grafted": rarity = DemonRarity.Grafted; return true;
            case "cultivated": rarity = DemonRarity.Cultivated; return true;
            case "fused": rarity = DemonRarity.Fused; return true;
            case "chimeric": rarity = DemonRarity.Chimeric; return true;
            case "heirloom": rarity = DemonRarity.Heirloom; return true;
            case "firstseed": rarity = DemonRarity.Firstseed; return true;
            case "sunwoven": rarity = DemonRarity.Sunwoven; return true;
            case "almanac": rarity = DemonRarity.Almanac; return true;
            default: rarity = default; return false;
        }
    }
}

/// <summary>
/// The one-way forward map from the four legacy rarity ids to the new ladder (spec-rarity-migration.md
/// §4): each legacy band maps to its <b>lowest</b> rung, so no player gains value on migration. Legacy
/// ids stay resolvable — never issuable again — for one release, so a stale client or saved reference
/// does not hard-fail (spec §4 point 4).
/// </summary>
public static class LegacyDemonRarityIds
{
    /// <summary>Legacy id -> new ladder id. Never the reverse — new content is never authored in legacy ids.</summary>
    public static readonly IReadOnlyDictionary<string, DemonRarity> ForwardMap = new Dictionary<string, DemonRarity>
    {
        ["common"] = DemonRarity.Chaff,        // Common band (10-30) -> lowest: chaff
        ["rare"] = DemonRarity.Cultivated,      // Rare band (40-60) -> lowest: cultivated
        ["epic"] = DemonRarity.Heirloom,        // Epic band (70-80) -> lowest: heirloom
        ["legendary"] = DemonRarity.Sunwoven,   // Legendary band (90-100) -> lowest: sunwoven
    };

    public static bool IsLegacyId(string? value) =>
        ForwardMap.ContainsKey((value ?? "").Trim().ToLowerInvariant());

    /// <summary>Resolves EITHER a legacy id or a current ladder id — the "resolvable but unissuable"
    /// window (spec §4 point 4). Never used to author new content; `DemonRarityIds.TryParse` alone is
    /// the current, issuable vocabulary.</summary>
    public static bool TryResolve(string? value, out DemonRarity rarity)
    {
        var key = (value ?? "").Trim().ToLowerInvariant();
        if (ForwardMap.TryGetValue(key, out rarity))
            return true;
        return DemonRarityIds.TryParse(value, out rarity);
    }
}
