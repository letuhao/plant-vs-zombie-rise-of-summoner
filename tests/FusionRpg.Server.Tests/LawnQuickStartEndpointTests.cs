using System.Linq;
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
using FusionRpg.Data.Tests;

namespace FusionRpg.Server.Tests;

/// <summary>Live-lawn-quick-start session: POST /api/debug/lawn/quick-start against a REAL in-process
/// host, proving the orchestration logic (already-live skip, bad-level-type refusal, unknown-scenario
/// 404, and the polling state machine's own timeout behavior) without needing a live game — the actual
/// Unity-side enter-level/board.start/run-steps.done handshake can't be proven here, only the server's
/// own decision logic around it (matching this session's established precedent for debug-orchestration
/// code paths that terminate at "sent a command, waiting for a real game to answer").</summary>
public class LawnQuickStartEndpointTests : IAsyncLifetime
{
    DataTestStore _testStore = null!;
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        _testStore = DataTestStore.Create();
        _store = _testStore.Store;

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
        builder.Services.AddSingleton<FusionRpg.Server.DelveBattleSessionManager>();
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
        _testStore.Dispose();
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
    public async Task Post_boardCyclingRapidly_refusesFastWithMetric_neverPollsIntoTheTimeout()
    {
        // Observability gap found live 2026-09-14: a board stuck in a rapid match-end/retry loop (a
        // "quick" setup-skip with no real plants placed loses every wave instantly) produced a
        // confusing, silent "debug.level.enter did not ack" timeout with nothing pointing at the real
        // cause -- diagnosing it required manually diffing thousands of raw events. quick-start must
        // detect this directly, fail fast, and report the real cause plus how long it actually waited.
        _store.Heartbeat(RpgConstants.SourceInjector);
        for (var i = 0; i < 4; i++)
        {
            _store.InsertEvent(new EventEnvelope
            {
                T = DateTime.UtcNow.ToString("o"),
                Kind = "board.end",
                Payload = JsonSerializer.SerializeToElement(new { })
            });
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { timeoutSec = 30 });
        sw.Stop();

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("cycling", body!["error"].ToString());
        Assert.True(((JsonElement)body["recentBoardEnds"]).GetInt32() >= 3);
        Assert.True(body.ContainsKey("waitedMs"), "expected a waitedMs metric on the refusal");
        // The whole point: this must be a fast, immediate refusal, never a poll into the 30s timeout.
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"expected an immediate refusal, took {sw.ElapsedMilliseconds}ms — cycling detection did not short-circuit the poll");
    }

    [Fact]
    public async Task Post_latestMatchResultIsDefeat_selfHealsWithResetBoard_beforeEnterLevel()
    {
        // Real gap found live 2026-09-14: after a genuine defeat (match.result payload result:
        // "defeat"), spawn commands still queued but landed against a dead board until
        // debug.reset-board ran -- proven live (plant.spawn/debug.spawn.plant fired for real right
        // after a manual reset-board call). quick-start must self-heal this the same way it already
        // self-enables its two toggles, rather than silently leaving a caller to spawn into nothing.
        _store.Heartbeat(RpgConstants.SourceInjector);
        _store.InsertEvent(new EventEnvelope
        {
            T = DateTime.UtcNow.ToString("o"),
            Kind = "match.result",
            Payload = JsonSerializer.SerializeToElement(new { result = "defeat", activeMatchMs = 57311 })
        });
        SeedLiveBoardStart();
        var inbox = _app.Services.GetRequiredService<InjectorCommandInbox>();

        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { scenario = "lab-overlay", timeoutSec = 1 });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode); // honest timeout -- no real game answering
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.True(((JsonElement)body!["defeatReset"]).GetBoolean());

        var sent = inbox.Drain(int.MaxValue).Select(c => c.Name).ToList();
        var resetIdx = sent.FindIndex(n => n == "debug.reset-board");
        var skipProbeIdx = sent.FindIndex(n => n == "debug.skip-setup");
        Assert.True(resetIdx >= 0, "expected debug.reset-board to be sent after a detected defeat");
        Assert.True(resetIdx < skipProbeIdx, "the board must be reset before the mid-entry probe runs");
    }

    [Fact]
    public async Task Post_latestMatchResultIsWin_doesNotResetBoard()
    {
        _store.Heartbeat(RpgConstants.SourceInjector);
        _store.InsertEvent(new EventEnvelope
        {
            T = DateTime.UtcNow.ToString("o"),
            Kind = "match.result",
            Payload = JsonSerializer.SerializeToElement(new { result = "victory" })
        });
        SeedLiveBoardStart();
        var inbox = _app.Services.GetRequiredService<InjectorCommandInbox>();

        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { scenario = "lab-overlay", timeoutSec = 1 });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.False(((JsonElement)body!["defeatReset"]).GetBoolean());

        var sent = inbox.Drain(int.MaxValue).Select(c => c.Name).ToList();
        Assert.DoesNotContain("debug.reset-board", sent);
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
    public async Task Post_boardAlreadyLive_sendsSetupSkip_beforeWaveFreezeAndScenario()
    {
        // Real bug found live 2026-09-13: EnterGame opens the level, but the vanilla "Choose Your
        // Plants" seed-picker screen stays up until debug.skip-setup dismisses it — with it open,
        // waves never start and plants/zombies never act. quick-start must self-enable the
        // DEBUG-SETUP-SKIP gate and send debug.skip-setup before any wave/scenario work, the same way
        // it already self-enables DEBUG-LEVEL-ENTRY for entering the level in the first place.
        _store.Heartbeat(RpgConstants.SourceInjector);
        SeedLiveBoardStart();
        var inbox = _app.Services.GetRequiredService<InjectorCommandInbox>();

        var resp = await _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { scenario = "lab-overlay", timeoutSec = 1 });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode); // honest timeout -- no real game answering

        var sent = inbox.Drain(int.MaxValue).Select(c => c.Name).ToList();
        var toggleIdx = sent.FindIndex(n => n == "cheat.toggle");
        var skipIdx = sent.FindIndex(n => n == "debug.skip-setup");
        var freezeIdx = sent.FindIndex(n => n == "debug.wave-freeze");
        var runStepsIdx = sent.FindIndex(n => n == "debug.run-steps");

        Assert.True(toggleIdx >= 0, "expected a cheat.toggle command (DEBUG-SETUP-SKIP) to be sent");
        Assert.True(skipIdx >= 0, "expected debug.skip-setup to be sent");
        Assert.True(freezeIdx >= 0, "expected debug.wave-freeze to be sent");
        Assert.True(runStepsIdx >= 0, "expected debug.run-steps to be sent");
        Assert.True(toggleIdx < skipIdx, "the DEBUG-SETUP-SKIP toggle must be enabled before debug.skip-setup is sent");
        Assert.True(skipIdx < freezeIdx, "the seed-picker screen must be dismissed before waves are frozen");
        Assert.True(freezeIdx < runStepsIdx, "waves must be frozen before scenario steps run");
    }

    [Fact]
    public async Task Post_noBoardStartRecorded_butSetupSkipProbeSucceeds_neverAttemptsEnterLevel()
    {
        // Real bug found live 2026-09-14: some game profiles never emit board.start for a board that
        // already exists behind the seed-picker screen (confirmed on pvzrh-3.9 -- several full match
        // cycles, zero board.start events, ever), so FindLatestLiveBoardStart sees "no board" even
        // though a real one is sitting there mid-entry. quick-start used to always attempt
        // debug.enter-level in that case, which times out because EnterGame is undefined when called
        // a second time on an already-mid-entry level. The fix: probe with debug.skip-setup FIRST --
        // a success proves a real Board already exists, so enter-level must never be attempted.
        _store.Heartbeat(RpgConstants.SourceInjector);
        var inbox = _app.Services.GetRequiredService<InjectorCommandInbox>();

        var request = _http.PostAsJsonAsync("/api/debug/lawn/quick-start", new { scenario = "lab-overlay", timeoutSec = 2 });

        // Wait for the probe's debug.skip-setup to be enqueued, then answer it ok:true -- simulating
        // a real injector that is already sitting on the seed-picker (Board exists, not yet entered
        // through a fresh EnterGame call this request).
        List<CommandDto> queued = new();
        for (var i = 0; i < 80 && !queued.Any(c => c.Name == "debug.skip-setup"); i++)
        {
            await Task.Delay(25);
            queued.AddRange(inbox.Drain(int.MaxValue));
        }
        Assert.Contains(queued, c => c.Name == "debug.skip-setup");
        _store.InsertEvent(new EventEnvelope
        {
            T = DateTime.UtcNow.ToString("o"),
            Kind = "debug.setup.skip",
            Payload = JsonSerializer.SerializeToElement(new { ok = true, method = "quick", board = true, ui = true })
        });

        var resp = await request;
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode); // honest scenario timeout -- no real game answering run-steps

        var sentAfter = queued.Concat(inbox.Drain(int.MaxValue)).Select(c => c.Name).ToList();
        Assert.DoesNotContain("debug.enter-level", sentAfter);
        Assert.Contains("debug.wave-freeze", sentAfter);
        Assert.Contains("debug.run-steps", sentAfter);
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

    [Fact]
    public async Task Post_setupSkip_injectorNotConnected_refusesBeforeQueueing()
    {
        var resp = await _http.PostAsJsonAsync("/api/debug/setup/skip", new { });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("injector not connected", body!["error"].ToString());
    }

    [Fact]
    public async Task Post_setupSkip_connected_timesOutHonestlyWhenNoUnityAckArrives()
    {
        _store.Heartbeat(RpgConstants.SourceInjector);

        var resp = await _http.PostAsJsonAsync("/api/debug/setup/skip",
            new { method = "quick", timeoutSec = 1 });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("debug.setup.skip did not ack", body!["error"].ToString());
    }

    [Fact]
    public async Task Post_setupSkip_acknowledgement_returns_success()
    {
        _store.Heartbeat(RpgConstants.SourceInjector);
        var inbox = _app.Services.GetRequiredService<InjectorCommandInbox>();
        var request = _http.PostAsJsonAsync("/api/debug/setup/skip",
            new { method = "quick", timeoutSec = 5 });

        for (var i = 0; i < 40 && inbox.Count == 0; i++)
            await Task.Delay(25);

        Assert.True(inbox.Count > 0);
        _store.InsertEvent(new EventEnvelope
        {
            T = DateTime.UtcNow.ToString("o"),
            Kind = "debug.setup.skip",
            Payload = JsonSerializer.SerializeToElement(new { ok = true, method = "quick", ready = true })
        });

        var resp = await request;

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("quick", body!["method"].ToString());
    }

    [Fact]
    public async Task Post_setupSkip_unknownMethod_refusesBeforeQueueing()
    {
        _store.Heartbeat(RpgConstants.SourceInjector);

        var resp = await _http.PostAsJsonAsync("/api/debug/setup/skip",
            new { method = "hide-panel" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
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
