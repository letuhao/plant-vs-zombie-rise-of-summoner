using FusionRpg.Core.World;
using FusionRpg.Core.World.Loam;
using Xunit;

namespace FusionRpg.Core.Tests.World.Loam;

/// <summary>L8 acceptance (spec-loam-calc.md #6): the owner's settlement rule.</summary>
public class HabitabilityTests
{
    [Fact]
    public void A_sector_with_a_rootbed_is_habitable()
    {
        var sector = new WorldSector
        {
            SectorId = "s", TypeId = "stable",
            Slots = new[] { new WorldSlot { SlotIndex = 0, SlotTypeId = SlotTypeCatalog.RootbedSlotTypeId } }
        };
        Assert.True(Habitability.For(sector));
    }

    [Fact]
    public void The_same_sector_shape_with_no_rootbed_is_not()
    {
        var sector = new WorldSector
        {
            SectorId = "s", TypeId = "stable",
            Slots = new[] { new WorldSlot { SlotIndex = 0, SlotTypeId = SlotTypeCatalog.WildlandSlotTypeId } }
        };
        Assert.False(Habitability.For(sector));
    }

    [Fact]
    public void A_sector_with_no_slots_at_all_is_never_habitable()
    {
        var sector = new WorldSector { SectorId = "s", TypeId = "stable" };
        Assert.False(Habitability.For(sector));
    }

    [Fact]
    public void The_belief_overload_agrees_with_the_truth_overload()
    {
        var sector = new WorldSector
        {
            SectorId = "s", TypeId = "stable",
            Slots = new[] { new WorldSlot { SlotIndex = 0, SlotTypeId = SlotTypeCatalog.RootbedSlotTypeId } }
        };
        Assert.Equal(Habitability.For(sector), Habitability.For(sector.Slots.Select(sl => sl.SlotTypeId)));
    }
}
