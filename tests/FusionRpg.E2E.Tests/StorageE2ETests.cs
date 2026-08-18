using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Contracts;
using Xunit;

namespace FusionRpg.E2E.Tests;

[Collection("e2e")]
public class StorageE2ETests : IAsyncLifetime
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    readonly HttpClient _http;

    public StorageE2ETests(RpgApiFactory factory)
    {
        _http = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var r = await _http.PostAsJsonAsync("/api/test/reset", new { });
        r.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task FlushIngest() => (await _http.GetAsync("/api/test/snapshot")).EnsureSuccessStatusCode();

    [Fact]
    public async Task Summary_and_archives_list()
    {
        var summary = await _http.GetFromJsonAsync<StorageSummaryDto>("/api/storage/summary", Json);
        Assert.NotNull(summary);
        Assert.True(summary!.ArchiveCount >= 0);
        Assert.True(summary.ClosedRunsStillHot >= 0);
        Assert.True(summary.OpenRuns >= 0);
        Assert.False(summary.ActivityOverTail);
        Assert.False(summary.XpOverTail);

        var archives = await _http.GetFromJsonAsync<JsonElement>("/api/storage/archives", Json);
        Assert.True(archives.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    public async Task Delete_archives_empty_ok()
    {
        var r = await _http.PostAsJsonAsync("/api/storage/archives/delete", new { uris = Array.Empty<string>() });
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<StoragePurgeResultDto>(Json);
        Assert.NotNull(body);
        Assert.Equal(0, body!.Deleted);
        Assert.Equal(0, body.Refused);
    }

    [Fact]
    public async Task Delete_archives_path_escape_refused()
    {
        var r = await _http.PostAsJsonAsync("/api/storage/archives/delete", new { uris = new[] { "../evil.sqlite" } });
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<StoragePurgeResultDto>(Json);
        Assert.NotNull(body);
        Assert.Equal(0, body!.Deleted);
        Assert.True(body.Refused >= 1);
    }

    [Fact]
    public async Task Purge_open_run_refused()
    {
        (await _http.PostAsJsonAsync("/api/sim/hello", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/board/start", new { levelName = "StorageOpen" })).EnsureSuccessStatusCode();
        await FlushIngest();

        var runs = await _http.GetFromJsonAsync<JsonElement>("/api/runs", Json);
        var open = runs.GetProperty("items").EnumerateArray()
            .First(e => e.TryGetProperty("endedUtc", out var ended) &&
                        (ended.ValueKind == JsonValueKind.Null || string.IsNullOrEmpty(ended.GetString())));
        var runId = open.GetProperty("id").GetInt64();

        var r = await _http.PostAsJsonAsync("/api/storage/runs/purge-capture", new { runIds = new[] { runId } });
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<StoragePurgeResultDto>(Json);
        Assert.NotNull(body);
        Assert.Equal(0, body!.Deleted);
        Assert.Equal(1, body.Refused);
    }

    [Fact]
    public async Task Purge_while_ActiveBound_returns_409()
    {
        var create = await _http.PostAsJsonAsync("/api/unique/actors", new { side = "plant", typeId = 1 });
        create.EnsureSuccessStatusCode();
        var actor = await create.Content.ReadFromJsonAsync<UniqueActorDto>(Json);
        Assert.NotNull(actor);

        var corr = "e2e-purge-" + Guid.NewGuid().ToString("N")[..8];
        var deploy = await _http.PostAsJsonAsync($"/api/unique/actors/{actor!.InstanceId}/deploy",
            new { correlationId = corr, matchKey = "m-active-bound" });
        deploy.EnsureSuccessStatusCode();

        var t = DateTime.UtcNow.ToString("o");
        (await _http.PostAsJsonAsync("/api/events", new
        {
            events = new[]
            {
                new
                {
                    t,
                    game = RpgConstants.GameId,
                    kind = "pvz.spawn.extra.ack",
                    matchKey = "m-active-bound",
                    payload = new { correlationId = corr, ptr = "0xE2E", side = "plant", typeId = 1 }
                }
            }
        })).EnsureSuccessStatusCode();
        await FlushIngest();

        var get = await _http.GetFromJsonAsync<UniqueActorDto>($"/api/unique/actors/{actor.InstanceId}", Json);
        Assert.Equal(UniqueActorPhases.ActiveBound, get!.Phase);

        (await _http.PostAsJsonAsync("/api/sim/hello", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/board/start", new { levelName = "PurgeWithBound" })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/plant/spawn", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/board/end", new { summary = new { } })).EnsureSuccessStatusCode();
        await FlushIngest();

        var runs = await _http.GetFromJsonAsync<JsonElement>("/api/runs", Json);
        var closed = runs.GetProperty("items").EnumerateArray()
            .First(e => e.TryGetProperty("endedUtc", out var ended) &&
                        ended.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrEmpty(ended.GetString()));
        var runId = closed.GetProperty("id").GetInt64();

        var r = await _http.PostAsJsonAsync("/api/storage/runs/purge-capture", new { runIds = new[] { runId } });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        var err = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("unique.active_bound", err.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Purge_closed_run_ok()
    {
        (await _http.PostAsJsonAsync("/api/sim/hello", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/board/start", new { levelName = "StorageClosed" })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/plant/spawn", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/board/end", new { summary = new { } })).EnsureSuccessStatusCode();
        await FlushIngest();

        var runs = await _http.GetFromJsonAsync<JsonElement>("/api/runs", Json);
        var closed = runs.GetProperty("items").EnumerateArray()
            .First(e => e.TryGetProperty("endedUtc", out var ended) &&
                        ended.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrEmpty(ended.GetString()));
        var runId = closed.GetProperty("id").GetInt64();

        var r = await _http.PostAsJsonAsync("/api/storage/runs/purge-capture", new { runIds = new[] { runId } });
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<StoragePurgeResultDto>(Json);
        Assert.NotNull(body);
        Assert.Equal(1, body!.Deleted);
        Assert.Equal(0, body.Refused);

        var trim = await _http.PostAsJsonAsync("/api/storage/trim-tails", new { });
        trim.EnsureSuccessStatusCode();
        var trimBody = await trim.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(trimBody.TryGetProperty("ok", out var ok) && ok.GetBoolean());
    }

    [Fact]
    public async Task Delete_closed_run_ok()
    {
        (await _http.PostAsJsonAsync("/api/sim/hello", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/board/start", new { levelName = "StorageDelete" })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/plant/spawn", new { })).EnsureSuccessStatusCode();
        (await _http.PostAsJsonAsync("/api/sim/board/end", new { summary = new { } })).EnsureSuccessStatusCode();
        await FlushIngest();

        var runs = await _http.GetFromJsonAsync<JsonElement>("/api/runs", Json);
        var closed = runs.GetProperty("items").EnumerateArray()
            .First(e => e.TryGetProperty("endedUtc", out var ended) &&
                        ended.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrEmpty(ended.GetString()));
        var runId = closed.GetProperty("id").GetInt64();

        var r = await _http.PostAsJsonAsync("/api/storage/runs/delete", new { runIds = new[] { runId } });
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<StoragePurgeResultDto>(Json);
        Assert.NotNull(body);
        Assert.Equal(1, body!.Deleted);
        Assert.Equal(0, body.Refused);

        var after = await _http.GetFromJsonAsync<JsonElement>("/api/runs", Json);
        Assert.DoesNotContain(
            after.GetProperty("items").EnumerateArray(),
            e => e.GetProperty("id").GetInt64() == runId);
    }
}
