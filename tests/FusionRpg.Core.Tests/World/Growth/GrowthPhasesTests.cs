using FusionRpg.Core.World;
using FusionRpg.Core.World.Growth;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World.Growth;

/// <summary>
/// world-map W50 acceptance: with the real, shipped pulse (0, identity until W58) every existing
/// golden is byte-identical and the locked-phase-order test is untouched; with a non-zero pulse
/// supplied locally (never the shared `RecruitPolicy` singleton), stock accrues on week boundaries
/// only, in stable sector-id order, and the report gains one structural entry per accruing sector.
/// </summary>
public class GrowthPhasesTests
{
    const string Phase = "Test";
    const long SeatPulse = 100;
    const int LairMultiplier = 1000; // identity — isolates the seat pulse alone unless a test says otherwise
    const int SpecialWeekMultiplier = 1000;

    static WorldSlot Seat(string? owner = null) =>
        new() { SlotIndex = 0, SlotTypeId = SlotTypeCatalog.SeatSlotTypeId, OwnerFactionId = owner };

    static WorldSlot Lair(GuardState guardState) =>
        new() { SlotIndex = 1, SlotTypeId = "lair", GuardState = guardState };

    static WorldSector Sector(string id, string? owner, IReadOnlyList<WorldSlot> slots, long recruitStock = 0) =>
        new() { SectorId = id, TypeId = "stable", OwnerFactionId = owner, Slots = slots, RecruitStock = recruitStock };

    static WorldState World(int currentTurn, params WorldSector[] sectors) => new()
    {
        WorldId = "w", TemplateId = "test", Seed = 1, CurrentTurn = currentTurn,
        Factions = new[] { new WorldFaction { FactionId = "f1", Kind = WorldFactionKind.Player, Name = "F1" } },
        Sectors = sectors.OrderBy(s => s.SectorId, StringComparer.Ordinal).ToList()
    };

    static WorldState RunGrowth(WorldState world, TurnReport report, int turn, ulong seed = 1,
        long seatPulse = SeatPulse, int lairMultiplier = LairMultiplier, int specialWeekMultiplier = SpecialWeekMultiplier) =>
        GrowthPhases.Growth(world, report, Phase, turn, seed, seatPulse, lairMultiplier, specialWeekMultiplier);

    [Fact]
    public void At_the_real_shipped_identity_pulse_nothing_accrues_and_nothing_reports()
    {
        var world = World(currentTurn: 6, Sector("s1", "f1", new[] { Seat("f1") }));
        var report = new TurnReport();

        // The real production caller reads RecruitPolicy.SeatPulsePerWeek, which ships at 0.
        var result = GrowthPhases.Growth(world, report, Phase, turn: 7, seed: 1,
            seatPulsePerWeek: RecruitPolicy.SeatPulsePerWeek,
            lairMultiplierMilli: RecruitPolicy.LairMultiplierMilli,
            specialWeekMultiplierMilli: RecruitPolicy.SpecialWeekMultiplierMilli);

        Assert.Equal(0, result.Sectors.Single().RecruitStock);
        Assert.Empty(report.Entries);
    }

    [Fact]
    public void A_held_seat_accrues_the_pulse_on_a_week_boundary_turn_and_only_then()
    {
        var world = World(currentTurn: 6, Sector("s1", "f1", new[] { Seat("f1") }));

        var onBoundary = RunGrowth(world, new TurnReport(), turn: 7); // 7 % DaysPerWeek(7) == 0
        Assert.Equal(SeatPulse, onBoundary.Sectors.Single().RecruitStock);

        var offBoundary = RunGrowth(world, new TurnReport(), turn: 8);
        Assert.Equal(0, offBoundary.Sectors.Single().RecruitStock);
    }

    [Fact]
    public void A_sector_with_no_seat_accrues_nothing_regardless_of_its_lair()
    {
        var world = World(currentTurn: 6, Sector("s1", "f1", new[] { Lair(GuardState.Cleared) }));
        var result = RunGrowth(world, new TurnReport(), turn: 7);

        Assert.Equal(0, result.Sectors.Single().RecruitStock);
    }

    [Fact]
    public void An_unowned_sector_accrues_nothing_even_with_a_seat_slot()
    {
        var world = World(currentTurn: 6, Sector("s1", null, new[] { Seat() }));
        var result = RunGrowth(world, new TurnReport(), turn: 7);

        Assert.Equal(0, result.Sectors.Single().RecruitStock);
    }

    [Fact]
    public void A_cleared_lair_multiplies_its_own_sectors_pulse_and_an_intact_one_does_not()
    {
        var withCleared = World(currentTurn: 6, Sector("s1", "f1", new[] { Seat("f1"), Lair(GuardState.Cleared) }));
        var withIntact = World(currentTurn: 6, Sector("s1", "f1", new[] { Seat("f1"), Lair(GuardState.Intact) }));

        var cleared = RunGrowth(withCleared, new TurnReport(), turn: 7, lairMultiplier: 2000);
        var intact = RunGrowth(withIntact, new TurnReport(), turn: 7, lairMultiplier: 2000);

        Assert.Equal(SeatPulse * 2, cleared.Sectors.Single().RecruitStock);
        Assert.Equal(SeatPulse, intact.Sectors.Single().RecruitStock);
    }

    [Fact]
    public void Stock_accrues_onto_whatever_was_already_there_rather_than_overwriting_it()
    {
        var world = World(currentTurn: 6, Sector("s1", "f1", new[] { Seat("f1") }, recruitStock: 50));
        var result = RunGrowth(world, new TurnReport(), turn: 7);

        Assert.Equal(50 + SeatPulse, result.Sectors.Single().RecruitStock);
    }

    [Fact]
    public void An_accruing_sector_gets_one_structural_report_entry_naming_it_by_SectorId()
    {
        var world = World(currentTurn: 6, Sector("s1", "f1", new[] { Seat("f1") }));
        var report = new TurnReport();

        RunGrowth(world, report, turn: 7);

        var entry = Assert.Single(report.Entries);
        Assert.Equal("s1", entry.SectorId);
        Assert.Equal(TurnReportKinds.Event, entry.Kind);
        Assert.StartsWith("growth.pulse:", entry.Detail);
    }

    [Fact]
    public void Every_held_seat_accrues_independently_in_stable_sector_id_order()
    {
        var world = World(currentTurn: 6,
            Sector("s2", "f1", new[] { Seat("f1") }),
            Sector("s1", "f1", new[] { Seat("f1") }));
        var report = new TurnReport();

        var result = RunGrowth(world, report, turn: 7);

        Assert.Equal(new[] { "s1", "s2" }, result.Sectors.Select(s => s.SectorId));
        Assert.All(result.Sectors, s => Assert.Equal(SeatPulse, s.RecruitStock));
        Assert.Equal(new[] { "s1", "s2" }, report.Entries.Select(e => e.SectorId));
    }
}
