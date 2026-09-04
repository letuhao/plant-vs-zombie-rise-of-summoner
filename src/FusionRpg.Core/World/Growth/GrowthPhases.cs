using FusionRpg.Core.World.Turn;

namespace FusionRpg.Core.World.Growth;

/// <summary>
/// Wakes the `Growth` phase up (spec-sector-development.md §1) — `TurnEngine.cs:196-200` was
/// `report.BeginPhase(Phases.Growth); return world;` in full before this task. Pure in
/// <c>(state, seed)</c> like every other phase — no wall clock, no unowned RNG, `CalendarRoll` is
/// the only randomness anywhere near this and it is derived, not drawn here. Kept out of
/// `TurnEngine.cs` for the same reason `LoamPhases`/`SiegePhase`/`MovementPhase` are their own
/// files: that file is already the busiest in the module.
/// </summary>
public static class GrowthPhases
{
    /// <summary>
    /// Every held sector with a Seat accrues the weekly pulse into
    /// <see cref="WorldSector.RecruitStock"/>, on week boundaries and only on week boundaries
    /// (<see cref="RecruitPolicy.PulseFor"/> itself gates that). Walks `world.Sectors` in its own
    /// stored order (never a dictionary) — the same stable-ordering discipline every other phase
    /// already keeps.
    ///
    /// Takes every tuning number as an explicit parameter rather than reading `RecruitPolicy`'s own
    /// accessors internally — the same split the module's own Code Style section states ("pure
    /// functions over the world model... the phase wiring calls them and applies the result") and
    /// `RecruitPolicy.PulseFor` itself already uses one level down: `TurnEngine.Growth` (the real
    /// caller) reads the live, process-wide tuning and passes it in; a test supplies whatever local
    /// values it needs directly, with no risk of racing `RecruitPolicy.Configure`'s shared static
    /// field against another test class under xUnit's default parallelism.
    /// </summary>
    public static WorldState Growth(
        WorldState world, TurnReport report, string phase, int turn, ulong seed,
        long seatPulsePerWeek, int lairMultiplierMilli, int specialWeekMultiplierMilli)
    {
        var roll = TurnCalendar.Roll(turn, seed);
        var sectors = new List<WorldSector>(world.Sectors.Count);

        foreach (var sector in world.Sectors)
        {
            if (sector.OwnerFactionId is null)
            {
                sectors.Add(sector);
                continue;
            }

            var hasSeat = sector.Slots.Any(sl => sl.SlotTypeId == SlotTypeCatalog.SeatSlotTypeId);
            var lairCleared = sector.Slots.Any(sl =>
                SlotTypeCatalog.Get(sl.SlotTypeId).Kind == SlotKind.Lair && sl.GuardState == GuardState.Cleared);

            var pulse = RecruitPolicy.PulseFor(
                hasSeat, lairCleared, roll, seatPulsePerWeek, lairMultiplierMilli, specialWeekMultiplierMilli);

            if (pulse <= 0)
            {
                sectors.Add(sector);
                continue;
            }

            // Structural, not prose (world-stage W39's SectorId field) — the same discipline
            // `LoamPhases.Production`'s own "loam.overflow" line already follows.
            report.Add(phase, TurnReportKinds.Event, sector.SectorId, "growth.pulse:" + pulse, sector.SectorId);
            sectors.Add(sector with { RecruitStock = sector.RecruitStock + pulse });
        }

        return world with { Sectors = sectors };
    }
}
