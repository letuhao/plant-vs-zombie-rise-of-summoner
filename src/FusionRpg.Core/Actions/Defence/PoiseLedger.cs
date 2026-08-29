using FusionRpg.Core.Actions.Cost;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Actions.Defence;

/// <summary>
/// T25/T26 (spec-defence-actions.md §3): `poise`'s three-part cost. A flat commit at raise, an
/// absorb drain proportional to what the guard stopped, and a per-tick hold while held — the third
/// component beyond spec-guard-economy.md's flat-commit-plus-absorb-drain, which priced guard as a
/// proc; a STANCE needs the per-tick hold specifically to satisfy the termination invariant (§2.2:
/// "no later layer can repair a pool that refills faster than it drains").
///
/// <para>A thin wrapper over T15's <see cref="ActorResourcePools.TrySpend"/> — never a second pool
/// mechanism. "A8 authors no damage math" (spec §0): <see cref="AbsorbDrainAmount"/> takes the
/// already-computed absorbed amount as an input; it does not compute what a guard stops, only what
/// stopping it costs.</para>
/// </summary>
public static class PoiseLedger
{
    public const string ResourceId = "poise";

    /// <summary>Flat commit, paid once at raise. Ordinary <see cref="ActorResourcePools.TrySpend"/>
    /// semantics: false and no state change on insufficient poise — the caller refuses the raise via
    /// the normal affordability path, not a silent no-op (spec testing strategy: "raising with zero
    /// poise is refused by affordability, not by silence").</summary>
    public static bool TryCommit(ActorResourcePools pools, long flatCommitAmount, long nowTick, ActorDerivedSnapshot derived) =>
        pools.TrySpend(ResourceId, flatCommitAmount, nowTick, derived);

    /// <summary>Per-tick hold, paid every tick while the stance is held. A failed pay here IS the
    /// termination brake (§2.2) — the caller ends the stance through the interrupt path exactly like
    /// any other `perTick` cost (T17's `CostLedger`), no special case for guard.</summary>
    public static bool TryPayHoldTick(ActorResourcePools pools, long perTickAmount, long nowTick, ActorDerivedSnapshot derived) =>
        pools.TrySpend(ResourceId, perTickAmount, nowTick, derived);

    /// <summary><c>absorbedAmount × drainRatioMilli / 1000</c> — widened before multiplying, divided
    /// by 1000 exactly once (CLAUDE.md "Numeric overflow"). <paramref name="drainRatioMilli"/> is a
    /// bounded ratio, never a magnitude a balance pass calibrates as a flat number.</summary>
    public static long AbsorbDrainAmount(long absorbedAmount, int drainRatioMilli)
    {
        if (absorbedAmount < 0) throw new ArgumentOutOfRangeException(nameof(absorbedAmount), absorbedAmount, "an absorbed amount is never negative");
        if (drainRatioMilli < 0) throw new ArgumentOutOfRangeException(nameof(drainRatioMilli), drainRatioMilli, "a drain ratio is never negative");
        return checked(absorbedAmount * drainRatioMilli / 1000);
    }

    /// <summary>Pays the absorb drain for one absorbed hit. Same all-or-nothing
    /// <see cref="ActorResourcePools.TrySpend"/> contract as the other two parts.</summary>
    public static bool TryPayAbsorbDrain(ActorResourcePools pools, long absorbedAmount, int drainRatioMilli, long nowTick, ActorDerivedSnapshot derived) =>
        pools.TrySpend(ResourceId, AbsorbDrainAmount(absorbedAmount, drainRatioMilli), nowTick, derived);
}
