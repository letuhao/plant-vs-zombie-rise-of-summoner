using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Ai;
using FusionRpg.Core.Delve.Encounter;
using FusionRpg.Core.Expeditions;
using FusionRpg.Core.Items.Consumables;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using Xunit;

namespace FusionRpg.Core.Tests.Stats;

/// <summary>
/// channelmods-hub T2 parity — UniqueCreature aptitude / Zomboss pattern / draught / expedition
/// injury / boss kit moved out of private <c>BattleChannelMod</c> arithmetic into Hub contributions
/// sharing one Core read path. Species <c>AptitudeChannelMods</c> has zero production callers
/// (proven-unused, DEBT-tagged) and is pinned by its own tests, not here.
/// </summary>
public class ChannelModsHubParityTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    // Same direct-file discipline as BossBuildTests: AptitudeTuningHub is NOT read (per-test-class
    // race); the tuning file is parsed inline. PowerTuningHub IS whole-assembly configured.
    static readonly AptitudeTuning AptitudeTuning =
        AptitudeTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "aptitudes.v2.json")));
    static readonly PowerTuning PowerTuning = PowerTuningHub.Tuning;
    static DerivedStatRegistry Registry() => DerivedStatRegistry.CreateDefault();

    // ExpeditionTuningHub stays on the assembly bootstrap's ambient values (same working set
    // as the shipped file): reconfiguring shared hubs from files here once flapped unrelated
    // tests by swapping their tuning mid-run. Only the divisor is read below — identical either way.

    static Dictionary<string, long> SumBattle(IEnumerable<BattleChannelMod> mods) =>
        mods.GroupBy(m => m.ChannelId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Amount), StringComparer.Ordinal);

    // Hub Resolve keeps Contest reads fractional; the battle twin narrows half-away-from-zero.
    // The ONLY permitted difference per channel is that narrowing.
    static Dictionary<string, long> SumHub(IEnumerable<DerivedModifier> mods) =>
        mods.GroupBy(m => m.ChannelId)
            .ToDictionary(g => g.Key,
                g => g.Sum(m => checked((long)Math.Round(m.Value, MidpointRounding.AwayFromZero))),
                StringComparer.Ordinal);

    static AptitudeAllocation FundedAllocation()
    {
        var ids = AptitudeCatalog.All.Take(3).Select(r => r.Id).ToArray();
        var alloc = AptitudeAllocation.Empty;
        foreach (var id in ids)
            alloc += AptitudeAllocation.Single(AllocationScope.Commander, id, 100);
        alloc += AptitudeAllocation.Single(AllocationScope.UniqueCreature, ids[0], 50);
        return alloc;
    }

    static AptitudeAllocation PatternAllocation(string patternId, long theta)
    {
        var commander = new ZombossCommanderAllocation(patternId);
        commander.Refresh(AllocationScope.Commander, theta, AptitudeTuning);
        return commander.Resolve(null!);
    }

    [Fact]
    public void AptitudeResolve_matches_retired_battle_twin_per_channel()
    {
        // battle-hub-fuse T6: ResolveForBattle is deleted; this literal was captured from its
        // output pre-delete (the passing parity run proved Hub == battle ± narrowing), so Hub
        // drift still fails loudly here. Capture run: see evidence map T2/T6.
        const int theta = 100;
        var allocation = FundedAllocation();
        var ladder = new PowerLadder(PowerTuning);

        var hub = SumHub(AptitudeResolver.Resolve(allocation, AptitudeTuning, ladder, theta, Registry()));

        Assert.Equal(new Dictionary<string, long>(StringComparer.Ordinal), hub);
    }

    [Fact]
    public void KitResolve_matches_retired_ResolveKit_per_channel()
    {
        // Captured pre-delete like above: BossBuild.ResolveKit("force-pure", 127) output.
        const int thetaBoss = 127;
        var allocation = PatternAllocation("force-pure", thetaBoss);
        var ladder = new PowerLadder(PowerTuning);

        var hub = SumHub(AptitudeResolver.Resolve(allocation, AptitudeTuning, ladder, thetaBoss, Registry()));

        Assert.Equal(new Dictionary<string, long>(StringComparer.Ordinal), hub);
    }

    [Fact]
    public void ZombossPatternResolve_matches_retired_Concat_per_channel()
    {
        // Captured pre-delete like above: the ResolveForBattle output ApplyZombossPattern concated.
        const int level = 42;
        var allocation = PatternAllocation("bastion-pure", level);
        var ladder = new PowerLadder(PowerTuning);

        var hub = SumHub(AptitudeResolver.Resolve(allocation, AptitudeTuning, ladder, level, Registry()));

        Assert.Equal(new Dictionary<string, long>(StringComparer.Ordinal), hub);
    }

    [Fact]
    public void DraughtTwin_maps_manifest_one_to_one_with_attributed_source_ids()
    {
        // battle-hub-fuse T6: the BattleChannelMod Apply is deleted; the twin's 1:1 mapping is
        // pinned literally (per-member fan-out lives in DraughtSubsystem, pinned separately).
        var manifest = new[]
        {
            new DraughtMod("draught:ember-flask", DerivedStatChannels.CombatPowerOmni, 25),
            new DraughtMod("draught:stone-tonic", DerivedStatChannels.CombatDefenseOmni, 10),
        };

        var twin = DraughtProjection.ToDerivedModifiers(manifest);

        Assert.Equal(2, twin.Count);
        Assert.Equal(DerivedStatChannels.CombatPowerOmni, twin[0].ChannelId);
        Assert.Equal(25L, checked((long)twin[0].Value));
        Assert.Equal("grant:draught:draught:ember-flask", twin[0].SourceId);
        Assert.Equal(DerivedStatChannels.CombatDefenseOmni, twin[1].ChannelId);
        Assert.Equal(10L, checked((long)twin[1].Value));
        Assert.Equal("grant:draught:draught:stone-tonic", twin[1].SourceId);
    }

    [Fact]
    public void InjuryTwin_matches_historical_expression_per_channel()
    {
        // ApplyInjuries is private — pin the twin against the historical expression copied from its
        // body (T1-test-1 style): count × -max(1, atk/divisor) on combat.power.omni.
        const string key = "squad:0";
        const int count = 2;
        const long atk = 400;
        var divisor = ExpeditionTuningHub.Tuning.EventRoll.InjuryPowerDivisor;

        var expected = count * -Math.Max(1, atk / divisor);
        var twin = ExpeditionResolver.InjuryDerivedModifiers(key, count, atk);

        Assert.Equal(expected, twin.Sum(m => checked((long)m.Value)));
        Assert.All(twin, m => Assert.Equal(DerivedStatChannels.CombatPowerOmni, m.ChannelId));
        Assert.All(twin, m => Assert.Equal($"grant:injury:{key}", m.SourceId));
    }

    [Fact]
    public void DraughtSubsystem_contributes_through_hub_with_attributed_source_ids()
    {
        var manifest = new[]
        {
            new DraughtMod("draught:ember-flask", DerivedStatChannels.CombatPowerOmni, 25),
        };
        var hub = ActorHubBootstrap.CreateDefault(draughts: _ => manifest);
        var (snapshot, bag) = hub.ResolveDerivedWithContributions(
            new StatContextFactory().ForPlant("squad:0", new EntityBaseline { Atk = 400 }));

        Assert.Equal(25.0, snapshot.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
        var sources = bag.ContributionsFor(DerivedStatChannels.CombatPowerOmni).Select(c => c.SourceId).ToList();
        Assert.Contains("grant:draught:draught:ember-flask", sources);
    }

    [Fact]
    public void InjurySubsystem_contributes_through_hub_with_attributed_source_ids()
    {
        const long atk = 400;
        var divisor = ExpeditionTuningHub.Tuning.EventRoll.InjuryPowerDivisor;
        var injuries = new Dictionary<string, int>(StringComparer.Ordinal) { ["squad:0"] = 2 };

        var hub = ActorHubBootstrap.CreateDefault(expeditionInjuries: _ => injuries);
        var (snapshot, bag) = hub.ResolveDerivedWithContributions(
            new StatContextFactory().ForPlant("squad:0", new EntityBaseline { Atk = atk }));

        Assert.Equal(2 * -Math.Max(1, atk / divisor), snapshot.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
        var sources = bag.ContributionsFor(DerivedStatChannels.CombatPowerOmni).Select(c => c.SourceId).ToList();
        Assert.Contains("grant:injury:squad:0", sources);
    }
}
