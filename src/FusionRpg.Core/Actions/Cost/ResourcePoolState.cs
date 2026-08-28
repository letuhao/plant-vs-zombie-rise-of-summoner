namespace FusionRpg.Core.Actions.Cost;

/// <summary>
/// One resource's runtime state (spec-action-costs.md §2): the only persisted fields are
/// <see cref="Stored"/> and <see cref="LastTick"/> — the current value is resolved lazily on read,
/// never advanced by a scheduled event. With six pools across 200 actors that would be 1,200 timers
/// doing nothing but arithmetic; compute-on-read gives an identical answer for free.
/// </summary>
public readonly record struct ResourcePoolState(long Stored, long LastTick)
{
    /// <summary><c>value(now) = clamp(stored + rate * (now - lastTick), 0, max)</c>. <c>rate</c> and
    /// <c>max</c> are read fresh by the caller on every resolve (buffs/debuffs move them), so this
    /// struct itself never caches either.</summary>
    public long Resolve(long nowTick, long ratePerTick, long max)
    {
        if (nowTick < LastTick)
            throw new ArgumentOutOfRangeException(nameof(nowTick), nowTick, "nowTick precedes LastTick");

        var elapsed = nowTick - LastTick;
        var accrued = ratePerTick * elapsed; // widen-before-multiply: both operands already long
        var raw = Stored + accrued;

        if (raw < 0) return 0;
        return raw > max ? max : raw;
    }

    /// <summary>
    /// Materializes <see cref="Resolve"/> as the new <see cref="Stored"/> and anchors
    /// <see cref="LastTick"/> at <paramref name="nowTick"/> — used at battle end, where the pool
    /// resolves to a concrete value and the clock it was ticking against is dropped (spec §2): the
    /// SAVED representation is a bare value with no <c>lastTick</c> attached, since a persisted tick
    /// count would make a reloaded actor's pool depend on wall-clock time between sessions.
    /// </summary>
    public ResourcePoolState Settle(long nowTick, long ratePerTick, long max) =>
        new(Resolve(nowTick, ratePerTick, max), nowTick);
}
