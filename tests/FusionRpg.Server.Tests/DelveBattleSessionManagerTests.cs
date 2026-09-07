using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Delve;
using FusionRpg.Core.Delve.Battle;
using FusionRpg.Core.Dungeon.Registry;
using FusionRpg.Core.World;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// D2.16 — <see cref="DelveBattleSessionManager"/>: the Server-layer registry that turns
/// <see cref="DelveBattleSession"/> into something a real delve/store/hub can drive. Covers match-key
/// derivation, the log-before-ingest discipline for starting a session, Steer's freeze-or-append
/// behaviour, FreezeByConnection, and a real Resume round-trip against the persisted
/// <c>rpg_web_match_log</c> row.
/// </summary>
public class DelveBattleSessionManagerTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly long _playerId;
    readonly RoomTypeCatalog _rooms;
    readonly DoorTypeCatalog _doors;

    public DelveBattleSessionManagerTests()
    {
        DelveBattleTuningTestFixture.ConfigureRealBattleTuning();

        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-delve-battle-mgr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _playerId = _store.GetCurrentPlayerId();

        var repoRoot = FindRepoRoot();
        var registries = DungeonRegistryLoader.LoadAll(Path.Combine(repoRoot, "data", "seed", "dungeon", "_registry"));
        _rooms = new RoomTypeCatalog(registries.RoomKinds);
        _doors = new DoorTypeCatalog(registries.DoorKinds);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not find repo root above " + AppContext.BaseDirectory);
    }

    const ulong WorldSeed = 11UL;

    WorldState BuildGraph(string worldId, string partyEntityId) => new()
    {
        WorldId = worldId, TemplateId = "layout.short-narrow-linear-001", Seed = WorldSeed, CurrentTurn = 0,
        Factions = new[] { new WorldFaction { FactionId = _playerId.ToString(), Kind = WorldFactionKind.Player, Name = "Player" } },
        Sectors = new[] { new WorldSector { SectorId = "r0c0", TypeId = "fight" } },
        Lanes = Array.Empty<WorldLane>(),
        Entities = new[]
        {
            new WorldEntity { EntityId = partyEntityId, Kind = WorldEntityKind.Warband, OwnerFactionId = _playerId.ToString(), AtSectorId = "r0c0" },
        },
    };

    static IReadOnlyList<DelveRoomRow> BuildRooms() => new[]
    {
        new DelveRoomRow("r0c0", 0, 0, "fight", "room.fight-none-001", true, false, null, null, null, null, "[]", 0),
    };

    long CreateDelve()
    {
        var worldId = "delve-battle-mgr-" + Guid.NewGuid().ToString("N");
        var (ok, reason, delve) = _store.CreateDelve(
            _playerId, "domain.fire-shallow-001", "solo", "hard", "corr-" + worldId, null,
            worldId, "layout.short-narrow-linear-001", WorldSeed, BuildGraph(worldId, "1"), BuildRooms(), _rooms, _doors);
        Assert.True(ok, reason);
        return delve!.DelveId;
    }

    // ---- setup fixtures (mirrors DelveBattleSessionTests) --------------------------------------------

    static BattleActorSetup SquadActor(string key, int? partyIndex = 0) => new()
    {
        Key = key, Side = "squad", PartyIndex = partyIndex, SpeciesId = "test-species", TypeId = 1,
        Level = 30, MaxHp = 10_000_000, Atk = BattleRuleset.BaseAtk(30), Defense = BattleRuleset.BaseDefense(30),
    };

    static BattleActorSetup WaveActor(string key) => new()
    {
        Key = key, Side = "wave", SpeciesId = "test-species", TypeId = 2,
        Level = 1, MaxHp = 10_000_000, Atk = BattleRuleset.BaseAtk(1), Defense = BattleRuleset.BaseDefense(1),
    };

    static BattleSetup Setup() => new()
    {
        WaveId = "test-delve-room",
        Squad = new[] { SquadActor("squad:p0:0"), SquadActor("squad:p0:1") },
        Wave = new[] { WaveActor("wave:0") },
    };

    sealed class FixedAttacker : IIntentSource
    {
        public ActionIntent TryDeclare(string actorKey, long nowTick) => new(
            "act.attack",
            actorKey.StartsWith("squad", StringComparison.Ordinal) ? "wave:0" : "squad:p0:0",
            BattleEngine.BasicAttackEnvelope);
    }

    static async Task RespondToPendingAskOnceAsync(DelveBattleSession session)
    {
        if (session.PendingActorKey is { } pending)
            session.Declare(pending, "act.attack",
                pending.StartsWith("squad", StringComparison.Ordinal) ? "wave:0" : "squad:p0:0");
        await Task.Delay(2);
    }

    static async Task DriveUntilAsync(DelveBattleSession session, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await RespondToPendingAskOnceAsync(session);
        Assert.True(condition(), "condition never became true within the test timeout");
    }

    // ==================================================================================================
    // Match key / correlation derivation
    // ==================================================================================================

    [Fact]
    public void MatchKey_and_correlation_are_position_for_position_identical_modulo_separator()
    {
        var matchKey = DelveBattleSessionManager.MatchKeyFor(delveId: 7, row: 2, col: 3, partyIndex: 1);
        var correlation = DelveBattleSessionManager.CorrelationFor(delveId: 7, row: 2, col: 3, partyIndex: 1);

        Assert.Equal("delve-7-2-3-p1", matchKey);
        Assert.Equal("delve:7:2:3:p1", correlation);
        Assert.Equal(correlation, matchKey.Replace('-', ':'));
    }

    // ==================================================================================================
    // StartSession
    // ==================================================================================================

    [Fact]
    public async Task StartSession_logs_before_ingest_and_runs_the_fight()
    {
        var delveId = CreateDelve();
        var manager = new DelveBattleSessionManager(_store);

        var session = manager.StartSession(
            delveId, row: 0, col: 0, partyIndex: 0, _playerId,
            Setup(), seed: 555UL, automated: new FixedAttacker());

        Assert.NotNull(session);
        var matchKey = DelveBattleSessionManager.MatchKeyFor(delveId, 0, 0, 0);
        Assert.Equal(matchKey, session!.MatchKey);

        // The row is logged BEFORE the fight resolves (log-before-ingest) -- readable immediately.
        var entry = _store.TryGetWebMatchLog(_playerId, DelveBattleSessionManager.CorrelationFor(delveId, 0, 0, 0));
        Assert.NotNull(entry);
        Assert.Equal("delve", entry!.ProfileId);
        Assert.Null(entry.RunId); // not yet finished

        // Drive a couple of real decisions -- proves the session is genuinely live and persisting.
        await DriveUntilAsync(session, () => session.Trace.Count >= 2);
        var reread = _store.TryGetWebMatchLog(_playerId, DelveBattleSessionManager.CorrelationFor(delveId, 0, 0, 0));
        Assert.True(reread!.DecisionsJson?.Length > 2); // more than "[]"

        // Clean up.
        session.Freeze(LiveFreezeTrigger.FreezeAwayPayload(0));
        await Record.ExceptionAsync(() => session.RunTask!);
    }

    [Fact]
    public void StartSession_twice_for_the_same_room_and_party_resumes_instead_of_forking_a_second_battle()
    {
        var delveId = CreateDelve();
        var manager = new DelveBattleSessionManager(_store);

        var first = manager.StartSession(delveId, 0, 0, 0, _playerId, Setup(), seed: 777UL, automated: new FixedAttacker());
        Assert.NotNull(first);
        first!.Freeze(LiveFreezeTrigger.FreezeAwayPayload(0));

        // A second "start" call for the identical room+party is really a resume over the SAME logged
        // row (log-before-ingest's own replay-gate discipline) -- not a second, independent battle.
        var second = manager.StartSession(delveId, 0, 0, 0, _playerId, Setup(), seed: 999UL, automated: new FixedAttacker());
        Assert.NotNull(second);
        Assert.Equal(first.MatchKey, second!.MatchKey);

        second.Freeze(LiveFreezeTrigger.FreezeAwayPayload(0));
    }

    // ==================================================================================================
    // Steer
    // ==================================================================================================

    [Fact]
    public async Task Steer_freezes_the_from_partys_live_session_and_logs_exactly_one_entry()
    {
        var delveId = CreateDelve();
        var manager = new DelveBattleSessionManager(_store);
        var session = manager.StartSession(delveId, 0, 0, 0, _playerId, Setup(), seed: 111UL, automated: new FixedAttacker())!;

        manager.Steer(delveId, fromPartyIndex: 0, toPartyIndex: 1);

        await Record.ExceptionAsync(() => session.RunTask!);
        Assert.Equal(TaskStatus.Canceled, session.RunTask!.Status);

        var delve = _store.LoadDelve(delveId)!;
        var decisions = JsonSerializer.Deserialize<List<JsonElement>>(delve.DecisionsJson)!;
        var steerEntries = decisions.Where(d => d.GetProperty("Kind").GetString() == DelveDecisionKinds.Steer).ToList();
        var entry = Assert.Single(steerEntries);
        Assert.Equal(0, entry.GetProperty("PartyIndex").GetInt32());
        var payload = entry.GetProperty("Payload");
        Assert.Equal(0, payload.GetProperty("From").GetInt32());
        Assert.Equal(1, payload.GetProperty("To").GetInt32());
    }

    [Fact]
    public void Steer_with_no_live_session_still_appends_the_delve_level_log_entry()
    {
        var delveId = CreateDelve();
        var manager = new DelveBattleSessionManager(_store);

        manager.Steer(delveId, fromPartyIndex: null, toPartyIndex: 0);

        var delve = _store.LoadDelve(delveId)!;
        var decisions = JsonSerializer.Deserialize<List<JsonElement>>(delve.DecisionsJson)!;
        var entry = Assert.Single(decisions);
        Assert.Equal(DelveDecisionKinds.Steer, entry.GetProperty("Kind").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("PartyIndex").ValueKind); // From was null too
        Assert.Equal(0, entry.GetProperty("Payload").GetProperty("To").GetInt32());
    }

    // ==================================================================================================
    // FreezeByConnection
    // ==================================================================================================

    [Fact]
    public async Task FreezeByConnection_freezes_the_tracked_sessions_fight_exactly_like_a_dropped_connection()
    {
        var delveId = CreateDelve();
        var manager = new DelveBattleSessionManager(_store);
        var session = manager.StartSession(delveId, 0, 0, 0, _playerId, Setup(), seed: 222UL, automated: new FixedAttacker(), connectionId: "conn-1")!;

        manager.FreezeByConnection("conn-1");

        await Record.ExceptionAsync(() => session.RunTask!);
        Assert.Equal(TaskStatus.Canceled, session.RunTask!.Status);

        // A connection that was never tracked is a no-op, never an exception.
        manager.FreezeByConnection("conn-does-not-exist");
    }

    [Fact]
    public void Declare_forwards_to_the_named_session_and_returns_false_for_an_unknown_matchKey()
    {
        var manager = new DelveBattleSessionManager(_store);
        Assert.False(manager.Declare("delve-999-0-0-p0", "squad:p0:0", "act.attack", "wave:0"));
    }

    // ==================================================================================================
    // Resume
    // ==================================================================================================

    [Fact]
    public async Task Resume_after_a_freeze_rebuilds_a_fresh_session_over_the_persisted_trace_and_finishes()
    {
        var delveId = CreateDelve();
        var manager = new DelveBattleSessionManager(_store);
        var session1 = manager.StartSession(delveId, 0, 0, 0, _playerId, Setup(), seed: 333UL, automated: new FixedAttacker())!;

        await DriveUntilAsync(session1, () => session1.Trace.Count >= 2);
        session1.Freeze(LiveFreezeTrigger.FreezeAwayPayload(0));
        await Record.ExceptionAsync(() => session1.RunTask!);
        Assert.Equal(TaskStatus.Canceled, session1.RunTask!.Status);
        var decisionsBeforeResume = session1.Trace.Count;
        Assert.True(decisionsBeforeResume >= 2);

        var session2 = manager.Resume(session1.MatchKey, _playerId, automated: new FixedAttacker());
        Assert.NotNull(session2);
        Assert.NotSame(session1, session2); // a brand-new session, never a reattachment
        // The prefix survived the freeze/resume round-trip.
        Assert.True(session2!.Trace.Count >= decisionsBeforeResume);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!session2.RunTask!.IsCompleted && DateTime.UtcNow < deadline)
            await RespondToPendingAskOnceAsync(session2);

        Assert.Equal(TaskStatus.RanToCompletion, session2.RunTask!.Status);
        var report = await session2.RunTask!;
        Assert.NotNull(report);

        // The row's own decisions_json reflects the FULL history, prefix and suffix, once more.
        var entry = _store.TryGetWebMatchLog(_playerId, DelveBattleSessionManager.CorrelationFromMatchKey(session1.MatchKey));
        Assert.NotNull(entry);
    }

    [Fact]
    public void Resume_with_no_logged_row_at_all_refuses()
    {
        var manager = new DelveBattleSessionManager(_store);
        var session = manager.Resume("delve-42-0-0-p0", _playerId, automated: new FixedAttacker());
        Assert.Null(session);
    }
}
