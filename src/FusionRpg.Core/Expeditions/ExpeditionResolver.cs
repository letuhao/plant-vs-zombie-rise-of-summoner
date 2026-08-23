using FusionRpg.Core.Battle;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Expeditions;

public static class ExpeditionTickKinds
{
    public const string Battle = "battle";
    public const string BossBattle = "boss-battle";
    public const string Quiet = "quiet";
    public const string FoundSouls = "found-souls";
    public const string WildDemonMet = "wild-demon-met";
    public const string Injury = "injury";
}

public sealed record ExpeditionTickOutcome(
    int TickIndex, string Kind, int BattleIndex, long Souls,
    string? WildSpeciesId, bool WildJoins, string? WildVariant, string? InjuredKey);

public sealed record ExpeditionBattlePlan(
    int BattleIndex, int TickIndex, bool Boss, BattleSetup Setup, ulong BattleSeed);

public sealed record MaterialDrop(string MaterialId, long Qty);

public sealed record WildJoinResult(string SpeciesId, string Variant, IReadOnlyList<string> TraitIds);

public sealed record ExpeditionRewards(
    long EventSouls,
    IReadOnlyList<MaterialDrop> Materials,
    IReadOnlyList<WildJoinResult> WildJoins,
    int SpecimenXpPerBattleWon);

public sealed record ExpeditionResolution(
    string TierId, int ElapsedTicks,
    IReadOnlyList<ExpeditionTickOutcome> Ticks,
    IReadOnlyList<ExpeditionBattlePlan> Battles,
    ExpeditionRewards Rewards);

/// <summary>
/// Pure chain+events resolver (spec-expeditions.md): (tier, squad, seed, elapsedTicks) →
/// tick outcomes + battle plans + rewards manifest. Every tick derives its own RNG stream from
/// the expedition seed, so recall pro-rating is exact by construction: elapsed ticks resolve
/// identically whether or not the tail ever runs. The battles themselves resolve later through
/// BattleEngine (via WebMatchService at collect) using the per-battle seeds minted here.
/// </summary>
public static class ExpeditionResolver
{
    // Event roll CEILINGS (‰, cumulative): quiet <400, found-souls <750, wild-demon-met <900,
    // injury ≥900 — i.e. band widths 400/350/150/100. Edit widths by moving every later ceiling.
    // Loaded (tunables-ssot.md T1) — see ExpeditionTuningHub. −25% Atk per injury, on the omni
    // power channel, is InjuryPowerDivisor's shape (divide by 4).
    static ExpeditionEventRollTuning R => ExpeditionTuningHub.Tuning.EventRoll;
    static int QuietCeilMilli => R.QuietCeilMilli;
    static int FoundSoulsCeilMilli => R.FoundSoulsCeilMilli;
    static int WildCeilMilli => R.WildCeilMilli;
    static int WildJoinMilli => R.WildJoinMilli;
    static int ShinyDie => R.ShinyDie;
    static int InjuryPowerDivisor => R.InjuryPowerDivisor;

