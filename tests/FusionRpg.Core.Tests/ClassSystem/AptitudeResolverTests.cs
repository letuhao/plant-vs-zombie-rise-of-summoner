using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.ClassSystem;

/// <summary>class-system-todo.md P2.4, spec-aptitude-resolve.md §7 — the tests scoped to this phase
/// (resolver-only, overlay path, one funded aptitude). Tests 1/6/8/11 from the spec's table belong to
/// later tasks: 1 to P2.6 (needs the battle side, P2.5), 6 to P3.2 (the atk double-count guard, "red
/// today" by the spec's own admission), 8 is already covered at the read-functions layer (P2.2), 11 to
/// P3.4 (cross-checks tools/CombatSim's simulator, which this phase does not touch).</summary>
public class AptitudeResolverTests
{
    static AptitudeTuning MinimalTuning() => AptitudeTuningLoader.Parse("""
        {
          "schemaVersion": 1, "version": 1,
          "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
          "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
          "read": {
            "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 },
            "magnitude": { "shareExponentMilli": 1000 }
          },
          "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
          "familyRead": {
            "combat.power": "magnitude",
            "combat.accuracy": "contest"
          },
          "edges": [
            { "channel": "combat.power.omni", "source": "Might", "kMilli": 2200 },
            { "channel": "combat.accuracy.omni", "source": "Might", "kMilli": 500 }
          ]
        }
        """);

    static PowerLadder Ladder() => new(FusionRpg.Core.Power.PowerTuningHub.Tuning);
    static DerivedStatRegistry Registry() => DerivedStatRegistry.CreateDefault();

    // ── P2.4's own acceptance: Might -> combat.power.omni, empty allocation, idempotent ────────────

