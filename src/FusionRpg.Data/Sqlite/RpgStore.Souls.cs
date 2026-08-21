using FusionRpg.Contracts;
using FusionRpg.Core.Activity;
using FusionRpg.Core.Demons;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public sealed partial class RpgStore
{
    /// <summary>
    /// Kill-earn memo per (player, run) — the store is single-writer under _gate, so this stays
    /// exact. Seeded once per run by an index-only COUNT, then incremented in memory: without it,
    /// every kill re-scanned the lifetime ledger inside the ingest transaction (review C1).
    /// </summary>
    readonly Dictionary<(long PlayerId, long RunId), int> _killEarnMemo = new();

    /// <summary>
    /// Soul earns from a freshly inserted Activity fact — called inside the fact's own transaction
    /// (spec-soul-economy.md). Dedupe key = fact id, so re-ingest can never double-earn.
    /// </summary>
    void ApplySoulEarnFromActivityUnlocked(
        SqliteConnection db, long playerId, long runId, string t, string factKind, string payload, long factId)
    {
        int delta;
        string reason;
        switch (factKind)
        {
            case PvzActivityKinds.ZombieKilled:
            {
                var key = (playerId, runId);
                if (!_killEarnMemo.TryGetValue(key, out var counted))
                {
                    if (_killEarnMemo.Count > 128) _killEarnMemo.Clear(); // stale-run safety valve
                    counted = (int)CountSoulEarnsUnlocked(db, playerId, SoulEarnPolicy.Reasons.Kill, runId);
                    _killEarnMemo[key] = counted;
                }

                // Patron bonus (spec-patron-demon.md): +1 on every 10th earning kill, same 50-soul
                // cap. Gated so the audited unpatroned shape stays byte-identical; the patron
                // check is a PK point lookup, not a scan (review-C1 discipline).
                delta = HasPatronUnlocked(db, playerId)
                    ? Core.Demons.Patron.PatronPolicy.KillEarnWithPatron(counted)
                    : SoulEarnPolicy.KillEarn(counted);
                if (delta > 0)
                    _killEarnMemo[key] = counted + 1;
                reason = SoulEarnPolicy.Reasons.Kill;
                break;
            }
            case PvzActivityKinds.MatchEnded:
            {
                // Same normalization as the runs projection ("win" → "victory", etc.).
                var victory = string.Equals(NormalizeResult(TryString(payload, "result")), "victory", StringComparison.Ordinal);
                if (victory)
                {
                    var priorToday = CountVictoriesOnDayUnlocked(db, playerId, t);
                    delta = SoulEarnPolicy.MatchEndEarn(true, (int)priorToday);
                    reason = SoulEarnPolicy.Reasons.Victory;
                }
                else
                {
                    delta = SoulEarnPolicy.MatchEndEarn(false, 0);
                    reason = SoulEarnPolicy.Reasons.Defeat;
                }

                break;
            }
            default:
                return;
        }

        if (delta <= 0) return;
        AppendSoulLedgerUnlocked(db, playerId, runId, delta, reason, "activity_fact",
            factId.ToString(), factId.ToString(), t);
    }

    long CountSoulEarnsUnlocked(SqliteConnection db, long playerId, string reason, long runId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM rpg_soul_ledger WHERE player_id=$p AND reason=$r AND run_id=$run;";
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$r", reason);
        cmd.Parameters.AddWithValue("$run", runId);
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    long CountVictoriesOnDayUnlocked(SqliteConnection db, long playerId, string t)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM rpg_soul_ledger
            WHERE player_id=$p AND reason=$r AND substr(t,1,10) = substr($t,1,10);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$r", SoulEarnPolicy.Reasons.Victory);
        cmd.Parameters.AddWithValue("$t", t);
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    /// <summary>Append one ledger row + fold it into the balance snapshot. True when newly inserted.</summary>
    bool AppendSoulLedgerUnlocked(
        SqliteConnection db, long playerId, long runId, long delta, string reason,
        string? refKind, string? refId, string dedupeKey, string t)
    {
        long ledgerId;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO rpg_soul_ledger(
                  player_id, run_id, delta, reason, ref_kind, ref_id, dedupe_key, t)
                VALUES($p, $run, $d, $r, $rk, $ri, $dk, $t);
                SELECT CASE WHEN changes() > 0 THEN last_insert_rowid() ELSE 0 END;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$run", runId);
            cmd.Parameters.AddWithValue("$d", delta);
            cmd.Parameters.AddWithValue("$r", reason);
            cmd.Parameters.AddWithValue("$rk", (object?)refKind ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ri", (object?)refId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$dk", dedupeKey);
            cmd.Parameters.AddWithValue("$t", t);
            ledgerId = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        if (ledgerId == 0) return false;

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO rpg_soul_balances(player_id, balance, earned_total, spent_total, through_ledger_id, revision, updated_utc)
                VALUES($p, $d, $earn, $spend, $lid, 1, $t)
                ON CONFLICT(player_id) DO UPDATE SET
                  balance = balance + $d,
                  earned_total = earned_total + $earn,
                  spent_total = spent_total + $spend,
                  through_ledger_id = $lid,
                  revision = revision + 1,
                  updated_utc = $t;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$d", delta);
            cmd.Parameters.AddWithValue("$earn", delta > 0 ? delta : 0);
            cmd.Parameters.AddWithValue("$spend", delta < 0 ? -delta : 0);
            cmd.Parameters.AddWithValue("$lid", ledgerId);
            cmd.Parameters.AddWithValue("$t", t);
            cmd.ExecuteNonQuery();
        }

        // Earn notifications ride PvzActivityUpdated (earns always accompany a fact);
        // spends/awards push SoulsUpdated explicitly from their endpoints.
        return true;
    }

    /// <summary>
    /// Balance ceiling — keeps SQLite integer addition far from 64-bit overflow (which silently
    /// degrades to REAL and permanently corrupts the snapshot). Last line of defense; endpoints clamp too.
    /// </summary>
    public const long MaxSoulAward = 1_000_000_000;

    /// <summary>Positive award outside the activity path (codex discovery, milestones). Idempotent on dedupe.</summary>
    public (bool Inserted, SoulBalanceDto Balance) AwardSouls(long playerId, long delta, string reason, string dedupeKey)
    {
        if (delta <= 0 || delta > MaxSoulAward) throw new ArgumentOutOfRangeException(nameof(delta));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var inserted = AppendSoulLedgerUnlocked(db, playerId, 0, delta, reason, null, null, dedupeKey,
                DateTime.UtcNow.ToString("o"));
            tx.Commit();
            return (inserted, ReadSoulBalanceUnlocked(db, playerId));
        }
    }

    /// <summary>
    /// Atomic spend. Refusals write nothing; a replayed (player, reason, correlationId) that
    /// previously succeeded returns the original success without spending again.
    /// </summary>
    public (bool Ok, string Reason, SoulBalanceDto Balance) TrySpendSouls(
        long playerId, long amount, string reason, string correlationId)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("correlationId required");
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var corr = correlationId.Trim();

            using (var check = db.CreateCommand())
            {
                check.CommandText = "SELECT delta FROM rpg_soul_ledger WHERE player_id=$p AND reason=$r AND dedupe_key=$dk;";
                check.Parameters.AddWithValue("$p", playerId);
                check.Parameters.AddWithValue("$r", reason);
                check.Parameters.AddWithValue("$dk", corr);
                if (check.ExecuteScalar() is long storedDelta)
                {
                    tx.Commit();
                    // Replay must be the SAME request — a different amount under a reused
                    // correlation is a caller bug, not an idempotent retry.
                    return storedDelta == -amount
                        ? (true, "replay", ReadSoulBalanceUnlocked(db, playerId))
                        : (false, "correlation.mismatch", ReadSoulBalanceUnlocked(db, playerId));
                }
            }

            var balance = ReadSoulBalanceUnlocked(db, playerId);
            if (balance.Balance < amount)
            {
                tx.Rollback();
                return (false, "souls.insufficient", balance);
            }

            AppendSoulLedgerUnlocked(db, playerId, 0, -amount, reason, "spend", corr, corr,
                DateTime.UtcNow.ToString("o"));
            tx.Commit();
            return (true, "", ReadSoulBalanceUnlocked(db, playerId));
        }
    }

    public SoulBalanceDto GetSoulBalance(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadSoulBalanceUnlocked(db, playerId);
        }
    }

    public SoulLedgerDto ListSoulLedger(long playerId, int limit = 100, long afterId = 0)
    {
        limit = Math.Clamp(limit, 1, 500);
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var items = new List<SoulLedgerEntryDto>();
            using var cmd = db.CreateCommand();
            // Descending pagination: afterId is the smallest id already seen — pass it back to get
            // the next OLDER page (id > $after with DESC order could never page backwards).
            cmd.CommandText = """
                SELECT id, run_id, delta, reason, ref_kind, ref_id, t
                FROM rpg_soul_ledger
                WHERE player_id=$p AND ($after <= 0 OR id < $after)
                ORDER BY id DESC LIMIT $lim;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$after", afterId);
            cmd.Parameters.AddWithValue("$lim", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                items.Add(new SoulLedgerEntryDto
                {
                    Id = r.GetInt64(0),
                    RunId = r.GetInt64(1),
                    Delta = r.GetInt64(2),
                    Reason = r.GetString(3),
                    RefKind = r.IsDBNull(4) ? null : r.GetString(4),
                    RefId = r.IsDBNull(5) ? null : r.GetString(5),
                    T = r.GetString(6)
                });
            }

            return new SoulLedgerDto { PlayerId = playerId, Items = items };
        }
    }

    SoulBalanceDto ReadSoulBalanceUnlocked(SqliteConnection db, long playerId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT balance, earned_total, spent_total, revision, updated_utc
            FROM rpg_soul_balances WHERE player_id=$p;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return new SoulBalanceDto { PlayerId = playerId };
        return new SoulBalanceDto
        {
            PlayerId = playerId,
            Balance = r.GetInt64(0),
            EarnedTotal = r.GetInt64(1),
            SpentTotal = r.GetInt64(2),
            Revision = r.GetInt64(3),
            UpdatedUtc = r.GetString(4)
        };
    }
}
