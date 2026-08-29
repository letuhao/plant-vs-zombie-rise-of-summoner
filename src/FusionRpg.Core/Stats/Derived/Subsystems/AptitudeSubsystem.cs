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
/// </summary>
public sealed class AptitudeSubsystem : IActorStatSubsystem
{
    readonly Func<StatContext, AptitudeAllocation> _allocation;
    readonly AptitudeTuning _tuning;
    readonly PowerLadder _ladder;
    readonly IPowerIndexProvider _powerIndex;
    readonly DerivedStatRegistry _registry;

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
        var allocation = _allocation(ctx) ?? AptitudeAllocation.Empty;
        var theta = _powerIndex.ActorIndex(ctx);
        foreach (var m in AptitudeResolver.Resolve(allocation, _tuning, _ladder, theta, _registry))
            mods.Add(m);
    }
}
