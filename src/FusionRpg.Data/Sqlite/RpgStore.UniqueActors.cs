using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Progression;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public sealed partial class RpgStore
{
    public UniqueActorDto CreateUniqueActor(long playerId, string side, int typeId)
    {
        var s = NormalizeUniqueSide(side);
        if (typeId < 0) throw new ArgumentOutOfRangeException(nameof(typeId));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null)
                throw new InvalidOperationException("player not found");
            var now = DateTime.UtcNow.ToString("o");
            var id = Guid.NewGuid().ToString("N");
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO rpg_unique_actors(
                      instance_id, player_id, side, type_id, phase, level, xp,
                      match_key, last_ptr, deploy_correlation_id, revision, created_utc, updated_utc)
                    VALUES($id, $pid, $side, $type, $phase, 1, 0, NULL, NULL, NULL, 0, $now, $now);
                    """;
                cmd.Parameters.AddWithValue("$id", id);
                cmd.Parameters.AddWithValue("$pid", playerId);
                cmd.Parameters.AddWithValue("$side", s);
                cmd.Parameters.AddWithValue("$type", typeId);
                cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Roster);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.ExecuteNonQuery();
            }
            return ReadUniqueActorUnlocked(db, id)!;
        }
    }

    public UniqueActorDto? GetUniqueActor(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadUniqueActorUnlocked(db, instanceId.Trim());
        }
    }

    public UniqueActorListDto ListUniqueActors(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var items = new List<UniqueActorDto>();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT instance_id, player_id, side, type_id, phase, level, xp,
                       match_key, last_ptr, deploy_correlation_id, revision, created_utc, updated_utc
                FROM rpg_unique_actors WHERE player_id = $pid
                ORDER BY created_utc ASC;
                """;
            cmd.Parameters.AddWithValue("$pid", playerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                items.Add(MapUniqueActor(r));
            return new UniqueActorListDto { PlayerId = playerId, Items = items };
        }
    }

    /// <summary>Roster → Deploying. Same correlationId while Deploying is idempotent (ok, queued=false).</summary>
    public (bool Ok, string Reason, UniqueActorDto? Actor, bool Queued) TryBeginUniqueDeploy(
        string instanceId, string correlationId, string? matchKey = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(correlationId))
            return (false, "bad_args", null, false);
        var id = instanceId.Trim();
        var corr = correlationId.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var row = ReadUniqueActorUnlocked(db, id);
            if (row is null) return (false, "not_found", null, false);

            if (string.Equals(row.Phase, UniqueActorPhases.Deploying, StringComparison.Ordinal) &&
                string.Equals(row.DeployCorrelationId, corr, StringComparison.Ordinal))
                return (true, "", row, false);

            // Cross-mode soft-lock (spec-expeditions.md): a specimen on an active expedition
            // cannot be PvZ-deployed; the mirror check lives in DispatchExpedition.
            if (HasActiveExpeditionMembershipUnlocked(db, id))
                return (false, "expedition.locked", row, false);

            if (!string.Equals(row.Phase, UniqueActorPhases.Roster, StringComparison.Ordinal))
                return (false, "phase." + row.Phase.ToLowerInvariant(), row, false);

            // Contract gate — demons only. A unique actor without a demon profile predates this
            // module entirely and deploys exactly as it always did.
            if (ReadDemonProfileUnlocked(db, id) != null)
            {
                var contract = ContractViewUnlocked(db, row.PlayerId, id);
                if (!contract.Bound) return (false, "contract.unbound", row, false);
                if (!contract.Deployable) return (false, "contract.insubordinate", row, false);
            }

            var now = DateTime.UtcNow.ToString("o");
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    UPDATE rpg_unique_actors SET
                      phase = $phase,
                      deploy_correlation_id = $corr,
                      match_key = $mk,
                      last_ptr = NULL,
                      revision = revision + 1,
                      updated_utc = $now
                    WHERE instance_id = $id;
                    """;
                cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Deploying);
                cmd.Parameters.AddWithValue("$corr", corr);
                cmd.Parameters.AddWithValue("$mk", (object?)NullIfEmpty(matchKey) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            return (true, "", ReadUniqueActorUnlocked(db, id), true);
        }
    }

    /// <summary>Deploying → ActiveBound on spawn ack (by correlationId).</summary>
    public (bool Ok, string Reason, UniqueActorDto? Actor) TryAckUniqueSpawn(
        string correlationId, string ptr, string? matchKey = null)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(ptr))
            return (false, "bad_args", null);
        var corr = correlationId.Trim();
        var p = ptr.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var row = FindUniqueByCorrelationUnlocked(db, corr);
            if (row is null) return (false, "not_found", null);
            if (!string.Equals(row.Phase, UniqueActorPhases.Deploying, StringComparison.Ordinal))
                return (false, "phase." + row.Phase.ToLowerInvariant(), row);

            var now = DateTime.UtcNow.ToString("o");
            var mk = NullIfEmpty(matchKey) ?? row.MatchKey;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    UPDATE rpg_unique_actors SET
                      phase = $phase,
                      last_ptr = $ptr,
                      match_key = $mk,
                      revision = revision + 1,
                      updated_utc = $now
                    WHERE instance_id = $id;
                    """;
                cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.ActiveBound);
                cmd.Parameters.AddWithValue("$ptr", p);
                cmd.Parameters.AddWithValue("$mk", (object?)mk ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$id", row.InstanceId);
                cmd.ExecuteNonQuery();
            }
            return (true, "", ReadUniqueActorUnlocked(db, row.InstanceId));
        }
    }

    /// <summary>Deploying → Roster (spawn failed).</summary>
    public (bool Ok, string Reason, UniqueActorDto? Actor) TryFailUniqueDeploy(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return (false, "bad_args", null);
        var id = instanceId.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var row = ReadUniqueActorUnlocked(db, id);
            if (row is null) return (false, "not_found", null);
            if (!string.Equals(row.Phase, UniqueActorPhases.Deploying, StringComparison.Ordinal))
                return (false, "phase." + row.Phase.ToLowerInvariant(), row);
            ClearBindToRosterUnlocked(db, id);
            return (true, "", ReadUniqueActorUnlocked(db, id));
        }
    }

    public (bool Ok, string Reason, UniqueActorDto? Actor) TryRetireUniqueActor(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return (false, "bad_args", null);
        var id = instanceId.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var (ok, reason) = RetireUniqueActorUnlocked(db, id);
            return (ok, reason, ReadUniqueActorUnlocked(db, id));
        }
    }

    /// <summary>The core of <see cref="TryRetireUniqueActor"/>, reusable from a caller that already
    /// holds an open connection (party-dungeon D2.23 — extraction settlement needs this on the SAME
    /// connection/transaction its own writes are in, exactly like <see cref="AwardUniqueActorXpUnlocked"/>
    /// already is for the expedition reward apply). Idempotent on an already-`Retired` row.</summary>
    (bool Ok, string Reason) RetireUniqueActorUnlocked(SqliteConnection db, string instanceId)
    {
        var row = ReadUniqueActorUnlocked(db, instanceId);
        if (row is null) return (false, "not_found");
        if (string.Equals(row.Phase, UniqueActorPhases.Retired, StringComparison.Ordinal))
            return (true, "");
        if (string.Equals(row.Phase, UniqueActorPhases.Deploying, StringComparison.Ordinal) ||
            string.Equals(row.Phase, UniqueActorPhases.Recovering, StringComparison.Ordinal))
            return (false, "phase." + row.Phase.ToLowerInvariant());

        var now = DateTime.UtcNow.ToString("o");
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE rpg_unique_actors SET
              phase = $phase,
              match_key = NULL,
              last_ptr = NULL,
              deploy_correlation_id = NULL,
              revision = revision + 1,
              updated_utc = $now
            WHERE instance_id = $id;
            """;
        cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Retired);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.Parameters.AddWithValue("$id", instanceId);
        cmd.ExecuteNonQuery();
        return (true, "");
    }

    /// <summary>
    /// party-dungeon D2.23 (spec-delve-attrition.md §7) — the FIRST real, persisted `Recovering`
    /// (unlike W4's lawn-death pass-through, `RecoverToRosterUnlocked`, which never actually writes
    /// this phase). Same-connection, called from `CloseDelve`'s own open transaction. Refuses a
    /// `Retired`/`Deploying` row for the identical reason <see cref="RetireUniqueActorUnlocked"/>
    /// refuses those two — neither state is a live roster member a delve wound can touch.
    /// </summary>
    (bool Ok, string Reason) BeginUniqueRecoveryUnlocked(
        SqliteConnection db, long playerId, string instanceId, int recoveryDelvesLeft, long woundedDelveId, int thetaRun)
    {
        if (recoveryDelvesLeft <= 0)
            throw new ArgumentOutOfRangeException(nameof(recoveryDelvesLeft), recoveryDelvesLeft, "a recovery count is never zero or negative");

        var row = ReadUniqueActorUnlocked(db, instanceId);
        if (row is null) return (false, "not_found");
        if (string.Equals(row.Phase, UniqueActorPhases.Retired, StringComparison.Ordinal) ||
            string.Equals(row.Phase, UniqueActorPhases.Deploying, StringComparison.Ordinal))
            return (false, "phase." + row.Phase.ToLowerInvariant());

        var now = DateTime.UtcNow.ToString("o");
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE rpg_unique_actors SET
                  phase = $phase, match_key = NULL, last_ptr = NULL, deploy_correlation_id = NULL,
                  revision = revision + 1, updated_utc = $now
                WHERE instance_id = $id;
                """;
            cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Recovering);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.Parameters.AddWithValue("$id", instanceId);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO rpg_unique_actor_recovery(instance_id, player_id, recovery_delves_left, wounded_delve_id, theta_run)
                VALUES($id, $pid, $left, $delve, $theta)
                ON CONFLICT(instance_id) DO UPDATE SET
                  recovery_delves_left = $left, wounded_delve_id = $delve, theta_run = $theta;
                """;
            cmd.Parameters.AddWithValue("$id", instanceId);
            cmd.Parameters.AddWithValue("$pid", playerId);
            cmd.Parameters.AddWithValue("$left", recoveryDelvesLeft);
            cmd.Parameters.AddWithValue("$delve", woundedDelveId);
            cmd.Parameters.AddWithValue("$theta", thetaRun);
            cmd.ExecuteNonQuery();
        }

        return (true, "");
    }

    /// <summary>
    /// R6 — virtual time, no clock: every `CloseDelve` (any delve, Extracted or Wiped) decrements
    /// EVERY `Recovering` row this player owns by one delve, in the same transaction. A row that
    /// reaches zero flips straight back to `Roster` and its recovery row is deleted in the same
    /// write — "0 flips the demon to Roster in the same write" (spec §7), never a separate sweep.
    /// </summary>
    void DecrementAllRecoveringForPlayerUnlocked(SqliteConnection db, long playerId, string now)
    {
        var ids = new List<(string InstanceId, int Left)>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT instance_id, recovery_delves_left FROM rpg_unique_actor_recovery WHERE player_id = $pid;";
            cmd.Parameters.AddWithValue("$pid", playerId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add((r.GetString(0), r.GetInt32(1)));
        }

        foreach (var (id, left) in ids)
        {
            var next = left - 1;
            if (next > 0)
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = "UPDATE rpg_unique_actor_recovery SET recovery_delves_left = $left WHERE instance_id = $id;";
                cmd.Parameters.AddWithValue("$left", next);
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
                continue;
            }

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM rpg_unique_actor_recovery WHERE instance_id = $id;";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    UPDATE rpg_unique_actors SET phase = $phase, revision = revision + 1, updated_utc = $now
                    WHERE instance_id = $id;
                    """;
                cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Roster);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>The one row of `rpg_unique_actor_recovery` a caller (the recovery-ritual endpoint, a
    /// roster read) needs — null when the actor is not `Recovering` (or was never wounded).</summary>
    public (int RecoveryDelvesLeft, long WoundedDelveId, int ThetaRun)? GetUniqueActorRecovery(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadUniqueActorRecoveryUnlocked(db, instanceId.Trim());
        }
    }

    static (int RecoveryDelvesLeft, long WoundedDelveId, int ThetaRun)? ReadUniqueActorRecoveryUnlocked(SqliteConnection db, string instanceId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT recovery_delves_left, wounded_delve_id, theta_run FROM rpg_unique_actor_recovery WHERE instance_id = $id;";
        cmd.Parameters.AddWithValue("$id", instanceId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetInt32(0), r.GetInt64(1), r.GetInt32(2)) : null;
    }

    /// <summary>
    /// party-dungeon D2.23 (spec-delve-attrition.md §7) — "the priced escape": spends
    /// <c>SoulSinkPolicy.Price(risk.recoveryRitualSouls.{rung}, theta_run, tuning)</c> souls, priced
    /// at the WOUNDING delve's own rung and `theta_run` (never the current delve, if any — the row
    /// remembers both since the wounding delve may since have been archived), clears the recovery
    /// row, and writes `Roster` — one transaction, correlation-idempotent exactly like
    /// <see cref="RpgStore.Contracts.PerformRitual"/> (a retry of the same correlation is a replay,
    /// never a second charge). <paramref name="tuning"/> is an explicit parameter, matching
    /// <see cref="CloseDelve"/>'s own shape in this same module — never a static hub read here.
    /// </summary>
    public (bool Ok, string Reason, UniqueActorDto? Actor) TryPerformRecoveryRitual(
        long playerId, string instanceId, string correlationId,
        FusionRpg.Core.Dungeon.Tuning.DungeonTuning tuning, DateTimeOffset? utcNow = null)
    {
        var id = (instanceId ?? "").Trim();
        var corr = (correlationId ?? "").Trim();
        if (id.Length == 0) return (false, "specimen.missing", null);
        if (corr.Length == 0) return (false, "correlation.missing", null);
        var now = utcNow ?? DateTimeOffset.UtcNow;

        lock (_gate)
        {
            using var db = OpenUnlocked();
            var row = ReadUniqueActorUnlocked(db, id);
            if (row is null || row.PlayerId != playerId) return (false, "not_found", null);

            // Checked BEFORE the phase guard, on purpose: a SUCCESSFUL ritual moves the actor out of
            // Recovering, so a replay of the same correlation arrives with a row that would otherwise
            // fail "must be Recovering" -- the opposite of PerformRitual's own contract-ritual replay,
            // whose state (bound, still ready to re-ritual) never disqualifies itself this way.
            if (HasSoulLedgerEntryUnlocked(db, playerId, SoulEarnPolicy.Reasons.DelveRecoveryRitual, corr))
                return (true, "replay", row);

            if (!string.Equals(row.Phase, UniqueActorPhases.Recovering, StringComparison.Ordinal))
                return (false, "phase." + row.Phase.ToLowerInvariant(), row);

            var recovery = ReadUniqueActorRecoveryUnlocked(db, id);
            if (recovery is null) return (false, "recovery.missing", row); // Recovering with no row is a data defect, not a valid state to price

            var woundedRungId = ReadDelveRungIdUnlocked(db, recovery.Value.WoundedDelveId);
            if (woundedRungId is null || !tuning.RiskRecoveryRitualSouls.TryGetValue(woundedRungId, out var basePriceSouls))
                return (false, "rung.unknown", row);

            using var tx = db.BeginTransaction();
            var price = SoulSinkPolicy.Price(basePriceSouls, recovery.Value.ThetaRun, Core.Power.PowerTuningHub.Tuning);
            if (ReadSoulBalanceUnlocked(db, playerId).Balance < price)
                return (false, "souls.insufficient", row);

            var stamp = now.UtcDateTime.ToString("o");
            if (!AppendSoulLedgerUnlocked(db, playerId, 0, -price, SoulEarnPolicy.Reasons.DelveRecoveryRitual,
                    "unique_actor", id, corr, stamp))
            {
                tx.Commit(); // the correlation already bought this ritual -- a replay, not a second purchase
                return (true, "replay", ReadUniqueActorUnlocked(db, id));
            }

            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM rpg_unique_actor_recovery WHERE instance_id = $id;";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE rpg_unique_actors SET phase = $phase, revision = revision + 1, updated_utc = $now
                    WHERE instance_id = $id;
                    """;
                cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Roster);
                cmd.Parameters.AddWithValue("$now", stamp);
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return (true, "", ReadUniqueActorUnlocked(db, id));
        }
    }

    /// <summary>Peeks `rpg_soul_ledger`'s own `UNIQUE(player_id, reason, dedupe_key)` without
    /// inserting — the read half of the check <see cref="AppendSoulLedgerUnlocked"/>'s `INSERT OR
    /// IGNORE` makes atomically for a caller that only finds out about the collision from inside its
    /// own state-mutating write.</summary>
    static bool HasSoulLedgerEntryUnlocked(SqliteConnection db, long playerId, string reason, string dedupeKey)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM rpg_soul_ledger WHERE player_id = $p AND reason = $r AND dedupe_key = $dk LIMIT 1;";
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$r", reason);
        cmd.Parameters.AddWithValue("$dk", dedupeKey);
        return cmd.ExecuteScalar() is not null and not DBNull;
    }

    static string? ReadDelveRungIdUnlocked(SqliteConnection db, long delveId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT rung_id FROM rpg_delves WHERE delve_id = $id;";
        cmd.Parameters.AddWithValue("$id", delveId);
        return cmd.ExecuteScalar() as string;
    }

    /// <summary>Reads the cross-delve-persisted subset of a member's pools (spec §1, S2-7 —
    /// `attrition.persistAcrossDelves[]`, `["hunger"]` today). Empty when the actor has never closed
    /// a delve before — the caller (party assembly) fills every other id from `resource.max`.</summary>
    public IReadOnlyDictionary<string, long> GetUniqueActorPersistedPools(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return new Dictionary<string, long>();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadPersistedPoolsUnlocked(db, instanceId.Trim());
        }
    }

    static IReadOnlyDictionary<string, long> ReadPersistedPoolsUnlocked(SqliteConnection db, string instanceId)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT resource_id, stored FROM rpg_unique_actor_pools WHERE instance_id = $id;";
        cmd.Parameters.AddWithValue("$id", instanceId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) result[r.GetString(0)] = r.GetInt64(1);
        return result;
    }

    /// <summary>Writes exactly the ids named by <paramref name="subsetPools"/> — the caller (`CloseDelve`)
    /// has already filtered to `attrition.persistAcrossDelves[]`; this method trusts that filter rather
    /// than re-deriving it, matching this whole module's "read model owned elsewhere" shape. One row
    /// per resource id (spec's own normalized shape), never a JSON blob.</summary>
    static void WritePersistedPoolsUnlocked(SqliteConnection db, string instanceId, IReadOnlyDictionary<string, long> subsetPools)
    {
        foreach (var (resourceId, stored) in subsetPools)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO rpg_unique_actor_pools(instance_id, resource_id, stored) VALUES($id, $rid, $stored)
                ON CONFLICT(instance_id, resource_id) DO UPDATE SET stored = $stored;
                """;
            cmd.Parameters.AddWithValue("$id", instanceId);
            cmd.Parameters.AddWithValue("$rid", resourceId);
            cmd.Parameters.AddWithValue("$stored", stored);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Observe capture events: ack → ActiveBound; die/end → Recovering → Roster.
    /// Fail-closed; never throws.
    /// </summary>
    /// <returns>
    /// T6.1 (2026-09-06, `mods-absorption`): the distinct player ids whose OWN <see
    /// cref="UniqueActorPhases.ActiveBound"/> roster changed (grew or shrank) while processing this
    /// batch — empty when nothing transitioned. Callers use this to re-push
    /// <see cref="AtomPushService.OwnersForPlayer"/>'s union for exactly the affected players, the
    /// same real gap T6.1's own audit entry named: nothing re-triggered the Hello-time union mid
    /// session, so an item equipped or unequipped after connecting never reached the runner.
    /// </returns>
    public IReadOnlyList<long> ObserveUniqueActorEvents(IEnumerable<(string Kind, string? MatchKey, string PayloadJson)> events)
    {
        var affected = new List<long>();
        try
        {
            foreach (var e in events)
                foreach (var pid in ObserveUniqueActorEvent(e.Kind, e.MatchKey, e.PayloadJson ?? "{}"))
                    if (!affected.Contains(pid))
                        affected.Add(pid);
        }
        catch
        {
            /* fail-closed */
        }
        return affected;
    }

    static readonly IReadOnlyList<long> NoPlayers = Array.Empty<long>();

    /// <returns>Every player id whose ActiveBound roster changed as a result of this one event —
    /// almost always zero or one, but a shared `match_key` recovering more than one specimen at once
    /// is handled without assuming they all belong to the same player.</returns>
    IReadOnlyList<long> ObserveUniqueActorEvent(string kind, string? matchKey, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(kind)) return NoPlayers;
        if (string.Equals(kind, "pvz.spawn.extra.ack", StringComparison.OrdinalIgnoreCase))
        {
            var corr = TryString(payloadJson, "correlationId");
            var ptr = TryString(payloadJson, "ptr");
            if (!string.IsNullOrWhiteSpace(corr) && !string.IsNullOrWhiteSpace(ptr))
            {
                var ack = TryAckUniqueSpawn(corr!, ptr!, matchKey);
                if (ack.Ok && ack.Actor is not null) return new[] { ack.Actor.PlayerId };
            }
            return NoPlayers;
        }

        if (string.Equals(kind, "plant.die", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "zombie.die", StringComparison.OrdinalIgnoreCase))
        {
            var ptr = TryString(payloadJson, "ptr");
            return !string.IsNullOrWhiteSpace(ptr) ? TryRecoverActiveByPtr(ptr!) : NoPlayers;
        }

        if (string.Equals(kind, "board.end", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "match.result", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(matchKey) ? TryRecoverActiveByMatchKey(matchKey!) : NoPlayers;
        }

        return NoPlayers;
    }

    IReadOnlyList<long> TryRecoverActiveByPtr(string ptr)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT instance_id, player_id FROM rpg_unique_actors
                WHERE phase = $phase AND last_ptr = $ptr;
                """;
            cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.ActiveBound);
            cmd.Parameters.AddWithValue("$ptr", ptr);
            var rows = new List<(string Id, long PlayerId)>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    rows.Add((r.GetString(0), r.GetInt64(1)));
            }
            foreach (var (id, _) in rows)
                RecoverToRosterUnlocked(db, id);
            return rows.Select(x => x.PlayerId).Distinct().ToList();
        }
    }

    IReadOnlyList<long> TryRecoverActiveByMatchKey(string matchKey)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT instance_id, player_id FROM rpg_unique_actors
                WHERE phase = $phase AND match_key = $mk;
                """;
            cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.ActiveBound);
            cmd.Parameters.AddWithValue("$mk", matchKey);
            var rows = new List<(string Id, long PlayerId)>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    rows.Add((r.GetString(0), r.GetInt64(1)));
            }
            foreach (var (id, _) in rows)
                RecoverToRosterUnlocked(db, id);
            return rows.Select(x => x.PlayerId).Distinct().ToList();
        }
    }

    void RecoverToRosterUnlocked(SqliteConnection db, string instanceId)
    {
        // ActiveBound → Recovering → Roster in one write (persist_ok immediate for W4).
        var now = DateTime.UtcNow.ToString("o");
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE rpg_unique_actors SET
              phase = $phase,
              match_key = NULL,
              last_ptr = NULL,
              deploy_correlation_id = NULL,
              revision = revision + 2,
              updated_utc = $now
            WHERE instance_id = $id AND phase = $active;
            """;
        cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Roster);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.Parameters.AddWithValue("$id", instanceId);
        cmd.Parameters.AddWithValue("$active", UniqueActorPhases.ActiveBound);
        cmd.ExecuteNonQuery();
    }

    void ClearBindToRosterUnlocked(SqliteConnection db, string instanceId)
    {
        var now = DateTime.UtcNow.ToString("o");
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE rpg_unique_actors SET
              phase = $phase,
              match_key = NULL,
              last_ptr = NULL,
              deploy_correlation_id = NULL,
              revision = revision + 1,
              updated_utc = $now
            WHERE instance_id = $id;
            """;
        cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Roster);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.Parameters.AddWithValue("$id", instanceId);
        cmd.ExecuteNonQuery();
    }

    UniqueActorDto? FindUniqueByCorrelationUnlocked(SqliteConnection db, string corr)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT instance_id, player_id, side, type_id, phase, level, xp,
                   match_key, last_ptr, deploy_correlation_id, revision, created_utc, updated_utc
            FROM rpg_unique_actors WHERE deploy_correlation_id = $corr LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$corr", corr);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapUniqueActor(r) : null;
    }

    UniqueActorDto? ReadUniqueActorUnlocked(SqliteConnection db, string instanceId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT instance_id, player_id, side, type_id, phase, level, xp,
                   match_key, last_ptr, deploy_correlation_id, revision, created_utc, updated_utc
            FROM rpg_unique_actors WHERE instance_id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", instanceId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapUniqueActor(r) : null;
    }

    static UniqueActorDto MapUniqueActor(SqliteDataReader r) => new()
    {
        InstanceId = r.GetString(0),
        PlayerId = r.GetInt64(1),
        Side = r.GetString(2),
        TypeId = r.GetInt32(3),
        Phase = r.GetString(4),
        Level = r.GetInt64(5),
        Xp = ReadXp(r, 6),
        MatchKey = r.IsDBNull(7) ? null : r.GetString(7),
        LastPtr = r.IsDBNull(8) ? null : r.GetString(8),
        DeployCorrelationId = r.IsDBNull(9) ? null : r.GetString(9),
        Revision = r.GetInt64(10),
        CreatedAt = r.GetString(11),
        UpdatedAt = r.GetString(12)
    };

    static string NormalizeUniqueSide(string? side)
    {
        var s = (side ?? "plant").Trim().ToLowerInvariant();
        return s is "plant" or "zombie" ? s : "plant";
    }

    static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string PayloadToJson(object? payload)
    {
        if (payload is null) return "{}";
        if (payload is string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "{}";
            try
            {
                using var _ = JsonDocument.Parse(s);
                return s;
            }
            catch
            {
                return JsonSerializer.Serialize(new { value = s });
            }
        }
        if (payload is JsonElement el)
            return el.GetRawText();
        try { return JsonSerializer.Serialize(payload, Json); }
        catch { return "{}"; }
    }

    /// <summary>Deploying rows older than <paramref name="timeout"/> → Roster (W5-D).</summary>
    public IReadOnlyList<(string InstanceId, string? CorrelationId)> FailExpiredUniqueDeploys(
        TimeSpan timeout, DateTimeOffset? utcNow = null)
    {
        if (timeout <= TimeSpan.Zero) return Array.Empty<(string, string?)>();
        var now = utcNow ?? DateTimeOffset.UtcNow;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var expired = new List<(string Id, string? Corr)>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT instance_id, deploy_correlation_id, updated_utc FROM rpg_unique_actors
                    WHERE phase = $phase;
                    """;
                cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Deploying);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var id = r.GetString(0);
                    var corr = r.IsDBNull(1) ? null : r.GetString(1);
                    var updated = r.GetString(2);
                    if (!DateTimeOffset.TryParse(updated, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                        continue;
                    if (now - ts >= timeout)
                        expired.Add((id, corr));
                }
            }

            foreach (var (id, _) in expired)
                ClearBindToRosterUnlocked(db, id);
            return expired;
        }
    }

    /// <summary>
    /// ActiveBound with missing/stale match_key (no open run) → Roster (W5-E).
    /// Observe lag OK; no Hot coupling. Call on boot / Init.
    /// </summary>
    public int SweepStaleActiveBoundUniqueActors()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return SweepStaleActiveBoundUnlocked(db);
        }
    }

    int SweepStaleActiveBoundUnlocked(SqliteConnection db)
    {
        var stale = new List<string>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT u.instance_id, u.match_key
                FROM rpg_unique_actors u
                WHERE u.phase = $phase;
                """;
            cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.ActiveBound);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var id = r.GetString(0);
                var mk = r.IsDBNull(1) ? null : r.GetString(1);
                if (string.IsNullOrWhiteSpace(mk) || !HasOpenRunForMatchKeyUnlocked(db, mk!))
                    stale.Add(id);
            }
        }

        foreach (var id in stale)
            RecoverToRosterUnlocked(db, id);
        return stale.Count;
    }

    static bool HasOpenRunForMatchKeyUnlocked(SqliteConnection db, string matchKey)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM runs
            WHERE match_key = $mk AND (ended_utc IS NULL OR ended_utc = '')
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$mk", matchKey);
        return cmd.ExecuteScalar() is not null and not DBNull;
    }

    public bool HasAnyActiveBoundUniqueActors()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return HasAnyActiveBoundUnlocked(db);
        }
    }

    static bool HasAnyActiveBoundUnlocked(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM rpg_unique_actors WHERE phase = $phase LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.ActiveBound);
        return cmd.ExecuteScalar() is not null and not DBNull;
    }

    public string GetUniqueStatModsJson(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return "{}";
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return GetUniqueStatModsJsonUnlocked(db, instanceId.Trim());
        }
    }

    public UniqueEquipmentListDto? GetUniqueEquipment(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        var id = instanceId.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var actor = ReadUniqueActorUnlocked(db, id);
            if (actor is null) return null;
            return new UniqueEquipmentListDto
            {
                InstanceId = id,
                Phase = actor.Phase,
                Items = ListUniqueEquipmentUnlocked(db, id),
                ModsJson = GetUniqueStatModsJsonUnlocked(db, id)
            };
        }
    }

    public void UpsertUniqueStatModsJson(string instanceId, string modsJson)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("instanceId");
        var json = string.IsNullOrWhiteSpace(modsJson) ? "{}" : modsJson.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            UpsertUniqueStatModsJsonUnlocked(db, instanceId.Trim(), json);
        }
    }

    static void UpsertUniqueStatModsJsonUnlocked(SqliteConnection db, string instanceId, string json)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rpg_unique_stat_mods(instance_id, mods_json) VALUES($id, $j)
            ON CONFLICT(instance_id) DO UPDATE SET mods_json = excluded.mods_json;
            """;
        cmd.Parameters.AddWithValue("$id", instanceId);
        cmd.Parameters.AddWithValue("$j", json);
        cmd.ExecuteNonQuery();
    }

    public List<UniqueEquipmentSlotDto> ListUniqueEquipment(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return new();
        var id = instanceId.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ListUniqueEquipmentUnlocked(db, id);
        }
    }

    /// <summary>
    /// ⭐ <b>The SSOT is <c>rpg_item_assignment</c>, not <c>rpg_unique_equipment</c></b> —
    /// `decision-d1-durable-ownership.md` §10 <b>M2</b>, landed 2026-09-06 alongside
    /// <see cref="MigrateUniqueEquipmentToAssignments"/> (M1). D1's own wording for this step:
    /// *"Repoint … to read assignments instead of `rpg_unique_equipment`. **Output shape unchanged**
    /// — same `mods_json`, same `instance:pending`, same binder, same FE."*
    ///
    /// <para><b>Legacy labels on the wire, canonical roles in the table.</b> The row carries an
    /// <see cref="FusionRpg.Core.Items.ItemRole"/> id; this projects it back through
    /// <see cref="FusionRpg.Core.Items.LegacyEquipSlots"/> so
    /// <c>GET /api/unique/actors/{id}/equipment</c> still answers <c>weapon|armor|trinket</c> and
    /// <c>RelicsLayer.tsx</c> needs no change. Widening the wire to all fifteen roles is D1's
    /// <b>M3</b>, a separate step that moves the REST payload and the FE literal together.</para>
    ///
    /// <para><b>The sort is preserved deliberately, not incidentally.</b> The old query ended
    /// <c>ORDER BY slot ASC</c> over the legacy labels (armor, trinket, weapon); ordering by
    /// <c>role</c> instead would answer armament-primary, core-guard, jewel-minor-a — i.e. weapon,
    /// armor, trinket — a different array on the same data. Sorted here by the projected legacy
    /// label with <see cref="string.CompareOrdinal(string?,string?)"/>, which is what SQLite's
    /// default BINARY collation on a TEXT column already did.</para>
    ///
    /// <para><b>Every <c>ref_kind</c> is surfaced, not just <c>stock</c>.</b> An assignment is what
    /// the specimen wears; filtering the view to the kind this wire happens to write would make the
    /// endpoint lie the day a rolled item lands in one of the three roles. No rolled assignment can
    /// exist today (no concrete item container is minted yet — module 17's own seed→concrete
    /// deferral), so this is a correctness property rather than a behaviour change.</para>
    /// </summary>
    static List<UniqueEquipmentSlotDto> ListUniqueEquipmentUnlocked(SqliteConnection db, string instanceId)
    {
        var items = new List<UniqueEquipmentSlotDto>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT role, ref_id FROM rpg_item_assignment WHERE specimen_id = $id;";
            cmd.Parameters.AddWithValue("$id", instanceId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!FusionRpg.Core.Items.ItemRoles.TryParse(r.GetString(0), out var role)) continue;
                if (!FusionRpg.Core.Items.LegacyEquipSlots.TryToLegacy(role, out var slot)) continue;
                items.Add(new UniqueEquipmentSlotDto
                {
                    Slot = slot,
                    ItemId = r.IsDBNull(1) ? "" : r.GetString(1)
                });
            }
        }
        items.Sort((a, b) => string.CompareOrdinal(a.Slot, b.Slot));
        return items;
    }

    /// <summary>Source marker on every assignment this legacy wire writes. <c>stock</c> is I13
    /// §4.4's kind for "identified by a catalog id, not by a rolled instance" — exactly what a
    /// relic or stub item is, and exactly what D1 §10 M1 specifies for the migrated rows.</summary>
    const string LegacyEquipRefKind = "stock";

    static void WriteLegacyEquipAssignmentUnlocked(
        SqliteConnection db, string instanceId, FusionRpg.Core.Items.ItemRole role, string itemId) =>
        ExecParams(db, """
            INSERT INTO rpg_item_assignment (specimen_id, role, ref_kind, ref_id, assigned_utc)
            VALUES ($sid, $role, $rk, $rid, $utc)
            ON CONFLICT(specimen_id, role) DO UPDATE SET
              ref_kind = excluded.ref_kind, ref_id = excluded.ref_id, assigned_utc = excluded.assigned_utc;
            """,
            ("$sid", instanceId), ("$role", FusionRpg.Core.Items.ItemRoles.Id(role)),
            ("$rk", LegacyEquipRefKind), ("$rid", itemId),
            ("$utc", DateTime.UtcNow.ToString("O")));

    static void ClearLegacyEquipAssignmentUnlocked(
        SqliteConnection db, string instanceId, FusionRpg.Core.Items.ItemRole role) =>
        ExecParams(db, "DELETE FROM rpg_item_assignment WHERE specimen_id = $sid AND role = $role;",
            ("$sid", instanceId), ("$role", FusionRpg.Core.Items.ItemRoles.Id(role)));

    /// <summary>
    /// ⭐ <b>D1 §10 <c>M1</c> — the relic row migration, closed 2026-09-06.</b> Copies every
    /// <c>rpg_unique_equipment(instance_id, slot, item_id)</c> row into
    /// <c>rpg_item_assignment(specimen_id, role, ref_kind='stock', ref_id=item_id)</c>, mapping the
    /// slot through I2's alias map (<see cref="FusionRpg.Core.Items.LegacyEquipSlots"/>).
    ///
    /// <para><b>Why this was stuck, and why it was never actually blocked.</b> `item-todo.md`
    /// module 4 (P1.4) deferred this to module 17 (`uniques`) on the grounds that relics had
    /// "no home to migrate into"; module 17 (P5.1) then deferred it back, correctly, as module 4's.
    /// Both notes were self-consistent and the item sat unbuilt. The premise of the first was
    /// wrong: module 17's <c>item_unique</c> is a nine-column <b>classification flag</b> keyed 1:1
    /// on an <c>effect_container</c> — no name, rarity, slot, description or effect column — so it
    /// never could have received an equipped-slot row. The home these rows needed is
    /// <c>rpg_item_assignment</c>, which is module 4's own table and shipped 2026-09-04.</para>
    ///
    /// <para><b>One-way and idempotent</b>, exactly as D1 words it. A specimen+role that already
    /// carries an assignment is left alone — never overwritten — so a second call writes nothing,
    /// and a row equipped through the post-M2 path can never be clobbered by a stale legacy row.
    /// The old table keeps its rows and stops being written; dropping it is D1's <b>M4</b>, gated on
    /// a <c>ref_kind='rolled'</c> grant path that does not exist yet, and D1 calls that drop
    /// *"the only irreversible act; do it last."*</para>
    ///
    /// <para>Returns the number of rows actually copied.</para>
    /// </summary>
    public int MigrateUniqueEquipmentToAssignments()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return MigrateUniqueEquipmentToAssignmentsUnlocked(db);
        }
    }

    internal static int MigrateUniqueEquipmentToAssignmentsUnlocked(SqliteConnection db)
    {
        var legacy = new List<(string InstanceId, string Slot, string ItemId)>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT instance_id, slot, item_id FROM rpg_unique_equipment;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                legacy.Add((r.GetString(0), r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2)));
        }
        if (legacy.Count == 0) return 0;

        var taken = new HashSet<string>(StringComparer.Ordinal);
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT specimen_id, role FROM rpg_item_assignment;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) taken.Add($"{r.GetString(0)} {r.GetString(1)}");
        }

        var now = DateTime.UtcNow.ToString("O");
        var copied = 0;
        foreach (var (instanceId, slot, itemId) in legacy)
        {
            if (string.IsNullOrWhiteSpace(itemId)) continue;
            if (!FusionRpg.Core.Items.LegacyEquipSlots.TryFromLegacy(slot, out var role)) continue;
            var roleId = FusionRpg.Core.Items.ItemRoles.Id(role);
            if (!taken.Add($"{instanceId} {roleId}")) continue; // already assigned — never overwrite
            ExecParams(db, """
                INSERT INTO rpg_item_assignment (specimen_id, role, ref_kind, ref_id, assigned_utc)
                VALUES ($sid, $role, $rk, $rid, $utc);
                """,
                ("$sid", instanceId), ("$role", roleId),
                ("$rk", LegacyEquipRefKind), ("$rid", itemId), ("$utc", now));
            copied++;
        }
        return copied;
    }

    /// <summary>Upsert slot. Empty itemId deletes the slot row. Non-empty must be a known stub.
    ///
    /// <para><b>Writes <c>rpg_item_assignment</c></b> (D1 §10 M2) — the legacy slot label is validated
    /// exactly as before, then mapped to its canonical role. The request, the response and every
    /// downstream effect (<c>mods_json</c>, the <c>unique-equip</c> atom bindings) are unchanged;
    /// only the table underneath moved. See <see cref="ListUniqueEquipmentUnlocked"/>.</para></summary>
    public UniqueEquipmentListDto UpsertUniqueEquipment(string instanceId, string slot, string? itemId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("instanceId");
        var id = instanceId.Trim();
        var s = FusionRpg.Core.Match.UniqueEquipmentCatalog.NormalizeSlot(slot);
        if (!FusionRpg.Core.Items.LegacyEquipSlots.TryFromLegacy(s, out var role))
            throw new ArgumentException("slot required", nameof(slot));
        var item = (itemId ?? "").Trim();
        if (!string.IsNullOrEmpty(item))
        {
            if (!FusionRpg.Core.Match.UniqueEquipmentCatalog.IsKnownItem(item))
                throw new ArgumentException("unknown_item", nameof(itemId));
            if (!FusionRpg.Core.Match.UniqueEquipmentCatalog.SlotMatchesItem(s, item))
                throw new ArgumentException("slot_mismatch", nameof(itemId));
        }
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (ReadUniqueActorUnlocked(db, id) is null)
                throw new InvalidOperationException("not_found");

            if (string.IsNullOrEmpty(item))
                ClearLegacyEquipAssignmentUnlocked(db, id, role);
            else
                WriteLegacyEquipAssignmentUnlocked(db, id, role, item);

            RebuildUniqueModsFromEquipmentUnlocked(db, id);
            ReconcileUniqueEquipmentAtomBindingsUnlocked(db, id);
            var actor = ReadUniqueActorUnlocked(db, id)!;
            return new UniqueEquipmentListDto
            {
                InstanceId = id,
                Phase = actor.Phase,
                Items = ListUniqueEquipmentUnlocked(db, id),
                ModsJson = GetUniqueStatModsJsonUnlocked(db, id)
            };
        }
    }

    public UniqueEquipmentListDto ClearUniqueEquipmentSlot(string instanceId, string slot) =>
        UpsertUniqueEquipment(instanceId, slot, "");

    /// <summary>Source tag on every binding this reconciliation owns — lets it find (and never touch)
    /// bindings any other system placed on the same <see cref="OwnerKind.UniqueActor"/> owner.</summary>
    const string UniqueEquipAtomSource = "unique-equip";

    /// <summary>
    /// contentScale's own pin (`ContentScale.cs`, `spec-content-scale.md` §2.1): Θc=20 is the anchor
    /// where `contentScale == 1.000` exactly, the same pin `AtomEndToEndTests.cs`'s own production-
    /// shape fixture uses. Stub equipment items are flat, unscaled effects — not loot that should get
    /// a level-derived curve invented for it as a side effect of this owner-scope fix (AGENTS.md "one
    /// power ladder": a private `f(level)` here is exactly the mistake that rule exists to prevent).
    /// </summary>
    const int UniqueEquipAtomPinTheta = 20;

    /// <summary>
    /// `mods-absorption` (T6.1, `tasks/seed-to-concrete-todo.md`, decision 1 —
    /// `tasks/seed-to-concrete-open-decisions.md`): reconcile this actor's atom-backed equipment slots
    /// against `effect_binding`, through <see cref="OwnerKind.UniqueActor"/> (owner-approved
    /// 2026-09-02 specifically because <see cref="OwnerKind.Entity"/> is session-scoped and would
    /// silently drop equipped-item bonuses on the next session boundary).
    ///
    /// <para><b>Idempotent by construction</b> — the "double-grant invariant": a slot whose existing
    /// binding's instance already points at the wanted item's own container is left untouched, never
    /// withdrawn and re-produced, so calling this twice with an unchanged loadout writes nothing new.
    /// A slot that changed item (or was cleared) has its stale binding withdrawn first, then whatever
    /// is now wanted is produced+bound.</para>
    ///
    /// <para>Only items <see cref="FusionRpg.Core.Match.UniqueEquipmentCatalog.TryGetAtomBackedContainerId"/>
    /// maps take this path. Everything else (today, only `stub.hp_charm` — its `fx.entity_atk` effect
    /// id has no real atom behind it) keeps flowing through the legacy `mods_json` grant
    /// (<see cref="RebuildUniqueModsFromEquipmentUnlocked"/>, called just before this, unchanged) —
    /// the two paths never grant the same slot twice because the catalog map is the single source of
    /// which path an item takes.</para>
    /// </summary>
    void ReconcileUniqueEquipmentAtomBindingsUnlocked(SqliteConnection db, string instanceId)
    {
        var actor = ReadUniqueActorUnlocked(db, instanceId);
        if (actor is null) return;
        var player = GetPlayerUnlocked(db, actor.PlayerId);
        if (player is null) return;

        var owner = new OwnerScope(OwnerKind.UniqueActor, instanceId);
        var slots = ListUniqueEquipmentUnlocked(db, instanceId);

        var wanted = new Dictionary<string, (string ContainerId, string ItemId)>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in slots)
        {
            if (string.IsNullOrWhiteSpace(s.ItemId)) continue;
            if (FusionRpg.Core.Match.UniqueEquipmentCatalog.TryGetAtomBackedContainerId(s.ItemId, out var containerId))
                wanted[s.Slot] = (containerId, s.ItemId);
        }

        var existing = ListBindings(owner)
            .Where(b => string.Equals(b.Source, UniqueEquipAtomSource, StringComparison.Ordinal))
            .ToDictionary(b => b.Slot ?? "", StringComparer.OrdinalIgnoreCase);

        bool MatchesWanted(BindingRow binding, (string ContainerId, string ItemId) want) =>
            GetInstance(binding.InstanceId) is { } inst &&
            string.Equals(inst.ContainerId, want.ContainerId, StringComparison.Ordinal);

        foreach (var (slot, binding) in existing)
        {
            var stillWanted = wanted.TryGetValue(slot, out var want) && MatchesWanted(binding, want);
            if (!stillWanted)
                Withdraw(binding.BindingId);
        }

        var catalogRevision = GetCatalogRevision();
        foreach (var (slot, want) in wanted)
        {
            if (existing.TryGetValue(slot, out var current) && MatchesWanted(current, want))
                continue; // already correctly bound — the double-grant invariant

            var container = GetContainer(want.ContainerId);
            if (container is null) continue; // seeded container missing; fail closed, nothing granted

            var rollSeed = WorldSeed.DeriveRollSeed(
                player.WorldSeed, UniqueEquipAtomSource, $"{instanceId}:{slot}:{want.ItemId}");
            ProduceAndBind(container, DomainMembers, rollSeed, UniqueEquipAtomPinTheta,
                FusionRpg.Core.Power.PowerTuningHub.Tuning, owner, slot, priority: 0,
                source: UniqueEquipAtomSource, out _, out _,
                origin: InstanceOrigin.Grant, catalogRevision: catalogRevision);
        }
    }

    /// <summary>
    /// `mods-absorption` (spec-mods-absorption.md) — the actual per-actor cutover for existing save
    /// data. Re-runs the same pair every equip/unequip already runs
    /// (<see cref="RebuildUniqueModsFromEquipmentUnlocked"/>, then
    /// <see cref="ReconcileUniqueEquipmentAtomBindingsUnlocked"/>) for every unique actor that has
    /// ever equipped something, so a row saved before this module shipped — carrying an atom-backed
    /// item's grant in <c>mods_json</c> from the pre-cutover <c>BuildModsJson</c> — gets rewritten to
    /// the post-cutover shape. Idempotent: an actor whose <c>mods_json</c> is already clean and whose
    /// bindings already match its loadout writes nothing new (the double-grant invariant, same as the
    /// per-equip path).
    ///
    /// <para><b>Per-actor atomic, not one giant transaction</b> (⛔ DECIDED 2026-09-03,
    /// spec-mods-absorption.md §"no read-through window"): each actor's own <c>lock(_gate)</c>
    /// critical section is the atomic unit, matching every other write in this file — the same shape
    /// <see cref="UpsertUniqueEquipment"/> already uses for one actor. A crash mid-sweep leaves
    /// already-cut-over actors cut over and the rest exactly as they were before this call; there is
    /// no window where one actor is half on each path. Returns the count of actors touched.</para>
    /// </summary>
    public int CutoverUniqueEquipmentModsAbsorption()
    {
        List<string> ids;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            // Repointed to the post-M2 SSOT alongside every other reader (D1 §10 M2). The legacy
            // table is migrated into this one at Init, so "every specimen that ever equipped
            // something" is the same set either way -- and after M2 it is the only correct one,
            // because a specimen that equipped for the first time post-cutover has no legacy row.
            cmd.CommandText = "SELECT DISTINCT specimen_id FROM rpg_item_assignment;";
            using var r = cmd.ExecuteReader();
            ids = new List<string>();
            while (r.Read()) ids.Add(r.GetString(0));
        }

        var touched = 0;
        foreach (var id in ids)
        {
            lock (_gate)
            {
                using var db = OpenUnlocked();
                if (ReadUniqueActorUnlocked(db, id) is null) continue;
                RebuildUniqueModsFromEquipmentUnlocked(db, id);
                ReconcileUniqueEquipmentAtomBindingsUnlocked(db, id);
            }
            touched++;
        }
        return touched;
    }

    public string RebuildUniqueModsFromEquipment(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("instanceId");
        var id = instanceId.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (ReadUniqueActorUnlocked(db, id) is null)
                throw new InvalidOperationException("not_found");
            return RebuildUniqueModsFromEquipmentUnlocked(db, id);
        }
    }

    string RebuildUniqueModsFromEquipmentUnlocked(SqliteConnection db, string instanceId)
    {
        var slots = ListUniqueEquipmentUnlocked(db, instanceId);
        var pairs = slots
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .Select(x => (x.Slot, x.ItemId));
        var existing = GetUniqueStatModsJsonUnlocked(db, instanceId);
        var json = FusionRpg.Core.Match.UniqueEquipmentCatalog.BuildModsJson(existing, pairs);
        UpsertUniqueStatModsJsonUnlocked(db, instanceId, json);
        return json;
    }

    static string GetUniqueStatModsJsonUnlocked(SqliteConnection db, string instanceId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT mods_json FROM rpg_unique_stat_mods WHERE instance_id = $id;";
        cmd.Parameters.AddWithValue("$id", instanceId);
        var v = cmd.ExecuteScalar();
        if (v is null or DBNull) return "{}";
        var s = Convert.ToString(v);
        return string.IsNullOrWhiteSpace(s) ? "{}" : s!;
    }

    /// <summary>
    /// W8-B: award specimen XP (not type RpgProgression).
    ///
    /// <para>The level ladder is <see cref="RpgXpCurve"/>'s shared arithmetic cost curve under the
    /// <see cref="RpgActorKinds.Specimen"/> kind, NOT the flat 100-per-level stub this carried until
    /// 2026-09-05. That stub was a private <c>f(level)</c> feeding the same quadratic <c>P(Theta)</c>
    /// every other level feeds, which made specimen power quadratic IN EFFORT where
    /// ssot-power-scale.md §10.5 requires linear — at level 1,000 a specimen level bought the same
    /// power as a player level for a small fraction of the effort. `first` is tuned to 100 so the
    /// cost at level 1 is byte-identical to the old stub; only the late-game divergence changes.</para>
    /// </summary>
    public (bool Ok, string Reason, UniqueActorDto? Actor) AwardUniqueActorXp(
        string instanceId, long delta, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return (false, "bad_args", null);
        if (delta <= 0) return (false, "bad_delta", null);
        var id = instanceId.Trim();
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var (ok, why, actor) = AwardUniqueActorXpUnlocked(db, id, delta);
            _ = reason; // audit reason reserved; no type progression write
            return (ok, why, actor);
        }
    }

    /// <summary>XP award inside an open transaction — used by the expedition reward apply.</summary>
    internal (bool Ok, string Reason, UniqueActorDto? Actor) AwardUniqueActorXpUnlocked(
        SqliteConnection db, string instanceId, long delta)
    {
        var row = ReadUniqueActorUnlocked(db, instanceId);
        if (row is null) return (false, "not_found", null);
        if (string.Equals(row.Phase, UniqueActorPhases.Retired, StringComparison.Ordinal))
            return (false, "phase.retired", row);

        long xp;
        checked { xp = row.Xp + delta; }
        var level = row.Level < 1 ? 1 : row.Level;
        // XpToNext is >= 1 by construction (RpgXpCurve clamps), so this always terminates.
        for (var need = RpgXpCurve.XpToNext(RpgActorKinds.Specimen, level); xp >= need;
             need = RpgXpCurve.XpToNext(RpgActorKinds.Specimen, level))
        {
            xp -= need;
            level++;
        }

        var now = DateTime.UtcNow.ToString("o");
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE rpg_unique_actors SET
              xp = $xp,
              level = $lvl,
              revision = revision + 1,
              updated_utc = $now
            WHERE instance_id = $id;
            """;
        cmd.Parameters.AddWithValue("$xp", xp);
        cmd.Parameters.AddWithValue("$lvl", level);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.Parameters.AddWithValue("$id", instanceId);
        cmd.ExecuteNonQuery();
        return (true, "", ReadUniqueActorUnlocked(db, instanceId));
    }
}
