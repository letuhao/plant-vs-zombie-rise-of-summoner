using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FusionRpg.E2E.Tests;

/// <summary>
/// D4: the full expedition loop in SIM — dispatch → force-due → collect → battles through
/// WebMatchService + rewards through the one economy; collect is exactly-once.
/// </summary>
[Collection("e2e")]
public class ExpeditionE2ETests : IAsyncLifetime
{
    readonly HttpClient _http;

    public ExpeditionE2ETests(RpgApiFactory factory)
    {
        _http = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        (await _http.PostAsJsonAsync("/api/test/reset", new { })).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<List<string>> SummonSquad(int take)
    {
        (await _http.PostAsJsonAsync("/api/test/seed-souls-demo?amount=2000", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/demons/summon", new
        {
            count = 10,
            correlationId = "exp-e2e-pull-" + Guid.NewGuid().ToString("N")
        })).EnsureSuccessStatusCode();
        var roster = await _http.GetFromJsonAsync<JsonElement>("/api/demons/1");
        return roster.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("actor").GetProperty("instanceId").GetString()!)
            .Take(take).ToList();
    }

    async Task<long> Balance() =>
        (await _http.GetFromJsonAsync<JsonElement>("/api/souls/1")).GetProperty("balance").GetInt64();

    [Fact]
    public async Task Full_loop_dispatch_force_due_collect()
    {
        var squad = await SummonSquad(2);
        var balanceBefore = await Balance();

        // Dispatch.
        var dispatch = await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-loop-1",
            tierId = "scout-30m",
            squad
        });
        dispatch.EnsureSuccessStatusCode();
        var row = await dispatch.Content.ReadFromJsonAsync<JsonElement>();
        var expeditionId = row.GetProperty("expedition").GetProperty("id").GetInt64();
        Assert.Equal("Dispatched", row.GetProperty("expedition").GetProperty("state").GetString());

        // Collect before due refuses.
        var early = await _http.PostAsJsonAsync($"/api/expeditions/{expeditionId}/collect", new { });
        Assert.False(early.IsSuccessStatusCode);

        // SIM force-due, then collect.
        (await _http.PostAsJsonAsync("/api/test/expedition-due", new { expeditionId }))
            .EnsureSuccessStatusCode();
        var collect = await _http.PostAsJsonAsync($"/api/expeditions/{expeditionId}/collect", new { });
        collect.EnsureSuccessStatusCode();
        var result = await collect.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Collected", result.GetProperty("state").GetString());
        var battles = result.GetProperty("battles").EnumerateArray().ToList();
        Assert.Single(battles); // scout-30m: one battle
        Assert.True(battles[0].GetProperty("runId").GetInt64() > 0);

        // Battles are real webrpg runs through the pipeline.
        var runs = await _http.GetFromJsonAsync<JsonElement>("/api/runs");
        var battleRun = runs.GetProperty("items").EnumerateArray()
            .Single(r => (r.GetProperty("matchKey").GetString() ?? "").StartsWith($"exp-{expeditionId}-"));
        Assert.Equal("webrpg-1", battleRun.GetProperty("game").GetString());

        // Any resolved battle earns through the one economy (victory or defeat both pay).
        Assert.True(await Balance() > balanceBefore);

        // The battle tick always drops a shard.
        var materials = await _http.GetFromJsonAsync<JsonElement>("/api/expeditions/1/materials");
        Assert.Contains(materials.GetProperty("items").EnumerateArray(),
            m => m.GetProperty("materialId").GetString() == "shard.common");

        // Squad released: the expedition list shows Collected and specimens are free again.
        var list = await _http.GetFromJsonAsync<JsonElement>("/api/expeditions/1");
        Assert.Equal("Collected", list.GetProperty("items").EnumerateArray()
            .Single(e => e.GetProperty("id").GetInt64() == expeditionId)
            .GetProperty("state").GetString());
    }

    [Fact]
    public async Task Collect_is_exactly_once()
    {
        var squad = await SummonSquad(2);
        var dispatch = await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-once-1",
            tierId = "scout-30m",
            squad
        });
        dispatch.EnsureSuccessStatusCode();
        var expeditionId = (await dispatch.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("expedition").GetProperty("id").GetInt64();
        (await _http.PostAsJsonAsync("/api/test/expedition-due", new { expeditionId })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync($"/api/expeditions/{expeditionId}/collect", new { })).EnsureSuccessStatusCode();

        var balanceAfterFirst = await Balance();
        var rosterAfterFirst = (await _http.GetFromJsonAsync<JsonElement>("/api/demons/1"))
            .GetProperty("items").EnumerateArray().Count();

        var second = await _http.PostAsJsonAsync($"/api/expeditions/{expeditionId}/collect", new { });
        Assert.False(second.IsSuccessStatusCode, "a second collect must refuse");
        Assert.Equal(balanceAfterFirst, await Balance());
        Assert.Equal(rosterAfterFirst, (await _http.GetFromJsonAsync<JsonElement>("/api/demons/1"))
            .GetProperty("items").EnumerateArray().Count());
    }

    [Fact]
    public async Task Instant_recall_closes_with_nothing()
    {
        var squad = await SummonSquad(1);
        var dispatch = await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-recall-1",
            tierId = "hunt-8h",
            squad
        });
        dispatch.EnsureSuccessStatusCode();
        var expeditionId = (await dispatch.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("expedition").GetProperty("id").GetInt64();
        var balanceBefore = await Balance();

        var recall = await _http.PostAsJsonAsync($"/api/expeditions/{expeditionId}/recall", new { });
        recall.EnsureSuccessStatusCode();
        var result = await recall.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Recalled", result.GetProperty("state").GetString());
        Assert.Empty(result.GetProperty("battles").EnumerateArray());
        Assert.Equal(balanceBefore, await Balance());
    }

    [Fact]
    public async Task Expedition_payloads_never_leak_the_sealed_seed()
    {
        var squad = await SummonSquad(1);
        var dispatch = await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-seed-leak",
            tierId = "scout-30m",
            squad
        });
        dispatch.EnsureSuccessStatusCode();
        var row = (await dispatch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expedition");
        Assert.False(row.TryGetProperty("seed", out _), "the sealed seed lets a client pre-read outcomes");
        Assert.False(row.TryGetProperty("correlationId", out _));
        Assert.False(row.TryGetProperty("squadJson", out _));

        var list = await _http.GetFromJsonAsync<JsonElement>("/api/expeditions/1");
        foreach (var item in list.GetProperty("items").EnumerateArray())
            Assert.False(item.TryGetProperty("seed", out _));
    }

    [Fact]
    public async Task Dispatch_replay_with_a_different_request_is_a_mismatch()
    {
        var squad = await SummonSquad(2);
        var first = await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-dup-req",
            tierId = "scout-30m",
            squad = new[] { squad[0] }
        });
        first.EnsureSuccessStatusCode();

        var mismatch = await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-dup-req",
            tierId = "scout-30m",
            squad = new[] { squad[1] } // different squad, same correlation
        });
        Assert.False(mismatch.IsSuccessStatusCode);
        Assert.Equal("correlation.mismatch",
            (await mismatch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reason").GetString());

        // The true replay still works.
        var replay = await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-dup-req",
            tierId = "scout-30m",
            squad = new[] { squad[0] }
        });
        replay.EnsureSuccessStatusCode();
        Assert.True((await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
    }

    [Fact]
    public async Task Dispatch_refuses_bad_requests()
    {
        var squad = await SummonSquad(1);
        Assert.False((await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-bad-1",
            tierId = "no-such-tier",
            squad
        })).IsSuccessStatusCode);
        Assert.False((await _http.PostAsJsonAsync("/api/expeditions/dispatch", new
        {
            correlationId = "exp-bad-2",
            tierId = "scout-30m",
            squad = Array.Empty<string>()
        })).IsSuccessStatusCode);
    }
}
