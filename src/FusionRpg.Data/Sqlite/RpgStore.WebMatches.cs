using FusionRpg.Contracts;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One row of rpg_web_match_log — the durable idempotency anchor for a web match.</summary>
public sealed record WebMatchLogEntry(
    long Id, long PlayerId, string CorrelationId, string MatchKey,
    string SetupJson, ulong Seed, int EngineVersion, int RulesetVersion, int RngAlgoVersion,
    long? RunId, string T, string? EnvironmentStamp = null, string? SweepRefused = null);

public sealed partial class RpgStore
{
    /// <summary>
    /// Appends the match log row BEFORE any ingest (spec-match-source-core.md §WebMatchService).
    /// Per-player correlation replay: an existing row is returned untouched — the caller compares
    /// the stored request against its own and never re-resolves on a mismatch.
    /// </summary>
    public (bool Created, WebMatchLogEntry Entry) AppendWebMatchLog(
        long playerId, string correlationId, string matchKey, string setupJson, ulong seed,
        int engineVersion, int rulesetVersion, int rngAlgoVersion, string? environmentStamp = null)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("correlationId is required.");
        if (string.IsNullOrWhiteSpace(matchKey)) throw new ArgumentException("matchKey is required.");
        var corr = correlationId.Trim();
        var key = matchKey.Trim();

        lock (_gate)
        {
            using var db = OpenUnlocked();
            var existing = ReadWebMatchLogUnlocked(db, playerId, corr);
            if (existing != null)
                return (false, existing);

            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO rpg_web_match_log(
                  player_id, correlation_id, match_key, setup_json, seed,
                  engine_version, ruleset_version, rng_algo_version, environment_stamp, t)
                VALUES($p,$c,$k,$s,$seed,$ev,$rv,$av,$env,$t);
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$c", corr);
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$s", setupJson ?? "{}");
            cmd.Parameters.AddWithValue("$seed", seed.ToString());
            cmd.Parameters.AddWithValue("$ev", engineVersion);
            cmd.Parameters.AddWithValue("$rv", rulesetVersion);
            cmd.Parameters.AddWithValue("$av", rngAlgoVersion);
            cmd.Parameters.AddWithValue("$env", (object?)environmentStamp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
            return (true, ReadWebMatchLogUnlocked(db, playerId, corr)!);
        }
    }

