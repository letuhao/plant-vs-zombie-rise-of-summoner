namespace FusionRpg.Core.World;

/// <summary>
/// The seven creation rules (spec-world-model.md §Creation and validation). A malformed world must
/// never reach the turn engine, so this throws loudly at the gate rather than returning a flag.
/// </summary>
public static class WorldValidation
{
    public static WorldState Validate(WorldState world)
    {
        RequireStableOrder(world);
        Rule1CatalogIdsExist(world);
        Rule2LaneShape(world);
        Rule3Connected(world);
        Rule4Homeworld(world);
        Rule5SeatCounts(world);
        Rule6SlotShape(world);
        Rule7EntityPlacement(world);
        return world;
    }

    /// <summary>Ordering is load-bearing for hashing and replay, so it is validated, not assumed.</summary>
    static void RequireStableOrder(WorldState w)
    {
        RequireSorted(w.Factions.Select(f => f.FactionId), "factions");
        RequireSorted(w.Sectors.Select(s => s.SectorId), "sectors");
        RequireSorted(w.Lanes.Select(l => l.LaneId), "lanes");
        RequireSorted(w.Entities.Select(e => e.EntityId), "entities");
    }

    static void RequireOwner(string? ownerFactionId, HashSet<string> factionIds, string what)
    {
        if (ownerFactionId != null && !factionIds.Contains(ownerFactionId))
            throw new InvalidOperationException($"{what} is owned by unknown faction '{ownerFactionId}'.");
    }

    static void RequireSorted(IEnumerable<string> ids, string what)
    {
        string? previous = null;
        foreach (var id in ids)
        {
            if (previous != null && string.CompareOrdinal(previous, id) >= 0)
                throw new InvalidOperationException($"World {what} must be in stable ascending id order ('{previous}' before '{id}').");
            previous = id;
        }
    }

    static void Rule1CatalogIdsExist(WorldState w)
    {
        var factionIds = w.Factions.Select(f => f.FactionId).ToHashSet(StringComparer.Ordinal);

        foreach (var f in w.Factions)
            WorldIds.RequireKebab(f.FactionId, "Faction id");

        // Ownership is a reference, so it is checked like one — a sector owned by a faction that
        // does not exist would otherwise validate and then break every ownership read downstream.
        foreach (var s in w.Sectors)
        {
            RequireOwner(s.OwnerFactionId, factionIds, $"Sector '{s.SectorId}'");
            foreach (var slot in s.Slots)
                RequireOwner(slot.OwnerFactionId, factionIds, $"Sector '{s.SectorId}' slot {slot.SlotIndex}");
        }

        foreach (var e in w.Entities)
            if (!factionIds.Contains(e.OwnerFactionId))
                throw new InvalidOperationException($"Entity '{e.EntityId}' belongs to unknown faction '{e.OwnerFactionId}'.");

        foreach (var s in w.Sectors)
        {
            if (!SectorTypeCatalog.IsKnown(s.TypeId))
                throw new InvalidOperationException($"Sector '{s.SectorId}' has unknown sector type '{s.TypeId}'.");
            foreach (var slot in s.Slots)
                if (!SlotTypeCatalog.IsKnown(slot.SlotTypeId))
                    throw new InvalidOperationException($"Sector '{s.SectorId}' slot {slot.SlotIndex} has unknown slot type '{slot.SlotTypeId}'.");
        }

        foreach (var l in w.Lanes)
            if (!LaneTypeCatalog.IsKnown(l.TypeId))
                throw new InvalidOperationException($"Lane '{l.LaneId}' has unknown lane type '{l.TypeId}'.");
    }

