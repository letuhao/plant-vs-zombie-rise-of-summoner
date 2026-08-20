using FusionRpg.Core.Combat;
using FusionRpg.Injector.Lawn;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Effects;

/// <summary>
/// Hook-fed live Plant/Zombie registry so <see cref="InjectorBoardSnapshot"/> never pays a
/// per-event <c>FindObjectsOfType</c> scan (~10ms each, b2-live-x2 baseline). Same pattern as
/// Fx.AnchorResolver: Start/InitHealth postfixes add, die hooks remove, and a throttled full
/// resync catches units the hooks never saw. Main-thread only, like every caller.
///
/// v3 A4 — incremental snapshots: each entry caches an immutable <see cref="BoardEntitySnap"/>.
/// Plants never move → snap built once per lifetime (glove-moves heal at the next resync).
/// Zombies walk → their snap refreshes at most every <see cref="ZombieSnapRefreshMs"/> (col
/// drift over 150ms is far below one lawn column — precision note in event-pipeline-v2-ssot).
/// Snap instances are immutable and safely shared across successive frozen BoardSnapshots.
/// </summary>
public static class InjectorEntityRegistry
{
    /// <summary>Frames between full-scan resyncs (~4s at 240fps, ~17s at 60fps).</summary>
    public const int ResyncFrames = 1024;

    /// <summary>Zombie snap (col/row/mind-control) refresh throttle.</summary>
    public const int ZombieSnapRefreshMs = 150;

    sealed class PlantEntry
    {
        public Plant P = null!;
        public BoardEntitySnap? Snap;
    }

    sealed class ZombieEntry
    {
        public Zombie Z = null!;
        public string PtrHex = "";
        public int TypeId;
        public BoardEntitySnap? Snap;
        public long SnapAtMs;
    }

    static readonly Dictionary<IntPtr, PlantEntry> Plants = new();
    static readonly Dictionary<IntPtr, ZombieEntry> Zombies = new();
    static int _lastResyncFrame = int.MinValue;

    public static void Add(Plant? p)
    {
        try
        {
            if (p == null || p.Pointer == IntPtr.Zero) return;
            Plants[p.Pointer] = new PlantEntry { P = p };
        }
        catch { }
    }

    public static void Add(Zombie? z)
    {
        try
        {
            if (z == null || z.Pointer == IntPtr.Zero) return;
            var typeId = 0;
            try { typeId = (int)z.theZombieType; } catch { }
            Zombies[z.Pointer] = new ZombieEntry
            {
                Z = z,
                PtrHex = z.Pointer.ToString("X"),
                TypeId = typeId
            };
        }
        catch { }
    }

    public static void Remove(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return;
        Plants.Remove(ptr);
        Zombies.Remove(ptr);
    }

    public static void Clear()
    {
        Plants.Clear();
        Zombies.Clear();
        _lastResyncFrame = int.MinValue;
    }

    public static bool NeedsResync(int frame) =>
        frame < 0 || frame - _lastResyncFrame >= ResyncFrames || frame < _lastResyncFrame;

    /// <summary>Full-scan resync — the only remaining <c>FindObjectsOfType</c> on the combat path.
    /// Also heals cached-snap staleness (glove-moved plants, missed hooks).</summary>
    public static void Resync(int frame)
    {
        _lastResyncFrame = frame;
        Plants.Clear();
        Zombies.Clear();
        try
        {
            foreach (var p in UObject.FindObjectsOfType<Plant>())
                Add(p);
        }
        catch { }
        try
        {
            foreach (var z in UObject.FindObjectsOfType<Zombie>())
                Add(z);
        }
        catch { }
    }

