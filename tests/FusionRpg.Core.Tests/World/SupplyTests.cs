using FusionRpg.Core.World;
using FusionRpg.Core.World.Movement;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// W12 (spec-world-movement.md §Supply connectivity): the chain back to the homeworld. It is
/// recomputed from scratch every turn rather than cached, because a stored "in supply" flag is
/// exactly the kind of derived state that rots the first time a lane is cut.
/// </summary>
public class SupplyTests
{
    static WorldState World() => WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1);

    static WorldState Own(WorldState w, string factionId, params string[] sectorIds) => w with
    {
        Sectors = w.Sectors
            .Select(s => sectorIds.Contains(s.SectorId)
                ? s with { OwnerFactionId = factionId, Phase = SectorPhase.Held }
                : s)
            .ToList()
    };

    static WorldState Sever(WorldState w, string laneId) => w with
    {
        Lanes = w.Lanes.Select(l => l.LaneId == laneId ? l with { State = LaneState.Severed } : l).ToList()
    };

    static WorldState Place(WorldState w, string entityId, string sectorId) => w with
    {
        Entities = w.Entities
            .Select(e => e.EntityId == entityId
                ? e with
                {
                    AtSectorId = sectorId, OnLaneId = null, OnLaneTowardSectorId = null,
                    LaneProgressMilli = 0, MovementRemaining = 1000
                }
                : e)
            .ToList()
    };

    /// <summary>
    /// Moves the wild pack off the frontier. Every sector but ash-waste carries a Seat, and a Seat
    /// you hold is a supply source in its own right — so ash-waste is the only holding that can be
    /// cut off at all, and these tests need it empty.
    /// </summary>
    static WorldState Banish(WorldState w) => Place(w, "e-wild-pack-1", "black-gate");

    static int Wounds(WorldState w, string entityId) =>
        w.Entities.Single(e => e.EntityId == entityId).Members.Sum(m => m.Wounds);

    static WorldCommand Stand() => new()
    {
        CommanderId = "dave", CommandId = "s1", Kind = WorldCommandKinds.StandFast
    };

    [Fact]
    public void The_homeworld_is_its_own_supply_source()
    {
        var connected = SupplyGraph.ConnectedSectors(World(), "dave");
        Assert.Contains("homeworld", connected);
    }

    [Fact]
    public void A_held_neighbour_of_the_homeworld_is_in_supply()
    {
        var world = Own(World(), "dave", "ember-hollow");
        var connected = SupplyGraph.ConnectedSectors(world, "dave");

        Assert.Contains("ember-hollow", connected);
        Assert.DoesNotContain("ash-waste", connected);   // not held, so not part of the chain
    }

    [Fact]
    public void Cutting_one_junction_disconnects_exactly_what_was_behind_it()
    {
        var world = Own(Banish(World()), "dave", "ember-hollow", "ash-waste");
        Assert.Equal(
            new[] { "ash-waste", "ember-hollow", "homeworld" },
            SupplyGraph.ConnectedSectors(world, "dave").OrderBy(s => s, StringComparer.Ordinal));

        // l-ember-ash is the only owned way into ash-waste; ember-hollow keeps its own Seat.
        var cut = Sever(world, "l-ember-ash");
        var connected = SupplyGraph.ConnectedSectors(cut, "dave");

        Assert.Equal(
            new[] { "ember-hollow", "homeworld" },
            connected.OrderBy(s => s, StringComparer.Ordinal));
    }

    [Fact]
    public void Reconnecting_the_lane_restores_the_chain()
    {
        var world = Own(Banish(World()), "dave", "ember-hollow", "ash-waste");
        var cut = Sever(world, "l-ember-ash");
        var healed = cut with
        {
            Lanes = cut.Lanes.Select(l => l.LaneId == "l-ember-ash" ? l with { State = LaneState.Open } : l).ToList()
        };

        Assert.Equal(3, SupplyGraph.ConnectedSectors(healed, "dave").Count);
    }

    [Fact]
    public void Supply_refuses_to_route_through_a_sector_a_hostile_force_stands_in()
    {
        var world = Own(World(), "dave", "ember-hollow", "ash-waste");
        world = Place(world, "e-wild-pack-1", "ember-hollow");

        var connected = SupplyGraph.ConnectedSectors(world, "dave");

        Assert.DoesNotContain("ember-hollow", connected);
        Assert.DoesNotContain("ash-waste", connected);   // the only way there ran through ember-hollow
    }

    [Fact]
    public void A_faction_with_no_seat_of_its_own_has_no_supply_and_never_starves()
    {
        var world = World();
        Assert.Empty(SupplyGraph.ConnectedSectors(world, "wild"));

        var before = Wounds(world, "e-wild-pack-1");
        var result = TurnEngine.Step(world, new[] { Stand() }, seed: 1);

        Assert.Equal(before, Wounds(result.World, "e-wild-pack-1"));
    }

    [Fact]
    public void A_legion_out_of_supply_takes_attrition_once_a_turn()
    {
        // verdant-shelf is unowned and empty — starvation with nothing else happening in it.
        var world = Place(World(), "e-dave-legion-1", "verdant-shelf");

        var first = TurnEngine.Step(world, new[] { Stand() }, seed: 1);
        var afterOne = Wounds(first.World, "e-dave-legion-1");
        Assert.True(afterOne > 0, "an unsupplied legion should be bleeding");

        var second = TurnEngine.Step(first.World, new[] { Stand() }, seed: 1);
        var afterTwo = Wounds(second.World, "e-dave-legion-1");

        // One turn, one bite — not one per member, per event, or per phase.
        Assert.Equal(afterOne * 2, afterTwo);
        Assert.Contains(first.Report.Entries, e => e.Detail.StartsWith("attrition"));
    }

    [Fact]
    public void A_legion_standing_in_supply_takes_none()
    {
        var result = TurnEngine.Step(World(), new[] { Stand() }, seed: 1);
        Assert.Equal(0, Wounds(result.World, "e-dave-legion-1"));
    }

    [Fact]
    public void A_disconnected_holding_is_reported()
    {
        var world = Own(Banish(World()), "dave", "ember-hollow", "ash-waste");
        var cut = Sever(world, "l-ember-ash");

        var result = TurnEngine.Step(cut, new[] { Stand() }, seed: 1);

        Assert.Contains(result.Report.Entries, e => e.Detail == "supply.cut:ash-waste");
    }

    [Fact]
    public void Supply_is_recomputed_every_turn_rather_than_carried_forward()
    {
        var world = Own(Banish(World()), "dave", "ember-hollow", "ash-waste");
        var first = TurnEngine.Step(world, new[] { Stand() }, seed: 1);
        Assert.DoesNotContain(first.Report.Entries, e => e.Detail.StartsWith("supply.cut"));

        // Nothing was stored, so cutting the lane between turns changes the answer immediately.
        var second = TurnEngine.Step(Sever(first.World, "l-ember-ash"), new[] { Stand() }, seed: 1);
        Assert.Contains(second.Report.Entries, e => e.Detail == "supply.cut:ash-waste");
    }

    [Fact]
    public void Attrition_eventually_finishes_a_stranded_legion()
    {
        var state = Place(World(), "e-dave-legion-1", "verdant-shelf");

        for (var turn = 0; turn < 40 && state.Entities.Any(e => e.EntityId == "e-dave-legion-1"); turn++)
            state = TurnEngine.Step(state, Array.Empty<WorldCommand>(), seed: 1).World;

        Assert.DoesNotContain(state.Entities, e => e.EntityId == "e-dave-legion-1");
    }
}
