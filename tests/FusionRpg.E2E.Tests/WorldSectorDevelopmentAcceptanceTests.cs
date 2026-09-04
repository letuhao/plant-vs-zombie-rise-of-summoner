using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Turn;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.E2E.Tests;

/// <summary>
/// world-map W59 (spec-sector-development.md §1/§2): the phase's own acceptance run. Forty scripted
/// turns end with Dave commanding several legions **he chose to raise**, not a template handout —
/// the same distinction <see cref="RaiseResolver"/>'s own doc draws between a pulse and a legion.
///
/// The script is adaptive, not a fixed turn table: it moves the starting legion onto `ember-hollow`,
/// re-issues `clear` against whichever slot is still `Intact` until none are (a `GuardLight` fight
/// against the unmodified starting legion is not assumed to resolve in exactly one turn — this
/// scenario, unlike <c>WorldWaveOneAcceptanceTests</c>, does not reinforce the template's own roster),
/// then claims, then spends every remaining turn spamming `raise` at both Seats it holds
/// (`homeworld`, never guarded; `ember-hollow`, once claimed — a cleared lair on that same sector
/// quadruples its own pulse, `growth.lairMultiplierMilli`). A `raise` a sector cannot yet afford is
/// dropped by <see cref="RaiseResolver"/> rather than refused at admission, so resubmitting it every
/// turn is harmless and self-correcting — the count that comes out the other end is genuinely
/// measured against `data/tuning/world.v5.json`'s tuning, not hand-timed against it.
///
/// Runs over the real HTTP surface (`RpgApiFactory`), the same one a browser drives — this is the
/// acceptance run, not a unit test of the resolvers `RaiseThreadingTests`/`GrowthPhasesTests` already
/// cover in isolation.
/// </summary>
[Collection("e2e")]
public class WorldSectorDevelopmentAcceptanceTests : IAsyncLifetime
{
    const int Turns = 40;
    const string WorldId = "w59-first-light";
    const string Dave = "dave";
    const string StarterLegion = "e-dave-legion-1";

    readonly RpgApiFactory _factory;
    readonly HttpClient _http;

    public WorldSectorDevelopmentAcceptanceTests(RpgApiFactory factory)
    {
        _factory = factory;
        _http = factory.CreateClient();
    }

    public async Task InitializeAsync() =>
        (await _http.PostAsJsonAsync("/api/test/reset", new { })).EnsureSuccessStatusCode();

    public Task DisposeAsync() => Task.CompletedTask;

    async Task Create(string worldId, string seed) =>
        (await _http.PostAsJsonAsync("/api/test/world/create", new
        {
            worldId, templateId = WorldTemplateCatalog.FirstLightId, seed
        })).EnsureSuccessStatusCode();

    async Task<JsonElement> State(string worldId) =>
        await _http.GetFromJsonAsync<JsonElement>($"/api/world/{worldId}/state");

    async Task<int> OpenTurn(string worldId) =>
        (await State(worldId)).GetProperty("currentTurn").GetInt32();

    async Task Submit(string worldId, string commanderId, IReadOnlyList<object> commands) =>
        (await _http.PostAsJsonAsync($"/api/world/{worldId}/commands", new
        {
            commanderId, commands
        })).EnsureSuccessStatusCode();

