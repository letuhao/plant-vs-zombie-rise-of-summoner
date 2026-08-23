using FusionRpg.Core.World;
using FusionRpg.Core.World.Loam;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World.Loam;

/// <summary>
/// L27 (spec-loam-legions.md): `LegionSupply.Resolve` wired after `LoamPhases.Pressure` — a legion
/// beyond supply burns toward zero and is destroyed outright, one inside supply tops up for free
/// from whatever the pool has left after sector upkeep, never burning.
/// </summary>
public class LegionSupplyResolveTests
{
    const string Dave = "dave";

    static WorldEntity Legion(string id, string atSectorId, int fighters, int bearers, long carriedLoam) => new()
    {
        EntityId = id, Kind = WorldEntityKind.Legion, OwnerFactionId = Dave, AtSectorId = atSectorId,
        CarriedLoam = carriedLoam,
        Members = Enumerable.Range(0, fighters)
            .Select(_ => new WorldEntityMember { SpeciesId = "grunt" })
            .Concat(Enumerable.Range(0, bearers)
                .Select(_ => new WorldEntityMember { SpeciesId = "grunt", Role = WorldEntityMemberRole.Bearer }))
            .ToList()
    };

    static WorldState Fixture(long homeStock = 0) => new()
    {
        WorldId = "w", TemplateId = "t", Seed = 1,
        Factions = new[] { new WorldFaction { FactionId = Dave, Kind = WorldFactionKind.Player, Name = "Dave" } },
        Sectors = new[]
        {
            new WorldSector
            {
                SectorId = "home", TypeId = "stable", OwnerFactionId = Dave, Phase = SectorPhase.Held,
                LoamStock = homeStock,
                Slots = new[] { new WorldSlot { SlotIndex = 0, SlotTypeId = SlotTypeCatalog.SeatSlotTypeId, State = SlotState.Claimed, OwnerFactionId = Dave } }
            },
            new WorldSector { SectorId = "field", TypeId = "stable", Phase = SectorPhase.Unknown }
        }
    };

    [Fact]
    public void A_legion_beyond_supply_survives_exactly_capacity_over_burn_turns()
    {
        var legion = Legion("legion", atSectorId: "field", fighters: 2, bearers: 1, carriedLoam: 0);
        var capacity = LegionSupply.Capacity(legion);
        var burn = LegionSupply.Burn(legion);
        var expectedLeash = (int)(capacity / burn);

        var world = Fixture() with { Entities = new[] { legion with { CarriedLoam = capacity } } };

        for (var i = 0; i < expectedLeash; i++)
        {
            world = LegionSupply.Resolve(world, new TurnReport(), "pressure");
            Assert.Contains(world.Entities, e => e.EntityId == "legion");
        }

        world = LegionSupply.Resolve(world, new TurnReport(), "pressure");
        Assert.DoesNotContain(world.Entities, e => e.EntityId == "legion");
    }

    [Fact]
    public void Destruction_is_reported_the_turn_it_happens()
    {
        var legion = Legion("legion", atSectorId: "field", fighters: 3, bearers: 0, carriedLoam: 0);
        var world = Fixture() with { Entities = new[] { legion } };

        var report = new TurnReport();
        LegionSupply.Resolve(world, report, "pressure");

        Assert.Contains(report.Entries, e => e.Detail.StartsWith("legion.starved"));
    }

    [Fact]
    public void A_legion_in_supply_tops_up_from_what_the_pool_has_left_after_upkeep()
    {
        var legion = Legion("legion", atSectorId: "home", fighters: 2, bearers: 1, carriedLoam: 0);
        var capacity = LegionSupply.Capacity(legion);

        // Enough left in the pool (post-Pressure, by construction of this fixture) to fully cover
        // the legion's demand.
        var world = Fixture(homeStock: capacity + 100) with { Entities = new[] { legion } };

        var result = LegionSupply.Resolve(world, new TurnReport(), "pressure");

        Assert.Equal(capacity, result.Entities.Single(e => e.EntityId == "legion").CarriedLoam);
        Assert.Equal(100, result.Sectors.Single(s => s.SectorId == "home").LoamStock);
    }

    [Fact]
    public void A_top_up_never_exceeds_what_the_pool_actually_holds()
    {
        var legion = Legion("legion", atSectorId: "home", fighters: 2, bearers: 1, carriedLoam: 0);
        var capacity = LegionSupply.Capacity(legion);

        // Only half of what the legion wants is actually available.
        var world = Fixture(homeStock: capacity / 2) with { Entities = new[] { legion } };

        var result = LegionSupply.Resolve(world, new TurnReport(), "pressure");

        Assert.Equal(capacity / 2, result.Entities.Single(e => e.EntityId == "legion").CarriedLoam);
        Assert.Equal(0, result.Sectors.Single(s => s.SectorId == "home").LoamStock);
    }

    [Fact]
    public void An_in_supply_legion_never_burns_even_with_no_bearers()
    {
        var legion = Legion("legion", atSectorId: "home", fighters: 3, bearers: 0, carriedLoam: 5);
        var world = Fixture(homeStock: 0) with { Entities = new[] { legion } };

        var report = new TurnReport();
        var result = LegionSupply.Resolve(world, report, "pressure");

        Assert.Equal(5, result.Entities.Single(e => e.EntityId == "legion").CarriedLoam);
        Assert.DoesNotContain(report.Entries, e => e.Detail.StartsWith("legion.starved"));
    }
}