    /// <summary>
    /// Append current-board snaps (A4). Plant snaps build once; zombie snaps refresh at most
    /// every <see cref="ZombieSnapRefreshMs"/>. Entities that throw (destroyed native side)
    /// are dropped from the registry.
    /// </summary>
    public static void CollectSnaps(List<BoardEntitySnap> into)
    {
        var now = Environment.TickCount64;
        List<IntPtr>? dead = null;

        foreach (var kv in Plants)
        {
            var e = kv.Value;
            try
            {
                if (e.P == null) { (dead ??= new List<IntPtr>()).Add(kv.Key); continue; }
                if (e.Snap == null)
                {
                    if (e.P.thePlantType == PlantType.Nothing) continue;
                    e.Snap = new BoardEntitySnap
                    {
                        Ptr = kv.Key.ToString("X"),
                        Side = "plant",
                        TypeId = (int)e.P.thePlantType,
                        Col = e.P.thePlantColumn,
                        Row = e.P.thePlantRow
                    };
                }
                into.Add(e.Snap);
            }
            catch { (dead ??= new List<IntPtr>()).Add(kv.Key); }
        }
        if (dead != null) { foreach (var k in dead) Plants.Remove(k); dead.Clear(); }

        foreach (var kv in Zombies)
        {
            var e = kv.Value;
            try
            {
                if (e.Z == null) { (dead ??= new List<IntPtr>()).Add(kv.Key); continue; }
                if (e.Snap == null || now - e.SnapAtMs >= ZombieSnapRefreshMs)
                {
                    if (e.TypeId == (int)ZombieType.Nothing && e.Snap == null)
                    {
                        try { e.TypeId = (int)e.Z.theZombieType; } catch { }
                        if (e.TypeId == (int)ZombieType.Nothing) continue;
                    }

                    var col = -1;
                    try { col = LawnCoords.ColFromX(e.Z.transform.position.x); } catch { }
                    if (col < 0)
                    {
                        try { col = e.Z.Column; } catch { }
                    }
                    var row = 0;
                    try { row = e.Z.theZombieRow; } catch { }
                    var mc = false;
                    try { mc = e.Z.isMindControlled; } catch { }

                    e.Snap = new BoardEntitySnap
                    {
                        Ptr = e.PtrHex,
                        Side = "zombie",
                        TypeId = e.TypeId,
                        Col = col,
                        Row = row,
                        MindControlled = mc
                    };
                    e.SnapAtMs = now;
                }
                into.Add(e.Snap);
            }
            catch { (dead ??= new List<IntPtr>()).Add(kv.Key); }
        }
        if (dead != null) foreach (var k in dead) Zombies.Remove(k);
    }

    /// <summary>Iterate live plants; entities that throw (destroyed native side) are dropped.</summary>
    public static void VisitPlants(Action<Plant> visit)
    {
        List<IntPtr>? dead = null;
        foreach (var kv in Plants)
        {
            try
            {
                if (kv.Value.P == null) { (dead ??= new List<IntPtr>()).Add(kv.Key); continue; }
                visit(kv.Value.P);
            }
            catch { (dead ??= new List<IntPtr>()).Add(kv.Key); }
        }
        if (dead != null)
            foreach (var k in dead) Plants.Remove(k);
    }

    public static void VisitZombies(Action<Zombie> visit)
    {
        List<IntPtr>? dead = null;
        foreach (var kv in Zombies)
        {
            try
            {
                if (kv.Value.Z == null) { (dead ??= new List<IntPtr>()).Add(kv.Key); continue; }
                visit(kv.Value.Z);
            }
            catch { (dead ??= new List<IntPtr>()).Add(kv.Key); }
        }
        if (dead != null)
            foreach (var k in dead) Zombies.Remove(k);
    }

    public static int PlantCount => Plants.Count;
    public static int ZombieCount => Zombies.Count;

    /// <summary>O(1) ptr-string lookup. Null on miss — callers needing certainty fall back to a scan.</summary>
    public static Zombie? FindZombie(string? ptrHex) =>
        TryParsePtr(ptrHex, out var p) && Zombies.TryGetValue(p, out var e) ? e.Z : null;

    public static Plant? FindPlant(string? ptrHex) =>
        TryParsePtr(ptrHex, out var p) && Plants.TryGetValue(p, out var e) ? e.P : null;

    static bool TryParsePtr(string? s, out IntPtr ptr)
    {
        ptr = IntPtr.Zero;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t.Substring(2);
        if (!long.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var v) || v == 0)
            return false;
        ptr = new IntPtr(v);
        return true;
    }
}
