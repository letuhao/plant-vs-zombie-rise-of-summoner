using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Atoms;
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
            var row = ReadUniqueActorUnlocked(db, id);
            if (row is null) return (false, "not_found", null);
            if (string.Equals(row.Phase, UniqueActorPhases.Retired, StringComparison.Ordinal))
                return (true, "", row);
            if (string.Equals(row.Phase, UniqueActorPhases.Deploying, StringComparison.Ordinal) ||
                string.Equals(row.Phase, UniqueActorPhases.Recovering, StringComparison.Ordinal))
                return (false, "phase." + row.Phase.ToLowerInvariant(), row);

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
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            return (true, "", ReadUniqueActorUnlocked(db, id));
        }
    }

    /// <summary>
    /// Observe capture events: ack → ActiveBound; die/end → Recovering → Roster.
    /// Fail-closed; never throws.
    /// </summary>
    public void ObserveUniqueActorEvents(IEnumerable<(string Kind, string? MatchKey, string PayloadJson)> events)
    {
        try
        {
            foreach (var e in events)
                ObserveUniqueActorEvent(e.Kind, e.MatchKey, e.PayloadJson ?? "{}");
        }
        catch
        {
            /* fail-closed */
        }
    }

    void ObserveUniqueActorEvent(string kind, string? matchKey, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(kind)) return;
        if (string.Equals(kind, "pvz.spawn.extra.ack", StringComparison.OrdinalIgnoreCase))
        {
            var corr = TryString(payloadJson, "correlationId");
            var ptr = TryString(payloadJson, "ptr");
            if (!string.IsNullOrWhiteSpace(corr) && !string.IsNullOrWhiteSpace(ptr))
                TryAckUniqueSpawn(corr!, ptr!, matchKey);
            return;
        }

        if (string.Equals(kind, "plant.die", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "zombie.die", StringComparison.OrdinalIgnoreCase))
        {
            var ptr = TryString(payloadJson, "ptr");
            if (!string.IsNullOrWhiteSpace(ptr))
                TryRecoverActiveByPtr(ptr!);
            return;
        }

        if (string.Equals(kind, "board.end", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "match.result", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(matchKey))
                TryRecoverActiveByMatchKey(matchKey!);
        }
    }

    void TryRecoverActiveByPtr(string ptr)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT instance_id FROM rpg_unique_actors
                WHERE phase = $phase AND last_ptr = $ptr;
                """;
            cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.ActiveBound);
            cmd.Parameters.AddWithValue("$ptr", ptr);
            var ids = new List<string>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    ids.Add(r.GetString(0));
            }
            foreach (var id in ids)
                RecoverToRosterUnlocked(db, id);
        }
    }

    void TryRecoverActiveByMatchKey(string matchKey)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT instance_id FROM rpg_unique_actors
                WHERE phase = $phase AND match_key = $mk;
                """;
            cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.ActiveBound);
            cmd.Parameters.AddWithValue("$mk", matchKey);
            var ids = new List<string>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    ids.Add(r.GetString(0));
            }
            foreach (var id in ids)
                RecoverToRosterUnlocked(db, id);
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

    static List<UniqueEquipmentSlotDto> ListUniqueEquipmentUnlocked(SqliteConnection db, string instanceId)
    {
        var items = new List<UniqueEquipmentSlotDto>();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT slot, item_id FROM rpg_unique_equipment
            WHERE instance_id = $id ORDER BY slot ASC;
            """;
        cmd.Parameters.AddWithValue("$id", instanceId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            items.Add(new UniqueEquipmentSlotDto
            {
                Slot = r.GetString(0),
                ItemId = r.IsDBNull(1) ? "" : r.GetString(1)
            });
        }
        return items;
    }

    /// <summary>Upsert slot. Empty itemId deletes the slot row. Non-empty must be a known stub.</summary>
    public UniqueEquipmentListDto UpsertUniqueEquipment(string instanceId, string slot, string? itemId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("instanceId");
        var id = instanceId.Trim();
        var s = FusionRpg.Core.Match.UniqueEquipmentCatalog.NormalizeSlot(slot);
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
            {
                using var del = db.CreateCommand();
                del.CommandText = "DELETE FROM rpg_unique_equipment WHERE instance_id = $id AND slot = $slot;";
                del.Parameters.AddWithValue("$id", id);
                del.Parameters.AddWithValue("$slot", s);
                del.ExecuteNonQuery();
            }
            else
            {
                using var ups = db.CreateCommand();
                ups.CommandText = """
                    INSERT INTO rpg_unique_equipment(instance_id, slot, item_id) VALUES($id, $slot, $item)
                    ON CONFLICT(instance_id, slot) DO UPDATE SET item_id = excluded.item_id;
                    """;
                ups.Parameters.AddWithValue("$id", id);
                ups.Parameters.AddWithValue("$slot", s);
                ups.Parameters.AddWithValue("$item", item);
                ups.ExecuteNonQuery();
            }

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
            cmd.CommandText = "SELECT DISTINCT instance_id FROM rpg_unique_equipment;";
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
    /// W8-B: award specimen XP (not type RpgProgression). Level stub: 100 XP per level.
    /// </summary>
    public (bool Ok, string Reason, UniqueActorDto? Actor) AwardUniqueActorXp(
        string instanceId, double delta, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return (false, "bad_args", null);
        if (!double.IsFinite(delta) || delta <= 0) return (false, "bad_delta", null);
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
        SqliteConnection db, string instanceId, double delta)
    {
        var row = ReadUniqueActorUnlocked(db, instanceId);
        if (row is null) return (false, "not_found", null);
        if (string.Equals(row.Phase, UniqueActorPhases.Retired, StringComparison.Ordinal))
            return (false, "phase.retired", row);

        var xp = row.Xp + delta;
        var level = row.Level < 1 ? 1 : row.Level;
        while (xp >= 100.0)
        {
            xp -= 100.0;
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
