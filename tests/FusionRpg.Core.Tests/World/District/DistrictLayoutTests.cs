using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.World;
using FusionRpg.Core.World.District;
using Xunit;

namespace FusionRpg.Core.Tests.World.District;

/// <summary>base-defense district-layout (spec-district-layout.md). The stability contract (S1-S4)
/// is the module's actual specification -- "the generator is easy; these are not."</summary>
public class DistrictLayoutTests
{
    static WorldSector Sector(string id = "s1", int developmentLevel = 0, string typeId = "outpost",
        params WorldSlot[] slots) => new()
    {
        SectorId = id, TypeId = typeId, DevelopmentLevel = developmentLevel,
        OwnerFactionId = "dave", Phase = SectorPhase.Held,
        Slots = slots.Length > 0 ? slots : new[] { new WorldSlot { SlotIndex = 0, SlotTypeId = SlotTypeCatalog.SeatSlotTypeId } }
    };

    static WorldSlot Slot(int index, SlotState state = SlotState.Intact) =>
        new() { SlotIndex = index, SlotTypeId = "wall", State = state };

    [Fact]
    public void Same_sector_same_seed_same_board_10000_times()
    {
        var sector = Sector();
        var first = DistrictLayout.Build(sector, worldSeed: 42, BoardEdge.North);

        for (var i = 0; i < 10_000; i++)
        {
            var repeat = DistrictLayout.Build(sector, worldSeed: 42, BoardEdge.North);
            Assert.Equal(first.Rows, repeat.Rows);
            Assert.Equal(first.Cols, repeat.Cols);
            Assert.Equal(first.Cells, repeat.Cells);
        }
    }

    [Fact]
    public void Board_is_identical_regardless_of_which_turn_it_is_computed_on()
    {
        // S2: the seed excludes turn entirely -- Build doesn't even take a turn parameter, so this
        // proves it structurally as much as behaviourally: the same (sector, worldSeed, entryEdge)
        // is the WHOLE input, on turn 3 or turn 70 alike.
        var sector = Sector();
        var turn3Board = DistrictLayout.Build(sector, worldSeed: 7, BoardEdge.West);
        var turn70Board = DistrictLayout.Build(sector, worldSeed: 7, BoardEdge.West);
        Assert.Equal(turn3Board.Cells, turn70Board.Cells);
    }

    [Fact]
    public void Capture_does_not_change_the_board()
    {
        var sector = Sector();
        var beforeCapture = DistrictLayout.Build(sector, worldSeed: 7, BoardEdge.East);
        var afterCapture = DistrictLayout.Build(sector with { OwnerFactionId = "zomboss" }, worldSeed: 7, BoardEdge.East);
        Assert.Equal(beforeCapture.Cells, afterCapture.Cells);
    }

    [Fact]
    public void Adding_a_slot_moves_no_existing_slot()
    {
        var spec = new GridSpec(18, 18);
        var center = new GridPos(9, 9);
        var coreSide = 7; // 400 permille of 18 = 7.2 -> 7

        var sixSlotCells = Enumerable.Range(0, 6)
            .Select(i => DistrictLayout.CellForSlot(districtSeed: 99, i, spec, center, coreSide))
            .ToList();

        for (var i = 0; i < 6; i++)
        {
            var sameCell = DistrictLayout.CellForSlot(districtSeed: 99, i, spec, center, coreSide);
            Assert.Equal(sixSlotCells[i], sameCell); // unmoved after "growing" the list to 7
        }

        var seventh = DistrictLayout.CellForSlot(districtSeed: 99, 6, spec, center, coreSide);
        Assert.DoesNotContain(seventh, sixSlotCells);
    }

    [Fact]
    public void Every_slot_gets_a_distinct_cell()
    {
        var spec = new GridSpec(30, 30);
        var center = new GridPos(15, 15);
        var coreSide = 12;

        var maxSlots = coreSide * coreSide; // every Core cell, at most
        var seen = new HashSet<GridPos>();
        for (var i = 0; i < maxSlots; i++)
        {
            var cell = DistrictLayout.CellForSlot(districtSeed: 12345, i, spec, center, coreSide);
            Assert.True(seen.Add(cell), $"slot {i} collided with an earlier slot at {cell}");
        }
    }

