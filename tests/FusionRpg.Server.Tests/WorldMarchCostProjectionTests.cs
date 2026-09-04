using System.Net.Http.Json;
using System.Text.Json;
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
/// world-stage W9 (re-homed from `world-targeting`): a lane's march cost for a named legion is
/// projected server-side via `?forLegion=`, empty when no legion is named, and fog-honest — priced
/// against the viewer's *believed* climate, never truth, so an un-scouted ley discount does not
/// silently apply.
/// </summary>
public class WorldMarchCostProjectionTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    const string WorldId = "w9-projection";
    const string LegionId = "e-dave-legion-1";

    public async Task InitializeAsync()
    {
        ConfigureWorldTuningOnce();

        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-w9-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();

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
        var test = _app.MapGroup("/api/test");
        test.MapWorldTest();
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };

        var created = await _http.PostAsJsonAsync("/api/test/world/create", new
        {
            worldId = WorldId, templateId = "two-hearths", seed = "7"
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

    SqliteConnection OpenHot()
    {
        var db = new SqliteConnection($"Data Source={_store.HotPath}");
        db.Open();
        return db;
    }

    void Exec(string sql, params (string Name, object Value)[] parameters)
    {
        using var db = OpenHot();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        Assert.Equal(1, cmd.ExecuteNonQuery());
    }

    async Task<JsonElement> StateAsync(string? forLegion = null)
    {
        var url = $"/api/world/{WorldId}/state?asFaction=dave";
        if (forLegion is not null) url += $"&forLegion={forLegion}";
        return await _http.GetFromJsonAsync<JsonElement>(url);
    }

    [Fact]
    public async Task March_costs_are_empty_when_no_legion_is_selected()
    {
        var state = await StateAsync();
        Assert.Empty(state.GetProperty("marchCosts").EnumerateObject());
    }

    [Fact]
    public async Task A_selected_legions_march_cost_is_real_lane_cost_math()
    {
        // l-df1-df2: corridor, length 800, no hazard -> 800 * 700‰ = 560 (data/tuning/world.v5.json's
        // corridor multiplier), a plain check that the projection isn't a placeholder.
        var state = await StateAsync(LegionId);
        var costs = state.GetProperty("marchCosts");
        Assert.Equal(560, costs.GetProperty("l-df1-df2").GetInt32());
    }

    [Fact]
    public async Task An_unscouted_leys_discount_does_not_apply_priced_against_belief_not_truth()
    {
        // e-dave-legion-1's banner is Ice (peashooterzombie=Earth, conezombie=Ice, paperzombie=Light,
        // all singletons -> first in ElementTypeId's own declared order wins: Ice).
        // Make l-dh-df1 a ley lane, and give d-flank-1's TRUTH climate a matching Ice — if the
        // projection read truth, the ley discount would apply (576). Its BELIEVED climate (Dave
        // scouted it before this) stays Earth, so a fog-honest reading must NOT discount (720).
        Exec("UPDATE rpg_world_lanes SET type_id = 'ley' WHERE world_id = $w AND lane_id = 'l-dh-df1';", ("$w", WorldId));
        Exec("UPDATE rpg_world_sectors SET climate = 'Ice' WHERE world_id = $w AND sector_id = 'd-flank-1';", ("$w", WorldId));

        var state = await StateAsync(LegionId);
        var costs = state.GetProperty("marchCosts");
        // Length 800 * ley 900‰ = 720, no discount applied.
        Assert.Equal(720, costs.GetProperty("l-dh-df1").GetInt32());
    }

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>Same tuning bootstrap `WorldSectorProjectionTests` needs — see its own doc comment.</summary>
    static bool _tuningConfigured;
    static void ConfigureWorldTuningOnce()
    {
        if (_tuningConfigured) return;
        var tuningDir = Path.Combine(FindRepoRoot(), "data", "tuning");
        string Read(string name) => File.ReadAllText(Path.Combine(tuningDir, name));
        FusionRpg.Core.World.Loam.LoamPolicy.Configure(
            FusionRpg.Core.World.Loam.LoamTuningLoader.Parse(Read("loam.v4.json")));
        FusionRpg.Core.World.WorldTuningHub.Configure(
            FusionRpg.Core.World.WorldTuningLoader.Parse(Read("world.v5.json")));
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
