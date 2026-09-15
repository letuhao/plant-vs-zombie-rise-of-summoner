using FusionRpg.Core.Combat;

namespace FusionRpg.Injector.Stats;

/// <summary>
/// Per-ptr durable HP override for `debug.spawn-plant`/`debug.spawn-zombie`'s own absolute
/// <c>hp</c>/<c>maxHp</c> params (lawn-combat-wire, 2026-09-15 finding: the existing
/// <c>P-HP</c>/<c>P-MAXHP</c> Tab-B cheat channels <see cref="CheatState.BuildPlantAbsolute"/> reads
/// are GLOBAL — they apply to every plant on the board when <c>includeAbsolute</c> is true, and are
/// silently dropped for every plant when it is false, e.g. <c>CheatActions.ReapplyLivingFromStats</c>'s
/// own <c>includeAbsolute: false</c> "cheat.pushScales" reapply. A debug-spawned plant's own spawn-time
/// HP override was therefore reverted by the very next such reapply — proven live: `hp
/// 500000/500000-&gt;300/300` one line after the spawn's own write).
///
/// <para>This is the narrower, per-instance fix: pinned once at spawn, keyed by ptr, and re-asserted
/// by <see cref="EntityApply"/> on every reapply for that one ptr regardless of
/// <c>includeAbsolute</c> — the same "pin once, re-read every apply" shape
/// <see cref="InjectorDerivedOverride"/> already uses for a different per-ptr override.</para>
///
/// <para><b>Deliberately NOT wired into `DebugRuntime.EndSession`/`EffectRuntime.ClearAll` the way
/// `InjectorDerivedOverride.Clear()` is.</b> This fix exists specifically for the "buff HP at spawn,
/// then end the debug session so combat routes through the real v2 path" workflow — clearing on
/// session end would wipe the pin before the very combat window it was pinned for. A stale entry for
/// a despawned ptr is harmless (this is a debug-only feature, never populated in real gameplay, so
/// unbounded growth is not a practical concern); <see cref="Clear"/> exists for a future genuine
/// match-end hook if one is ever needed, not wired here on purpose.</para>
/// </summary>
public static class InjectorSpawnHpPin
{
    static readonly object Gate = new();
    static readonly Dictionary<string, long> Pins = new(StringComparer.Ordinal);

    public static void Pin(string? ptr, long hp)
    {
        var key = CombatPtr.Normalize(ptr);
        if (string.IsNullOrEmpty(key) || hp <= 0) return;
        lock (Gate)
            Pins[key] = hp;
    }

    public static bool TryGet(string? ptr, out long hp)
    {
        var key = CombatPtr.Normalize(ptr);
        lock (Gate)
            return Pins.TryGetValue(key, out hp);
    }

    public static void Clear()
    {
        lock (Gate)
            Pins.Clear();
    }
}
