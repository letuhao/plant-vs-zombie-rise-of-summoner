namespace FusionRpg.Core.Stats.Derived;

/// <summary>Known derived channel id patterns — see actor-hub-ssot.md §3.</summary>
public static class DerivedStatChannels
{
    public const string ProgressionBonusMaxHp = "progression.bonus.maxHp";
    public const string ProgressionBonusAtk = "progression.bonus.atk";
    public const string ProgressionBonusDefense = "progression.bonus.defense";
    public const string ProgressionBonusArm1 = "progression.bonus.arm1";
    public const string ProgressionBonusArm2 = "progression.bonus.arm2";

    public const string ProgressionPower = "progression.power";
    public const string ProgressionRealm = "progression.realm";

    public const string StatusPowerOmni = "status.power.omni";
    public const string StatusPowerDot = "status.power.dot";
    public const string StatusPowerCc = "status.power.cc";
    public const string StatusPowerContagion = "status.power.contagion";

    public const string StatusResistOmni = "status.resist.omni";
    public const string StatusResistDot = "status.resist.dot";
    public const string StatusResistCc = "status.resist.cc";
    public const string StatusResistContagion = "status.resist.contagion";

    public static string StatusPower(string statusId) => $"status.power.{statusId}";
    public static string StatusResist(string statusId) => $"status.resist.{statusId}";
    public static string StatusImmune(string tag) => $"status.immune.{tag}";
    public static string StatusImmuneReduction(string tag) => $"status.immuneReduction.{tag}";
    public static string StatusExpose(string category) => $"status.expose.{category}";

    public const string CombatPowerOmni = "combat.power.omni";
    public const string CombatPowerFire = "combat.power.fire";
    public const string CombatPowerIce = "combat.power.ice";
    public const string CombatPowerAir = "combat.power.air";
    public const string CombatPowerEarth = "combat.power.earth";

    public const string CombatDefenseOmni = "combat.defense.omni";
    public const string CombatDefenseFire = "combat.defense.fire";
    public const string CombatDefenseIce = "combat.defense.ice";
    public const string CombatDefenseAir = "combat.defense.air";
    public const string CombatDefenseEarth = "combat.defense.earth";

    public const string CombatCritRateOmni = "combat.crit.rate.omni";
    public const string CombatCritRateFire = "combat.crit.rate.fire";
    public const string CombatCritRateIce = "combat.crit.rate.ice";
    public const string CombatCritRateAir = "combat.crit.rate.air";
    public const string CombatCritRateEarth = "combat.crit.rate.earth";

    public const string CombatCritResistOmni = "combat.crit.resist.omni";
    public const string CombatCritResistFire = "combat.crit.resist.fire";
    public const string CombatCritResistIce = "combat.crit.resist.ice";
    public const string CombatCritResistAir = "combat.crit.resist.air";
    public const string CombatCritResistEarth = "combat.crit.resist.earth";

    public const string CombatCritDamageOmni = "combat.crit.damage.omni";
    public const string CombatCritDamageFire = "combat.crit.damage.fire";
    public const string CombatCritDamageIce = "combat.crit.damage.ice";
    public const string CombatCritDamageAir = "combat.crit.damage.air";
    public const string CombatCritDamageEarth = "combat.crit.damage.earth";

    public const string CombatCritResistDamageOmni = "combat.crit.resist.damage.omni";
    public const string CombatCritResistDamageFire = "combat.crit.resist.damage.fire";
    public const string CombatCritResistDamageIce = "combat.crit.resist.damage.ice";
    public const string CombatCritResistDamageAir = "combat.crit.resist.damage.air";
    public const string CombatCritResistDamageEarth = "combat.crit.resist.damage.earth";

    public const string CombatAccuracyOmni = "combat.accuracy.omni";
    public const string CombatAccuracyFire = "combat.accuracy.fire";
    public const string CombatAccuracyIce = "combat.accuracy.ice";
    public const string CombatAccuracyAir = "combat.accuracy.air";
    public const string CombatAccuracyEarth = "combat.accuracy.earth";

    public const string CombatDodgeOmni = "combat.dodge.omni";
    public const string CombatDodgeFire = "combat.dodge.fire";
    public const string CombatDodgeIce = "combat.dodge.ice";
    public const string CombatDodgeAir = "combat.dodge.air";
    public const string CombatDodgeEarth = "combat.dodge.earth";

    /// <summary>All v1 overlay combat derived channels — see element-hub-ssot.md §6.</summary>
    public static readonly IReadOnlyList<string> AllCombatChannelIds = new[]
    {
        CombatPowerOmni,
        CombatPowerFire,
        CombatPowerIce,
        CombatPowerAir,
        CombatPowerEarth,
        CombatDefenseOmni,
        CombatDefenseFire,
        CombatDefenseIce,
        CombatDefenseAir,
        CombatDefenseEarth,
        CombatCritRateOmni,
        CombatCritRateFire,
        CombatCritRateIce,
        CombatCritRateAir,
        CombatCritRateEarth,
        CombatCritResistOmni,
        CombatCritResistFire,
        CombatCritResistIce,
        CombatCritResistAir,
        CombatCritResistEarth,
        CombatCritDamageOmni,
        CombatCritDamageFire,
        CombatCritDamageIce,
        CombatCritDamageAir,
        CombatCritDamageEarth,
        CombatCritResistDamageOmni,
        CombatCritResistDamageFire,
        CombatCritResistDamageIce,
        CombatCritResistDamageAir,
        CombatCritResistDamageEarth,
        CombatAccuracyOmni,
        CombatAccuracyFire,
        CombatAccuracyIce,
        CombatAccuracyAir,
        CombatAccuracyEarth,
        CombatDodgeOmni,
        CombatDodgeFire,
        CombatDodgeIce,
        CombatDodgeAir,
        CombatDodgeEarth
    };
}

/// <summary>Actor element type metadata field names — see element-hub-ssot.md §5.</summary>
public static class ElementMetadataKeys
{
    public const string Primary = "element.type.primary";
    public const string Secondary = "element.type.secondary";
}
