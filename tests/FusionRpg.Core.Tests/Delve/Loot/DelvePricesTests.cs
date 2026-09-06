using FusionRpg.Core.Delve.Loot;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Loot;

/// <summary>D3.13 (spec-dungeon-loot.md §6, "Sinks and prices — one function, three callers"). Reuses
/// `SoulEarnPolicyTests.cs`'s own exact-value `TuningAt`/`Pin=20` fixture, so every price is a byte-exact
/// golden against the real `SoulSinkPolicy.Price`, not an approximate sanity check.</summary>
public class DelvePricesTests
{
    static PowerTuning TuningAt(long bMilli) => PowerTuning.Build(
        1, 1, PowerTuning.FixedCMilli, bMilli, PowerTuning.FixedPinIndex, PowerTuning.FixedPinValue,
        1000, 25000, 250, 1000, 5000, 5000, 25000);

    const int Pin = 20;
    static readonly PowerTuning Tuning = TuningAt(400);

    // ---- Merchant ----

    [Fact]
    public void Merchant_null_tuning_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DelvePrices.Merchant(1000, Pin, 0, 1000, null!, out _));
    }

    [Fact]
    public void Merchant_with_no_derived_price_refuses_price_undesigned()
    {
        var r = DelvePrices.Merchant(basePriceSouls: null, Pin, 0, 1000, Tuning, out var price);
        Assert.False(r.IsOk);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, r.Reason);
        Assert.Contains(DelvePriceRules.PriceUndesigned, r.Detail);
        Assert.Equal(0, price);
    }

    [Fact]
    public void Merchant_with_zero_markup_matches_SoulSinkPolicy_Price_exactly()
    {
        // markupMilli 0, rung mult 1000 (identity): (1000+0) * 1000 / 1_000_000 = 1 -- no scaling at all.
        var r = DelvePrices.Merchant(basePriceSouls: 5000, Pin, merchantMarkupMilli: 0, rungMerchantMarkupMultMilli: 1000, Tuning, out var price);
        Assert.True(r.IsOk);
        Assert.Equal(SoulSinkPolicy.Price(5000, Pin, Tuning), price);
    }

    [Fact]
    public void Merchant_markup_scales_the_base_price_before_the_theta_read()
    {
        // +200‰ markup, identity rung mult: base 5000 -> marked-up base 6000 (5000*1200/1000).
        var r = DelvePrices.Merchant(basePriceSouls: 5000, Pin, merchantMarkupMilli: 200, rungMerchantMarkupMultMilli: 1000, Tuning, out var price);
        Assert.True(r.IsOk);
        Assert.Equal(SoulSinkPolicy.Price(6000, Pin, Tuning), price);
    }

    [Fact]
    public void Merchant_rung_multiplier_also_scales_the_base_price()
    {
        // no markup, rung mult 1500 (1.5x): base 5000 -> marked-up base 7500.
        var r = DelvePrices.Merchant(basePriceSouls: 5000, Pin, merchantMarkupMilli: 0, rungMerchantMarkupMultMilli: 1500, Tuning, out var price);
        Assert.True(r.IsOk);
        Assert.Equal(SoulSinkPolicy.Price(7500, Pin, Tuning), price);
    }

    [Fact]
    public void Merchant_price_genuinely_tracks_the_callers_own_base_price_no_hidden_literal()
    {
        // Proves the "no price literal exists" acceptance line as an outcome: the SAME markup/rung/Θ
        // with two different caller-supplied base prices must produce two different, proportional
        // results -- a hidden internal constant would make this fail (either flat or wrong ratio).
        DelvePrices.Merchant(1000, Pin, 100, 1000, Tuning, out var priceA);
        DelvePrices.Merchant(2000, Pin, 100, 1000, Tuning, out var priceB);
        Assert.NotEqual(priceA, priceB);
        Assert.Equal(priceA * 2, priceB); // linear in the base price at a fixed Θ
    }

    // ---- PullPrice ----

    [Fact]
    public void PullPrice_null_tuning_throws()
    {
        Assert.Throws<ArgumentNullException>(() => DelvePrices.PullPrice(1000, Pin, null!));
    }

    [Fact]
    public void PullPrice_matches_SoulSinkPolicy_Price_exactly()
    {
        Assert.Equal(SoulSinkPolicy.Price(1500, Pin, Tuning), DelvePrices.PullPrice(1500, Pin, Tuning));
    }

    // ---- OfferFloor ----

    [Fact]
    public void OfferFloor_null_tuning_throws()
    {
        Assert.Throws<ArgumentNullException>(() => DelvePrices.OfferFloor(1000, Pin, 1000, null!));
    }

    [Fact]
    public void OfferFloor_at_exactly_1000_milli_equals_the_pull_price()
    {
        // The tunable's own documented floor ("offerSoulsMilliOfPullPrice >= 1000") -- at exactly the
        // floor, the offer costs exactly the pull price, never less.
        var pull = DelvePrices.PullPrice(2000, Pin, Tuning);
        Assert.Equal(pull, DelvePrices.OfferFloor(2000, Pin, offerSoulsMilliOfPullPrice: 1000, Tuning));
    }

    [Fact]
    public void OfferFloor_above_1000_milli_costs_more_than_the_pull_price()
    {
        var pull = DelvePrices.PullPrice(2000, Pin, Tuning);
        var offer = DelvePrices.OfferFloor(2000, Pin, offerSoulsMilliOfPullPrice: 1500, Tuning);
        Assert.Equal(pull * 3 / 2, offer);
        Assert.True(offer > pull);
    }

    // ---- RecoveryRitual ----

    [Fact]
    public void RecoveryRitual_null_tuning_throws()
    {
        Assert.Throws<ArgumentNullException>(() => DelvePrices.RecoveryRitual(1000, Pin, null!));
    }

    [Fact]
    public void RecoveryRitual_matches_SoulSinkPolicy_Price_exactly()
    {
        Assert.Equal(SoulSinkPolicy.Price(800, Pin, Tuning), DelvePrices.RecoveryRitual(800, Pin, Tuning));
    }

    [Fact]
    public void RecoveryRitual_reads_the_wounding_delves_own_theta_run_not_a_fixed_one()
    {
        // Two different theta_run readings for the identical base price must give two different
        // prices -- proving the Θ argument is genuinely read, not a constant folded away.
        var atPin = DelvePrices.RecoveryRitual(800, Pin, Tuning);
        var atHigherTheta = DelvePrices.RecoveryRitual(800, Pin + 100, Tuning);
        Assert.NotEqual(atPin, atHigherTheta);
    }
}
