using FusionRpg.Contracts;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using Xunit;

namespace FusionRpg.Core.Tests.ActorHub;

public class ActorHubResolveTests
{
    [Fact]
    public void Resolve_derived_stub_tier_power()
    {
        var stats = StatSystemBootstrap.CreateDefault();
        var hub = new FusionRpg.Core.Stats.Derived.ActorHub(stats);
        hub.Register(new RpgProgressionSubsystem());

        var ctx = stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });
        var result = hub.Resolve(ctx);

        Assert.Equal(100, result.RuntimePrimary.Hp);
        Assert.Equal(100, result.AppliedCombat.Hp);
        Assert.Equal(1.0, result.Derived.TierPower);
        Assert.Equal(1.0, result.Derived.Get(DerivedStatChannels.ProgressionPower));
    }

    [Fact]
    public void ResolveDerived_fresh_each_call()
    {
        var hub = ActorHubBootstrap.CreateDefault();
        var ctx = hub.Stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });
        var a = hub.ResolveDerived(ctx);
        var b = hub.ResolveDerived(ctx);
        Assert.Equal(a.TierPower, b.TierPower);
    }

    [Fact]
    public void Applied_combat_matches_primary_without_bonus_level()
    {
        var stats = StatSystemBootstrap.CreateDefault();
        var hub = ActorHubBootstrap.CreateDefault(stats);
        var ctx = stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 300, MaxHp = 300, Atk = 20 },
            cheatScale: new StatsConfig { ApplyStats = true, Plants = { HpPercent = 2f } });

        var result = hub.Resolve(ctx);
        Assert.Equal(result.RuntimePrimary.Hp, result.AppliedCombat.Hp);
        Assert.Equal(result.RuntimePrimary.Atk, result.AppliedCombat.Atk);
    }

    [Fact]
    public void Applied_combat_includes_progression_bonus_flats()
    {
        var stats = StatSystemBootstrap.CreateDefault();
        var hub = new FusionRpg.Core.Stats.Derived.ActorHub(stats);
        hub.Register(new RpgProgressionSubsystem(new FixedLevelProgressionProvider(5)));

        var ctx = stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });
        var result = hub.Resolve(ctx);

        Assert.Equal(150, result.AppliedCombat.MaxHp);
        Assert.Equal(150, result.AppliedCombat.Hp);
        Assert.Equal(15, result.AppliedCombat.Atk);
        Assert.Equal(100, result.RuntimePrimary.MaxHp);
    }

    sealed class FixedLevelProgressionProvider : IProgressionPowerProvider
    {
        readonly int _level;
        public FixedLevelProgressionProvider(int level) => _level = level;
        public int GetLevel(StatContext ctx) => _level;
        public double GetPower(StatContext ctx) => ProgressionPowerCurve.PowerFromLevel(_level);
        public double GetRealm(StatContext ctx) => 1.0;
    }
}
