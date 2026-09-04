using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Loam;
using FusionRpg.Core.World.Movement;

namespace FusionRpg.Core.World.Turn;

/// <summary>What one turn produced: the new world, what happened, and the drift detector.</summary>
public sealed record TurnResult(WorldState World, TurnReport Report, string StateHash);

/// <summary>
/// The map's clock (spec-turn-engine.md). <c>state(N+1) = Step(state(N), commands(N), seed)</c> —
/// pure: no I/O, no wall clock, no ambient state, and no knowledge of which commander is human.
///
/// The phase order is locked; changing it bumps the ruleset version, so it is written once here and
/// observable in the report. Movement resolves through a discrete-event queue rather than fixed
/// sub-steps, so a lane crossing lands where it actually crosses instead of on the nearest sample.
/// </summary>
public static class TurnEngine
{
    public const int EngineVersion = 1;
    /// <summary>
    /// Bumped to 4 on 2026-08-23 (spec-loam-turn.md): `Production` and `Pressure` stop being
    /// pass-throughs. A sector now earns loam and pays upkeep every turn, and ground can be lost to
    /// the Fracture for the first time. The program's second and last golden re-bless — the first
    /// was `loam-model`'s field addition, both recorded here per the same discipline W20 established.
    ///
    /// Bumped to 3 on 2026-08-22: `Intel` moved *after* `Snapshot`. Belief is what you know at the
    /// end of the turn, and the end of the turn is after the turn has finished happening — before
    /// this, a claim settled in Snapshot was invisible to the faction that made it, which re-filed
    /// the order and had it dropped as `claim.already-yours`. Found by playing, not by testing.
    ///
    /// Bumped to 2 on 2026-08-22 when the `Intel` phase landed: every turn now writes each faction's
    /// belief, so the same commands produce a different — and larger — state than they did under
    /// version 1. Stored version-1 reports refuse to re-derive rather than fabricating, which is the
    /// behaviour this counter exists for.
    ///
    /// Bumped to 5 on 2026-08-23 (L27, spec-loam-legions.md): `Pressure` retires wound-based
    /// attrition and wires `LegionSupply.Resolve` in its place — a legion beyond supply now burns
    /// carried loam and is destroyed outright rather than bled slowly, a real change to what the
    /// same command log produces, not a field-only addition.
    ///
    /// Bumped to 6 on 2026-09-04 (world-stage W24-W30, decisions.md:98 — one bump for the whole
    /// `cede`/`bind-warden`/`dowse` wave, not three): `LoamPhases.Pressure`'s shortfall selection
    /// (`LoamForecast.Weakest`, W25) now reads a filed `cede` order as an input to which sector
    /// absorbs the shortfall. A world that files no `cede` order resolves identically to version 5
    /// — this bump exists only for the case a real order changes the outcome, and covers `bind-warden`
    /// (W28) and `dowse` (W30) landing after it without a second bump, per the same decision.
    /// </summary>
    public const int RulesetVersion = 6;

    public static class Phases
    {
        public const string Reveal = "Reveal";
        public const string Movement = "Movement";
        public const string Sieges = "Sieges";
        public const string Production = "Production";
        public const string Growth = "Growth";
        public const string Pressure = "Pressure";
        public const string Events = "Events";

        /// <summary>Claims settle and postures land, because everything they depend on is decided.</summary>
        public const string Snapshot = "Snapshot";

        /// <summary>
        /// **Last**, and it moved here in RulesetVersion 3. A faction records the world as it *ends*
        /// the turn — which means after claims have settled, so it can see that its own claim
        /// worked. Recording before Snapshot left a commander unable to observe the one thing it had
        /// just done.
        /// </summary>
        public const string Intel = "Intel";
    }

    /// <summary>
    /// <paramref name="resolver"/> defaults to the wave-1 placeholder. It is a parameter rather than
    /// a container registration on purpose: the world module is the only thing that names it, so
    /// nothing else can start depending on its numbers before the real combat seam lands.
    /// </summary>
    public static TurnResult Step(
        WorldState world, IReadOnlyList<WorldCommand> commands, ulong seed, IBattleResolver? resolver = null)
    {
        var report = new TurnReport();
        var turn = world.CurrentTurn + 1;
        var battles = resolver ?? PlaceholderBattleResolver.Instance;

        // A rout is spent at the *top* of the turn it costs, not at the bottom. Clearing it here
        // rather than in Snapshot is what lets a force that is broken again during this same turn
        // keep the new rout instead of having it cancelled by the one it was already serving.
        var recovering = world.Entities
            .Where(e => e.Routed)
            .Select(e => e.EntityId)
            .ToHashSet(StringComparer.Ordinal);

        var opening = recovering.Count == 0
            ? world
            : world with
            {
                Entities = world.Entities.Select(e => e.Routed ? e with { Routed = false } : e).ToList()
            };

        var revealed = Reveal(opening, commands, recovering, report);
        var movement = Movement(opening, revealed, report, turn, battles, seed);
        var next = movement.World;
        next = Sieges(next, revealed, report, turn, battles, seed);
        next = Production(next, report);
        next = Growth(next, report, turn, seed);
        next = Pressure(next, revealed, report, turn, seed);
        next = Events(next, report, turn, seed);

        // Snapshot before Intel (RulesetVersion 3): a claim settles in Snapshot, so recording belief
        // first meant a faction could not see that its *own* claim had succeeded — it re-filed the
        // order the next turn and the engine dropped it as `claim.already-yours`. Belief is what you
        // know at the end of the turn, and the end of the turn is after the turn has finished
        // happening. Found by playing twenty turns and watching Zomboss claim the same sector twice.
        next = Snapshot(next, revealed, report, turn);
        next = Observe(world, next, report, turn, movement.VisitedByFaction);

        return new TurnResult(next, report, StateHasher.Hash(next));
    }

