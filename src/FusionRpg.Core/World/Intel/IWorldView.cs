namespace FusionRpg.Core.World.Intel;

/// <summary>
/// The world as one faction knows it (spec-world-intel.md §On the wire).
///
/// After this exists, **nothing outside the engine reads `WorldState` directly** — not a policy, not
/// a projection. That is the whole guarantee: an AI cannot accidentally consult the truth if it has
/// no way to ask for it, and a leak becomes a compile error rather than a subtle advantage nobody
/// notices for six months.
///
/// Three things are public knowledge and always answered in full: which factions exist, the sector
/// ids, and the lanes between them. You can see that six places exist and how the roads join them;
/// you just do not know what is in them. Hiding the graph itself would make the map unreadable.
/// </summary>
public interface IWorldView
{
    string FactionId { get; }
    int CurrentTurn { get; }

    /// <summary>Public: who is playing.</summary>
    IReadOnlyList<WorldFaction> Factions { get; }

    /// <summary>Public: the shape of the map, in ordinal order.</summary>
    IReadOnlyList<string> SectorIds { get; }

    /// <summary>
    /// Public in shape, believed in state: where every road goes, how long and how wide, but a lane
    /// with neither end in sight always reads <see cref="LaneState.Open"/> whether it is or not.
    /// You find out a bridge is down by going to look at it.
    /// </summary>
    IReadOnlyList<WorldLane> Lanes { get; }

    /// <summary>Your own forces, in full. You know what you brought.</summary>
    IReadOnlyList<WorldEntity> OwnForces { get; }

    /// <summary>Null when this faction has never seen the sector — an absent snapshot *is* "unknown".</summary>
    IntelSnapshot? Believed(string sectorId);

    /// <summary>The derived ladder: <c>Watched</c>, <c>Scouted</c>, <c>Rumored</c> or <c>Unknown</c>.</summary>
    IntelState StateOf(string sectorId);

    /// <summary>How many turns stale the belief is; zero when it was seen this turn.</summary>
    int AgeOf(string sectorId);

    /// <summary>
    /// What this sector's <c>LoamStock</c> actually is, if this faction owns it — self-knowledge of
    /// your own resource, not fog (spec-loam-ai-survival.md). "Only the owner" (loam-model) still
    /// holds: this answers zero for anyone else's ground, and it is a live read of your own current
    /// holding rather than a remembered snapshot, because <see cref="IntelSnapshot"/> is deliberately
    /// never where stock lives — not even for the owner.
    /// </summary>
    long OwnLoamStock(string sectorId);
}

/// <summary>
/// The real implementation: answers from what the faction believes, never from what is true.
///
/// Visibility is recomputed here rather than stored on the snapshot, because "can I see it *now*"
/// changes the moment a legion moves, while "when did I last see it" does not.
/// </summary>
public sealed class BelievedWorldView : IWorldView
{
    readonly WorldState _world;
    readonly FactionIntel _intel;
    readonly IReadOnlyDictionary<string, SectorSight> _sight;

    public BelievedWorldView(WorldState world, string factionId)
    {
        _world = world;
        FactionId = factionId;
        _intel = world.Intel.FirstOrDefault(i => string.Equals(i.FactionId, factionId, StringComparison.Ordinal))
                 ?? new FactionIntel { FactionId = factionId };
        _sight = Visibility.SeenBy(world, factionId);

        SectorIds = world.Sectors.Select(s => s.SectorId).ToList();
        OwnForces = world.Entities
            .Where(e => string.Equals(e.OwnerFactionId, factionId, StringComparison.Ordinal))
            .ToList();

        // The road is drawn on the map — where it goes, how long, how wide. Whether it is still
        // passable is not: that is learned by looking. A lane with neither end in sight reads open,
        // so a faction routes over it confidently and finds out the hard way, which is what fog is
        // supposed to feel like. Handing back the truth here would be the quietest possible leak.
        Lanes = world.Lanes
            .Select(lane => SeesNow(lane.FromSectorId) || SeesNow(lane.ToSectorId)
                ? lane
                : lane with { State = LaneState.Open })
            .ToList();
    }

    public string FactionId { get; }
    public int CurrentTurn => _world.CurrentTurn;
    public IReadOnlyList<WorldFaction> Factions => _world.Factions;
    public IReadOnlyList<string> SectorIds { get; }

    /// <summary>
    /// Public in shape, believed in state — see the constructor. A lane you cannot see reads open.
    /// </summary>
    public IReadOnlyList<WorldLane> Lanes { get; }
    public IReadOnlyList<WorldEntity> OwnForces { get; }

    public IntelSnapshot? Believed(string sectorId) => _intel.Of(sectorId);

    public IntelState StateOf(string sectorId) =>
        IntelLadder.StateOf(Believed(sectorId), _world.CurrentTurn, SeesNow(sectorId));

    public int AgeOf(string sectorId) =>
        Believed(sectorId) is { } snapshot ? IntelLadder.AgeOf(snapshot, _world.CurrentTurn) : 0;

    public long OwnLoamStock(string sectorId)
    {
        var sector = _world.Sectors.FirstOrDefault(s => string.Equals(s.SectorId, sectorId, StringComparison.Ordinal));
        return sector is not null && string.Equals(sector.OwnerFactionId, FactionId, StringComparison.Ordinal)
            ? sector.LoamStock
            : 0;
    }

    bool SeesNow(string sectorId) =>
        _sight.TryGetValue(sectorId, out var level) && level != SectorSight.None;
}
