using System.Net.Http.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using FusionRpg.Data.Abstractions;
using FusionRpg.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>`species-build` T2.2 (module 5, `demon-type-allocation`) — GET/POST
/// `/api/aptitudes/species/*` against a real, minimal in-process host (same pattern as
/// `AptitudeEndpointsTests`/`AptitudesInjectorBroadcastTests`, not a mock): a baseline read with no
/// override, an override round-trip, budget refusal, and the ⛔ both-groups broadcast this module's
/// own spec calls out by name (the exact defect already found once for the Commander endpoint).</summary>
public class SpeciesAllocationEndpointsTests : IAsyncLifetime
{
    const int FumeshroomDemonTypeId = 60007;

    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    string _baseUrl = "";
    long _playerId;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-speciesalloc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _playerId = _store.GetCurrentPlayerId();

        FusionRpg.Core.Power.PowerTuningHub.Configure(
            FusionRpg.Core.Power.PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "power-scale.v2.json"))));
        FusionRpg.Core.Stats.Aptitudes.AptitudeTuningHub.Configure(
            FusionRpg.Core.Stats.Aptitudes.AptitudeTuningLoader.Parse(File.ReadAllText(LatestAptitudesPath())));
        FusionRpg.Core.Progression.ProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.ProgressionTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "progression.v1.json"))));
        FusionRpg.Core.Progression.SpeciesProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.SpeciesProgressionTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "species-progression.v1.json"))));
        DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        SpeciesBuildPlanCatalog.Configure(new Dictionary<string, IReadOnlyDictionary<string, long>>(StringComparer.Ordinal)
        {
            ["fumeshroom"] = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["Might"] = 500, ["Vigor"] = 300, ["Fortitude"] = 200
            }
        });

        SeedSpeciesLevel(_playerId, FumeshroomDemonTypeId, level: 21, "fumeshroom"); // source = 20

        var port = GetFreeTcpPort();
        _baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR(o => o.EnableDetailedErrors = true);
        builder.Services.AddSingleton(_store);
        builder.Services.AddSingleton<IPowerIndexProvider>(sp =>
            new FusionRpg.Server.Power.ServerPowerIndexProvider(sp.GetRequiredService<RpgStore>(), PowerTuningHub.Tuning));
        // RpgHub's own constructor deps (RpgHub.cs) -- without these DI resolution fails the moment a
        // client invokes any hub method, which manifests client-side as a silently dead connection
        // rather than a clear server error (found running this test for real, mirroring
        // AptitudesInjectorBroadcastTests' own registration list).
        builder.Services.AddSingleton<InjectorCommandInbox>();
        builder.Services.AddSingleton<EffectGrantSession>();
        builder.Services.AddSingleton<IHotCompactor>(sp => new HotCompactor(sp.GetRequiredService<RpgStore>()));
        builder.Services.AddSingleton<CompactionWorker>();
        builder.Services.AddSingleton<UniqueActorService>();
        builder.Services.AddSingleton<EventIngest>();
        builder.WebHost.UseUrls(_baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapHub<RpgHub>("/hub/rpg");
        _app.MapAptitudes();
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    void SeedSpeciesLevel(long playerId, int demonTypeId, long level, string speciesId)
    {
        using var db = SqliteConnectionFactory.Open(_store.HotPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rpg_actor_progression(
              player_id, kind, type_id, level, xp, highest_level, demotion_count, revision, updated_utc, scope_key)
            VALUES ($p, 'species', $tid, $lvl, 0, $lvl, 0, 0, $now, $sk);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$tid", demonTypeId);
        cmd.Parameters.AddWithValue("$lvl", level);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$sk", speciesId);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task Get_withNoOverride_returnsThePlansBaseline_notZero()
    {
        var resp = await _http.GetAsync($"/api/aptitudes/species/{_playerId}/fumeshroom");
        if (!resp.IsSuccessStatusCode) throw new Exception(await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<SpeciesStateDto>();

        Assert.NotNull(body);
        Assert.True(body!.Shares["Might"] > body.Shares["Vigor"]);
        Assert.True(body.Spent > 0); // NOT the silent-zero this module's spec calls out by name
        Assert.True(body.WithinBudget);
    }

    [Fact]
    public async Task Get_unknownSpecies_returnsBadRequest()
    {
        var resp = await _http.GetAsync($"/api/aptitudes/species/{_playerId}/not-a-real-species");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Allocate_overridesTheBaseline_androundTripsThroughGet()
    {
        var baseline = await (await _http.GetAsync($"/api/aptitudes/species/{_playerId}/fumeshroom"))
            .Content.ReadFromJsonAsync<SpeciesStateDto>();
        var budget = baseline!.Budget;

        var postResp = await _http.PostAsJsonAsync("/api/aptitudes/species/allocate", new
        {
            playerId = _playerId,
            speciesId = "fumeshroom",
            shares = new Dictionary<string, long> { ["Ferocity"] = budget }
        });
        if (!postResp.IsSuccessStatusCode) throw new Exception(await postResp.Content.ReadAsStringAsync());

        var getResp = await _http.GetAsync($"/api/aptitudes/species/{_playerId}/fumeshroom");
        var body = await getResp.Content.ReadFromJsonAsync<SpeciesStateDto>();
        Assert.Equal(budget, body!.Shares["Ferocity"]);
        Assert.Equal(0, body.Shares["Might"]); // replaced wholesale, not layered
    }

    [Fact]
    public async Task Allocate_overspending_isRefused_scopeLocally()
    {
        var baseline = await (await _http.GetAsync($"/api/aptitudes/species/{_playerId}/fumeshroom"))
            .Content.ReadFromJsonAsync<SpeciesStateDto>();
        var overspend = baseline!.Budget + 1;

        // A huge Commander allocation must NOT fund this DemonType overspend (scope-local budgets).
        await _http.PostAsJsonAsync("/api/aptitudes/allocate", new
        {
            playerId = _playerId,
            shares = new Dictionary<string, long> { ["Might"] = 1_000_000 }
        });

        var postResp = await _http.PostAsJsonAsync("/api/aptitudes/species/allocate", new
        {
            playerId = _playerId,
            speciesId = "fumeshroom",
            shares = new Dictionary<string, long> { ["Ferocity"] = overspend }
        });
        Assert.Equal(System.Net.HttpStatusCode.Conflict, postResp.StatusCode);
    }

    [Fact]
    public async Task Allocate_notifies_a_client_joined_as_injector_not_just_web()
    {
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hub = new HubConnectionBuilder().WithUrl($"{_baseUrl}/hub/rpg").Build();
        hub.On<object>("AptitudesUpdated", _ => received.TrySetResult(true));
        await hub.StartAsync();
        await hub.InvokeAsync("Join", RpgConstants.InjectorGroup);

        var postResp = await _http.PostAsJsonAsync("/api/aptitudes/species/allocate", new
        {
            playerId = _playerId,
            speciesId = "fumeshroom",
            shares = new Dictionary<string, long> { ["Might"] = 1 }
        });
        postResp.EnsureSuccessStatusCode();

        var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(got, "a species allocate never reached an injector-group connection " +
            "(the exact WebGroup-only defect this module's spec calls out by name)");
        await hub.DisposeAsync();
    }

    [Fact]
    public async Task Allocate_still_notifies_a_client_joined_as_web()
    {
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hub = new HubConnectionBuilder().WithUrl($"{_baseUrl}/hub/rpg").Build();
        hub.On<object>("AptitudesUpdated", _ => received.TrySetResult(true));
        await hub.StartAsync();
        await hub.InvokeAsync("Join", RpgConstants.WebGroup);

        var postResp = await _http.PostAsJsonAsync("/api/aptitudes/species/allocate", new
        {
            playerId = _playerId,
            speciesId = "fumeshroom",
            shares = new Dictionary<string, long> { ["Might"] = 1 }
        });
        postResp.EnsureSuccessStatusCode();

        var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(got, "regression: the web-group notification broke for the species route");
        await hub.DisposeAsync();
    }

    sealed class SpeciesStateDto
    {
        public string SpeciesId { get; set; } = "";
        public long Level { get; set; }
        public long Budget { get; set; }
        public long Spent { get; set; }
        public bool WithinBudget { get; set; }
        public Dictionary<string, long> Shares { get; set; } = new();
    }

    static string RepoTuningDir() => Path.Combine(FindRepoRoot(), "data", "tuning");

    static string LatestAptitudesPath()
    {
        var dir = RepoTuningDir();
        var best = Directory.EnumerateFiles(dir, "aptitudes.v*.json")
            .Select(Path.GetFileName)
            .Select(n => (Name: n!, Match: System.Text.RegularExpressions.Regex.Match(n!, @"^aptitudes\.v(\d+)\.json$")))
            .Where(x => x.Match.Success)
            .OrderByDescending(x => int.Parse(x.Match.Groups[1].Value))
            .First();
        return Path.Combine(dir, best.Name);
    }

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "collect-class-system-realrun.ps1"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
