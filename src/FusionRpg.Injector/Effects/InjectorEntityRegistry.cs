using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Effects;

/// <summary>
/// Hook-fed live Plant/Zombie registry so <see cref="InjectorBoardSnapshot"/> never pays a
/// per-event <c>FindObjectsOfType</c> scan (~10ms each, b2-live-x2 baseline). Same pattern as
/// Fx.AnchorResolver: Start/InitHealth postfixes add, die hooks remove, and a throttled full
/// resync catches units the hooks never saw (injector attached mid-match, unhooked spawn paths).
/// Main-thread only, like every caller.
/// </summary>
public static class InjectorEntityRegistry
{
    /// <summary>Frames between full-scan resyncs (~4s at 240fps, ~17s at 60fps).</summary>
    public const int ResyncFrames = 1024;

    static readonly Dictionary<IntPtr, Plant> Plants = new();
    static readonly Dictionary<IntPtr, Zombie> Zombies = new();
    static int _lastResyncFrame = int.MinValue;

    public static void Add(Plant? p)
    {
        try
        {
            if (p == null || p.Pointer == IntPtr.Zero) return;
            Plants[p.Pointer] = p;
        }
        catch { }
    }

    public static void Add(Zombie? z)
    {
        try
        {
            if (z == null || z.Pointer == IntPtr.Zero) return;
            Zombies[z.Pointer] = z;
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

    /// <summary>Full-scan resync — the only remaining <c>FindObjectsOfType</c> on the combat path.</summary>
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

    /// <summary>Iterate live plants; entities that throw (destroyed native side) are dropped.</summary>
    public static void VisitPlants(Action<Plant> visit)
    {
        List<IntPtr>? dead = null;
        foreach (var kv in Plants)
        {
            try
            {
                if (kv.Value == null) { (dead ??= new List<IntPtr>()).Add(kv.Key); continue; }
                visit(kv.Value);
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
                if (kv.Value == null) { (dead ??= new List<IntPtr>()).Add(kv.Key); continue; }
                visit(kv.Value);
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
        TryParsePtr(ptrHex, out var p) && Zombies.TryGetValue(p, out var z) ? z : null;

    public static Plant? FindPlant(string? ptrHex) =>
        TryParsePtr(ptrHex, out var p) && Plants.TryGetValue(p, out var pl) ? pl : null;

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
