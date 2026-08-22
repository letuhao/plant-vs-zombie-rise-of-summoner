using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Ai;
using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Turn;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// The per-turn command log (spec-turn-engine.md §Persistence). Submission is idempotent on
/// (world, turn, commander, commandId); the stored original always wins a replay, so a client can
/// retry a request it never saw the answer to without rewriting an order it already committed.
/// </summary>
public sealed partial class RpgStore
{
    void EnsureWorldTurnSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS rpg_world_commands (
              world_id TEXT NOT NULL,
              turn INTEGER NOT NULL,
              commander_id TEXT NOT NULL,
              command_id TEXT NOT NULL,
              seq INTEGER NOT NULL,
              kind TEXT NOT NULL,
              payload_json TEXT NOT NULL,
              submitted_utc TEXT NOT NULL,
              PRIMARY KEY (world_id, turn, commander_id, command_id)
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_world_commands_turn
              ON rpg_world_commands(world_id, turn, commander_id, seq);
            CREATE TABLE IF NOT EXISTS rpg_world_turn_commits (
              world_id TEXT NOT NULL,
              turn INTEGER NOT NULL,
              commander_id TEXT NOT NULL,
              committed_utc TEXT NOT NULL,
              PRIMARY KEY (world_id, turn, commander_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_world_turn_log (
              world_id TEXT NOT NULL,
              turn INTEGER NOT NULL,
              state_hash TEXT NOT NULL,
              engine_version INTEGER NOT NULL,
              ruleset_version INTEGER NOT NULL,
              seed TEXT NOT NULL,
              committed_utc TEXT NOT NULL,
              report_json TEXT,
              PRIMARY KEY (world_id, turn)
            );
            """);

        // Why an AI filed an order. Nullable because the player never explains themselves, and a
        // column rather than a field on WorldCommand because that record is the replay unit — an
        // audit string inside it would travel through the engine and the hash for no reason.
        EnsureColumn(db, "rpg_world_commands", "reason", "TEXT");
    }

    /// <summary>Files one order against the world's open turn.</summary>
    public (bool Ok, string Reason, bool Replayed) SubmitWorldCommand(
        string worldId, WorldCommand command, DateTimeOffset? utcNow = null)
    {
        var result = SubmitWorldCommands(worldId, new[] { command }, utcNow)[0];
        return (result.Ok, result.Reason, result.Replayed);
    }

    /// <summary>
    /// Files a batch of orders against the world's **open** turn, loading the world exactly once.
    ///
    /// Two rules the single-command shape got wrong and this one fixes: the caller does not choose
    /// the turn (filing into a resolved or future turn would silently corrupt that turn's replay),
    /// and a batch does not re-read the whole graph per order. Results are per command — one stale
    /// order must not throw away the rest of a commander's turn.
    /// </summary>
    public IReadOnlyList<WorldCommandOutcome> SubmitWorldCommands(
        string worldId, IReadOnlyList<WorldCommand> commands, DateTimeOffset? utcNow = null)
    {
        if (commands.Count == 0) return Array.Empty<WorldCommandOutcome>();

        var world = LoadWorldState(worldId);
        if (world is null)
            return commands
                .Select(c => new WorldCommandOutcome(c.CommandId, false, "world.unknown", false))
                .ToList();

        var turn = world.CurrentTurn;
        var now = (utcNow ?? DateTimeOffset.UtcNow).ToString("o");
        var outcomes = new List<WorldCommandOutcome>(commands.Count);

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            foreach (var command in commands)
            {
                var (admitted, reason) = WorldCommandAdmission.Admit(world, command);
                if (!admitted)
                {
                    outcomes.Add(new WorldCommandOutcome(command.CommandId, false, reason, false));
                    continue;
                }

                if (CommandExistsUnlocked(db, tx, worldId, turn, command))
                {
                    outcomes.Add(new WorldCommandOutcome(command.CommandId, true, "replay", true));
                    continue;
                }

                InsertCommandUnlocked(db, tx, worldId, turn, command, now);
                outcomes.Add(new WorldCommandOutcome(command.CommandId, true, "ok", false));
            }

            tx.Commit();
        }

        return outcomes;
    }

    static bool CommandExistsUnlocked(
        SqliteConnection db, SqliteTransaction tx, string worldId, int turn, WorldCommand command)
    {
        using var existing = db.CreateCommand();
        existing.Transaction = tx;
        existing.CommandText = """
            SELECT 1 FROM rpg_world_commands
            WHERE world_id = $w AND turn = $t AND commander_id = $c AND command_id = $id;
            """;
        existing.Parameters.AddWithValue("$w", worldId);
        existing.Parameters.AddWithValue("$t", turn);
        existing.Parameters.AddWithValue("$c", command.CommanderId);
        existing.Parameters.AddWithValue("$id", command.CommandId);
        return existing.ExecuteScalar() != null;
    }

    static void InsertCommandUnlocked(
        SqliteConnection db, SqliteTransaction tx, string worldId, int turn, WorldCommand command,
        string now, string? reason = null)
    {
        long seq;
        using (var next = db.CreateCommand())
        {
            next.Transaction = tx;
            next.CommandText = """
                SELECT COALESCE(MAX(seq), -1) + 1 FROM rpg_world_commands
                WHERE world_id = $w AND turn = $t AND commander_id = $c;
                """;
            next.Parameters.AddWithValue("$w", worldId);
            next.Parameters.AddWithValue("$t", turn);
            next.Parameters.AddWithValue("$c", command.CommanderId);
            seq = Convert.ToInt64(next.ExecuteScalar());
        }

        using var ins = db.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO rpg_world_commands (world_id, turn, commander_id, command_id, seq,
                kind, payload_json, submitted_utc, reason)
            VALUES ($w, $t, $c, $id, $seq, $kind, $payload, $now, $reason);
            """;
        ins.Parameters.AddWithValue("$w", worldId);
        ins.Parameters.AddWithValue("$t", turn);
        ins.Parameters.AddWithValue("$c", command.CommanderId);
        ins.Parameters.AddWithValue("$id", command.CommandId);
        ins.Parameters.AddWithValue("$seq", seq);
        ins.Parameters.AddWithValue("$kind", command.Kind);
        ins.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(new CommandPayload(
            command.EntityId, command.SectorId, command.SlotIndex, command.LanePath, command.Stance)));
        ins.Parameters.AddWithValue("$now", now);
        // Bounded at the boundary like every other free-text field: an audit string is not worth
        // failing a turn over, and an unbounded one is a row nobody budgeted for.
        ins.Parameters.AddWithValue("$reason", reason is null
            ? DBNull.Value
            : reason.Length <= MaxCommandReasonLength ? reason : reason[..MaxCommandReasonLength]);
        ins.ExecuteNonQuery();
    }

    /// <summary>How much of an AI's reasoning is kept. Long enough to be useful, short enough to bound.</summary>
    public const int MaxCommandReasonLength = 200;

    /// <summary>
    /// Every faction with a policy takes its turn (spec-ai-commander.md §The commander loop).
    ///
    /// Each one is handed <see cref="BelievedWorldView"/> — its own fog, on the same terms the
    /// player plays under — files whatever it decides, and commits. Factions are walked in ordinal
    /// order and each gets its own seed stream, so adding one never shifts another's rolls.
    ///
    /// Nothing here is wrapped in a try. A policy is pure integer arithmetic over validated data; if
    /// it throws, the commit's transaction rolls back and the world is untouched, which is a visible
    /// bug rather than a faction that quietly stopped playing.
    /// </summary>
    static void FillAiCommandersUnlocked(
        SqliteConnection db, SqliteTransaction tx, string worldId, int turn, WorldState world,
        ulong worldSeed, string now, Func<string, IFactionPolicy>? policies)
    {
        var resolve = policies ?? FactionPolicies.Resolve;
        var committed = ReadCommittersUnlocked(db, tx, worldId, turn);

        foreach (var faction in world.Factions.OrderBy(f => f.FactionId, StringComparer.Ordinal))
        {
            if (faction.PolicyId is not { } policyId) continue;                  // a person plays this one
            if (committed.Contains(faction.FactionId)) continue;                 // already ended its turn

            // Orders already filed speak for a faction as loudly as a commit does. Without this the
            // escape hatch does not work: the *first* commit of a turn fills every AI faction that
            // has not committed yet, so a scenario scripting two of them has its second one filled
            // over by whichever commit happened to land first. Found by dumping a scenario's command
            // log and finding orders in it that nobody had written.
            if (HasCommandsUnlocked(db, tx, worldId, turn, faction.FactionId)) continue;

            var view = new BelievedWorldView(world, faction.FactionId);
            var seed = SeededRng.DeriveStream(worldSeed, $"ai:{faction.FactionId}:{turn}").NextULong();
            var orders = resolve(policyId).Decide(view, seed);

            // One order per entity, and no more orders than the world has entities. The spec makes
            // this the AI's bound the way MaxCommandsPerSubmit bounds a client — stated but, until
            // it is checked here, worth nothing: a policy with a runaway loop would fill the command
            // table from inside a write transaction holding the store's global lock.
            if (orders.Count > world.Entities.Count + 1)
                throw new InvalidOperationException(
                    $"Policy '{policyId}' filed {orders.Count} orders for '{faction.FactionId}'; " +
                    "at most one per entity is allowed.");

            var subjects = new HashSet<string>(StringComparer.Ordinal);

            foreach (var order in orders)
            {
                // A policy files as the faction whose eyes it was given, full stop. Admission checks
                // that a commander exists and owns the entity it names, so without this a policy
                // could file a *legal* order on another faction's behalf — orders that faction never
                // chose, under its name, and it would still be waiting at the barrier.
                if (!string.Equals(order.Command.CommanderId, faction.FactionId, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Policy '{policyId}' filed for '{order.Command.CommanderId}' " +
                        $"while acting as '{faction.FactionId}'.");

                if (order.Command.EntityId is { } subject && !subjects.Add(subject))
                    throw new InvalidOperationException(
                        $"Policy '{policyId}' gave '{subject}' two orders in turn {turn}.");

                // Admission is the same gate a person's order passes. A policy that fails it is a
                // bug in the policy, and a silent `continue` here would hide it forever: the faction
                // would commit every turn having done nothing, which looks exactly like standing fast.
                var (admitted, why) = WorldCommandAdmission.Admit(world, order.Command);
                if (!admitted)
                    throw new InvalidOperationException(
                        $"Policy '{policyId}' filed an inadmissible order " +
                        $"'{order.Command.CommandId}' ({order.Command.Kind}): {why}.");

                if (CommandExistsUnlocked(db, tx, worldId, turn, order.Command)) continue;

                InsertCommandUnlocked(db, tx, worldId, turn, order.Command, now, order.Reason);
            }

            MarkCommittedUnlocked(db, tx, worldId, turn, faction.FactionId, now);
        }
    }

    /// <summary>Whether this commander has already filed anything for the turn.</summary>
    static bool HasCommandsUnlocked(
        SqliteConnection db, SqliteTransaction tx, string worldId, int turn, string commanderId)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "SELECT 1 FROM rpg_world_commands WHERE world_id = $w AND turn = $t AND commander_id = $c LIMIT 1;";
        cmd.Parameters.AddWithValue("$w", worldId);
        cmd.Parameters.AddWithValue("$t", turn);
        cmd.Parameters.AddWithValue("$c", commanderId);
        return cmd.ExecuteScalar() != null;
    }

    static void MarkCommittedUnlocked(
        SqliteConnection db, SqliteTransaction tx, string worldId, int turn, string commanderId, string now)
    {
        using var commit = db.CreateCommand();
        commit.Transaction = tx;
        commit.CommandText = """
            INSERT OR IGNORE INTO rpg_world_turn_commits (world_id, turn, commander_id, committed_utc)
            VALUES ($w, $t, $c, $now);
            """;
        commit.Parameters.AddWithValue("$w", worldId);
        commit.Parameters.AddWithValue("$t", turn);
        commit.Parameters.AddWithValue("$c", commanderId);
        commit.Parameters.AddWithValue("$now", now);
        commit.ExecuteNonQuery();
    }

    /// <summary>
    /// A turn's orders with the reasoning behind them — what the turn report shows so a player can
    /// tell an AI's mistake from a bug. Commands are never trimmed, so neither is this.
    /// </summary>
    public IReadOnlyList<LoggedWorldCommand> ListLoggedWorldCommands(string worldId, int turn)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT commander_id, command_id, kind, payload_json, reason
                FROM rpg_world_commands
                WHERE world_id = $w AND turn = $t
                ORDER BY commander_id, seq;
                """;
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$t", turn);

            var list = new List<LoggedWorldCommand>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new LoggedWorldCommand(
                    ReadCommandRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)),
                    r.IsDBNull(4) ? null : r.GetString(4)));

            return list;
        }
    }

    /// <summary>
    /// Every order filed for a turn, in stable (commander, submission) order — the engine's input,
    /// and the reason a replay reproduces a turn exactly.
    /// </summary>
    public IReadOnlyList<WorldCommand> ListWorldCommands(string worldId, int turn)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT commander_id, command_id, kind, payload_json
                FROM rpg_world_commands
                WHERE world_id = $w AND turn = $t
                ORDER BY commander_id, seq;
                """;
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$t", turn);

            var list = new List<WorldCommand>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(ReadCommandRow(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
            }

            return list;
        }
    }