    /// <summary>
    /// Orders seal at commit and reveal together, in stable (commander, command) order — never the
    /// order they arrived in, or two clients racing to submit would change a turn's outcome.
    ///
    /// Legality is re-checked here because the world may have moved since submission. A stale order
    /// is reported and skipped: one commander's out-of-date plan must never abort everyone's turn.
    /// </summary>
    static List<WorldCommand> Reveal(
        WorldState world, IReadOnlyList<WorldCommand> commands,
        IReadOnlySet<string> recovering, TurnReport report)
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

            // A force beaten in the field spends the following turn recovering, so its orders are
            // dropped here rather than silently ignored downstream.
            if (command.EntityId is { } subject && recovering.Contains(subject))
            {
                report.Add(Phases.Reveal, TurnReportKinds.CommandDropped, command.CommandId, "entity.routed");
                continue;
            }

            // A garrison has given up its mobility for the turn; a march order for one is refused
            // here rather than silently producing a zero-distance move.
            if (command.Kind == WorldCommandKinds.Move && command.EntityId is { } marcher
                && world.Entities.Any(e =>
                    string.Equals(e.EntityId, marcher, StringComparison.Ordinal)
                    && string.Equals(e.Stance, MovementPolicy.Hold, StringComparison.Ordinal)))
            {
                report.Add(Phases.Reveal, TurnReportKinds.CommandDropped, command.CommandId, "entity.held");
                continue;
            }

