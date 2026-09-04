using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Core.World.Turn;
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
/// world-stage W15: `WorldStateDto.Calendar` carries the current turn's roll only — never the seed,
/// never a future roll (both of which would let a client enumerate the campaign's plague months).
/// </summary>
public class WorldCalendarProjectionTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    const string WorldId = "w15-calendar";
    const ulong Seed = 7;

    public async Task InitializeAsync()
    {
        ConfigureWorldTuningOnce();

        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-w15-" + Guid.NewGuid().ToString("N"));
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
            worldId = WorldId, templateId = "two-hearths", seed = Seed.ToString()
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

    void SetCurrentTurn(int turn)
    {
        using var db = new SqliteConnection($"Data Source={_store.HotPath}");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE rpg_worlds SET current_turn = $t WHERE world_id = $w;";
        cmd.Parameters.AddWithValue("$t", turn);
        cmd.Parameters.AddWithValue("$w", WorldId);
        Assert.Equal(1, cmd.ExecuteNonQuery());
    }

    async Task<JsonElement> State() =>
        await _http.GetFromJsonAsync<JsonElement>($"/api/world/{WorldId}/state?asFaction=dave");

    [Fact]
    public async Task Turn_zero_carries_a_blank_roll()
    {
        var calendar = (await State()).GetProperty("calendar");
        Assert.False(calendar.GetProperty("weekBoundary").GetBoolean());
        Assert.False(calendar.GetProperty("monthBoundary").GetBoolean());
        Assert.False(calendar.GetProperty("specialWeek").GetBoolean());
        Assert.False(calendar.GetProperty("specialMonth").GetBoolean());
        Assert.False(calendar.GetProperty("plague").GetBoolean());
        Assert.Equal(7, calendar.GetProperty("daysPerWeek").GetInt32());
        Assert.Equal(4, calendar.GetProperty("weeksPerMonth").GetInt32());
    }

    [Fact]
    public async Task A_week_boundary_turn_carries_the_real_roll_matching_turn_calendar_directly()
    {
        // DaysPerWeek is 7 (data/tuning/world.v4.json) — turn 7 is the first week boundary.
        SetCurrentTurn(7);
        var expected = TurnCalendar.Roll(7, Seed);

        var calendar = (await State()).GetProperty("calendar");
        Assert.Equal(expected.WeekBoundary, calendar.GetProperty("weekBoundary").GetBoolean());
        Assert.Equal(expected.MonthBoundary, calendar.GetProperty("monthBoundary").GetBoolean());
        Assert.Equal(expected.SpecialWeek, calendar.GetProperty("specialWeek").GetBoolean());
        Assert.Equal(expected.SpecialMonth, calendar.GetProperty("specialMonth").GetBoolean());
        Assert.Equal(expected.Plague, calendar.GetProperty("plague").GetBoolean());
        Assert.True(expected.WeekBoundary, "turn 7 must actually be a week boundary, or this test proves nothing");
    }

    [Fact]
    public async Task No_seed_and_no_future_roll_ever_reach_the_wire()
    {
        var raw = await (await _http.GetAsync($"/api/world/{WorldId}/state?asFaction=dave")).Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);

        // Structural: no "seed" key anywhere in the whole response, not just absent from Calendar.
        Assert.DoesNotContain("\"seed\"", raw, StringComparison.OrdinalIgnoreCase);

        // No future roll: the calendar object has exactly the seven fields this turn's roll needs,
        // nothing shaped like a next-turn or next-week preview.
        var calendar = (await State()).GetProperty("calendar");
        var propertyNames = calendar.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[] { "daysPerWeek", "monthBoundary", "plague", "specialMonth", "specialWeek", "weekBoundary", "weeksPerMonth" },
            propertyNames);
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
            FusionRpg.Core.World.Loam.LoamTuningLoader.Parse(Read("loam.v1.json")));
        FusionRpg.Core.World.WorldTuningHub.Configure(
            FusionRpg.Core.World.WorldTuningLoader.Parse(Read("world.v4.json")));
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
