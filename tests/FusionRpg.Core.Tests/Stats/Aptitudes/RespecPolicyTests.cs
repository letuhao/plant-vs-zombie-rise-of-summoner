using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.Stats.Aptitudes;

/// <summary>species-build-todo.md T4.1 — <see cref="RespecPolicy"/> (spec-species-respec.md, read in
/// full this session). Covers the policy's own slice of the spec's testing strategy: free at count
/// zero, strict escalation, the exact linear formula, and the never-refused/never-a-cooldown
/// invariants this policy inherits from class-system-todo.md P6.3's original design.</summary>
public class RespecPolicyTests
{
    static SpeciesBuildTuning Tuning(long basePrice = 50, long escalationPermille = 500) => new(
        SchemaVersion: 1, Version: 1,
        ParityFloorPermille: 50, ParityCeilingPermille: 200,
        LeanMinPermille: 350, LeanMaxPermille: 600,
        CrowdingFactor: 633, SecondarySharePermille: 300,
        MaxAptitudesPerSpecies: 5, MinAptitudesPerSpecies: 2,
        RespecBasePrice: basePrice, RespecEscalationPermille: escalationPermille, RespecDecayDays: 3);

    [Fact]
    public void PriceOf_atCountZero_isExactlyBasePrice()
    {
        // The first override is free (spec: "the player expressing a build for the first time"); T4.2
        // owns never CALLING PriceOf for that case, but the policy's own count=0 reading must still be
        // the base price, not zero — free-first-override is a caller-side decision, not this formula's.
        var price = RespecPolicy.PriceOf(Tuning(basePrice: 50), count: 0);
        Assert.Equal(RespecResource.Soul, price.Resource);
        Assert.Equal(50, price.Amount);
    }

    [Fact]
    public void PriceOf_matchesTheLinearFormula_atNamedCounts()
    {
        var tuning = Tuning(basePrice: 50, escalationPermille: 500);
        // price(count) = base + base * count * escalationPermille / 1000
        Assert.Equal(50, RespecPolicy.PriceOf(tuning, 0).Amount);
        Assert.Equal(75, RespecPolicy.PriceOf(tuning, 1).Amount);   // 50 + 50*1*500/1000
        Assert.Equal(100, RespecPolicy.PriceOf(tuning, 2).Amount); // 50 + 50*2*500/1000
        Assert.Equal(150, RespecPolicy.PriceOf(tuning, 4).Amount); // 50 + 50*4*500/1000
    }

    [Fact]
    public void PriceOf_isStrictlyIncreasing_asCountRises()
    {
        // Escalation, not a flat repeated price -- each successive change must cost strictly more.
        var tuning = Tuning();
        var second = RespecPolicy.PriceOf(tuning, 1).Amount;
        var third = RespecPolicy.PriceOf(tuning, 2).Amount;
        var fourth = RespecPolicy.PriceOf(tuning, 3).Amount;

        Assert.True(second > RespecPolicy.PriceOf(tuning, 0).Amount);
        Assert.True(third > second);
        Assert.True(fourth > third);
    }

    [Fact]
    public void PriceOf_isNeverRefused_returnsUnconditionally()
    {
        // "Always available" -- PriceOf has no "cannot respec right now" return path: calling it for
        // any non-negative count always succeeds and always returns a price.
        var tuning = Tuning();
        Assert.True(RespecPolicy.PriceOf(tuning, 0).Amount > 0);
        Assert.True(RespecPolicy.PriceOf(tuning, 1_000).Amount > 0);
    }

    [Fact]
    public void PriceOf_isPure_sameInputsAlwaysGiveTheSamePrice()
    {
        // No hidden cooldown or mutable state -- repeated calls at the same count never drift.
        var tuning = Tuning();
        Assert.Equal(RespecPolicy.PriceOf(tuning, 2), RespecPolicy.PriceOf(tuning, 2));
    }

    [Fact]
    public void PriceOf_negativeCount_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RespecPolicy.PriceOf(Tuning(), -1));
    }

    [Fact]
    public void PriceOf_nullTuning_throws()
    {
        Assert.Throws<ArgumentNullException>(() => RespecPolicy.PriceOf(null!, 0));
    }
}