    async Task<JsonElement> Commit(string worldId, string commanderId)
    {
        var turn = await OpenTurn(worldId);
        var res = await _http.PostAsJsonAsync($"/api/world/{worldId}/commit", new { commanderId, turn });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Dave's own orders this turn, decided from what the last `/state` actually shows rather than a
    /// pre-baked turn number — <see cref="Turns"/>'s own doc explains why. Wild and Zomboss self-fill:
    /// both carry a `PolicyId` on `first-light`, so ending Dave's turn alone releases the barrier,
    /// the same discipline <see cref="WorldTurnE2ETests"/> already relies on.
    /// </summary>
    static List<object> DecideDave(JsonElement state, int turn)
    {
        var commands = new List<object>();

        var legion = state.GetProperty("entities").EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("entityId").GetString() == StarterLegion);
        var atEmberHollow = legion.ValueKind != JsonValueKind.Undefined
            && legion.GetProperty("atSectorId").GetString() == "ember-hollow";

        var ember = state.GetProperty("sectors").EnumerateArray()
            .Single(s => s.GetProperty("sectorId").GetString() == "ember-hollow");
        var emberOwner = ember.TryGetProperty("ownerFactionId", out var o) && o.ValueKind == JsonValueKind.String
            ? o.GetString()
            : null;

        if (!atEmberHollow && emberOwner != Dave)
        {
            // Turn zero only: the legion has not been ordered anywhere yet.
            if (turn == 0)
                commands.Add(new
                {
                    commandId = "t0-move", kind = WorldCommandKinds.Move,
                    entityId = StarterLegion, lanePath = new[] { "l-home-ember" }
                });
            // Any later turn while still en route: nothing to add — the move already in flight
            // needs no repeat, and there is nothing else legal to order yet.
        }
        else if (emberOwner != Dave)
        {
            var intactSlot = ember.GetProperty("slots").EnumerateArray()
                .Where(sl => sl.GetProperty("guardState").GetString() == "Intact")
                .Select(sl => sl.GetProperty("slotIndex").GetInt32())
                .Cast<int?>()
                .FirstOrDefault();

            commands.Add(intactSlot is { } slot
                ? new
                {
                    commandId = $"t{turn}-clear", kind = WorldCommandKinds.Clear,
                    entityId = StarterLegion, sectorId = "ember-hollow", slotIndex = slot
                }
                : new
                {
                    commandId = $"t{turn}-claim", kind = WorldCommandKinds.Claim,
                    entityId = StarterLegion, sectorId = "ember-hollow"
                });
        }
        else
        {
            // The steady state: both held Seats get a raise order every remaining turn.
            // `RaiseResolver` drops whichever cannot afford it (`raise.cannot-afford`) rather than
            // refusing the command outright, so this never needs to know the exact turn stock
            // crosses `growth.raiseCostPoints` — it only needs to keep asking.
            commands.Add(new { commandId = $"t{turn}-raise-home", kind = WorldCommandKinds.Raise, sectorId = "homeworld" });
            commands.Add(new { commandId = $"t{turn}-raise-ember", kind = WorldCommandKinds.Raise, sectorId = "ember-hollow" });
        }

        return commands;
    }

    async Task<List<string>> PlayFortyTurns(string worldId, string seed)
    {
        await Create(worldId, seed);

        var hashes = new List<string>();
        for (var turn = 0; turn < Turns; turn++)
        {
            var state = await State(worldId);
            var dave = DecideDave(state, turn);
            if (dave.Count > 0)
                await Submit(worldId, Dave, dave);

            var result = await Commit(worldId, Dave);
            Assert.True(result.GetProperty("advanced").GetBoolean(), $"turn {turn} did not advance");
            hashes.Add(result.GetProperty("stateHash").GetString()!);
        }

        return hashes;
    }

    [Fact]
    public async Task Forty_turns_leave_Dave_commanding_a_legion_count_inside_the_calibrated_target()
    {
        await PlayFortyTurns(WorldId, seed: "59");

        var state = await State(WorldId);
        var legionCount = state.GetProperty("entities").EnumerateArray()
            .Count(e => e.GetProperty("kind").GetString() == "Legion"
                && e.GetProperty("ownerFactionId").GetString() == Dave);

        // growth.legionTarget (data/tuning/world.v5.json): 6-10 by turn 40. A calibration
        // assertion over tuning, not an engine limit — if this ever falls outside the range the
        // tuning moves, not this test's meaning (RecruitPolicy.LegionTarget's own doc comment).
        Assert.InRange(legionCount, 6, 10);
    }

    [Fact]
    public async Task A_season_boundary_is_visible_inside_the_forty_turns()
    {
        await PlayFortyTurns("w59-season", seed: "59");

        // data/tuning/world.v5.json: daysPerWeek 7 x weeksPerMonth 4 x monthsPerSeason 1 = a season
        // every 28 turns. A turn *report*'s index N is the step that advances CurrentTurn N -> N+1
        // (WorldTwentyTurnCheckpointTests's own week-boundary check follows the identical off-by-one:
        // report 6 carries the week-7 boundary, not report 7) — so the season entry that fires when
        // CalendarRoll reads the *new* current turn 28 lands under report index 27.
        var report = await _http.GetFromJsonAsync<JsonElement>("/api/world/w59-season/turn/27");
        Assert.Contains(report.GetProperty("entries").EnumerateArray(),
            e => e.GetProperty("kind").GetString() == TurnReportKinds.Calendar
                && e.GetProperty("subject").GetString() == "season");
    }

