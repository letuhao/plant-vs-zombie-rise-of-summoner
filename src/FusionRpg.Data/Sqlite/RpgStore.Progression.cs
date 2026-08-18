using FusionRpg.Contracts;
using FusionRpg.Core.Activity;
using FusionRpg.Core.Progression;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public readonly record struct RpgProgressionDirty(long PlayerId, string Kind, int TypeId, long Revision);

public readonly record struct EventInsertNotify(
    IReadOnlyList<long> ActivityPlayers,
    IReadOnlyList<RpgProgressionDirty> Progression,
    IReadOnlyList<long> ClosedRunIds);

public sealed partial class RpgStore
{
    static readonly LevelChangePipeline ProgressionPipeline = new();

    List<RpgProgressionDirty> ApplyRpgProgressionFromActivityUnlocked(
        SqliteConnection db, long playerId, long? runId, string t, string factKind,
        string payload, string dedupeKey, long factId)
    {
        var dirty = new List<RpgProgressionDirty>();
        var result = TryString(payload, "result");
        var typeId = TryInt(payload, "type");
        foreach (var award in RpgXpAwardMap.FromActivity(factKind, result, typeId, payload))
        {
            var ledgerPayload = award.Reason == RpgXpReasons.Kill
                ? MergePowerScalePayload(payload, award.PowerScale)
                : payload;
            var d = TryApplyXpUnlocked(
                db, playerId, award.Kind, award.TypeId, runId ?? 0, t,
                award.Delta, award.Reason, dedupeKey, factId, ledgerPayload);
            if (d is { } item)
            {
                dirty.Add(item);
                _progressionNotifyBatch?.Add(item);
            }
        }
        return dirty;
    }

