using FusionRpg.Core.Combat.Guard;
using Xunit;

namespace FusionRpg.Core.Tests.Combat.Guard;

/// <summary>class-system-todo.md P7.1-P7.3 — <see cref="PoiseRuntime"/> (spec-guard-economy.md, read
/// in full this session). Table in §9: tests 1, 2, 3, 5, 6, 7 covered here directly. Test 4 ("r < 1"
/// from "V5's emitted poiseRegenPerRound/peerDamagePerRound") needs a real caller feeding live
/// telemetry to measure — V5 (Phase V) itself already deferred this exact metric ("poise/stamina
/// metrics await their runtime" — this program's own earlier evidence), and nothing yet calls
/// `PoiseRuntime` from live gameplay (the action layer that would raise a guard is unbuilt). Covered
/// here via the SAME closed-form methodology this whole program already uses in place of live
/// telemetry (Phase 4/5's `Predictor`/`TerminationGuard`) rather than building unscoped live-telemetry
/// plumbing with no real caller to feed it. Test 8 (the termination re-run) is P7.4's own file. Test 9
/// ("guard costs stamina before the ADR") does not apply to this implementation — see its own note
/// below.</summary>
public class PoiseRuntimeTests
{
    [Fact]
    public void Raising_a_guard_costs_even_when_nothing_lands()
    {
        // spec §3 test 1: Reading C's flat half. Commit takes no "did it land" parameter at all --
        // structurally unconditional.
        var runtime = new PoiseRuntime();
        runtime.SetPoise("bastion", 1000);

        runtime.Commit("bastion", flatCost: 50);

        Assert.Equal(950, runtime.PoiseOf("bastion"));
    }

    [Fact]
    public void Commit_repeatedRegardlessOfOutcome_keepsCostingTheFlatAmount()
    {
        // The "even when nothing lands" claim only means something if it holds across repeated
        // commits, not just once.
        var runtime = new PoiseRuntime();
        runtime.SetPoise("bastion", 1000);

        runtime.Commit("bastion", 50);
        runtime.Commit("bastion", 50);
        runtime.Commit("bastion", 50);

        Assert.Equal(850, runtime.PoiseOf("bastion"));
    }

    [Fact]
    public void Absorb_drain_is_proportional_to_what_was_stopped()
    {
        // spec §3 test 2: Reading C's proportional half. 30% of a 1,000-damage stop, on a full pool.
        var runtime = new PoiseRuntime();
        runtime.SetPoise("bastion", 10_000);

        var drained = runtime.Absorb("bastion", damageStopped: 1000, absorbDrainSharePermille: 300);

        Assert.Equal(300, drained);
        Assert.Equal(9700, runtime.PoiseOf("bastion"));
    }

    [Fact]
    public void Absorb_neverDrainsMoreThanThePoolHolds()
    {
        // Mirrors ShieldRuntime.Absorb's own "never spend more than is there" contract -- the IDEAL
        // share (per absorbDrainSharePermille) can exceed what remains; the runtime must never go negative.
        var runtime = new PoiseRuntime();
        runtime.SetPoise("bastion", 100);

        var drained = runtime.Absorb("bastion", damageStopped: 10_000, absorbDrainSharePermille: 300); // ideal = 3000, pool only has 100

        Assert.Equal(100, drained); // capped at what was actually there
        Assert.Equal(0, runtime.PoiseOf("bastion"));
        Assert.True(runtime.IsExhausted("bastion"));
    }

    [Fact]
    public void Heavy_hits_break_the_guard_and_attrition_does_not()
    {
        // spec §4/§9 test 3: "the FORCE -> BASTION arrow, as an assertion." One round of light,
        // repeated ("attrition") pressure where regen outpaces drain must not net-drain the pool; one
        // round of a single heavy stop must break it outright, even from full.
        const long maxPoise = 1000;
        const long regenPerTick = 80;
        const long absorbShare = 300; // 30%

        var attrition = new PoiseRuntime();
        attrition.SetPoise("bastion", maxPoise);
        attrition.Absorb("bastion", damageStopped: 100, absorbShare); // drains 30
        attrition.Regen("bastion", regenPerTick, maxPoise);            // regens 80 -- net +50, capped at max
        Assert.False(attrition.IsExhausted("bastion"), "light, regen-outpaced pressure must not break the guard");
        Assert.Equal(maxPoise, attrition.PoiseOf("bastion")); // capped, did not overflow past max either

        var heavy = new PoiseRuntime();
        heavy.SetPoise("bastion", maxPoise);
        heavy.Absorb("bastion", damageStopped: 10_000, absorbShare); // ideal drain 3,000 >> the 1,000 pool
        Assert.True(heavy.IsExhausted("bastion"), "a single heavy stop must break the guard outright");
    }

