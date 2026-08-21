using FusionRpg.Contracts;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// D2: rpg_expeditions + soft-lock membership rows (Cold-plane, no FSM change) + the material
/// inventory. The lock is consulted in BOTH directions: expedition dispatch refuses deployed
/// specimens; PvZ deploy refuses expedition members.
/// </summary>
public class ExpeditionStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public ExpeditionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-exped-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    static readonly FusionRpg.Core.Demons.DemonSpeciesDef CatalogSpecies =
        FusionRpg.Core.Demons.DemonSpeciesCatalog.All.First(s => s.Side == "zombie");

    string Mint()
    {
        var (specimen, _) = _store.MintDemon(1, new DemonMintSpec
        {
            SpeciesId = CatalogSpecies.SpeciesId,
            Side = "zombie",
            GameTypeId = CatalogSpecies.GameTypeId,
            Rarity = "common",
            Variant = "normal",
            ElementPrimary = "fire",
            TraitIds = new List<string> { "swift" },
            Origin = "summon"
        });
        return specimen.Actor.InstanceId;
    }

    [Fact]
    public void Dispatch_creates_row_locks_members_and_computes_due()
    {
        var a = Mint();
        var b = Mint();
        var now = DateTimeOffset.Parse("2026-08-21T10:00:00Z");
        var (ok, reason, row) = _store.DispatchExpedition(
            1, "exp-1", "scout-30m", new[] { a, b }, seed: 42, utcNow: now);

        Assert.True(ok, reason);
        Assert.Equal("Dispatched", row!.State);
        Assert.Equal("scout-30m", row.TierId);
        Assert.Equal(now.AddMinutes(30).UtcDateTime.ToString("o"), row.DueUtc);
        Assert.True(_store.HasActiveExpeditionMembership(a));
        Assert.True(_store.HasActiveExpeditionMembership(b));
        Assert.Single(_store.ListExpeditions(1));
    }

    [Fact]
    public void Dispatch_validates_tier_slots_and_ownership()
    {
        var a = Mint();
        Assert.False(_store.DispatchExpedition(1, "x1", "no-such-tier", new[] { a }, 1).Ok);
        // scout-30m has 2 slots — 3 specimens refuse.
        var squad = new[] { Mint(), Mint(), Mint() };
        Assert.Equal("squad.toolarge", _store.DispatchExpedition(1, "x2", "scout-30m", squad, 1).Reason);
        Assert.Equal("squad.empty", _store.DispatchExpedition(1, "x3", "scout-30m", Array.Empty<string>(), 1).Reason);
        Assert.Equal("squad.unknown-specimen",
            _store.DispatchExpedition(1, "x4", "scout-30m", new[] { "ghost-id" }, 1).Reason);
    }

    [Fact]
    public void Soft_lock_refuses_cross_mode_both_ways()
    {
        var onExpedition = Mint();
        var deployed = Mint();

        // Direction 1: a specimen mid-PvZ-deploy cannot be dispatched.
        var (dOk, _, _, _) = _store.TryBeginUniqueDeploy(deployed, "pvz-corr-1");
        Assert.True(dOk);
        Assert.Equal("specimen.deployed",
            _store.DispatchExpedition(1, "e1", "scout-30m", new[] { deployed }, 1).Reason);

        // Direction 2: an expedition member cannot be PvZ-deployed.
        Assert.True(_store.DispatchExpedition(1, "e2", "scout-30m", new[] { onExpedition }, 1).Ok);
        var (deployOk, deployReason, _, _) = _store.TryBeginUniqueDeploy(onExpedition, "pvz-corr-2");
        Assert.False(deployOk);
        Assert.Equal("expedition.locked", deployReason);
    }

    [Fact]
    public void Double_dispatch_of_the_same_specimen_refuses()
    {
        var a = Mint();
        Assert.True(_store.DispatchExpedition(1, "e1", "scout-30m", new[] { a }, 1).Ok);
        Assert.Equal("specimen.on-expedition",
            _store.DispatchExpedition(1, "e2", "scout-30m", new[] { a }, 2).Reason);
    }

    [Fact]
    public void Closing_releases_the_lock()
    {
        var a = Mint();
        var (_, _, row) = _store.DispatchExpedition(1, "e1", "scout-30m", new[] { a }, 1);
        Assert.True(_store.TryCloseExpedition(row!.Id, "Collected"));
        Assert.False(_store.HasActiveExpeditionMembership(a));
        var (deployOk, _, _, _) = _store.TryBeginUniqueDeploy(a, "pvz-after");
        Assert.True(deployOk);
        Assert.Equal("Collected", _store.ListExpeditions(1).Single().State);
    }

    [Fact]
    public void Correlation_replays_return_the_stored_expedition()
    {
        var a = Mint();
        var first = _store.DispatchExpedition(1, "exp-dup", "scout-30m", new[] { a }, 7);
        var replay = _store.DispatchExpedition(1, "exp-dup", "scout-30m", new[] { a }, 999);
        Assert.True(replay.Ok);
        Assert.Equal("replay", replay.Reason);
        Assert.Equal(first.Expedition!.Id, replay.Expedition!.Id);
        Assert.Equal(7UL, replay.Expedition.Seed);
        Assert.Single(_store.ListExpeditions(1));
    }

    [Fact]
    public void Force_due_rewinds_the_timer()
    {
        var a = Mint();
        var (_, _, row) = _store.DispatchExpedition(1, "e1", "hunt-8h", new[] { a }, 1);
        var past = DateTimeOffset.UtcNow.AddMinutes(-1);
        Assert.True(_store.ForceExpeditionDue(row!.Id, past));
        var reread = _store.ListExpeditions(1).Single();
        Assert.Equal(past.UtcDateTime.ToString("o"), reread.DueUtc);
    }

    [Fact]
    public void Materials_accumulate_and_validate_ids()
    {
        _store.AddDemonMaterials(1, new[] { ("essence.fire", 3L), ("shard.rare", 1L) });
        _store.AddDemonMaterials(1, new[] { ("essence.fire", 2L) });
        var shelf = _store.ListDemonMaterials(1);
        Assert.Equal(5, shelf.Single(m => m.MaterialId == "essence.fire").Qty);
        Assert.Equal(1, shelf.Single(m => m.MaterialId == "shard.rare").Qty);

        Assert.ThrowsAny<Exception>(() =>
            _store.AddDemonMaterials(1, new[] { ("essence.plasma", 1L) }));
    }
}
