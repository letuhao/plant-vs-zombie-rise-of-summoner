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

    public const string CombatCritChance = "combat.crit.chance";
}
