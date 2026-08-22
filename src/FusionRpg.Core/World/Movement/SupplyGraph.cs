using FusionRpg.Core.World.Turn;

namespace FusionRpg.Core.World.Movement;

/// <summary>
/// The chain back to the homeworld (spec-world-movement.md §Supply connectivity).
///
/// A plain breadth-first pass in stable id order, recomputed every turn and never cached: a stored
/// "in supply" flag is exactly the kind of derived state that goes stale the first time a lane is
/// cut, and it would then be wrong in the one situation the player cares about.
/// </summary>
public static class SupplyGraph
{
    /// <summary>What an unsupplied force loses each turn, as a fraction of each member's health.</summary>
    public const int AttritionWoundMilli = 50;

    /// <summary>
    /// Sectors this faction can still reach from a Seat it holds. A faction with no Seat of its own
    /// has no supply network at all — the wild do not starve for want of a capital they never had —
    /// so the result is empty and <see cref="Run"/> leaves them alone.
    /// </summary>
    public static IReadOnlySet<string> ConnectedSectors(WorldState world, string factionId)
    {
        var byId = world.Sectors.ToDictionary(s => s.SectorId, StringComparer.Ordinal);

        bool Usable(WorldSector sector) =>
            string.Equals(sector.OwnerFactionId, factionId, StringComparison.Ordinal)
            && !ZoneOfControl.IsHeldAgainst(world, sector.SectorId, factionId);

        var reached = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Queue<string>();

        // Sources, in stable order: every Seat this faction still holds.
        foreach (var sector in world.Sectors)
        {
            if (!Usable(sector)) continue;
            if (sector.Slots.All(sl => sl.SlotTypeId != SlotTypeCatalog.SeatSlotTypeId)) continue;
            if (reached.Add(sector.SectorId)) frontier.Enqueue(sector.SectorId);
        }

        if (reached.Count == 0) return reached;

        // Neighbours, walked in lane id order so the traversal is reproducible even though the set
        // it produces would be the same either way.
        var outgoing = new Dictionary<string, List<WorldLane>>(StringComparer.Ordinal);
        foreach (var lane in world.Lanes)
        {
            if (lane.State != LaneState.Open) continue;
            if (!LaneTypeCatalog.Get(lane.TypeId).CarriesSupply) continue;

            Add(outgoing, lane.FromSectorId, lane);
            Add(outgoing, lane.ToSectorId, lane);
        }

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (!outgoing.TryGetValue(current, out var lanes)) continue;

            foreach (var lane in lanes)
            {
                var type = LaneTypeCatalog.Get(lane.TypeId);
                // A temporal current only carries supply the way it flows.
                if (type.OneWay && !string.Equals(lane.FromSectorId, current, StringComparison.Ordinal))
                    continue;

                var next = string.Equals(lane.FromSectorId, current, StringComparison.Ordinal)
                    ? lane.ToSectorId
                    : lane.FromSectorId;

                if (!byId.TryGetValue(next, out var sector) || !Usable(sector)) continue;
                if (reached.Add(next)) frontier.Enqueue(next);
            }
        }

        return reached;
    }

    /// <summary>
    /// The Pressure phase's supply pass: report every holding that has fallen off the chain, and
    /// bleed every force that is standing outside one. Exactly once, because it runs exactly once.
    /// </summary>
    public static WorldState Run(WorldState world, TurnReport report, string phase)
    {
        var connectedByFaction = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var faction in world.Factions)
            connectedByFaction[faction.FactionId] = ConnectedSectors(world, faction.FactionId);

        foreach (var sector in world.Sectors)
        {
            if (sector.OwnerFactionId is not { } owner) continue;
            if (!connectedByFaction.TryGetValue(owner, out var connected) || connected.Count == 0) continue;
            if (!connected.Contains(sector.SectorId))
                report.Add(phase, TurnReportKinds.Event, owner, "supply.cut:" + sector.SectorId);
        }

        var survivors = new List<WorldEntity>(world.Entities.Count);
        foreach (var entity in world.Entities)
        {
            if (!connectedByFaction.TryGetValue(entity.OwnerFactionId, out var connected) || connected.Count == 0)
            {
                survivors.Add(entity);
                continue;
            }

            if (InSupply(world, entity, connected))
            {
                // Supply gives as well as takes: a garrison with a line home mends. This is the only
                // healing there is, which is why `hold` had to mean something.
                survivors.Add(string.Equals(entity.Stance, MovementPolicy.Hold, StringComparison.Ordinal)
                    ? Recover(entity, report, phase)
                    : entity);
                continue;
            }

            var bitten = Starve(entity);
            report.Add(phase, TurnReportKinds.Event, entity.EntityId,
                "attrition:" + (bitten.Members.Count == 0 ? "lost" : entity.AtSectorId ?? entity.OnLaneId ?? ""));

            // A force that starves to nothing leaves the map, the same as one destroyed in a fight.
            if (bitten.Members.Count > 0) survivors.Add(bitten);
        }

        return world with { Entities = survivors };
    }

    /// <summary>A force on a lane counts as supplied if either end of it still is.</summary>
    static bool InSupply(WorldState world, WorldEntity entity, IReadOnlySet<string> connected)
    {
        if (entity.AtSectorId is { } at) return connected.Contains(at);
        if (entity.OnLaneId is not { } laneId) return false;

        var lane = world.Lanes.FirstOrDefault(l => l.LaneId == laneId);
        return lane is not null
               && (connected.Contains(lane.FromSectorId) || connected.Contains(lane.ToSectorId));
    }

    static WorldEntity Recover(WorldEntity entity, TurnReport report, string phase)
    {
        var mended = false;
        var members = new List<WorldEntityMember>(entity.Members.Count);

        foreach (var m in entity.Members)
        {
            var healed = Math.Max(0, m.Wounds - Math.Max(1, (int)((long)m.Hp * MovementPolicy.RecoveryMilli / 1000)));
            if (healed != m.Wounds) mended = true;
            members.Add(m with { Wounds = healed });
        }

        if (!mended) return entity;

        report.Add(phase, TurnReportKinds.Event, entity.EntityId, "recovery:" + (entity.AtSectorId ?? ""));
        return entity with { Members = members };
    }

    static WorldEntity Starve(WorldEntity entity)
    {
        var members = new List<WorldEntityMember>(entity.Members.Count);
        foreach (var m in entity.Members)
        {
            var wounds = m.Wounds + Math.Max(1, (int)((long)m.Hp * AttritionWoundMilli / 1000));
            if (wounds < m.Hp) members.Add(m with { Wounds = wounds });
        }

        return entity with { Members = members };
    }

    static void Add(Dictionary<string, List<WorldLane>> map, string sectorId, WorldLane lane)
    {
        if (!map.TryGetValue(sectorId, out var list)) map[sectorId] = list = new List<WorldLane>();
        list.Add(lane);
    }
}
