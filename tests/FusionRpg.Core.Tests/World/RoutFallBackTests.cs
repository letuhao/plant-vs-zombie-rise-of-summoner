using FusionRpg.Core.World;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// world-map: a routed force falls back rather than standing where the fight left it
/// (spec-world-movement.md's own "`routed` — routed legions fall back and skip a turn's orders",
/// specced but never wired — the owner confirmed directly, 2026-09-05, that it should be).
///
/// <see cref="RoutLifecycleTests"/> already proves the turn-cost bookkeeping using a stationary
/// Contact standoff (both forces `AtSectorId`, never mid-lane) — this file is the lane-crossing
/// counterpart, the one shape that actually has somewhere to fall back *to*.
/// </summary>
public class RoutFallBackTests
{
    /// <summary>The attacker always routs, the defender always wins, nobody dies either way.</summary>
    sealed class AttackerAlwaysRouts : IBattleResolver
    {
        public BattleOutcome Resolve(BattleRequest request, IReadOnlyList<WorldEntity> combatants, ulong seed)
        {
            var attacker = combatants.Single(e => e.EntityId == request.AttackerEntityId);
            var defender = combatants.Single(e => e.EntityId == request.DefenderEntityId);

            return new BattleOutcome
            {
                BattleId = request.BattleId,
                WinnerEntityId = defender.EntityId,
                Sides = new[]
                {
                    new BattleSideOutcome { EntityId = defender.EntityId, Survivors = defender.Members },
                    new BattleSideOutcome { EntityId = attacker.EntityId, Survivors = attacker.Members, Routed = true }
                }
            };
        }
    }

    /// <summary>Dave's legion, mid-crossing `l-home-ember` toward `ember-hollow`; the wild pack meets
    /// it head-on from the other end, matching <see cref="CrossingSymmetryTests"/>'s own fixture
    /// shape. `MovementRemaining` is generous enough that neither side's own budget, only the fight
    /// itself, decides what happens this turn.</summary>
    static WorldState MidCrossing() =>
        WorldValidation.Validate(WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1) is var world
            ? world with
            {
                Entities = world.Entities
                    .Select(e => e.EntityId switch
                    {
                        "e-dave-legion-1" => e with
                        {
                            AtSectorId = null,
                            OnLaneId = "l-home-ember",
                            OnLaneTowardSectorId = "ember-hollow",
                            LaneProgressMilli = 400,
                            MovementRemaining = 1000
                        },
                        "e-wild-pack-1" => e with
                        {
                            AtSectorId = null,
                            OnLaneId = "l-home-ember",
                            OnLaneTowardSectorId = "homeworld",
                            LaneProgressMilli = 400,
                            MovementRemaining = 1000,
                            Stance = "march"
                        },
                        _ => e
                    })
                    .ToList()
            }
            : throw new InvalidOperationException());

    static WorldEntity Legion(WorldState w) => w.Entities.Single(e => e.EntityId == "e-dave-legion-1");

    static WorldCommand March(string commander, string entityId) => new()
    {
        CommanderId = commander,
        CommandId = "m-" + entityId,
        Kind = WorldCommandKinds.Move,
        EntityId = entityId,
        LanePath = new[] { "l-home-ember" }
    };

    [Fact]
    public void A_legion_routed_mid_lane_falls_back_to_the_sector_it_crossed_from()
    {
        // Both budgets and starting progress are generous enough that the 200-milli gap between
        // them closes this same turn (matching CrossingSymmetryTests' own "did they meet" check) —
        // orders are what actually close the gap, not merely standing on the lane.
        var result = TurnEngine.Step(
            MidCrossing(),
            new[] { March("dave", "e-dave-legion-1"), March("wild", "e-wild-pack-1") },
            seed: 1, new AttackerAlwaysRouts());

        Assert.Contains(result.Report.Entries, e => e.Kind == TurnReportKinds.Battle); // sanity: they met

        var legion = Legion(result.World);
        Assert.True(legion.Routed);
        // `l-home-ember`'s ends are homeworld/ember-hollow; heading toward ember-hollow, "the way it
        // came" is homeworld.
        Assert.Equal("homeworld", legion.AtSectorId);
        Assert.Null(legion.OnLaneId);
        Assert.Null(legion.OnLaneTowardSectorId);
        Assert.Equal(0, legion.LaneProgressMilli);
    }

    [Fact]
    public void Falling_back_does_not_touch_a_legion_that_was_not_newly_routed_this_fight()
    {
        // The defender in this fixture never routs — its own lane position must be untouched by
        // BattleApplication's fall-back logic, proving the reversal is scoped to the routed side only.
        var result = TurnEngine.Step(MidCrossing(), Array.Empty<WorldCommand>(), seed: 1, new AttackerAlwaysRouts());

        var wildPack = result.World.Entities.Single(e => e.EntityId == "e-wild-pack-1");
        Assert.False(wildPack.Routed);
        Assert.Equal("l-home-ember", wildPack.OnLaneId);
        Assert.Equal("homeworld", wildPack.OnLaneTowardSectorId);
    }

    [Fact]
    public void A_force_already_routed_this_turn_does_not_fall_back_a_second_time()
    {
        // Route it once (falls back to homeworld), then feed the same already-routed world straight
        // back into a second fight naming the same entity — BattleApplication must not reverse a
        // force that arrived at this fight already routed (Apply's own `newlyRouted` gate).
        var first = TurnEngine.Step(MidCrossing(), Array.Empty<WorldCommand>(), seed: 1, new AttackerAlwaysRouts());
        var routedAtHomeworld = Legion(first.World);
        Assert.Equal("homeworld", routedAtHomeworld.AtSectorId);

        var outcome = new BattleOutcome
        {
            BattleId = "b2",
            Sides = new[]
            {
                new BattleSideOutcome { EntityId = routedAtHomeworld.EntityId, Survivors = routedAtHomeworld.Members, Routed = true }
            }
        };

        var again = FusionRpg.Core.World.Turn.BattleApplication.Apply(first.World, outcome);
        var stillAtHomeworld = again.Entities.Single(e => e.EntityId == "e-dave-legion-1");
        Assert.Equal("homeworld", stillAtHomeworld.AtSectorId);
        Assert.Null(stillAtHomeworld.OnLaneId);
    }
}
