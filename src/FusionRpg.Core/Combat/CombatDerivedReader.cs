using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Combat;

/// <summary>Read typed combat derived channels — omni + element additive rule.</summary>
public static class CombatDerivedReader
{
    public static double Power(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatPowerOmni) + snap.Get(PowerChannel(element));

    public static double Defense(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatDefenseOmni) + snap.Get(DefenseChannel(element));

    public static double Accuracy(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatAccuracyOmni) + snap.Get(AccuracyChannel(element));

    public static double Dodge(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatDodgeOmni) + snap.Get(DodgeChannel(element));

    public static double CritRate(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatCritRateOmni) + snap.Get(CritRateChannel(element));

    public static double CritResist(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatCritResistOmni) + snap.Get(CritResistChannel(element));

    public static double CritDamage(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatCritDamageOmni) + snap.Get(CritDamageChannel(element));

    public static double CritResistDamage(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatCritResistDamageOmni) + snap.Get(CritResistDamageChannel(element));

    // T5.1 (spec-mitigation-chain.md §2): penetration/absorption scale defense inside the delta.
    // Contest, paired, neither half capped — the omni + element additive rule applies exactly like
    // Power/Defense above.
    public static double Penetration(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatPenetrationOmni) + snap.Get(DerivedStatChannels.CombatPenetration(element));

    public static double Absorption(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatAbsorptionOmni) + snap.Get(DerivedStatChannels.CombatAbsorption(element));

    // amplification/reduction (spec-mitigation-chain.md §2.3) apply ONCE to the already-summed final
    // damage, not per component -- but reading omni+element here and weight-accumulating across
    // components in the SAME loop as weightedDelta produces the identical result, since component
    // weights sum to 1.0 (ElementPayload.Validate): Sigma(w * omni) = omni * Sigma(w) = omni. No
    // separate "add omni once" code path needed.
    public static double Amplification(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatAmplificationOmni) + snap.Get(DerivedStatChannels.CombatAmplification(element));

    public static double Reduction(ActorDerivedSnapshot snap, ElementTypeId element) =>
        snap.Get(DerivedStatChannels.CombatReductionOmni) + snap.Get(DerivedStatChannels.CombatReduction(element));

    // T5.3 (spec-evasion-chain.md §3): parry/block are role-inverted Contest pairs, resolved OMNI
    // ONLY -- the spec never describes a per-component breakdown for them, and §7 explicitly bans
    // reading ShieldElementMatrix ("block is not a shield"). The per-element slots H.1's generator
    // still builds stay registered and unread, the same honest partial-wiring every other
    // not-yet-consumed sparse slot in this catalog already has.
    public static double ParryRate(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatParryRateOmni);
    public static double ParryBreak(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatParryBreakOmni);
    public static double ParryStrength(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatParryStrengthOmni);
    public static double ParryShred(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatParryShredOmni);
    public static double BlockRate(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatBlockRateOmni);
    public static double BlockBreak(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatBlockBreakOmni);
    public static double BlockStrength(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatBlockStrengthOmni);
    public static double BlockShred(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatBlockShredOmni);

    // T5.4 (spec-reflection.md §3): reflection reads finalDamage (post-mitigation) -- omni only, same
    // reasoning as parry/block.
    public static double ReflectRate(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatReflectRateOmni);
    public static double ReflectResistRate(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatReflectResistRateOmni);
    public static double ReflectDamage(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatReflectDamageOmni);
    public static double ReflectResistDamage(ActorDerivedSnapshot snap) => snap.Get(DerivedStatChannels.CombatReflectResistDamageOmni);

    // Shield families (shield-system-spec.md §2.3): element is nullable — an untyped shield
    // (element = none) reads the omni half only. Channel ids are roster-generated, so these
    // never fall through a hand-maintained switch.
    public static double ShieldCapacity(ActorDerivedSnapshot snap, ElementTypeId? element) =>
        snap.Get(DerivedStatChannels.CombatShieldCapacityOmni)
        + (element is { } e ? snap.Get(DerivedStatChannels.CombatShieldCapacity(e)) : 0);

    public static double ShieldToughness(ActorDerivedSnapshot snap, ElementTypeId? element) =>
        snap.Get(DerivedStatChannels.CombatShieldToughnessOmni)
        + (element is { } e ? snap.Get(DerivedStatChannels.CombatShieldToughness(e)) : 0);

    public static double ShieldPen(ActorDerivedSnapshot snap, ElementTypeId? element) =>
        snap.Get(DerivedStatChannels.CombatShieldPenOmni)
        + (element is { } e ? snap.Get(DerivedStatChannels.CombatShieldPen(e)) : 0);

    public static double ShieldRegen(ActorDerivedSnapshot snap, ElementTypeId? element) =>
        snap.Get(DerivedStatChannels.CombatShieldRegenOmni)
        + (element is { } e ? snap.Get(DerivedStatChannels.CombatShieldRegen(e)) : 0);

