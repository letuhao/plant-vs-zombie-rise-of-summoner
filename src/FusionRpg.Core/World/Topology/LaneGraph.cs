using FusionRpg.Core.World.Movement;

namespace FusionRpg.Core.World.Topology;

/// <summary>One traversable step between two sectors, and what it costs to walk it.</summary>
public readonly record struct LaneStepEdge(string FromSectorId, string ToSectorId, int Cost);

/// <summary>
/// The lane network as a plain graph, scoped to a set of sectors (spec-world-topology.md).
///
/// The scoping is what makes one implementation answer three questions: pass nothing for the map's
/// own topology, a faction's holdings for that empire's internal connectivity, or those holdings
/// minus a hostile-held sector for what a zone of control actually costs.
///
/// **This is the supply lens, not the march lens.** An edge exists where a lane could hold an empire
/// together, so a deep rift and a temporal current — which carry no supply — are absent even though an
/// army can walk down both. March distance is a different question with a different answer, and
/// `ai-commander`'s `ReachMap` asks it separately over <see cref="LaneCost"/>.
///
/// Everything is built in ordinal id order. Tarjan and Floyd–Warshall both depend on traversal
/// order — two orders give different-but-equally-valid answers, and a replay cannot survive that.
/// </summary>
public sealed class LaneGraph
{
    readonly Dictionary<string, int> _index;

    LaneGraph(IReadOnlyList<string> sectors, IReadOnlyList<LaneStepEdge> edges, Dictionary<string, int> index)
    {
        Sectors = sectors;
        Edges = edges;
        _index = index;
    }

    /// <summary>In ordinal id order.</summary>
    public IReadOnlyList<string> Sectors { get; }

    /// <summary>In the order their lanes appear, which is ordinal by lane id.</summary>
    public IReadOnlyList<LaneStepEdge> Edges { get; }

    public int Count => Sectors.Count;

    public bool Contains(string sectorId) => _index.ContainsKey(sectorId);

    public int IndexOf(string sectorId) => _index.TryGetValue(sectorId, out var i)
        ? i
        : throw new KeyNotFoundException($"Sector '{sectorId}' is outside this graph's filter.");

    /// <param name="include">Null means every sector; otherwise only these.</param>
    public static LaneGraph Build(WorldState world, IReadOnlySet<string>? include = null)
    {
        var sectors = world.Sectors
            .Where(s => include is null || include.Contains(s.SectorId))
            .Select(s => s.SectorId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < sectors.Count; i++) index[sectors[i]] = i;

        var edges = new List<LaneStepEdge>();
        foreach (var lane in world.Lanes.OrderBy(l => l.LaneId, StringComparer.Ordinal))
        {
            if (!IsTraversable(lane)) continue;
            if (!index.ContainsKey(lane.FromSectorId) || !index.ContainsKey(lane.ToSectorId)) continue;

            // Null banner on purpose: topology is about the ground, not about who happens to be
            // walking it. A ley discount belongs to a particular legion's march, not to the map.
            var cost = LaneCost.For(world, lane, null);

            edges.Add(new LaneStepEdge(lane.FromSectorId, lane.ToSectorId, cost));

            // Directed lanes are walked one way only. No shipped lane type is both one-way and
            // supply-carrying, so this branch does not fire today — it is here because silently
            // walking a directed supply lane backwards would be a very quiet bug.
            if (!LaneTypeCatalog.Get(lane.TypeId).OneWay)
                edges.Add(new LaneStepEdge(lane.ToSectorId, lane.FromSectorId, cost));
        }

        return new LaneGraph(sectors, edges, index);
    }

    static bool IsTraversable(WorldLane lane)
    {
        if (lane.State != LaneState.Open) return false;

        var type = LaneTypeCatalog.Get(lane.TypeId);
        if (!type.CarriesSupply) return false;
        return !(type.Gated && lane.GateKeyId != null);
    }
}
