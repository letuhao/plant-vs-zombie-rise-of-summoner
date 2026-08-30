using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Data;
using FusionRpg.Data.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>Live-lawn-quick-start session: POST /api/debug/lawn/quick-start against a REAL in-process
/// host, proving the orchestration logic (already-live skip, bad-level-type refusal, unknown-scenario
/// 404, and the polling state machine's own timeout behavior) without needing a live game — the actual
/// Unity-side enter-level/board.start/run-steps.done handshake can't be proven here, only the server's
/// own decision logic around it (matching this session's established precedent for debug-orchestration
/// code paths that terminate at "sent a command, waiting for a real game to answer").</summary>
public class LawnQuickStartEndpointTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-lawnquickstart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(_store);
        builder.Services.AddSingleton<InjectorCommandInbox>();
        builder.Services.AddSingleton<EffectGrantSession>();
        builder.Services.AddSingleton<IHotCompactor>(sp => new HotCompactor(sp.GetRequiredService<RpgStore>()));
        builder.Services.AddSingleton<EventIngest>();
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapHub<RpgHub>("/hub/rpg");
        _app.MapDebug();
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    void SeedLiveBoardStart(string levelType = "Adventure")
    {
        _store.InsertEvent(new EventEnvelope
        {
            T = DateTime.UtcNow.ToString("o"),
            Kind = "board.start",
            Payload = JsonSerializer.SerializeToElement(new { levelType, boardLevel = 1, levelName = "" })
        });
    }

    [Fact]
    public async Task Post_injectorNotConnected_refusesBeforeTouchingAnything()
    {
        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("injector not connected", body!["error"].ToString());
    }

    [Fact]
    public async Task Post_boardAlreadyLive_skipsEnterLevel_andRefusesOnBadLevelTypeBeforeAnyWait()
    {
        _store.Heartbeat(RpgConstants.SourceInjector);
        SeedLiveBoardStart(levelType: "Explore");

        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { timeoutSec = 1 });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("levelType=Explore", body!["error"].ToString());
    }

    [Fact]
    public async Task Post_boardAlreadyLive_unknownScenario_returns404BeforeAnyWait()
    {
        _store.Heartbeat(RpgConstants.SourceInjector);
        SeedLiveBoardStart();

        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { scenario = "not-a-real-scenario", timeoutSec = 1 });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_boardAlreadyLive_realScenario_sendsFreezeAndScenario_thenTimesOutHonestly()
    {
        // No real game is answering, so debug.run-steps.done never arrives -- this proves the
        // orchestration reaches and dispatches the real scenario (skipping enter-level entirely
        // because a board is already live) rather than claiming success it never observed.
        _store.Heartbeat(RpgConstants.SourceInjector);
        SeedLiveBoardStart();

        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { scenario = "lab-overlay", timeoutSec = 1 });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("did not complete within 1s", body!["error"].ToString());
    }

    [Fact]
    public async Task Post_noBoardLive_attemptsEnterLevel_andTimesOutHonestlyWhenNothingAcks()
    {
        _store.Heartbeat(RpgConstants.SourceInjector);
        // No board.start seeded -- the endpoint must attempt debug.enter-level and wait for an ack
        // that (with no real game) will never come, timing out honestly rather than guessing.
        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { timeoutSec = 1 });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("debug.level.enter did not ack", body!["error"].ToString());
    }

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
