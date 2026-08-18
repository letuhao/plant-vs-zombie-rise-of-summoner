using FusionRpg.Core.Combat;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Injector.Stats;

/// <summary>Per-ptr derived pin for LIVE status prove — no SQL.</summary>
public static class InjectorDerivedOverride
{
    static readonly object Gate = new();
    static readonly Dictionary<string, ActorDerivedSnapshot> Pins = new(StringComparer.Ordinal);

    public static void Pin(string? ptr, ActorDerivedSnapshot snapshot)
    {
        var key = CombatPtr.Normalize(ptr);
        if (string.IsNullOrEmpty(key) || snapshot == null) return;
        lock (Gate)
            Pins[key] = snapshot;
    }

    public static void PinProfile(
        string? ptr,
        string? profile,
        IReadOnlyDictionary<string, double>? overlay = null)
    {
        Pin(ptr, ActorDerivedProfiles.Resolve(profile, overlay));
    }

    public static bool TryGet(string? ptr, out ActorDerivedSnapshot snapshot)
    {
        var key = CombatPtr.Normalize(ptr);
        lock (Gate)
            return Pins.TryGetValue(key, out snapshot!);
    }

    public static void Clear()
    {
        lock (Gate)
            Pins.Clear();
    }
}
