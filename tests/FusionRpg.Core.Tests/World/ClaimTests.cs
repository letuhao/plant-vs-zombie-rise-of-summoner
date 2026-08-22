using FusionRpg.Core.World;
using FusionRpg.Core.World.Movement;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// W11 (spec-world-movement.md §Claiming): holding ground. A sector falls only when nothing hostile
/// stands in it and every slot has been cleared — so a rich sector costs several turns and several
/// fights, which is the intended shape of the map rather than a side effect.
/// </summary>
public class ClaimTests
{
    static WorldState World() => WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1);

    static WorldCommand Claim(string commander, string entityId, string sectorId) => new()
    {
        CommanderId = commander,
        CommandId = "k-" + entityId,
        Kind = WorldCommandKinds.Claim,
        EntityId = entityId,
        SectorId = sectorId
    };

    static WorldCommand Move(string commander, string entityId, params string[] lanePath) => new()
    {
        CommanderId = commander,
        CommandId = "m-" + entityId,
        Kind = WorldCommandKinds.Move,
        EntityId = entityId,
        LanePath = lanePath
    };

    static WorldCommand Clear(string commander, string entityId, string sectorId, int slotIndex) => new()
    {
        CommanderId = commander,
        CommandId = "c-" + entityId + "-" + slotIndex,
        Kind = WorldCommandKinds.Clear,
        EntityId = entityId,
        SectorId = sectorId,
        SlotIndex = slotIndex
    };

    static WorldState Place(WorldState w, string entityId, string sectorId, int movement = 1000) => w with
    {
        Entities = w.Entities
            .Select(e => e.EntityId == entityId
                ? e with
                {
                    AtSectorId = sectorId, OnLaneId = null, OnLaneTowardSectorId = null,
                    LaneProgressMilli = 0, MovementRemaining = movement, Stance = "march"
                }
                : e)
            .ToList()
    };

    /// <summary>Every guard in a sector already beaten — the state a `clear` campaign ends in.</summary>
    static WorldState AllGuardsCleared(WorldState w, string sectorId) => w with
    {
        Sectors = w.Sectors
            .Select(s => s.SectorId == sectorId
                ? s with { Slots = s.Slots.Select(sl => sl with { GuardState = GuardState.Cleared }).ToList() }
                : s)
            .ToList()
    };

    static WorldSector Sector(WorldState w, string id) => w.Sectors.Single(s => s.SectorId == id);

    [Fact]
    public void A_cleared_sector_you_are_standing_in_becomes_yours()
    {
        var world = Place(AllGuardsCleared(World(), "ember-hollow"), "e-dave-legion-1", "ember-hollow");

        var result = TurnEngine.Step(world, new[] { Claim("dave", "e-dave-legion-1", "ember-hollow") }, seed: 1);

        var ember = Sector(result.World, "ember-hollow");
        Assert.Equal("dave", ember.OwnerFactionId);
        Assert.Equal(SectorPhase.Held, ember.Phase);
        Assert.Equal(result.World.CurrentTurn, ember.LastSeenTurn);
    }

    [Fact]
    public void A_sector_with_any_guard_left_standing_cannot_be_claimed()
    {
        // ember-hollow slot 3 is a lair with a live guard; slot 2 is cleared first so the reason has
        // to name the one that is actually still in the way.
        var world = World();
        var partly = world with
        {
            Sectors = world.Sectors
                .Select(s => s.SectorId == "ember-hollow"
                    ? s with
                    {
                        Slots = s.Slots
                            .Select(sl => sl.SlotIndex == 2 ? sl with { GuardState = GuardState.Cleared } : sl)
                            .ToList()
                    }
                    : s)
                .ToList()
        };

        var result = TurnEngine.Step(Place(partly, "e-dave-legion-1", "ember-hollow"),
            new[] { Claim("dave", "e-dave-legion-1", "ember-hollow") }, seed: 1);

        var dropped = Assert.Single(result.Report.Dropped);
        Assert.Equal("claim.guarded:3", dropped.Detail);
        Assert.Null(Sector(result.World, "ember-hollow").OwnerFactionId);
    }

    [Fact]
    public void Claiming_what_you_already_hold_is_a_reported_no_op()
    {
        var result = TurnEngine.Step(World(), new[] { Claim("dave", "e-dave-legion-1", "homeworld") }, seed: 1);

        Assert.Empty(result.Report.Dropped);
        Assert.Contains(result.Report.Entries, e => e.Detail.Contains("claim.already-yours"));
        Assert.Equal("dave", Sector(result.World, "homeworld").OwnerFactionId);
    }

    [Fact]
    public void A_sector_a_hostile_force_stands_in_cannot_be_claimed()
    {
        // The wild pack lives in ash-waste. Dave arrives heavy enough to beat it but not to wipe it
        // out, so a broken but still-present enemy is what blocks the claim.
        var world = AllGuardsCleared(World(), "ash-waste");
        world = world with
        {
            Entities = world.Entities
                .Select(e => e.EntityId == "e-dave-legion-1"
                    ? e with { Members = e.Members.Select(m => m with { Hp = 400 }).ToList() }
                    : e)
                .ToList()
        };
        world = Place(world, "e-dave-legion-1", "ash-waste");

        var result = TurnEngine.Step(world, new[] { Claim("dave", "e-dave-legion-1", "ash-waste") }, seed: 1);

        Assert.Null(Sector(result.World, "ash-waste").OwnerFactionId);
        Assert.Contains(result.Report.Dropped, e => e.Detail == "claim.contested");
    }

    [Fact]
    public void Claiming_a_sector_you_are_not_standing_in_is_dropped()
    {
        var world = AllGuardsCleared(World(), "ember-hollow");

        var result = TurnEngine.Step(world, new[] { Claim("dave", "e-dave-legion-1", "ember-hollow") }, seed: 1);

        Assert.Contains(result.Report.Dropped, e => e.Detail == "claim.elsewhere");
        Assert.Null(Sector(result.World, "ember-hollow").OwnerFactionId);
    }

    /// <summary>
    /// Two factions cannot both claim one sector, and there is no separate tie-break for it: while
    /// both are standing there each blocks the other, and the fight that put them in the same place
    /// is what eventually decides it. That is the contest, and it is worth having a test that says so.
    /// </summary>
    [Fact]
    public void Two_factions_claiming_one_sector_settle_it_by_fighting_over_it()
    {
        var world = AllGuardsCleared(World(), "ember-hollow");
        world = Place(world, "e-wild-pack-1", "ember-hollow", movement: 0);

        var first = TurnEngine.Step(world, new[]
        {
            Move("dave", "e-dave-legion-1", "l-home-ember"),
            Claim("dave", "e-dave-legion-1", "ember-hollow"),
            Claim("wild", "e-wild-pack-1", "ember-hollow")
        }, seed: 1);

        // Nobody holds it while both are still on it.
        Assert.Null(Sector(first.World, "ember-hollow").OwnerFactionId);
        Assert.Contains(first.Report.Dropped, e => e.Subject == "k-e-wild-pack-1" && e.Detail == "claim.contested");

        // The next turn finishes the routed legion, and the survivor takes the ground.
        var second = TurnEngine.Step(first.World,
            new[] { Claim("wild", "e-wild-pack-1", "ember-hollow") }, seed: 1);

        Assert.Null(second.World.Entities.FirstOrDefault(e => e.EntityId == "e-dave-legion-1"));
        Assert.Equal("wild", Sector(second.World, "ember-hollow").OwnerFactionId);
    }

    [Fact]
    public void A_second_legion_claiming_what_its_own_side_just_took_is_a_no_op()
    {
        var world = AllGuardsCleared(World(), "ember-hollow");
        var reinforced = world with
        {
            Entities = world.Entities
                .Append(world.Entities.Single(e => e.EntityId == "e-dave-legion-1") with
                {
                    EntityId = "e-dave-legion-2", AtSectorId = "ember-hollow"
                })
                .OrderBy(e => e.EntityId, StringComparer.Ordinal)
                .ToList()
        };
        reinforced = Place(WorldValidation.Validate(reinforced), "e-dave-legion-1", "ember-hollow");

        var result = TurnEngine.Step(reinforced, new[]
        {
            Claim("dave", "e-dave-legion-1", "ember-hollow"),
            Claim("dave", "e-dave-legion-2", "ember-hollow")
        }, seed: 1);

        Assert.Equal("dave", Sector(result.World, "ember-hollow").OwnerFactionId);
        Assert.Empty(result.Report.Dropped);
        Assert.Contains(result.Report.Entries, e => e.Detail.Contains("claim.already-yours"));
    }

    [Fact]
    public void March_clear_clear_claim_takes_a_sector_over_four_turns()
    {
        var state = World();

        var t1 = TurnEngine.Step(state, new[] { Move("dave", "e-dave-legion-1", "l-home-ember") }, seed: 1);
        Assert.Equal("ember-hollow", t1.World.Entities.Single(e => e.EntityId == "e-dave-legion-1").AtSectorId);

        var t2 = TurnEngine.Step(t1.World, new[] { Clear("dave", "e-dave-legion-1", "ember-hollow", 2) }, seed: 1);
        var t3 = TurnEngine.Step(t2.World, new[] { Clear("dave", "e-dave-legion-1", "ember-hollow", 3) }, seed: 1);
        Assert.All(Sector(t3.World, "ember-hollow").Slots, sl => Assert.Equal(GuardState.Cleared, sl.GuardState));

        var t4 = TurnEngine.Step(t3.World, new[] { Claim("dave", "e-dave-legion-1", "ember-hollow") }, seed: 1);
        Assert.Equal("dave", Sector(t4.World, "ember-hollow").OwnerFactionId);
        Assert.Equal(SectorPhase.Held, Sector(t4.World, "ember-hollow").Phase);
    }

    [Fact]
    public void Claiming_is_deterministic_and_independent_of_command_order()
    {
        var world = Place(AllGuardsCleared(World(), "ember-hollow"), "e-dave-legion-1", "ember-hollow");
        var claim = Claim("dave", "e-dave-legion-1", "ember-hollow");
        var stand = new WorldCommand
        {
            CommanderId = "wild", CommandId = "s1", Kind = WorldCommandKinds.StandFast
        };

        var forward = TurnEngine.Step(world, new[] { claim, stand }, seed: 1);
        var reversed = TurnEngine.Step(world, new[] { stand, claim }, seed: 1);

        Assert.Equal(forward.StateHash, reversed.StateHash);
    }

    [Fact]
    public void A_claim_without_a_sector_is_refused_at_admission()
    {
        var (ok, reason) = WorldCommandAdmission.Admit(World(), new WorldCommand
        {
            CommanderId = "dave",
            CommandId = "k1",
            Kind = WorldCommandKinds.Claim,
            EntityId = "e-dave-legion-1"
        });

        Assert.False(ok);
        Assert.Equal("sector.missing", reason);
    }

    [Fact]
    public void Claiming_never_costs_a_legion_its_march_budget()
    {
        var world = Place(AllGuardsCleared(World(), "ember-hollow"), "e-dave-legion-1", "ember-hollow");
        var result = TurnEngine.Step(world, new[] { Claim("dave", "e-dave-legion-1", "ember-hollow") }, seed: 1);

        Assert.All(result.World.Entities,
            e => Assert.Equal(MovementPolicy.BudgetFor(e.Stance), e.MovementRemaining));
    }
}
