using FusionRpg.Core.World;
using FusionRpg.Core.World.Loam;
using Xunit;
namespace FusionRpg.Core.Tests.World.Loam;
public class DiagTempTests
{
    static WorldSlot Rootbed(int index) => new() { SlotIndex = index, SlotTypeId = SlotTypeCatalog.RootbedSlotTypeId };
    static WorldSector Sector(string id, long stock = 0, int stability = 1000, int development = 0, int danger = 0, IReadOnlyList<WorldSlot>? slots = null) =>
        new()
        {
            SectorId = id, TypeId = "stable", OwnerFactionId = "f1", LoamStock = stock, StabilityMilli = stability,
            DevelopmentLevel = development, DangerBand = danger, Slots = slots ?? Array.Empty<WorldSlot>()
        };
    static WorldSector Elsewhere() => Sector("elsewhere", slots: new[] { Rootbed(0) });
    static WorldState World(IReadOnlyList<WorldSector> sectors, IReadOnlyList<WorldLane>? lanes = null) => new()
    {
        WorldId = "w", TemplateId = "t", Seed = 1,
        Factions = new[] { new WorldFaction { FactionId = "f1", Kind = WorldFactionKind.Player, Name = "F1" } },
        Sectors = sectors,
        Lanes = lanes ?? Array.Empty<WorldLane>()
    };
    static IReadOnlyList<string> ComponentOf(WorldState world, string sectorId) =>
        TerritoryComponents.For(world, "f1").Single(c => c.Contains(sectorId));

    [Fact]
    public void Diag3()
    {
        var mild = Sector("mild", stock: 0, stability: 50, development: 1, danger: 0);
        var harsh = Sector("harsh", stock: 0, stability: 50, development: 5, danger: 4);
        var world = World(
            new[] { mild, harsh, Elsewhere() },
            new[] { new WorldLane { LaneId = "l", FromSectorId = "mild", ToSectorId = "harsh", TypeId = LaneTypeCatalog.RiftLaneTypeId } });

        var component = ComponentOf(world, "mild");
        var upkeep = component.Sum(id => LoamUpkeep.For(world, world.Sectors.First(s => s.SectorId == id)));
        var projected = LoamForecast.ProjectedStock(component, world);
        var weakest = LoamForecast.Weakest(world, component, projected, upkeep);
        var release = LoamForecast.WillRelease(world, component);

        Assert.Fail($"component=[{string.Join(",", component)}] upkeep={upkeep} projected={projected} weakest={weakest} release={release}");
    }
}
