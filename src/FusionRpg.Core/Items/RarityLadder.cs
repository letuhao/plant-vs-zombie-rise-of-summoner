namespace FusionRpg.Core.Items;

/// <summary>
/// The ten-rung ladder's **structural** facts (item-ideal.md, `rarity-bands`, module 7) — the ones
/// that are logic, not a balance number. The rows themselves (id, ordinal, tier window, prefix/suffix
/// floor) are content, seeded from `data/seed/rarity/ladder.v1.json` through the existing
/// `AtomSeedFile.ReadRarity` → `RpgStore` import pipeline, exactly like every other seeded table —
/// this class does not duplicate that data. The balance-surface numbers (drop weight, enhancement
/// cap, power-ceiling share) live in `data/tuning/item-rarity.v1.json` and are read by
/// <see cref="ItemRarityTuning"/>, never hardcoded here — a balance pass must be able to change them
/// with a file save, not a rebuild (tunables-ssot.md).
/// </summary>
public static class RarityLadder
{
    /// <summary>Append-only, ordinal order — the order `ssot-rarity.md` §3.3 publishes.</summary>
    public static readonly IReadOnlyList<string> RungIds = new[]
    {
        "chaff", "sprout", "grafted", "cultivated", "fused",
        "chimeric", "heirloom", "firstseed", "sunwoven", "almanac",
    };

    /// <summary>D7's own registered rule: no rung is drop-only. All ten promote from a lower rung.</summary>
    public static int PromoteFrom(string rarityId) => 1;

    /// <summary>§3.8: the two guarded rungs, mirroring the summon precedent's two counters. `almanac`
    /// is deliberately unguarded — D7 lifted rule 7, so it is reachable by promotion, and its
    /// deterministic source is module 11's to register.</summary>
    public static bool IsPityGuarded(string rarityId) =>
        rarityId is "heirloom" or "sunwoven";
}
