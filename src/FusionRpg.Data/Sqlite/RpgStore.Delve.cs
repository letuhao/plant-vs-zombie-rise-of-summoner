using System.Text.Json;
using FusionRpg.Core.Delve;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Movement;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One party's route/pity/haul, one element of <c>rpg_delves.parties_json</c> per
/// <c>PartyIndex</c> — a single JSON array on the header, not a third table (spec-delve-scope.md
/// §1: "a raid is one, two or four parties written in one transaction with the delve").</summary>
public sealed record DelvePartyState(
    long EntityId, IReadOnlyList<string> Route, IReadOnlyDictionary<string, int> Pity, IReadOnlyList<string> Haul);

public sealed record DelveRow(
    long DelveId, long PlayerId, string WorldId, string DomainId, string RaidMode, string RungId,
    ulong Seed, string State, string CorrelationId, string EnteredUtc, string? ClosedUtc,
    IReadOnlyList<DelvePartyState> Parties, string DecisionsJson,
    long SoulsUnbanked, int ThetaRun, string QuestsJson, string? ContentTermsJson, long Revision)
{
    public static IReadOnlyList<DelvePartyState> ParsePartiesJson(string json) =>
        JsonSerializer.Deserialize<List<DelvePartyState>>(json) ?? new List<DelvePartyState>();
}

/// <summary>One row of <c>rpg_delve_rooms</c> — a rolled room, keyed by its <c>WorldSector</c> id
/// in the delve world (spec-delve-scope.md §1).</summary>
public sealed record DelveRoomRow(
    string SectorId, int RowIndex, int ColIndex, string Kind, string ArchetypeId,
    bool Visited, bool Cleared, string? KeyForLaneId,
    string? EventId, string? ResolvedKind, string? ResolvedArchetypeId, string FloorJson, long Revision);

public static class DelveStates
{
    public const string Active = "Active";
    public const string Extracted = "Extracted";
    public const string Wiped = "Wiped";
    public const string Archived = "Archived";
}

