using FusionRpg.Core.Battle;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Demons;

/// <summary>Per-player pity counters — persisted, cross-banner, visible in the UI. Field names
/// renamed to their guarded rungs (spec-rarity-migration.md §6, T4.1) — `PullsSinceHeirloom` guards
/// ordinal 70, `PullsSinceSunwoven` guards ordinal 90 (ssot-rarity.md §3.3). The underlying
/// `rpg_summon_pity` SQL columns keep their old names (`pulls_since_epic`/`pulls_since_legendary`)
/// — the counters' function is unchanged, only which rung they guarantee moved, so a schema
/// migration for a label-only rename is not warranted.</summary>
public sealed record PityState(int PullsSinceHeirloom, int PullsSinceSunwoven)
{
    public static readonly PityState Fresh = new(0, 0);
}

public sealed record SummonRollResult(
    string SpeciesId,
    DemonRarity Rarity,
    string Variant,
    IReadOnlyList<string> TraitIds);

/// <summary>
/// Pure summon roller (spec-demon-summoning.md, pity v2; widened to the ten-rung ladder by
/// `seed-to-concrete` T4.1, owner Q15). Deterministic from (banner, focus, count, pity, rng) — the
/// store runs it inside the pull transaction with the recorded seed. Rates are a monotone staircase,
/// chaff (implicit remainder) down to almanac (2‰, never pity-boosted) — see
/// `data/tuning/summoning.v1.json`'s own note for the exact table and why it is a starting value.
/// Heirloom (ordinal 70) hard-pities at pull 25; sunwoven (ordinal 90) soft-ramps from pull 41,
/// hard-pities at 55; a 10-pull sprout-or-better floor rolls in the last slot.
/// </summary>
public static class SummonRoller
{
    static SummoningTuning? _tuning;

