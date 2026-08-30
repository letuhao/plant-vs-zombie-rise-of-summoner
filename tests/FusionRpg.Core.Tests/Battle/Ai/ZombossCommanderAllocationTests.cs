using FusionRpg.Core.Battle.Ai;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Ai;

/// <summary>aura-skill T9b: `ZombossCommanderAllocation` is the first production caller of
/// `ZombossPattern.ToAllocation` — nine authored patterns existed with zero production callers before
/// this. Mirrors `CommanderAllocationSourceTests`' own shape for Dave's half of the same acceptance
/// bullet ("each commander resolves an aptitude allocation").</summary>
public class ZombossCommanderAllocationTests
{
    static AptitudeTuning CommanderRateOneTuning() => AptitudeTuningLoader.Parse("""
        {
          "schemaVersion": 1, "version": 1,
          "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
          "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 1, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
          "read": { "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 }, "magnitude": { "shareExponentMilli": 1000 } },
          "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
          "familyRead": { "combat.power": "magnitude" },
          "edges": [ { "channel": "combat.power.omni", "source": "Might", "kMilli": 1000 } ]
        }
        """);

    [Fact]
    public void Constructing_with_an_unknown_pattern_id_throws()
    {
        Assert.Throws<ArgumentException>(() => new ZombossCommanderAllocation("not-a-real-pattern"));
    }

    [Fact]
    public void Setting_an_unknown_pattern_id_throws_and_leaves_the_active_pattern_unchanged()
    {
        var source = new ZombossCommanderAllocation("force-pure");
        Assert.Throws<ArgumentException>(() => source.SetActivePattern("not-a-real-pattern"));
        Assert.Equal("force-pure", source.ActivePatternId);
    }

    [Fact]
    public void Before_any_refresh_resolves_to_empty_allocation()
    {
        var source = new ZombossCommanderAllocation("force-pure");
        var result = source.Resolve(new StatContext());
        Assert.Equal(0, result.PointsAt(AllocationScope.Commander, "Might"));
    }

    [Fact]
    public void After_refresh_resolves_the_active_patterns_shares_scaled_by_the_real_point_budget()
    {
        var tuning = CommanderRateOneTuning();
        var source = new ZombossCommanderAllocation("force-pure");
        source.Refresh(theta: 1000, tuning); // budget = PointsFor(Commander, 1000, tuning) = 1000*1 = 1000

        var result = source.Resolve(new StatContext());

        // force-pure: Might=396, Vigor=150, Onslaught=153, Retribution=300 (permille) -- ZombossPattern.
        // ToAllocation's own formula: points = budget * permille / 1000 = 1000 * permille / 1000 = permille.
        Assert.Equal(396, result.PointsAt(AllocationScope.Commander, "Might"));
        Assert.Equal(150, result.PointsAt(AllocationScope.Commander, "Vigor"));
        Assert.Equal(153, result.PointsAt(AllocationScope.Commander, "Onslaught"));
        Assert.Equal(300, result.PointsAt(AllocationScope.Commander, "Retribution"));
    }

    [Fact]
    public void Switching_pattern_and_refreshing_changes_the_resolved_allocation()
    {
        var tuning = CommanderRateOneTuning();
        var source = new ZombossCommanderAllocation("force-pure");
        source.Refresh(theta: 1000, tuning);
        var forcePureMight = source.Resolve(new StatContext()).PointsAt(AllocationScope.Commander, "Might");

        source.SetActivePattern("bastion-pure");
        source.Refresh(theta: 1000, tuning);
        var bastionPureMight = source.Resolve(new StatContext()).PointsAt(AllocationScope.Commander, "Might");
        var bastionPureFerocity = source.Resolve(new StatContext()).PointsAt(AllocationScope.Commander, "Ferocity");

        Assert.Equal(396, forcePureMight);
        Assert.Equal(0, bastionPureMight); // bastion-pure has no Might share at all
        Assert.Equal(402, bastionPureFerocity); // bastion-pure: Ferocity=402 permille
    }

    [Fact]
    public void Every_authored_pattern_resolves_without_throwing_at_a_real_theta()
    {
        var tuning = CommanderRateOneTuning();
        foreach (var patternId in ZombossPatterns.All)
        {
            var source = new ZombossCommanderAllocation(patternId);
            var exception = Record.Exception(() => source.Refresh(theta: 5000, tuning));
            Assert.Null(exception);
        }
    }
}
