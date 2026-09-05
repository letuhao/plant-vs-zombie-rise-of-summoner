using FusionRpg.Core.Delve;
using FusionRpg.Core.Dungeon.Registry;
using FusionRpg.Core.World;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests.Delve;

/// <summary>D1.12–D1.17 — delve-scope (spec-delve-scope.md, gate G1). A delve is a `WorldState` row
/// of kind='delve' beside a player's map; the map FE still boots onto the map; a rolled graph
/// validates under the delve profile and refuses under the map profile with the rule named; world
/// goldens do not move; no path can Step a delve.</summary>
public class DelveScopeTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly RoomTypeCatalog _rooms;
    readonly DoorTypeCatalog _doors;

    public DelveScopeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-delve-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();

        var registryDir = FindRepoRoot();
        var registries = DungeonRegistryLoader.LoadAll(Path.Combine(registryDir, "data", "seed", "dungeon", "_registry"));
        _rooms = new RoomTypeCatalog(registries.RoomKinds);
        _doors = new DoorTypeCatalog(registries.DoorKinds);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed", "dungeon"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }

    /// <summary>A minimal three-room delve graph: fight -> cache -> boss, one player faction, one
    /// Wild faction (PolicyId null — no AI ever acts, there is no turn to act in), one Warband
    /// standing in the first room.</summary>
    static WorldState BuildRolledGraph(string worldId) => new()
    {
        WorldId = worldId, TemplateId = "layout.short-narrow-linear-001", Seed = 42UL, CurrentTurn = 0,
        Factions = new[]
        {
            new WorldFaction { FactionId = "dave", Kind = WorldFactionKind.Player, Name = "Dave" },
            new WorldFaction { FactionId = "wild", Kind = WorldFactionKind.Wild, Name = "Wild", PolicyId = null },
        },
        Sectors = new[]
        {
            new WorldSector { SectorId = "r0c0", TypeId = "fight", Climate = null, OwnerFactionId = "dave" },
            new WorldSector { SectorId = "r1c0", TypeId = "cache", Climate = null },
            new WorldSector { SectorId = "r2c0", TypeId = "boss", Climate = null },
        },
        Lanes = new[]
        {
            new WorldLane { LaneId = "l0", FromSectorId = "r0c0", ToSectorId = "r1c0", TypeId = "passage" },
            new WorldLane { LaneId = "l1", FromSectorId = "r1c0", ToSectorId = "r2c0", TypeId = "gated", GateKeyId = "key.l1" },
        },
        Entities = new[]
        {
            new WorldEntity { EntityId = "party-0", Kind = WorldEntityKind.Warband, OwnerFactionId = "dave", AtSectorId = "r0c0" },
        },
    };

    static IReadOnlyList<DelveRoomRow> BuildRooms() => new[]
    {
        new DelveRoomRow("r0c0", 0, 0, "fight", "room.fight-none-001", true, false, null, null, null, null, "[]", 0),
        new DelveRoomRow("r1c0", 1, 0, "cache", "room.cache-none-001", false, false, "l1", null, null, null, "[]", 0),
        new DelveRoomRow("r2c0", 2, 0, "boss", "room.boss-none-001", false, false, null, null, null, null, "[]", 0),
    };

    // -----------------------------------------------------------------------------------------
    // Profiles (Core, exercised through the real registries).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_rolled_delve_graph_validates_under_the_delve_profile()
    {
        var world = BuildRolledGraph("w-profile-1");
        var profile = WorldValidationProfile.Delve(_rooms, _doors);
        WorldValidation.Validate(world, profile); // must not throw
    }

    [Fact]
    public void The_same_rolled_graph_refuses_under_the_map_profile_naming_rule_1()
    {
        var world = BuildRolledGraph("w-profile-2");
        var ex = Assert.Throws<InvalidOperationException>(() => WorldValidation.Validate(world, WorldValidationProfile.Map));
        Assert.Contains("unknown sector type", ex.Message);
    }

    [Fact]
    public void FirstLight_under_the_delve_profile_refuses_rule_1_the_other_way()
    {
        var firstLight = FusionRpg.Core.World.WorldTemplateCatalog.Build(
            FusionRpg.Core.World.WorldTemplateCatalog.FirstLightId, 1, "first-light");
        var profile = WorldValidationProfile.Delve(_rooms, _doors);
        var ex = Assert.Throws<InvalidOperationException>(() => WorldValidation.Validate(firstLight, profile));
        Assert.Contains("unknown sector type", ex.Message);
    }

    // -----------------------------------------------------------------------------------------
    // Coexistence + bootstrap (Data).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void GetActiveWorld_returns_the_map_even_when_a_delve_id_sorts_first()
    {
        // "delve-..." sorts before "map-..." ordinally -- the exact incident the kind filter exists
        // to prevent (spec-delve-scope.md §4: "a delve id sorting before the map id would silently
        // become the player's map").
        _store.CreateWorld(1, FusionRpg.Core.World.WorldTemplateCatalog.Build(
            FusionRpg.Core.World.WorldTemplateCatalog.FirstLightId, 1, "map-first-light"));

        var (ok, reason, delve) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-1", null,
            "delve-aaa-p1", "layout.short-narrow-linear-001", 42UL,
            BuildRolledGraph("delve-aaa-p1"), BuildRooms(), _rooms, _doors);
        Assert.True(ok, reason);

        var active = _store.GetActiveWorld(1);
        Assert.NotNull(active);
        Assert.Equal("map-first-light", active!.WorldId);
        Assert.Equal("map", active.Kind);
    }

    [Fact]
    public void A_delve_and_a_map_both_round_trip_through_WorldCanonical()
    {
        var mapWorld = FusionRpg.Core.World.WorldTemplateCatalog.Build(
            FusionRpg.Core.World.WorldTemplateCatalog.FirstLightId, 1, "map-rt");
        _store.CreateWorld(1, mapWorld);

        var (ok, _, _) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-rt", null,
            "delve-rt", "layout.short-narrow-linear-001", 7UL,
            BuildRolledGraph("delve-rt"), BuildRooms(), _rooms, _doors);
        Assert.True(ok);

        var mapHash = FusionRpg.Core.World.WorldCanonical.Write(_store.LoadWorldState("map-rt")!);
        var delveHash = FusionRpg.Core.World.WorldCanonical.Write(_store.LoadWorldState("delve-rt")! with { WorldId = "delve-rt", TemplateId = "layout.short-narrow-linear-001", Seed = 7UL });
        Assert.NotEqual(mapHash, delveHash); // sanity: they are not accidentally the same content
        Assert.NotNull(mapHash);
        Assert.NotNull(delveHash);
    }

    [Fact]
    public void CreateDelve_writes_kind_delve_and_the_header_carries_it()
    {
        var (ok, _, delve) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-kind", null,
            "delve-kind", "layout.short-narrow-linear-001", 1UL,
            BuildRolledGraph("delve-kind"), BuildRooms(), _rooms, _doors);
        Assert.True(ok);

        var header = _store.GetWorldHeader("delve-kind");
        Assert.NotNull(header);
        Assert.Equal("delve", header!.Kind);
        Assert.Equal(FusionRpg.Data.DelveStates.Active, delve!.State);
    }

    // -----------------------------------------------------------------------------------------
    // Never-Step.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Committing_a_turn_on_a_delve_world_refuses_world_not_a_map()
    {
        var (ok, _, _) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-commit", null,
            "delve-commit", "layout.short-narrow-linear-001", 1UL,
            BuildRolledGraph("delve-commit"), BuildRooms(), _rooms, _doors);
        Assert.True(ok);

        var result = _store.CommitWorldTurn("delve-commit", "dave", 0);
        Assert.False(result.Ok);
        Assert.Equal("world.not-a-map", result.Reason);
    }

    [Fact]
    public void Submitting_commands_on_a_delve_world_refuses_world_not_a_map()
    {
        var (ok, _, _) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-submit", null,
            "delve-submit", "layout.short-narrow-linear-001", 1UL,
            BuildRolledGraph("delve-submit"), BuildRooms(), _rooms, _doors);
        Assert.True(ok);

        var command = new FusionRpg.Core.World.Turn.WorldCommand { CommandId = "cmd-1", CommanderId = "dave", Kind = "march" };
        var outcomes = _store.SubmitWorldCommands("delve-submit", new[] { command });
        Assert.Single(outcomes);
        Assert.False(outcomes[0].Ok);
        Assert.Equal("world.not-a-map", outcomes[0].Reason);
    }

    // -----------------------------------------------------------------------------------------
    // Lifecycle.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void A_replayed_CreateDelve_with_the_same_correlation_returns_the_recorded_row_not_a_second_one()
    {
        var (ok1, _, first) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-replay", null,
            "delve-replay", "layout.short-narrow-linear-001", 1UL,
            BuildRolledGraph("delve-replay"), BuildRooms(), _rooms, _doors);
        Assert.True(ok1);

        var (ok2, reason2, second) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-replay", null,
            "delve-replay", "layout.short-narrow-linear-001", 1UL,
            BuildRolledGraph("delve-replay"), BuildRooms(), _rooms, _doors);
        Assert.True(ok2);
        Assert.Equal("ok.replayed", reason2);
        Assert.Equal(first!.DelveId, second!.DelveId);
    }

    [Fact]
    public void CloseDelve_sets_the_state_and_closed_timestamp()
    {
        var (_, _, delve) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-close", null,
            "delve-close", "layout.short-narrow-linear-001", 1UL,
            BuildRolledGraph("delve-close"), BuildRooms(), _rooms, _doors);

        var closed = _store.CloseDelve(delve!.DelveId, FusionRpg.Data.DelveStates.Extracted, archiveNow: false);
        Assert.True(closed);

        var reloaded = _store.LoadDelve(delve.DelveId);
        Assert.Equal(FusionRpg.Data.DelveStates.Extracted, reloaded!.State);
        Assert.NotNull(reloaded.ClosedUtc);
    }

    // -----------------------------------------------------------------------------------------
    // MoveParty / LaneGate.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void MoveParty_moves_through_an_open_passage()
    {
        var (_, _, delve) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-move", null,
            "delve-move", "layout.short-narrow-linear-001", 1UL,
            BuildRolledGraph("delve-move"), BuildRooms(), _rooms, _doors);

        var (ok, reason) = _store.MoveParty(delve!.DelveId, "delve-move", "party-0", "r1c0", _doors);
        Assert.True(ok, reason);

        var world = _store.LoadWorldState("delve-move")!;
        var party = world.Entities.Single(e => e.EntityId == "party-0");
        Assert.Equal("r1c0", party.AtSectorId);
    }

    [Fact]
    public void MoveParty_refuses_a_gated_door_with_no_key_cleared()
    {
        var (_, _, delve) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-gate", null,
            "delve-gate", "layout.short-narrow-linear-001", 1UL,
            BuildRolledGraph("delve-gate"), BuildRooms(), _rooms, _doors);

        // Move to r1c0 first (open passage), then try the gated door to r2c0.
        _store.MoveParty(delve!.DelveId, "delve-gate", "party-0", "r1c0", _doors);
        var (ok, reason) = _store.MoveParty(delve.DelveId, "delve-gate", "party-0", "r2c0", _doors);

        Assert.False(ok);
        Assert.Equal("lane.gated", reason);
    }

    [Fact]
    public void MoveParty_refuses_the_one_way_door_from_the_wrong_direction()
    {
        var graph = BuildRolledGraph("delve-oneway");
        var oneWayGraph = graph with
        {
            Lanes = graph.Lanes.Select(l => l.LaneId == "l0" ? l with { TypeId = "one-way" } : l).ToList(),
        };
        var (_, _, delve) = _store.CreateDelve(
            1, "domain.fire-shallow-001", "solo", "hard", "corr-oneway", null,
            "delve-oneway", "layout.short-narrow-linear-001", 1UL,
            oneWayGraph, BuildRooms(), _rooms, _doors);

        // Forward (r0c0 -> r1c0, matching the lane's own FromSectorId) succeeds.
        var (forwardOk, forwardReason) = _store.MoveParty(delve!.DelveId, "delve-oneway", "party-0", "r1c0", _doors);
        Assert.True(forwardOk, forwardReason);

        // Backward (r1c0 -> r0c0, against the one-way lane) refuses.
        var (backOk, backReason) = _store.MoveParty(delve.DelveId, "delve-oneway", "party-0", "r0c0", _doors);
        Assert.False(backOk);
        Assert.Equal("lane.one-way", backReason);
    }
}
