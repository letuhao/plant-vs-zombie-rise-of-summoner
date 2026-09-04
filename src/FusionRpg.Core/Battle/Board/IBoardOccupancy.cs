using FusionRpg.Core.Actions;

namespace FusionRpg.Core.Battle.Board;

/// <summary>
/// What the pathfinder treats as blocked (spec-siege-pathing.md §3). A parameter, not a fact of the
/// board — a unit standing in a doorway blocks it now and will not next round, so the pathfinder
/// takes a view rather than reading <see cref="BoardState"/> directly.
/// </summary>
public interface IBoardOccupancy
{
    bool IsBlocked(GridPos p);
}

/// <summary>Terrain plus every occupant. Actual movement — you cannot walk through anyone.</summary>
public sealed class SolidOccupancy : IBoardOccupancy
{
    readonly GridSpec _spec;
    readonly BoardState _board;

    public SolidOccupancy(GridSpec spec, BoardState board)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        _board = board ?? throw new ArgumentNullException(nameof(board));
    }

    public bool IsBlocked(GridPos p) =>
        !_spec.Contains(p)
        || _spec.TerrainAt(p) is CellTerrain.Blocking or CellTerrain.Gap
        || _board.OccupantAt(p) is not null;
}

/// <summary>
/// Terrain only, no occupants. `siege-ai`'s planning question — "can I ever get there" vs "can I get
/// there this instant." Without this split, an AI surrounded by its own allies concludes the goal is
/// unreachable and stands still — the single most visible AI failure in any tactical game.
/// </summary>
public sealed class TerrainOnlyOccupancy : IBoardOccupancy
{
    readonly GridSpec _spec;

    public TerrainOnlyOccupancy(GridSpec spec) => _spec = spec ?? throw new ArgumentNullException(nameof(spec));

    public bool IsBlocked(GridPos p) =>
        !_spec.Contains(p) || _spec.TerrainAt(p) is CellTerrain.Blocking or CellTerrain.Gap;
}
