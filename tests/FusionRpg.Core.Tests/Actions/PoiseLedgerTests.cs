using FusionRpg.Core.Actions.Cost;
using FusionRpg.Core.Actions.Defence;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Actions;

/// <summary>
/// T25/T26 (action-todo.md, spec-defence-actions.md §3): `poise`'s three-part cost, exercised
/// directly against T15's real `ActorResourcePools`, not a mock. The mutual-guard TERMINATION proof
/// (the module's hardest acceptance line) lives in <see cref="PoiseTerminationTests"/>.
/// </summary>
public class PoiseLedgerTests
{
    static ActorDerivedSnapshot PoiseSnapshot(double max, double regenPerTick)
    {
        var registry = DerivedStatRegistry.CreateDefault();
        var composer = new DerivedComposer(registry);
        return composer.Compose(new[]
        {
            new DerivedModifier(DerivedStatChannels.ResourceMax("poise"), DerivedModifierOp.Flat, max, SourceId: "test"),
            new DerivedModifier(DerivedStatChannels.ResourceRegen("poise"), DerivedModifierOp.Flat, regenPerTick, SourceId: "test"),
        });
    }

    [Fact]
    public void TryCommitSpendsTheFlatAmountOnce()
    {
        var derived = PoiseSnapshot(max: 100, regenPerTick: 0);
        var pools = ActorResourcePools.CreateFull(derived, atTick: 0);

        var ok = PoiseLedger.TryCommit(pools, flatCommitAmount: 30, nowTick: 0, derived);

        Assert.True(ok);
        Assert.Equal(70, pools.Resolve(PoiseLedger.ResourceId, 0, derived));
    }

    [Fact]
    public void RaisingWithInsufficientPoiseIsRefusedByAffordabilityNotSilence()
    {
        var derived = PoiseSnapshot(max: 10, regenPerTick: 0);
        var pools = ActorResourcePools.CreateFull(derived, atTick: 0);

        var ok = PoiseLedger.TryCommit(pools, flatCommitAmount: 50, nowTick: 0, derived);

        Assert.False(ok);
        Assert.Equal(10, pools.Resolve(PoiseLedger.ResourceId, 0, derived)); // untouched, not partially spent
    }

    [Fact]
    public void AbsorbDrainScalesWithWhatWasAbsorbedNotAFlatAmount()
    {
        Assert.Equal(50, PoiseLedger.AbsorbDrainAmount(absorbedAmount: 500, drainRatioMilli: 100)); // 10%
        Assert.Equal(500, PoiseLedger.AbsorbDrainAmount(absorbedAmount: 500, drainRatioMilli: 1000)); // 100%
        Assert.Equal(0, PoiseLedger.AbsorbDrainAmount(absorbedAmount: 0, drainRatioMilli: 1000));
    }

    [Fact]
    public void TryPayAbsorbDrainSpendsTheScaledAmount()
    {
        var derived = PoiseSnapshot(max: 100, regenPerTick: 0);
        var pools = ActorResourcePools.CreateFull(derived, atTick: 0);

        var ok = PoiseLedger.TryPayAbsorbDrain(pools, absorbedAmount: 400, drainRatioMilli: 250, nowTick: 0, derived); // 25% of 400 = 100

        Assert.True(ok);
        Assert.Equal(0, pools.Resolve(PoiseLedger.ResourceId, 0, derived));
    }

    [Fact]
    public void HoldTickIsJustAnOrdinaryPerTickSpend()
    {
        var derived = PoiseSnapshot(max: 100, regenPerTick: 0);
        var pools = ActorResourcePools.CreateFull(derived, atTick: 0);

        Assert.True(PoiseLedger.TryPayHoldTick(pools, perTickAmount: 10, nowTick: 0, derived));
        Assert.True(PoiseLedger.TryPayHoldTick(pools, perTickAmount: 10, nowTick: 1, derived));
        Assert.Equal(80, pools.Resolve(PoiseLedger.ResourceId, 1, derived));
    }

    [Fact]
    public void NegativeAbsorbedAmountOrRatioIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PoiseLedger.AbsorbDrainAmount(-1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => PoiseLedger.AbsorbDrainAmount(100, -1));
    }
}