    static string PowerChannel(ElementTypeId e) => e switch
    {
        ElementTypeId.Fire => DerivedStatChannels.CombatPowerFire,
        ElementTypeId.Ice => DerivedStatChannels.CombatPowerIce,
        ElementTypeId.Air => DerivedStatChannels.CombatPowerAir,
        ElementTypeId.Earth => DerivedStatChannels.CombatPowerEarth,
        ElementTypeId.Light => DerivedStatChannels.CombatPowerLight,
        ElementTypeId.Dark => DerivedStatChannels.CombatPowerDark,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };

    static string DefenseChannel(ElementTypeId e) => e switch
    {
        ElementTypeId.Fire => DerivedStatChannels.CombatDefenseFire,
        ElementTypeId.Ice => DerivedStatChannels.CombatDefenseIce,
        ElementTypeId.Air => DerivedStatChannels.CombatDefenseAir,
        ElementTypeId.Earth => DerivedStatChannels.CombatDefenseEarth,
        ElementTypeId.Light => DerivedStatChannels.CombatDefenseLight,
        ElementTypeId.Dark => DerivedStatChannels.CombatDefenseDark,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };

    static string AccuracyChannel(ElementTypeId e) => e switch
    {
        ElementTypeId.Fire => DerivedStatChannels.CombatAccuracyFire,
        ElementTypeId.Ice => DerivedStatChannels.CombatAccuracyIce,
        ElementTypeId.Air => DerivedStatChannels.CombatAccuracyAir,
        ElementTypeId.Earth => DerivedStatChannels.CombatAccuracyEarth,
        ElementTypeId.Light => DerivedStatChannels.CombatAccuracyLight,
        ElementTypeId.Dark => DerivedStatChannels.CombatAccuracyDark,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };

    static string DodgeChannel(ElementTypeId e) => e switch
    {
        ElementTypeId.Fire => DerivedStatChannels.CombatDodgeFire,
        ElementTypeId.Ice => DerivedStatChannels.CombatDodgeIce,
        ElementTypeId.Air => DerivedStatChannels.CombatDodgeAir,
        ElementTypeId.Earth => DerivedStatChannels.CombatDodgeEarth,
        ElementTypeId.Light => DerivedStatChannels.CombatDodgeLight,
        ElementTypeId.Dark => DerivedStatChannels.CombatDodgeDark,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };

    static string CritRateChannel(ElementTypeId e) => e switch
    {
        ElementTypeId.Fire => DerivedStatChannels.CombatCritRateFire,
        ElementTypeId.Ice => DerivedStatChannels.CombatCritRateIce,
        ElementTypeId.Air => DerivedStatChannels.CombatCritRateAir,
        ElementTypeId.Earth => DerivedStatChannels.CombatCritRateEarth,
        ElementTypeId.Light => DerivedStatChannels.CombatCritRateLight,
        ElementTypeId.Dark => DerivedStatChannels.CombatCritRateDark,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };

    static string CritResistChannel(ElementTypeId e) => e switch
    {
        ElementTypeId.Fire => DerivedStatChannels.CombatCritResistFire,
        ElementTypeId.Ice => DerivedStatChannels.CombatCritResistIce,
        ElementTypeId.Air => DerivedStatChannels.CombatCritResistAir,
        ElementTypeId.Earth => DerivedStatChannels.CombatCritResistEarth,
        ElementTypeId.Light => DerivedStatChannels.CombatCritResistLight,
        ElementTypeId.Dark => DerivedStatChannels.CombatCritResistDark,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };

    static string CritDamageChannel(ElementTypeId e) => e switch
    {
        ElementTypeId.Fire => DerivedStatChannels.CombatCritDamageFire,
        ElementTypeId.Ice => DerivedStatChannels.CombatCritDamageIce,
        ElementTypeId.Air => DerivedStatChannels.CombatCritDamageAir,
        ElementTypeId.Earth => DerivedStatChannels.CombatCritDamageEarth,
        ElementTypeId.Light => DerivedStatChannels.CombatCritDamageLight,
        ElementTypeId.Dark => DerivedStatChannels.CombatCritDamageDark,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };

    static string CritResistDamageChannel(ElementTypeId e) => e switch
    {
        ElementTypeId.Fire => DerivedStatChannels.CombatCritResistDamageFire,
        ElementTypeId.Ice => DerivedStatChannels.CombatCritResistDamageIce,
        ElementTypeId.Air => DerivedStatChannels.CombatCritResistDamageAir,
        ElementTypeId.Earth => DerivedStatChannels.CombatCritResistDamageEarth,
        ElementTypeId.Light => DerivedStatChannels.CombatCritResistDamageLight,
        ElementTypeId.Dark => DerivedStatChannels.CombatCritResistDamageDark,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };
}
