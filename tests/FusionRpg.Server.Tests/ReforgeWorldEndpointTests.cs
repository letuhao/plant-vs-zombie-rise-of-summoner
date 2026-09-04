using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data;
using FusionRpg.Data.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// T5.7 (`dev-reforge`, spec-player-materialise.md §6, A4): `POST /api/debug/reforge-world` against a
/// REAL in-process host, same harness `LawnQuickStartEndpointTests` already established — proving the
/// endpoint actually re-derives a player's roster and is idempotent, not just that a service method
/// compiles. `PowerTuningHub` is configured for the whole assembly by
/// <see cref="PowerAndAptitudeTuningTestBootstrap"/>'s own `[ModuleInitializer]`, so this endpoint's
/// `PowerTuningHub.Tuning` read never throws here — the same real tuning every other endpoint test in
/// this assembly already runs against.
/// </summary>
public class ReforgeWorldEndpointTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        var host = await BuildHostAsync(mapDebug: true);
        _dir = host.Dir;
        _store = host.Store;
        _app = host.App;
        _http = host.Http;
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    /// <summary>One real in-process host, wired exactly like <c>Program.cs</c> — the same builder this
    /// class already used inline, now parameterised on <paramref name="mapDebug"/> so the two "outside
    /// the debug build" tests below can reproduce Program.cs:1064-1070's own condition (`app.MapDebug()`
    /// is only ever called on a loopback bind, or `FUSIONRPG_DEBUG_REMOTE=1`) instead of inventing a
    /// second gate to test against.</summary>
    static async Task<TestHost> BuildHostAsync(bool mapDebug)
    {
        var dir = Path.Combine(Path.GetTempPath(), "fusionrpg-reforge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new RpgStore(dir);
        store.Init();

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<InjectorCommandInbox>();
        builder.Services.AddSingleton<EffectGrantSession>();
        builder.Services.AddSingleton<IHotCompactor>(sp => new HotCompactor(sp.GetRequiredService<RpgStore>()));
        builder.Services.AddSingleton<CompactionWorker>();
        builder.Services.AddSingleton<UniqueActorService>();
        builder.Services.AddSingleton<EventIngest>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<EventIngest>()); // Program.cs:127's own pairing — the singleton is also the drain loop
        builder.WebHost.UseUrls(baseUrl);
        var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.MapHub<RpgHub>("/hub/rpg");
        if (mapDebug)
            app.MapDebug();
        await app.StartAsync();

        var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
        return new TestHost(app, http, store, dir);
    }

    /// <summary>Disposable bundle for a second, independently-gated host — used only by the two tests
    /// that need a host where <c>MapDebug()</c> was never called, alongside the fixture's own host
    /// (<see cref="InitializeAsync"/>) where it always is.</summary>
    sealed record TestHost(WebApplication App, HttpClient Http, RpgStore Store, string Dir) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await App.StopAsync();
            try { Directory.Delete(Dir, recursive: true); } catch { /* temp dir */ }
        }
    }

    void SeedSpecies(string speciesId, int amount)
    {
        var atomId = $"atom.{speciesId}-vitality.t1";
        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = atomId, KindId = "stat.modify", FamilyId = $"atom.{speciesId}-vitality", Tier = 1,
            Name = $"{speciesId} vitality",
            ParamsJson = $$"""{"channel":"maxHp","op":"flat","amount":{{amount}}}""",
        }).IsOk);

        Assert.True(_store.UpsertContainer(new ContainerRow
        {
            ContainerId = $"species-passive.{speciesId}", Kind = ContainerKind.SpeciesPassive,
            Atoms = new[] { new ContainerAtomRow(1, atomId) },
        }).IsOk);
    }

    [Fact]
    public async Task Reforge_rolls_the_current_players_roster_against_the_current_catalog()
    {
        SeedSpecies("conezombie", 10);
        var player = _store.CreatePlayer("Owner");
        _store.SetCurrentPlayer(player.Id);

        var resp = await _http.PostAsJsonAsync("/api/debug/reforge-world", new { });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.True(body!["ok"].ToString() == "True" || body["ok"].ToString() == "true");
        Assert.Equal(player.Id.ToString(), body["playerId"].ToString());
        Assert.Single(_store.ListPlayerSpecies(player.Id));
    }

    [Fact]
    public async Task Reforge_is_idempotent_when_the_catalog_is_unchanged()
    {
        SeedSpecies("peashooter", 20);
        var player = _store.CreatePlayer("Owner");

        var first = await _http.PostAsJsonAsync("/api/debug/reforge-world", new { playerId = player.Id });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rowAfterFirst = _store.ListPlayerSpecies(player.Id).Single();
        var fingerprintAfterFirst = _store.GetInstance(rowAfterFirst.InstanceId)!.ContentFingerprint();

        var second = await _http.PostAsJsonAsync("/api/debug/reforge-world", new { playerId = player.Id });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var rowAfterSecond = _store.ListPlayerSpecies(player.Id).Single();

        Assert.Equal(rowAfterFirst.InstanceId, rowAfterSecond.InstanceId); // same row, not a new one
        Assert.Equal(fingerprintAfterFirst, _store.GetInstance(rowAfterSecond.InstanceId)!.ContentFingerprint());
    }

    [Fact]
    public async Task Reforge_never_changes_the_players_world_seed()
    {
        SeedSpecies("conezombie", 10);
        var player = _store.CreatePlayer("Owner");

        var resp = await _http.PostAsJsonAsync("/api/debug/reforge-world", new { playerId = player.Id });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var after = _store.ListPlayers().Single(p => p.Id == player.Id);
        Assert.Equal(player.WorldSeed, after.WorldSeed); // spec-dev-reforge.md: "only the catalog side re-derives"
    }

    [Fact]
    public async Task Reforge_logs_the_before_and_after_catalog_revision_it_touched()
    {
        SeedSpecies("conezombie", 10);
        var player = _store.CreatePlayer("Owner");
        // RpgStore.InsertOneUnlocked stamps a non-"board.start" event's player_id from
        // GetCurrentPlayerIdUnlocked, not from EventEnvelope.PlayerId (that field only takes effect
        // for board.start) — so the persisted log's own player attribution follows "current player,"
        // matching how the endpoint's own default (`store.GetCurrentPlayerId()`) is realistically used.
        _store.SetCurrentPlayer(player.Id);

        var resp = await _http.PostAsJsonAsync("/api/debug/reforge-world", new { playerId = player.Id });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // EventIngest.Enqueue only writes to an in-memory channel — its own FlushPendingAsync is the
        // real wait-for-drain primitive (EventIngest.cs:95), not a hand-rolled poll loop.
        var ingest = _app.Services.GetRequiredService<EventIngest>();
        await ingest.FlushPendingAsync();

        var found = _store.ListEvents(200, 0, player.Id);
        Assert.Contains(found, e => e.Kind == "debug.reforge-world");
        var entry = found.Single(e => e.Kind == "debug.reforge-world");
        Assert.Equal(player.Id, entry.PlayerId);
        var payload = Assert.IsType<JsonElement>(entry.Payload);
        Assert.True(payload.TryGetProperty("catalogRevisionBefore", out _));
        Assert.True(payload.TryGetProperty("catalogRevisionAfter", out _));
    }

    [Fact]
    public async Task Reforge_picks_up_a_retuned_affix_that_a_plain_materialise_would_have_frozen_out()
    {
        SeedSpecies("sunflower", 30);
        var player = _store.CreatePlayer("Owner");
        var materialise = _store.MaterialisePlayerSpecies(player.Id, thetaContent: 0, PowerTuningHubTuning());
        Assert.True(materialise.IsOk, materialise.Rejection.ToString());
        var beforeValuesJson = _store.GetInstance(_store.ListPlayerSpecies(player.Id).Single().InstanceId)!
            .Atoms.Single().ValuesJson;

        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = "atom.sunflower-vitality.t1", KindId = "stat.modify",
            FamilyId = "atom.sunflower-vitality", Tier = 1, Name = "sunflower vitality",
            ParamsJson = """{"channel":"maxHp","op":"flat","amount":777}""",
        }).IsOk);

        var resp = await _http.PostAsJsonAsync("/api/debug/reforge-world", new { playerId = player.Id });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var afterValuesJson = _store.GetInstance(_store.ListPlayerSpecies(player.Id).Single().InstanceId)!
            .Atoms.Single().ValuesJson;
        Assert.NotEqual(beforeValuesJson, afterValuesJson); // the retune IS observable after reforge
    }

    [Fact]
    public async Task Endpoint_refuses_outside_the_debug_build()
    {
        // spec-dev-reforge.md's own "never to players" guarantee (A4), mechanically enforced: Program.cs
        // (~line 1064-1070) only calls `app.MapDebug()` on a loopback bind (or FUSIONRPG_DEBUG_REMOTE=1).
        // Skipping that call reproduces exactly what a non-loopback / Release deploy does — reforge-world
        // never gets a route at all, the same as every other /api/debug/* route.
        await using var host = await BuildHostAsync(mapDebug: false);

        var resp = await host.Http.PostAsJsonAsync("/api/debug/reforge-world", new { });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Endpoint_requires_the_same_debug_auth_gate_as_every_other_debug_route()
    {
        // No special-cased bypass: reforge-world and an unrelated debug route (GET /api/debug/session)
        // go dark together when MapDebug() is skipped, and come back together when it is called — proof
        // reforge-world does not carry its own gate, it lives inside the one every other route shares.
        await using var withoutGate = await BuildHostAsync(mapDebug: false);
        Assert.Equal(HttpStatusCode.NotFound, (await withoutGate.Http.GetAsync("/api/debug/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await withoutGate.Http.PostAsJsonAsync("/api/debug/reforge-world", new { })).StatusCode);

        await using var withGate = await BuildHostAsync(mapDebug: true);
        Assert.Equal(HttpStatusCode.OK, (await withGate.Http.GetAsync("/api/debug/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await withGate.Http.PostAsJsonAsync("/api/debug/reforge-world", new { })).StatusCode);
    }

    static FusionRpg.Core.Power.PowerTuning PowerTuningHubTuning() => FusionRpg.Core.Power.PowerTuningHub.Tuning;

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
