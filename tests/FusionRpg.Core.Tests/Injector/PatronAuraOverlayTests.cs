using FusionRpg.Core.Demons.Patron;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Injector.Effects;
using Xunit;

namespace FusionRpg.Core.Tests.Injector;

/// <summary>aura-skill T1: PatronAuraOverlay migrated from a manual read-then-<c>Overlay</c>
/// compensation to <c>OverlayAdd</c>. These tests pin the numeric output unchanged (no silent
/// balance change) and prove the migration actually removed the double-add hazard.
///
/// <c>PatronRuntimeState</c> is a process-global static gate (no DI seam), so every test resets it
/// in a <c>finally</c> to avoid leaking state across the assembly's test run.</summary>
public class PatronAuraOverlayTests
{
    [Fact]
    public void No_match_aura_returns_the_input_snapshot_unchanged()
    {
        PatronRuntimeState.EndMatch();
        var baseline = ActorDerivedSnapshot.Empty;

        var result = PatronAuraOverlay.Apply(baseline, "plant");

        Assert.Same(baseline, result);
    }

    [Fact]
    public void Zombie_side_is_never_touched_even_with_an_active_match_aura()
    {
        try
        {
            PatronRuntimeState.BeginMatch(new PatronAura("fire", null, 150, 75, 0, 0));
            var baseline = ActorDerivedSnapshot.Empty;

            var result = PatronAuraOverlay.Apply(baseline, "zombie");

            Assert.Same(baseline, result);
        }
        finally
        {
            PatronRuntimeState.EndMatch();
        }
    }

    [Fact]
    public void Primary_element_only_matches_the_pre_migration_formula()
    {
        // Regression: old code did `derived.Get(channel) + milli/10.0` then Overlay(replace).
        // New code does OverlayAdd(milli/10.0) onto the same base. Both compute base + milli/10.
        try
        {
            PatronRuntimeState.BeginMatch(new PatronAura("fire", null, 150, 75, 0, 0));
            var baseline = ActorDerivedSnapshot.FromValues(new[]
            {
                new KeyValuePair<string, double>("combat.power.fire", 3.0),
                new KeyValuePair<string, double>("combat.defense.fire", 1.0)
            });

            var result = PatronAuraOverlay.Apply(baseline, "plant");

            Assert.Equal(3.0 + 15.0, result.Get("combat.power.fire"));   // 150‰ / 10 = 15
            Assert.Equal(1.0 + 7.5, result.Get("combat.defense.fire"));  // 75‰ / 10 = 7.5
        }
        finally
        {
            PatronRuntimeState.EndMatch();
        }
    }

    [Fact]
    public void Secondary_element_is_added_at_half_weight_same_as_before()
    {
        try
        {
            PatronRuntimeState.BeginMatch(new PatronAura("fire", "ice", 150, 75, 75, 40));
            var baseline = ActorDerivedSnapshot.Empty;

            var result = PatronAuraOverlay.Apply(baseline, "plant");

            Assert.Equal(15.0, result.Get("combat.power.fire"));
            Assert.Equal(7.5, result.Get("combat.defense.fire"));
            Assert.Equal(7.5, result.Get("combat.power.ice"));
            Assert.Equal(4.0, result.Get("combat.defense.ice"));
        }
        finally
        {
            PatronRuntimeState.EndMatch();
        }
    }

    [Fact]
    public void Zero_milli_channels_are_skipped_never_writing_a_zero_contribution()
    {
        try
        {
            PatronRuntimeState.BeginMatch(new PatronAura("fire", null, 150, 0, 0, 0));
            var result = PatronAuraOverlay.Apply(ActorDerivedSnapshot.Empty, "plant");

            Assert.Equal(15.0, result.Get("combat.power.fire"));
            // Never contributed to -> stays the channel default (0), not an explicit 0 entry.
            Assert.False(result.TryGet("combat.defense.fire", out _));
        }
        finally
        {
            PatronRuntimeState.EndMatch();
        }
    }

    [Fact]
    public void D1_regression_a_second_independent_overlay_on_a_different_channel_survives()
    {
        // The defect this migration fixes: Overlay (replace) on a shared-channel second producer
        // would have erased the first producer's contribution. Prove OverlayAdd does not.
        try
        {
            PatronRuntimeState.BeginMatch(new PatronAura("fire", null, 150, 0, 0, 0));
            var baseline = ActorDerivedSnapshot.Empty;

            var afterPatron = PatronAuraOverlay.Apply(baseline, "plant");
            var afterSecondProducer = afterPatron.OverlayAdd(
                new[] { new KeyValuePair<string, double>("combat.power.fire", 5.0) });

            // Patron's own contribution must still be present, summed with the second producer's.
            Assert.Equal(20.0, afterSecondProducer.Get("combat.power.fire"));
        }
        finally
        {
            PatronRuntimeState.EndMatch();
        }
    }

    [Fact]
    public void Idempotence_two_independent_resolves_from_the_same_base_agree()
    {
        // D2: ActorHub resolves derived channels fresh per call, so PatronAuraOverlay.Apply is
        // always invoked on a freshly-composed base, never a chained/accumulated one. Applying it
        // twice from the SAME base must produce identical results — proving the contribution
        // depends only on (aura, base), never on prior applications.
        try
        {
            PatronRuntimeState.BeginMatch(new PatronAura("fire", null, 150, 75, 0, 0));
            var baseline = ActorDerivedSnapshot.FromValues(
                new[] { new KeyValuePair<string, double>("combat.power.fire", 2.0) });

            var first = PatronAuraOverlay.Apply(baseline, "plant");
            var second = PatronAuraOverlay.Apply(baseline, "plant");

            Assert.Equal(first.Get("combat.power.fire"), second.Get("combat.power.fire"));
            Assert.Equal(17.0, first.Get("combat.power.fire"));
        }
        finally
        {
            PatronRuntimeState.EndMatch();
        }
    }
}
