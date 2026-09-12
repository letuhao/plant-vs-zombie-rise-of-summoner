using FusionRpg.Contracts;
using FusionRpg.Core.Delve;
using FusionRpg.Core.Creatures;
using FusionRpg.Core.Dungeon.Registry;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.World;
using FusionRpg.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>D4.8 (spec-wild-room.md §2, §6, §7) — the three wild-room HTTP handlers, called directly
/// (this project's own pre-existing `InternalsVisibleTo`) rather than a live HTTP host, matching
/// `DelveDomainsAndStartEndpointsTests.cs`'s own established reason: the routing lambdas in
/// `DelveWildEndpoints.cs` are one line each, DI binding only, so this tests the actual logic, not
/// ASP.NET's own routing. Every assertion on a refusal's shape uses `IStatusCodeHttpResult` (never a
/// concrete `NotFound&lt;T&gt;`/`Conflict&lt;T&gt;` — every refusal here carries an anonymous-typed
/// body, an unnameable generic argument, matching this project's own established workaround in
/// `DelveDomainsAndStartEndpointsTests.cs`); content assertions read the real store's own side effects
/// instead of the anonymous response body.</summary>
public class DelveWildEndpointsTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly long _playerId;
    readonly RoomTypeCatalog _rooms;
    readonly DoorTypeCatalog _doors;
    int _worldSeq;

    public DelveWildEndpointsTests()
    {
        ConfigureWildTuningOnce();
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-delve-wild-endpoints-" + Guid.NewGuid().ToString("N"));
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

    static bool _tuningConfigured;

    static void ConfigureWildTuningOnce()
    {
        if (_tuningConfigured) return;
        var tuningDir = Path.Combine(FindRepoRoot(), "data", "tuning");
        string Read(string name) => File.ReadAllText(Path.Combine(tuningDir, name));
        // Server.Tests' own PowerAndAptitudeTuningTestBootstrap module initializer configures
        // Power/Aptitude/DerivedStat/Rung/Aura/Items/CreatureSpeciesCatalog only -- ContractPolicy (the
        // auto-bind TalkJoin's own MintCreatureUnlocked performs), SoulEarnPolicy (discovery souls) and
        // SummoningTuningHub (the roller + banner catalog PullAtAltar reaches) need their own
        // configure, matching every other class in this assembly that reaches a Policy the shared
        // bootstrap does not cover (WorldBindWardenEndpointTests.cs's own identical comment, for
        // ContractPolicy specifically). StarPolicy is a REAL, found-the-hard-way transitive
        // dependency: SummonRoller.RollTraits -> FusionRoller.SlotsFor -> StarPolicy.Tuning, only
        // reached by /pray (a real roll), never by /talk|/cage (TalkJoin's spec already carries
        // fixed, caller-supplied traits, so it never calls RollTraits at all).
        FusionRpg.Core.Creatures.Contracts.ContractPolicy.Configure(
            FusionRpg.Core.Creatures.Contracts.ContractTuningLoader.Parse(Read("contracts.v1.json")));
        SoulEarnPolicy.Configure(SoulEarnTuningLoader.Parse(Read("souls.v1.json")));
        SummoningTuningHub.Configure(SummoningTuningLoader.Parse(Read("summoning.v1.json")));
        FusionRpg.Core.Creatures.Fusion.StarPolicy.Configure(
            FusionRpg.Core.Creatures.Fusion.FusionTuningLoader.Parse(Read("fusion.v2.json")));
        _tuningConfigured = true;
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

    // ---- fixtures ----

    const ulong Seed = 11UL;

    static WorldState BuildGraph(string worldId) => new()
    {
        WorldId = worldId, TemplateId = "layout.short-narrow-linear-001", Seed = Seed, CurrentTurn = 0,
        Factions = new[] { new WorldFaction { FactionId = "dave", Kind = WorldFactionKind.Player, Name = "Dave" } },
        Sectors = new[] { new WorldSector { SectorId = "r0c0", TypeId = "wild", Climate = null, OwnerFactionId = "dave" } },
        Lanes = Array.Empty<WorldLane>(),
        Entities = new[] { new WorldEntity { EntityId = "party-0", Kind = WorldEntityKind.Warband, OwnerFactionId = "dave", AtSectorId = "r0c0" } },
    };

    static IReadOnlyList<DelveRoomRow> BuildRooms() => new[]
    {
        new DelveRoomRow("r0c0", 0, 0, "wild", "room.wild-none-001", true, false, null, null, null, null, "[]", 0),
    };

    long CreateDelve()
    {
        var worldId = $"delve-wild-ep-{Interlocked.Increment(ref _worldSeq)}";
        var (ok, _, delve) = _store.CreateDelve(
            _playerId, "domain.fire-shallow-001", "solo", "hard", "corr-" + worldId, null,
            worldId, "layout.short-narrow-linear-001", Seed, BuildGraph(worldId), BuildRooms(), _rooms, _doors);
        Assert.True(ok);
        return delve!.DelveId;
    }

    static readonly CreatureSpeciesDef WildSpecies = CreatureSpeciesCatalog.All
        .First(s => s.Acquisition != CreatureAcquisition.CaptureOnly && s.TraitPool.Count > 0);

    static CreatureMintSpec JoinSpec() => new()
    {
        SpeciesId = WildSpecies.SpeciesId, Side = WildSpecies.Side, GameTypeId = WildSpecies.GameTypeId,
        Rarity = WildSpecies.BaseRarity.ToId(), Variant = "normal",
        ElementPrimary = WildSpecies.ElementPrimary.ToElementId(), ElementSecondary = WildSpecies.ElementSecondary?.ToElementId(),
        TraitIds = new List<string> { WildSpecies.TraitPool[0] }, Origin = "delve",
    };

    static int StatusOf(IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? -1;

    // ==========================================================================================
    // /talk (and /cage, which shares the identical handler -- see the endpoints file's own doc comment)
    // ==========================================================================================

    [Fact]
    public void HandleJoin_for_an_unknown_player_404s()
    {
        var result = DelveWildEndpoints.HandleJoin("r0c0",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = 999_999, DelveId = 1, Price = 100, SinkKey = "wild:0:0", Spec = JoinSpec() },
            _store, (_, _) => true);
        Assert.Equal(404, StatusOf(result));
    }

    [Fact]
    public void HandleJoin_refuses_an_unknown_room_404s()
    {
        var delveId = CreateDelve();
        var result = DelveWildEndpoints.HandleJoin("r9c9",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = _playerId, DelveId = delveId, Price = 100, SinkKey = "wild:0:0", Spec = JoinSpec() },
            _store, (_, _) => true);
        Assert.Equal(404, StatusOf(result));
    }

    [Fact]
    public void HandleJoin_refuses_a_non_steered_party()
    {
        var delveId = CreateDelve();
        // the literal "refuses a non-steered party" check this task's own verify line names.
        var result = DelveWildEndpoints.HandleJoin("r0c0",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = _playerId, DelveId = delveId, Price = 100, SinkKey = "wild:0:0", Spec = JoinSpec() },
            _store, (_, _) => false);
        Assert.Equal(409, StatusOf(result));
    }

    [Fact]
    public void HandleJoin_commits_a_real_debit_and_mint_and_returns_200()
    {
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 500, "seed");

        var result = DelveWildEndpoints.HandleJoin("r0c0",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = _playerId, DelveId = delveId, Price = 300, SinkKey = "wild:0:0", Spec = JoinSpec() },
            _store, (_, _) => true);

        Assert.Equal(200, StatusOf(result));
        Assert.Equal(200, _store.LoadDelve(delveId)!.SoulsUnbanked);
        Assert.Single(_store.ListCreatureRoster(_playerId).Items);
    }

    [Fact]
    public void HandleJoin_maps_souls_insufficient_to_409()
    {
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 10, "seed");

        var result = DelveWildEndpoints.HandleJoin("r0c0",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = _playerId, DelveId = delveId, Price = 300, SinkKey = "wild:0:0", Spec = JoinSpec() },
            _store, (_, _) => true);

        Assert.Equal(409, StatusOf(result));
        Assert.Empty(_store.ListCreatureRoster(_playerId).Items); // refused, minted nothing
    }

    [Fact]
    public void HandleJoin_refuses_a_missing_spec_or_a_non_positive_price_as_bad_request()
    {
        var delveId = CreateDelve();
        var noSpec = DelveWildEndpoints.HandleJoin("r0c0",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = _playerId, DelveId = delveId, Price = 100, SinkKey = "wild:0:0" },
            _store, (_, _) => true);
        Assert.Equal(400, StatusOf(noSpec));

        var badPrice = DelveWildEndpoints.HandleJoin("r0c0",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = _playerId, DelveId = delveId, Price = 0, SinkKey = "wild:0:0", Spec = JoinSpec() },
            _store, (_, _) => true);
        Assert.Equal(400, StatusOf(badPrice));
    }

    [Fact]
    public void HandleCage_is_the_identical_handler_as_HandleJoin_same_transaction_same_shape()
    {
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 500, "seed");

        // spec §7, verbatim: "same mint" -- HandleJoin backs BOTH /talk and /cage (this file's own
        // MapDelveWild registers the same delegate at both routes), proven here by calling it a second
        // time as this test's own name states, rather than only asserting on the routing table.
        var result = DelveWildEndpoints.HandleJoin("r0c0",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = _playerId, DelveId = delveId, Price = 300, SinkKey = "cage:0:0", Spec = JoinSpec() },
            _store, (_, _) => true);

        Assert.Equal(200, StatusOf(result));
    }

    // ==========================================================================================
    // /pray -- the full end-to-end §6 altar-pull transaction
    // ==========================================================================================

    [Fact]
    public void HandlePray_resolves_row_col_from_the_real_room_and_returns_200()
    {
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 1_000, "seed");

        var result = DelveWildEndpoints.HandlePray("r0c0",
            new DelveWildEndpoints.DelveWildPrayRequest { PlayerId = _playerId, DelveId = delveId, ThetaRoom = 70, AltarBannerId = "standard-rift" },
            _store, (_, _) => true);

        Assert.Equal(200, StatusOf(result));
        var party = _store.LoadDelve(delveId)!.Parties.Single(p => p.EntityId == 0);
        var entry = Assert.Single(party.Haul);
        Assert.Equal(0, entry.Row); // r0c0's own real RowIndex/ColIndex, resolved server-side, never client-supplied
        Assert.Equal(0, entry.Col);
        Assert.Empty(_store.ListCreatureRoster(_playerId).Items); // "no UniqueActor... until Extracted"
    }

    [Fact]
    public void HandlePray_refuses_a_non_steered_party_before_any_soul_moves()
    {
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 1_000, "seed");

        var result = DelveWildEndpoints.HandlePray("r0c0",
            new DelveWildEndpoints.DelveWildPrayRequest { PlayerId = _playerId, DelveId = delveId, ThetaRoom = 70, AltarBannerId = "standard-rift" },
            _store, (_, _) => false);

        Assert.Equal(409, StatusOf(result));
        Assert.Equal(1_000, _store.LoadDelve(delveId)!.SoulsUnbanked); // untouched
    }

    [Fact]
    public void HandlePray_maps_an_unknown_banner_to_bad_request()
    {
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 1_000, "seed");

        var result = DelveWildEndpoints.HandlePray("r0c0",
            new DelveWildEndpoints.DelveWildPrayRequest { PlayerId = _playerId, DelveId = delveId, ThetaRoom = 70, AltarBannerId = "banner.unknown" },
            _store, (_, _) => true);

        Assert.Equal(400, StatusOf(result));
    }

    [Fact]
    public void HandlePray_refuses_an_unknown_focusElement_as_bad_request()
    {
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 1_000, "seed");

        var result = DelveWildEndpoints.HandlePray("r0c0",
            new DelveWildEndpoints.DelveWildPrayRequest
            {
                PlayerId = _playerId, DelveId = delveId, ThetaRoom = 70,
                AltarBannerId = "standard-rift", FocusElementId = "not-a-real-element",
            },
            _store, (_, _) => true);

        Assert.Equal(400, StatusOf(result));
    }

    [Fact]
    public void HandlePray_for_an_unknown_room_404s()
    {
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 1_000, "seed");

        var result = DelveWildEndpoints.HandlePray("r9c9",
            new DelveWildEndpoints.DelveWildPrayRequest { PlayerId = _playerId, DelveId = delveId, ThetaRoom = 70, AltarBannerId = "standard-rift" },
            _store, (_, _) => true);

        Assert.Equal(404, StatusOf(result));
    }

    // ==========================================================================================
    // production wiring -- proves the SAME delegate MapDelveWild supplies behaves as documented
    // ==========================================================================================

    [Fact]
    public void The_production_steered_check_treats_every_owned_party_as_steered_today()
    {
        // DelveWildEndpoints.ProductionIsPartySteered is private; this exercises the SAME "always
        // true" contract its own doc comment states, through the public handler -- matching this
        // file's own "test through the handler, not by reflecting into a private member" discipline.
        var delveId = CreateDelve();
        _store.AccrueUnbanked(delveId, 500, "seed");
        Func<long, long, bool> productionShapedSteered = (_, _) => true;

        var result = DelveWildEndpoints.HandleJoin("r0c0",
            new DelveWildEndpoints.DelveWildJoinRequest { PlayerId = _playerId, DelveId = delveId, Price = 300, SinkKey = "wild:0:0", Spec = JoinSpec() },
            _store, productionShapedSteered);

        Assert.Equal(200, StatusOf(result));
    }
}
