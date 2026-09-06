using FusionRpg.Core.Vfx;
using FusionRpg.Injector.Host;
using FusionRpg.Injector.Lawn;
using UnityEngine;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Bounds-aware unit frame resolution — vfx-ssot.md §9.1 / spec-unit-frame.md.
/// One read per Transform per frame; consumers must not call BodyWorld or bounds directly.
/// </summary>
public static class UnitFrameResolver
{
    static int _cacheFrame = -1;
    static readonly Dictionary<int, VfxUnitFrame> Cache = new();

    /// <summary>Resolve using the unit's board cell for span (plant col/row or zombie col/row).</summary>
    public static VfxUnitFrame Resolve(Transform follow)
    {
        var (col, row) = CellForTransform(follow);
        return Resolve(follow, col, row);
    }

    public static VfxUnitFrame Resolve(Transform follow, int col, int row)
    {
        EnsureFrameCache();
        var id = follow.GetInstanceID();
        if (Cache.TryGetValue(id, out var cached)) return cached;

        var frame = BuildUnitFrame(follow, col, row);
        Cache[id] = frame;
        return frame;
    }

    public static VfxUnitFrame ResolveCell(int col, int row)
    {
        var cellSize = EstimateCellSize(col, row);
        var cellSpan = Mathf.Max(VfxSpanMath.MinSpan, Mathf.Min(cellSize.x, cellSize.y));
        Vector3 center;
        try { center = LawnCoords.CellCenter(col, row); }
        catch { center = Vector3.zero; }

        return new VfxUnitFrame
        {
            PivotX = center.x,
            LaneY = center.y,
            BoundsCenterX = center.x,
            BoundsCenterY = center.y,
            BoundsWidth = 0f,
            BoundsHeight = 0f,
            CellSpan = cellSpan,
            CellSize = cellSize,
            DepthZ = center.z,
            HasBounds = false,
            SortingOrderHint = 0
        };
    }

    static (int col, int row) CellForTransform(Transform follow)
    {
        try
        {
            var plant = follow.GetComponent<Plant>() ?? follow.GetComponentInParent<Plant>();
            if (plant != null)
                return (LawnCoords.ClampCol(plant.thePlantColumn), LawnCoords.ClampRow(plant.thePlantRow));
        }
        catch { }

        try
        {
            var zombie = follow.GetComponent<Zombie>() ?? follow.GetComponentInParent<Zombie>();
            if (zombie != null)
            {
                var row = LawnCoords.ClampRow(zombie.theZombieRow);
                var col = CheatState.SpawnCol;
                try
                {
                    var fromX = LawnCoords.ColFromX(zombie.transform.position.x);
                    if (fromX >= 0) col = LawnCoords.ClampCol(fromX);
                }
                catch { }

                try
                {
                    var zCol = zombie.Column;
                    if (zCol >= 0) col = LawnCoords.ClampCol(zCol);
                }
                catch { }

                return (col, row);
            }
        }
        catch { }

        return (CheatState.SpawnCol, CheatState.SpawnRow);
    }

    static void EnsureFrameCache()
    {
        var fc = Time.frameCount;
        if (fc == _cacheFrame) return;
        _cacheFrame = fc;
        Cache.Clear();
    }

    static VfxUnitFrame BuildUnitFrame(Transform follow, int col, int row)
    {
        var cellSize = EstimateCellSize(col, row);
        var cellSpan = Mathf.Max(VfxSpanMath.MinSpan, Mathf.Min(cellSize.x, cellSize.y));

        Vector3 pivot;
        try { pivot = follow.position; }
        catch
        {
            return ResolveCell(col, row);
        }

        Vector3 body;
        try { body = LawnCoords.BodyWorld(follow); }
        catch { body = pivot; }

        var hasBounds = false;
        var boundsCenterX = body.x;
        var boundsCenterY = body.y;
        var boundsW = 0f;
        var boundsH = 0f;
        var sortingOrder = 0;

        // Prefer encapsulated SpriteRenderer bounds — a single first Renderer is often a tiny FX
        // mesh, so "bottom = center - h/2" collapsed to mid-body and HUD sat on faces (LIVE 2026-09-06).
        if (TryCollectSpriteBounds(follow, cellSpan, out var bx, out var by, out var bw, out var bh, out var sort))
        {
            hasBounds = true;
            boundsCenterX = bx;
            boundsCenterY = by;
            boundsW = bw;
            boundsH = bh;
            sortingOrder = sort;
        }

        return new VfxUnitFrame
        {
            PivotX = pivot.x,
            LaneY = body.y,
            BoundsCenterX = boundsCenterX,
            BoundsCenterY = boundsCenterY,
            BoundsWidth = boundsW,
            BoundsHeight = boundsH,
            CellSpan = cellSpan,
            CellSize = cellSize,
            DepthZ = pivot.z,
            HasBounds = hasBounds,
            SortingOrderHint = sortingOrder
        };
    }

    /// <summary>
    /// Largest enabled SpriteRenderer under <paramref name="follow"/> (by world area).
    /// First-child Renderer / full-tree encapsulate both failed LIVE: tiny FX → mid-body HUD;
    /// union with shadows/FX → inflated Span. Skips FusionRpg overlays and decoys
    /// (&lt; 35% cell on both axes, or taller than 2.5× cell).
    /// </summary>
    static bool TryCollectSpriteBounds(
        Transform follow, float cellSpan,
        out float centerX, out float centerY, out float width, out float height, out int sortingOrder)
    {
        centerX = centerY = width = height = 0f;
        sortingOrder = 0;
        try
        {
            var sprites = follow.GetComponentsInChildren<SpriteRenderer>(true);
            if (sprites == null) return false;

            var minAxis = cellSpan * 0.35f;
            var maxH = cellSpan * 2.5f;
            var bestArea = 0f;
            var best = default(Bounds);
            var bestOrder = 0;
            var found = false;

            foreach (var sr in sprites)
            {
                if (sr == null) continue;
                try { if (!sr.enabled) continue; } catch { continue; }
                try
                {
                    var n = sr.gameObject.name;
                    if (n != null && n.StartsWith("FusionRpg", StringComparison.Ordinal)) continue;
                }
                catch { }

                Bounds b;
                try { b = sr.bounds; }
                catch { continue; }
                if (b.size.sqrMagnitude < 1e-6f) continue;
                if (b.size.y < minAxis && b.size.x < minAxis) continue;
                if (b.size.y > maxH) continue;

                var area = b.size.x * b.size.y;
                if (area <= bestArea) continue;
                bestArea = area;
                best = b;
                found = true;
                try { bestOrder = sr.sortingOrder; } catch { bestOrder = 0; }
            }

            if (!found) return false;

            centerX = best.center.x;
            centerY = best.center.y;
            width = best.size.x;
            height = best.size.y;
            sortingOrder = bestOrder;
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static Vector2 EstimateCellSize(int col, int row)
    {
        try
        {
            var a = LawnCoords.CellCenter(col, row);
            var col2 = col >= LawnCoords.LastCol ? Math.Max(0, col - 1) : col + 1;
            var row2 = row >= LawnCoords.LastRow ? Math.Max(0, row - 1) : row + 1;
            var b = LawnCoords.CellCenter(col2, row);
            var c = LawnCoords.CellCenter(col, row2);
            var w = Mathf.Abs(b.x - a.x);
            var h = Mathf.Abs(c.y - a.y);
            if (w < 0.05f) w = 1f;
            if (h < 0.05f) h = 1f;
            return new Vector2(w, h);
        }
        catch
        {
            return Vector2.one;
        }
    }
}
