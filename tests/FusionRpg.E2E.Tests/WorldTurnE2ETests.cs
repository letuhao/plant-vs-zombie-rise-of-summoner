using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FusionRpg.E2E.Tests;

/// <summary>
/// The turn's HTTP surface (spec-turn-engine.md §Server): file orders, end your turn, read back what
/// happened. The barrier is the point — the world steps when the *last* commander commits, not when
/// the impatient one does.
/// </summary>
[Collection("e2e")]
public class WorldTurnE2ETests : IAsyncLifetime
{
    static readonly string[] Commanders = { "dave", "wild", "zomboss" };

    readonly HttpClient _http;

    public WorldTurnE2ETests(RpgApiFactory factory) => _http = factory.CreateClient();

    public async Task InitializeAsync() =>
        (await _http.PostAsJsonAsync("/api/test/reset", new { })).EnsureSuccessStatusCode();

    public Task DisposeAsync() => Task.CompletedTask;

    async Task Create(string worldId) =>
        (await _http.PostAsJsonAsync("/api/test/world/create", new
        {
            worldId, templateId = "first-light", seed = "5"
        })).EnsureSuccessStatusCode();

    async Task<JsonElement> Commit(string worldId, string commanderId)
    {
        var res = await _http.PostAsJsonAsync($"/api/world/{worldId}/commit", new { commanderId });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task The_world_steps_only_when_the_last_commander_ends_its_turn()
    {
        await Create("e2e-turn");

        var first = await Commit("e2e-turn", "dave");
        Assert.True(first.GetProperty("ok").GetBoolean());
        Assert.False(first.GetProperty("advanced").GetBoolean());
        Assert.Equal(0, first.GetProperty("currentTurn").GetInt32());

        await Commit("e2e-turn", "wild");
        var last = await Commit("e2e-turn", "zomboss");

        Assert.True(last.GetProperty("advanced").GetBoolean());
        Assert.Equal(1, last.GetProperty("currentTurn").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(last.GetProperty("stateHash").GetString()));
    }

    [Fact]
    public async Task An_order_filed_before_the_barrier_shows_up_in_the_turn_it_belongs_to()
    {
        await Create("e2e-order");

        var submit = await _http.PostAsJsonAsync("/api/world/e2e-order/commands", new
        {
            commanderId = "dave",
            commands = new[]
            {
                new { commandId = "m1", kind = "move", entityId = "e-dave-legion-1", lanePath = new[] { "l-home-ember" } }
            }
        });
        submit.EnsureSuccessStatusCode();
        var accepted = await submit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(accepted.GetProperty("results")[0].GetProperty("ok").GetBoolean());

        foreach (var commander in Commanders) await Commit("e2e-order", commander);

        var state = await _http.GetFromJsonAsync<JsonElement>("/api/world/e2e-order/state");
        var legion = state.GetProperty("entities").EnumerateArray()
            .Single(e => e.GetProperty("entityId").GetString() == "e-dave-legion-1");
        Assert.Equal("ember-hollow", legion.GetProperty("atSectorId").GetString());

        var report = await _http.GetFromJsonAsync<JsonElement>("/api/world/e2e-order/turn/0");
        Assert.Equal(0, report.GetProperty("turn").GetInt32());
        Assert.Contains(report.GetProperty("phases").EnumerateArray().Select(p => p.GetString()), p => p == "Movement");
        Assert.Contains(report.GetProperty("entries").EnumerateArray(),
            e => e.GetProperty("detail").GetString()!.Contains("ember-hollow"));
    }

    [Fact]
    public async Task A_turn_that_has_not_happened_yet_is_a_404_rather_than_an_empty_report()
    {
        await Create("e2e-future");

        var res = await _http.GetAsync("/api/world/e2e-future/turn/7");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Committing_for_a_faction_that_does_not_exist_is_refused()
    {
        await Create("e2e-ghost");

        var res = await _http.PostAsJsonAsync("/api/world/e2e-ghost/commit", new { commanderId = "nobody" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Committing_against_a_world_that_does_not_exist_is_a_404()
    {
        var res = await _http.PostAsJsonAsync("/api/world/no-such-world/commit", new { commanderId = "dave" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
