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

    // CategoryResistCap moved to Stats.Derived.DerivedStatPolicy (cap-consolidation, T1, 2026-08-24) —
    // it was clamped a second time here after DerivedComposer already applied it, making a raised
    // tunable a silent no-op. One key now: data/tuning/derived-stats.v1.json.
    public static double ApplyScaleK => Tuning.ApplyScaleK;
    public static double ApplyScaleFloor => Tuning.ApplyScaleFloor;
    public static double ResistFromPowerRatio => Tuning.ResistFromPowerRatio;
    public static double MinNetFactor => Tuning.MinNetFactor;
    public static double MaxNetFactor => Tuning.MaxNetFactor;
    /// <summary>T3.2 (audit F4): netFactor = 1 + delta/NetFactorScale — linear, no cliff.</summary>
    public static double NetFactorScale => Tuning.NetFactorScale;
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

/// <summary>
/// <c>Delta</c>/<c>NetFactor</c> stay Phase 1's apply-chance delta and its net factor — untouched by
/// spec-status-potency.md's split (§2.1: "Phase 1 is untouched"). <c>DurationNetFactor</c> and
/// <c>IntensityNetFactor</c> are the NEW, independent Phase 2 values that actually drive
/// <c>EffectiveDuration</c>/<c>EffectiveMagnitude</c> now — the potency floor
/// (<see cref="StatusResistReason.PotencyFloor"/>) checks <c>IntensityNetFactor</c> only (§2.2: a
/// zero-duration status is instantaneous, a legitimate effect, not a resist).
/// </summary>
public sealed record StatusApplyResult(
    bool Applied,
    StatusResistReason? ResistReason,
    double Delta,
    double NetFactor,
    double PApply,
    double PFinal,
    double EffectiveApplyScale,
    double EffectiveMagnitude,
    double EffectiveDuration,
    double DurationNetFactor = 0,
    double IntensityNetFactor = 0);

public sealed record StatusApplyRequest(
    string StatusId,
    string HostPtr,
    string? AttackerPtr,
    double BaseMagnitude,
    double BaseDuration,
    double GrantChance = 1.0,
    bool AttackerLess = false,
    IReadOnlyList<string>? ImmunityTags = null);
