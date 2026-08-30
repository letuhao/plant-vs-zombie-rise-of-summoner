using FusionRpg.Core.Vfx;
using UnityEngine;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Snapshot of a unit or cell anchor+scale basis — resolved once per host per frame via
/// <see cref="UnitFrameResolver"/>. Primitives call <see cref="World"/> + <see cref="Span"/> only.
/// </summary>
public readonly struct VfxUnitFrame
{
    public float PivotX { get; init; }
    public float LaneY { get; init; }
    public float BoundsCenterX { get; init; }
    public float BoundsCenterY { get; init; }
    public float BoundsWidth { get; init; }
    public float BoundsHeight { get; init; }
    public float CellSpan { get; init; }
    public Vector2 CellSize { get; init; }
    public float DepthZ { get; init; }
    public bool HasBounds { get; init; }
    public int SortingOrderHint { get; init; }

    public Vector3 World(VfxAnchorKind kind)
    {
        var halfCell = CellSpan * 0.5f;
        var x = VfxUnitFrameMath.WorldX(PivotX, BoundsCenterX, HasBounds);
        var y = VfxUnitFrameMath.WorldY(LaneY, BoundsCenterY, halfCell, HasBounds, kind);
        return new Vector3(x, y, DepthZ);
    }

    public float Span(float recipeSizeScale = 1f) =>
        VfxSpanMath.ComputeSpan(
            CellSpan, BoundsWidth, BoundsHeight, HasBounds,
            (float)VfxTuningHub.Tuning.Sustained.SpanScale, recipeSizeScale);

    public int ParticleSortingOrder =>
        VfxTuningHub.Tuning.Render.ParticleSortingOrder + VfxTuningHub.Tuning.Render.SortOffsetAboveUnit;
}