    /// <summary>
    /// Every optional field a command can carry. Adding one to <see cref="WorldCommand"/> and
    /// forgetting it here loses it in the round trip and the order comes back malformed — which is
    /// exactly how `stance` was found missing.
    /// </summary>
    sealed record CommandPayload(
        string? EntityId, string? SectorId, int? SlotIndex, IReadOnlyList<string>? LanePath,
        string? Stance = null);

    /// <summary>Reports are kept for the most recent turns; older ones are re-derived on demand.</summary>
    public const int ReportHotTail = 50;

    /// <summary>
    /// Marks one commander committed, and — when the barrier releases — resolves the turn in a
    /// single transaction: step the engine, replace the world graph, append the turn log, advance
    /// the turn counter.
    ///
    /// <paramref name="expectedTurn"/> is the turn the caller means to end, and it is required
    /// rather than optional. Once an AI commander commits automatically (`ai-commander`), *any*
    /// commit can be the one that releases the barrier — so a retried request would read the new
    /// current turn, commit that instead, and silently resolve a second turn the player never
    /// played. Naming the turn makes a retry a refusal.
    ///
    /// The world is loaded **inside** the lock for the same reason: a pre-lock read could be
    /// resolved out from under this call, leaving it committing against a world that no longer
    /// exists.
    /// </summary>
    public WorldTurnCommitResult CommitWorldTurn(
        string worldId, string commanderId, int expectedTurn, DateTimeOffset? utcNow = null,
        Func<string, IFactionPolicy>? policies = null)
    {
        var now = (utcNow ?? DateTimeOffset.UtcNow).ToString("o");

        lock (_gate)
        {
            var world = LoadWorldState(worldId);
            if (world is null) return new WorldTurnCommitResult(false, "world.unknown", false, null);
            if (world.Factions.All(f => !string.Equals(f.FactionId, commanderId, StringComparison.Ordinal)))
                return new WorldTurnCommitResult(false, "commander.unknown", false, null);

            var header = GetWorldHeader(worldId)!;
            var turn = world.CurrentTurn;

            // Checked after "who are you", so a stranger cannot learn which turn is open, and
            // refused in both directions: the question is "is this the turn you were looking at",
            // not "is this in the past".
            if (expectedTurn != turn)
                return new WorldTurnCommitResult(false, "turn.stale", false, null);

            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            MarkCommittedUnlocked(db, tx, worldId, turn, commanderId, now);

            // Every commander that is not a person now takes its turn, before the barrier is read.
            // This is the only place it can happen: the barrier is here, so filling anywhere else
            // would leave the caller told "waiting" for a turn that in fact resolved — and would
            // leave every non-HTTP caller unable to advance at all.
            FillAiCommandersUnlocked(db, tx, worldId, turn, world, header.Seed, now, policies);

            var committed = ReadCommittersUnlocked(db, tx, worldId, turn);
            var commanders = world.Factions.Select(f => f.FactionId).ToList();
            if (!new WaitForAllCommitted().ShouldFire(commanders, committed))
            {
                tx.Commit();
                return new WorldTurnCommitResult(true, "waiting", false, null);
            }

            // Everyone is in: resolve. The seed is per world; each turn derives its own stream so
            // one turn's rolls never shift another's.
            var commands = ListWorldCommandsUnlocked(db, tx, worldId, turn);
            var result = TurnEngine.Step(world, commands, header.Seed);

            ClearWorldGraphUnlocked(db, tx, worldId);
            WriteWorldGraphUnlocked(db, tx, result.World);

            using (var log = db.CreateCommand())
            {
                log.Transaction = tx;
                log.CommandText = """
                    INSERT OR REPLACE INTO rpg_world_turn_log
                        (world_id, turn, state_hash, engine_version, ruleset_version, seed, committed_utc, report_json)
                    VALUES ($w, $t, $hash, $ev, $rv, $seed, $now, $report);
                    """;
                log.Parameters.AddWithValue("$w", worldId);
                log.Parameters.AddWithValue("$t", turn);
                log.Parameters.AddWithValue("$hash", result.StateHash);
                log.Parameters.AddWithValue("$ev", TurnEngine.EngineVersion);
                log.Parameters.AddWithValue("$rv", TurnEngine.RulesetVersion);
                log.Parameters.AddWithValue("$seed", header.Seed.ToString());
                log.Parameters.AddWithValue("$now", now);
                log.Parameters.AddWithValue("$report", JsonSerializer.Serialize(result.Report.Entries));
                log.ExecuteNonQuery();
            }

            using (var advance = db.CreateCommand())
            {
                advance.Transaction = tx;
                advance.CommandText = """
                    UPDATE rpg_worlds
                    SET current_turn = $next, last_advanced_utc = $now, revision = revision + 1
                    WHERE world_id = $w;
                    """;
                advance.Parameters.AddWithValue("$w", worldId);
                advance.Parameters.AddWithValue("$next", result.World.CurrentTurn);
                advance.Parameters.AddWithValue("$now", now);
                advance.ExecuteNonQuery();
            }

            tx.Commit();
            TrimWorldTurnReportsUnlocked(db, worldId, ReportHotTail);
            return new WorldTurnCommitResult(true, "advanced", true, result.StateHash);
        }
    }

