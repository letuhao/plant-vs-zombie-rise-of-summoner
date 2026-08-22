namespace FusionRpg.Core.World.Turn;

/// <summary>
/// The world's half of the combat seam: turning an outcome back into map state. Kept apart from the
/// resolver on purpose — whatever fights the battle, only this file decides what a rout or a
/// wipeout means to the map, and there is exactly one of it.
/// </summary>
public static class BattleApplication
{
    public static WorldState Apply(WorldState world, BattleOutcome outcome)
    {
        if (outcome.Sides.Count == 0) return world;

        var sides = outcome.Sides.ToDictionary(s => s.EntityId, StringComparer.Ordinal);
        var entities = new List<WorldEntity>(world.Entities.Count);

        foreach (var entity in world.Entities)
        {
            if (!sides.TryGetValue(entity.EntityId, out var side))
            {
                entities.Add(entity);
                continue;
            }

            // Destroyed forces leave the map entirely. Keeping an empty husk around would let it
            // hold ground, project zone of control, and block a claim with nothing in it.
            if (side.Destroyed) continue;

            entities.Add(entity with
            {
                Members = side.Survivors,
                Routed = entity.Routed || side.Routed
            });
        }

        return world with { Entities = entities };
    }

    /// <summary>Flips one slot's guard. Only the slot named — a sector falls one fight at a time.</summary>
    public static WorldState ClearGuard(WorldState world, string sectorId, int slotIndex) => world with
    {
        Sectors = world.Sectors
            .Select(s => string.Equals(s.SectorId, sectorId, StringComparison.Ordinal)
                ? s with
                {
                    Slots = s.Slots
                        .Select(sl => sl.SlotIndex == slotIndex
                            ? sl with { GuardState = GuardState.Cleared }
                            : sl)
                        .ToList()
                }
                : s)
            .ToList()
    };
}