    static void Rule2LaneShape(WorldState w)
    {
        var sectorIds = w.Sectors.Select(s => s.SectorId).ToHashSet(StringComparer.Ordinal);
        var seenPairs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var l in w.Lanes)
        {
            if (string.Equals(l.FromSectorId, l.ToSectorId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Lane '{l.LaneId}' joins sector '{l.FromSectorId}' to itself.");
            if (!sectorIds.Contains(l.FromSectorId))
                throw new InvalidOperationException($"Lane '{l.LaneId}' starts at unknown sector '{l.FromSectorId}'.");
            if (!sectorIds.Contains(l.ToSectorId))
                throw new InvalidOperationException($"Lane '{l.LaneId}' ends at unknown sector '{l.ToSectorId}'.");

            // Undirected identity: A→B and B→A are the same edge.
            var key = string.CompareOrdinal(l.FromSectorId, l.ToSectorId) <= 0
                ? l.FromSectorId + "|" + l.ToSectorId
                : l.ToSectorId + "|" + l.FromSectorId;
            if (!seenPairs.Add(key))
                throw new InvalidOperationException($"Duplicate lane between '{l.FromSectorId}' and '{l.ToSectorId}' ('{l.LaneId}').");
        }
    }

    static void Rule3Connected(WorldState w)
    {
        if (w.Sectors.Count == 0)
            throw new InvalidOperationException("A world needs at least one sector.");

        // Undirected reachability: a one-way current still means the places are neighbours.
        var neighbours = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var s in w.Sectors)
            neighbours[s.SectorId] = new List<string>();
        foreach (var l in w.Lanes)
        {
            neighbours[l.FromSectorId].Add(l.ToSectorId);
            neighbours[l.ToSectorId].Add(l.FromSectorId);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { w.Sectors[0].SectorId };
        var queue = new Queue<string>();
        queue.Enqueue(w.Sectors[0].SectorId);
        while (queue.Count > 0)
            foreach (var next in neighbours[queue.Dequeue()])
                if (seen.Add(next))
                    queue.Enqueue(next);

        foreach (var s in w.Sectors)
            if (!seen.Contains(s.SectorId))
                throw new InvalidOperationException($"Sector '{s.SectorId}' is unreachable — the world graph must be connected.");
    }

    static void Rule4Homeworld(WorldState w)
    {
        var homes = w.Sectors
            .Where(s => SectorTypeCatalog.Get(s.TypeId).Flags.HasFlag(SectorTypeFlags.Home))
            .ToList();

        if (homes.Count != 1)
            throw new InvalidOperationException($"A world needs exactly one homeworld sector; found {homes.Count}.");

        var home = homes[0];
        var player = w.Factions.FirstOrDefault(f => f.Kind == WorldFactionKind.Player)
            ?? throw new InvalidOperationException("A world needs a player faction to own the homeworld.");

        if (!string.Equals(home.OwnerFactionId, player.FactionId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Homeworld '{home.SectorId}' must be owned by the player faction '{player.FactionId}'.");
        if (home.Slots.All(sl => sl.SlotTypeId != SlotTypeCatalog.SeatSlotTypeId))
            throw new InvalidOperationException($"Homeworld '{home.SectorId}' must hold a Seat slot.");
    }

    static void Rule5SeatCounts(WorldState w)
    {
        foreach (var s in w.Sectors)
        {
            var type = SectorTypeCatalog.Get(s.TypeId);
            var seats = s.Slots.Count(sl => sl.SlotTypeId == SlotTypeCatalog.SeatSlotTypeId);

            if (type.CanHostSeat && seats != 1)
                throw new InvalidOperationException($"Sector '{s.SectorId}' is base-capable and needs exactly one Seat slot; found {seats}.");
            if (!type.CanHostSeat && seats != 0)
                throw new InvalidOperationException($"Sector '{s.SectorId}' cannot host a base yet holds a Seat slot.");
        }
    }

    static void Rule6SlotShape(WorldState w)
    {
        foreach (var s in w.Sectors)
        {
            var type = SectorTypeCatalog.Get(s.TypeId);
            for (var i = 0; i < s.Slots.Count; i++)
                if (s.Slots[i].SlotIndex != i)
                    throw new InvalidOperationException(
                        $"Sector '{s.SectorId}' slot indexes must be contiguous from 0 (index {i} is {s.Slots[i].SlotIndex}).");

            foreach (var slot in s.Slots)
            {
                if (!type.AllowedSlotTypes.Contains(slot.SlotTypeId, StringComparer.Ordinal))
                    throw new InvalidOperationException(
                        $"Sector '{s.SectorId}' (type '{type.TypeId}') does not allow slot type '{slot.SlotTypeId}'.");

                // An intact guard with no encounter can never be cleared, and a sector is claimable
                // only when every slot is cleared — so this would soft-lock the map while validating
                // perfectly. A *cleared* guard may keep its id: it is the record of what was here.
                if (slot.GuardState == GuardState.Intact && string.IsNullOrEmpty(slot.GuardWaveId))
                    throw new InvalidOperationException(
                        $"Sector '{s.SectorId}' slot {slot.SlotIndex} has an intact guard with no encounter id — it could never be cleared.");
            }
        }
    }

    static void Rule7EntityPlacement(WorldState w)
    {
        var sectorIds = w.Sectors.Select(s => s.SectorId).ToHashSet(StringComparer.Ordinal);
        var laneIds = w.Lanes.Select(l => l.LaneId).ToHashSet(StringComparer.Ordinal);
        foreach (var e in w.Entities)
        {
            var atSector = e.AtSectorId != null;
            var onLane = e.OnLaneId != null;

            if (atSector && onLane)
                throw new InvalidOperationException($"Entity '{e.EntityId}' is both at a sector and on a lane.");
            if (!atSector && !onLane)
                throw new InvalidOperationException($"Entity '{e.EntityId}' is neither at a sector nor on a lane.");
            if (atSector && !sectorIds.Contains(e.AtSectorId!))
                throw new InvalidOperationException($"Entity '{e.EntityId}' stands at unknown sector '{e.AtSectorId}'.");
            if (onLane && !laneIds.Contains(e.OnLaneId!))
                throw new InvalidOperationException($"Entity '{e.EntityId}' marches on unknown lane '{e.OnLaneId}'.");
            if (e.LaneProgressMilli is < 0 or > 1000)
                throw new InvalidOperationException($"Entity '{e.EntityId}' lane progress {e.LaneProgressMilli} is outside 0..1000.");
        }
    }
}
