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

            var newlyRouted = side.Routed && !entity.Routed;
            var placed = newlyRouted ? FallBack(entity, world.Lanes) : entity;

            entities.Add(placed with
            {
                Members = side.Survivors,
                Routed = entity.Routed || side.Routed
            });
        }

        return world with { Entities = entities };
    }

    /// <summary>
    /// world-map: a routed force falls back rather than standing where the fight left it
    /// (spec-world-movement.md's own "`routed` — routed legions fall back and skip a turn's orders",
    /// specced but never wired — the owner confirmed directly, 2026-09-05, that it should be). Called
    /// only for a side <see cref="Apply"/> finds <b>newly</b> routed this fight (an already-routed
    /// force sits out its recovery turn where it already fell back to; the state itself blocks a
    /// second reversal even without that caller-side check, since a routed entity's next battle would
    /// have already cleared its lane fields below).
    ///
    /// No new stored state: a legion mid-crossing a lane already carries
    /// <see cref="WorldEntity.OnLaneId"/> and <see cref="WorldEntity.OnLaneTowardSectorId"/>, so "the
    /// way it came" is simply the lane's other endpoint — turning around, not pathing anywhere new.
    ///
    /// A force that was already standing at a sector when it routed (a garrison losing a Contact or
    /// Siege fight on its own ground, or the entrenched-bonus defender) has nothing to fall back
    /// *from* — it stays exactly where entrenched combat already left the map, which is the same
    /// distinction `PlaceholderBattleResolver`'s own `DefenderBonusMilli` already draws between a
    /// legion caught marching and one dug in.
    /// </summary>
    static WorldEntity FallBack(WorldEntity entity, IReadOnlyList<WorldLane> lanes)
    {
        if (entity.OnLaneId is not { } laneId || entity.OnLaneTowardSectorId is not { } toward) return entity;

        var lane = lanes.FirstOrDefault(l => string.Equals(l.LaneId, laneId, StringComparison.Ordinal));
        if (lane is null) return entity; // defensive: a vanished lane id falls through unmoved

        var origin = string.Equals(lane.ToSectorId, toward, StringComparison.Ordinal)
            ? lane.FromSectorId
            : lane.ToSectorId;

        return entity with
        {
            AtSectorId = origin,
            OnLaneId = null,
            OnLaneTowardSectorId = null,
            LaneProgressMilli = 0
        };
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
