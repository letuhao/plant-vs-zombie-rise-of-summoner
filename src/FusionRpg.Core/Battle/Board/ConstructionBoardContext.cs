using FusionRpg.Core.Actions;
using FusionRpg.Core.World;

namespace FusionRpg.Core.Battle.Board;

/// <summary>
/// base-defense `siege-construction` (decision 27, 2026-09-06): one battle's worth of everything
/// <c>structure.place</c>'s executor needs about the live siege board, built ONCE by whoever assembles
/// the board (<c>DistrictAssaultResolver</c>) and threaded into <see cref="BattleEffectHost"/> —
/// closing <see cref="ConstructionPlacement"/>'s own named gap ("the tactical board has no mapping
/// from a <see cref="GridPos"/> cell to a world-layer <c>SlotKind</c> today — that data flow is
/// `siege-resolver`'s job once it assembles a real board") for the one caller that finally has the
/// world-side data (<c>WorldSector.Slots</c>) to build it.
/// </summary>
public sealed class ConstructionBoardContext
{
    public BoardState Board { get; }
    public int BoardSide { get; }
    public int CoreSideMilli { get; }
    public int RampartThickness { get; }

    /// <summary>Board cell → the world slot that sits there, keyed by <see cref="DistrictLayout.CellForSlot"/>'s
    /// own deterministic mapping — the SAME cell <see cref="DistrictAssaultResolver"/>'s own
    /// `PlaceStructures` already places an existing structure at. Every legal construction target is one
    /// of these cells; there is no "arbitrary open cell" placement (a `WorldSlot` is the only thing the
    /// world layer can persist a structure onto).</summary>
    public IReadOnlyDictionary<GridPos, (int SlotIndex, SlotKind Kind)> SlotByCell { get; }

    /// <summary>What `structure.place` actually did this battle, in firing order — read back by
    /// <see cref="DistrictAssaultResolver"/> once <c>BattleEngine.Resolve</c> returns, to build the
    /// <see cref="World.Turn.SlotOutcome.StructurePlaced"/> entries that cross the seam. A plain list
    /// the sink appends to, the same shape <c>BattleEffectHost.LastApplied</c> already uses for FA10
    /// deltas.</summary>
    public List<StructurePlacementRecord> Placed { get; } = new();

    public ConstructionBoardContext(
        BoardState board, int boardSide, int coreSideMilli, int rampartThickness,
        IReadOnlyDictionary<GridPos, (int SlotIndex, SlotKind Kind)> slotByCell)
    {
        Board = board ?? throw new ArgumentNullException(nameof(board));
        BoardSide = boardSide;
        CoreSideMilli = coreSideMilli;
        RampartThickness = rampartThickness;
        SlotByCell = slotByCell ?? throw new ArgumentNullException(nameof(slotByCell));
    }
}

/// <summary>One successful `structure.place` firing this battle.</summary>
public sealed record StructurePlacementRecord(int SlotIndex, string StructureId, bool Instant);
