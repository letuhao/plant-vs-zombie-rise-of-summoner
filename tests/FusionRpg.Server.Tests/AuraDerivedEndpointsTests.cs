using System.Net;
using System.Net.Http.Json;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>aura-skill T18b — GET /api/actors/{instanceId}/derived against a REAL in-process host
/// (same pattern as AptitudeEndpointsTests.cs), proving the endpoint spec-aura-surface.md §3 names as
/// missing actually resolves a live actor's derived channels WITH their per-source contributions —
/// not a stub, not a bridge to `pvz_stat_contributions`.</summary>
public class AuraDerivedEndpointsTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    long _playerId;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-auraderived-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _playerId = _store.GetCurrentPlayerId();

        PowerTuningHub.Configure(
            PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "power-scale.v2.json"))));
        AptitudeTuningHub.Configure(
            AptitudeTuningLoader.Parse(File.ReadAllText(LatestAptitudesPath())));
        FusionRpg.Core.Progression.ProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.ProgressionTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "progression.v1.json"))));
        FusionRpg.Core.Status.StatusPolicy.Configure(
            FusionRpg.Core.Status.StatusTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "status.v1.json"))));
        FusionRpg.Core.Stats.Derived.StatsTuningHub.Configure(
            FusionRpg.Core.Stats.Derived.StatsTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "stats.v1.json"))));
        // species-build `battle-allocation` (module 10, path 4): this endpoint now resolves a species
        // allocation too (SpeciesAllocationSource), so its own fixture needs the same roster/tuning a
        // real server configures at startup -- brought in line here rather than relying on another
        // test class in the same process happening to have configured DemonSpeciesCatalog first.
        FusionRpg.Core.Demons.DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        FusionRpg.Core.Progression.SpeciesProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.SpeciesProgressionTuningLoader.Parse(
                File.ReadAllText(Path.Combine(RepoTuningDir(), "species-progression.v1.json"))));

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(_store);
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapAuraDerived();
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
    public async Task Get_unknownInstance_returns404()
    {
        var resp = await _http.GetAsync("/api/actors/not-a-real-instance/derived");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_freshPlantActor_returnsRealProgressionChannelsFromTheRealSubsystem()
    {
        var actor = _store.CreateUniqueActor(_playerId, "plant", typeId: 42);

        var resp = await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived");
        if (!resp.IsSuccessStatusCode) throw new Exception(await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<DerivedResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(actor.InstanceId, body!.InstanceId);

        // rpg.progression always contributes these two -- proves ActorHub actually resolved, not a stub.
        var power = Assert.Single(body.Channels, c => c.ChannelId == "progression.power");
        var powerSource = Assert.Single(power.Contributions);
        Assert.Equal("rpg.progression", powerSource.SourceId);

        var realm = Assert.Single(body.Channels, c => c.ChannelId == "progression.realm");
        Assert.Equal("rpg.progression", Assert.Single(realm.Contributions).SourceId);

        // Channels come back sorted -- a stable, deterministic contract for the web layer to render.
        var ids = body.Channels.Select(c => c.ChannelId).ToList();
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal).ToList(), ids);
    }

    [Fact]
    public async Task Get_zombieActor_resolvesTooNotJustPlant()
    {
        var actor = _store.CreateUniqueActor(_playerId, "zombie", typeId: 7);

        var resp = await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DerivedResponseDto>();
        Assert.NotNull(body);
        Assert.Contains(body!.Channels, c => c.ChannelId == "progression.power");
    }

    [Fact]
    public async Task Get_afterARealAptitudeAllocation_reflectsANonVacuousAptitudeSourcedContribution()
    {
        var actor = _store.CreateUniqueActor(_playerId, "plant", typeId: 1);

        var before = await (await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived"))
            .Content.ReadFromJsonAsync<DerivedResponseDto>();
        // AptitudeResolver.Resolve: "an aptitude with zero share contributes nothing -- not a
        // zero-valued modifier" (AptitudeResolver.cs:20-22), so an empty allocation names no source.
        Assert.DoesNotContain(before!.Channels, c => c.Contributions.Any(x => x.SourceId.StartsWith("aptitude.", StringComparison.Ordinal)));

        _store.SaveAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(_playerId),
            AptitudeAllocation.Single(AllocationScope.Commander, "Might", 50));

        var after = await (await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived"))
            .Content.ReadFromJsonAsync<DerivedResponseDto>();
        Assert.NotNull(after);
        // GG-49, non-vacuously: a real allocation now names a real source (AptitudeResolver.cs:61 --
        // SourceId is "aptitude.{edge.Source}", e.g. "aptitude.Might") on a real channel.
        Assert.Contains(after!.Channels, c => c.Contributions.Any(x => x.SourceId == "aptitude.Might"));
    }

    sealed class DerivedResponseDto
    {
        public string InstanceId { get; set; } = "";
        public List<DerivedChannelDto> Channels { get; set; } = new();
    }

    sealed class DerivedChannelDto
    {
        public string ChannelId { get; set; } = "";
        public double Value { get; set; }
        public List<DerivedContributionDto> Contributions { get; set; } = new();
    }

    sealed class DerivedContributionDto
    {
        public string SourceId { get; set; } = "";
        public string Op { get; set; } = "";
        public double Value { get; set; }
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
