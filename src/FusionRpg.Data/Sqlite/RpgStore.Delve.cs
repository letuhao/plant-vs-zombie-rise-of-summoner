using System.Text.Json;
using FusionRpg.Core.Delve;
using FusionRpg.Core.Delve.Attrition;
using FusionRpg.Core.Delve.Pack;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Movement;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>D3.22 (spec-loot-pack.md §Structure: "parties_json[p].pack read/write") — one party's
/// pack grid, the JSON element `{rows, cols, cells: [...]}`. Reuses <see cref="PackCell"/> directly
/// (a plain record of primitives) rather than a private copy of the same shape. Floor items are NOT
/// persisted here — they live on `rpg_delve_rooms.floor_json`, already `delve-scope`'s own column for
/// whatever room a party currently stands in.</summary>
public sealed record DelvePartyPackState(int Rows, int Cols, IReadOnlyList<PackCell> Cells);

/// <summary>D4.8 (spec-wild-room.md §6, "Altar pulls as at-risk haul") — one pending altar pull, the
/// spec's own literal shape: "written as `parties_json[p].haul[] += {kind: "pull", speciesId, rarity,
/// variant, traitIds, r, c, n}`." <see cref="Rarity"/> is the wire id (<c>DemonRarityIds.ToId</c>),
/// matching <see cref="FusionRpg.Contracts.DemonMintSpec.Rarity"/>'s own string shape — no separate
/// enum round-trip between a roll and a mint. <see cref="Row"/>/<see cref="Col"/>/<see cref="N"/> are
/// the altar's own coordinates and pull ordinal — "the rng is `dungeon:altar:{r}:{c}:{n}`... replay-
/// safe" (spec, verbatim); nothing else in this schema tracks "how many times has this altar been
/// pulled," so <see cref="RpgStore.PullAtAltar"/> derives <c>n</c> by counting a party's own existing
/// entries at that <c>(r, c)</c> rather than inventing a second counter.</summary>
public sealed record DelveHaulEntry(
    string Kind, string SpeciesId, string Rarity, string Variant, IReadOnlyList<string> TraitIds,
    int Row, int Col, int N);

