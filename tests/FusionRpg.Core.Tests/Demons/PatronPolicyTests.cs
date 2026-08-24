using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Patron;
using FusionRpg.Core.Power;
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

    static PowerTuning TuningAt(long bMilli) => PowerTuning.Build(
        1, 1, PowerTuning.FixedCMilli, bMilli, PowerTuning.FixedPinIndex, PowerTuning.FixedPinValue,
        1000, 25000, 250, 1000, 5000, 5000, 25000);
    const int Pin = 20;

    [Fact]
    public void Patron_kill_earn_pays_a_bonus_every_tenth_earning_kill_at_the_pin()
    {
        // At the pin (contentScale=1.000) this is byte-identical to the pre-T3.6 shape: kill 10 pays
        // 2 (base + bonus); the rest pay 1.
        var tuning = TuningAt(400);
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(0, Pin, tuning));
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(8, Pin, tuning));
        Assert.Equal(2, PatronPolicy.KillEarnWithPatron(9, Pin, tuning));   // the 10th earning kill
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(10, Pin, tuning));
        Assert.Equal(2, PatronPolicy.KillEarnWithPatron(19, Pin, tuning)); // the 20th
    }

    [Fact]
    public void Patron_kill_earn_is_uncapped_past_the_old_fifty_soul_boundary()
    {
        // T3.6: KillSoulCap is deleted. Walking well past the old 50-soul boundary (the 46th earning
        // kill used to be exactly where the cap engaged) must keep paying the same +1/+1-per-10th
        // shape, never dropping to zero the way the pre-T3.6 Math.Min did.
        var tuning = TuningAt(400);
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(45, Pin, tuning)); // used to be the 46th->cap kill
        Assert.Equal(1, PatronPolicy.KillEarnWithPatron(46, Pin, tuning)); // used to be capped at 0 -- not anymore
        Assert.Equal(2, PatronPolicy.KillEarnWithPatron(99, Pin, tuning)); // the 100th earning kill, still paying the bonus

        long souls = 0;
        var counted = 0;
        for (var kill = 0; kill < 200; kill++)
        {
            var delta = PatronPolicy.KillEarnWithPatron(counted, Pin, tuning);
            if (delta > 0) counted++;
            souls += delta;
        }
        Assert.True(souls > 50, $"200 kills must earn more than the old 50-soul cap, got {souls}");
    }

    [Fact]
    public void Patron_kill_earn_scales_with_content_depth_same_as_the_unpatroned_path()
    {
        // Deliberate extension beyond SSOT §11.7a's own named formula list (power-todo.md T3.6):
        // the patron bonus scales too, so owning a patron never becomes a net PENALTY at depth.
        var tuning = TuningAt(400);
        var atPin = PatronPolicy.KillEarnWithPatron(9, Pin, tuning);   // the 10th earning kill, +2 at the pin
        var atDepth = PatronPolicy.KillEarnWithPatron(9, 100, tuning); // same kill, much deeper content
        Assert.True(atDepth > atPin, $"patron bonus must scale with depth: pin={atPin}, depth={atDepth}");
    }
}
