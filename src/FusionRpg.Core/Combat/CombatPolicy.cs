namespace FusionRpg.Core.Combat;

/// <summary>
/// Match/runtime combat knobs. Defaults live here so resolver/dispatcher never hardcode literals.
/// Override per match or grant overlay (<c>procDepthLimit</c>).
/// </summary>
public sealed class CombatPolicy
{
    public static CombatPolicy Default { get; } = new();

    /// <summary>
    /// Host-only (Injector/Server startup, or a test's inline construction) — sets <see cref="Default"/>'s
    /// balance-surface properties from data/tuning/combat.v1.json (tunables-ssot.md T1). Per-match /
    /// grant overlays still mutate <see cref="Default"/> or a caller's own instance exactly as before;
    /// this only changes what the baseline starts as.
    /// </summary>
    public static void Configure(CombatTuning tuning)
    {
        if (tuning == null) throw new ArgumentNullException(nameof(tuning));
        Default.ProcDepthLimit = tuning.ProcDepthLimit;
        Default.DefaultMaxTargets = tuning.DefaultMaxTargets;
        Default.AreaDefaultSquareSize = tuning.AreaDefaultSquareSize;
        Default.AreaDefaultRectangleWidth = tuning.AreaDefaultRectangleWidth;
        Default.AreaDefaultRectangleHeight = tuning.AreaDefaultRectangleHeight;
        Default.DotDefaultPeriodMs = tuning.DotDefaultPeriodMs;
        Default.DotDefaultDurationMs = tuning.DotDefaultDurationMs;
        Default.PierceScale = tuning.PierceScale;
        Default.AmpScale = tuning.AmpScale;
        Default.BlockCapPermille = tuning.BlockCapPermille;
        Default.ParryCapPermille = tuning.ParryCapPermille;
        Default.AvoidanceBandCapPermille = tuning.AvoidanceBandCapPermille;
        Default.ReflectRateScale = tuning.ReflectRateScale;
        Default.ReflectShareScale = tuning.ReflectShareScale;
    }

    // No inline defaults (tunables-ssot.md T5 — no built-in default to fall back to): the only
    // shared instance is Default, and Configure(...) runs at host startup before any real consumer
    // reads it, the same guarantee every other migrated Policy class relies on. A caller that
    // constructs its own CombatPolicy for a one-off override (tests) copies Default's already-loaded
    // values rather than relying on a redundant, hard-coded second copy of the balance surface here.
    public int ProcDepthLimit { get; set; }
    public int DefaultMaxTargets { get; set; }
    public int AreaDefaultSquareSize { get; set; }
    public int AreaDefaultRectangleWidth { get; set; }
    public int AreaDefaultRectangleHeight { get; set; }
    public int LastCol { get; set; } = Lawn.LawnCoordMath.DefaultLastCol;
    public int LastRow { get; set; } = Lawn.LawnCoordMath.DefaultLastRow;
    public int DotDefaultPeriodMs { get; set; }
    public int DotDefaultDurationMs { get; set; }

    /// <summary>
    /// spec-mitigation-chain.md §5 (T5.1) — shapes only, chosen so both factors are identity at
    /// delta 0 for ANY positive scale (the property does not depend on the specific value): reused
    /// StatusPolicy.NetFactorScale's own shape (10.0) rather than inventing a fresh, ungrounded
    /// number — a balance VALUE for either scale is a separate, later pass (§7 "Ask first").
    /// </summary>
    public double PierceScale { get; set; }
    public double AmpScale { get; set; }

    /// <summary>
    /// spec-evasion-chain.md §2.1/§3.1 (T5.3) — bounded ratios, PS-8 exempt: mitigation may not reach
    /// total. <see cref="BlockCapPermille"/>/<see cref="ParryCapPermille"/> bound the FRACTION OF ONE
    /// HIT a single block/parry may remove (a block removes at most 95%, never 100% — immunity
    /// impossible by construction, the ceiling-side mirror of the shield's floor-side guarantee).
    /// <see cref="AvoidanceBandCapPermille"/> bounds the SHARE OF ONE ROLL miss+parry+block together
    /// may occupy — an attack always retains its own independent ≥5% chance to land. Same constant
    /// (950) and reasoning as <c>StatusPolicy.CategoryResistCap</c> (0.95); own tuning keys, since
    /// they merely AGREE with it today rather than depend on it. `parry.rate`/`block.rate` etc.
    /// themselves stay uncapped magnitudes — what is bounded is the exchange, never the stats.
    /// </summary>
    public long BlockCapPermille { get; set; }
    public long ParryCapPermille { get; set; }
    public long AvoidanceBandCapPermille { get; set; }

    /// <summary>
    /// spec-reflection.md §3 (T5.4) — shapes only, same reasoning as PierceScale/AmpScale: both
    /// formulas are LINEAR from zero (<c>max(0,delta)/scale</c>, clamped to [0,1]), not the spec's
    /// own sigmoid sketch — a sigmoid gives 0.5 at delta=0, which would hand every actor a 50%
    /// reflect chance before any content authors reflect.rate, contradicting NoGoldensMoveAtZero
    /// (the same reasoning already applied to parry/block's rate, T5.3). Reused
    /// StatusPolicy.NetFactorScale's own shape value (10.0) rather than a fresh, ungrounded number.
    /// </summary>
    public double ReflectRateScale { get; set; }
    public double ReflectShareScale { get; set; }

    /// <summary>Copies every property from <see cref="Default"/> — the safe starting point for a
    /// one-off override, so a caller changing one field never silently zeroes the rest.</summary>
    public static CombatPolicy FromDefault() => new()
    {
        ProcDepthLimit = Default.ProcDepthLimit,
        DefaultMaxTargets = Default.DefaultMaxTargets,
        AreaDefaultSquareSize = Default.AreaDefaultSquareSize,
        AreaDefaultRectangleWidth = Default.AreaDefaultRectangleWidth,
        AreaDefaultRectangleHeight = Default.AreaDefaultRectangleHeight,
        LastCol = Default.LastCol,
        LastRow = Default.LastRow,
        DotDefaultPeriodMs = Default.DotDefaultPeriodMs,
        DotDefaultDurationMs = Default.DotDefaultDurationMs,
        PierceScale = Default.PierceScale,
        AmpScale = Default.AmpScale,
        BlockCapPermille = Default.BlockCapPermille,
        ParryCapPermille = Default.ParryCapPermille,
        AvoidanceBandCapPermille = Default.AvoidanceBandCapPermille,
        ReflectRateScale = Default.ReflectRateScale,
        ReflectShareScale = Default.ReflectShareScale,
    };

    public int ResolveDotPeriodMs(int? overlay) =>
        overlay is > 0 ? overlay.Value : DotDefaultPeriodMs;

    public int ResolveDotDurationMs(int? overlay) =>
        overlay is > 0 ? overlay.Value : DotDefaultDurationMs;

    public int ResolveProcDepthLimit(int? overlayOverride) =>
        overlayOverride is > 0 ? overlayOverride.Value : ProcDepthLimit;

    public int ResolveMaxTargets(int? overlay) =>
        overlay is > 0 ? overlay.Value : DefaultMaxTargets;
}