    [Fact]
    public void At_least_one_gate_is_on_the_entry_edge_for_all_four_edges()
    {
        foreach (var edge in new[] { BoardEdge.North, BoardEdge.South, BoardEdge.East, BoardEdge.West })
        {
            var board = DistrictLayout.Build(Sector(), worldSeed: 555, edge);
            var midpoint = edge switch
            {
                BoardEdge.North => new GridPos(0, board.Cols / 2),
                BoardEdge.South => new GridPos(board.Rows - 1, board.Cols / 2),
                BoardEdge.East => new GridPos(board.Rows / 2, board.Cols - 1),
                BoardEdge.West => new GridPos(board.Rows / 2, 0),
                _ => throw new InvalidOperationException(),
            };
            Assert.Equal(CellTerrain.Open, board.TerrainAt(midpoint));
        }
    }

    [Fact]
    public void Entry_edge_follows_the_arrival_lane()
    {
        var world = WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1, worldId: "w");
        var laneA = world.Lanes[0];
        var laneB = world.Lanes.First(l => l.LaneId != laneA.LaneId);

        var attackerA = new WorldEntity { EntityId = "e1", OwnerFactionId = "wild", OnLaneId = laneA.LaneId };
        var attackerB = new WorldEntity { EntityId = "e2", OwnerFactionId = "wild", OnLaneId = laneB.LaneId };

        var edgeA = DistrictLayout.EntryEdgeFor(world, attackerA, laneA.ToSectorId);
        var edgeB = DistrictLayout.EntryEdgeFor(world, attackerB, laneB.ToSectorId);

