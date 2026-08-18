using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Status;

public class ResistanceEvaluatorTests
{
    static readonly ResistanceEvaluator Eval = new();

    static StatusApplyRequest Req(string statusId = "wither") => new(
        statusId,
        HostPtr: "Z1",
        AttackerPtr: "P1",
        BaseMagnitude: 20,
        BaseDuration: 5000);

    [Fact]
    public void Neutral_stub_tier_power_contributes_to_delta()
    {
        var attacker = ActorDerivedSnapshot.StubNeutral();
        var defender = ActorDerivedSnapshot.StubNeutral();
        var delta = ResistanceEvaluator.ComputeDelta("wither", StatusL2bCategory.Dot, attacker, defender);
        Assert.Equal(1.0, delta, 3);
        Assert.Equal(1.0, ResistanceEvaluator.ComputeNetFactor(delta));
    }

    [Fact]
    public void Neutral_stub_p_apply_near_half()
    {
        var result = Eval.Evaluate(
            Req(),
            ActorDerivedSnapshot.StubNeutral(),
            ActorDerivedSnapshot.StubNeutral(),
            new FixedStatusRng(0.0));
        Assert.True(result.Applied);
        Assert.InRange(result.PApply, 0.49, 0.51);
        Assert.Equal(1.0, result.NetFactor);
    }

    [Fact]
    public void Delta_negative_ten_potency_floor_skips_roll()
    {
        var attacker = ActorDerivedSnapshot.StubNeutral();
        var defender = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusResistOmni, 5.0)
        });
        var result = Eval.Evaluate(Req(), attacker, defender, new FixedStatusRng(0.0));
        Assert.False(result.Applied);
        Assert.Equal(StatusResistReason.PotencyFloor, result.ResistReason);
        Assert.Equal(0, result.PApply);
    }

    [Fact]
    public void Omni_resist_1M_vs_power_100_potency_floor()
    {
        var attacker = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPower(statusId: "rot"), 100)
        });
        var defender = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusResistOmni, 1_000_000)
        });
        var result = Eval.Evaluate(Req("rot"), attacker, defender, new FixedStatusRng(0.0));
        Assert.False(result.Applied);
        Assert.Equal(StatusResistReason.PotencyFloor, result.ResistReason);
    }

    [Theory]
    [InlineData(-1500, 0.01)]
    [InlineData(0, 0.50)]
    [InlineData(50, 0.62)]
    [InlineData(1500, 0.99)]
    public void Golden_apply_chance_table(double delta, double expectedApprox)
    {
        var scale = 100.0;
        var p = ResistanceEvaluator.Sigmoid(delta / scale);
        Assert.InRange(p, expectedApprox - 0.02, expectedApprox + 0.02);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 1.0)]
    [InlineData(50, 50)]
    public void Golden_potency_table(double delta, double expectedNet)
    {
        Assert.Equal(expectedNet, ResistanceEvaluator.ComputeNetFactor(delta));
    }

    [Fact]
    public void Complete_immunity_blocks_before_roll()
    {
        var defender = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.StatusImmune("poison"), 1.0)
        });
        var result = Eval.Evaluate(
            Req("poison") with { ImmunityTags = new[] { "poison" } },
            ActorDerivedSnapshot.StubNeutral(),
            defender,
            new FixedStatusRng(0.0));
        Assert.False(result.Applied);
        Assert.Equal(StatusResistReason.Immunity, result.ResistReason);
    }

    [Fact]
    public void Attacker_less_uses_zero_power()
    {
        var attackerLess = ActorDerivedSnapshot.AttackerLess();
        var defender = ActorDerivedSnapshot.StubNeutral();
        var delta = ResistanceEvaluator.ComputeDelta("blight", StatusL2bCategory.Contagion, attackerLess, defender);
        Assert.Equal(0.0, delta, 3);
    }

    [Fact]
    public void Grant_chance_combines_with_p_apply()
    {
        var result = Eval.Evaluate(
            Req() with { GrantChance = 0.5 },
            ActorDerivedSnapshot.StubNeutral(),
            ActorDerivedSnapshot.StubNeutral(),
            new FixedStatusRng(0.0));
        Assert.True(result.Applied);
        Assert.InRange(result.PFinal, 0.24, 0.26);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 2.0)]
    [InlineData(3, 8.0)]
    public void Progression_power_curve_feeds_delta(int level, double expectedTier)
    {
        var power = ProgressionPowerCurve.PowerFromLevel(level);
        Assert.Equal(expectedTier, power, 3);
        var attacker = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, power),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0)
        });
        var defender = ActorDerivedSnapshot.AttackerLess();
        var delta = ResistanceEvaluator.ComputeDelta("wither", StatusL2bCategory.Dot, attacker, defender);
        Assert.Equal(expectedTier, delta, 3);
    }
}

public class StatusCategoryRegistryTests
{
    [Theory]
    [InlineData("wither", StatusL2bCategory.Dot)]
    [InlineData("butter", StatusL2bCategory.Cc)]
    [InlineData("blight", StatusL2bCategory.Contagion)]
    public void Known_ids_map_to_category(string statusId, string category)
    {
        Assert.Equal(category, StatusCategoryRegistry.GetRequiredCategory(statusId));
    }

    [Fact]
    public void All_twenty_one_ids_registered()
    {
        Assert.Equal(21, StatusCategoryRegistry.AllStatusIds.Count);
    }
}

public class StatusCatalogTests
{
    [Fact]
    public void Bootstrap_registers_21_ids()
    {
        var catalog = StatusCatalogBootstrap.CreateDefault();
        Assert.Equal(21, catalog.All().Count);
    }

    [Fact]
    public void Unknown_statusId_rejects()
    {
        var catalog = StatusCatalogBootstrap.CreateDefault();
        Assert.Throws<UnknownStatusIdException>(() => catalog.GetRequired("not_a_status"));
    }

    [Fact]
    public void Elemental_family_mutex_defs_exist()
    {
        var catalog = StatusCatalogBootstrap.CreateDefault();
        Assert.Equal("elemental", catalog.GetRequired("freeze").Family);
        Assert.Equal(StatusStacking.Replace, catalog.GetRequired("freeze").Stacking);
    }
}
