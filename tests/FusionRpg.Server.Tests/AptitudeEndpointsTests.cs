using System.Net;
using System.Net.Http.Json;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using FusionRpg.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>spec-aptitude-allocation-surface.md — GET/POST /api/aptitudes against a REAL, minimal
/// in-process host (same pattern as RealRunCollectorTests.cs), not a mock. Proves the shipped endpoint
/// end to end: budget refusal never clamps (PS-8), an unknown aptitude id 400s, and a successful POST
/// actually round-trips through GET.</summary>
public class AptitudeEndpointsTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    long _playerId;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-aptend-" + Guid.NewGuid().ToString("N"));
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

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(_store);
        builder.Services.AddSingleton<IPowerIndexProvider>(sp =>
            new FusionRpg.Server.Power.ServerPowerIndexProvider(sp.GetRequiredService<RpgStore>(), PowerTuningHub.Tuning));
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapAptitudes();
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    [Fact]
    public async Task Get_onAFreshPlayer_returnsAllTwelveIdsAtZero()
    {
        var resp = await _http.GetAsync($"/api/aptitudes/{_playerId}");
        if (!resp.IsSuccessStatusCode) throw new Exception(await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<AptitudesStateDto>();
        Assert.NotNull(body);
        Assert.Equal(12, body!.Shares.Count);
        Assert.All(body.Shares.Values, v => Assert.Equal(0, v));
        Assert.True(body.WithinBudget);
        Assert.Equal(0, body.Spent);
    }

    [Fact]
    public async Task Get_unknownPlayer_returns404()
    {
        var resp = await _http.GetAsync("/api/aptitudes/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_withinBudget_savesAndRoundTripsThroughGet()
    {
        var getBefore = await (await _http.GetAsync($"/api/aptitudes/{_playerId}")).Content.ReadFromJsonAsync<AptitudesStateDto>();
        Assert.NotNull(getBefore);
        var affordable = getBefore!.Budget; // spend exactly the whole budget on Might -- within budget by construction
        Assert.True(affordable > 0, "expected a nonzero commander budget on the shipped tuning");

        var postResp = await _http.PostAsJsonAsync("/api/aptitudes/allocate",
            new { playerId = _playerId, shares = new Dictionary<string, long> { ["Might"] = affordable } });
        postResp.EnsureSuccessStatusCode();
        var postBody = await postResp.Content.ReadFromJsonAsync<AptitudesStateDto>();
        Assert.NotNull(postBody);
        Assert.Equal(affordable, postBody!.Shares["Might"]);
        Assert.True(postBody.WithinBudget);

        var getAfter = await (await _http.GetAsync($"/api/aptitudes/{_playerId}")).Content.ReadFromJsonAsync<AptitudesStateDto>();
        Assert.Equal(affordable, getAfter!.Shares["Might"]);
    }

    [Fact]
    public async Task Post_overBudget_refusesAndDoesNotSave_neverClamps()
    {
        var before = await (await _http.GetAsync($"/api/aptitudes/{_playerId}")).Content.ReadFromJsonAsync<AptitudesStateDto>();
        var tooMuch = before!.Budget + 1;

        var postResp = await _http.PostAsJsonAsync("/api/aptitudes/allocate",
            new { playerId = _playerId, shares = new Dictionary<string, long> { ["Might"] = tooMuch } });
        Assert.Equal(HttpStatusCode.Conflict, postResp.StatusCode);

        // PS-8: refused, never silently clamped to the budget -- re-GET must show the allocation
        // UNCHANGED from before the attempt, not truncated to the max legal value.
        var after = await (await _http.GetAsync($"/api/aptitudes/{_playerId}")).Content.ReadFromJsonAsync<AptitudesStateDto>();
        Assert.Equal(before.Shares["Might"], after!.Shares["Might"]);
    }

    [Fact]
    public async Task Post_unknownAptitudeId_returns400()
    {
        var postResp = await _http.PostAsJsonAsync("/api/aptitudes/allocate",
            new { playerId = _playerId, shares = new Dictionary<string, long> { ["NotARealAptitude"] = 1 } });
        Assert.Equal(HttpStatusCode.BadRequest, postResp.StatusCode);
    }

    // ---- species-build T3.1 (allocation-transport): the additive `species` field --------------

    [Fact]
    public async Task Get_forAPlayerWithNoSpecies_hasAnEmptySpeciesMap_commanderHalfUnaffected()
    {
        var resp = await _http.GetAsync($"/api/aptitudes/{_playerId}");
        var raw = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(raw);

        // The commander half's own keys are exactly what shipped before this task -- `species` is
        // additive, not a replacement shape (spec's own ⛔ callout).
        Assert.True(doc.RootElement.TryGetProperty("theta", out _));
        Assert.True(doc.RootElement.TryGetProperty("budget", out _));
        Assert.True(doc.RootElement.TryGetProperty("spent", out _));
        Assert.True(doc.RootElement.TryGetProperty("withinBudget", out _));
        Assert.True(doc.RootElement.TryGetProperty("shares", out var shares));
        Assert.Equal(12, shares.EnumerateObject().Count());

        Assert.True(doc.RootElement.TryGetProperty("species", out var species));
        Assert.Empty(species.EnumerateObject());
    }

    [Fact]
    public async Task Get_sendsOnlyTheSpeciesThePlayerHasActuallyLevelled()
    {
        const int fumeshroomDemonTypeId = 60007;
        DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        SpeciesBuildPlanCatalog.Configure(new Dictionary<string, IReadOnlyDictionary<string, long>>(StringComparer.Ordinal)
        {
            ["fumeshroom"] = new Dictionary<string, long>(StringComparer.Ordinal) { ["Might"] = 700, ["Vigor"] = 300 }
        });
        FusionRpg.Core.Progression.SpeciesProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.SpeciesProgressionTuningLoader.Parse(
                File.ReadAllText(Path.Combine(RepoTuningDir(), "species-progression.v1.json"))));

        using (var db = SqliteConnectionFactory.Open(_store.HotPath))
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO rpg_actor_progression(
                  player_id, kind, type_id, level, xp, highest_level, demotion_count, revision, updated_utc, scope_key)
                VALUES ($p, 'species', $tid, 21, 0, 21, 0, 0, $now, 'fumeshroom');
                """;
            cmd.Parameters.AddWithValue("$p", _playerId);
            cmd.Parameters.AddWithValue("$tid", fumeshroomDemonTypeId);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        var body = await (await _http.GetAsync($"/api/aptitudes/{_playerId}"))
            .Content.ReadFromJsonAsync<AptitudesStateDto>();

        Assert.NotNull(body);
        var fumeshroom = Assert.Single(body!.Species);
        Assert.Equal("fumeshroom", fumeshroom.Key);
        Assert.True(fumeshroom.Value["Might"] > fumeshroom.Value["Vigor"]);
        Assert.True(fumeshroom.Value["Might"] > 0); // NOT the silent-zero this program keeps naming
    }

    sealed class AptitudesStateDto
    {
        public long Theta { get; set; }
        public long Budget { get; set; }
        public long Spent { get; set; }
        public bool WithinBudget { get; set; }
        public Dictionary<string, long> Shares { get; set; } = new();
        public Dictionary<string, Dictionary<string, long>> Species { get; set; } = new();
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
