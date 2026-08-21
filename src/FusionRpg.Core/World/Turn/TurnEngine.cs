namespace FusionRpg.Core.World.Turn;

/// <summary>What one turn produced: the new world, what happened, and the drift detector.</summary>
public sealed record TurnResult(WorldState World, TurnReport Report, string StateHash);

/// <summary>
/// The map's clock (spec-turn-engine.md). <c>state(N+1) = Step(state(N), commands(N), seed)</c> —
/// pure: no I/O, no wall clock, no ambient state, and no knowledge of which commander is human.
///
/// The phase order is locked; changing it bumps the ruleset version, so it is written once here and
/// observable in the report. Movement resolves through a discrete-event queue rather than fixed
/// sub-steps — wave 1 leaves that queue empty, and <c>world-movement</c> fills it.
/// </summary>
public static class TurnEngine
{
    public const int EngineVersion = 1;
    public const int RulesetVersion = 1;

    public static class Phases
    {
        public const string Reveal = "Reveal";
        public const string Movement = "Movement";
        public const string Sieges = "Sieges";
        public const string Production = "Production";
        public const string Growth = "Growth";
        public const string Pressure = "Pressure";
        public const string Events = "Events";
        public const string Snapshot = "Snapshot";
    }

    public static TurnResult Step(WorldState world, IReadOnlyList<WorldCommand> commands, ulong seed)
    {
        var report = new TurnReport();
        var turn = world.CurrentTurn + 1;

        var revealed = Reveal(world, commands, report);
        var next = Movement(world, revealed, report, seed);
        next = Sieges(next, report);
        next = Production(next, report);
        next = Growth(next, report);
        next = Pressure(next, report);
        next = Events(next, report, turn, seed);
        next = Snapshot(next, report, turn);

        return new TurnResult(next, report, StateHasher.Hash(next));
    }

    /// <summary>
    /// Orders seal at commit and reveal together, in stable (commander, command) order — never the
    /// order they arrived in, or two clients racing to submit would change a turn's outcome.
    ///
    /// Legality is re-checked here because the world may have moved since submission. A stale order
    /// is reported and skipped: one commander's out-of-date plan must never abort everyone's turn.
    /// </summary>
    static List<WorldCommand> Reveal(WorldState world, IReadOnlyList<WorldCommand> commands, TurnReport report)
    {
        report.BeginPhase(Phases.Reveal);

        var ordered = commands
            .OrderBy(c => c.CommanderId, StringComparer.Ordinal)
            .ThenBy(c => c.CommandId, StringComparer.Ordinal)
            .ToList();

        var legal = new List<WorldCommand>(ordered.Count);
        foreach (var command in ordered)
        {
            var (ok, reason) = WorldCommandAdmission.Admit(world, command);
            if (!ok)
            {
                report.Add(Phases.Reveal, TurnReportKinds.CommandDropped, command.CommandId, reason);
                continue;
            }

            legal.Add(command);
            report.Add(Phases.Reveal, TurnReportKinds.CommandAccepted, command.CommandId, command.Kind);
        }

        return legal;
    }

    /// <summary>
    /// Discrete-event movement resolution. Wave 1 has no movement kinds, so the queue drains empty
    /// and the phase is a no-op — the pipeline exists first so `world-movement` adds rules, not
    /// plumbing.
    /// </summary>
    static WorldState Movement(WorldState world, IReadOnlyList<WorldCommand> commands, TurnReport report, ulong seed)
    {
        report.BeginPhase(Phases.Movement);

        var queue = new TurnEventQueue();
        // world-movement seeds arrivals and crossings here.

        while (queue.TryDequeue(out var next))
            report.Add(Phases.Movement, TurnReportKinds.Event, next.EntityId, next.Kind);

        return world;
    }

    static WorldState Sieges(WorldState world, TurnReport report)
    {
        report.BeginPhase(Phases.Sieges);
        return world;
    }

    static WorldState Production(WorldState world, TurnReport report)
    {
        report.BeginPhase(Phases.Production);
        return world;
    }

    static WorldState Growth(WorldState world, TurnReport report)
    {
        report.BeginPhase(Phases.Growth);
        return world;
    }

    static WorldState Pressure(WorldState world, TurnReport report)
    {
        report.BeginPhase(Phases.Pressure);
        return world;
    }

    /// <summary>Calendar boundaries are rolled and reported; their effects belong to later modules.</summary>
    static WorldState Events(WorldState world, TurnReport report, int turn, ulong seed)
    {
        report.BeginPhase(Phases.Events);

        var roll = TurnCalendar.Roll(turn, seed);
        if (roll.WeekBoundary)
        {
            report.Add(Phases.Events, TurnReportKinds.Calendar, "week", roll.SpecialWeek ? "special" : "ordinary");
            if (roll.MonthBoundary)
                report.Add(Phases.Events, TurnReportKinds.Calendar, "month",
                    roll.Plague ? "plague" : roll.SpecialMonth ? "special" : "ordinary");
        }

        return world;
    }

    static WorldState Snapshot(WorldState world, TurnReport report, int turn)
    {
        report.BeginPhase(Phases.Snapshot);
        return world with { CurrentTurn = turn };
    }
}
