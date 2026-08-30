using FusionRpg.Contracts;

namespace FusionRpg.Core.Battle;

/// <summary>
/// aura-skill T21b: the real, production `IOwnSideOracle` for the SPECIMEN case —
/// `BattlefieldOwnSideReactor`'s own doc comment names this as the harder half `MechanicalOwnSideOracle`
/// (T21a) deliberately left unanswered: when a demon SPECIMEN is on the lawn, ownership is "which
/// player deployed it," not "which mechanical side is it on."
///
/// <para><b>The Cold-plane bridge this needs is real now, not invented here.</b>
/// `UniqueActorService.DeployAsync` already sends `playerId` to the Injector in the `pvz.spawn.extra`
/// command payload (Server → Injector, confirmed by direct read) — it was simply never cached
/// ptr-keyed on arrival, and never read back out. `CheatCommandRunner.cs`'s `pvz.spawn.extra` handler
/// now reads it and calls `CheatState.RegisterSpecimenOwner(ptr, playerId)` the moment the entity's
/// `ptr` becomes known (`CheatActions.cs`'s ack-building code, mirroring the existing
/// `RegisterSpawnSourceTag`/`SpawnSourceByPtr` cache shape). This class is the read side: same
/// injected-resolver pattern <see cref="MechanicalOwnSideOracle"/> already established, so a test never
/// needs a live game or a real ptr cache.</para>
///
/// <para>Decision recorded in <c>docs/architecture/decisions.md</c> ("Specimen ownership bridge,
/// 2026-08-30").</para>
/// </summary>
public sealed class SpecimenOwnershipOracle : IOwnSideOracle
{
    readonly long _myPlayerId;
    readonly Func<string, long?> _resolveOwner;

    /// <summary>
    /// <paramref name="myPlayerId"/> is the reactor's own perspective (the player this reactor grants
    /// on behalf of). <paramref name="resolveOwner"/> is a ptr → owning-player-id lookup
    /// (`CheatState.TryGetSpecimenOwner`, in production) — injected rather than a hard dependency, so
    /// a test never needs the real Injector cache.
    /// </summary>
    public SpecimenOwnershipOracle(long myPlayerId, Func<string, long?> resolveOwner)
    {
        if (myPlayerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(myPlayerId), myPlayerId, "myPlayerId must be a real, positive player id");
        _myPlayerId = myPlayerId;
        _resolveOwner = resolveOwner ?? throw new ArgumentNullException(nameof(resolveOwner));
    }

    /// <summary>Null exactly when the resolver genuinely has no owner recorded for this ptr (not a
    /// specimen, or not yet registered) — <see cref="IOwnSideOracle.RelationOf"/>'s own contract.</summary>
    public RelationKind? RelationOf(string ptr)
    {
        var owner = _resolveOwner(ptr);
        if (owner is null) return null;
        return owner.Value == _myPlayerId ? RelationKind.Ally : RelationKind.Enemy;
    }
}
