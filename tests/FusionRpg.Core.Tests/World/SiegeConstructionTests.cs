using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Siege;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// base-defense `siege-construction` (spec-siege-construction.md): the `rubble`/`ironwork` two-stock
/// economy, the lossy/gated refine chain (decision 28), and the one placement validator shared by all
/// four acquisition paths (§6) — scoped to the pure stock arithmetic and the board-level gate; the
/// `WorldCommandKinds.Assault` order-kind plumbing and the action-system wiring for
/// `Assembled`/`Summoned`/`Laboured` are named as deferred scope in `SiegeConstruction.cs`'s own doc
/// comment, not built here.
/// </summary>
public class SiegeConstructionTests
{
    static WorldState World(params WorldSector[] sectors) => new() { TemplateId = "t", Sectors = sectors };

    [Fact]
    public void World_goldens_are_byte_identical_at_zero_stock()
    {
        var withDefaults = World(new WorldSector { SectorId = "s1" });
        var withoutFields = World(new WorldSector { SectorId = "s1" });

        Assert.Equal(WorldCanonical.Write(withoutFields), WorldCanonical.Write(withDefaults));
        Assert.DoesNotContain("sector-rubble", WorldCanonical.Write(withDefaults), StringComparison.Ordinal);
        Assert.DoesNotContain("sector-ironwork", WorldCanonical.Write(withDefaults), StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_gains_exactly_one_row_per_nonzero_stock()
    {
        var world = World(new WorldSector { SectorId = "s1", RubbleStock = 40, IronworkStock = 0 });
        var text = WorldCanonical.Write(world);

        Assert.Contains("sector-rubble\ts1\t40", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sector-ironwork", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_stocks_round_trip_as_long_fields()
    {
        var sector = new WorldSector { SectorId = "s1", RubbleStock = 9_000_000_000L, IronworkStock = 1_234_567L };
        Assert.Equal(9_000_000_000L, sector.RubbleStock);
        Assert.Equal(1_234_567L, sector.IronworkStock);
    }

    [Fact]
    public void Refining_is_lossy()
    {
        // Decision 28: 4 rubble does not become 4 ironwork at any authored yield below 1000‰.
        var ironwork = SiegeConstruction.Refine(rubbleSpent: 4, yieldMilli: 600);
        Assert.True(ironwork < 4);
        Assert.Equal(2, ironwork); // checked(4 * 600 / 1000) = 2
    }

    [Fact]
    public void Refine_divides_by_1000_last_and_is_checked()
    {
        Assert.Equal(0, SiegeConstruction.Refine(rubbleSpent: 1, yieldMilli: 600)); // 600/1000 truncates to 0
        Assert.Throws<OverflowException>(() => SiegeConstruction.Refine(long.MaxValue, 600));
    }

    [Fact]
    public void Refine_rejects_negative_inputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SiegeConstruction.Refine(-1, 600));
        Assert.Throws<ArgumentOutOfRangeException>(() => SiegeConstruction.Refine(4, -1));
    }

    [Fact]
    public void Refining_is_gated_by_a_refinery_structure_not_a_cooldown()
    {
        Assert.Equal(0, SiegeConstruction.RefineGated(hasWorkingRefinery: false, rubbleSpent: 100, yieldMilli: 600));
        Assert.Equal(60, SiegeConstruction.RefineGated(hasWorkingRefinery: true, rubbleSpent: 100, yieldMilli: 600));
    }

    [Fact]
    public void Refinery_joins_LoamSource_and_Storage_as_a_real_structure_kind()
    {
        var def = new StructureDef
        {
            StructureId = "test-refinery", Name = "Test Refinery", Kind = StructureKind.Refinery,
            RequiredSlotKind = SlotKind.Wildland, AcquisitionPaths = new[] { AcquisitionPath.Built }
        };
        Assert.Equal(StructureKind.Refinery, def.Kind);
        // Validate does not throw for the new kind -- it carries no kind-specific validation rule.
        StructureCatalog.Validate(new[] { def });
    }

    // -- ConstructionPlacement: the one validator shared by all four acquisition paths (§6) --

    static (BoardState board, GridSpec spec) Board(int rows, int cols)
    {
        var spec = new GridSpec(rows, cols);
        return (new BoardState(spec), spec);
    }

    // With side=20, coreSideMilli=400: coreSideCells=max(1,400*20/1000)=8, coreHalfCeil=(8+1)/2=4, so
    // every cell within Chebyshev distance 3 of (10,10) is Core. (10,15)/(10,16) sit at distance 5/6 —
    // safely in Approach, and adjacent to each other — the fixture used by every non-Core test below.
    static readonly GridPos NonCoreBuilder = new(10, 15);
    static readonly GridPos NonCoreTarget = new(10, 16);

    [Fact]
    public void Adjacent_open_unoccupied_non_core_cell_is_placeable()
    {
        var (board, spec) = Board(20, 20);

        Assert.True(ConstructionPlacement.CanPlace(
            board, spec, NonCoreTarget, NonCoreBuilder,
            boardSide: 20, coreSideMilli: 400, rampartThickness: 1, requiredSlotKindSatisfied: true));
    }

    [Fact]
    public void Placement_requires_adjacency()
    {
        var (board, spec) = Board(20, 20);
        var farCell = new GridPos(10, 18); // Chebyshev distance 3 from NonCoreBuilder

        Assert.False(ConstructionPlacement.CanPlace(
            board, spec, farCell, NonCoreBuilder,
            boardSide: 20, coreSideMilli: 400, rampartThickness: 1, requiredSlotKindSatisfied: true));
    }

    [Fact]
    public void Occupied_cell_is_rejected()
    {
        var (board, spec) = Board(20, 20);
        board.Place("other-actor", NonCoreTarget);

        Assert.False(ConstructionPlacement.CanPlace(
            board, spec, NonCoreTarget, NonCoreBuilder,
            boardSide: 20, coreSideMilli: 400, rampartThickness: 1, requiredSlotKindSatisfied: true));
    }

    [Fact]
    public void Blocking_cell_is_rejected()
    {
        var cells = Enumerable.Repeat(CellTerrain.Open, 400).ToList();
        cells[10 * 20 + 16] = CellTerrain.Blocking;
        var spec = new GridSpec(20, 20, cells);
        var board = new BoardState(spec);

        Assert.False(ConstructionPlacement.CanPlace(
            board, spec, NonCoreTarget, NonCoreBuilder,
            boardSide: 20, coreSideMilli: 400, rampartThickness: 1, requiredSlotKindSatisfied: true));
    }

    [Fact]
    public void Nothing_can_be_built_in_the_core()
    {
        var (board, spec) = Board(20, 20);
        // DistrictLayout.ZoneOf centers the Core on (side/2, side/2) = (10, 10).
        var center = new GridPos(10, 10);
        var builder = new GridPos(10, 9);

        Assert.False(ConstructionPlacement.CanPlace(
            board, spec, center, builder,
            boardSide: 20, coreSideMilli: 400, rampartThickness: 1, requiredSlotKindSatisfied: true));
    }

    [Fact]
    public void Off_board_cell_is_rejected()
    {
        var (board, spec) = Board(5, 5);
        var builder = new GridPos(4, 4);
        var offBoard = new GridPos(5, 5);

        Assert.False(ConstructionPlacement.CanPlace(
            board, spec, offBoard, builder,
            boardSide: 5, coreSideMilli: 400, rampartThickness: 1, requiredSlotKindSatisfied: true));
    }

    [Fact]
    public void Required_slot_kind_mismatch_is_rejected()
    {
        var (board, spec) = Board(20, 20);

        Assert.False(ConstructionPlacement.CanPlace(
            board, spec, NonCoreTarget, NonCoreBuilder,
            boardSide: 20, coreSideMilli: 400, rampartThickness: 1, requiredSlotKindSatisfied: false));
    }

    [Fact]
    public void Either_side_may_build_anywhere_legal_because_the_gate_has_no_ownership_parameter()
    {
        // Decision 4: no ownership check anywhere in placement. Enforced structurally here — the same
        // board, same cell, same builder position produces the same verdict regardless of which side
        // is asking, because CanPlace's signature has no faction/owner parameter to differ on.
        var (boardA, specA) = Board(20, 20);
        var (boardB, specB) = Board(20, 20);

        var attackerVerdict = ConstructionPlacement.CanPlace(
            boardA, specA, NonCoreTarget, NonCoreBuilder, boardSide: 20, coreSideMilli: 400, rampartThickness: 1,
            requiredSlotKindSatisfied: true);
        var defenderVerdict = ConstructionPlacement.CanPlace(
            boardB, specB, NonCoreTarget, NonCoreBuilder, boardSide: 20, coreSideMilli: 400, rampartThickness: 1,
            requiredSlotKindSatisfied: true);

        Assert.True(attackerVerdict);
        Assert.Equal(attackerVerdict, defenderVerdict);
    }
}
