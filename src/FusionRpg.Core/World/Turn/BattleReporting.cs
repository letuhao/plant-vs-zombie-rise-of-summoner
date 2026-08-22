namespace FusionRpg.Core.World.Turn;

/// <summary>
/// One request in, one line of report and a new world out. Both places that start a fight — a
/// meeting during movement and a `clear` during sieges — go through here, so a battle always costs
/// the same and always shows up in the report the same way.
/// </summary>
public static class BattleReporting
{
    public static WorldState Fight(
        WorldState world, BattleRequest request, IBattleResolver resolver,
        TurnReport report, string phase, ulong seed)
    {
        var attacker = world.Entities.FirstOrDefault(e =>
            string.Equals(e.EntityId, request.AttackerEntityId, StringComparison.Ordinal));
        if (attacker is null) return world;

        var combatants = new List<WorldEntity> { attacker };
        if (request.DefenderEntityId is { } defenderId)
        {
            var defender = world.Entities.FirstOrDefault(e =>
                string.Equals(e.EntityId, defenderId, StringComparison.Ordinal));

            // The other side may already have died earlier in the same turn; a fight with nobody in
            // it is silently no fight rather than a phantom victory.
            if (defender is null) return world;
            combatants.Add(defender);
        }

        var outcome = resolver.Resolve(request, combatants, seed);
        var next = BattleApplication.Apply(world, outcome);

        if (outcome.GuardCleared && request.SlotIndex is { } slotIndex)
            next = BattleApplication.ClearGuard(next, request.LocationId, slotIndex);

        report.Add(phase, TurnReportKinds.Battle, request.BattleId,
            $"{request.Kind}:{request.LocationId}:{outcome.WinnerEntityId ?? "none"}");

        return next;
    }
}