    [Fact]
    public async Task The_same_script_and_seed_replay_to_the_same_forty_hashes()
    {
        var a = await PlayFortyTurns("w59-replay-a", seed: "59");
        var b = await PlayFortyTurns("w59-replay-b", seed: "59");

        Assert.Equal(a, b);
        Assert.Equal(Turns, a.Distinct().Count()); // every turn actually moved the world
    }

    /// <summary>
    /// The sharpest check in the phase, and the one every other acceptance test in this project
    /// carries: replaying the persisted command log through the *pure* engine — no HTTP, no AI
    /// policy re-run, nothing but `(seed, template, command log)` — reproduces the exact hashes the
    /// store wrote. `RpgApiFactory` runs the server against `DataDir` on disk, so a second `RpgStore`
    /// opened on that same directory after the run sees exactly what the HTTP surface persisted,
    /// auto-filled Wild/Zomboss orders included.
    /// </summary>
    [Fact]
    public async Task The_pure_engine_reproduces_the_stored_hashes_from_the_command_log_alone()
    {
        var stored = await PlayFortyTurns("w59-pure-replay", seed: "59");

        var store = new RpgStore(_factory.DataDir);
        store.Init();

        var world = WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 59, "w59-pure-replay");
        var replayed = new List<string>();
        for (var turn = 0; turn < Turns; turn++)
        {
            var result = TurnEngine.Step(world, store.ListWorldCommands("w59-pure-replay", turn), seed: 59);
            world = result.World;
            replayed.Add(result.StateHash);
        }

        Assert.Equal(stored, replayed);
    }

    /// <summary>
    /// The order Dave's own commands arrive in within one turn changes nothing — the established
    /// invariant <see cref="TurnEngineTests.The_order_commands_arrive_in_does_not_matter"/> already
    /// proves in isolation (there, three single-faction `stand-fast` commands). This scenario is the
    /// one place in the phase that gives one commander *two* commands in the same turn for real
    /// (`raise` at both held Seats, every steady-state turn) — this proves the same invariant holds
    /// for that shape too, grounded in this scenario's own real state rather than a synthetic one.
    ///
    /// (A raw <see cref="WorldState.Entities"/> list reversed at the *start* of a run is a different
    /// claim and does not hold in this codebase — checked directly: even a single `stand-fast` turn
    /// hashes differently, because nothing re-sorts a collection no phase that turn happens to touch.
    /// Only command order within a turn is an established invariant, which is what this checks.)
    /// </summary>
    [Fact]
    public async Task The_order_Daves_two_raise_commands_arrive_in_changes_nothing()
    {
        const int ProbeTurn = 10; // steady state: ember-hollow claimed at turn 3, well past it

        var stored = await PlayFortyTurns("w59-cmdorder", seed: "59");

        var store = new RpgStore(_factory.DataDir);
        store.Init();

        var world = WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 59, "w59-cmdorder");
        for (var turn = 0; turn < ProbeTurn; turn++)
            world = TurnEngine.Step(world, store.ListWorldCommands("w59-cmdorder", turn), seed: 59).World;

        var forwardCommands = store.ListWorldCommands("w59-cmdorder", ProbeTurn);
        Assert.Equal(2, forwardCommands.Count(c => c.CommanderId == Dave)); // both raises, as scripted
        var reversedCommands = forwardCommands.Reverse().ToList();

        var forwardResult = TurnEngine.Step(world, forwardCommands, seed: 59);
        var reversedResult = TurnEngine.Step(world, reversedCommands, seed: 59);

        Assert.Equal(forwardResult.StateHash, reversedResult.StateHash);
        Assert.Equal(stored[ProbeTurn], forwardResult.StateHash); // the store's own turn 10 agrees too
    }
}
