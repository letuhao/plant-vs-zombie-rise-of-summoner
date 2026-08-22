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

        bool Usable(string sectorId) =>
            byId.TryGetValue(sectorId, out var sector)
            && string.Equals(sector.OwnerFactionId, factionId, StringComparison.Ordinal)
            && !ZoneOfControl.IsHeldAgainst(world, sector.SectorId, factionId);

        // Sources: every Seat this faction still holds. The traversal itself belongs to
        // `SupplyReach`, because a faction policy asks the same question of what it *believes* —
        // same rule, different and deliberately less reliable inputs.
        var seats = world.Sectors
            .Where(s => Usable(s.SectorId)
                        && s.Slots.Any(sl => sl.SlotTypeId == SlotTypeCatalog.SeatSlotTypeId))
            .Select(s => s.SectorId);

        return SupplyReach.From(seats, SupplyReach.LinksOf(world.Lanes), Usable);
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

}
