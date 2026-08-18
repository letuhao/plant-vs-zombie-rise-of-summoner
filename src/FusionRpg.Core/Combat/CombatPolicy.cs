namespace FusionRpg.Core.Combat;

/// <summary>
/// Match/runtime combat knobs. Defaults live here so resolver/dispatcher never hardcode literals.
/// Override per match or grant overlay (<c>procDepthLimit</c>).
/// </summary>
public sealed class CombatPolicy
{
    public static CombatPolicy Default { get; } = new();

    public int ProcDepthLimit { get; set; } = 6;
    public int DefaultMaxTargets { get; set; } = 8;
    public int AreaDefaultSquareSize { get; set; } = 3;
    public int AreaDefaultRectangleWidth { get; set; } = 3;
    public int AreaDefaultRectangleHeight { get; set; } = 3;
    public int LastCol { get; set; } = Lawn.LawnCoordMath.DefaultLastCol;
    public int LastRow { get; set; } = Lawn.LawnCoordMath.DefaultLastRow;
    public int DotDefaultPeriodMs { get; set; } = 1000;
    public int DotDefaultDurationMs { get; set; } = 5000;

    public int ResolveDotPeriodMs(int? overlay) =>
        overlay is > 0 ? overlay.Value : DotDefaultPeriodMs;

    public int ResolveDotDurationMs(int? overlay) =>
        overlay is > 0 ? overlay.Value : DotDefaultDurationMs;

    public int ResolveProcDepthLimit(int? overlayOverride) =>
        overlayOverride is > 0 ? overlayOverride.Value : ProcDepthLimit;

    public int ResolveMaxTargets(int? overlay) =>
        overlay is > 0 ? overlay.Value : DefaultMaxTargets;
}
