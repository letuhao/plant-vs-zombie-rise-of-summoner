using FusionRpg.Core.World;
using FusionRpg.Core.World.Turn;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// W5 (spec-turn-engine.md §Persistence): the per-turn command log, keyed
/// (world, turn, commander, commandId). Submission is idempotent, commanders never overwrite each
/// other, and turns never bleed.
/// </summary>
public class WorldCommandStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public WorldCommandStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-worldcmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _store.CreateWorld(1, WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, 1, "w"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    static WorldCommand Cmd(string commander, string id, string? entityId = null) => new()
    {
        CommanderId = commander,
        CommandId = id,
        Kind = WorldCommandKinds.StandFast,
        EntityId = entityId
    };

    [Fact]
    public void A_submitted_command_comes_back_on_the_turn_it_was_filed_for()
    {
        var (ok, reason, replayed) = _store.SubmitWorldCommand("w", Cmd("dave", "c1"));
        Assert.True(ok, reason);
        Assert.False(replayed);

        var listed = _store.ListWorldCommands("w", 0);
        var only = Assert.Single(listed);
        Assert.Equal("dave", only.CommanderId);
        Assert.Equal("c1", only.CommandId);
        Assert.Equal(WorldCommandKinds.StandFast, only.Kind);
    }

    [Fact]
    public void Resubmitting_the_same_command_id_is_a_replay_that_changes_nothing()
    {
        _store.SubmitWorldCommand("w", Cmd("dave", "c1", entityId: "e-dave-legion-1"));

        // Same id, different payload — the stored original must win, or a client could rewrite
        // an order it already committed.
        var (ok, _, replayed) = _store.SubmitWorldCommand("w", Cmd("dave", "c1"));
        Assert.True(ok);
        Assert.True(replayed);

        var only = Assert.Single(_store.ListWorldCommands("w", 0));
        Assert.Equal("e-dave-legion-1", only.EntityId);
    }

    [Fact]
    public void Two_commanders_may_use_the_same_command_id()
    {
        _store.SubmitWorldCommand("w", Cmd("dave", "shared-id"));
        var (ok, reason, replayed) = _store.SubmitWorldCommand("w", Cmd("zomboss", "shared-id"));

        Assert.True(ok, reason);
        Assert.False(replayed);
        Assert.Equal(2, _store.ListWorldCommands("w", 0).Count);
    }

    [Fact]
    public void Commands_are_listed_in_stable_commander_then_sequence_order()
    {
        _store.SubmitWorldCommand("w", Cmd("zomboss", "z1"));
        _store.SubmitWorldCommand("w", Cmd("dave", "d2"));
        _store.SubmitWorldCommand("w", Cmd("dave", "d1"));

        var listed = _store.ListWorldCommands("w", 0);
        Assert.Equal(new[] { "dave", "dave", "zomboss" }, listed.Select(c => c.CommanderId));
        Assert.Equal(new[] { "d2", "d1" }, listed.Where(c => c.CommanderId == "dave").Select(c => c.CommandId));
    }

    [Fact]
    public void The_whole_payload_survives_the_log()
    {
        // Payload is typed in Core and serialized here; a field silently lost in the round trip
        // would surface much later as a command the engine cannot act on.
        var rich = Cmd("dave", "p1", entityId: "e-dave-legion-1") with
        {
            SectorId = "ember-hollow",
            SlotIndex = 2,
            LanePath = new[] { "l-home-ember", "l-ember-ash" }
        };
        Assert.True(_store.SubmitWorldCommand("w", rich).Ok);

        var stored = Assert.Single(_store.ListWorldCommands("w", 0));
        Assert.Equal("e-dave-legion-1", stored.EntityId);
        Assert.Equal("ember-hollow", stored.SectorId);
        Assert.Equal(2, stored.SlotIndex);
        Assert.Equal(new[] { "l-home-ember", "l-ember-ash" }, stored.LanePath);
    }

    [Fact]
    public void An_unknown_world_is_refused()
    {
        var (ok, reason, _) = _store.SubmitWorldCommand("no-such-world", Cmd("dave", "c1"));
        Assert.False(ok);
        Assert.Contains("world", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_inadmissible_command_never_reaches_the_log()
    {
        var (ok, reason, _) = _store.SubmitWorldCommand("w", Cmd("dave", "c1", entityId: "e-wild-pack-1"));
        Assert.False(ok);
        Assert.Equal("entity.not-yours", reason);
        Assert.Empty(_store.ListWorldCommands("w", 0));
    }
}