/// <summary>One party's route/pity/haul, one element of <c>rpg_delves.parties_json</c> per
/// <c>PartyIndex</c> — a single JSON array on the header, not a third table (spec-delve-scope.md
/// §1: "a raid is one, two or four parties written in one transaction with the delve").
///
/// <para><b><see cref="Members"/></b> — delve-attrition D2.23 (spec-delve-attrition.md §1, §7:
/// "each [party] gains `members[]`, one record per demon, read and written only through
/// `RpgStore.Delve.cs`"). Nullable, not <see cref="Array.Empty{T}"/>: a party written before this
/// field existed deserializes to <c>null</c> rather than a lossy empty list a caller could mistake
/// for "this party genuinely has no members."</para>
///
/// <para><b><see cref="Pack"/></b> — loot-pack D3.22, same nullable-not-empty reasoning as
/// <see cref="Members"/>: a party written before this field existed has no pack yet, not an empty
/// one.</para>
///
/// <para><b><see cref="Haul"/></b> — D4.8 (spec-wild-room.md §6): pending altar pulls, "no
/// `UniqueActor` and no phase until `CloseDelve(Extracted)`." A real, already-present positional
/// field on this record from BEFORE this task — every one of this file's own writers already
/// constructed it empty (confirmed by reading every real call site: `WritePartyMembers`,
/// `WritePartyRoute`, `WritePartyPack` all passed an empty array here, and nothing anywhere read it
/// back), so this task RETYPES it from a bare <c>IReadOnlyList&lt;string&gt;</c> to the real
/// <see cref="DelveHaulEntry"/> shape the spec names, rather than adding a second, differently-named
/// list beside an already-unused one. Safe: every persisted row's own `haul` JSON is `[]` today (an
/// empty array deserializes identically regardless of its element type), so no real data changes
/// shape.</para></summary>
public sealed record DelvePartyState(
    long EntityId, IReadOnlyList<string> Route, IReadOnlyDictionary<string, int> Pity, IReadOnlyList<DelveHaulEntry> Haul,
    IReadOnlyList<DelveMemberState>? Members = null, DelvePartyPackState? Pack = null);

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
    /// <summary>The three delve tables (`rpg_delve_pack_lock` added D3.22). Called from
    /// <c>EnsureWorldSchemaUnlocked</c> beside <c>EnsureWorldTurnSchemaUnlocked</c> — a delve world is
    /// a <c>rpg_worlds</c> row, so its own schema setup lives beside the world program's
    /// (spec-delve-scope.md §1).</summary>
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

            CREATE TABLE IF NOT EXISTS rpg_delve_pack_lock (
              delve_id INTEGER NOT NULL,
              instance_id TEXT NOT NULL PRIMARY KEY
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_delve_pack_lock_delve ON rpg_delve_pack_lock(delve_id);
            """);
    }

    /// <summary>
    /// One transaction: validate under the delve profile, insert <c>rpg_delves</c>, insert the
    /// <c>rpg_worlds</c> row (<c>kind='delve'</c>), write the graph, insert <c>rpg_delve_rooms</c>.
    /// Correlation-idempotent like expeditions (spec-expeditions.md §"exactly once") — a replay
    /// with the same <paramref name="correlationId"/> returns the already-recorded row rather than
    /// creating a second one. The seed is sealed by the CALLER (delve-graph-roll rolls it) and
    /// never re-derived here.
    ///
    /// <para>D4.21 (spec-domain-catalog.md §6 step 7) added the four optional, trailing parameters:
    /// <paramref name="contentTermsJson"/> writes the already-existing-but-never-populated
    /// `content_terms_json` column (confirmed by direct read: every prior caller left it `NULL`);
    /// <paramref name="firstDecisionJson"/> seeds `decisions_json` with the `{seq:0, kind:"enter",
    /// ...}` entry `DelveStart.Run`'s own plan carries, rather than the bare `'[]'` every existing
    /// call leaves it at; <paramref name="packLockInstanceIds"/> and <paramref name="stockDebits"/>
    /// compose <see cref="LockPackInstanceUnlocked"/> / <see cref="AdjustStockUnlocked"/> into THIS
    /// SAME transaction, after `delveId` exists — "one transaction" (spec §6 step 7, verbatim), not a
    /// second one a caller could partially fail between. All four default to their old no-op values,
    /// so every existing caller (`DelveGraphRollRoundTripTests.cs`, `DelveScopeTests.cs`, etc.) is
    /// byte-for-byte unaffected.</para>
    /// </summary>
    public (bool Ok, string Reason, DelveRow? Delve) CreateDelve(
        long playerId, string domainId, string raidMode, string rungId, string correlationId,
        string? parentWorldId, string worldId, string templateId, ulong seed,
        WorldState world, IReadOnlyList<DelveRoomRow> rooms,
        RoomTypeCatalog roomCatalog, DoorTypeCatalog doorCatalog,
        string? contentTermsJson = null, string? firstDecisionJson = null,
        IReadOnlyList<string>? packLockInstanceIds = null, IReadOnlyList<(string ContainerId, int Qty)>? stockDebits = null)
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
                                        correlation_id, entered_utc, parties_json, decisions_json, content_terms_json)
                VALUES ($p, $w, $d, $raid, $rung, $seed, $state, $corr, $now, '[]', $dec, $terms);
                """, "$p", "$w", "$d", "$raid", "$rung", "$seed", "$state", "$corr", "$now", "$dec", "$terms"))
                ExecuteWith(cmd, playerId, worldId, domainId, raidMode, rungId, seed.ToString(), DelveStates.Active, corr, now,
                    firstDecisionJson ?? "[]", (object?)contentTermsJson);

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

            if (packLockInstanceIds is not null)
                foreach (var instanceId in packLockInstanceIds)
                    LockPackInstanceUnlocked(db, delveId, instanceId);

            if (stockDebits is not null)
                foreach (var (containerId, qty) in stockDebits)
                    AdjustStockUnlocked(db, playerId.ToString(), containerId, -qty, now);

            tx.Commit();
            var created = ReadDelveByCorrelationUnlocked(db, playerId, corr)!;
            return (true, "ok", created);
        }
    }

    /// <summary>D4.21 (spec-domain-catalog.md §6 step 3, `member.unavailable`) — "is this actor
    /// currently a member of ANY of this player's own `Active` delves". No existing read answered
    /// this (`rpg_delve_pack_lock` is keyed by ITEM instance, never actor instance, confirmed by
    /// direct read of its own schema and writer); parties are stored as `parties_json` on each
    /// `rpg_delves` row, not a queryable column, so this scans every Active row's own JSON rather
    /// than a single indexed WHERE — acceptable here since a player has at most a handful of
    /// concurrently-Active delves (one per domain, spec §4).</summary>
    public bool IsActorInAnyActiveDelve(long playerId, string instanceId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT parties_json FROM rpg_delves WHERE player_id = $p AND state = $active;";
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$active", DelveStates.Active);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var parties = JsonSerializer.Deserialize<List<DelvePartyState>>(r.GetString(0)) ?? new List<DelvePartyState>();
                if (parties.Any(p => p.Members?.Any(m => string.Equals(m.InstanceId, instanceId, StringComparison.Ordinal)) == true))
                    return true;
            }
            return false;
        }
    }

    /// <summary>D4.22 — the live `(state, delveId)` pair `DomainOffers.For`'s own `delves` parameter
    /// needs, for one `(player, domain)`. `Active`/`Archived` are the only two states that DTO
    /// projection reads (spec §4); a `Wiped`/`Extracted` row (or none at all) reads back `(null,
    /// null)` — sealed-or-in-progress is a property of the LATEST row for this domain, so this picks
    /// the most recently entered one rather than an arbitrary one when a `many` domain has several
    /// closed delves in its own history.</summary>
    public (string? State, long? DelveId) GetDelveStateForDomain(long playerId, string domainId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT state, delve_id FROM rpg_delves
                WHERE player_id = $p AND domain_id = $d
                ORDER BY entered_utc DESC LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$d", domainId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? (r.GetString(0), r.GetInt64(1)) : (null, null);
        }
    }

    /// <summary>D4.22 — the public form of <see cref="ReadDelveByCorrelationUnlocked"/>, for a caller
    /// outside this file (`DelveEndpoints.cs`'s own replay lookup, matching the spec's own "a replay
    /// returns the recorded delve" step 1) that needs the lookup WITHOUT also creating a row.</summary>
    public DelveRow? LoadDelveByCorrelation(long playerId, string correlationId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadDelveByCorrelationUnlocked(db, playerId, correlationId);
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

    /// <summary>D4.8 (spec-wild-room.md §7): <paramref name="resolvedKind"/> lets a caller record
    /// `rpg_delve_rooms.resolved_kind` — `'cage'` is the wild-room module's own new legal value,
    /// written the same way `visited`/`cleared` already are, no schema change (the column has always
    /// been a bare `TEXT`, never `CHECK`-constrained).</summary>
    public void MarkRoom(long delveId, string sectorId, bool? visited = null, bool? cleared = null, string? resolvedKind = null)
    {
        if (visited is null && cleared is null && resolvedKind is null) return;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var sets = new List<string> { "revision = revision + 1" };
            if (visited is { } v) sets.Add($"visited = {(v ? 1 : 0)}");
            if (cleared is { } c) sets.Add($"cleared = {(c ? 1 : 0)}");
            if (resolvedKind is not null) sets.Add("resolved_kind = $rk");
            var paramNames = resolvedKind is not null ? new[] { "$id", "$s", "$rk" } : new[] { "$id", "$s" };
            using (var cmd = Prepared(db, tx,
                $"UPDATE rpg_delve_rooms SET {string.Join(", ", sets)} WHERE delve_id = $id AND sector_id = $s;",
                paramNames))
            {
                if (resolvedKind is not null) ExecuteWith(cmd, delveId, sectorId, resolvedKind);
                else ExecuteWith(cmd, delveId, sectorId);
            }
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

    /// <summary>D4.14 (spec-delve-quests.md §2, §4): "the offer persists as `rpg_delves.quests_json`
    /// (ids in draw order, `need` per quest)... `quests_json` gains the verdicts" at close. `Done`/
    /// `Have` are `null` until `WriteQuestVerdicts` fills them in — the stored offer is the truth
    /// D4.10's `Draw` produced; nothing here re-derives it.</summary>
    public sealed record QuestJsonRow(string QuestId, int Need, bool? Done = null, int? Have = null);

    /// <summary>Overwrites `quests_json` with the delve's own offer, in draw order — called once, at
    /// `CreateDelve` (D4.10's own `Draw` output, `domain-catalog`'s still-unbuilt wiring).</summary>
    public void WriteQuestOffer(long delveId, IReadOnlyList<QuestJsonRow> offer)
    {
        if (offer is null) throw new ArgumentNullException(nameof(offer));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var json = JsonSerializer.Serialize(offer);
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET quests_json = $q, revision = revision + 1 WHERE delve_id = $id;", "$q", "$id"))
                ExecuteWith(cmd, json, delveId);
            tx.Commit();
        }
    }

    /// <summary>The stored offer, read back exactly as written — "the stored offer is truth and a
    /// rebuild is asserted equal on load" (spec §2, verbatim) is a property of THIS round-trip, not
    /// a claim about re-deriving the offer from the graph again.</summary>
    public IReadOnlyList<QuestJsonRow> ReadQuestOffer(long delveId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var json = ReadDelveJsonColumnUnlocked(db, tx, delveId, "quests_json");
            return JsonSerializer.Deserialize<List<QuestJsonRow>>(json ?? "[]") ?? new List<QuestJsonRow>();
        }
    }

    /// <summary>Merges each quest's own verdict into the already-stored offer — called once, at
    /// `CloseDelve` (D4.11's own `Evaluate` output per offered quest). A quest id the stored offer
    /// does not contain is silently ignored, never appended — the offer's own draw-order membership,
    /// fixed at `CreateDelve`, is what `quests_json` is truth for; a verdict cannot grow the list.</summary>
    public void WriteQuestVerdicts(long delveId, IReadOnlyDictionary<string, (bool Done, int Have)> verdictsByQuestId)
    {
        if (verdictsByQuestId is null) throw new ArgumentNullException(nameof(verdictsByQuestId));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var current = ReadDelveJsonColumnUnlocked(db, tx, delveId, "quests_json");
            var rows = JsonSerializer.Deserialize<List<QuestJsonRow>>(current ?? "[]") ?? new List<QuestJsonRow>();
            var updated = rows.Select(r => verdictsByQuestId.TryGetValue(r.QuestId, out var v)
                ? r with { Done = v.Done, Have = v.Have }
                : r).ToList();
            var json = JsonSerializer.Serialize(updated);
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET quests_json = $q, revision = revision + 1 WHERE delve_id = $id;", "$q", "$id"))
                ExecuteWith(cmd, json, delveId);
            tx.Commit();
        }
    }

    /// <summary>D4.12 (spec-delve-quests.md §4) — everything <see cref="ApplyQuestRewardBankingUnlocked"/>
    /// needs that this Data-layer file cannot derive itself. `quests_json` only persists
    /// <c>{questId, need, done, have}</c> (<see cref="QuestJsonRow"/>) — D4.9's own full
    /// <c>QuestRow</c>, carrying <c>RewardBand</c>, lives in a Core-layer catalog this file must never
    /// load from disk itself, matching every other "read model owned elsewhere" delegate this program
    /// already uses (<c>DomainQuestPreflightBridge</c>'s own <c>archetypeEventPoolHasKind</c>/
    /// <c>lootBindingOffersRole</c>, <c>LootContentView.BaseTypesFor</c>, etc.) — so <see cref="ResolveQuest"/>
    /// is a plain caller-supplied lookup (<c>QuestCatalog.Resolve</c> in production, a hand-built
    /// dictionary in a test). <see cref="View"/>/<see cref="Drops"/>/<see cref="RoleFamilyCells"/>/
    /// <see cref="MintTuning"/> are the real `mintAt` ingredients — party-dungeon-todo.md's own D4.12
    /// entry (2026-09-07) already names these as having NO live `Program.cs` wiring yet for ANY caller,
    /// not just this one ("Program.cs itself does not yet compute or thread roleFamilyCells into a live
    /// caller"); supplying them here, explicitly, keeps that gap honest rather than guessing at boot-time
    /// plumbing this task does not own. <c>RewardBandsByMember</c> is deliberately NOT a field here:
    /// <see cref="CloseDelve"/>'s own <c>tuning</c> parameter already carries it
    /// (<c>DungeonTuning.QuestsRewardBand</c>) — threading a second copy would risk the two disagreeing.</summary>
    public sealed record QuestRewardBankingInputs(
        Func<string, FusionRpg.Core.Delve.Quests.QuestRow?> ResolveQuest,
        FusionRpg.Core.Items.Drops.LootContentView View,
        FusionRpg.Core.Items.Drops.DropVolumeTuning Drops,
        IReadOnlyList<FusionRpg.Core.Items.RoleFamilyCell> RoleFamilyCells,
        FusionRpg.Core.Power.PowerTuning MintTuning);

    /// <summary>
    /// D4.12/D4.14 (spec-delve-quests.md §4; party-dungeon-todo.md D4.12's own final remaining piece,
    /// closed here) — the quest-reward half of <see cref="CloseDelve"/>'s own hook order, banking every
    /// `Done`-verdict quest's own reward as one more `LootPipeline` request on the SAME transaction as
    /// pack settlement, attrition and loot/souls earn. Spec's own full stated order is
    /// "pack/attrition/souls/quest-verdicts/domain-unlocks" (see <see cref="CloseDelve"/>'s own doc
    /// comment) — this hook is the fourth of those five, slotted in right after
    /// <see cref="ApplyLootEarnUnlocked"/> and before the still-unbuilt domain-unlocks step, and before
    /// <see cref="ApplyHaulMintUnlocked"/>'s own bolt-on (that hook is not part of the spec's numbered
    /// order at all, per its own doc comment).
    ///
    /// <para><b>Gated behind TWO independent switches, not one.</b> <paramref name="tuning"/> not null
    /// (the SAME byte-identical-without-tuning contract every other hook already honors:
    /// <c>tuning.QuestsRewardBand</c> is this hook's own reward-band source, so it cannot run without
    /// it) AND <paramref name="inputs"/> not null (checked by <see cref="CloseDelve"/> before this method
    /// is even invoked). The second gate exists because a real <c>LootContentView</c>/
    /// <c>DropVolumeTuning</c>/<c>roleFamilyCells</c>/mint <c>PowerTuning</c> has no live `Program.cs`
    /// wiring yet for ANY caller — D4.12's own todo entry names this precisely — so every EXISTING
    /// `tuning`-only caller (D3.16/D3.22/D4.8's own already-shipped tests, none of which know about
    /// quest rewards) stays byte-identical.</para>
    ///
    /// <para><b>Only banks on `Extracted`</b> — mirroring <see cref="ApplyLootEarnUnlocked"/>'s own "a
    /// wipe forfeits the whole pot" rule and D4.8's own "Wiped drops the rows WITH the haul": a quest
    /// can go `Done` mid-delve (its verdict is evaluated and persisted before extraction, D4.14's own
    /// <see cref="WriteQuestVerdicts"/>) and still be lost entirely if the party never makes it out.
    /// `Wiped` (or any other `finalState`) is a plain no-op here — there is no `*_unbanked` column for
    /// quest rewards to reset the way `souls_unbanked` needs resetting, since nothing is accrued between
    /// rooms.</para>
    ///
    /// <para><b>A quest that cannot be resolved into a real reward is skipped, never thrown.</b> An
    /// unresolvable quest id (<paramref name="inputs"/>'s own <c>ResolveQuest</c> returns null), a
    /// domain with no `cache` lootBinding entry, or a `RewardBand` absent from `tuning.QuestsRewardBand`
    /// all name the SAME "provably correct, zero real production content yet" gap this whole session has
    /// already used dozens of times — the six real shipped domains all correctly refuse
    /// `DomainPreflight` today (D4.17 rows 5/6/8/10), so nothing here can be exercised against real
    /// content in production, only hand-built fixtures. A skip must never abort the REST of this
    /// extraction — attrition/pack/souls are already committed earlier in this SAME call and must not
    /// roll back because one quest's own reward could not be assembled.</para>
    ///
    /// <para><b>Replay-safety rides `LootPipeline.Resolve`'s own step-1 idempotency gate</b>
    /// (`view.RecordedManifestFor`) — a replayed <see cref="CloseDelve"/> re-derives the SAME
    /// deterministic `dungeon:loot:quest:{questId}` seed and the SAME correlation id, finds the manifest
    /// already recorded, and returns it with empty `Grants` WITHOUT calling `mintAt` again — so this
    /// method never mints a second, orphaned instance for an already-banked quest. This requires
    /// <paramref name="inputs"/>'s own `View.RecordedManifestFor` to be wired to the real store (built
    /// via <see cref="BuildLiveLootContentView"/>, never a stripped test double missing that delegate) —
    /// proven directly by this task's own replay test, not merely asserted.</para>
    ///
    /// <para><b>`ReadLootBinding`/`GetLootPity`/`GetCatalogRevision` are called self-locked (their own
    /// short-lived second connection), not threaded through as `Unlocked` siblings on this method's own
    /// `db`.</b> Each runs exactly ONCE per call, before the loop below does any writing — reading
    /// already-committed state through a second connection before this hook's own writes start is safe
    /// regardless of connection. <b>This is NOT the same shape as a self-locked read called REPEATEDLY
    /// inside the loop</b> — see the `RecordedManifestFor` rebind and the armoury-count fix immediately
    /// below, both real, reproduced bugs of exactly that second shape, caught by this task's own
    /// multi-quest-reward test and fixed by reading through this hook's own `(db, tx)` instead.</para>
    /// </summary>
    void ApplyQuestRewardBankingUnlocked(
        SqliteConnection db, SqliteTransaction tx, DelveRow delve, string finalState,
        FusionRpg.Core.Dungeon.Tuning.DungeonTuning tuning, QuestRewardBankingInputs inputs, string now)
    {
        if (!string.Equals(finalState, DelveStates.Extracted, StringComparison.Ordinal)) return;

        var offer = JsonSerializer.Deserialize<List<QuestJsonRow>>(delve.QuestsJson ?? "[]") ?? new List<QuestJsonRow>();
        if (offer.Count == 0) return;

        var lootBinding = ReadLootBinding(delve.DomainId);
        if (!lootBinding.ContainsKey("cache")) return; // named above: an honest, expected-today content gap

        var playerId = delve.PlayerId.ToString();
        var catalogRevision = GetCatalogRevision();
        var pity = GetLootPity(playerId);

        // `inputs.View.RecordedManifestFor` (typically `BuildLiveLootContentView`'s own
        // `RecordedLootManifest`) is SELF-LOCKING -- it opens its own second connection. That reads fine
        // for a lone call, but `LootPipeline.Resolve`'s own step-1 idempotency gate calls it on EVERY
        // roll in this loop, including a SECOND quest's roll after the FIRST quest's own
        // `PersistLootUnlocked` has already written (uncommitted) into `item_drop_log` on THIS
        // transaction -- a real, reproduced bug (`SQLite Error 6: database table is locked:
        // item_drop_log`, caught by this task's own two-quest test). Fixed by reading the SAME check
        // through this hook's own `(db, tx)` instead, mirroring `PersistLootUnlocked`'s own existing-row
        // SELECT exactly (`RpgStore.Loot.cs`). The identical bug, same root cause, was also found and
        // fixed one call later in this same loop -- see `AcquireItemUnlocked`'s own updated doc comment
        // (`RpgStore.Items.cs`) for the armoury-row-count half of this same fix.
        var view = inputs.View with { RecordedManifestFor = (p, c) => RecordedLootManifestUnlocked(db, tx, p, c) };

        foreach (var row in offer)
        {
            if (row.Done != true) continue;
            var quest = inputs.ResolveQuest(row.QuestId);
            if (quest is null) continue; // unresolvable quest id -- content gap, not a crash
            if (!tuning.QuestsRewardBand.ContainsKey(quest.RewardBand)) continue; // ditto

            var reward = FusionRpg.Core.Delve.Quests.QuestReward.Request(
                quest, delve.DelveId, lootBinding, tuning.QuestsRewardBand, view.Ladder, delve.ThetaRun);

            FusionRpg.Core.Items.Drops.LootMintResult MintAt(FusionRpg.Core.Items.Drops.LootGrant grant, int theta) =>
                MintGrantUnlocked(db, tx, grant, theta, inputs.RoleFamilyCells, inputs.MintTuning);

            var rejection = FusionRpg.Core.Delve.Loot.DelveLoot.RollQuestReward(
                reward, quest.QuestId, delve.Seed, playerId, delve.ThetaRun,
                view, inputs.Drops, pity, MintAt, catalogRevision, dropTableRevision: 0, out var manifest);
            if (!rejection.IsOk || manifest is null) continue; // structural drop-table refusal -- skip, don't abort the close

            foreach (var grant in manifest.Grants)
            {
                if (grant.InstanceId is not { Length: > 0 } instanceId) continue;
                AcquireItemUnlocked(db, tx, new RpgItemRow
                {
                    InstanceId = instanceId, PlayerId = playerId, AcquiredUtc = now,
                    OriginKind = "quest-reward", OriginRef = reward.Source.SourceId,
                });
            }

            PersistLootUnlocked(db, tx, playerId, manifest, reward.Source.SourceKind, reward.Source.SourceId,
                catalogRevision, dropTableRevision: 0, Array.Empty<ItemGenerationRow>(), now);

            pity = manifest.PityOut; // chain pity forward across multiple quest rewards in the same close
        }
    }

    /// <summary>Same read as <see cref="RecordedLootManifest"/>, on the caller's own connection/
    /// transaction — <see cref="ApplyQuestRewardBankingUnlocked"/>'s own named fix for a real, reproduced
    /// cross-connection lock (see that method's own doc comment). Mirrors <see cref="PersistLootUnlocked"/>'s
    /// own existing-row SELECT exactly (same table, same WHERE, same explicit `Transaction` assignment).</summary>
    static string? RecordedLootManifestUnlocked(SqliteConnection db, SqliteTransaction tx, string playerId, string correlationId)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT result_json FROM item_drop_log WHERE player_id = $p AND correlation_id = $c;";
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$c", correlationId);
        return cmd.ExecuteScalar() as string;
    }

    /// <summary>
    /// Closes a delve — <c>Active -&gt; Extracted|Wiped -&gt; Archived</c> for a <c>once</c> domain
    /// (spec-delve-scope.md §7). <paramref name="finalState"/> is the CALLER's own decision — this
    /// method never re-derives wipe from member state, matching D2.21's own "a wipe is its own
    /// settlement path" (the room loop that ends the delve is the one place that already knows every
    /// party's live state; re-deriving it here from a stale `parties_json` snapshot would be a second,
    /// possibly-disagreeing source of truth). <paramref name="tuning"/> is the single opt-in switch for
    /// every extraction-time settlement this seam has grown since D2.21: <c>null</c> keeps the
    /// pre-D2.23 caller shape byte-identical (only the trailing state UPDATE runs). Supplying it runs,
    /// in this SAME transaction, in the spec's own stated hook order:
    /// <list type="number">
    /// <item>D3.22's own pack settlement (spec-loot-pack.md §7) — see
    /// <see cref="ApplyPackSettlementUnlocked"/>.</item>
    /// <item>D2.23's attrition settlement (spec-delve-attrition.md §7, §9) — every member's
    /// Retire/Recover/Roster outcome, contract loyalty credited once, persisted cross-delve pools, and
    /// every OTHER delve-recovering actor this player owns aged one delve.</item>
    /// <item>D3.16's own souls settlement (spec-dungeon-loot.md §7, `:213-219`) — see
    /// <see cref="ApplyLootEarnUnlocked"/>.</item>
    /// <item>D4.12's own quest-reward banking (spec-delve-quests.md §4) — see
    /// <see cref="ApplyQuestRewardBankingUnlocked"/>. Only runs when <paramref name="questRewards"/> is
    /// ALSO supplied (a second, independent gate beyond <paramref name="tuning"/> — see that method's
    /// own doc comment for why), so every existing `tuning`-only caller stays byte-identical.</item>
    /// <item>D4.8's own altar-haul minting (spec-wild-room.md §6) — see
    /// <see cref="ApplyHaulMintUnlocked"/>. Not itself one of the five items spec-dungeon-loot.md
    /// §7's own numbered order names (that list is pack/attrition/souls/quest-verdicts/domain-unlocks)
    /// — wild-room's own §6 is a separate spec naming a separate, independent side effect ("no
    /// `UniqueActor`... until `CloseDelve(Extracted)`"), so it is appended after the four above
    /// rather than interleaved into an order that never mentioned it.</item>
    /// </list>
    /// The spec's own full stated hook order names quest verdicts right after souls and before domain
    /// unlocks — `domain-catalog`'s own unlock step is the one piece of that order still genuinely
    /// unbuilt (a separate, still-unbuilt module), named here as an honest gap rather than silently
    /// skipped.
    /// </summary>
    public bool CloseDelve(long delveId, string finalState, bool archiveNow,
        FusionRpg.Core.Dungeon.Tuning.DungeonTuning? tuning = null, QuestRewardBankingInputs? questRewards = null)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var now = DateTime.UtcNow.ToString("o");

            if (tuning is not null)
            {
                var delve = ReadDelveUnlocked(db, delveId);
                if (delve is not null)
                {
                    ApplyPackSettlementUnlocked(db, tx, delve, finalState, now);
                    SettleExtractionUnlocked(db, delve, finalState, tuning, now);
                    ApplyLootEarnUnlocked(db, delve, finalState, now);
                    if (questRewards is not null)
                        ApplyQuestRewardBankingUnlocked(db, tx, delve, finalState, tuning, questRewards, now);
                    ApplyHaulMintUnlocked(db, delve, finalState, now);
                }
            }

            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET state = $s, closed_utc = $now, revision = revision + 1 WHERE delve_id = $id;",
                "$s", "$now", "$id"))
                ExecuteWith(cmd, archiveNow ? DelveStates.Archived : finalState, now, delveId);
            var rows = DelveRowExistsUnlocked(db, tx, delveId);
            tx.Commit();
            return rows;
        }
    }

    /// <summary>
    /// D2.23's own settlement pass, one call per member across every party of the raid. Spirit's
    /// resolved value for the affliction check is read straight off <c>DelveMemberState.Pools["spirit"]</c>
    /// — the same value `NerveLadder.StageFor` itself would read at the last room this member fought
    /// in, since nothing DRAINS spirit between the last room and extraction.
    /// </summary>
    void SettleExtractionUnlocked(
        SqliteConnection db, DelveRow delve, string finalState, FusionRpg.Core.Dungeon.Tuning.DungeonTuning tuning, string now)
    {
        var wiped = string.Equals(finalState, DelveStates.Wiped, StringComparison.Ordinal);
        var extracted = string.Equals(finalState, DelveStates.Extracted, StringComparison.Ordinal);
        var rooms = ReadDelveRoomsUnlocked(db, delve.DelveId).ToDictionary(r => r.SectorId, StringComparer.Ordinal);
        var thresholds = tuning.AttritionNerve.StageThresholds;

        // R6 (spec-delve-attrition.md §7): every OTHER recovering actor this player owns also ages one
        // delve, in this SAME transaction — regardless of which demons this particular delve carried.
        DecrementAllRecoveringForPlayerUnlocked(db, delve.PlayerId, now);

        // No domain-catalog exists yet (Phase 4, unbuilt) to supply a real per-domain permadeath
        // override -- an explicit "no override" placeholder, not a silent default (SoulSinkPolicy.
        // VanillaPvzTheta's own precedent): PermadeathFromRungOverride stays null, so
        // PermadeathGate.Applies falls back to dungeon.Domain.PermadeathFromRung, which is correct
        // today because no domain has ever been able to raise the gate.
        var domainInputs = new FusionRpg.Core.Delve.Difficulty.DomainThetaInputs(EntranceBand: 0, IsOnceEntry: false);
        var permadeathApplies = FusionRpg.Core.Delve.Difficulty.PermadeathGate.Applies(tuning, domainInputs, delve.RungId);

        var wonMembers = new List<string>();
        var lostMembers = new List<string>();

        foreach (var party in delve.Parties)
        {
            if (party.Members is not { Count: > 0 } members) continue;

            var (bossKilled, routeHalf) = RouteFacts(party.Route, rooms);

            foreach (var member in members)
            {
                if (!member.Pools.TryGetValue("spirit", out var spirit))
                    throw new ArgumentException($"member '{member.InstanceId}' has no 'spirit' pool.", nameof(delve));
                var stage = FusionRpg.Core.Delve.Attrition.NerveLadder.StageFor(member.NerveStacks, spirit, thresholds);
                var afflicted = stage == thresholds.Count - 1;

                var settlement = FusionRpg.Core.Delve.Attrition.ExtractionSettlement.Decide(
                    downedOnce: member.DownedOnce || wiped,
                    permadeathApplies: permadeathApplies,
                    downedRecoveryDelves: tuning.RiskDownedRecoveryDelves,
                    afflicted: afflicted,
                    extracted: extracted,
                    bossKilled: bossKilled,
                    routeAtLeastHalfCleared: routeHalf);

                switch (settlement.Outcome)
                {
                    case FusionRpg.Core.Delve.Attrition.SettlementOutcome.Retire:
                        RetireUniqueActorUnlocked(db, member.InstanceId);
                        break;
                    case FusionRpg.Core.Delve.Attrition.SettlementOutcome.Recover:
                        BeginUniqueRecoveryUnlocked(db, delve.PlayerId, member.InstanceId, settlement.RecoverDelves, delve.DelveId, delve.ThetaRun);
                        break;
                    case FusionRpg.Core.Delve.Attrition.SettlementOutcome.Roster:
                        break; // already a live roster member -- nothing to transition
                }

                // S2-7: only the tuning-declared subset persists across delves -- every other id
                // starts fresh at resource.max next time, per HungerCharge's own established contract.
                var persisted = tuning.AttritionPersistAcrossDelves
                    .Where(member.Pools.ContainsKey)
                    .ToDictionary(id => id, id => member.Pools[id], StringComparer.Ordinal);
                WritePersistedPoolsUnlocked(db, member.InstanceId, persisted);

                (settlement.Won ? wonMembers : lostMembers).Add(member.InstanceId);
            }
        }

        // §9: loyalty applied once per delve, here -- grouped by `won` since ApplyContractResults
        // takes one verdict for its whole instanceIds list, and two members of the SAME delve can
        // legitimately disagree (one afflicted, one not).
        if (wonMembers.Count > 0) ApplyContractResultsUnlocked(db, delve.PlayerId, wonMembers, won: true, DateTimeOffset.Parse(now));
        if (lostMembers.Count > 0) ApplyContractResultsUnlocked(db, delve.PlayerId, lostMembers, won: false, DateTimeOffset.Parse(now));
    }

    /// <summary>
    /// D3.16 (spec-dungeon-loot.md §7, `:213-219`) — the souls half of <see cref="CloseDelve"/>'s own
    /// extraction settlement, called right after <see cref="SettleExtractionUnlocked"/> in the SAME
    /// transaction. Reads <c>PowerTuningHub.Tuning</c> directly (matching
    /// <c>ApplySoulEarnFromActivityUnlocked</c>'s own established convention in RpgStore.Souls.cs) —
    /// never the <see cref="FusionRpg.Core.Dungeon.Tuning.DungeonTuning"/> that <see cref="CloseDelve"/>
    /// itself takes, which is a different tuning object entirely.
    ///
    /// <para>Spec, verbatim: "Extracted → §2's two AwardSouls rows, then souls_unbanked := 0; Wiped →
    /// souls_unbanked := 0, no award." A wipe forfeits the WHOLE at-risk pot, not just the victory
    /// bonus — "souls earned in a delve are at risk until extraction; a wipe forfeits them with the
    /// haul; an extraction banks them once" (spec §7's own closing rule).</para>
    ///
    /// <para>Writes go through <see cref="AppendSoulLedgerUnlocked"/> directly, never the public,
    /// self-locking <see cref="AwardSouls"/> — calling <c>AwardSouls</c> here would open a SECOND,
    /// independently-committing transaction nested inside this one, breaking the "single transaction,
    /// all-or-nothing" requirement this method exists to satisfy. Each append is dedupe-keyed by delve
    /// id ("the dedupe keys make a replayed close idempotent", spec §7) and guarded by
    /// <see cref="GuardSoulAwardOrThrow"/> first — the same overflow-headroom check <c>AwardSouls</c>
    /// itself uses ("shared by every path that can credit a balance"); this is a new such path.</para>
    /// </summary>
    void ApplyLootEarnUnlocked(SqliteConnection db, DelveRow delve, string finalState, string now)
    {
        if (string.Equals(finalState, DelveStates.Extracted, StringComparison.Ordinal))
        {
            var tuning = FusionRpg.Core.Power.PowerTuningHub.Tuning;
            var earn = FusionRpg.Core.Delve.Loot.DelveSoulLedger.AtExtraction(
                delve.SoulsUnbanked, delve.ThetaRun, won: true, tuning);
            var balance = ReadSoulBalanceUnlocked(db, delve.PlayerId).Balance;

            if (earn.Kills > 0)
            {
                GuardSoulAwardOrThrow(balance, earn.Kills);
                if (AppendSoulLedgerUnlocked(db, delve.PlayerId, 0, earn.Kills,
                        FusionRpg.Core.Demons.SoulEarnPolicy.Reasons.Kill, "delve", delve.DelveId.ToString(),
                        $"delve:{delve.DelveId}:kills", now))
                    balance += earn.Kills;
            }

            if (earn.Victory > 0)
            {
                GuardSoulAwardOrThrow(balance, earn.Victory);
                AppendSoulLedgerUnlocked(db, delve.PlayerId, 0, earn.Victory,
                    FusionRpg.Core.Demons.SoulEarnPolicy.Reasons.Victory, "delve", delve.DelveId.ToString(),
                    $"delve:{delve.DelveId}:victory", now);
            }
        }

        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE rpg_delves SET souls_unbanked = 0, revision = revision + 1 WHERE delve_id = $id;";
        cmd.Parameters.AddWithValue("$id", delve.DelveId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// D4.8 (spec-wild-room.md §6) — the 4th `CloseDelve` hook this task adds, alongside pack
    /// settlement/attrition/loot earn (an independent concern; `CloseDelve`'s own doc comment already
    /// names the spec's FULL stated hook order as also including quest verdicts and domain unlocks,
    /// both separate, still-unbuilt modules — this hook slots in beside the three that already exist,
    /// not ranked against any of them). Spec, verbatim: "no `UniqueActor` and no phase until
    /// `CloseDelve(Extracted)`, where `RpgStore.Delve` calls `MintDemonUnlocked` per row (`Origin =
    /// "delve"`, `Level` null — a pull is a pull), the free auto-bind and discovery souls as the summon
    /// path pays them." `Wiped` drops the rows WITH the haul (spec-loot-pack.md `:88`) — the pity
    /// advance and the spend already happened at pull time (<see cref="PullAtAltar"/>) and are NOT
    /// undone here; only delivery was at risk.
    ///
    /// <para>Every party's haul list is cleared UNCONDITIONALLY at the end (both `Extracted` and
    /// `Wiped`) — mirroring <see cref="ApplyLootEarnUnlocked"/>'s own "always reset regardless of
    /// state" shape for `souls_unbanked`. There is no per-row dedupe key the way
    /// <see cref="AppendSoulLedgerUnlocked"/> gives souls their own "a replayed close does not
    /// double-pay" guarantee — <see cref="MintDemonUnlocked"/> always inserts a fresh row — so a
    /// replayed `CloseDelve` mints nothing the SECOND time for the identical reason it does not pay
    /// souls twice: nothing is left in the list to re-apply.</para>
    /// </summary>
    void ApplyHaulMintUnlocked(SqliteConnection db, DelveRow delve, string finalState, string now)
    {
        if (!delve.Parties.Any(p => p.Haul.Count > 0)) return;
        var extracted = string.Equals(finalState, DelveStates.Extracted, StringComparison.Ordinal);

        if (extracted)
        {
            foreach (var party in delve.Parties)
            {
                foreach (var entry in party.Haul)
                {
                    var species = FusionRpg.Core.Demons.DemonSpeciesCatalog.Get(entry.SpeciesId);
                    var spec = new FusionRpg.Contracts.DemonMintSpec
                    {
                        SpeciesId = species.SpeciesId,
                        Side = species.Side,
                        GameTypeId = species.GameTypeId,
                        Rarity = entry.Rarity,
                        Variant = entry.Variant,
                        ElementPrimary = FusionRpg.Core.Stats.Derived.ElementTypeIdExtensions.ToElementId(species.ElementPrimary),
                        ElementSecondary = species.ElementSecondary is { } es
                            ? FusionRpg.Core.Stats.Derived.ElementTypeIdExtensions.ToElementId(es)
                            : null,
                        TraitIds = entry.TraitIds.ToList(),
                        Origin = "delve",
                    };
                    MintDemonUnlocked(db, delve.PlayerId, spec, now, out var newlyDiscovered);

                    if (newlyDiscovered)
                    {
                        var reward = FusionRpg.Core.Demons.SoulEarnPolicy.DiscoveryDelta(species.BaseRarity);
                        if (reward > 0)
                        {
                            GuardSoulAwardOrThrow(ReadSoulBalanceUnlocked(db, delve.PlayerId).Balance, reward);
                            AppendSoulLedgerUnlocked(db, delve.PlayerId, 0, reward,
                                FusionRpg.Core.Demons.SoulEarnPolicy.Reasons.Discovery,
                                "species", entry.SpeciesId, "species:" + entry.SpeciesId, now);
                        }
                    }
                }
            }
        }

        var cleared = delve.Parties
            .Select(p => p.Haul.Count > 0 ? p with { Haul = Array.Empty<DelveHaulEntry>() } : p)
            .ToList();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE rpg_delves SET parties_json = $j, revision = revision + 1 WHERE delve_id = $id;";
        cmd.Parameters.AddWithValue("$j", JsonSerializer.Serialize(cleared));
        cmd.Parameters.AddWithValue("$id", delve.DelveId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>"The party cleared at least half the rooms on its route" (spec §9) and "the boss was
    /// killed" — both read directly off `rpg_delve_rooms.cleared`, since clearing a `boss`-kind room
    /// IS defeating its encounter. A route naming a sector this delve has no room for is skipped, not
    /// thrown on — a room can be added mid-delve by content this module does not need to know about.</summary>
    static (bool BossKilled, bool RouteAtLeastHalfCleared) RouteFacts(
        IReadOnlyList<string> route, IReadOnlyDictionary<string, DelveRoomRow> roomsBySector)
    {
        if (route.Count == 0) return (false, false);
        var clearedCount = 0;
        var bossKilled = false;
        foreach (var sectorId in route)
        {
            if (!roomsBySector.TryGetValue(sectorId, out var room) || !room.Cleared) continue;
            clearedCount++;
            if (string.Equals(room.Kind, "boss", StringComparison.Ordinal)) bossKilled = true;
        }
        return (bossKilled, clearedCount * 2 >= route.Count);
    }

    /// <summary>
    /// party-dungeon D2.23 (spec-delve-attrition.md §1, §7) — the `members[]` writer: replaces one
    /// party's member list wholesale (the room loop's own per-room `DelveMemberState` update, never a
    /// partial merge here). The only writer of `DelvePartyState.Members`. An upsert, not an update-
    /// only: `CreateDelve` seeds `parties_json` at `'[]'` (spec-delve-scope.md's own "parties written
    /// in one transaction with the delve" is about the WORLD entities, not this JSON array), so the
    /// FIRST room a party ever fights is genuinely the first time its own row exists here.
    /// </summary>
    public DelveRow? WritePartyMembers(long delveId, long partyEntityId, IReadOnlyList<FusionRpg.Core.Delve.Attrition.DelveMemberState> members)
    {
        if (members is null) throw new ArgumentNullException(nameof(members));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var current = ReadDelveJsonColumnUnlocked(db, tx, delveId, "parties_json");
            var parties = DelveRow.ParsePartiesJson(current ?? "[]");

            var updated = parties.Any(p => p.EntityId == partyEntityId)
                ? parties.Select(p => p.EntityId == partyEntityId ? p with { Members = members } : p).ToList()
                : parties.Append(new DelvePartyState(partyEntityId, Array.Empty<string>(), new Dictionary<string, int>(), Array.Empty<DelveHaulEntry>(), members)).ToList();
            var json = JsonSerializer.Serialize(updated);
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET parties_json = $j, revision = revision + 1 WHERE delve_id = $id;",
                "$j", "$id"))
                ExecuteWith(cmd, json, delveId);

            tx.Commit();
            return ReadDelveUnlocked(db, delveId);
        }
    }

    /// <summary>
    /// The `DelvePartyState.Route` writer — `delve-graph-roll`'s own future caller records each
    /// room a party actually walks here, the same upsert shape as <see cref="WritePartyMembers"/>
    /// (and the same "first room, first row" reason). Route is what §9's "at least half the rooms on
    /// its route" reads at extraction; this module reads it, and — since nothing else in the tree
    /// writes it yet — also exposes the one writer.
    /// </summary>
    public DelveRow? WritePartyRoute(long delveId, long partyEntityId, IReadOnlyList<string> route)
    {
        if (route is null) throw new ArgumentNullException(nameof(route));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var current = ReadDelveJsonColumnUnlocked(db, tx, delveId, "parties_json");
            var parties = DelveRow.ParsePartiesJson(current ?? "[]");

            var updated = parties.Any(p => p.EntityId == partyEntityId)
                ? parties.Select(p => p.EntityId == partyEntityId ? p with { Route = route } : p).ToList()
                : parties.Append(new DelvePartyState(partyEntityId, route, new Dictionary<string, int>(), Array.Empty<DelveHaulEntry>())).ToList();
            var json = JsonSerializer.Serialize(updated);
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET parties_json = $j, revision = revision + 1 WHERE delve_id = $id;",
                "$j", "$id"))
                ExecuteWith(cmd, json, delveId);

            tx.Commit();
            return ReadDelveUnlocked(db, delveId);
        }
    }

    /// <summary>
    /// D3.22 (spec-loot-pack.md §Structure) — the pack grid writer, same "first room, first row" upsert
    /// shape as <see cref="WritePartyMembers"/>/<see cref="WritePartyRoute"/>.
    /// </summary>
    public DelveRow? WritePartyPack(long delveId, long partyEntityId, DelvePartyPackState pack)
    {
        if (pack is null) throw new ArgumentNullException(nameof(pack));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var current = ReadDelveJsonColumnUnlocked(db, tx, delveId, "parties_json");
            var parties = DelveRow.ParsePartiesJson(current ?? "[]");

            var updated = parties.Any(p => p.EntityId == partyEntityId)
                ? parties.Select(p => p.EntityId == partyEntityId ? p with { Pack = pack } : p).ToList()
                : parties.Append(new DelvePartyState(partyEntityId, Array.Empty<string>(), new Dictionary<string, int>(), Array.Empty<DelveHaulEntry>(), Pack: pack)).ToList();
            var json = JsonSerializer.Serialize(updated);
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET parties_json = $j, revision = revision + 1 WHERE delve_id = $id;",
                "$j", "$id"))
                ExecuteWith(cmd, json, delveId);

            tx.Commit();
            return ReadDelveUnlocked(db, delveId);
        }
    }

    /// <summary>D4.8 (spec-wild-room.md §6) — the `parties_json[p].haul[]` writer: appends one pending
    /// altar-pull row, the same "first room, first row" upsert shape as <see cref="WritePartyMembers"/>/
    /// <see cref="WritePartyRoute"/>/<see cref="WritePartyPack"/> (a party that has never fought or
    /// pulled yet has no row here until now). The composable half — takes an existing connection so
    /// <see cref="PullAtAltar"/> can append the haul row in the SAME transaction as its own spend and
    /// pity write, matching this file's own established "public locked + private/internal Unlocked"
    /// convention (D3.16's own doc comment).</summary>
    void AppendPartyHaulUnlocked(SqliteConnection db, long delveId, long partyEntityId, DelveHaulEntry entry)
    {
        var delve = ReadDelveUnlocked(db, delveId) ?? throw new InvalidOperationException($"delve {delveId} not found");
        var updated = delve.Parties.Any(p => p.EntityId == partyEntityId)
            ? delve.Parties.Select(p => p.EntityId == partyEntityId
                ? p with { Haul = p.Haul.Append(entry).ToList() }
                : p).ToList()
            : delve.Parties.Append(new DelvePartyState(
                partyEntityId, Array.Empty<string>(), new Dictionary<string, int>(), new[] { entry })).ToList();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE rpg_delves SET parties_json = $j, revision = revision + 1 WHERE delve_id = $id;";
        cmd.Parameters.AddWithValue("$j", JsonSerializer.Serialize(updated));
        cmd.Parameters.AddWithValue("$id", delveId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Standalone, self-locking form of <see cref="AppendPartyHaulUnlocked"/> for a caller
    /// with nothing else to compose the append with (a direct test, or an admin tool) — every real
    /// production caller is <see cref="PullAtAltar"/>'s own composed transaction.</summary>
    public void AppendPartyHaul(long delveId, long partyEntityId, DelveHaulEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            AppendPartyHaulUnlocked(db, delveId, partyEntityId, entry);
            tx.Commit();
        }
    }

    /// <summary>
    /// D3.22 (spec-loot-pack.md §5: "acquired at placement... and lock-rowed"). The composable half —
    /// takes an existing connection so a FUTURE placement caller can lock an instance in the SAME
    /// transaction as its own <c>AcquireItem</c> call (spec's own Structure note: <c>RpgStore.Items.cs</c>
    /// stays untouched, so that composition happens at the CALL SITE, not inside either method).
    /// `INSERT OR IGNORE`: locking an already-locked instance for the SAME delve is a harmless replay.
    /// </summary>
    static void LockPackInstanceUnlocked(SqliteConnection db, long delveId, string instanceId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO rpg_delve_pack_lock(delve_id, instance_id) VALUES ($d, $i);";
        cmd.Parameters.AddWithValue("$d", delveId);
        cmd.Parameters.AddWithValue("$i", instanceId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Standalone, self-locking form of <see cref="LockPackInstanceUnlocked"/> for a caller
    /// with nothing else to compose the lock write with.</summary>
    public void LockPackInstance(long delveId, string instanceId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            LockPackInstanceUnlocked(db, delveId, instanceId);
            tx.Commit();
        }
    }

    /// <summary>The check spec-loot-pack.md's own Interface table names as "owed to the item program":
    /// "salvage, transfer, assign and bulk paths refuse an instance in `rpg_delve_pack_lock`
    /// (`pack.carried`)". This is the read those future call sites fold into their own
    /// `SalvageCandidate.Locked`-shaped bool — this store never decides what "locked" means to them.</summary>
    public bool IsPackLocked(string instanceId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM rpg_delve_pack_lock WHERE instance_id = $i;";
            cmd.Parameters.AddWithValue("$i", instanceId);
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }
    }

    /// <summary>
    /// D3.22 (spec-loot-pack.md §7) — the pack half of `CloseDelve`'s own settlement, first in the
    /// spec's own stated hook order (pack settlement, then attrition, then loot earn). Reads each
    /// party's OWN persisted <see cref="DelvePartyPackState"/> (grid cells only — floor items live on
    /// `rpg_delve_rooms.floor_json`, a separate read this pass does not join; an honest, named gap,
    /// see <see cref="FusionRpg.Core.Delve.Pack.PackSettlement"/>'s own doc comment), decides via the
    /// pure <see cref="FusionRpg.Core.Delve.Pack.PackSettlement.Decide"/>, and applies every write on
    /// THIS SAME transaction — `AdjustStock`'s/`DeleteInstance`'s own SQL inlined here rather than
    /// calling either (both self-lock their own transaction, the same reason `AwardSouls` cannot be
    /// called from inside `CloseDelve` either), mirroring `RpgStore.Loot.cs`'s own `PersistLoot`
    /// precedent for keeping several writes on one `tx`. Every lock row for this delve is deleted
    /// either way — extraction and a wipe both end the reservation.
    /// </summary>
    static void ApplyPackSettlementUnlocked(SqliteConnection db, SqliteTransaction tx, DelveRow delve, string finalState, string now)
    {
        var extracted = string.Equals(finalState, DelveStates.Extracted, StringComparison.Ordinal);
        var items = delve.Parties
            .Where(p => p.Pack is not null)
            .SelectMany(p => p.Pack!.Cells)
            .Select(c => c.Item)
            .ToList();

        foreach (var write in FusionRpg.Core.Delve.Pack.PackSettlement.Decide(items, extracted))
        {
            switch (write.Action)
            {
                case FusionRpg.Core.Delve.Pack.PackSettlementAction.BankStack:
                    ExecIn(db, tx, """
                        INSERT INTO rpg_item_stock (player_id, container_id, qty, updated_utc)
                        VALUES ($p, $c, MAX(0, $d), $utc)
                        ON CONFLICT(player_id, container_id) DO UPDATE SET
                          qty = MAX(0, rpg_item_stock.qty + $d), updated_utc = excluded.updated_utc;
                        """,
                        ("$p", delve.PlayerId.ToString()), ("$c", write.RefId), ("$d", write.Qty), ("$utc", now));
                    break;

                case FusionRpg.Core.Delve.Pack.PackSettlementAction.UnlockHaulInstance:
                case FusionRpg.Core.Delve.Pack.PackSettlementAction.UnlockCarryInInstance:
                    ExecIn(db, tx, "DELETE FROM rpg_delve_pack_lock WHERE instance_id = $id;", ("$id", write.InstanceId!));
                    break;

                case FusionRpg.Core.Delve.Pack.PackSettlementAction.DestroyHaulInstance:
                    if (write.InstanceId is not null)
                    {
                        ExecIn(db, tx, "DELETE FROM effect_binding WHERE instance_id = $id;", ("$id", write.InstanceId));
                        ExecIn(db, tx, "DELETE FROM effect_instance_atom WHERE instance_id = $id;", ("$id", write.InstanceId));
                        ExecIn(db, tx, "DELETE FROM rpg_item WHERE instance_id = $id;", ("$id", write.InstanceId));
                        ExecIn(db, tx, "DELETE FROM effect_instance WHERE instance_id = $id;", ("$id", write.InstanceId));
                        ExecIn(db, tx, "DELETE FROM rpg_delve_pack_lock WHERE instance_id = $id;", ("$id", write.InstanceId));
                    }
                    break;
            }
        }

        ExecIn(db, tx, "DELETE FROM rpg_delve_pack_lock WHERE delve_id = $id;", ("$id", delve.DelveId));
    }

    /// <summary>
    /// D3.16 (spec-dungeon-loot.md §7, `:213-214`) — kills accrue here room by room, never straight to
    /// the bank ("Kills accrue to `souls_unbanked` (§7), never the bank"). <paramref name="roomKey"/> is
    /// a caller-supplied descriptive tag, the same style as <see cref="SpendUnbanked"/>'s own
    /// <c>sinkKey</c> (spec-wild-room.md `:125`'s cited call, `SpendUnbanked(delveId, price,
    /// "wild:{r}:{c}")`) — there is no per-room ledger to dedupe it against. "The dedupe keys" the souls
    /// section names (spec §7) are the two <see cref="CloseDelve"/>-time ledger keys guarding a REPLAYED
    /// CLOSE, not a replayed accrual; the room loop that calls this already owns not calling it twice
    /// for the same room, the same way it already owns not calling <see cref="WritePartyMembers"/>
    /// twice for a stale snapshot.
    ///
    /// <para><c>checked</c>: `souls_unbanked` is a magnitude (CLAUDE.md's numeric-overflow rule) with no
    /// existing guard to mirror the way <c>rpg_soul_balances.balance</c> has
    /// <see cref="GuardSoulAwardOrThrow"/> — this method is the one true owner of this column's
    /// arithmetic, so it throws on overflow itself before any write, rather than risking a silent
    /// SQL-level wrap.</para>
    /// </summary>
    public DelveRow? AccrueUnbanked(long delveId, long delta, string roomKey)
    {
        if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (string.IsNullOrWhiteSpace(roomKey)) throw new ArgumentException("roomKey required", nameof(roomKey));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var delve = ReadDelveUnlocked(db, delveId);
            if (delve is null) return null;
            var next = checked(delve.SoulsUnbanked + delta);
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET souls_unbanked = $v, revision = revision + 1 WHERE delve_id = $id;",
                "$v", "$id"))
                ExecuteWith(cmd, next, delveId);
            tx.Commit();
            return ReadDelveUnlocked(db, delveId);
        }
    }

    /// <summary>
    /// D3.16 (spec-dungeon-loot.md §7, `:214-215`) — the wild-altar and merchant-pull sink, "refuses on
    /// shortfall" against the delve's OWN at-risk pot, never the player's bank
    /// (<see cref="TrySpendSouls"/>'s own, separate pot). The standalone, self-locking form for a caller
    /// with nothing else to compose the spend with; <see cref="SpendUnbankedUnlocked"/> is the
    /// composable half spec's own text asks for twice ("one transaction with the purchase",
    /// spec-dungeon-loot.md `:215`; spec-wild-room.md `:202`'s altar-pull price and `:355-357`'s
    /// "no-slot refuses before any soul moves" property) — an altar pull or merchant buy that also
    /// instantiates an item needs the spend and the instantiation in ONE transaction.
    /// </summary>
    public (bool Ok, string Reason, long SoulsUnbanked) SpendUnbanked(long delveId, long price, string sinkKey)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var result = SpendUnbankedUnlocked(db, delveId, price, sinkKey);
            tx.Commit();
            return result;
        }
    }

    /// <summary>The composable primitive behind <see cref="SpendUnbanked"/> — takes an existing
    /// connection so a future caller (the wild altar's `PullPrice`, a merchant `PullPrice`/`OfferFloor`
    /// buy) can spend and instantiate its own grant in one transaction, matching the
    /// <see cref="AppendSoulLedgerUnlocked"/> composability shape every other soul-moving path already
    /// has. No explicit <c>SqliteTransaction</c> parameter: Microsoft.Data.Sqlite auto-attaches a
    /// command to whatever transaction is active on <paramref name="db"/>
    /// (<see cref="DecrementAllRecoveringForPlayerUnlocked"/> already relies on the same behavior).</summary>
    (bool Ok, string Reason, long SoulsUnbanked) SpendUnbankedUnlocked(SqliteConnection db, long delveId, long price, string sinkKey)
    {
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (string.IsNullOrWhiteSpace(sinkKey)) throw new ArgumentException("sinkKey required", nameof(sinkKey));

        var delve = ReadDelveUnlocked(db, delveId);
        if (delve is null) return (false, "delve.not-found", 0);
        if (delve.SoulsUnbanked < price) return (false, "delve.souls-insufficient", delve.SoulsUnbanked);

        var next = delve.SoulsUnbanked - price;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "UPDATE rpg_delves SET souls_unbanked = $v, revision = revision + 1 WHERE delve_id = $id;";
            cmd.Parameters.AddWithValue("$v", next);
            cmd.Parameters.AddWithValue("$id", delveId);
            cmd.ExecuteNonQuery();
        }
        return (true, "", next);
    }

    /// <summary>
    /// D4.8 (spec-wild-room.md §4) — closes <see cref="FusionRpg.Core.Delve.Wild.RecruitMint"/>'s own
    /// named gap: its doc comment says "actually calling `MintDemonUnlocked` with the spec this
    /// returns is D4.8's own, still-unbuilt... job." One transaction: <see cref="SpendUnbankedUnlocked"/>
    /// (the souls debit — §3's `wild:{r}:{c}` sinkKey for a normal join, reused as-is for a cage
    /// `open` join per §7's "same mint"), then <see cref="MintDemonUnlocked"/> (§4's mint, which
    /// already performs the free auto-bind internally), then the SAME discovery-souls-on-first-sight
    /// award every other mint path already pays — `RpgStore.Summons.cs:118-125` (keyed off
    /// `species.BaseRarity`, the species' OWN rarity, not the minted rarity — the two are
    /// definitionally equal for a wild join per `RecruitMint.Build`'s own doc comment: "`Rarity =
    /// BaseRarity` — `ConcreteSpecies.Rarity` already IS the species' base rarity"), dedupe-keyed
    /// `"species:" + speciesId` — "one discovery policy across acquisition paths... the shared dedupe
    /// keeps it once-ever no matter which path lands first" (`RpgStore.Expeditions.cs`'s own
    /// 2026-08-21 review S5 comment, quoted verbatim).
    ///
    /// <para>Takes an already-assembled <paramref name="spec"/> (Core's own decision, via
    /// <see cref="FusionRpg.Core.Delve.Wild.RecruitMint.Build"/>) and an already-priced
    /// <paramref name="price"/> (via <see cref="FusionRpg.Core.Delve.Wild.OfferPricing.Souls"/> or the
    /// caller's own equivalent) — this method composes the STORE transaction only, matching this
    /// program's "Core decides, Data writes" split throughout; it does not re-derive `TalkTree`'s own
    /// verb/outcome resolution (see `DelveWildEndpoints.cs`'s own doc comment for the honest gap that
    /// leaves: no single Core function resolves "which verb was chosen, what did it draw" end to end
    /// today — `TalkTree.cs`'s real, shipped shape has no such orchestrator, despite the spec's own
    /// §Interface table citing a `TalkTree.Step(...)` that does not exist in the tree).</para>
    /// </summary>
    public (bool Ok, string Reason, FusionRpg.Contracts.DemonSpecimenDto? Specimen, long SoulsUnbanked) TalkJoin(
        long delveId, long playerId, long price, string sinkKey, FusionRpg.Contracts.DemonMintSpec spec)
    {
        if (spec is null) throw new ArgumentNullException(nameof(spec));
        if (string.IsNullOrWhiteSpace(sinkKey)) throw new ArgumentException("sinkKey required", nameof(sinkKey));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return (false, "player.unknown", null, 0);

            using var tx = db.BeginTransaction();
            var (ok, reason, soulsUnbanked) = SpendUnbankedUnlocked(db, delveId, price, sinkKey);
            if (!ok) { tx.Commit(); return (false, reason, null, soulsUnbanked); }

            var now = DateTime.UtcNow.ToString("o");
            var specimen = MintDemonUnlocked(db, playerId, spec, now, out var newlyDiscovered);

            if (newlyDiscovered)
            {
                var species = FusionRpg.Core.Demons.DemonSpeciesCatalog.Get(spec.SpeciesId);
                var reward = FusionRpg.Core.Demons.SoulEarnPolicy.DiscoveryDelta(species.BaseRarity);
                if (reward > 0)
                {
                    GuardSoulAwardOrThrow(ReadSoulBalanceUnlocked(db, playerId).Balance, reward);
                    AppendSoulLedgerUnlocked(db, playerId, 0, reward, FusionRpg.Core.Demons.SoulEarnPolicy.Reasons.Discovery,
                        "species", spec.SpeciesId, "species:" + spec.SpeciesId, now);
                }
            }

            tx.Commit();
            return (true, "", specimen, soulsUnbanked);
        }
    }

    /// <summary>
    /// D4.8 (spec-wild-room.md §6, "Altar pulls as at-risk haul") — "one pull on the shipped roller,"
    /// priced via <see cref="SpendUnbankedUnlocked"/> "in the same transaction," delivered as a
    /// `parties_json[p].haul[]` pending row rather than a mint: "no `UniqueActor` and no phase until
    /// `CloseDelve(Extracted)`." One transaction: resolve <paramref name="altarBannerId"/> for its own
    /// `CostPerPull` (pricing only — <see cref="FusionRpg.Core.Delve.Wild.AltarPull.TryPull"/> resolves
    /// the SAME banner again itself for the roll, a second cheap in-memory catalog lookup, never a
    /// second store read), price via <see cref="FusionRpg.Core.Delve.Loot.DelvePrices.PullPrice"/>
    /// (<paramref name="thetaRoom"/> is THIS room's own Θ, a caller-supplied fact — this schema has no
    /// per-room Θ column anywhere, matching <see cref="RecordClear"/>'s own identical `thetaRoom`
    /// parameter shape), spend, roll on the delve-seed-derived `dungeon:altar:{r}:{c}:{n}` stream (`n`
    /// = however many haul rows this party already has at this exact `(row, col)` — the haul array
    /// itself is the only durable "how many pulls so far" this program persists for an altar, so
    /// counting it rather than inventing a second counter keeps this self-contained), pity read/write
    /// on the player's one Sanctum row (`RpgStore.Summons.cs`'s `ReadPityUnlocked`/`WritePityUnlocked`
    /// — per player, cross-banner, shared, spec verbatim: "the altar reads and writes the Sanctum's one
    /// row"), then append the haul row via <see cref="AppendPartyHaulUnlocked"/>. No mint yet —
    /// <see cref="ApplyHaulMintUnlocked"/> at `CloseDelve(Extracted)` is where a haul row becomes a
    /// demon.
    /// </summary>
    public (bool Ok, string Reason, FusionRpg.Core.Demons.SummonRollResult? Result, long SoulsUnbanked) PullAtAltar(
        long delveId, long partyEntityId, int row, int col, int thetaRoom,
        string altarBannerId, FusionRpg.Core.Stats.Derived.ElementTypeId? focusElement)
    {
        if (string.IsNullOrWhiteSpace(altarBannerId)) throw new ArgumentException("altarBannerId required", nameof(altarBannerId));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var banner = FusionRpg.Core.Demons.SummonBannerCatalog.TryGet(altarBannerId);
            if (banner is null) return (false, FusionRpg.Core.Delve.Wild.AltarRefusal.BannerUnknown, null, 0);

            var delve = ReadDelveUnlocked(db, delveId);
            if (delve is null) return (false, "delve.not-found", null, 0);

            var existingHaul = delve.Parties.FirstOrDefault(p => p.EntityId == partyEntityId)?.Haul
                ?? Array.Empty<DelveHaulEntry>();
            var n = existingHaul.Count(h => h.Row == row && h.Col == col) + 1;

            var tuning = FusionRpg.Core.Power.PowerTuningHub.Tuning;
            var price = FusionRpg.Core.Delve.Loot.DelvePrices.PullPrice(banner.CostPerPull, thetaRoom, tuning);

            using var tx = db.BeginTransaction();
            var (ok, reason, soulsUnbanked) = SpendUnbankedUnlocked(db, delveId, price, $"wild:{row}:{col}");
            if (!ok) { tx.Commit(); return (false, reason, null, soulsUnbanked); }

            var pity = ReadPityUnlocked(db, delve.PlayerId);
            var rng = FusionRpg.Core.Battle.SeededRng.DeriveStream(delve.Seed, $"dungeon:altar:{row}:{col}:{n}");
            var pulled = FusionRpg.Core.Delve.Wild.AltarPull.TryPull(
                altarBannerId, focusElement, pity, rng, out var result, out var newPity, out var refusalId);
            if (!pulled) { tx.Commit(); return (false, refusalId!, null, soulsUnbanked); }

            var now = DateTime.UtcNow.ToString("o");
            WritePityUnlocked(db, delve.PlayerId, newPity, now);

            var entry = new DelveHaulEntry(
                "pull", result.SpeciesId, FusionRpg.Core.Demons.DemonRarityIds.ToId(result.Rarity), result.Variant,
                result.TraitIds, row, col, n);
            AppendPartyHaulUnlocked(db, delveId, partyEntityId, entry);

            tx.Commit();
            return (true, "", result, soulsUnbanked);
        }
    }

    /// <summary>
    /// D3.16 (spec-dungeon-loot.md §7, `:215-216`) — the depth watermark: <c>theta_run = MAX(theta_run,
    /// thetaRoom)</c>, the seam a later contracts-on-Θ follow-up reads via <c>MAX(theta_run) WHERE
    /// state = 'Extracted'</c> (`:199`). <paramref name="row"/>/<paramref name="col"/> match the spec's
    /// own cited call shape (<c>RecordClear(delveId, r, c, thetaRoom)</c>, `:215`) but this method's own
    /// write needs only <paramref name="thetaRoom"/> — marking the room ITSELF cleared is
    /// <see cref="MarkRoom"/>'s job already (the one existing writer of `rpg_delve_rooms.cleared`;
    /// duplicating that write here would give the same column two owners). <paramref name="row"/>/
    /// <paramref name="col"/> are accepted so a room-clear caller can pass its own `(r, c, thetaRoom)`
    /// straight through without pre-formatting anything itself; this method does not need them for its
    /// own write today.
    /// </summary>
    public DelveRow? RecordClear(long delveId, int row, int col, int thetaRoom)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            using (var cmd = Prepared(db, tx,
                "UPDATE rpg_delves SET theta_run = MAX(theta_run, $t), revision = revision + 1 WHERE delve_id = $id;",
                "$t", "$id"))
                ExecuteWith(cmd, thetaRoom, delveId);
            tx.Commit();
            return ReadDelveUnlocked(db, delveId);
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
