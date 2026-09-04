using FusionRpg.Core.Items;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>SC7, enforced by <see cref="RarityBudgetKeys"/> (spec-rarity-bands.md).</summary>
public class RarityBudgetKeysTests
{
    [Theory]
    [InlineData("promote_from")]
    [InlineData("pity_guarded")]
    [InlineData("drop_weight_default")]
    [InlineData("enhance_cap")]
    [InlineData("power_ceiling")]
    public void The_five_ready_keys_are_registered(string key) =>
        Assert.True(RarityBudgetKeys.IsRegistered(key));

    [Theory]
    [InlineData("socket_min")]
    [InlineData("socket_max")]
    [InlineData("reroll_cost_mult")]
    [InlineData("salvage_yield")]
    public void A_key_awaiting_a_decided_shape_is_not_registered_yet(string key) =>
        Assert.False(RarityBudgetKeys.IsRegistered(key));

    [Fact]
    public void A_rarity_budget_key_with_no_shipped_consumer_is_rejected()
    {
        var ex = Assert.Throws<RarityBudgetKeyRejection>(() => RarityBudgetKeys.Validate("socket_min"));
        Assert.Contains("socket_min", ex.Message);
    }

    [Fact]
    public void An_unknown_key_is_rejected()
    {
        Assert.False(RarityBudgetKeys.IsRegistered("does-not-exist"));
        Assert.Throws<RarityBudgetKeyRejection>(() => RarityBudgetKeys.Validate("does-not-exist"));
    }

    [Fact]
    public void Set_eligible_and_charm_potency_are_not_in_the_key_registry()
    {
        // D15 makes set_eligible vacuous; spec-set-charm-gen.md never reads charm_potency. Both were
        // resolved by DROPPING, not deferring -- a re-add with no consumer is the exact SC7 violation
        // this test pins against regressing.
        Assert.DoesNotContain(RarityBudgetKeys.All, k => k.Key == "set_eligible");
        Assert.DoesNotContain(RarityBudgetKeys.All, k => k.Key == "charm_potency");
        Assert.False(RarityBudgetKeys.IsRegistered("set_eligible"));
        Assert.False(RarityBudgetKeys.IsRegistered("charm_potency"));
    }

    [Fact]
    public void Registered_keys_each_name_a_real_consumer_module()
    {
        foreach (var k in RarityBudgetKeys.All)
            Assert.False(string.IsNullOrWhiteSpace(k.ConsumerModule));
    }
}
