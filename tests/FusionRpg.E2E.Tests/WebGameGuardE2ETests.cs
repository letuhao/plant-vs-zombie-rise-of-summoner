using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FusionRpg.E2E.Tests;

/// <summary>
/// Wave B concurrency guard (audit 2026-08-21): a web-mode battle resolved while a real match
/// is live must not clear the live match's effect-grant session.
/// </summary>
[Collection("e2e")]
public class WebGameGuardE2ETests : IAsyncLifetime
{
    readonly HttpClient _http;

    public WebGameGuardE2ETests(RpgApiFactory factory)
    {
        _http = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var r = await _http.PostAsJsonAsync("/api/test/reset", new { });
        r.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<int> SessionGrantCount()
    {
        var doc = await _http.GetFromJsonAsync<JsonElement>("/api/debug/effects/session-grants");
        // Shape-tolerant count: array root or an items/grants property.
        if (doc.ValueKind == JsonValueKind.Array) return doc.GetArrayLength();
        foreach (var name in new[] { "items", "grants" })
            if (doc.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
                return arr.GetArrayLength();
        return 0;
    }

    [Fact]
    public async Task Web_board_lifecycle_does_not_wipe_live_match_grant_session()
    {
        // Live PvZ match with one session grant.
        (await _http.PostAsJsonAsync("/api/sim/hello", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/board/start", new { levelName = "guard" })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/debug/effect/grant", new
        {
            effectId = "fx.guard-test",
            ownerKey = "match",
            grantId = "guard-e2e-1"
        })).EnsureSuccessStatusCode();
        Assert.Equal(1, await SessionGrantCount());

        // A web-mode battle resolves mid-match: board.start + board.end under game=webrpg-1.
        var webKey = Guid.NewGuid().ToString("N");
        var t = DateTime.UtcNow.ToString("o");
        var post = await _http.PostAsJsonAsync("/api/events", new
        {
            events = new object[]
            {
                new { t, game = "webrpg-1", kind = "board.start", matchKey = webKey, payload = new { levelName = "web" } },
                new { t, game = "webrpg-1", kind = "match.result", matchKey = webKey, payload = new { result = "victory" } },
                new { t, game = "webrpg-1", kind = "board.end", matchKey = webKey, payload = new { levelName = "web" } }
            }
        });
        post.EnsureSuccessStatusCode();
        await _http.GetAsync("/api/test/snapshot"); // flush the writer

        // The live match's grant session survives; a pvzrh board.end would have cleared it.
        Assert.Equal(1, await SessionGrantCount());
    }
}