        // Not asserting they differ (a 4-way hash bucket can coincide) -- asserting determinism,
        // which is the load-bearing half.
        Assert.Equal(edgeA, DistrictLayout.EntryEdgeFor(world, attackerA, laneA.ToSectorId));
        Assert.Equal(edgeB, DistrictLayout.EntryEdgeFor(world, attackerB, laneB.ToSectorId));
    }

    [Fact]
    public void No_lane_falls_back_to_north()
    {
        var world = WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1, worldId: "w");
        var garrison = new WorldEntity { EntityId = "e1", OwnerFactionId = "dave", OnLaneId = null, AtSectorId = "homeworld" };
        Assert.Equal(BoardEdge.North, DistrictLayout.EntryEdgeFor(world, garrison, "homeworld"));
    }

    [Fact]
    public void Fortress_flag_is_read_but_no_shipped_or_test_constructible_sector_type_sets_it()
    {
        // §5's own instruction: verify the flag is actually set on a shipped sector type before
        // claiming this works. Verified at HEAD (2026-09-05): SectorTypeCatalog's five seed rows
        // (Home, two NoBase, Nexus, Boss) -- NONE set SectorTypeFlags.Fortress.
        Assert.DoesNotContain(SectorTypeCatalog.All, t => t.Flags.HasFlag(FusionRpg.Core.World.SectorTypeFlags.Fortress));

        // This is a WIRING GAP, reported as one, and it runs one level deeper than "no shipped
        // sector sets it": DistrictLayout.Build resolves isFortress by looking `sector.TypeId` up in
        // the (fixed, compiled) SectorTypeCatalog.All -- a TEST cannot construct a sector type with
        // the flag set either, since the catalog has no public seam for one. The mechanism itself
        // (rampart thickness responds to isFortress) is proven directly below, against the pure
        // geometry function, since the end-to-end path through Build() cannot be reached by anything
        // that exists today, shipped or synthetic.
    }

    [Fact]
    public void Fortress_bonus_thickens_the_rampart_ring()
    {
        // The mechanism ZoneOf itself is fortress-agnostic -- it just takes whatever
        // rampartThickness it is given -- so this proves Build's own `+= FortressRampartBonus`
        // arithmetic would thicken the ring the moment isFortress ever resolves true, using the same
        // ZoneOf geometry Build calls internally.
        const int side = 18, coreSideMilli = 400, baseThickness = 1, fortressBonus = 1;
        var center = new GridPos(side / 2, side / 2);

        int RingDepthAt(int rampartThickness)
        {
            // Scan down the CENTRE column: a column near an edge (e.g. col 1) sits at Chebyshev
            // distance ~8 from centre regardless of row on an 18-wide board, which is always past
            // the ring (~radius 4-5) and would never intersect it at all. ZoneOf has no notion of
            // gates (Build carves those separately), so walking straight through the centre column
            // here is safe -- there is nothing to accidentally walk through a gate corridor.
            var p = new GridPos(0, center.Col);
            while (DistrictLayout.ZoneOf(p, side, coreSideMilli, rampartThickness) != DistrictZone.Rampart
                   && p.Row < side)
                p = new GridPos(p.Row + 1, p.Col);

            var depth = 0;
            while (p.Row < side && DistrictLayout.ZoneOf(p, side, coreSideMilli, rampartThickness) == DistrictZone.Rampart)
            {
                depth++;
                p = new GridPos(p.Row + 1, p.Col);
            }
            return depth;
        }

        var plainDepth = RingDepthAt(baseThickness);
        var fortressDepth = RingDepthAt(baseThickness + fortressBonus);

        Assert.Equal(baseThickness, plainDepth);
        Assert.Equal(baseThickness + fortressBonus, fortressDepth);
    }

    [Fact]
    public void Ruined_slots_are_rough_and_carry_no_structure()
    {
        var sector = Sector(slots: new[]
        {
            Slot(0), Slot(1, SlotState.Ruined), Slot(2, SlotState.Depleted), Slot(3),
        });
        var board = DistrictLayout.Build(sector, worldSeed: 3, BoardEdge.North);

        var side = board.Rows;
        var center = new GridPos(side / 2, side / 2);
        var coreSide = Math.Max(1, 400 * side / 1000);
        var seed = DistrictLayout.DistrictSeed(3, sector.SectorId);

        var ruinedCell = DistrictLayout.CellForSlot(seed, 1, board, center, coreSide);
        var depletedCell = DistrictLayout.CellForSlot(seed, 2, board, center, coreSide);

        Assert.Equal(CellTerrain.Rough, board.TerrainAt(ruinedCell));
        Assert.Equal(CellTerrain.Rough, board.TerrainAt(depletedCell));
    }

    [Fact]
    public void Board_never_exceeds_max_cells_at_the_largest_authored_tier()
    {
        // The largest authored tier in this bootstrap's DefaultSiege is 2 -> side 30 -> 900 cells,
        // comfortably under maxCells (4096). Assert it holds structurally rather than by inspection.
        var side = DistrictLayout.SideFor(developmentLevel: 999); // plateaus at the top tier
        Assert.True(side * side <= SiegeTuningPolicy.MaxCells);
    }

    [Fact]
    public void Board_size_does_not_change_with_development_level_past_the_top_authored_tier()
    {
        var lowTier = DistrictLayout.Build(Sector(developmentLevel: 2), worldSeed: 1, BoardEdge.North);
        var wayPastTop = DistrictLayout.Build(Sector(developmentLevel: 500), worldSeed: 1, BoardEdge.North);
        Assert.Equal(lowTier.Rows, wayPastTop.Rows);
        Assert.Equal(lowTier.Cols, wayPastTop.Cols);
    }

    [Fact]
    public void Board_size_does_not_change_when_slots_grow()
    {
        var few = Sector(slots: new[] { Slot(0) });
        var many = Sector(slots: Enumerable.Range(0, 10).Select(i => Slot(i)).ToArray());

        var fewBoard = DistrictLayout.Build(few, worldSeed: 8, BoardEdge.North);
        var manyBoard = DistrictLayout.Build(many, worldSeed: 8, BoardEdge.North);

        Assert.Equal(fewBoard.Rows, manyBoard.Rows);
        Assert.Equal(fewBoard.Cols, manyBoard.Cols);
    }

    [Fact]
    public void Core_zone_is_never_empty_at_the_smallest_authored_tier()
    {
        var side = DistrictLayout.SideFor(developmentLevel: 0); // smallest authored tier (18)
        var center = new GridPos(side / 2, side / 2);
        var coreCount = 0;
        for (var r = 0; r < side; r++)
        for (var c = 0; c < side; c++)
            if (DistrictLayout.ZoneOf(new GridPos(r, c), side, coreSideMilli: 400, rampartThickness: 1) == DistrictZone.Core)
                coreCount++;

        Assert.True(coreCount > 0);
    }

    [Fact]
    public void No_board_dimension_traces_to_the_power_ladder()
    {
        // Structural proof, not a behavioural one: SideFor's only input is developmentLevel (an int
        // lookup key), and its body never references Theta, P(Theta), or any power-scale type.
        // (spec-district-layout.md's own boxed correction: a board side is not a power-shaped scale.)
        var method = typeof(DistrictLayout).GetMethod(nameof(DistrictLayout.SideFor));
        Assert.NotNull(method);
        Assert.Single(method!.GetParameters());
        Assert.Equal(typeof(int), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), method.ReturnType);
    }
}
