namespace FusionRpg.Core.Status;

/// <summary>Design defaults — actor-hub-ssot.md §5.</summary>
public static class StatusPolicy
{
    public const double CategoryResistCap = 0.95;
    public const double ApplyScaleK = 100.0;
    public const double ApplyScaleFloor = 1.0;
    public const double ResistFromPowerRatio = 0.0;
    public const double MinNetFactor = 0.0;
    public const double MaxNetFactor = 10_000.0;
    public const double ProgressionPowerStubDefault = 1.0;
    public const int ProcDepthLimitDefault = 6;

    /// <summary>P1: tierPower feeds ApplyScale and delta totals.</summary>
    public const bool IncludeTierPowerInDelta = true;

    public static double ApplyScaleKForCategory(string category) => ApplyScaleK;

    public static double ApplySteepnessForCategory(string category) => 1.0;
}

public static class StatusL2bCategory
{
    public const string Dot = "dot";
    public const string Cc = "cc";
    public const string Contagion = "contagion";
}

public enum StatusResistReason
{
    Immunity,
    PotencyFloor,
    ApplyRoll,
    UselessMagnitude,
    UnknownStatus,
    StatusIcd
}

public sealed record StatusApplyResult(
    bool Applied,
    StatusResistReason? ResistReason,
    double Delta,
    double NetFactor,
    double PApply,
    double PFinal,
    double EffectiveApplyScale,
    double EffectiveMagnitude,
    double EffectiveDuration);

public sealed record StatusApplyRequest(
    string StatusId,
    string HostPtr,
    string? AttackerPtr,
    double BaseMagnitude,
    double BaseDuration,
    double GrantChance = 1.0,
    bool AttackerLess = false,
    IReadOnlyList<string>? ImmunityTags = null);
