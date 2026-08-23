using FusionRpg.Core.World;
using FusionRpg.Core.World.Loam;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World.Loam;

/// <summary>
/// L38 (spec-loam-texture.md): the granary raises capacity, additive to `LoamProduction`'s existing
/// shape — the same discipline `loam-structures`' own well multiplier already followed.
/// </summary>
public class LoamTextureTests
{
    static WorldSector Sector(long loamStock, params WorldSlot[] slots) => new()
    {
        SectorId = "s", TypeId = "stable", OwnerFactionId = "dave", LoamStock = loamStock, Slots = slots
    };

    static WorldSlot Rootbed() => new() { SlotIndex = 0, SlotTypeId = SlotTypeCatalog.RootbedSlotTypeId };

    static WorldSlot Granary(int? constructionTurnsRemaining = null) => new()
    {
        SlotIndex = 1, SlotTypeId = SlotTypeCatalog.WildlandSlotTypeId,
        StructureId = "granary", ConstructionTurnsRemaining = constructionTurnsRemaining
    };

    [Fact]
    public void A_sector_with_an_active_granary_has_a_raised_effective_capacity()
    {
        var withGranary = Sector(0, Rootbed(), Granary());
        var without = Sector(0, Rootbed());

        Assert.Equal(LoamPolicy.LoamCapacity + LoamPolicy.GranaryCapacityBonus, LoamPhases.EffectiveCapacity(withGranary));
        Assert.Equal(LoamPolicy.LoamCapacity, LoamPhases.EffectiveCapacity(without));
    }

    [Fact]
    public void A_granary_still_under_construction_does_not_raise_capacity_yet()
    {
        var sector = Sector(0, Rootbed(), Granary(constructionTurnsRemaining: 2));
        Assert.Equal(LoamPolicy.LoamCapacity, LoamPhases.EffectiveCapacity(sector));
    }

    [Fact]
    public void A_sector_with_a_granary_accepts_more_stock_before_overflowing()
    {
        var world = new WorldState
        {
            Sectors = new[] { Sector(LoamPolicy.LoamCapacity - 10, Rootbed(), Granary()) }
        };
        var report = new TurnReport();

        var result = LoamPhases.Production(world, report, "production");

        var stock = result.Sectors[0].LoamStock;
        Assert.True(stock > LoamPolicy.LoamCapacity, $"granary-equipped stock {stock} did not exceed the base cap");
        Assert.DoesNotContain(report.Entries, e => e.Detail.StartsWith("loam.overflow"));
    }

    [Fact]
    public void Overflow_above_the_raised_cap_is_still_lost_and_still_reported()
    {
        var world = new WorldState
        {
            Sectors = new[] { Sector(LoamPolicy.LoamCapacity + LoamPolicy.GranaryCapacityBonus - 5, Rootbed(), Granary()) }
        };
        var report = new TurnReport();

        var result = LoamPhases.Production(world, report, "production");

        Assert.Equal(LoamPolicy.LoamCapacity + LoamPolicy.GranaryCapacityBonus, result.Sectors[0].LoamStock);
        Assert.Contains(report.Entries, e => e.Detail.StartsWith("loam.overflow") && e.SectorId == "s");
    }

    [Fact]
    public void A_rootbed_with_no_granary_overflows_exactly_as_it_does_today()
    {
        var world = new WorldState
        {
            Sectors = new[] { Sector(LoamPolicy.LoamCapacity - 5, Rootbed()) }
        };
        var report = new TurnReport();

        var result = LoamPhases.Production(world, report, "production");

        Assert.Equal(LoamPolicy.LoamCapacity, result.Sectors[0].LoamStock);
        Assert.Contains(report.Entries, e => e.Detail.StartsWith("loam.overflow"));
    }
}
