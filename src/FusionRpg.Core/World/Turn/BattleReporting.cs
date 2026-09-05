namespace FusionRpg.Core.World.Turn;

/// <summary>
/// One request in, one line of report and a new world out. Both places that start a fight — a
/// meeting during movement and a `clear` during sieges — go through here, so a battle always costs
/// the same and always shows up in the report the same way.
/// </summary>
public static class BattleReporting
{
    /// <param name="arrivedViaLane">
    /// Entity id to lane id, for any force that fully arrived at a sector earlier in this same
    /// <see cref="Movement.MovementPhase"/> call — only <see cref="Movement.MovementPhase"/> ever
    /// populates it. Lets <see cref="BattleApplication"/> fall a freshly-arrived attacker back down
    /// the lane it just used if this fight routs it, the same way a mid-crossing rout already falls
    /// back down the lane it was on.
    /// </param>
    public static WorldState Fight(
        WorldState world, BattleRequest request, IBattleResolver resolver,
        TurnReport report, string phase, ulong seed,
        IReadOnlyDictionary<string, string>? arrivedViaLane = null)
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
        var next = BattleApplication.Apply(world, outcome, arrivedViaLane);

        if (outcome.GuardCleared && request.SlotIndex is { } slotIndex)
            next = BattleApplication.ClearGuard(next, request.LocationId, slotIndex);

        // New, and empty for every existing kind — so every existing battle takes the identical path.
        if (outcome.SlotResults.Count > 0)
            next = BattleApplication.ApplySlotResults(next, request.LocationId, outcome.SlotResults);

        // `LocationId` is "sector id, or lane id for a crossing" (`BattleSeam.cs:34`) — a lane-kind
        // battle must not put a lane id in the sector slot, which is exactly the class of bug
        // world-stage W13 exists to fix elsewhere in `MovementPhase.cs`; this line does not
        // reintroduce it for the one battle kind that can carry a lane id here.
        //
        // base-defense `siege-engagement` (module 20): a district assault names its own exit instead
        // of the generic winner-only line — "one report line per engagement, exit named" — scoped to
        // District only so every other kind's report text is byte-for-byte unchanged. Report text is
        // never part of `StateHasher.Hash` (confirmed via `TurnEngine.Step`'s own return line), so this
        // is golden-safe regardless, but the District-only guard keeps the change minimal anyway.
        var detail = request.Kind == BattleKinds.District && outcome.Exit is { } exit
            ? $"district:{request.LocationId}:{exit}"
            : $"{request.Kind}:{request.LocationId}:{outcome.WinnerEntityId ?? "none"}";
        report.Add(phase, TurnReportKinds.Battle, request.BattleId, detail,
            sectorId: request.Kind == BattleKinds.Lane ? null : request.LocationId);

        return next;
    }
}