    /// <summary>
    /// Terminal state for a log row the sweep will never heal (version or platform mismatch).
    /// The row is KEPT for forensics — it just stops being re-listed. Reason is stamped with
    /// the time so an operator can tell a runtime upgrade from a moved database.
    /// </summary>
    public void MarkWebMatchSweepRefused(long id, string reason)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "UPDATE rpg_web_match_log SET sweep_refused=$r WHERE id=$i AND sweep_refused IS NULL;";
            cmd.Parameters.AddWithValue("$i", id);
            cmd.Parameters.AddWithValue("$r", DateTime.UtcNow.ToString("o") + " " + (reason ?? "unspecified"));
            cmd.ExecuteNonQuery();
        }
    }

    public WebMatchLogEntry? TryGetWebMatchLog(long playerId, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return null;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadWebMatchLogUnlocked(db, playerId, correlationId.Trim());
        }
    }

    /// <summary>Boot sweep: logged matches whose ingest never landed (crash window) — resolution
    /// is deterministic from setup_json + seed, so the service re-ingests them on start.
    ///
    /// Rows already refused (<see cref="MarkWebMatchSweepRefused"/>) are excluded. Without that
    /// they would be re-listed every boot forever, and because this is ORDER BY id ASC LIMIT n,
    /// enough of them at the low end would push every newer row out of the window — crash
    /// recovery would silently stop working while still reporting a clean sweep.</summary>
    public List<WebMatchLogEntry> ListUnresolvedWebMatches(int limit = 100)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = SelectLog +
                " WHERE run_id IS NULL AND sweep_refused IS NULL ORDER BY id ASC LIMIT $l;";
            cmd.Parameters.AddWithValue("$l", limit);
            using var r = cmd.ExecuteReader();
            var list = new List<WebMatchLogEntry>();
            while (r.Read())
                list.Add(MapLog(r));
            return list;
        }
    }

    /// <summary>
    /// The DEDICATED web report ingest: one transaction, explicit player, never the shared
    /// channel — a rollback here can never drop interleaved live PvZ events. Links the log row
    /// to the created run in the same transaction (the crash-window invariant).
    /// </summary>
    public EventInsertNotify InsertWebMatchEvents(long playerId, string matchKey, IReadOnlyList<EventEnvelope> batch)
    {
        if (string.IsNullOrWhiteSpace(matchKey)) throw new ArgumentException("matchKey is required.");
        if (batch.Count == 0)
            return new EventInsertNotify(Array.Empty<long>(), Array.Empty<RpgProgressionDirty>(), Array.Empty<long>());

        lock (_gate)
        {
            _activityNotifyBatch = new HashSet<long>();
            _progressionNotifyBatch = new List<RpgProgressionDirty>();
            _closedRunNotifyBatch = new HashSet<long>();
            try
            {
                using var db = OpenUnlocked();
                Exec(db, "BEGIN IMMEDIATE;");
                try
                {
                    foreach (var e in batch)
                        InsertOneUnlocked(db, e, explicitPlayerId: playerId);

                    using (var link = db.CreateCommand())
                    {
                        link.CommandText = """
                            UPDATE rpg_web_match_log
                            SET run_id=(SELECT id FROM runs WHERE match_key=$k ORDER BY id DESC LIMIT 1)
                            WHERE match_key=$k AND run_id IS NULL;
                            """;
                        link.Parameters.AddWithValue("$k", matchKey.Trim());
                        link.ExecuteNonQuery();
                    }

                    Exec(db, "COMMIT;");
                }
                catch
                {
                    try { Exec(db, "ROLLBACK;"); } catch { /* ignore */ }
                    _killEarnMemo.Clear(); // in-memory memo must not outrun the rolled-back ledger
                    throw;
                }

                return new EventInsertNotify(
                    _activityNotifyBatch.ToList(),
                    _progressionNotifyBatch.ToList(),
                    _closedRunNotifyBatch.ToList());
            }
            finally
            {
                _activityNotifyBatch = null;
                _progressionNotifyBatch = null;
                _closedRunNotifyBatch = null;
            }
        }
    }

    const string SelectLog = """
        SELECT id, player_id, correlation_id, match_key, setup_json, seed,
               engine_version, ruleset_version, rng_algo_version, run_id, t, environment_stamp,
               sweep_refused
        FROM rpg_web_match_log
        """;

    WebMatchLogEntry? ReadWebMatchLogUnlocked(SqliteConnection db, long playerId, string correlationId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = SelectLog + " WHERE player_id=$p AND correlation_id=$c;";
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$c", correlationId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapLog(r) : null;
    }

    static WebMatchLogEntry MapLog(SqliteDataReader r) => new(
        r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3),
        r.GetString(4), ParseSeed(Convert.ToString(r.GetValue(5), System.Globalization.CultureInfo.InvariantCulture)),
        r.GetInt32(6), r.GetInt32(7), r.GetInt32(8),
        r.IsDBNull(9) ? null : r.GetInt64(9), r.GetString(10),
        r.IsDBNull(11) ? null : r.GetString(11),
        r.IsDBNull(12) ? null : r.GetString(12));

    /// <summary>Defensive: a hand-edited/foreign row must not take down boot (the sweep lists
    /// rows before its per-entry catch). Unparseable seeds map to 0 — deterministic garbage the
    /// row's owner can inspect, never a startup crash.</summary>
    internal static ulong ParseSeed(string? text) =>
        ulong.TryParse(text, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0UL;
}
