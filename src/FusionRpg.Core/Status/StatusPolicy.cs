namespace FusionRpg.Core.Status;

/// <summary>Design defaults — actor-hub-ssot.md §5.</summary>
public static class StatusPolicy
{
    static StatusTuning? _tuning;

    /// <summary>Host-only (Injector/Server startup, or a test's inline construction).</summary>
    public static void Configure(StatusTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static StatusTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "StatusPolicy.Configure(...) has not run. Every status rule reads data/tuning/" +
        "status.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");

    public static double CategoryResistCap => Tuning.CategoryResistCap;
    public static double ApplyScaleK => Tuning.ApplyScaleK;
    public static double ApplyScaleFloor => Tuning.ApplyScaleFloor;
    public static double ResistFromPowerRatio => Tuning.ResistFromPowerRatio;
    public static double MinNetFactor => Tuning.MinNetFactor;
    public static double MaxNetFactor => Tuning.MaxNetFactor;
    public static double ProgressionPowerStubDefault => Tuning.ProgressionPowerStubDefault;
    public static int ProcDepthLimitDefault => Tuning.ProcDepthLimitDefault;

    /// <summary>P1: tierPower feeds ApplyScale and delta totals.</summary>
    public const bool IncludeTierPowerInDelta = true;

    public static double ApplyScaleKForCategory(string category) => ApplyScaleK;

    public static double ApplySteepnessForCategory(string category) => Tuning.ApplySteepnessDefault;
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