    public static ExpeditionResolution Resolve(
        string tierId, IReadOnlyList<BattleActorSetup> squad, ulong seed, int elapsedTicks)
    {
        var tier = ExpeditionTierCatalog.Get(tierId);
        if (squad.Count == 0) throw new ArgumentException("Squad is empty.");
        if (squad.Count > tier.SquadSlots) throw new ArgumentException("Squad exceeds tier slots.");
        var elapsed = Math.Clamp(elapsedTicks, 0, tier.TickCount);

        var battleTicks = BattleTickIndices(tier);
        var waveChain = WaveChain(tier);
        var (soulsMin, soulsMax) = SoulsRange(tier);

        var ticks = new List<ExpeditionTickOutcome>();
        var battles = new List<ExpeditionBattlePlan>();
        var materials = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var wildJoins = new List<WildJoinResult>();
        var injuries = new Dictionary<string, int>(StringComparer.Ordinal);
        long eventSouls = 0;
        var battleIndex = 0;

        for (var t = 1; t <= elapsed; t++)
        {
            if (battleTicks.TryGetValue(t, out var isBoss))
            {
                var waveId = isBoss ? BossWaveId : waveChain[Math.Min(battleIndex, waveChain.Length - 1)];
                var setup = new BattleSetup
                {
                    WaveId = waveId,
                    Squad = ApplyInjuries(squad, injuries),
                    Wave = WaveCatalog.Get(waveId).Enemies
                };
                var battleSeed = SeededRng.DeriveStream(seed, "battle:" + battleIndex).NextULong();
                battles.Add(new ExpeditionBattlePlan(battleIndex, t, isBoss, setup, battleSeed));
                // Shards drop at plan time, win or lose — the resolver never sees battle outcomes
                // (they resolve later at collect), and win-gating here would break the manifest's
                // determinism. Outcome-dependent rewards belong to the battle reports.
                materials.TryGetValue(isBoss ? ShardRare : ShardCommon, out var have);
                materials[isBoss ? ShardRare : ShardCommon] = have + 1;
                ticks.Add(new ExpeditionTickOutcome(
                    t, isBoss ? ExpeditionTickKinds.BossBattle : ExpeditionTickKinds.Battle,
                    battleIndex, 0, null, false, null, null));
                battleIndex++;
                continue;
            }

            var rng = SeededRng.DeriveStream(seed, "tick:" + t);
            var roll = rng.NextPerMille();
            if (roll < QuietCeilMilli)
            {
                ticks.Add(new ExpeditionTickOutcome(t, ExpeditionTickKinds.Quiet, -1, 0, null, false, null, null));
            }
            else if (roll < FoundSoulsCeilMilli)
            {
                var souls = soulsMin + rng.NextInt(soulsMax - soulsMin + 1);
                eventSouls += souls;
                ticks.Add(new ExpeditionTickOutcome(t, ExpeditionTickKinds.FoundSouls, -1, souls, null, false, null, null));
            }
            else if (roll < WildCeilMilli)
            {
                var species = RollWildSpecies(rng);
                var joins = rng.NextPerMille() < WildJoinMilli;
                var variant = rng.NextInt(ShinyDie) == 0 ? "shiny" : "normal";
                if (joins)
                {
                    // Traits roll here, on the tick's own stream — the whole rewards manifest is
                    // Core's (spec-expeditions §Resolver); splitting streams across layers invites
                    // an unseen name collision (2026-08-21 review I3).
                    wildJoins.Add(new WildJoinResult(species.SpeciesId, variant,
                        SummonRoller.RollTraits(species, species.BaseRarity, rng)));
                }
                else
                {
                    // The demon slips away but sheds a trace of its element.
                    var essence = "essence." + species.ElementPrimary.ToElementId();
                    materials.TryGetValue(essence, out var have);
                    materials[essence] = have + 1;
                }

                ticks.Add(new ExpeditionTickOutcome(
                    t, ExpeditionTickKinds.WildDemonMet, -1, 0, species.SpeciesId, joins,
                    joins ? variant : null, null));
            }
            else
            {
                var victim = squad[rng.NextInt(squad.Count)].Key;
                injuries.TryGetValue(victim, out var count);
                injuries[victim] = count + 1;
                ticks.Add(new ExpeditionTickOutcome(t, ExpeditionTickKinds.Injury, -1, 0, null, false, null, victim));
            }
        }

        // Greedy squad members raise the found-souls take (their battle-loot half rides the
        // battle reports' SoulLootMilli). Part of the manifest, so goldens cover it.
        var greedyBonus = TraitBattleCatalog.Get("greedy").SoulLootBonusMilli;
        var greedyCount = squad.Count(s => s.TraitIds.Contains("greedy", StringComparer.Ordinal));
        eventSouls = eventSouls * (1000 + greedyBonus * greedyCount) / 1000;

        return new ExpeditionResolution(
            tier.TierId, elapsed, ticks, battles,
            new ExpeditionRewards(
                eventSouls,
                materials.Select(kv => new MaterialDrop(kv.Key, kv.Value)).ToList(),
                wildJoins,
                XpPerBattleWon(tier)));
    }

