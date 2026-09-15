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
/// unbounded growth is not a practical concern). <see cref="Clear"/> IS wired to match end
/// (<c>GameHooks.ClearMatch</c>) — a pin on an entity still alive at match end would otherwise leak onto
/// the next match's reused ptr.</para>
///
/// <para><b>Evidence caveat:</b> the pin is re-asserted AFTER the Hub write, so it overrides any Hub
/// maxHp bonus (aptitude, loadout, equipment) on that ptr. A pinned debug entity is never a valid
/// subject for a Hub HP live proof. Moving the pin into a Hub override input is a tracked next-run
/// task (lawn-combat-wire-todo).</para>
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

    /// <summary>Removes one ptr's pin — call from <see cref="GameHooks.ForgetEntity"/> (the same
    /// site <c>LawnElementResolverHost.Invalidate</c> uses and for the identical reason, per that
    /// call's own doc comment: "IL2CPP can hand this exact address to a NEW entity later in the same
    /// match" — without this, a fresh, never-pinned entity that happens to land on a reused address
    /// would silently inherit a long-dead entity's HP pin. Confirmed live, not theoretical: a debug
    /// respawn during this same session reused a prior pinned zombie's exact ptr and inherited its
    /// 20000 HP pin before this method existed.</summary>
    public static void Remove(string? ptr)
    {
        var key = CombatPtr.Normalize(ptr);
        if (string.IsNullOrEmpty(key)) return;
        lock (Gate)
            Pins.Remove(key);
    }

    public static void Clear()
    {
        lock (Gate)
            Pins.Clear();
    }
}