    public static void Configure(SummoningTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static RollerTuning Tuning => (_tuning ?? throw new InvalidOperationException(
        "SummonRoller.Configure(...) has not run. Pity/rarity math reads " +
        "data/tuning/summoning.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.")).Roller;

    public static int HeirloomHardPity => Tuning.HeirloomHardPity;
    public static int SunwovenSoftStart => Tuning.SunwovenSoftStart;   // pull number where the ramp begins
    public static int SunwovenHardPity => Tuning.SunwovenHardPity;
    public static int SunwovenBasePerMille => Tuning.SunwovenBasePerMille;
    public static int SunwovenRampPerMille => Tuning.SunwovenRampPerMille;
    public static int AlmanacPerMille => Tuning.AlmanacPerMille;
    public static int HeirloomPerMille => Tuning.HeirloomPerMille;
    public static int ShinyOneIn => Tuning.ShinyOneIn;

    /// <summary>The rung a 10-pull's guaranteed floor targets — Cultivated, Rare's own migration
    /// target (ssot-rarity.md §4.3's forward map), preserving the OLD floor's actual GUARANTEE
    /// LEVEL rather than its ordinal position. This also happens to be the right choice for
    /// today's catalog: only four of the ten rungs are populated (the four legacy-mapped ones),
    /// so a floor targeting one of the six currently-empty rungs (e.g. Sprout) would silently
    /// degrade to Chaff via `BandWithFallback`'s own empty-band search — a real bug this design
    /// avoids, found by `Ten_pull_guarantees_sprout_or_better` failing against the real catalog.</summary>
    static readonly DemonRarity FloorTarget = DemonRarity.Cultivated;

    public static (IReadOnlyList<SummonRollResult> Results, PityState Pity) Roll(
        SummonBannerDef banner, ElementTypeId? focusElement, int count, PityState pity, SeededRng rng)
    {
        if (count is not (1 or 10)) throw new ArgumentOutOfRangeException(nameof(count));
        var results = new List<SummonRollResult>(count);
        var sawFloorOrBetter = false;
        for (var i = 0; i < count; i++)
        {
            // 10-pull floor: the guarantee slot rolls last.
            var floorRare = count == 10 && i == count - 1 && !sawFloorOrBetter;
            var rolled = RollRarity(pity, floorRare, rng);
            var result = RollSpecies(rolled, banner, focusElement, rng);
            // Pity and the floor track the DELIVERED band — if band fallback ever downgrades a
            // roll (empty summonable band), the player must not lose a pity hit they never got.
            pity = Advance(pity, result.Rarity);
            if (DemonRarityLadder.AtLeast(result.Rarity, FloorTarget)) sawFloorOrBetter = true;
            results.Add(result);
        }

        return (results, pity);
    }

    static DemonRarity RollRarity(PityState pity, bool floorRare, SeededRng rng)
    {
        var natural = RollRarityNatural(pity, rng);
        // The floor is a clean post-process, not woven into the cascade: whatever the natural
        // roll (and pity) produced, bump it up to FloorTarget if it landed below that — never
        // silently swallowed by one specific branch of the cascade (the earlier shape only forced
        // the floor from the bottommost branch, which broke the moment the catalog didn't
        // populate that exact rung — see FloorTarget's own doc comment).
        if (floorRare && !DemonRarityLadder.AtLeast(natural, FloorTarget))
            return FloorTarget;
        return natural;
    }

    static DemonRarity RollRarityNatural(PityState pity, SeededRng rng)
    {
        // Guard 2 (ordinal 90, Sunwoven) — soft ramp then hard pity. Almanac (ordinal 100, the
        // true top) sits ABOVE this guard and is never boosted by it (Q15: guards are at 70/90).
        var sunwovenPull = pity.PullsSinceSunwoven + 1;
        if (sunwovenPull >= SunwovenHardPity)
            return DemonRarity.Sunwoven;

        var sunwovenPerMille = SunwovenBasePerMille +
            Math.Max(0, sunwovenPull - (SunwovenSoftStart - 1)) * SunwovenRampPerMille;
        var roll = rng.NextPerMille();
        if (roll < AlmanacPerMille)
            return DemonRarity.Almanac;
        if (roll < AlmanacPerMille + sunwovenPerMille)
            return DemonRarity.Sunwoven;

        // Guard 1 (ordinal 70, Heirloom) — independent hard pity, no ramp (matches the old
        // EpicHardPity shape: a flat pull-count floor, not a growing chance).
        if (pity.PullsSinceHeirloom + 1 >= HeirloomHardPity)
            return DemonRarity.Heirloom;

        var cum = AlmanacPerMille + sunwovenPerMille;
        if (roll < cum + HeirloomPerMille) return DemonRarity.Heirloom;
        cum += HeirloomPerMille;
        if (roll < cum + Tuning.FirstseedPerMille) return DemonRarity.Firstseed;
        cum += Tuning.FirstseedPerMille;
        if (roll < cum + Tuning.ChimericPerMille) return DemonRarity.Chimeric;
        cum += Tuning.ChimericPerMille;
        if (roll < cum + Tuning.FusedPerMille) return DemonRarity.Fused;
        cum += Tuning.FusedPerMille;
        if (roll < cum + Tuning.CultivatedPerMille) return DemonRarity.Cultivated;
        cum += Tuning.CultivatedPerMille;
        if (roll < cum + Tuning.GraftedPerMille) return DemonRarity.Grafted;
        cum += Tuning.GraftedPerMille;
        if (roll < cum + Tuning.SproutPerMille) return DemonRarity.Sprout;
        return DemonRarity.Chaff;
    }

    static PityState Advance(PityState pity, DemonRarity rolled) => new(
        PullsSinceHeirloom: DemonRarityLadder.AtLeast(rolled, DemonRarity.Heirloom) ? 0 : pity.PullsSinceHeirloom + 1,
        PullsSinceSunwoven: DemonRarityLadder.AtLeast(rolled, DemonRarity.Sunwoven) ? 0 : pity.PullsSinceSunwoven + 1);

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
        var r = rarity;
        while (true)
        {
            var pool = DemonSpeciesCatalog.All
                .Where(s => s.BaseRarity == r && s.Acquisition.HasFlag(DemonAcquisition.Summonable))
                .ToList();
            if (pool.Count > 0) return (r, pool);
            if (DemonRarityLadder.IsBottomRung(r))
                throw new InvalidOperationException("No summonable species in the catalog.");
            r = DemonRarityLadder.OneRungBelow(r);
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