    const string BossWaveId = "rift-tyrant";
    const string ShardCommon = "shard.common";
    const string ShardRare = "shard.rare";

    /// <summary>The tier's fixed battle schedule (index, tick, boss) — pure tier math, exposed so
    /// callers can reason about logged battles without resolving a timeline.</summary>
    public static IReadOnlyList<(int BattleIndex, int TickIndex, bool Boss)> BattleSchedule(ExpeditionTierDef tier)
    {
        var schedule = new List<(int, int, bool)>();
        var i = 0;
        foreach (var (tick, boss) in BattleTickIndices(tier).OrderBy(kv => kv.Key))
            schedule.Add((i++, tick, boss));
        return schedule;
    }

    /// <summary>Battle ticks evenly spaced at b·T/(B+1); the boss wave sits on the final tick.</summary>
    static Dictionary<int, bool> BattleTickIndices(ExpeditionTierDef tier)
    {
        var map = new Dictionary<int, bool>();
        for (var b = 1; b <= tier.BattleCount; b++)
            map[Math.Max(1, b * tier.TickCount / (tier.BattleCount + 1))] = false;
        if (tier.HasBossWave)
            map[tier.TickCount] = true;
        return map;
    }

    static string[] WaveChain(ExpeditionTierDef tier) => tier.TierId switch
    {
        "scout-30m" => new[] { "rift-skirmish" },
        "forage-4h" => new[] { "rift-skirmish", "rift-warband" },
        "hunt-8h" => new[] { "rift-skirmish", "rift-warband", "rift-onslaught" },
        "warpath-20h" => new[] { "rift-warband", "rift-onslaught", "rift-onslaught", "rift-tyrant" },
        _ => throw new ArgumentException($"No wave chain for tier '{tier.TierId}'.")
    };

    static (int Min, int Max) SoulsRange(ExpeditionTierDef tier) => tier.TierId switch
    {
        "scout-30m" => (5, 15),
        "forage-4h" => (20, 50),
        "hunt-8h" => (40, 90),
        _ => (80, 180)
    };

    static int XpPerBattleWon(ExpeditionTierDef tier) => tier.TierId switch
    {
        "scout-30m" => 10,
        "forage-4h" => 20,
        "hunt-8h" => 30,
        _ => 40
    };

    /// <summary>Wild pool: summonable-adjacent species only — never capture-exclusive, never
    /// legendary. Rarity bands 84/15/1 (‰-scaled) with fallback to common on an empty band.</summary>
    static DemonSpeciesDef RollWildSpecies(SeededRng rng)
    {
        var rarityRoll = rng.NextPerMille();
        var rarity = rarityRoll < 840 ? DemonRarity.Common
            : rarityRoll < 990 ? DemonRarity.Rare
            : DemonRarity.Epic;
        var band = WildBand(rarity);
        if (band.Count == 0) band = WildBand(DemonRarity.Common);
        return band[rng.NextInt(band.Count)];
    }

    static List<DemonSpeciesDef> WildBand(DemonRarity rarity) =>
        DemonSpeciesCatalog.All
            .Where(s => s.Acquisition != DemonAcquisition.CaptureOnly
                        && s.BaseRarity == rarity
                        && s.BaseRarity != DemonRarity.Legendary)
            .OrderBy(s => s.SpeciesId, StringComparer.Ordinal)
            .ToList();

    static IReadOnlyList<BattleActorSetup> ApplyInjuries(
        IReadOnlyList<BattleActorSetup> squad, Dictionary<string, int> injuries)
    {
        if (injuries.Count == 0) return squad;
        return squad.Select(s =>
        {
            if (!injuries.TryGetValue(s.Key, out var count) || count == 0) return s;
            var mods = s.ChannelMods.ToList();
            for (var i = 0; i < count; i++)
                mods.Add(new BattleChannelMod(
                    DerivedStatChannels.CombatPowerOmni, -Math.Max(1, s.Atk / InjuryPowerDivisor)));
            return s with { ChannelMods = mods };
        }).ToList();
    }
}
