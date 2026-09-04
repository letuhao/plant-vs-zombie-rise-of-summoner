using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Contracts;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// world-stage W29: the `POST /api/world/{worldId}/bind-warden` endpoint — the first production
/// caller of <see cref="RpgStore.BindAsWarden"/>. Proves the documented two-step failure mode: step 1
/// (the demon-contract bind) is not rolled back when step 2 (filing the world order) fails, and the
/// correct client response — retrying the whole call — lands cleanly on both idempotent paths
/// without a double soul charge or a duplicate command row.
/// </summary>
public class WorldBindWardenEndpointTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    const string WorldId = "w29-bind-warden";

    static readonly DemonSpeciesDef Species = DemonSpeciesCatalog.All
        .First(s => s.Acquisition != DemonAcquisition.CaptureOnly && s.TraitPool.Count > 0);

    public async Task InitializeAsync()
    {
        ConfigureWorldTuningOnce();

        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-w29-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _store.AwardSouls(1, 50_000, "seed", "ops-bank");

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(_store);
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapHub<RpgHub>("/hub/rpg");
        _app.MapWorld();
        _app.MapWorldWarden();
        var test = _app.MapGroup("/api/test");
        test.MapWorldTest();
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };

        var created = await _http.PostAsJsonAsync("/api/test/world/create", new
        {
            worldId = WorldId, templateId = "first-light", seed = "7"
        });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    /// <summary>An unbound demon with a free capacity slot — <c>MintDemon</c> auto-binds up to base
    /// capacity, so a plain bindable demon needs a slot freed first, matching
    /// <c>WardenContractTests.cs</c>'s own established fixture.</summary>
    string MintUnboundWithFreeSlot()
    {
        string Mint()
        {
            var (specimen, _) = _store.MintDemon(1, new DemonMintSpec
            {
                SpeciesId = Species.SpeciesId,
                Side = Species.Side,
                GameTypeId = Species.GameTypeId,
                Rarity = Species.BaseRarity.ToId(),
                Variant = "normal",
                ElementPrimary = Species.ElementPrimary.ToElementId(),
                ElementSecondary = Species.ElementSecondary?.ToElementId(),
                TraitIds = new List<string> { Species.TraitPool[0] },
                Origin = "summon"
            });
            return specimen.Actor.InstanceId;
        }

        var bound = new List<string>();
        for (var i = 0; i < ContractPolicy.BaseSlots; i++) bound.Add(Mint());
        var id = Mint();
        Assert.Null(_store.GetContract(id));
        Assert.True(_store.ReleaseContract(1, bound[0]).Ok);
        return id;
    }

    async Task<BindWardenResultDto> Call(string commanderId, string sectorId, string instanceId)
    {
        var res = await _http.PostAsJsonAsync($"/api/world/{WorldId}/bind-warden", new
        {
            commanderId, sectorId, instanceId
        });
        return (await res.Content.ReadFromJsonAsync<BindWardenResultDto>())!;
    }

    [Fact]
    public async Task A_step_two_failure_leaves_step_one_bound_and_a_retry_lands_the_order_without_double_charging()
    {
        var instanceId = MintUnboundWithFreeSlot();
        var fee = ContractPolicy.UpkeepPerDay(Species.BaseRarity, ContractPolicy.PersonalityFor(instanceId));
        var before = _store.GetSoulBalance(1).Balance;

        // Step 2 fails here on purpose: "nobody" is not a real faction, so
        // WorldCommandAdmission refuses the order at submit time — but only *after* step 1 has
        // already run and charged the soul fee.
        var failed = await Call("nobody", "homeworld", instanceId);

        Assert.False(failed.Ok);
        Assert.Equal("commander.unknown", failed.Reason);
        Assert.True(_store.GetContract(instanceId)!.Warden, "step 1 must not be rolled back");
        Assert.True(_store.GetContract(instanceId)!.Bound);
        Assert.Equal(before - fee, _store.GetSoulBalance(1).Balance);
        Assert.Empty(_store.ListWorldCommands(WorldId, 0));

        // The documented recovery: retry the whole call with the real commander.
        var retried = await Call("dave", "homeworld", instanceId);

        Assert.True(retried.Ok, retried.Reason);
        Assert.Equal(instanceId, retried.InstanceId);
        Assert.False(retried.CommandReplayed, "the world order lands for the first time on this retry");
        // Step 1 replayed rather than re-charging — the whole point of it being idempotent.
        Assert.Equal(before - fee, _store.GetSoulBalance(1).Balance);

        var commands = _store.ListWorldCommands(WorldId, 0);
        var landed = Assert.Single(commands);
        Assert.Equal("dave", landed.CommanderId);
        Assert.Equal("homeworld", landed.SectorId);
        Assert.Equal(instanceId, landed.WardenId);

        // A second retry after full success hits both idempotent paths at once.
        var thirdCall = await Call("dave", "homeworld", instanceId);
        Assert.True(thirdCall.Ok, thirdCall.Reason);
        Assert.True(thirdCall.CommandReplayed);
        Assert.Equal(before - fee, _store.GetSoulBalance(1).Balance);
        Assert.Single(_store.ListWorldCommands(WorldId, 0));
    }

    static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static bool _tuningConfigured;

    static void ConfigureWorldTuningOnce()
    {
        if (_tuningConfigured) return;
        var tuningDir = Path.Combine(FindRepoRoot(), "data", "tuning");
        string Read(string name) => File.ReadAllText(Path.Combine(tuningDir, name));
        FusionRpg.Core.World.Loam.LoamPolicy.Configure(
            FusionRpg.Core.World.Loam.LoamTuningLoader.Parse(Read("loam.v2.json")));
        FusionRpg.Core.World.WorldTuningHub.Configure(
            FusionRpg.Core.World.WorldTuningLoader.Parse(Read("world.v4.json")));
        FusionRpg.Core.World.Ai.WorldAiPolicy.Configure(
            FusionRpg.Core.World.Ai.WorldAiTuningLoader.Parse(Read("ai.v2.json")));
        // Server.Tests' own PowerAndAptitudeTuningTestBootstrap module initializer configures
        // Power/Aptitude/DerivedStat/Rung/Aura only — ContractPolicy (this file's own MintDemon /
        // BindAsWarden fixtures) needs its own configure, matching every other Policy this file reads.
        ContractPolicy.Configure(ContractTuningLoader.Parse(Read("contracts.v1.json")));
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
}