public sealed partial class RpgStore
{
    /// <summary>The two delve tables. Called from <c>EnsureWorldSchemaUnlocked</c> beside
    /// <c>EnsureWorldTurnSchemaUnlocked</c> — a delve world is a <c>rpg_worlds</c> row, so its own
    /// schema setup lives beside the world program's (spec-delve-scope.md §1).</summary>
    void EnsureDelveSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS rpg_delves (
              delve_id INTEGER PRIMARY KEY AUTOINCREMENT, player_id INTEGER NOT NULL,
              world_id TEXT NOT NULL UNIQUE,
              domain_id TEXT NOT NULL, raid_mode TEXT NOT NULL, rung_id TEXT NOT NULL,
              seed TEXT NOT NULL,
              state TEXT NOT NULL,
              correlation_id TEXT NOT NULL, entered_utc TEXT NOT NULL, closed_utc TEXT,
              parties_json TEXT NOT NULL DEFAULT '[]',
              decisions_json TEXT NOT NULL DEFAULT '[]',
              souls_unbanked INTEGER NOT NULL DEFAULT 0, theta_run INTEGER NOT NULL DEFAULT 0,
              quests_json TEXT NOT NULL DEFAULT '[]',
              content_terms_json TEXT,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_rpg_delves_corr   ON rpg_delves(player_id, correlation_id);
            CREATE INDEX        IF NOT EXISTS ix_rpg_delves_domain ON rpg_delves(player_id, domain_id, state);

            CREATE TABLE IF NOT EXISTS rpg_delve_rooms (
              delve_id INTEGER NOT NULL, sector_id TEXT NOT NULL,
              row_index INTEGER NOT NULL, col_index INTEGER NOT NULL,
              kind TEXT NOT NULL, archetype_id TEXT NOT NULL,
              visited INTEGER NOT NULL DEFAULT 0, cleared INTEGER NOT NULL DEFAULT 0,
              key_for_lane_id TEXT,
              event_id TEXT, resolved_kind TEXT, resolved_archetype_id TEXT,
              floor_json TEXT NOT NULL DEFAULT '[]',
              revision INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (delve_id, sector_id)
            );
            """);
    }

    /// <summary>
    /// One transaction: validate under the delve profile, insert <c>rpg_delves</c>, insert the
    /// <c>rpg_worlds</c> row (<c>kind='delve'</c>), write the graph, insert <c>rpg_delve_rooms</c>.
    /// Correlation-idempotent like expeditions (spec-expeditions.md §"exactly once") — a replay
    /// with the same <paramref name="correlationId"/> returns the already-recorded row rather than
    /// creating a second one. The seed is sealed by the CALLER (delve-graph-roll rolls it) and
    /// never re-derived here.
    /// </summary>
    public (bool Ok, string Reason, DelveRow? Delve) CreateDelve(
        long playerId, string domainId, string raidMode, string rungId, string correlationId,
        string? parentWorldId, string worldId, string templateId, ulong seed,
        WorldState world, IReadOnlyList<DelveRoomRow> rooms,
        RoomTypeCatalog roomCatalog, DoorTypeCatalog doorCatalog)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return (false, "correlation.missing", null);
        var corr = correlationId.Trim();

        lock (_gate)
        {
            using var db = OpenUnlocked();

            var existing = ReadDelveByCorrelationUnlocked(db, playerId, corr);
            if (existing != null) return (true, "ok.replayed", existing);

            // Validate BEFORE any write — a malformed graph leaves the database untouched
            // (spec-delve-scope.md "Boundaries: Always validate before any write").
            WorldValidation.Validate(world, WorldValidationProfile.Delve(roomCatalog, doorCatalog));

            using var tx = db.BeginTransaction();
            var now = DateTime.UtcNow.ToString("o");

            using (var cmd = Prepared(db, tx, """
                INSERT INTO rpg_worlds (world_id, player_id, template_id, seed, mode, kind, parent_world_id,
                                        current_turn, engine_version, ruleset_version, state, created_utc, revision)
                VALUES ($w, $p, $t, $seed, 'turn', 'delve', $parent, 0, 1, 1, 'active', $now, 0);
                """, "$w", "$p", "$t", "$seed", "$parent", "$now"))
                ExecuteWith(cmd, worldId, playerId, templateId, seed.ToString(), (object?)parentWorldId, now);

            WriteWorldGraphUnlocked(db, tx, world with { WorldId = worldId, TemplateId = templateId, Seed = seed, CurrentTurn = 0 });

            using (var cmd = Prepared(db, tx, """
                INSERT INTO rpg_delves (player_id, world_id, domain_id, raid_mode, rung_id, seed, state,
                                        correlation_id, entered_utc, parties_json)
                VALUES ($p, $w, $d, $raid, $rung, $seed, $state, $corr, $now, '[]');
                """, "$p", "$w", "$d", "$raid", "$rung", "$seed", "$state", "$corr", "$now"))
                ExecuteWith(cmd, playerId, worldId, domainId, raidMode, rungId, seed.ToString(), DelveStates.Active, corr, now);

            var delveId = LastInsertRowId(db, tx);

            using (var cmd = Prepared(db, tx, """
                INSERT INTO rpg_delve_rooms (delve_id, sector_id, row_index, col_index, kind, archetype_id,
                    visited, cleared, key_for_lane_id, floor_json)
                VALUES ($id, $s, $r, $c, $k, $a, $v, $cl, $key, '[]');
                """, "$id", "$s", "$r", "$c", "$k", "$a", "$v", "$cl", "$key"))
            {
                foreach (var room in rooms)
                    ExecuteWith(cmd, delveId, room.SectorId, room.RowIndex, room.ColIndex, room.Kind,
                        room.ArchetypeId, room.Visited ? 1 : 0, room.Cleared ? 1 : 0, (object?)room.KeyForLaneId);
            }

            tx.Commit();
            var created = ReadDelveByCorrelationUnlocked(db, playerId, corr)!;
            return (true, "ok", created);
        }
    }

    public DelveRow? LoadDelve(long delveId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadDelveUnlocked(db, delveId);
        }
    }

    public IReadOnlyList<DelveRoomRow> LoadDelveRooms(long delveId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadDelveRoomsUnlocked(db, delveId);
        }
    }

    /// <summary>
    /// Moves one party from its current room to an adjacent one — an UPDATE of the party's
    /// <see cref="WorldEntity.AtSectorId"/> plus <c>rpg_delve_rooms.visited</c>, one transaction,
    /// through this store, never <c>TurnEngine.Step</c> (spec-delve-scope.md §5). Reuses
    /// <see cref="LaneGate.Refusal"/> — the same door rule the map's march uses — so a gated or
    /// wrong-direction one-way door refuses here exactly as it would on the map.
    /// </summary>
    public (bool Ok, string Reason) MoveParty(long delveId, string worldId, string partyEntityId, string toSectorId, DoorTypeCatalog doorCatalog)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            // LoadWorldGraphUnlocked leaves WorldId/TemplateId/Seed/CurrentTurn at their defaults
            // (the header lives separately) -- WriteWorldGraphUnlocked below keys every row on
            // world.WorldId, so it must be set before the round-trip.
            var world = LoadWorldGraphUnlocked(db, worldId) with { WorldId = worldId };
            var party = world.Entities.FirstOrDefault(e => e.EntityId == partyEntityId);
            if (party?.AtSectorId is not { } at) return (false, "party.not-standing");

            var lane = world.Lanes.FirstOrDefault(l =>
                (l.FromSectorId == at && l.ToSectorId == toSectorId) ||
                (l.ToSectorId == at && l.FromSectorId == toSectorId));
            if (lane is null) return (false, "lane.unknown");

            var doorType = doorCatalog.Get(lane.TypeId);
            if (LaneGate.Refusal(doorType, lane, at) is { } refusal) return (false, refusal.Reason);

            using var tx = db.BeginTransaction();
            // A single UPDATE, not a graph rewrite: WriteWorldGraphUnlocked is clear-and-rewrite
            // for a brand-new world only (CreateWorld/CreateDelve) and has no delete-first step —
            // calling it on a world that already has rows duplicates every one of them. An UPDATE
            // is exactly what spec-delve-scope.md §5 asks for: "an UPDATE of the party's
            // WorldEntity.AtSectorId ... one transaction."
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_world_entities SET at_sector_id = $to, revision = revision + 1 WHERE world_id = $w AND entity_id = $e;",
                "$to", "$w", "$e"))
                ExecuteWith(cmd, toSectorId, worldId, partyEntityId);

            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delve_rooms SET visited = 1, revision = revision + 1 WHERE delve_id = $id AND sector_id = $s;",
                "$id", "$s"))
                ExecuteWith(cmd, delveId, toSectorId);

            tx.Commit();
            return (true, "ok");
        }
    }

    public void MarkRoom(long delveId, string sectorId, bool? visited = null, bool? cleared = null)
    {
        if (visited is null && cleared is null) return;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var sets = new List<string> { "revision = revision + 1" };
            if (visited is { } v) sets.Add($"visited = {(v ? 1 : 0)}");
            if (cleared is { } c) sets.Add($"cleared = {(c ? 1 : 0)}");
            using (var cmd = Prepared(db, tx,
                $"UPDATE rpg_delve_rooms SET {string.Join(", ", sets)} WHERE delve_id = $id AND sector_id = $s;",
                "$id", "$s"))
                ExecuteWith(cmd, delveId, sectorId);
            tx.Commit();
        }
    }

    /// <summary>Appends one entry to the delve-level decision log — route, pack, talk, steer, every
    /// kind spec-delve-graph-roll/wild-room/event-deck/loot-pack name (spec-delve-scope.md §1).
    /// Append-only: the whole array is re-serialised because SQLite JSON is text, matching
    /// `rpg_web_match_log.decisions_json`'s own convention.</summary>
    public void AppendDecision(long delveId, object decision)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var current = ReadDelveJsonColumnUnlocked(db, tx, delveId, "decisions_json");
            var list = JsonSerializer.Deserialize<List<JsonElement>>(current ?? "[]") ?? new List<JsonElement>();
            var appended = list.Select(e => (object)e).Append(decision).ToList();
            var json = JsonSerializer.Serialize(appended);
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET decisions_json = $j, revision = revision + 1 WHERE delve_id = $id;",
                "$j", "$id"))
                ExecuteWith(cmd, json, delveId);
            tx.Commit();
        }
    }

    /// <summary>
    /// Closes a delve — <c>Active -&gt; Extracted|Wiped -&gt; Archived</c> for a <c>once</c> domain
    /// (spec-delve-scope.md §7). This module writes only the state/timestamp transition; the
    /// per-module settlements (loot earn, attrition retire/recover, quest verdicts, domain unlocks)
    /// are each their own module's own writer, called in the order that module's own spec states —
    /// none of those modules exist yet as of this task, so this is the seam they attach to, not a
    /// finished pipeline. `won` and the once/many archive split are the CALLER's decision (the
    /// attrition and domain-catalog specs own that logic); this method only persists the outcome.
    /// </summary>
    public bool CloseDelve(long delveId, string finalState, bool archiveNow)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var now = DateTime.UtcNow.ToString("o");
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET state = $s, closed_utc = $now, revision = revision + 1 WHERE delve_id = $id;",
                "$s", "$now", "$id"))
                ExecuteWith(cmd, archiveNow ? DelveStates.Archived : finalState, now, delveId);
            var rows = DelveRowExistsUnlocked(db, tx, delveId);
            tx.Commit();
            return rows;
        }
    }

    static bool DelveRowExistsUnlocked(SqliteConnection db, SqliteTransaction tx, long delveId)
    {
        using var check = db.CreateCommand();
        check.Transaction = tx;
        check.CommandText = "SELECT COUNT(*) FROM rpg_delves WHERE delve_id = $id;";
        check.Parameters.AddWithValue("$id", delveId);
        return Convert.ToInt64(check.ExecuteScalar()) > 0;
    }

    static long LastInsertRowId(SqliteConnection db, SqliteTransaction tx)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT last_insert_rowid();";
        return (long)cmd.ExecuteScalar()!;
    }

    static string? ReadDelveJsonColumnUnlocked(SqliteConnection db, SqliteTransaction tx, long delveId, string column)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT {column} FROM rpg_delves WHERE delve_id = $id;";
        cmd.Parameters.AddWithValue("$id", delveId);
        return cmd.ExecuteScalar() as string;
    }

    const string SelectDelve = """
        SELECT delve_id, player_id, world_id, domain_id, raid_mode, rung_id, seed, state,
               correlation_id, entered_utc, closed_utc, parties_json, decisions_json,
               souls_unbanked, theta_run, quests_json, content_terms_json, revision
        FROM rpg_delves
        """;

    static DelveRow ReadDelveRow(SqliteDataReader r) => new(
        r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5),
        ulong.TryParse(r.GetString(6), out var seed) ? seed : 0UL, r.GetString(7),
        r.GetString(8), r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10),
        DelveRow.ParsePartiesJson(r.GetString(11)), r.GetString(12),
        r.GetInt64(13), r.GetInt32(14), r.GetString(15), r.IsDBNull(16) ? null : r.GetString(16), r.GetInt64(17));

    DelveRow? ReadDelveUnlocked(SqliteConnection db, long delveId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = SelectDelve + " WHERE delve_id = $id;";
        cmd.Parameters.AddWithValue("$id", delveId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadDelveRow(r) : null;
    }

    DelveRow? ReadDelveByCorrelationUnlocked(SqliteConnection db, long playerId, string correlationId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = SelectDelve + " WHERE player_id = $p AND correlation_id = $c;";
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$c", correlationId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadDelveRow(r) : null;
    }

    static IReadOnlyList<DelveRoomRow> ReadDelveRoomsUnlocked(SqliteConnection db, long delveId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT sector_id, row_index, col_index, kind, archetype_id, visited, cleared,
                   key_for_lane_id, event_id, resolved_kind, resolved_archetype_id, floor_json, revision
            FROM rpg_delve_rooms WHERE delve_id = $id ORDER BY sector_id;
            """;
        cmd.Parameters.AddWithValue("$id", delveId);
        using var r = cmd.ExecuteReader();
        var rows = new List<DelveRoomRow>();
        while (r.Read())
            rows.Add(new DelveRoomRow(
                r.GetString(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3), r.GetString(4),
                r.GetInt32(5) != 0, r.GetInt32(6) != 0, r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9),
                r.IsDBNull(10) ? null : r.GetString(10), r.GetString(11), r.GetInt64(12)));
        return rows;
    }
}
