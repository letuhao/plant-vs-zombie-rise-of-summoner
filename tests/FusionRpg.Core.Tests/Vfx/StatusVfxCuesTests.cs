using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Vfx;
using CoreStatus = FusionRpg.Core.Status;
using Xunit;

namespace FusionRpg.Core.Tests.Vfx;

/// <summary>SPEC W5: status applies emit status.{id}.apply cues; resists and ICD emit nothing.</summary>
public class StatusVfxCuesTests
{
    static CoreStatus.StatusRuntime Runtime() =>
        new(CoreStatus.StatusCatalogBootstrap.CreateDefault(), (_, attackerLess) =>
            attackerLess ? ActorDerivedSnapshot.AttackerLess() : ActorDerivedSnapshot.StubNeutral());

    static (CoreStatus.StatusRuntime Rt, RecordingVfxSink Sink) Wired()
    {
        var rt = Runtime();
        var sink = new RecordingVfxSink();
        rt.OnApplied = inst => sink.Play(StatusVfxCues.Cue(inst));
        return (rt, sink);
    }

    [Fact]
    public void Cue_id_shape_is_locked()
    {
        Assert.Equal("status.wither.apply", StatusVfxCues.CueId("wither"));
        Assert.Equal("status.charm_pulse.apply", StatusVfxCues.CueId("Charm_Pulse"));
    }

    [Fact]
    public void Successful_apply_emits_one_ptr_anchored_cue()
    {
        var (rt, sink) = Wired();
        rt.Apply(
            new CoreStatus.StatusApplyInput("wither", "Z1", "P1", "g1", 20, 5000),
            new CoreStatus.FixedStatusRng(0.0),
            DateTimeOffset.UtcNow);
        var cue = Assert.Single(sink.Items);
        Assert.Equal("status.wither.apply", cue.CueId);
        Assert.Equal("Z1", cue.TargetPtr);
        Assert.Null(cue.Col);
    }

    [Fact]
    public void Resisted_apply_emits_nothing()
    {
        var (rt, sink) = Wired();
        rt.OnResisted = _ => { };
        rt.Apply(
            new CoreStatus.StatusApplyInput("wither", "Z1", "P1", "g1", 20, 5000),
            new CoreStatus.FixedStatusRng(1.0),
            DateTimeOffset.UtcNow);
        Assert.Empty(sink.Items);
        Assert.Single(rt.ResistedEvents);
    }

    [Fact]
    public void Spread_hop_emits_a_cue_for_the_new_host()
    {
        var (rt, sink) = Wired();
        var now = DateTimeOffset.UtcNow;
        var input = new CoreStatus.StatusApplyInput(
            "blight", "Z1", "P1", "g-blight", -12, 5000,
            SpreadChance: 1.0, SpreadStatusId: "blight", SpreadMaxHops: 2);
        rt.Apply(input, new CoreStatus.FixedStatusRng(0.0), now);
        var inst = rt.ForHost("Z1").Single();
        var template = input with { HostPtr = "", HopDepth = 1 };
        CoreStatus.StatusSpread.Execute(
            rt,
            new CoreStatus.StatusSpreadRequest(inst, new[] { "Z2" }, 1, 1.0, template),
            new CoreStatus.FixedStatusRng(0.0),
            now);
        Assert.Equal(2, sink.Items.Count);
        Assert.Contains(sink.Items, c => c.TargetPtr == "Z1");
        Assert.Contains(sink.Items, c => c.TargetPtr == "Z2");
    }

    [Fact]
    public void Every_catalog_status_has_a_seeded_apply_recipe()
    {
        var statusIds = CoreStatus.StatusCatalogBootstrap.CreateDefault().All()
            .Select(d => d.StatusId).ToList();
        Assert.Equal(21, statusIds.Count);
        var catalog = new VfxCatalog();
        catalog.ReplaceAll(VfxSeedCatalog.CreateAll());
        foreach (var id in statusIds)
        {
            Assert.True(catalog.TryGet(StatusVfxCues.CueId(id), out var recipe), id);
            Assert.All(recipe.Primitives, p => Assert.True(p.LifeSeconds > 0f));
        }

        // 3 combat/debug cues + 21 status cues
        Assert.Equal(24, catalog.Ids.Count);
    }
}
