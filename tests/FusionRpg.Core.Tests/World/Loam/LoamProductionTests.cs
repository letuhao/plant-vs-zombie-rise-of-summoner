using FusionRpg.Core.World;
using FusionRpg.Core.World.Loam;
using Xunit;

namespace FusionRpg.Core.Tests.World.Loam;

/// <summary>L7 acceptance (spec-loam-calc.md #1): a hand-built fixture, workable on paper.</summary>
public class LoamProductionTests
{
    static WorldSlot Rootbed(int index) => new() { SlotIndex = index, SlotTypeId = SlotTypeCatalog.RootbedSlotTypeId };
    static WorldSlot NonSource(int index) => new() { SlotIndex = index, SlotTypeId = SlotTypeCatalog.WildlandSlotTypeId };

    [Fact]
    public void One_rootbed_produces_exactly_the_seep_constant()
    {
        var sector = new WorldSector { SectorId = "s", OwnerFactionId = "f1", Slots = new[] { Rootbed(0) } };
        Assert.Equal(LoamPolicy.SeepPerTurn, LoamProduction.For(sector));
    }

    [Fact]
    public void Two_rootbeds_produce_double_and_a_non_source_slot_adds_nothing()
    {
        var sector = new WorldSector
        {
            SectorId = "s", OwnerFactionId = "f1",
            Slots = new[] { Rootbed(0), Rootbed(1), NonSource(2) }
        };
        Assert.Equal(LoamPolicy.SeepPerTurn * 2, LoamProduction.For(sector));
    }

    [Fact]
    public void Reversing_slot_order_changes_no_answer()
    {
        var forward = new WorldSector
        {
            SectorId = "s", OwnerFactionId = "f1",
            Slots = new[] { Rootbed(0), NonSource(1), Rootbed(2) }
        };
        var reversed = forward with { Slots = forward.Slots.Reverse().ToList() };

        Assert.Equal(LoamProduction.For(forward), LoamProduction.For(reversed));
    }

    [Fact]
    public void An_unowned_sector_produces_nothing_even_with_a_rootbed()
    {
        var sector = new WorldSector { SectorId = "s", OwnerFactionId = null, Slots = new[] { Rootbed(0) } };
        Assert.Equal(0, LoamProduction.For(sector));
    }

    [Fact]
    public void The_belief_overload_produces_nothing_for_an_unowned_sector_even_with_a_rootbed()
    {
        // The truth side's own G-B guard (`An_unowned_sector_produces_nothing_even_with_a_rootbed`)
        // has no belief-side twin — a mutation pass (2026-08-23, loam-texture Checkpoint 10) found
        // the belief overload's identical guard could be deleted outright with nothing noticing.
        Assert.Equal(0, LoamProduction.For(null, new[] { SlotTypeCatalog.RootbedSlotTypeId }));
    }

    [Fact]
    public void The_belief_overload_agrees_with_the_truth_overload()
    {
        var sector = new WorldSector
        {
            SectorId = "s", OwnerFactionId = "f1",
            Slots = new[] { Rootbed(0), NonSource(1) }
        };

        Assert.Equal(
            LoamProduction.For(sector),
            LoamProduction.For(sector.OwnerFactionId, sector.Slots.Select(sl => sl.SlotTypeId)));
    }
}