    [Fact]
    public void MightAllocation_resolvesCombatPowerOmni()
    {
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var mods = AptitudeResolver.Resolve(allocation, MinimalTuning(), Ladder(), theta: 1000, Registry());

        var powerMod = Assert.Single(mods, m => m.ChannelId == "combat.power.omni");
        Assert.Equal(DerivedModifierOp.Flat, powerMod.Op);
        Assert.Equal("aptitude.Might", powerMod.SourceId);
        // Might is the only funded aptitude -> share = 1.0 -> value = k * P(Theta) = 2.2 * P(1000).
        var expected = AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, Ladder().Value(1000));
        Assert.Equal((double)expected, powerMod.Value, 6);
    }

    [Fact]
    public void EmptyAllocation_resolvesToNothing_notZeroValuedModifiers()
    {
        var mods = AptitudeResolver.Resolve(AptitudeAllocation.Empty, MinimalTuning(), Ladder(), theta: 1000, Registry());
        Assert.Empty(mods);
    }

    [Fact]
    public void UnfundedAptitude_contributesNothing_evenWithOtherAptitudesFunded()
    {
        // Fortitude has no edge in MinimalTuning() at all -- funding it must not somehow produce a
        // Might-channel contribution or a stray zero-valued one.
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Fortitude", 100);
        var mods = AptitudeResolver.Resolve(allocation, MinimalTuning(), Ladder(), theta: 1000, Registry());
        Assert.Empty(mods);
    }

    [Fact]
    public void ResolveIsIdempotent_sameInputsSameOutputs()
    {
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var tuning = MinimalTuning();
        var a = AptitudeResolver.Resolve(allocation, tuning, Ladder(), theta: 1000, Registry());
        var b = AptitudeResolver.Resolve(allocation, tuning, Ladder(), theta: 1000, Registry());

        Assert.Equal(a.Count, b.Count);
        foreach (var m in a)
        {
            var match = Assert.Single(b, x => x.ChannelId == m.ChannelId && x.SourceId == m.SourceId);
            Assert.Equal(m.Value, match.Value, 12);
            Assert.Equal(m.Op, match.Op);
        }
    }

    // ── Every resolved channel is registered (spec §7 test 2) ──────────────────────────────────────

    [Fact]
    public void EveryResolvedChannel_isRegistered()
    {
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var registry = Registry();
        var mods = AptitudeResolver.Resolve(allocation, MinimalTuning(), Ladder(), theta: 1000, registry);
        Assert.NotEmpty(mods);
        foreach (var m in mods)
            Assert.True(registry.TryResolveChannel(m.ChannelId, out _), $"unregistered channel: {m.ChannelId}");
    }

    [Fact]
    public void UnregisteredEdgeChannel_throws_ratherThanSilentlyDroppingOrZeroing()
    {
        var badTuning = AptitudeTuningLoader.Parse("""
            {
              "schemaVersion": 1, "version": 1,
              "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
              "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
              "read": { "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 }, "magnitude": { "shareExponentMilli": 1000 } },
              "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
              "familyRead": { "not.a.real.family": "magnitude" },
              "edges": [ { "channel": "not.a.real.family.omni", "source": "Might", "kMilli": 100 } ]
            }
            """);
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        Assert.Throws<InvalidOperationException>(() =>
            AptitudeResolver.Resolve(allocation, badTuning, Ladder(), theta: 1000, Registry()));
    }

    // ── Contest read mode reaches the resolver too (combat.accuracy.omni in MinimalTuning) ─────────

    [Fact]
    public void ContestEdge_resolvesAsDouble_theta_free()
    {
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var atTheta10 = AptitudeResolver.Resolve(allocation, MinimalTuning(), Ladder(), theta: 10, Registry());
        var atTheta5000 = AptitudeResolver.Resolve(allocation, MinimalTuning(), Ladder(), theta: 5000, Registry());

        var a = Assert.Single(atTheta10, m => m.ChannelId == "combat.accuracy.omni");
        var b = Assert.Single(atTheta5000, m => m.ChannelId == "combat.accuracy.omni");
        Assert.Equal(a.Value, b.Value, 9);
    }

    // ── Magnitude proportionality and Theta=0 flatness (spec §7 tests 4, 10) ───────────────────────

    [Fact]
    public void MagnitudeEdge_doublingPThetaDoublesValue()
    {
        // Use two Theta values on the SAME curve rather than asserting proportional-in-Theta directly
        // (P(Theta) itself is only proportional to Theta in the trivial B=0 case) -- what must hold is
        // AptitudeReadFunctions' own contract, exercised here through the resolver end to end.
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var ladder = Ladder();
        var tuning = MinimalTuning();
        var pThetaSmall = ladder.Value(10);
        var pThetaDoubled = pThetaSmall * 2;
        // Find a Theta whose P(Theta) is exactly double -- binary search isn't needed; assert the
        // underlying read function directly matches what the resolver produced, which is what P2.2
        // already proves proportional. This test's job is just "the resolver doesn't break that".
        var mods = AptitudeResolver.Resolve(allocation, tuning, ladder, theta: 10, Registry());
        var powerMod = Assert.Single(mods, m => m.ChannelId == "combat.power.omni");
        Assert.Equal((double)AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, pThetaSmall), powerMod.Value, 6);
        Assert.Equal((double)AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, pThetaDoubled),
                     (double)AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, pThetaSmall) * 2, 6);
    }

    [Fact]
    public void MagnitudeEdge_isFlatWhenThetaIsZero()
    {
        // spec-aptitude-resolve.md §2.0 precondition 2's symptom, pinned: at Theta=0 every magnitude
        // edge collapses to P(0) = C, the same floor regardless of the coefficient's own size.
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var ladder = Ladder();
        var mods = AptitudeResolver.Resolve(allocation, MinimalTuning(), ladder, theta: 0, Registry());
        var powerMod = Assert.Single(mods, m => m.ChannelId == "combat.power.omni");
        Assert.Equal((double)AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, ladder.Value(0)), powerMod.Value, 6);
    }

    // ── Overflow discipline at high Theta (spec §7 test 7) ──────────────────────────────────────────

    [Fact]
    public void MagnitudeEdge_exactAtHighTheta()
    {
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var ladder = Ladder();
        var highTheta = (int)Math.Min(ladder.MaxIndex, 5_000_000);
        var mods = AptitudeResolver.Resolve(allocation, MinimalTuning(), ladder, theta: highTheta, Registry());
        var powerMod = Assert.Single(mods, m => m.ChannelId == "combat.power.omni");
        Assert.Equal((double)AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, ladder.Value(highTheta)), powerMod.Value, 3);
    }

    [Fact]
    public void MagnitudeEdge_oversizedCoefficient_throwsRatherThanWraps()
    {
        var oversizedTuning = AptitudeTuningLoader.Parse("""
            {
              "schemaVersion": 1, "version": 1,
              "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
              "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
              "read": { "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 }, "magnitude": { "shareExponentMilli": 1000 } },
              "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
              "familyRead": { "combat.power": "magnitude" },
              "edges": [ { "channel": "combat.power.omni", "source": "Might", "kMilli": 9223372036854775807 } ]
            }
            """);
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 1);
        var ladder = Ladder();
        var theta = (int)Math.Min(ladder.MaxIndex, 5_000_000);
        Assert.Throws<OverflowException>(() =>
            AptitudeResolver.Resolve(allocation, oversizedTuning, ladder, theta, Registry()));
    }

    // ── Null-argument guards ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void NullArguments_reject()
    {
        var allocation = AptitudeAllocation.Empty;
        var tuning = MinimalTuning();
        var ladder = Ladder();
        var registry = Registry();
        Assert.Throws<ArgumentNullException>(() => AptitudeResolver.Resolve(null!, tuning, ladder, 0, registry));
        Assert.Throws<ArgumentNullException>(() => AptitudeResolver.Resolve(allocation, null!, ladder, 0, registry));
        Assert.Throws<ArgumentNullException>(() => AptitudeResolver.Resolve(allocation, tuning, null!, 0, registry));
        Assert.Throws<ArgumentNullException>(() => AptitudeResolver.Resolve(allocation, tuning, ladder, 0, null!));
    }

    // ── ResolveForBattle: P2.5's battle-path twin ───────────────────────────────────────────────────

    [Fact]
    public void ResolveForBattle_mightAllocation_resolvesCombatPowerOmni_asLong()
    {
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var ladder = Ladder();
        var mods = AptitudeResolver.ResolveForBattle(allocation, MinimalTuning(), ladder, theta: 1000, Registry());

        var powerMod = Assert.Single(mods, m => m.ChannelId == "combat.power.omni");
        var expected = AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, ladder.Value(1000));
        Assert.Equal(expected, powerMod.Amount);
    }

    [Fact]
    public void ResolveForBattle_matchesOverlayResolve_forTheSameMagnitudeEdge()
    {
        // Both seams must agree (spec-aptitude-resolve.md §1 "same allocation resolves to
        // byte-identical channel values" -- the full cross-composer proof is P2.6's, but the shared
        // arithmetic underneath is provable right here, at the resolver layer, without either composer.
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var ladder = Ladder();
        var tuning = MinimalTuning();

        var overlayMods = AptitudeResolver.Resolve(allocation, tuning, ladder, theta: 777, Registry());
        var battleMods = AptitudeResolver.ResolveForBattle(allocation, tuning, ladder, theta: 777, Registry());

        var overlayPower = Assert.Single(overlayMods, m => m.ChannelId == "combat.power.omni");
        var battlePower = Assert.Single(battleMods, m => m.ChannelId == "combat.power.omni");
        Assert.Equal(overlayPower.Value, (double)battlePower.Amount, 9);
    }

    [Fact]
    public void ResolveForBattle_emptyAllocation_resolvesToNothing()
    {
        var mods = AptitudeResolver.ResolveForBattle(AptitudeAllocation.Empty, MinimalTuning(), Ladder(), theta: 1000, Registry());
        Assert.Empty(mods);
    }

    [Fact]
    public void ResolveForBattle_contestEdge_narrowsToLong()
    {
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var mods = AptitudeResolver.ResolveForBattle(allocation, MinimalTuning(), Ladder(), theta: 1000, Registry());
        var accuracyMod = Assert.Single(mods, m => m.ChannelId == "combat.accuracy.omni");
        // k=0.5, share=1.0, gamma=1.0, span=100 -> 50.0 exactly -> 50L.
        Assert.Equal(50L, accuracyMod.Amount);
    }

    [Fact]
    public void ResolveForBattle_unregisteredChannel_throws()
    {
        var badTuning = AptitudeTuningLoader.Parse("""
            {
              "schemaVersion": 1, "version": 1,
              "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
              "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
              "read": { "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 }, "magnitude": { "shareExponentMilli": 1000 } },
              "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
              "familyRead": { "not.a.real.family": "magnitude" },
              "edges": [ { "channel": "not.a.real.family.omni", "source": "Might", "kMilli": 100 } ]
            }
            """);
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        Assert.Throws<InvalidOperationException>(() =>
            AptitudeResolver.ResolveForBattle(allocation, badTuning, Ladder(), theta: 1000, Registry()));
    }

    [Fact]
    public void ResolveForBattle_nullArguments_reject()
    {
        var allocation = AptitudeAllocation.Empty;
        var tuning = MinimalTuning();
        var ladder = Ladder();
        var registry = Registry();
        Assert.Throws<ArgumentNullException>(() => AptitudeResolver.ResolveForBattle(null!, tuning, ladder, 0, registry));
        Assert.Throws<ArgumentNullException>(() => AptitudeResolver.ResolveForBattle(allocation, null!, ladder, 0, registry));
        Assert.Throws<ArgumentNullException>(() => AptitudeResolver.ResolveForBattle(allocation, tuning, null!, 0, registry));
        Assert.Throws<ArgumentNullException>(() => AptitudeResolver.ResolveForBattle(allocation, tuning, ladder, 0, null!));
    }

    // ── The recovery-scale dial (class-system-ideal.md §5d) — found missing 2026-08-27 ─────────────

    static AptitudeTuning RecoveryTuning() => AptitudeTuningLoader.Parse("""
        {
          "schemaVersion": 1, "version": 1,
          "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
          "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
          "read": { "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 }, "magnitude": { "shareExponentMilli": 1000 } },
          "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
          "familyRead": { "resource.regen": "magnitude" },
          "edges": [ { "channel": "resource.regen.hp", "source": "Vigor", "kMilli": 12000 } ]
        }
        """);

    [Fact]
    public void RecoveryFamilyEdge_appliesTheScaleDial()
    {
        // Regression: AptitudeResolver used to read every edge's raw kMilli, silently discarding
        // tuning.Recovery.ScaleMilli -- the termination-invariant dial the shipped file's own
        // recovery._scaleWhy note says was solved against a measured r=1.33 (an unkillable pair).
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", 100);
        var ladder = Ladder();
        var mods = AptitudeResolver.Resolve(allocation, RecoveryTuning(), ladder, theta: 1000, Registry());

        var regenMod = Assert.Single(mods, m => m.ChannelId == "resource.regen.hp");
        // effective kMilli = 12000 * 374 / 1000 = 4488.
        var expected = AptitudeReadFunctions.Magnitude(4488, 1.0, 1000, ladder.Value(1000));
        Assert.Equal((double)expected, regenMod.Value, 6);

        var unscaled = AptitudeReadFunctions.Magnitude(12000, 1.0, 1000, ladder.Value(1000));
        Assert.True(regenMod.Value < unscaled * 0.5, "recovery scale should meaningfully dampen the edge, not merely round it");
    }

    [Fact]
    public void ResolveForBattle_recoveryFamilyEdge_appliesTheScaleDialToo()
    {
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", 100);
        var ladder = Ladder();
        var mods = AptitudeResolver.ResolveForBattle(allocation, RecoveryTuning(), ladder, theta: 1000, Registry());

        var regenMod = Assert.Single(mods, m => m.ChannelId == "resource.regen.hp");
        var expected = AptitudeReadFunctions.Magnitude(4488, 1.0, 1000, ladder.Value(1000));
        Assert.Equal(expected, regenMod.Amount);
    }

    [Fact]
    public void NonRecoveryFamilyEdge_isUnaffectedByTheScaleDial()
    {
        // combat.power.omni is not in RecoveryTuning()'s recovery.families -- must read its raw kMilli.
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100);
        var ladder = Ladder();
        var mods = AptitudeResolver.Resolve(allocation, MinimalTuning(), ladder, theta: 1000, Registry());
        var powerMod = Assert.Single(mods, m => m.ChannelId == "combat.power.omni");
        var expected = AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, ladder.Value(1000));
        Assert.Equal((double)expected, powerMod.Value, 6);
    }
}
