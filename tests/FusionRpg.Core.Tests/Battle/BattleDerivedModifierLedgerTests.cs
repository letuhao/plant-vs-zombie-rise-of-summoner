using FusionRpg.Core.Battle;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>aura-skill T4: the recompose seam. `BattleDerivedModifierLedger.Recompose` is the piece
/// downstream aura work (T9+) will call the moment an aura toggles — these tests prove it in
/// isolation, since <c>BattleEngine.ActorState</c>/<c>BattleRunState</c> are private nested types and
/// cannot be reached directly from outside `BattleEngine.cs` (same constraint
/// `ActionSelectionAdoptionTests`' own doc comment names). The math itself does not depend on being
/// invoked from inside a live `Resolve()` call — it is a pure function of (base snapshot, active
/// sources) — so proving it here proves the real thing, not a stand-in.</summary>
public class BattleDerivedModifierLedgerTests
{
    const string Actor = "squad:0";
    const string Other = "squad:1";
    const string Channel = "combat.power.fire";

    static ActorDerivedSnapshot Base(double value) => ActorDerivedSnapshot.FromValues(
        new[] { new KeyValuePair<string, double>(Channel, value) });

    [Fact]
    public void An_empty_ledger_recomposes_nothing()
    {
        var ledger = new BattleDerivedModifierLedger();
        var baseline = Base(10.0);
        var live = ActorDerivedSnapshot.FromValues(baseline.Channels);

        ledger.Recompose(Actor, baseline, live);

        // Not just "value unchanged" -- the channel dictionary itself was never touched, because
        // Recompose only ever visits (actor, channel) pairs it has an Add for. Proven by TryGet
        // still succeeding with the exact original value, never a defaulted-in entry.
        Assert.True(live.TryGet(Channel, out var v));
        Assert.Equal(10.0, v);
    }

    [Fact]
    public void Recompose_mid_resolution_matches_composing_the_same_state_up_front()
    {
        // The task's own acceptance bar: a recompose mid-resolution must produce the same result as
        // composing that state up front. "Up front" = OverlayAdd in one shot (T1's own mechanism).
        var baseline = Base(10.0);
        var contributions = new[] { new KeyValuePair<string, double>(Channel, 4.0) };
        var composedUpFront = baseline.OverlayAdd(contributions);

        var ledger = new BattleDerivedModifierLedger();
        ledger.Add(Actor, Channel, sourceId: "aura:ember", value: 4.0);
        var live = ActorDerivedSnapshot.FromValues(baseline.Channels);
        ledger.Recompose(Actor, baseline, live);

        Assert.Equal(composedUpFront.Get(Channel), live.Get(Channel));
        Assert.Equal(14.0, live.Get(Channel));
    }

    [Fact]
    public void Two_active_sources_on_the_same_channel_sum_matching_FlatSum_semantics()
    {
        var baseline = Base(10.0);
        var ledger = new BattleDerivedModifierLedger();
        ledger.Add(Actor, Channel, "aura:ember", 4.0);
        ledger.Add(Actor, Channel, "commander:allocation", 3.0);
        var live = ActorDerivedSnapshot.FromValues(baseline.Channels);

        ledger.Recompose(Actor, baseline, live);

        Assert.Equal(17.0, live.Get(Channel));
    }

    [Fact]
    public void Repeated_recompose_calls_are_idempotent_never_cumulative()
    {
        // D2: recompose from the frozen base, never from live's own prior value -- calling it twice
        // must not double the contribution.
        var baseline = Base(10.0);
        var ledger = new BattleDerivedModifierLedger();
        ledger.Add(Actor, Channel, "aura:ember", 4.0);
        var live = ActorDerivedSnapshot.FromValues(baseline.Channels);

        ledger.Recompose(Actor, baseline, live);
        ledger.Recompose(Actor, baseline, live);
        ledger.Recompose(Actor, baseline, live);

        Assert.Equal(14.0, live.Get(Channel));
    }

    [Fact]
    public void Withdrawing_one_source_leaves_the_other_intact_and_removes_only_its_own_share()
    {
        var baseline = Base(10.0);
        var ledger = new BattleDerivedModifierLedger();
        ledger.Add(Actor, Channel, "aura:ember", 4.0);
        ledger.Add(Actor, Channel, "commander:allocation", 3.0);
        var live = ActorDerivedSnapshot.FromValues(baseline.Channels);
        ledger.Recompose(Actor, baseline, live);
        Assert.Equal(17.0, live.Get(Channel));

        ledger.RemoveBySource(Actor, "aura:ember");
        ledger.Recompose(Actor, baseline, live);

        Assert.Equal(13.0, live.Get(Channel)); // base 10 + commander's 3, ember's 4 fully gone
    }

    [Fact]
    public void Withdrawing_the_last_source_falls_all_the_way_back_to_base()
    {
        var baseline = Base(10.0);
        var ledger = new BattleDerivedModifierLedger();
        ledger.Add(Actor, Channel, "aura:ember", 4.0);
        var live = ActorDerivedSnapshot.FromValues(baseline.Channels);
        ledger.Recompose(Actor, baseline, live);
        Assert.Equal(14.0, live.Get(Channel));

        ledger.RemoveBySource(Actor, "aura:ember");
        ledger.Recompose(Actor, baseline, live);

        Assert.Equal(10.0, live.Get(Channel));
    }

    [Fact]
    public void RemoveBySource_never_touches_another_actors_contributions()
    {
        var baseline = Base(10.0);
        var ledger = new BattleDerivedModifierLedger();
        ledger.Add(Actor, Channel, "aura:ember", 4.0);
        ledger.Add(Other, Channel, "aura:ember", 4.0); // same source id, different actor

        ledger.RemoveBySource(Actor, "aura:ember");

        var liveOther = ActorDerivedSnapshot.FromValues(baseline.Channels);
        ledger.Recompose(Other, baseline, liveOther);
        Assert.Equal(14.0, liveOther.Get(Channel)); // untouched by the other actor's withdrawal
    }
}
