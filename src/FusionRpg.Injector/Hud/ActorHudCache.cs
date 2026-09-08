using FusionRpg.Core.Combat;
using FusionRpg.Core.Hud;

namespace FusionRpg.Injector.Hud;

/// <summary>Per-ptr HUD snapshot cache — always rebuilds on observe read; dirty set drives delta emit only.</summary>
public static class ActorHudCache
{
    static readonly object Gate = new();
    static readonly Dictionary<string, ActorHudSnapshot> Cache = new(StringComparer.Ordinal);
    static readonly HashSet<string> Dirty = new(StringComparer.Ordinal);

    /// <summary>Production builder — set once from <see cref="ActorHudInvalidator.Install"/>.</summary>
    public static Func<string, ActorHudSnapshot>? Build { get; set; }

    /// <summary>Optional delta observe hook — wired by <see cref="ActorHudInvalidator"/>.</summary>
    public static Action<string, Dictionary<string, object>>? DeltaEmit { get; set; }

    public static ActorHudSnapshot? GetOrBuild(string? ptrHex)
    {
        var ptr = CombatPtr.Normalize(ptrHex);
        if (string.IsNullOrEmpty(ptr)) return null;

        lock (Gate)
        {
            var wasDirty = Dirty.Contains(ptr);
            var built = BuildSnapshot(ptr);
            Cache[ptr] = built;
            Dirty.Remove(ptr);

            if (wasDirty)
                TryEmitDelta(ptr, built);

            return built;
        }
    }

    public static void MarkDirty(string? ptrHex)
    {
        var ptr = CombatPtr.Normalize(ptrHex);
        if (string.IsNullOrEmpty(ptr)) return;
        lock (Gate)
            Dirty.Add(ptr);
    }

    public static void Remove(string? ptrHex)
    {
        var ptr = CombatPtr.Normalize(ptrHex);
        if (string.IsNullOrEmpty(ptr)) return;
        lock (Gate)
        {
            Cache.Remove(ptr);
            Dirty.Remove(ptr);
        }

        ActorHudUniqueFlags.Remove(ptr);
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Cache.Clear();
            Dirty.Clear();
        }

        ActorHudUniqueFlags.Clear();
    }

    /// <summary>Fallback tick — reconcile at most one dirty ptr per frame.</summary>
    public static void ReconcileDirty()
    {
        string? ptr;
        lock (Gate)
        {
            if (Dirty.Count == 0) return;
            ptr = Dirty.First();
        }

        GetOrBuild(ptr);
    }

    static ActorHudSnapshot BuildSnapshot(string ptr) =>
        Build?.Invoke(ptr)
        ?? throw new InvalidOperationException(
            "ActorHudCache.Build is not wired. Call ActorHudInvalidator.Install() during bootstrap.");

    static void TryEmitDelta(string ptr, ActorHudSnapshot snapshot)
    {
        try
        {
            DeltaEmit?.Invoke(ptr, ActorHudWireSerializer.ToDictionary(snapshot));
        }
        catch { /* observe must not block gameplay */ }
    }
}
