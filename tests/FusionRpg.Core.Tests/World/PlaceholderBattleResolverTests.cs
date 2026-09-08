using FusionRpg.Core.World;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// W10: the wave-1 stand-in for real combat. It exists so the world can exercise "who holds this
/// ground" — not to balance anything. What it must be is deterministic, symmetric, and honest about
/// the three outcomes the world knows how to apply: a winner, a rout, and mutual destruction.
/// </summary>
public class PlaceholderBattleResolverTests
{
    static WorldEntity Force(string id, string faction, int members, int hp, int level) => new()
    {
        EntityId = id,
        Kind = WorldEntityKind.Legion,
        OwnerFactionId = faction,
        AtSectorId = "ember-hollow",
        Stance = "march",
        MovementRemaining = 1000,
        Members = Enumerable.Range(0, members)
            .Select(_ => new WorldEntityMember { SpeciesId = "normalzombie", Level = level, Hp = hp })
            .ToList()
    };

    static BattleRequest Fight(bool defenderStationary = false) => new()
    {
        BattleId = "b1",
        Kind = BattleKinds.Sector,
        LocationId = "ember-hollow",
        AttackerEntityId = "a",
        DefenderEntityId = "b",
        DefenderStationary = defenderStationary
    };

    static readonly IBattleResolver Resolver = PlaceholderBattleResolver.Instance;

    [Fact]
    public void The_heavier_force_wins_and_the_lighter_one_routs()
    {
        var a = Force("a", "dave", 3, 110, 1);   // 330
        var b = Force("b", "wild", 2, 140, 2);   // 560

        var outcome = Resolver.Resolve(Fight(), new[] { a, b }, seed: 1);

        Assert.Equal("b", outcome.WinnerEntityId);
        var loser = outcome.Sides.Single(s => s.EntityId == "a");
        Assert.True(loser.Routed || loser.Destroyed);
    }

    [Fact]
    public void Equal_forces_destroy_each_other()
    {
        var a = Force("a", "dave", 2, 100, 1);
        var b = Force("b", "wild", 2, 100, 1);

        var outcome = Resolver.Resolve(Fight(), new[] { a, b }, seed: 1);

        Assert.Null(outcome.WinnerEntityId);
        Assert.All(outcome.Sides, s => Assert.True(s.Destroyed));
    }

    [Fact]
    public void Holding_the_ground_is_worth_something()
    {
        // b is slightly the lighter force and would lose in the open...
        var a = Force("a", "dave", 10, 100, 1);  // 1000
        var b = Force("b", "wild", 9, 100, 1);   // 900

        Assert.Equal("a", Resolver.Resolve(Fight(), new[] { a, b }, seed: 1).WinnerEntityId);

        // ...but it was standing there when the attacker arrived.
        Assert.Equal("b", Resolver.Resolve(Fight(defenderStationary: true), new[] { a, b }, seed: 1).WinnerEntityId);
    }

    [Fact]
    public void District_assault_reads_defender_bonus_as_zero_so_it_is_not_paid_twice()
    {
        // base-defense siege-objective section 7: the SAME entrenched matchup that flips a Sector-kind
        // battle to the defender above must NOT flip a District-kind one -- structure-state/siege-cover
        // model the real fortification bonus on the board itself, and stacking the placeholder's flat
        // 1250 on top would pay the defender twice for the same thing.
        var a = Force("a", "dave", 10, 100, 1);  // 1000
        var b = Force("b", "wild", 9, 100, 1);   // 900

        var districtRequest = Fight(defenderStationary: true) with { Kind = BattleKinds.District };
        Assert.Equal("a", Resolver.Resolve(districtRequest, new[] { a, b }, seed: 1).WinnerEntityId);

        // The non-district path is untouched -- still reads the real, tunable 1250.
        Assert.Equal("b", Resolver.Resolve(Fight(defenderStationary: true), new[] { a, b }, seed: 1).WinnerEntityId);
    }

    [Fact]
    public void The_result_does_not_depend_on_which_side_is_listed_first()
    {
        var a = Force("a", "dave", 3, 110, 1);
        var b = Force("b", "wild", 2, 140, 2);

        var forward = Resolver.Resolve(Fight(), new[] { a, b }, seed: 1);
        var reversed = Resolver.Resolve(Fight(), new[] { b, a }, seed: 1);

        Assert.Equal(forward.WinnerEntityId, reversed.WinnerEntityId);
        Assert.Equal(
            forward.Sides.OrderBy(s => s.EntityId, StringComparer.Ordinal).Select(s => s.Survivors.Count),
            reversed.Sides.OrderBy(s => s.EntityId, StringComparer.Ordinal).Select(s => s.Survivors.Count));
    }

    [Fact]
    public void A_guard_falls_to_anyone_still_standing_but_never_for_free()
    {
        var a = Force("a", "dave", 3, 110, 1);
        var request = new BattleRequest
        {
            BattleId = "g1",
            Kind = BattleKinds.Guard,
            LocationId = "ember-hollow",
            AttackerEntityId = "a",
            GuardWaveId = "guard-light",
            SlotIndex = 2
        };

        var outcome = Resolver.Resolve(request, new[] { a }, seed: 1);

        Assert.True(outcome.GuardCleared);
        Assert.Equal("a", outcome.WinnerEntityId);
        var side = Assert.Single(outcome.Sides);
        Assert.Equal(3, side.Survivors.Count);
        Assert.All(side.Survivors, m => Assert.True(m.Wounds > 0, "clearing a guard costs something"));
    }

    [Fact]
    public void A_force_with_nothing_left_cannot_clear_a_guard()
    {
        var spent = Force("a", "dave", 2, 100, 1) with
        {
            Members = new WorldEntityMember[]
            {
                new() { SpeciesId = "normalzombie", Level = 1, Hp = 100, Wounds = 100 },
                new() { SpeciesId = "normalzombie", Level = 1, Hp = 100, Wounds = 100 }
            }
        };

        var outcome = Resolver.Resolve(new BattleRequest
        {
            BattleId = "g1",
            Kind = BattleKinds.Guard,
            LocationId = "ember-hollow",
            AttackerEntityId = "a",
            GuardWaveId = "guard-light",
            SlotIndex = 2
        }, new[] { spent }, seed: 1);

        Assert.False(outcome.GuardCleared);
        Assert.Null(outcome.WinnerEntityId);
    }
}
