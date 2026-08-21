using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Patron;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>PT1: patron aura magnitudes + the patron-modified kill-earn shape
/// (spec-patron-demon.md, owner locks; tuning ask-first).</summary>
public class PatronPolicyTests
{
    [Theory]
    [InlineData(DemonRarity.Common, 20)]
    [InlineData(DemonRarity.Rare, 30)]
    [InlineData(DemonRarity.Epic, 45)]
    [InlineData(DemonRarity.Legendary, 60)]
    public void Rarity_bases_are_locked(DemonRarity rarity, int baseMilli) =>
        Assert.Equal(baseMilli, PatronPolicy.RarityBaseMilli(rarity));

    [Fact]
    public void Aura_formula_and_clamp()
    {
        Assert.Equal(20, PatronPolicy.AuraMilli(DemonRarity.Common, star: 0, level: 0));
        Assert.Equal(75, PatronPolicy.AuraMilli(DemonRarity.Epic, star: 2, level: 10)); // 45+20+10
        Assert.Equal(150, PatronPolicy.AuraMilli(DemonRarity.Legendary, star: 5, level: 90)); // clamped
        Assert.Equal(PatronPolicy.AuraClampMilli, PatronPolicy.AuraMilli(DemonRarity.Legendary, 5, 999));
    }

    [Fact]
    public void Aura_shape_primary_full_secondary_half_defense_half()
    {
        var aura = PatronPolicy.Aura(DemonRarity.Epic, star: 2, level: 10, "fire", "ice");
        Assert.Equal("fire", aura.ElementPrimary);
        Assert.Equal(75, aura.PowerMilli);
        Assert.Equal(37, aura.DefenseMilli); // half, truncating
        Assert.Equal("ice", aura.ElementSecondary);
        Assert.Equal(37, aura.SecondaryPowerMilli);
        Assert.Equal(18, aura.SecondaryDefenseMilli);

        var mono = PatronPolicy.Aura(DemonRarity.Common, 0, 0, "dark", null);
        Assert.Null(mono.ElementSecondary);
        Assert.Equal(0, mono.SecondaryPowerMilli);
    }

    [Fact]
    public void Switch_cost_is_locked() => Assert.Equal(100, PatronPolicy.SwitchCostSouls);

    [Fact]
    public void Patron_kill_earn_pays_a_bonus_every_tenth_earning_kill()
    {
        // Kill 10 pays 2 (base + bonus); the rest pay 1.
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(0));
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(8));
        Assert.Equal(2, PatronPolicy.KillEarnWithPatron(9));   // the 10th earning kill
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(10));
        Assert.Equal(2, PatronPolicy.KillEarnWithPatron(19));  // the 20th
    }

    [Fact]
    public void Patron_kill_earn_respects_the_soul_cap_boundary()
    {
        // Total souls across a whole match can never pass 50 — walk the full sequence.
        var souls = 0;
        var counted = 0;
        for (var kill = 0; kill < 200; kill++)
        {
            var delta = PatronPolicy.KillEarnWithPatron(counted);
            if (delta > 0) counted++;
            souls += delta;
        }

        Assert.Equal(50, souls);
        // And the boundary is tight: at 45 earning kills the tally is 49, one kill from done.
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(45)); // 46th kill → exactly 50
        Assert.Equal(0, PatronPolicy.KillEarnWithPatron(46)); // capped
    }

    [Fact]
    public void Cap_constant_mirrors_the_audited_earn_policy()
    {
        // The unpatroned path must be byte-identical to the audited KillEarn: 1/kill to 50.
        var baseline = 0;
        for (var counted = 0; counted < 60; counted++)
            baseline += SoulEarnPolicy.KillEarn(counted);
        Assert.Equal(PatronPolicy.KillSoulCap, baseline);
    }
}
