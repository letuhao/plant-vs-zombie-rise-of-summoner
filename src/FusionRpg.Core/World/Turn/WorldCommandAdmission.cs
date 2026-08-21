namespace FusionRpg.Core.World.Turn;

/// <summary>
/// Admission — the cheap gate at submit time (spec-turn-engine.md §Commands). It answers "is this
/// order well-formed, and is this commander entitled to give it?" and nothing else.
///
/// It deliberately does NOT judge whether the order will still be possible when the turn resolves.
/// That is legality-at-reveal, it belongs to the engine, and it drops the command into the turn
/// report with a reason rather than refusing the submission — one commander's stale order must
/// never abort a turn.
/// </summary>
public static class WorldCommandAdmission
{
    /// <summary>Command ids reach the store as a primary key, so they are bounded here.</summary>
    public const int MaxCommandIdLength = 64;

    public static (bool Ok, string Reason) Admit(WorldState world, WorldCommand command)
    {
        if (!WorldCommandKinds.IsKnown(command.Kind))
            return (false, "kind.unknown");

        if (string.IsNullOrWhiteSpace(command.CommandId))
            return (false, "command.id-missing");
        if (command.CommandId.Length > MaxCommandIdLength)
            return (false, "command.id-too-long");

        var commander = world.Factions
            .FirstOrDefault(f => string.Equals(f.FactionId, command.CommanderId, StringComparison.Ordinal));
        if (commander is null)
            return (false, "commander.unknown");

        if (command.EntityId is { } entityId)
        {
            var entity = world.Entities
                .FirstOrDefault(e => string.Equals(e.EntityId, entityId, StringComparison.Ordinal));
            if (entity is null)
                return (false, "entity.unknown");
            if (!string.Equals(entity.OwnerFactionId, command.CommanderId, StringComparison.Ordinal))
                return (false, "entity.not-yours");
        }

        if (command.SectorId is { } sectorId
            && world.Sectors.All(s => !string.Equals(s.SectorId, sectorId, StringComparison.Ordinal)))
            return (false, "sector.unknown");

        foreach (var laneId in command.LanePath)
            if (world.Lanes.All(l => !string.Equals(l.LaneId, laneId, StringComparison.Ordinal)))
                return (false, "lane.unknown");

        return (true, "ok");
    }
}
