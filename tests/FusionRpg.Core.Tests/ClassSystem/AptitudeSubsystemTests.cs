using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using Xunit;

namespace FusionRpg.Core.Tests.ClassSystem;

/// <summary>class-system-todo.md P2.4 — AptitudeSubsystem, the registered IActorStatSubsystem seam
/// (spec-aptitude-resolve.md §2), and its wiring through ActorHub.Register /
/// ActorHubBootstrap.CreateDefault.</summary>
public class AptitudeSubsystemTests
{
    static AptitudeTuning MightOnlyTuning() => AptitudeTuningLoader.Parse("""
        {
          "schemaVersion": 1, "version": 1,
          "grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 },
          "pointEconomy": { "aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }, "respecPrice": 10 }, "guardEconomy": { "flatCommitCost": 50, "absorbDrainSharePermille": 300, "riposteShareCapPermille": 400 }, "mitigation": { "scaleMilli": 1000, "families": ["combat.defense", "combat.dodge", "combat.parry", "combat.block", "combat.absorption", "combat.heal"] },
          "read": { "contest": { "spanPoints": 100.0, "shareExponentMilli": 1000 }, "magnitude": { "shareExponentMilli": 1000 } },
          "recovery": { "scaleMilli": 374, "targetRecoveryShareMilli": 670, "families": ["resource.regen"] },
          "familyRead": { "combat.power": "magnitude" },
          "edges": [ { "channel": "combat.power.omni", "source": "Might", "kMilli": 2200 } ]
        }
        """);

    static PowerLadder Ladder() => new(PowerTuningHub.Tuning);

    [Fact]
    public void IsAnIActorStatSubsystem()
    {
        Assert.IsAssignableFrom<IActorStatSubsystem>(
            new AptitudeSubsystem(MightOnlyTuning(), Ladder()));
    }

    [Fact]
    public void SubsystemId_isStable()
    {
        var s = new AptitudeSubsystem(MightOnlyTuning(), Ladder());
        Assert.Equal("rpg.aptitude", s.SubsystemId);
    }

    [Fact]
    public void RegisteredThroughActorHub_contributesTheFundedChannel()
    {
        var stats = StatSystemBootstrap.CreateDefault();
        var hub = new FusionRpg.Core.Stats.Derived.ActorHub(stats);
        hub.Register(new AptitudeSubsystem(
            MightOnlyTuning(), Ladder(),
            allocation: _ => AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100)));

        var ctx = stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });
        var derived = hub.ResolveDerived(ctx);

        Assert.True(derived.Get("combat.power.omni", double.NaN) > 0);
    }

    [Fact]
    public void EmptyAllocation_leavesDerivedSnapshotAtDefaults()
    {
        // The default allocation (nobody has spent a point) must be provably inert through the real
        // ActorHub.Register seam -- the property class-system-todo.md P2.4 and success criterion 9
        // both name ("zero goldens move on an empty allocation").
        var stats = StatSystemBootstrap.CreateDefault();
        var hub = new FusionRpg.Core.Stats.Derived.ActorHub(stats);
        hub.Register(new AptitudeSubsystem(MightOnlyTuning(), Ladder())); // allocation omitted -> Empty

        var ctx = stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });
        var derived = hub.ResolveDerived(ctx);

        Assert.Equal(0.0, derived.Get("combat.power.omni", 0.0));
    }

    [Fact]
    public void ContributeDerived_isIdempotent_callingTwiceYieldsOneSetNotTwo()
    {
        var s = new AptitudeSubsystem(
            MightOnlyTuning(), Ladder(),
            allocation: _ => AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100));
        var stats = StatSystemBootstrap.CreateDefault();
        var ctx = stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });

        var modsA = new List<DerivedModifier>();
        s.ContributeDerived(ctx, modsA);
        var modsB = new List<DerivedModifier>();
        s.ContributeDerived(ctx, modsB);

        Assert.Equal(modsA.Count, modsB.Count);
        Assert.Equal(modsA[0].Value, modsB[0].Value, 12);
    }

    [Fact]
    public void DoubleRegistration_throughActorHub_doesNotDoubleContribute()
    {
        // ActorHub.Register replaces by SubsystemId -- registering twice must not double the resolved
        // value (spec-aptitude-resolve.md §2 rule 1).
        var stats = StatSystemBootstrap.CreateDefault();
        var hub = new FusionRpg.Core.Stats.Derived.ActorHub(stats);
        var subsystem = new AptitudeSubsystem(
            MightOnlyTuning(), Ladder(),
            allocation: _ => AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100));
        hub.Register(subsystem);
        hub.Register(subsystem);

        var ctx = stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });
        var derived = hub.ResolveDerived(ctx);
        var once = new List<DerivedModifier>();
        subsystem.ContributeDerived(ctx, once);

        Assert.Equal(once[0].Value, derived.Get("combat.power.omni", double.NaN), 6);
    }

    [Fact]
    public void ThetaComesFromThePowerIndexProvider()
    {
        var provider = new FixedPowerIndexProvider(1000);
        var s = new AptitudeSubsystem(
            MightOnlyTuning(), Ladder(), powerIndex: provider,
            allocation: _ => AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100));
        var stats = StatSystemBootstrap.CreateDefault();
        var ctx = stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });

        var mods = new List<DerivedModifier>();
        s.ContributeDerived(ctx, mods);

        var expected = AptitudeReadFunctions.Magnitude(2200, 1.0, 1000, Ladder().Value(1000));
        Assert.Equal((double)expected, mods[0].Value, 6);
    }

    sealed class FixedPowerIndexProvider : IPowerIndexProvider
    {
        readonly int _theta;
        public FixedPowerIndexProvider(int theta) => _theta = theta;
        public int ActorIndex(StatContext ctx) => _theta;
        public int ContentIndex(ContentContext ctx) => _theta;
        public PowerAxisReport Explain(StatContext ctx) => throw new NotSupportedException();
    }

    // ── ActorHubBootstrap.CreateDefault wiring (opt-in, and safe when omitted) ─────────────────────

    [Fact]
    public void CreateDefault_withoutAptitudeTuning_registersNoAptitudeSubsystem()
    {
        var hub = ActorHubBootstrap.CreateDefault();
        Assert.DoesNotContain(hub.Subsystems, s => s.SubsystemId == "rpg.aptitude");
    }

    [Fact]
    public void CreateDefault_withAptitudeTuning_registersIt()
    {
        var hub = ActorHubBootstrap.CreateDefault(aptitudeTuning: MightOnlyTuning());
        Assert.Contains(hub.Subsystems, s => s.SubsystemId == "rpg.aptitude");
    }

    [Fact]
    public void CreateDefault_withAptitudeTuning_defaultAllocationIsEmpty_zeroChannelImpact()
    {
        var hub = ActorHubBootstrap.CreateDefault(aptitudeTuning: MightOnlyTuning());
        var ctx = hub.Stats.Contexts.ForPlant("P1", new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 });
        var derived = hub.ResolveDerived(ctx);
        Assert.Equal(0.0, derived.Get("combat.power.omni", 0.0));
    }
}