    static string MergePowerScalePayload(string? payloadJson, double powerScale)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(
                string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
            using var stream = new System.IO.MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    if (p.NameEquals("powerScale")) continue;
                    p.WriteTo(writer);
                }
                writer.WriteNumber("powerScale", powerScale);
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return System.Text.Json.JsonSerializer.Serialize(new { powerScale });
        }
    }

    RpgProgressionDirty? TryApplyXpUnlocked(
        SqliteConnection db, long playerId, string kind, int typeId, long runId, string t,
        double delta, string reason, string dedupeKey, long? factId, string? payloadJson)
    {
        EnsureActorRowUnlocked(db, playerId, kind, typeId);
        var state = ReadActorStateUnlocked(db, playerId, kind, typeId);
        var levelBefore = state.Level;
        var xpBefore = state.Xp;
        var demotionBefore = state.DemotionCount;

        var applied = RpgXpApply.Apply(kind, state, delta, playerId, typeId, reason);
        ProgressionPipeline.RunAll(applied.LevelChanges);

        using (var ins = db.CreateCommand())
        {
            ins.CommandText = """
                INSERT OR IGNORE INTO rpg_xp_ledger(
                  player_id, kind, type_id, run_id, t, delta, reason, activity_fact_id,
                  level_before, xp_before, level_after, xp_after, demotion_before, demotion_after, payload_json, dedupe_key)
                VALUES($p,$k,$tid,$r,$t,$d,$reason,$fid,$lb,$xb,$la,$xa,$db,$da,$pj,$dk);
                """;
            ins.Parameters.AddWithValue("$p", playerId);
            ins.Parameters.AddWithValue("$k", kind);
            ins.Parameters.AddWithValue("$tid", typeId);
            ins.Parameters.AddWithValue("$r", runId);
            ins.Parameters.AddWithValue("$t", t);
            ins.Parameters.AddWithValue("$d", delta);
            ins.Parameters.AddWithValue("$reason", reason);
            ins.Parameters.AddWithValue("$fid", (object?)factId ?? DBNull.Value);
            ins.Parameters.AddWithValue("$lb", levelBefore);
            ins.Parameters.AddWithValue("$xb", xpBefore);
            ins.Parameters.AddWithValue("$la", applied.State.Level);
            ins.Parameters.AddWithValue("$xa", applied.State.Xp);
            ins.Parameters.AddWithValue("$db", demotionBefore);
            ins.Parameters.AddWithValue("$da", applied.State.DemotionCount);
            ins.Parameters.AddWithValue("$pj", (object?)payloadJson ?? DBNull.Value);
            ins.Parameters.AddWithValue("$dk", dedupeKey);
            if (ins.ExecuteNonQuery() <= 0)
                return null;
        }

        long ledgerId;
        using (var idCmd = db.CreateCommand())
        {
            idCmd.CommandText = "SELECT last_insert_rowid();";
            ledgerId = Convert.ToInt64(idCmd.ExecuteScalar() ?? 0L);
        }

        var bucketsJson = ReadXpByReasonJsonUnlocked(db, playerId, kind, typeId);
        bucketsJson = MergeXpReasonBucket(bucketsJson, reason, delta);

        var now = DateTime.UtcNow.ToString("o");
        using (var up = db.CreateCommand())
        {
            up.CommandText = """
                UPDATE rpg_actor_progression SET
                  level=$l, xp=$x, highest_level=$h, demotion_count=$dm, revision=$rev, updated_utc=$t,
                  through_ledger_id=$tl, xp_by_reason_json=$bj
                WHERE player_id=$p AND kind=$k AND type_id=$tid;
                """;
            up.Parameters.AddWithValue("$l", applied.State.Level);
            up.Parameters.AddWithValue("$x", applied.State.Xp);
            up.Parameters.AddWithValue("$h", applied.State.HighestLevel);
            up.Parameters.AddWithValue("$dm", applied.State.DemotionCount);
            up.Parameters.AddWithValue("$rev", applied.State.Revision);
            up.Parameters.AddWithValue("$t", now);
            up.Parameters.AddWithValue("$tl", ledgerId);
            up.Parameters.AddWithValue("$bj", bucketsJson);
            up.Parameters.AddWithValue("$p", playerId);
            up.Parameters.AddWithValue("$k", kind);
            up.Parameters.AddWithValue("$tid", typeId);
            up.ExecuteNonQuery();
        }

        return new RpgProgressionDirty(playerId, kind, typeId, applied.State.Revision);
    }

    static string ReadXpByReasonJsonUnlocked(SqliteConnection db, long playerId, string kind, int typeId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT xp_by_reason_json FROM rpg_actor_progression
            WHERE player_id=$p AND kind=$k AND type_id=$t;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$k", kind);
        cmd.Parameters.AddWithValue("$t", typeId);
        var o = cmd.ExecuteScalar();
        if (o is string s && !string.IsNullOrWhiteSpace(s)) return s;
        return "{}";
    }

    static string MergeXpReasonBucket(string bucketsJson, string reason, double delta)
    {
        Dictionary<string, XpReasonBucket> map;
        try
        {
            map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, XpReasonBucket>>(bucketsJson)
                  ?? new Dictionary<string, XpReasonBucket>(StringComparer.Ordinal);
        }
        catch
        {
            map = new Dictionary<string, XpReasonBucket>(StringComparer.Ordinal);
        }
        if (!map.TryGetValue(reason, out var bucket) || bucket is null)
            bucket = new XpReasonBucket();
        bucket.Sum += delta;
        bucket.Count += 1;
        map[reason] = bucket;
        return System.Text.Json.JsonSerializer.Serialize(map);
    }

    sealed class XpReasonBucket
    {
        public double Sum { get; set; }
        public int Count { get; set; }
    }

    void EnsureActorRowUnlocked(SqliteConnection db, long playerId, string kind, int typeId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO rpg_actor_progression(
              player_id, kind, type_id, level, xp, highest_level, demotion_count, revision, updated_utc)
            VALUES($p,$k,$t,1,0,1,0,0,$u);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$k", kind);
        cmd.Parameters.AddWithValue("$t", typeId);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    RpgActorState ReadActorStateUnlocked(SqliteConnection db, long playerId, string kind, int typeId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT level, xp, highest_level, demotion_count, revision
            FROM rpg_actor_progression WHERE player_id=$p AND kind=$k AND type_id=$t;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$k", kind);
        cmd.Parameters.AddWithValue("$t", typeId);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return new RpgActorState();
        return new RpgActorState
        {
            Level = r.GetInt64(0),
            Xp = r.GetDouble(1),
            HighestLevel = r.GetInt64(2),
            DemotionCount = r.GetInt64(3),
            Revision = r.GetInt64(4)
        };
    }

    public RpgProgressionSummaryDto? GetRpgProgressionSummary(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return null;
            var player = ReadActorDtoUnlocked(db, playerId, RpgActorKinds.Player, 0)
                ?? DefaultPlayerDtoUnlocked(playerId);
            var plants = ListActorsUnlocked(db, playerId, RpgActorKinds.Plant, "level", 5);
            var zombies = ListActorsUnlocked(db, playerId, RpgActorKinds.Zombie, "level", 5);
            var plantCount = CountActorsUnlocked(db, playerId, RpgActorKinds.Plant);
            var zombieCount = CountActorsUnlocked(db, playerId, RpgActorKinds.Zombie);
            return new RpgProgressionSummaryDto
            {
                PlayerId = playerId,
                Player = player,
                PlantActorCount = plantCount,
                ZombieActorCount = zombieCount,
                HighestPlantLevel = MaxHighestLevelUnlocked(db, playerId, RpgActorKinds.Plant),
                HighestZombieLevel = MaxHighestLevelUnlocked(db, playerId, RpgActorKinds.Zombie),
                TopPlants = plants,
                TopZombies = zombies
            };
        }
    }

    static RpgActorProgressionDto DefaultPlayerDtoUnlocked(long playerId)
    {
        var (first, step) = RpgXpCurve.ParamsFor(RpgActorKinds.Player);
        return new RpgActorProgressionDto
        {
            PlayerId = playerId,
            Kind = RpgActorKinds.Player,
            TypeId = 0,
            TypeName = "Player",
            Level = 1,
            Xp = 0,
            XpToNext = RpgXpCurve.XpToNext(RpgActorKinds.Player, 1),
            HighestLevel = 1,
            DemotionCount = 0,
            Revision = 0,
            UpdatedAt = "",
            CurveFirst = first,
            CurveStep = step
        };
    }

    static long MaxHighestLevelUnlocked(SqliteConnection db, long playerId, string kind)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(MAX(highest_level), 0)
            FROM rpg_actor_progression WHERE player_id=$p AND kind=$k;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$k", kind);
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
    }

    public RpgProgressionListDto? ListRpgProgression(
        long playerId, string? kind, string sort = "level", int limit = 200, int offset = 0)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return null;
            limit = Math.Clamp(limit, 1, 500);
            offset = Math.Max(0, offset);
            var total = string.IsNullOrWhiteSpace(kind)
                ? CountActorsUnlocked(db, playerId, null)
                : CountActorsUnlocked(db, playerId, kind);
            return new RpgProgressionListDto
            {
                PlayerId = playerId,
                Items = ListActorsUnlocked(db, playerId, kind, sort, limit, offset),
                Total = total,
                Limit = limit,
                Offset = offset
            };
        }
    }

    public RpgActorProgressionDto? GetRpgActor(long playerId, string kind, int typeId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return null;
            if (!RpgActorKinds.IsKnown(kind)) return null;
            return ReadActorDtoUnlocked(db, playerId, kind, typeId);
        }
    }

    public RpgXpLedgerPageDto? ListRpgXpLedger(
        long playerId, string? kind, int? typeId, string? reason, int limit = 100, long? afterId = null)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return null;
            limit = Math.Clamp(limit, 1, 500);
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT id, player_id, kind, type_id, run_id, t, delta, reason, activity_fact_id,
                       level_before, xp_before, level_after, xp_after, demotion_before, demotion_after, payload_json
                FROM rpg_xp_ledger WHERE player_id=$p
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            if (!string.IsNullOrWhiteSpace(kind))
            {
                cmd.CommandText += " AND kind=$k";
                cmd.Parameters.AddWithValue("$k", kind.Trim());
            }
            if (typeId is { } tid)
            {
                cmd.CommandText += " AND type_id=$tid";
                cmd.Parameters.AddWithValue("$tid", tid);
            }
            if (!string.IsNullOrWhiteSpace(reason))
            {
                cmd.CommandText += " AND reason=$rs";
                cmd.Parameters.AddWithValue("$rs", reason.Trim());
            }
            if (afterId is { } aid)
            {
                cmd.CommandText += " AND id < $after";
                cmd.Parameters.AddWithValue("$after", aid);
            }
            cmd.CommandText += " ORDER BY id DESC LIMIT $lim;";
            cmd.Parameters.AddWithValue("$lim", limit);
            var items = new List<(long Id, long PlayerId, string Kind, int TypeId, long RunId, string T, double Delta, string Reason, long? FactId, long Lb, double Xb, long La, double Xa, long Db, long Da, string? Payload)>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    items.Add((
                        r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetInt32(3), r.GetInt64(4), r.GetString(5),
                        r.GetDouble(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetInt64(8),
                        r.GetInt64(9), r.GetDouble(10), r.GetInt64(11), r.GetDouble(12), r.GetInt64(13), r.GetInt64(14),
                        r.IsDBNull(15) ? null : r.GetString(15)));
                }
            }
            long? nextAfter = items.Count > 0 ? items[^1].Id : null;
            return new RpgXpLedgerPageDto
            {
                PlayerId = playerId,
                Items = items.Select(x => new RpgXpLedgerEntryDto
                {
                    Id = x.Id,
                    PlayerId = x.PlayerId,
                    Kind = x.Kind,
                    TypeId = x.TypeId,
                    TypeName = LookupTypeNameUnlocked(db, x.Kind, x.TypeId),
                    RunId = x.RunId,
                    T = x.T,
                    Delta = x.Delta,
                    Reason = x.Reason,
                    ActivityFactId = x.FactId,
                    LevelBefore = x.Lb,
                    XpBefore = x.Xb,
                    LevelAfter = x.La,
                    XpAfter = x.Xa,
                    DemotionBefore = x.Db,
                    DemotionAfter = x.Da,
                    PayloadJson = x.Payload
                }).ToList(),
                Limit = limit,
                NextAfterId = items.Count >= limit ? nextAfter : null
            };
        }
    }

    public RpgProgressionStatsDto? GetRpgProgressionStats(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return null;

            var xpByReasonMap = new Dictionary<string, RpgXpReasonStatDto>(StringComparer.Ordinal);
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT xp_by_reason_json FROM rpg_actor_progression
                    WHERE player_id=$p AND xp_by_reason_json IS NOT NULL AND xp_by_reason_json != '' AND xp_by_reason_json != '{}';
                    """;
                cmd.Parameters.AddWithValue("$p", playerId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (r.IsDBNull(0)) continue;
                    Dictionary<string, XpReasonBucket>? map = null;
                    try
                    {
                        map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, XpReasonBucket>>(r.GetString(0));
                    }
                    catch { /* ignore */ }
                    if (map is null) continue;
                    foreach (var kv in map)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value is null) continue;
                        if (!xpByReasonMap.TryGetValue(kv.Key, out var agg))
                        {
                            agg = new RpgXpReasonStatDto { Reason = kv.Key };
                            xpByReasonMap[kv.Key] = agg;
                        }
                        agg.SumDelta += kv.Value.Sum;
                        agg.Count += kv.Value.Count;
                    }
                }
            }

            if (xpByReasonMap.Count == 0)
                BackfillXpReasonBucketsFromLedgerUnlocked(db, playerId, xpByReasonMap);

            var xpByReason = xpByReasonMap.Values.OrderBy(x => x.Reason, StringComparer.Ordinal).ToList();

            var recent = new List<RpgRecentDeltaDto>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT t, delta, reason FROM rpg_xp_ledger
                    WHERE player_id=$p ORDER BY id DESC LIMIT 40;
                    """;
                cmd.Parameters.AddWithValue("$p", playerId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    recent.Add(new RpgRecentDeltaDto
                    {
                        T = r.GetString(0),
                        Delta = r.GetDouble(1),
                        Reason = r.GetString(2)
                    });
                }
            }

            return new RpgProgressionStatsDto
            {
                PlayerId = playerId,
                XpByReason = xpByReason,
                PlantLevels = LevelBucketsUnlocked(db, playerId, RpgActorKinds.Plant),
                ZombieLevels = LevelBucketsUnlocked(db, playerId, RpgActorKinds.Zombie),
                RecentDeltas = recent
            };
        }
    }

    /// <summary>
    /// One-shot heal for pre-W6 rows: rebuild per-actor buckets from ledger and fill aggregate map.
    /// </summary>
    void BackfillXpReasonBucketsFromLedgerUnlocked(
        SqliteConnection db, long playerId, Dictionary<string, RpgXpReasonStatDto> aggregate)
    {
        long ledgerCount;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM rpg_xp_ledger WHERE player_id=$p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            ledgerCount = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }
        if (ledgerCount <= 0) return;

        var actors = new List<(string Kind, int TypeId)>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT kind, type_id FROM rpg_actor_progression
                WHERE player_id=$p
                  AND (xp_by_reason_json IS NULL OR xp_by_reason_json = '' OR xp_by_reason_json = '{}');
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                actors.Add((r.GetString(0), r.GetInt32(1)));
        }

        foreach (var (kind, typeId) in actors)
        {
            var map = new Dictionary<string, XpReasonBucket>(StringComparer.Ordinal);
            long maxId = 0;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT id, reason, delta FROM rpg_xp_ledger
                    WHERE player_id=$p AND kind=$k AND type_id=$t ORDER BY id;
                    """;
                cmd.Parameters.AddWithValue("$p", playerId);
                cmd.Parameters.AddWithValue("$k", kind);
                cmd.Parameters.AddWithValue("$t", typeId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    maxId = Math.Max(maxId, r.GetInt64(0));
                    var reason = r.GetString(1);
                    var delta = r.GetDouble(2);
                    if (!map.TryGetValue(reason, out var bucket) || bucket is null)
                        bucket = new XpReasonBucket();
                    bucket.Sum += delta;
                    bucket.Count += 1;
                    map[reason] = bucket;
                }
            }
            if (map.Count == 0) continue;
            var json = System.Text.Json.JsonSerializer.Serialize(map);
            using (var up = db.CreateCommand())
            {
                up.CommandText = """
                    UPDATE rpg_actor_progression
                    SET xp_by_reason_json=$j, through_ledger_id=$tl
                    WHERE player_id=$p AND kind=$k AND type_id=$t;
                    """;
                up.Parameters.AddWithValue("$j", json);
                up.Parameters.AddWithValue("$tl", maxId);
                up.Parameters.AddWithValue("$p", playerId);
                up.Parameters.AddWithValue("$k", kind);
                up.Parameters.AddWithValue("$t", typeId);
                up.ExecuteNonQuery();
            }
            foreach (var kv in map)
            {
                if (!aggregate.TryGetValue(kv.Key, out var agg))
                {
                    agg = new RpgXpReasonStatDto { Reason = kv.Key };
                    aggregate[kv.Key] = agg;
                }
                agg.SumDelta += kv.Value.Sum;
                agg.Count += kv.Value.Count;
            }
        }

        // If no actors needed backfill but ledger exists (edge), still expose ledger GROUP BY once.
        if (aggregate.Count == 0)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT reason, COALESCE(SUM(delta),0), COUNT(*)
                FROM rpg_xp_ledger WHERE player_id=$p
                GROUP BY reason ORDER BY reason;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                aggregate[r.GetString(0)] = new RpgXpReasonStatDto
                {
                    Reason = r.GetString(0),
                    SumDelta = r.GetDouble(1),
                    Count = r.GetInt32(2)
                };
            }
        }
    }

    static List<RpgLevelBucketDto> LevelBucketsUnlocked(SqliteConnection db, long playerId, string kind)
    {
        var list = new List<RpgLevelBucketDto>();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT level, COUNT(*) FROM rpg_actor_progression
            WHERE player_id=$p AND kind=$k GROUP BY level ORDER BY level;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$k", kind);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new RpgLevelBucketDto { Level = r.GetInt64(0), Count = r.GetInt32(1) });
        return list;
    }

    public RpgActorProgressionDto? ClearRpgDemotion(long playerId, string kind, int typeId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return null;
            if (!RpgActorKinds.IsKnown(kind)) return null;
            if (ReadActorDtoUnlocked(db, playerId, kind, typeId) is null) return null;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    UPDATE rpg_actor_progression
                    SET demotion_count=0, revision=revision+1, updated_utc=$t
                    WHERE player_id=$p AND kind=$k AND type_id=$tid;
                    """;
                cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("$p", playerId);
                cmd.Parameters.AddWithValue("$k", kind);
                cmd.Parameters.AddWithValue("$tid", typeId);
                if (cmd.ExecuteNonQuery() <= 0) return null;
            }
            return ReadActorDtoUnlocked(db, playerId, kind, typeId);
        }
    }

    public RpgProgressionSummaryDto SeedRpgProgressionDemo(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null)
                throw new InvalidOperationException("player not found");
            var t = DateTime.UtcNow.ToString("o");
            var killPayload = MergePowerScalePayload("{}", 1.0);
            TryApplyXpUnlocked(db, playerId, RpgActorKinds.Player, 0, 0, t, RpgXpAwards.Kill * 20, RpgXpReasons.Kill, "seed-player", null, killPayload);
            TryApplyXpUnlocked(db, playerId, RpgActorKinds.Plant, 0, 0, t, RpgXpAwards.PlantPlace * 15, RpgXpReasons.PlantPlace, "seed-plant-0", null, "{}");
            TryApplyXpUnlocked(db, playerId, RpgActorKinds.Zombie, 1, 0, t, RpgXpAwards.ZombieSpawn * 20, RpgXpReasons.ZombieSpawn, "seed-zombie-1", null, "{}");
        }
        return GetRpgProgressionSummary(playerId)!;
    }

    int CountActorsUnlocked(SqliteConnection db, long playerId, string? kind)
    {
        using var cmd = db.CreateCommand();
        if (string.IsNullOrWhiteSpace(kind))
        {
            cmd.CommandText = "SELECT COUNT(*) FROM rpg_actor_progression WHERE player_id=$p;";
            cmd.Parameters.AddWithValue("$p", playerId);
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM rpg_actor_progression WHERE player_id=$p AND kind=$k;";
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$k", kind);
        }
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    List<RpgActorProgressionDto> ListActorsUnlocked(
        SqliteConnection db, long playerId, string? kind, string sort, int limit, int offset = 0)
    {
        limit = Math.Clamp(limit, 1, 500);
        offset = Math.Max(0, offset);
        var order = sort switch
        {
            "xp" => "xp DESC, level DESC",
            "updated" => "updated_utc DESC",
            "typeId" => "type_id ASC",
            _ => "level DESC, xp DESC"
        };
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"""
            SELECT player_id, kind, type_id, level, xp, highest_level, demotion_count, revision, updated_utc
            FROM rpg_actor_progression WHERE player_id=$p
            {(string.IsNullOrWhiteSpace(kind) ? "" : " AND kind=$k")}
            ORDER BY {order} LIMIT $lim OFFSET $off;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        if (!string.IsNullOrWhiteSpace(kind))
            cmd.Parameters.AddWithValue("$k", kind.Trim());
        cmd.Parameters.AddWithValue("$lim", limit);
        cmd.Parameters.AddWithValue("$off", offset);
        var rows = new List<(long PlayerId, string Kind, int TypeId, long Level, double Xp, long Highest, long Demotion, long Revision, string Updated)>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                rows.Add((
                    r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3), r.GetDouble(4),
                    r.GetInt64(5), r.GetInt64(6), r.GetInt64(7), r.GetString(8)));
            }
        }
        return rows.Select(row => ToActorDto(db, row.PlayerId, row.Kind, row.TypeId, row.Level, row.Xp, row.Highest, row.Demotion, row.Revision, row.Updated)).ToList();
    }

    RpgActorProgressionDto? ReadActorDtoUnlocked(SqliteConnection db, long playerId, string kind, int typeId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT player_id, kind, type_id, level, xp, highest_level, demotion_count, revision, updated_utc
            FROM rpg_actor_progression WHERE player_id=$p AND kind=$k AND type_id=$t;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$k", kind);
        cmd.Parameters.AddWithValue("$t", typeId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return ToActorDto(db, r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3), r.GetDouble(4),
            r.GetInt64(5), r.GetInt64(6), r.GetInt64(7), r.GetString(8));
    }

    RpgActorProgressionDto ToActorDto(
        SqliteConnection db, long playerId, string kind, int typeId, long level, double xp,
        long highest, long demotion, long revision, string updated)
    {
        var (first, step) = RpgXpCurve.ParamsFor(kind);
        var dto = new RpgActorProgressionDto
        {
            PlayerId = playerId,
            Kind = kind,
            TypeId = typeId,
            TypeName = LookupTypeNameUnlocked(db, kind, typeId),
            DisplayName = LookupDisplayNameUnlocked(db, kind, typeId),
            Level = level,
            Xp = xp,
            XpToNext = RpgXpCurve.XpToNext(kind, level),
            HighestLevel = highest,
            DemotionCount = demotion,
            Revision = revision,
            UpdatedAt = updated,
            CurveFirst = first,
            CurveStep = step
        };
        ApplyAlmanacPromoteUnlocked(db, dto);
        return dto;
    }

    /// <summary>
    /// Promote curated almanac dump fields onto progression actors:
    /// name → displayName, enumName → typeName fallback, info / introduce / cost.
    /// </summary>
    void ApplyAlmanacPromoteUnlocked(SqliteConnection hot, RpgActorProgressionDto dto)
    {
        if (dto.Kind is not (RpgActorKinds.Plant or RpgActorKinds.Zombie)) return;
        var side = dto.Kind == RpgActorKinds.Plant ? "plant" : "zombie";
        using var media = OpenMediaUnlocked();
        var dump = ReadAlmanacDumpUnlocked(media, hot, side, dto.TypeId);
        if (dump?.Fields is not { Count: > 0 } fields) return;

        static string? Field(Dictionary<string, string?> map, string key)
        {
            if (!map.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v)) return null;
            return v.Trim();
        }

        var name = Field(fields, "name") ?? Field(fields, "displayName");
        if (!string.IsNullOrWhiteSpace(name))
            dto.DisplayName = name;

        var enumName = Field(fields, "enumName");
        if (!string.IsNullOrWhiteSpace(enumName))
            dto.TypeName = enumName;

        dto.AlmanacInfo = Field(fields, "info");
        dto.AlmanacIntroduce = Field(fields, "introduce");
        dto.AlmanacCost = Field(fields, "cost");
    }

    string? LookupTypeNameUnlocked(SqliteConnection db, string kind, int typeId)
    {
        if (kind == RpgActorKinds.Player) return "Player";
        var side = kind == RpgActorKinds.Plant ? "plant" : kind == RpgActorKinds.Zombie ? "zombie" : null;
        if (side is null) return null;
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT type_name FROM types WHERE side=$s AND type=$t LIMIT 1;";
        cmd.Parameters.AddWithValue("$s", side);
        cmd.Parameters.AddWithValue("$t", typeId);
        return cmd.ExecuteScalar() as string;
    }

    string? LookupDisplayNameUnlocked(SqliteConnection db, string kind, int typeId)
    {
        if (kind == RpgActorKinds.Player) return null;
        var side = kind == RpgActorKinds.Plant ? "plant" : kind == RpgActorKinds.Zombie ? "zombie" : null;
        if (side is null) return null;
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT display_name FROM types WHERE side=$s AND type=$t LIMIT 1;";
        cmd.Parameters.AddWithValue("$s", side);
        cmd.Parameters.AddWithValue("$t", typeId);
        return cmd.ExecuteScalar() as string;
    }
}
