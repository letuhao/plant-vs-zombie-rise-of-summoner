using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.ActorHub;

public class DerivedStatRegistryTests
{
    [Fact]
    public void Unknown_channel_rejects()
    {
        var reg = DerivedStatRegistry.CreateDefault();
        Assert.Throws<UnknownDerivedChannelException>(() => reg.ValidateChannel("not.a.real.channel"));
    }

    [Fact]
    public void Sparse_status_power_resolves()
    {
        var reg = DerivedStatRegistry.CreateDefault();
        Assert.True(reg.TryResolveChannel("status.power.wither", out var def));
        Assert.Equal(DerivedComposeKind.SumIncreased, def.Compose);
    }

    [Fact]
    public void Category_resist_cap_is_095()
    {
        var reg = DerivedStatRegistry.CreateDefault();
        reg.TryGet(DerivedStatChannels.StatusResistDot, out var def);
        Assert.Equal(0.95, def.Cap);
    }

    [Fact]
    public void Composer_neutral_stub_defaults()
    {
        var composer = new DerivedComposer();
        var snap = composer.Compose();
        Assert.Equal(1.0, snap.Get(DerivedStatChannels.ProgressionPower));
        Assert.Equal(1.0, snap.Get(DerivedStatChannels.ProgressionRealm));
        Assert.Equal(0, snap.Get(DerivedStatChannels.StatusPowerDot));
        Assert.Equal(0, snap.Get(DerivedStatChannels.StatusResistOmni));
    }
}

public class DerivedComposerTests
{
    [Fact]
    public void Category_resist_capped_before_snapshot()
    {
        var composer = new DerivedComposer();
        var snap = composer.Compose(new[]
        {
            new DerivedModifier(DerivedStatChannels.StatusResistDot, DerivedModifierOp.Increased, 2.0)
        });
        Assert.Equal(0.95, snap.Get(DerivedStatChannels.StatusResistDot));
    }
}