    public WorldTurnLogRow? GetWorldTurnLog(string worldId, int turn)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT turn, state_hash, engine_version, ruleset_version, seed, committed_utc, report_json
                FROM rpg_world_turn_log WHERE world_id = $w AND turn = $t;
                """;
            cmd.Parameters.AddWithValue("$w", worldId);
            cmd.Parameters.AddWithValue("$t", turn);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new WorldTurnLogRow(
                r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3),
                ulong.Parse(r.GetString(4)), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6));
        }
    }

    /// <summary>
    /// A turn's report: served from the hot tail when it is still stored, and otherwise **re-derived**
    /// by replaying the world from turn zero with its recorded command log. The engine is
    /// deterministic, so the re-derived report is the same one that was written — which is why the
    /// log does not have to keep every report forever.
    ///
    /// Re-derivation refuses across a version change rather than fabricating a report the current
    /// engine would not have produced.
    /// </summary>
    public TurnReport? GetWorldTurnReport(string worldId, int turn)
    {
        var log = GetWorldTurnLog(worldId, turn);
        if (log is null) return null;

        if (log.ReportJson is { } json)
        {
            var entries = JsonSerializer.Deserialize<List<TurnReportEntry>>(json) ?? new List<TurnReportEntry>();
            return TurnReport.FromEntries(entries);
        }

        if (log.EngineVersion != TurnEngine.EngineVersion || log.RulesetVersion != TurnEngine.RulesetVersion)
            return null;

        var header = GetWorldHeader(worldId);
        if (header is null) return null;

        var world = WorldTemplateCatalog.Build(header.TemplateId, header.Seed, worldId);
        TurnReport? replayed = null;
        for (var t = 0; t <= turn; t++)
        {
            var result = TurnEngine.Step(world, ListWorldCommands(worldId, t), header.Seed);
            world = result.World;
            replayed = result.Report;
        }

        return replayed;
    }

    /// <summary>Drops report bodies older than the hot tail. Hashes and versions always stay.</summary>
    public void TrimWorldTurnReports(string worldId, int keepLast)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            TrimWorldTurnReportsUnlocked(db, worldId, keepLast);
        }
    }

    static void TrimWorldTurnReportsUnlocked(SqliteConnection db, string worldId, int keepLast)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE rpg_world_turn_log SET report_json = NULL
            WHERE world_id = $w AND report_json IS NOT NULL AND turn <= (
              SELECT COALESCE(MAX(turn), -1) - $keep FROM rpg_world_turn_log WHERE world_id = $w
            );
            """;
        cmd.Parameters.AddWithValue("$w", worldId);
        cmd.Parameters.AddWithValue("$keep", keepLast);
        cmd.ExecuteNonQuery();
    }

    public WorldHeaderRow? GetWorldHeader(string worldId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadWorldHeaderUnlocked(db, worldId);
        }
    }

    /// <summary>
    /// One stored row back into a command. Shared by both listers: the payload's shape is the thing
    /// most likely to drift, and it should drift in exactly one place.
    /// </summary>
    static WorldCommand ReadCommandRow(string commanderId, string commandId, string kind, string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<CommandPayload>(payloadJson)
                      ?? new CommandPayload(null, null, null, Array.Empty<string>(), null);

        return new WorldCommand
        {
            CommanderId = commanderId,
            CommandId = commandId,
            Kind = kind,
            EntityId = payload.EntityId,
            SectorId = payload.SectorId,
            SlotIndex = payload.SlotIndex,
            Stance = payload.Stance,
            LanePath = payload.LanePath ?? Array.Empty<string>()
        };
    }

    static List<string> ReadCommittersUnlocked(
        SqliteConnection db, SqliteTransaction tx, string worldId, int turn)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT commander_id FROM rpg_world_turn_commits WHERE world_id = $w AND turn = $t;";
        cmd.Parameters.AddWithValue("$w", worldId);
        cmd.Parameters.AddWithValue("$t", turn);
        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    static List<WorldCommand> ListWorldCommandsUnlocked(
        SqliteConnection db, SqliteTransaction tx, string worldId, int turn)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT commander_id, command_id, kind, payload_json
            FROM rpg_world_commands
            WHERE world_id = $w AND turn = $t
            ORDER BY commander_id, seq;
            """;
        cmd.Parameters.AddWithValue("$w", worldId);
        cmd.Parameters.AddWithValue("$t", turn);

        var list = new List<WorldCommand>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var payload = JsonSerializer.Deserialize<CommandPayload>(r.GetString(3))
                          ?? new CommandPayload(null, null, null, Array.Empty<string>(), null);
            list.Add(new WorldCommand
            {
                CommanderId = r.GetString(0),
                CommandId = r.GetString(1),
                Kind = r.GetString(2),
                EntityId = payload.EntityId,
                SectorId = payload.SectorId,
                SlotIndex = payload.SlotIndex,
                Stance = payload.Stance,
                LanePath = payload.LanePath ?? Array.Empty<string>()
            });
        }

        return list;
    }
}

/// <summary>An order as the log holds it, with the reasoning an AI attached to it.</summary>
public sealed record LoggedWorldCommand(WorldCommand Command, string? Reason);

/// <summary>Outcome of a commit: did it land, and did it release the turn?</summary>
public sealed record WorldTurnCommitResult(bool Ok, string Reason, bool Advanced, string? StateHash);

/// <summary>A turn's durable record. `ReportJson` is null once the body has been trimmed.</summary>
public sealed record WorldTurnLogRow(
    int Turn, string StateHash, int EngineVersion, int RulesetVersion,
    ulong Seed, string CommittedUtc, string? ReportJson);

/// <summary>Per-command result of a submission — a batch never fails as a whole.</summary>
public sealed record WorldCommandOutcome(string CommandId, bool Ok, string Reason, bool Replayed);
