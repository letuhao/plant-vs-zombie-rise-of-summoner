using FusionRpg.Core.World;
using FusionRpg.Core.World.Turn;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// W7 (spec-turn-engine.md §Persistence): one turn is one transaction. The barrier waits for every
/// commander, the log keeps hash + versions always and the report only for a hot tail, and an older
/// report is re-derived from the command log rather than stored forever.
/// </summary>
public class WorldTurnCommitTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public WorldTurnCommitTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-turn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _store.CreateWorld(1, WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, 1, "w"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    static readonly string[] AllCommanders = { "dave", "wild", "zomboss" };

    /// <summary>Commits every commander and returns the last result — the turn fires on the last one.</summary>
    WorldTurnCommitResult CommitAll(params string[] commanders)
    {
        // `params` with no arguments is an empty array, not null — spell the default out.
        var who = commanders.Length == 0 ? AllCommanders : commanders;
        WorldTurnCommitResult last = default!;
        foreach (var c in who)
            last = _store.CommitWorldTurn("w", c);
        return last;
    }

    [Fact]
    public void The_turn_fires_only_when_the_last_commander_commits()
    {
        var first = _store.CommitWorldTurn("w", "dave");
        Assert.True(first.Ok);
        Assert.False(first.Advanced);
        Assert.Equal(0, _store.GetActiveWorld(1)!.CurrentTurn);

        var second = _store.CommitWorldTurn("w", "wild");
        Assert.False(second.Advanced);

        var last = _store.CommitWorldTurn("w", "zomboss");
        Assert.True(last.Advanced);
        Assert.Equal(1, _store.GetActiveWorld(1)!.CurrentTurn);
    }

    [Fact]
    public void Committing_twice_is_idempotent_and_does_not_advance_twice()
    {
        _store.CommitWorldTurn("w", "dave");
        var repeat = _store.CommitWorldTurn("w", "dave");
        Assert.True(repeat.Ok);
        Assert.False(repeat.Advanced);

        CommitAll("wild", "zomboss");
        Assert.Equal(1, _store.GetActiveWorld(1)!.CurrentTurn);

        // A late duplicate for the turn that already resolved must not roll the world forward.
        var stale = _store.CommitWorldTurn("w", "dave");
        Assert.True(stale.Ok);
        Assert.False(stale.Advanced);
        Assert.Equal(1, _store.GetActiveWorld(1)!.CurrentTurn);
    }

    [Fact]
    public void Each_turn_writes_exactly_one_log_row_carrying_its_hash_and_versions()
    {
        CommitAll();
        CommitAll();

        var t0 = _store.GetWorldTurnLog("w", 0)!;
        var t1 = _store.GetWorldTurnLog("w", 1)!;

        Assert.False(string.IsNullOrWhiteSpace(t0.StateHash));
        Assert.NotEqual(t0.StateHash, t1.StateHash); // the turn number alone moves the world
        Assert.Equal(TurnEngine.EngineVersion, t0.EngineVersion);
        Assert.Equal(TurnEngine.RulesetVersion, t0.RulesetVersion);
        Assert.Null(_store.GetWorldTurnLog("w", 2));
    }

    [Fact]
    public void The_state_hash_in_the_log_matches_the_world_it_describes()
    {
        CommitAll();

        var log = _store.GetWorldTurnLog("w", 0)!;
        var world = _store.LoadWorldState("w")!;
        Assert.Equal(StateHasher.Hash(world), log.StateHash);
    }

    [Fact]
    public void Orders_belong_to_the_turn_they_were_filed_in()
    {
        _store.SubmitWorldCommand("w", new WorldCommand
        {
            CommanderId = "dave", CommandId = "c1", Kind = WorldCommandKinds.StandFast
        });
        CommitAll();

        Assert.Single(_store.ListWorldCommands("w", 0));
        Assert.Empty(_store.ListWorldCommands("w", 1)); // the new turn starts clean
    }

    [Fact]
    public void A_stored_report_comes_back_as_it_was_written()
    {
        _store.SubmitWorldCommand("w", new WorldCommand
        {
            CommanderId = "dave", CommandId = "c1", Kind = WorldCommandKinds.StandFast
        });
        CommitAll();

        var report = _store.GetWorldTurnReport("w", 0);
        Assert.NotNull(report);
        Assert.Contains(report!.Accepted, e => e.Subject == "c1");
    }

    [Fact]
    public void A_trimmed_report_is_re_derived_identically_from_the_command_log()
    {
        _store.SubmitWorldCommand("w", new WorldCommand
        {
            CommanderId = "dave", CommandId = "c1", Kind = WorldCommandKinds.StandFast
        });
        CommitAll();

        var stored = _store.GetWorldTurnReport("w", 0)!;
        var storedEntries = stored.Entries.ToList();

        _store.TrimWorldTurnReports("w", keepLast: 0); // the report body is gone; hash and versions stay
        Assert.NotNull(_store.GetWorldTurnLog("w", 0));

        var rederived = _store.GetWorldTurnReport("w", 0);
        Assert.NotNull(rederived);
        Assert.Equal(storedEntries, rederived!.Entries.ToList());
    }

    [Fact]
    public void An_unknown_world_or_commander_is_refused()
    {
        Assert.False(_store.CommitWorldTurn("nope", "dave").Ok);
        Assert.False(_store.CommitWorldTurn("w", "stranger").Ok);
        Assert.Equal(0, _store.GetActiveWorld(1)!.CurrentTurn);
    }
}