    [Fact]
    public void Poise_regen_never_exceeds_peer_pressure_mechanismCorrectlyBreaksUnderSustainedPressureAtRLessThanOne()
    {
        // spec §9 test 4 (r < 1). Covered structurally, not against a live shipped regen rate --
        // a real gap, recorded here rather than papered over: no aptitude edge feeds
        // resource.max.poise/resource.regen.poise yet (confirmed by reading every edge role in
        // data/seed/aptitudes/roster.json this session -- none names poise), so there is no MEASURED
        // regen coefficient to assert "r < 1" against today. `guardEconomy` ships the absorb SHARE
        // (Reading C's proportional half) but deliberately not a regen rate (spec §4: "sized against
        // peer pressure" is its own balance pass, the same shape as the recovery-scale dial's own
        // "measured, not guessed" solve) -- authoring that edge is a later, dedicated coefficient task
        // (residual-fit-shaped), not P7.1-P7.3's own scope of building the MECHANISM.
        //
        // What IS this task's job: prove PoiseRuntime.Regen correctly encodes what "r < 1" means --
        // sustained drain that exceeds regen EVERY round eventually exhausts the pool (never
        // per-tick-unbreakable, the r >= 1 defect §4 explicitly calls "the same defect the termination
        // invariant names"). Multi-round, not the single-round check test 3 already covers.
        var runtime = new PoiseRuntime();
        const long maxPoise = 1000;
        const long regenPerTick = 80;
        const long drainPerRound = 120; // r = 80/120 = 0.667, under 1 -- deliberately chosen low.
        runtime.SetPoise("bastion", maxPoise);

        // "Broke this round" is checked immediately after THAT round's own Absorb, before its own
        // end-of-round Regen tick runs -- the guard breaking is a moment-in-time event during the hit
        // that drains it, not something a later regen tick should retroactively erase. Checking after
        // Regen instead was this test's own first, wrong draft: Absorb floors gracefully (never drains
        // more than is there) and Regen unconditionally adds a fixed amount, so the pool actually
        // settles into a LOW EQUILIBRIUM (0 after Absorb, back up to regenPerTick after Regen, every
        // round) rather than ever being OBSERVED at zero at the point this test checked -- a genuine
        // property of the mechanism, not a bug, and worth naming rather than silently working around.
        var brokeWithinBudget = false;
        var roundsToBreak = 0;
        for (var round = 0; round < 100 && !brokeWithinBudget; round++)
        {
            runtime.Absorb("bastion", damageStopped: drainPerRound, absorbDrainSharePermille: 1000); // 100% of drainPerRound each round.
            roundsToBreak++;
            if (runtime.IsExhausted("bastion")) { brokeWithinBudget = true; break; }
            runtime.Regen("bastion", regenPerTick, maxPoise);
        }

        Assert.True(brokeWithinBudget,
            "expected sustained drain > regen (r < 1) to break the guard within 100 rounds -- it never did, which is the r >= 1 defect");
        Assert.True(roundsToBreak > 1, "a single round should not have been enough -- this is meant to show SUSTAINED pressure breaking it, not an immediate heavy hit (that is test 3's own case)");
    }

    [Fact]
    public void Spent_poise_converts_to_damage()
    {
        // spec §5/§9 test 5.
        var damage = PoiseRuntime.Riposte(spentPoise: 500, riposteShareCapPermille: 400);
        Assert.Equal(200, damage);
    }

    [Fact]
    public void Riposte_scales_with_the_ladder()
    {
        // spec §9 test 6: an uncapped pool converting a bounded share stays uncapped (PS-8). A huge
        // spend produces a proportionally huge riposte -- no clamp, matching this session's own
        // No_cap_on_an_aptitude precedent (PointBudgetTests.cs).
        const long enormousSpend = 10_000_000_000;
        var damage = PoiseRuntime.Riposte(enormousSpend, riposteShareCapPermille: 400);
        Assert.Equal(enormousSpend * 400 / 1000, damage); // exact, not clamped to any ceiling.
    }

    [Fact]
    public void Poise_at_zero_applies_exhaustion_not_death()
    {
        // spec §2's table / §9 test 7: hp's death-on-empty exemption does not transfer to poise.
        // Structurally guaranteed -- PoiseRuntime touches no HP-shaped state at all, so "not death" is
        // not something this test can fail to observe even in principle -- confirmed via reflection
        // that PoiseRuntime declares nothing HP-shaped, then the behavioural half: draining to zero
        // sets IsExhausted true and throws nothing.
        var members = typeof(PoiseRuntime).GetMembers().Select(m => m.Name);
        Assert.DoesNotContain(members, name => name.Contains("Hp", StringComparison.OrdinalIgnoreCase)
                                             || name.Contains("Death", StringComparison.OrdinalIgnoreCase)
                                             || name.Contains("Kill", StringComparison.OrdinalIgnoreCase));

        var runtime = new PoiseRuntime();
        runtime.SetPoise("bastion", 50);
        runtime.Commit("bastion", 50);

        Assert.Equal(0, runtime.PoiseOf("bastion"));
        Assert.True(runtime.IsExhausted("bastion"));
    }

    // ── argument validation, matching this program's own established Guards/*.cs convention ─────────

    [Fact]
    public void Commit_negativeCost_throws()
    {
        var runtime = new PoiseRuntime();
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Commit("bastion", -1));
    }

    [Fact]
    public void Absorb_negativeDamageStopped_throws()
    {
        var runtime = new PoiseRuntime();
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Absorb("bastion", -1, 300));
    }

    [Fact]
    public void Riposte_negativeSpentPoise_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PoiseRuntime.Riposte(-1, 400));
    }
}
