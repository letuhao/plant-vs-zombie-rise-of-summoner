namespace FusionRpg.Core.Demons.Fusion;

/// <summary>
/// One rarity rung's fusion-pairing capacity: `demon-seed` module 17 (`fusion-recipe-generator`,
/// spec §1 `distribution-index`)'s own per-rung report row.
/// </summary>
public readonly record struct RungCapacity(
    DemonRarity Rarity, int OutputCount, DemonRarity? NearestBelow, int BelowCount, long MaxPairs)
{
    /// <summary>The exact capacity ceiling `DemonRecipeCatalog.Build()` hits live: this rung's
    /// eligible outputs exceed how many distinct unordered pairs its own input pool can produce.</summary>
    public bool Shortfall => OutputCount > MaxPairs;

    /// <summary>How many outputs the deterministic pass alone cannot cover — zero when there is no
    /// shortfall.</summary>
    public long Deficit => Shortfall ? OutputCount - MaxPairs : 0;
}

/// <summary>
/// `fusion-recipe-generator` (demon-seed module 17) §1 `distribution-index` — deterministic, no model
/// calls. Reused directly by both the CLI (`tools/DemonRecipeDistributionIndex`) and its own tests, so
/// neither can silently drift from what `Build()` itself actually does: every rung's "nearest populated
/// rung below" comes from <see cref="DemonRecipeCatalog.NearestPopulatedRungBelow"/>, the exact search
/// `Build()` uses — never a second, parallel walk-down.
/// </summary>
public static class FusionRecipeDistributionIndex
{
    /// <summary>Requires <see cref="DemonSpeciesCatalog"/> already configured (real or scoped) —
    /// this type owns no I/O and reads no file itself, matching every other Core policy in this
    /// program.</summary>
    public static IReadOnlyList<RungCapacity> Compute()
    {
        var rows = new List<RungCapacity>();
        foreach (var rarity in Enum.GetValues<DemonRarity>().OrderBy(r => (int)r))
        {
            if (!DemonRarityLadder.AtLeast(rarity, DemonRecipeCatalog.OutputEligibilityFloor)) continue;

            var outputCount = DemonSpeciesCatalog.All.Count(s =>
                s.BaseRarity == rarity && s.Acquisition != DemonAcquisition.CaptureOnly);
            if (outputCount == 0) continue;

            var pool = DemonRecipeCatalog.NearestPopulatedRungBelow(rarity);
            DemonRarity? nearestBelow = pool.Count > 0 ? pool[0].BaseRarity : null;
            var belowCount = pool.Count;
            var maxPairs = (long)belowCount * (belowCount - 1) / 2;

            rows.Add(new RungCapacity(rarity, outputCount, nearestBelow, belowCount, maxPairs));
        }
        return rows;
    }
}
