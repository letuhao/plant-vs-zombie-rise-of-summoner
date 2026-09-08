using FusionRpg.Contracts;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Match;
using FusionRpg.Core.Scope;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>aura-skill T21b: the real, production `IOwnSideOracle` for the SPECIMEN case — ownership
/// by which player deployed the specimen, not by mechanical plant/zombie side. Mirrors
/// `MechanicalOwnSideOracleTests`'s own split exactly: pure oracle-logic cases, then integration cases
/// running the real `BattlefieldOwnSideReactor` against it.</summary>
public class SpecimenOwnershipOracleTests
{
    [Fact]
    public void A_specimen_owned_by_the_same_player_is_an_ally()
    {
        var oracle = new SpecimenOwnershipOracle(myPlayerId: 1, resolveOwner: _ => 1L);
        Assert.Equal(RelationKind.Ally, oracle.RelationOf("S1"));
    }

    [Fact]
    public void A_specimen_owned_by_a_different_player_is_an_enemy()
    {
        var oracle = new SpecimenOwnershipOracle(myPlayerId: 1, resolveOwner: _ => 2L);
        Assert.Equal(RelationKind.Enemy, oracle.RelationOf("S1"));
    }

    [Fact]
    public void An_untracked_ptr_resolves_to_null_genuinely_unknown()
    {
        var oracle = new SpecimenOwnershipOracle(myPlayerId: 1, resolveOwner: _ => null);
        Assert.Null(oracle.RelationOf("ghost"));
    }

    [Fact]
    public void Different_ptrs_can_resolve_to_different_owners_off_the_same_oracle()
    {
        var owners = new Dictionary<string, long> { ["S1"] = 1L, ["S2"] = 2L };
        var oracle = new SpecimenOwnershipOracle(myPlayerId: 1, resolveOwner: ptr => owners.TryGetValue(ptr, out var v) ? v : null);
        Assert.Equal(RelationKind.Ally, oracle.RelationOf("S1"));
        Assert.Equal(RelationKind.Enemy, oracle.RelationOf("S2"));
        Assert.Null(oracle.RelationOf("S3"));
    }

    [Fact]
    public void Constructing_with_a_non_positive_playerId_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpecimenOwnershipOracle(0, _ => null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpecimenOwnershipOracle(-1, _ => null));
    }

    [Fact]
    public void Constructing_with_a_null_resolver_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SpecimenOwnershipOracle(1, null!));
    }

    // ---- integration: BattlefieldOwnSideReactor runs with the REAL oracle, not a fake ----

    static EffectBag NewBag()
    {
        var harness = new FoundationHarness();
        harness.Bag.Catalog.Upsert(new EffectDef
        {
            EffectId = "fx.t21b-probe",
            EffectType = EffectTypes.Passive,
            Name = "T21b probe",
        });
        return harness.Bag;
    }

    static bool HasGrant(EffectBag bag, string ptr) =>
        bag.ForOwner("entity", EffectOwnerKeys.Entity(ptr)).Count > 0;

    [Fact]
    public void Reactor_with_the_real_oracle_grants_a_specimen_owned_by_this_player()
    {
        var bag = NewBag();
        var owners = new Dictionary<string, long> { ["S1"] = 1L };
        var oracle = new SpecimenOwnershipOracle(1, ptr => owners.TryGetValue(ptr, out var v) ? v : null);
        var reactor = new BattlefieldOwnSideReactor(
            bag, oracle, RelationKind.Ally, "fx.t21b-probe", "test", "aura:t21b",
            "resource.delta", ScopeHost.Sim);

        reactor.OnMembershipChanged(new ScopeMembershipEvent("S1", ScopeMembershipTransition.Bound));

        Assert.True(HasGrant(bag, "S1"));
    }

    [Fact]
    public void Reactor_with_the_real_oracle_never_grants_a_specimen_owned_by_another_player()
    {
        var bag = NewBag();
        var owners = new Dictionary<string, long> { ["S2"] = 2L };
        var oracle = new SpecimenOwnershipOracle(1, ptr => owners.TryGetValue(ptr, out var v) ? v : null);
        var reactor = new BattlefieldOwnSideReactor(
            bag, oracle, RelationKind.Ally, "fx.t21b-probe", "test", "aura:t21b",
            "resource.delta", ScopeHost.Sim);

        reactor.OnMembershipChanged(new ScopeMembershipEvent("S2", ScopeMembershipTransition.Bound));

        Assert.False(HasGrant(bag, "S2"));
    }

    [Fact]
    public void Both_owners_perspective_reactors_resolve_correctly_off_the_SAME_ownership_map()
    {
        // "ownership resolves correctly for both players" -- proven with two reactors reading the
        // identical ptr->owner map, one per player's own perspective, matching T21a's own twin-reactor
        // proof for the mechanical case.
        var owners = new Dictionary<string, long> { ["S1"] = 1L, ["S2"] = 2L };
        long? Resolve(string ptr) => owners.TryGetValue(ptr, out var v) ? v : null;

        var player1Bag = NewBag();
        var player2Bag = NewBag();
        var player1Oracle = new SpecimenOwnershipOracle(1, Resolve);
        var player2Oracle = new SpecimenOwnershipOracle(2, Resolve);
        var player1Reactor = new BattlefieldOwnSideReactor(
            player1Bag, player1Oracle, RelationKind.Ally, "fx.t21b-probe", "test", "aura:p1",
            "resource.delta", ScopeHost.Sim);
        var player2Reactor = new BattlefieldOwnSideReactor(
            player2Bag, player2Oracle, RelationKind.Ally, "fx.t21b-probe", "test", "aura:p2",
            "resource.delta", ScopeHost.Sim);

        player1Reactor.OnMembershipChanged(new ScopeMembershipEvent("S1", ScopeMembershipTransition.Bound));
        player1Reactor.OnMembershipChanged(new ScopeMembershipEvent("S2", ScopeMembershipTransition.Bound));
        player2Reactor.OnMembershipChanged(new ScopeMembershipEvent("S1", ScopeMembershipTransition.Bound));
        player2Reactor.OnMembershipChanged(new ScopeMembershipEvent("S2", ScopeMembershipTransition.Bound));

        Assert.True(HasGrant(player1Bag, "S1"));
        Assert.False(HasGrant(player1Bag, "S2"));
        Assert.False(HasGrant(player2Bag, "S1"));
        Assert.True(HasGrant(player2Bag, "S2"));
    }
}