            legal.Add(command);
            report.Add(Phases.Reveal, TurnReportKinds.CommandAccepted, command.CommandId, command.Kind);
        }

        return legal;
    }

    /// <summary>
    /// Discrete-event movement, contact, and the fights either one starts. The work lives in
    /// <see cref="MovementPhase"/>; the engine owns only the fact that it happens here, before
    /// sieges and after reveal.
    /// </summary>
    static MovementResult Movement(
        WorldState world, IReadOnlyList<WorldCommand> commands, TurnReport report,
        int turn, IBattleResolver resolver, ulong seed)
    {
        report.BeginPhase(Phases.Movement);
        return MovementPhase.Run(world, commands, report, Phases.Movement, turn, resolver, seed);
    }

    /// <summary>Deliberate attacks on slot guards — never a consequence of walking past one.</summary>
    static WorldState Sieges(
        WorldState world, IReadOnlyList<WorldCommand> commands, TurnReport report,
        int turn, IBattleResolver resolver, ulong seed)
    {
        report.BeginPhase(Phases.Sieges);
        return SiegePhase.Run(world, commands, report, Phases.Sieges, turn, resolver, seed);
    }

    /// <summary>A sector earns before it pays — `LoamPhases.Production` (spec-loam-turn.md).</summary>
    static WorldState Production(WorldState world, TurnReport report)
    {
        report.BeginPhase(Phases.Production);
        return LoamPhases.Production(world, report, Phases.Production);
    }

    static WorldState Growth(WorldState world, TurnReport report, int turn, ulong seed)
    {
        report.BeginPhase(Phases.Growth);
        return FusionRpg.Core.World.Growth.GrowthPhases.Growth(
            world, report, Phases.Growth, turn, seed,
            FusionRpg.Core.World.Growth.RecruitPolicy.SeatPulsePerWeek,
            FusionRpg.Core.World.Growth.RecruitPolicy.LairMultiplierMilli,
            FusionRpg.Core.World.Growth.RecruitPolicy.SpecialWeekMultiplierMilli);
    }

    /// <summary>
    /// `Sustain` resolves first, at the very top: its spend must already be sitting in a sector's
    /// stock before the component's automatic accounting runs this same turn (spec-loam-legions.md).
    /// Supply is recomputed next, from scratch, and nothing about it is carried between turns.
    /// Loam's upkeep and fade run *after*, so garrison upkeep reads the garrison that survived this
    /// turn's supply pass. `LegionSupply.Resolve` runs last: sector upkeep first, legion top-up
    /// and burn second, from whatever the same pool has left.
    /// </summary>
    static WorldState Pressure(
        WorldState world, IReadOnlyList<WorldCommand> commands, TurnReport report, int turn, ulong seed)
    {
        report.BeginPhase(Phases.Pressure);
        var afterSustain = SustainResolver.Run(world, commands, report, Phases.Pressure);
        var afterSupply = SupplyGraph.Run(afterSustain, report, Phases.Pressure);

        // Built the same way `Snapshot` derives `postures` from `stance` orders (:285-288): a plain
        // faction id → sector id map, last order per faction wins, never a service or a lookup.
        var ceded = commands
            .Where(c => c.Kind == WorldCommandKinds.Cede && c.SectorId != null)
            .GroupBy(c => c.CommanderId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().SectorId!, StringComparer.Ordinal);

        var afterPressure = LoamPhases.Pressure(afterSupply, report, Phases.Pressure, turn, seed, ceded);
        return LegionSupply.Resolve(afterPressure, report, Phases.Pressure);
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

    /// <summary>
    /// Every faction writes down what it can see. Visibility spans the turn's start *and* end, so a
    /// legion that marched through somewhere reports on it and a faction driven off its own ground
    /// remembers it as of this turn — neither needs a special case.
    /// </summary>
    static WorldState Observe(
        WorldState atStart, WorldState world, TurnReport report, int turn,
        IReadOnlyDictionary<string, IReadOnlySet<string>> marchedThrough)
    {
        report.BeginPhase(Phases.Intel);

        var before = world.Intel;
        var after = IntelRecorder.Observe(atStart, world, turn, marchedThrough);

        // Report only what changed, and only as a count: the map view reads belief from the
        // projection, and a line per sector per faction would drown every other event in the turn.
        foreach (var faction in after)
        {
            var previously = before.FirstOrDefault(f => f.FactionId == faction.FactionId);
            var learned = faction.Sectors.Count - (previously?.Sectors.Count ?? 0);
            if (learned > 0)
                report.Add(Phases.Intel, TurnReportKinds.Event, faction.FactionId, "intel.new:" + learned);
        }

        return world with { Intel = after };
    }

    /// <summary>
    /// Closes the turn: claims settle, and every legion starts the next one with a full march
    /// budget. Rout is deliberately untouched here — it was already spent at the top of the turn,
    /// so anything still flagged was broken during *this* turn and owes next turn's orders.
    /// </summary>
    static WorldState Snapshot(
        WorldState world, IReadOnlyList<WorldCommand> commands, TurnReport report, int turn)
    {
        report.BeginPhase(Phases.Snapshot);

        // Claims settle here because everything they depend on — who is standing where, who is still
        // alive, which guards are left — is only decided once the rest of the turn has run.
        world = ClaimResolver.Run(world, commands, report, Phases.Snapshot, turn);

        // Build resolves right after — the same reason, and so it sees this same turn's claim if
        // one just landed on the same sector (spec-loam-structures.md).
        world = BuildResolver.Run(world, commands, report, Phases.Snapshot);

        // Raise resolves right after Build, the same reason and the same order (world-map W51,
        // spec-sector-development.md §1) — a claim and a raise may land on the same sector in the
        // same turn.
        world = FusionRpg.Core.World.Growth.RaiseResolver.Run(world, commands, report, Phases.Snapshot, turn);

        // Develop resolves right after Raise, the same reason and the same order (world-map W52,
        // spec-sector-development.md §3) — a claim and a develop may land on the same sector in the
        // same turn.
        world = FusionRpg.Core.World.Growth.DevelopResolver.Run(world, commands, report, Phases.Snapshot);

        // Warden binding resolves right after Build, the same reason and the same order — a claim
        // and a bind-warden may land in the same turn (spec-loam-texture.md, world-stage W28).
        world = WardenResolver.Run(world, commands, report, Phases.Snapshot);

        // Posture changes land here, then the refill reads them — so a legion keeps the budget it
        // started the turn with and only pays for its new posture from the next turn. Digging in
        // *after* marching your full distance must not be free.
        var postures = commands
            .Where(c => c.Kind == WorldCommandKinds.Stance && c.EntityId != null && c.Stance != null)
            .GroupBy(c => c.EntityId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Stance!, StringComparer.Ordinal);

        return world with
        {
            CurrentTurn = turn,
            Entities = world.Entities
                .Select(e =>
                {
                    var stance = postures.TryGetValue(e.EntityId, out var ordered) ? ordered : e.Stance;
                    return e with { Stance = stance, MovementRemaining = MovementPolicy.BudgetFor(stance) };
                })
                .ToList()
        };
    }
}
