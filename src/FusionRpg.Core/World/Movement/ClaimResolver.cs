using FusionRpg.Core.World.Loam;
using FusionRpg.Core.World.Turn;

namespace FusionRpg.Core.World.Movement;

/// <summary>
/// Taking ground (spec-world-movement.md §Claiming). A sector falls when nothing hostile stands in
/// it and **every** slot's guard has been cleared — so a rich sector costs several turns and several
/// fights before it can be held.
///
/// Claims settle at the end of the turn, after movement and sieges, because all three of the things
/// a claim depends on — who is standing where, who is still alive, and which guards are left — are
/// decided by then. There is no separate tie-break between rival claims: two hostile forces in one
/// sector each block the other, and the battle they are already in is what decides it.
/// </summary>
public static class ClaimResolver
{
    public static WorldState Run(
        WorldState world, IReadOnlyList<WorldCommand> commands, TurnReport report, string phase, int turn)
    {
        var next = world;

        foreach (var command in commands.Where(c => c.Kind == WorldCommandKinds.Claim))
        {
            var entity = next.Entities.FirstOrDefault(e =>
                string.Equals(e.EntityId, command.EntityId, StringComparison.Ordinal));

            // The legion may have died or been broken since the order was revealed — both are
            // ordinary outcomes of the same turn, not errors.
            if (entity is null)
            {
                Drop(report, phase, command, "entity.gone");
                continue;
            }

            if (entity.Routed)
            {
                Drop(report, phase, command, "entity.routed");
                continue;
            }

            if (entity.AtSectorId is null
                || !string.Equals(entity.AtSectorId, command.SectorId, StringComparison.Ordinal))
            {
                Drop(report, phase, command, "claim.elsewhere");
                continue;
            }

            var sector = next.Sectors.First(s =>
                string.Equals(s.SectorId, entity.AtSectorId, StringComparison.Ordinal));

            // Claiming what your side already holds is a no-op, not a mistake — a standing order
            // that keeps firing must not fill the report with refusals.
            if (string.Equals(sector.OwnerFactionId, command.CommanderId, StringComparison.Ordinal))
            {
                report.Add(phase, TurnReportKinds.Event, command.CommandId, "claim.already-yours:" + sector.SectorId, sector.SectorId);
                continue;
            }

            if (ZoneOfControl.IsHeldAgainst(next, sector.SectorId, command.CommanderId))
            {
                Drop(report, phase, command, "claim.contested");
                continue;
            }

            var guarded = sector.Slots.FirstOrDefault(sl => sl.GuardState == GuardState.Intact);
            if (guarded is not null)
            {
                Drop(report, phase, command, "claim.guarded:" + guarded.SlotIndex);
                continue;
            }

            next = next with
            {
                Sectors = next.Sectors
                    .Select(s => string.Equals(s.SectorId, sector.SectorId, StringComparison.Ordinal)
                        ? s with
                        {
                            OwnerFactionId = command.CommanderId,
                            Phase = SectorPhase.Held,
                            LastSeenTurn = turn,
                            // Warding vs. capture (spec-loam-texture.md): the ward exempts FadePolicy
                            // only, never combat — capture ends the binding outright, no transfer to
                            // the new owner and no refund to the old one.
                            WardenBindingId = null
                        }
                        : s)
                    .ToList()
            };

            report.Add(phase, TurnReportKinds.Event, command.CommandId, "claim.held:" + sector.SectorId, sector.SectorId);

            // spec-loam-turn.md's settlement rule needs no enforcement — the fade *is* the
            // enforcement (ideal §8.10: barren ground can be taken but never kept). Refusing this
            // claim would delete a real play (seizing a corridor to sever an enemy's chain), so it
            // is allowed and simply warned about, naming the ground as temporary.
            if (!Habitability.For(sector))
                report.Add(phase, TurnReportKinds.Event, command.CommandId, "claim.barren:" + sector.SectorId, sector.SectorId);
        }

        return next;
    }

    static void Drop(TurnReport report, string phase, WorldCommand command, string reason) =>
        report.Add(phase, TurnReportKinds.CommandDropped, command.CommandId, reason);
}
