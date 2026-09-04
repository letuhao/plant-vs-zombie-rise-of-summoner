using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;

namespace FusionRpg.Core.Stats.Derived.Subsystems;

/// <summary>
/// class-system-todo.md P2.4 — the registered seam spec-aptitude-resolve.md §2 identifies:
/// <see cref="IActorStatSubsystem"/>, through <c>ActorHub.Register</c>, exactly where
/// <see cref="RpgProgressionSubsystem"/> already sits. Not <c>ClassStatPlugin</c> — that is a
/// different pipeline entirely (see that class's own doc comment).
///
/// <para>The allocation source is a per-context delegate, same shape as
/// <see cref="RpgProgressionSubsystem"/>'s <c>level</c> delegate and for the identical reason: this
/// module owns resolving an allocation into channel values, not where the allocation itself is
/// stored. `point-economy`'s `AllocationStore` (Phase 6) is the real source-of-truth once it exists;
/// until then the default is <see cref="AptitudeAllocation.Empty"/> — nobody has spent a point, so
/// this subsystem is wired into production and provably inert (§9: "zero goldens move on an empty
/// allocation") ahead of there being anything to allocate.</para>
///
/// <para><c>ContributeDerived</c> is idempotent (§2 rule 1): it holds no state between calls and
/// <see cref="AptitudeResolver.Resolve"/> is pure, so two calls with the same inputs produce the same
/// modifiers — and <c>ActorHub.Register</c> replacing by <see cref="SubsystemId"/> means a double
/// registration never double-adds regardless.</para>
///
/// <para><b>`species-build` T0.1/T0.2 — memoized.</b> <see cref="AptitudeResolver.Resolve"/> loops
/// every tuning edge (526 shipped) and calls <c>Share()</c> per edge — a `GrandTotal()` over 12
/// aptitudes x 4 scopes each, ~25,000 dictionary lookups per call — and this runs on the status/hit
/// path as well as the apply path. The memo is keyed on every input the resolve actually reads:
/// <c>(Side, TypeId, Theta)</c>. <b>`Theta` is in the key, not merely in the invalidation table</b> —
/// it is a per-actor value (<see cref="IPowerIndexProvider.ActorIndex"/>), so two entities of the same
/// `(Side, TypeId)` at different power indices are different cache entries by construction; no bump
/// is needed for a level-up, and no stale-build risk exists from omitting one.</para>
///
/// <para><b>The one real invalidation path.</b> The only input NOT in the key is `_allocation(ctx)`'s
/// own return value — <see cref="InvalidateMemo"/> exists for exactly the case where the SAME
/// `(Side, TypeId, Theta)` triple must resolve differently because the allocation itself changed
/// (an override save, a session refresh, a match edge). Traced against the real call graph: every one
/// of those funnels through <c>CheatState.RefreshCommanderAllocationCache</c>
/// (session start / reconnect / `AptitudesUpdated` push, and both `MatchHost` match-edge sites all
/// call it) — so wiring the bump there covers every live path with one call site, not several.</para>
///
/// <para><b>`StatSystem.Invalidate()` needs no bump, and an earlier plan draft said otherwise.</b>
/// That signal is StatSystem's own primary-compose dirty flag (cheats, items, session mods) — it does
/// not touch the aptitude allocation at all, so a reapply it triggers legitimately wants the SAME
/// aptitude modifiers as before. Bumping on it would defeat part of the memo's own purpose. Corrected
/// here rather than silently implemented wrong, per this program's own audit discipline.</para>
///
/// <para><b>Tuning reconfiguration needs no bump either, and it cannot happen to a live instance.</b>
/// <c>_tuning</c> is an immutable constructor field; the only way to change it is to construct a NEW
/// <see cref="AptitudeSubsystem"/> (which starts with an empty memo by construction). Production never
/// even does this today — `CheatState.ActorHub` is built once via `??=` and never rebuilt — so this
/// path is structurally unreachable, not merely untested.</para>
/// </summary>
public sealed class AptitudeSubsystem : IActorStatSubsystem
{
    readonly Func<StatContext, AptitudeAllocation> _allocation;
    readonly AptitudeTuning _tuning;
    readonly PowerLadder _ladder;
    readonly IPowerIndexProvider _powerIndex;
    readonly DerivedStatRegistry _registry;

    // The memo. Keyed on every input AptitudeResolver.Resolve reads except the allocation itself (see
    // the type's own doc comment for why that one input needs an explicit bump instead of a key slot).
    // Never static: a static memo would leak an allocation from one scoped test host into another, the
    // exact AptitudeTuningHub race this repo already fixed once (PointBudget.cs's own doc comment).
    readonly Dictionary<(StatSide Side, int TypeId, int Theta), IReadOnlyList<DerivedModifier>> _memo = new();

    public AptitudeSubsystem(
        AptitudeTuning tuning,
        PowerLadder ladder,
        IPowerIndexProvider? powerIndex = null,
        Func<StatContext, AptitudeAllocation>? allocation = null,
        DerivedStatRegistry? registry = null)
    {
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _ladder = ladder ?? throw new ArgumentNullException(nameof(ladder));
        _powerIndex = powerIndex ?? new StubPowerIndexProvider();
        _allocation = allocation ?? (_ => AptitudeAllocation.Empty);
        _registry = registry ?? DerivedStatRegistry.CreateDefault();
    }

    public string SubsystemId => "rpg.aptitude";
    public int Order => 100; // same tier as RpgProgressionSubsystem; FlatSum/SumIncreased are both
                              // commutative, so relative order between the two carries no meaning today

    public void ContributeDerived(StatContext ctx, ICollection<DerivedModifier> mods)
    {
        var theta = _powerIndex.ActorIndex(ctx);
        var key = (ctx.Side, ctx.TypeId, theta);

        if (!_memo.TryGetValue(key, out var cached))
        {
            var allocation = _allocation(ctx) ?? AptitudeAllocation.Empty;
            cached = AptitudeResolver.Resolve(allocation, _tuning, _ladder, theta, _registry).ToList();
            _memo[key] = cached;
        }

        foreach (var m in cached)
            mods.Add(m);
    }

    /// <summary>Called whenever the allocation this subsystem's delegate returns can have changed for
    /// an already-memoized key — see this type's own doc comment for the one real trigger. Clears the
    /// whole memo rather than tracking per-key staleness: an allocation change is rare (a save, a
    /// reconnect, a match edge), never per-frame, so a full clear costs nothing measurable and needs no
    /// generation-stamp bookkeeping to get right.</summary>
    public void InvalidateMemo() => _memo.Clear();
}
