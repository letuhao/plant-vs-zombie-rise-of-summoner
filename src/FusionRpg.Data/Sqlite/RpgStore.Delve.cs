using System.Text.Json;
using FusionRpg.Core.Delve;
using FusionRpg.Core.Delve.Attrition;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Movement;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One party's route/pity/haul, one element of <c>rpg_delves.parties_json</c> per
/// <c>PartyIndex</c> — a single JSON array on the header, not a third table (spec-delve-scope.md
/// §1: "a raid is one, two or four parties written in one transaction with the delve").
///
/// <para><b><see cref="Members"/></b> — delve-attrition D2.23 (spec-delve-attrition.md §1, §7:
/// "each [party] gains `members[]`, one record per demon, read and written only through
/// `RpgStore.Delve.cs`"). Nullable, not <see cref="Array.Empty{T}"/>: a party written before this
/// field existed deserializes to <c>null</c> rather than a lossy empty list a caller could mistake
/// for "this party genuinely has no members."</para></summary>
public sealed record DelvePartyState(
    long EntityId, IReadOnlyList<string> Route, IReadOnlyDictionary<string, int> Pity, IReadOnlyList<string> Haul,
    IReadOnlyList<DelveMemberState>? Members = null);

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
    /// (spec-delve-scope.md §7). <paramref name="finalState"/> is the CALLER's own decision — this
    /// method never re-derives wipe from member state, matching D2.21's own "a wipe is its own
    /// settlement path" (the room loop that ends the delve is the one place that already knows every
    /// party's live state; re-deriving it here from a stale `parties_json` snapshot would be a second,
    /// possibly-disagreeing source of truth). <paramref name="tuning"/> is the single opt-in switch for
    /// every extraction-time settlement this seam has grown since D2.21: <c>null</c> keeps the
    /// pre-D2.23 caller shape byte-identical (only the trailing state UPDATE runs). Supplying it runs,
    /// in this SAME transaction:
    /// <list type="number">
    /// <item>D2.23's attrition settlement (spec-delve-attrition.md §7, §9) — every member's
    /// Retire/Recover/Roster outcome, contract loyalty credited once, persisted cross-delve pools, and
    /// every OTHER delve-recovering actor this player owns aged one delve.</item>
    /// <item>D3.16's own souls settlement (spec-dungeon-loot.md §7, `:213-219`) — see
    /// <see cref="ApplyLootEarnUnlocked"/>.</item>
    /// </list>
    /// The spec's own full stated hook order is pack settlement, attrition settlement, loot earn, quest
    /// verdicts, then domain unlocks (spec-dungeon-loot.md's own Structure-table row for this file). Only
    /// the middle two exist today — pack settlement (`loot-pack`), quest verdicts (`delve-quests`) and
    /// domain unlocks (`domain-catalog`) are separate, still-unbuilt modules, named here as an honest gap
    /// rather than silently skipped.
    /// </summary>
    public bool CloseDelve(long delveId, string finalState, bool archiveNow, FusionRpg.Core.Dungeon.Tuning.DungeonTuning? tuning = null)
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
                    SettleExtractionUnlocked(db, delve, finalState, tuning, now);
                    ApplyLootEarnUnlocked(db, delve, finalState, now);
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
                : parties.Append(new DelvePartyState(partyEntityId, Array.Empty<string>(), new Dictionary<string, int>(), Array.Empty<string>(), members)).ToList();
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
                : parties.Append(new DelvePartyState(partyEntityId, route, new Dictionary<string, int>(), Array.Empty<string>())).ToList();
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
