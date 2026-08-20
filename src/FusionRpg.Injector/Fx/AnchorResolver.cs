using FusionRpg.Core.Vfx;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// ptr → Transform cache — vfx-ssot.md §9. Hook-fed (Plant/Zombie Start postfixes register),
/// with a reconciliation sweep throttled to one per 0.5s for units the hooks never saw
/// (injector attached mid-match). Never FindObjectsOfType per cue.
/// </summary>
public static class AnchorResolver
{
    static readonly Dictionary<string, Transform> Cache = new(StringComparer.OrdinalIgnoreCase);
    static float _now;
    static float _lastSweep = -999f;

    public static void Tick(float dt)
    {
        if (dt > 0f) _now += dt;
    }

    public static void Register(string? ptr, Transform? transform)
    {
        if (string.IsNullOrWhiteSpace(ptr) || transform == null) return;
        Cache[ptr] = transform;
    }

    public static void Clear() => Cache.Clear();

    public static Transform? Resolve(string? ptr)
    {
        if (string.IsNullOrWhiteSpace(ptr)) return null;
        if (Cache.TryGetValue(ptr, out var t))
        {
            if (t != null) return t;
            Cache.Remove(ptr);
        }

        if (_now - _lastSweep < VfxRules.AnchorSweepMinIntervalSeconds) return null;
        _lastSweep = _now;
        Sweep();
        return Cache.TryGetValue(ptr, out var found) && found != null ? found : null;
    }

    static void Sweep()
    {
        try
        {
            foreach (var z in UObject.FindObjectsOfType<Zombie>())
            {
                if (z == null) continue;
                Cache[GameDumps.Ptr(z)] = z.transform;
            }

            foreach (var p in UObject.FindObjectsOfType<Plant>())
            {
                if (p == null) continue;
                Cache[GameDumps.Ptr(p)] = p.transform;
            }
        }
        catch
        {
            // Missing scene objects — resolve simply misses; never throw into the loop.
        }

        List<string>? dead = null;
        foreach (var kv in Cache)
        {
            if (kv.Value == null) (dead ??= new List<string>()).Add(kv.Key);
        }

        if (dead != null)
        {
            foreach (var k in dead) Cache.Remove(k);
        }
    }
}
