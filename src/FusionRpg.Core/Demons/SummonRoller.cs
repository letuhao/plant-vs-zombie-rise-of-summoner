using FusionRpg.Core.Battle;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Demons;

/// <summary>Per-player pity counters — persisted, cross-banner, visible in the UI.</summary>
public sealed record PityState(int PullsSinceEpic, int PullsSinceLegendary)
{
    public static readonly PityState Fresh = new(0, 0);
}

public sealed record SummonRollResult(
    string SpeciesId,
    DemonRarity Rarity,
    string Variant,
    IReadOnlyList<string> TraitIds);

/// <summary>
/// Pure summon roller (spec-demon-summoning.md, pity v2). Deterministic from (banner, focus,
/// count, pity, rng) — the store runs it inside the pull transaction with the recorded seed.
/// Rates: common 74 % / rare 20 % / epic 5 % / legendary 1 %. Epic hard pity 25; legendary soft
/// ramp +6 %/pull from pull 41, hard 55; 10-pull rare floor rolled in the last slot.
/// </summary>
public static class SummonRoller
{
    static SummoningTuning? _tuning;

    public static void Configure(SummoningTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static RollerTuning Tuning => (_tuning ?? throw new InvalidOperationException(
        "SummonRoller.Configure(...) has not run. Pity/rarity math reads " +
        "data/tuning/summoning.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.")).Roller;

    public static int EpicHardPity => Tuning.EpicHardPity;
    public static int LegendarySoftStart => Tuning.LegendarySoftStart;   // pull number where the ramp begins
    public static int LegendaryHardPity => Tuning.LegendaryHardPity;
    public static int LegendaryBasePerMille => Tuning.LegendaryBasePerMille;
    public static int LegendaryRampPerMille => Tuning.LegendaryRampPerMille;
    public static int EpicPerMille => Tuning.EpicPerMille;
    public static int RarePerMille => Tuning.RarePerMille;
    public static int ShinyOneIn => Tuning.ShinyOneIn;

    public static (IReadOnlyList<SummonRollResult> Results, PityState Pity) Roll(
        SummonBannerDef banner, ElementTypeId? focusElement, int count, PityState pity, SeededRng rng)
    {
        if (count is not (1 or 10)) throw new ArgumentOutOfRangeException(nameof(count));
        var results = new List<SummonRollResult>(count);
        var sawRarePlus = false;
        for (var i = 0; i < count; i++)
        {
            // 10-pull rare floor: the guarantee slot rolls last.
            var floorRare = count == 10 && i == count - 1 && !sawRarePlus;
            var rolled = RollRarity(pity, floorRare, rng);
            var result = RollSpecies(rolled, banner, focusElement, rng);
            // Pity and the floor track the DELIVERED band — if band fallback ever downgrades a
            // roll (empty summonable band), the player must not lose a pity hit they never got.
            pity = Advance(pity, result.Rarity);
            if (result.Rarity >= DemonRarity.Rare) sawRarePlus = true;
            results.Add(result);
        }

        return (results, pity);
    }

    static DemonRarity RollRarity(PityState pity, bool floorRare, SeededRng rng)
    {
        var legendaryPull = pity.PullsSinceLegendary + 1;
        if (legendaryPull >= LegendaryHardPity)
            return DemonRarity.Legendary;

        var legendaryPerMille = LegendaryBasePerMille +
            Math.Max(0, legendaryPull - (LegendarySoftStart - 1)) * LegendaryRampPerMille;
        var roll = rng.NextPerMille();
        if (roll < legendaryPerMille)
            return DemonRarity.Legendary;

        if (pity.PullsSinceEpic + 1 >= EpicHardPity)
            return DemonRarity.Epic;
        if (roll < legendaryPerMille + EpicPerMille)
            return DemonRarity.Epic;
        if (roll < legendaryPerMille + EpicPerMille + RarePerMille)
            return DemonRarity.Rare;
        return floorRare ? DemonRarity.Rare : DemonRarity.Common;
    }

    static PityState Advance(PityState pity, DemonRarity rolled) => new(
        PullsSinceEpic: rolled >= DemonRarity.Epic ? 0 : pity.PullsSinceEpic + 1,
        PullsSinceLegendary: rolled == DemonRarity.Legendary ? 0 : pity.PullsSinceLegendary + 1);

    static SummonRollResult RollSpecies(
        DemonRarity rarity, SummonBannerDef banner, ElementTypeId? focusElement, SeededRng rng)
    {
        var band = BandWithFallback(rarity);
        var picked = PickWeighted(band.Pool, banner, focusElement, rng);
        var variant = picked.Variants.Contains("shiny") && rng.NextInt(ShinyOneIn) == 0 ? "shiny" : "normal";
        return new SummonRollResult(picked.SpeciesId, band.Rarity, variant, RollTraits(picked, band.Rarity, rng));
    }

    static (DemonRarity Rarity, List<DemonSpeciesDef> Pool) BandWithFallback(DemonRarity rarity)
    {
        for (var r = rarity; ; r--)
        {
            var pool = DemonSpeciesCatalog.All
                .Where(s => s.BaseRarity == r && s.Acquisition.HasFlag(DemonAcquisition.Summonable))
                .ToList();
            if (pool.Count > 0) return (r, pool);
            if (r == DemonRarity.Common)
                throw new InvalidOperationException("No summonable species in the catalog.");
        }
    }

    static DemonSpeciesDef PickWeighted(
        List<DemonSpeciesDef> pool, SummonBannerDef banner, ElementTypeId? focusElement, SeededRng rng)
    {
        if (!banner.HasElementFocus || focusElement is null)
            return pool[rng.NextInt(pool.Count)];

        // Focus banner: focus-element species get FocusWeightMultiplier× weight within the band.
        var mult = (int)Math.Round(banner.FocusWeightMultiplier);
        var total = 0;
        foreach (var s in pool)
            total += s.ElementPrimary == focusElement ? mult : 1;
        var pick = rng.NextInt(total);
        foreach (var s in pool)
        {
            pick -= s.ElementPrimary == focusElement ? mult : 1;
            if (pick < 0) return s;
        }

        return pool[^1];
    }

    /// <summary>Rarity-scaled trait roll from the species pool — shared by summons and wild joins.</summary>
    public static IReadOnlyList<string> RollTraits(DemonSpeciesDef species, DemonRarity rarity, SeededRng rng)
    {
        // ONE slot table for every acquisition path — fusion's SlotsFor is the authority.
        var want = Fusion.FusionRoller.SlotsFor(rarity);
        var pool = species.TraitPool.ToList();
        var picked = new List<string>(want);
        while (picked.Count < want && pool.Count > 0)
        {
            var i = rng.NextInt(pool.Count);
            picked.Add(pool[i]);
            pool.RemoveAt(i);
        }

        return picked;
    }
}
