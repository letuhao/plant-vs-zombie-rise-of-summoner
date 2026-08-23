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
