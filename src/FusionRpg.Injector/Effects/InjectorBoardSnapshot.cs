using FusionRpg.Core.Combat;
using FusionRpg.Core.Diagnostics;
using FusionRpg.Injector.Lawn;
using UnityEngine;

namespace FusionRpg.Injector.Effects;

/// <summary>
/// Lawn census → Core <see cref="BoardSnapshot"/> for TargetResolver.
/// Two layers keep this off the ~10ms <c>FindObjectsOfType</c> path (b2-live-x2 baseline):
/// snapshots are cached per rendered frame (lifecycle hooks call <see cref="Invalidate"/> so a
/// same-frame spawn/death stays visible), and entities come from the hook-fed
/// <see cref="InjectorEntityRegistry"/> — a full scan runs only on the registry's throttled
/// resync. Never add an uncached scan on the damage path.
/// </summary>
public static class InjectorBoardSnapshot
{
    static BoardSnapshot? _cached;
    static int _cachedFrame = -1;

    public static BoardSnapshot Capture()
    {
        int frame;
        try { frame = Time.frameCount; }
        catch { frame = -1; }
        if (frame >= 0 && frame == _cachedFrame && _cached != null)
            return _cached;

        var snap = CaptureUncached(frame);
        _cached = snap;
        _cachedFrame = frame;
        return snap;
    }

    /// <summary>Drop the per-frame cache after board mutations (spawn/death/board swap).</summary>
    public static void Invalidate()
    {
        _cached = null;
        _cachedFrame = -1;
    }

    static BoardSnapshot CaptureUncached(int frame)
    {
        using var _perf = PerfProbe.Measure(PerfSection.BoardCapture);
        if (InjectorEntityRegistry.NeedsResync(frame))
            InjectorEntityRegistry.Resync(frame);

        var list = new List<BoardEntitySnap>(InjectorEntityRegistry.PlantCount + InjectorEntityRegistry.ZombieCount);

        InjectorEntityRegistry.VisitPlants(p =>
        {
            if (p.thePlantType == PlantType.Nothing) return;
            list.Add(new BoardEntitySnap
            {
                Ptr = GameDumps.Ptr(p),
                Side = "plant",
                TypeId = (int)p.thePlantType,
                Col = p.thePlantColumn,
                Row = p.thePlantRow
            });
        });

        InjectorEntityRegistry.VisitZombies(z =>
        {
            if (z.theZombieType == ZombieType.Nothing) return;
            var col = -1;
            try { col = LawnCoords.ColFromX(z.transform.position.x); } catch { }
            if (col < 0)
            {
                try { col = z.Column; } catch { }
            }

            var row = 0;
            try { row = z.theZombieRow; } catch { }

            var mc = false;
            try { mc = z.isMindControlled; } catch { }

            list.Add(new BoardEntitySnap
            {
                Ptr = GameDumps.Ptr(z),
                Side = "zombie",
                TypeId = (int)z.theZombieType,
                Col = col,
                Row = row,
                MindControlled = mc
            });
        });

        return new BoardSnapshot(list);
    }
}
