using System.Text.Json;
using FusionRpg.Core.World;
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
        SqliteConnection db, SqliteTransaction tx, string worldId, int turn, WorldCommand command, string now)
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
                kind, payload_json, submitted_utc)
            VALUES ($w, $t, $c, $id, $seq, $kind, $payload, $now);
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
        ins.ExecuteNonQuery();
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
    /// the turn counter. A duplicate commit is a no-op, so a retried request cannot double-advance
    /// the world.
    /// </summary>
    public WorldTurnCommitResult CommitWorldTurn(string worldId, string commanderId, DateTimeOffset? utcNow = null)
    {
        var world = LoadWorldState(worldId);
        if (world is null) return new WorldTurnCommitResult(false, "world.unknown", false, null);
        if (world.Factions.All(f => !string.Equals(f.FactionId, commanderId, StringComparison.Ordinal)))
            return new WorldTurnCommitResult(false, "commander.unknown", false, null);

        var header = GetWorldHeader(worldId)!;
        var turn = world.CurrentTurn;
        var now = (utcNow ?? DateTimeOffset.UtcNow).ToString("o");

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            using (var commit = db.CreateCommand())
            {
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

/// <summary>Outcome of a commit: did it land, and did it release the turn?</summary>
public sealed record WorldTurnCommitResult(bool Ok, string Reason, bool Advanced, string? StateHash);

/// <summary>A turn's durable record. `ReportJson` is null once the body has been trimmed.</summary>
public sealed record WorldTurnLogRow(
    int Turn, string StateHash, int EngineVersion, int RulesetVersion,
    ulong Seed, string CommittedUtc, string? ReportJson);

/// <summary>Per-command result of a submission — a batch never fails as a whole.</summary>
public sealed record WorldCommandOutcome(string CommandId, bool Ok, string Reason, bool Replayed);
