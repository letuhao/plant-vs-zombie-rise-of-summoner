using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle;

/// <summary>
/// How a trait acts in web battles. Funnel-routed traits are stat modifiers and HP mutations on
/// the battle funnel; engine behaviors (targeting, retreat, report multipliers) are engine
/// semantics outside the FA vocabulary BY DESIGN — FA opcodes describe board operations, not
/// battle AI (demon-standalone-plan.md §Refinement). Contracts later layers obedience on the
/// same trait keys.
/// </summary>
public enum TraitBattleMechanism
{
    FunnelRouted,
    EngineBehavior
}

/// <summary>One trait's battle semantics — integer per-mille params; 0 = facet unused.</summary>
public sealed record TraitBattleDef
{
    public string TraitId { get; init; } = "";
    public TraitBattleMechanism Mechanism { get; init; }

    /// <summary>Static derived-channel adjustments merged at compose time (e.g. crit rate).</summary>
    public IReadOnlyList<BattleChannelMod> ChannelMods { get; init; } = Array.Empty<BattleChannelMod>();

    public int BerserkRampHalfMilli { get; init; }      // extra damage below 50% own HP
    public int BerserkRampQuarterMilli { get; init; }   // extra damage below 25% own HP
    public int RegenPerRoundMilli { get; init; }        // of MaxHp, healed each round
    public int OnKillHealMilli { get; init; }           // of MaxHp, healed per kill
    public int GuardShareMilli { get; init; }           // damage share pulled off an adjacent ally
    public int InitiativeBonusMilli { get; init; }      // subtracted from the initiative roll
    public int DeathRefusalCharges { get; init; }       // survive-at-1 charges per battle
    public int RetreatBelowMilli { get; init; }         // of MaxHp — leave the battle alive below this
    public bool TargetsLowestHp { get; init; }
    public bool GuardsAdjacentAlly { get; init; }       // full redirect of hits aimed at the neighbor
    public int SoulLootBonusMilli { get; init; }        // battle-level Souls multiplier (report)
    public int SpecimenXpBonusMilli { get; init; }      // per-actor XP multiplier (report)
    public int EssenceProcMilli { get; init; }          // rider proc chance per landed hit
    public int EssenceRiderMilli { get; init; }         // rider damage, ‰ of the landed hit
}

public static class TraitBattleMath
{
    /// <summary>Berserker damage multiplier (‰) from own HP — staged, integer-only.</summary>
    public static int BerserkerRampMilli(TraitBattleDef def, int hp, int maxHp)
    {
        if (maxHp <= 0) return 1000;
        if (hp * 4 < maxHp) return 1000 + def.BerserkRampQuarterMilli;
        if (hp * 2 < maxHp) return 1000 + def.BerserkRampHalfMilli;
        return 1000;
    }
}

public static class TraitBattleCatalog
{
    public static readonly IReadOnlyList<TraitBattleDef> All = new TraitBattleDef[]
    {
        // Funnel-routed — stat mods + HP mutations through the battle-local EffectFunnel.
        new() { TraitId = "berserker", Mechanism = TraitBattleMechanism.FunnelRouted, BerserkRampHalfMilli = 250, BerserkRampQuarterMilli = 500 },
        new() { TraitId = "regenerator", Mechanism = TraitBattleMechanism.FunnelRouted, RegenPerRoundMilli = 20 },
        new() { TraitId = "soul-eater", Mechanism = TraitBattleMechanism.FunnelRouted, OnKillHealMilli = 100 },
        new()
        {
            TraitId = "critical-hunter", Mechanism = TraitBattleMechanism.FunnelRouted,
            // Re-costed at battle-adoption (owner decision 5): sigmoid points, not per-mille.
            // +150 over the −250 parity baseline → σ(−1.0) ≈ 26.9% crit (vs 7.6% base).
            ChannelMods = new[] { new BattleChannelMod(DerivedStatChannels.CombatCritRateOmni, 150) }
        },
        new() { TraitId = "guardian", Mechanism = TraitBattleMechanism.FunnelRouted, GuardShareMilli = 250 },
        // swift/berserker sit in the Funnel-routed HALF of the plan's locked 7/7 split even
        // though their mechanics run engine-side (initiative math, damage multiplier) — the
        // split classifies which traits the contracts module later layers obedience onto,
        // not the literal code path of every parameter.
        new() { TraitId = "swift", Mechanism = TraitBattleMechanism.FunnelRouted, InitiativeBonusMilli = 1000 },
        new() { TraitId = "immortal", Mechanism = TraitBattleMechanism.FunnelRouted, DeathRefusalCharges = 1 },

        // Engine-native behaviors — outside the FA vocabulary by design.
        new() { TraitId = "coward", Mechanism = TraitBattleMechanism.EngineBehavior, RetreatBelowMilli = 250 },
        new() { TraitId = "bloodthirsty", Mechanism = TraitBattleMechanism.EngineBehavior, TargetsLowestHp = true },
        new() { TraitId = "loyal", Mechanism = TraitBattleMechanism.EngineBehavior, GuardsAdjacentAlly = true },
        new() { TraitId = "greedy", Mechanism = TraitBattleMechanism.EngineBehavior, SoulLootBonusMilli = 250 },
        new() { TraitId = "genius", Mechanism = TraitBattleMechanism.EngineBehavior, SpecimenXpBonusMilli = 250 },
        new() { TraitId = "void-touched", Mechanism = TraitBattleMechanism.EngineBehavior, EssenceProcMilli = 100, EssenceRiderMilli = 150 },
        new() { TraitId = "chaos-marked", Mechanism = TraitBattleMechanism.EngineBehavior, EssenceProcMilli = 100, EssenceRiderMilli = 150 }
    };

    static readonly Dictionary<string, TraitBattleDef> ById =
        All.ToDictionary(t => t.TraitId, StringComparer.Ordinal);

    public static bool IsKnown(string? traitId) => traitId != null && ById.ContainsKey(traitId);

    public static TraitBattleDef Get(string traitId) =>
        ById.TryGetValue(traitId, out var def)
            ? def
            : throw new ArgumentException($"Unknown battle trait id '{traitId}'.");
}
