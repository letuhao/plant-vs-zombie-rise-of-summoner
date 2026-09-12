using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Expeditions;
using FusionRpg.Core.Items.Consumables;
using FusionRpg.Core.Items.Consumables;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>
/// battle-hub-fuse T5 parity — <see cref="BattleHubCompose"/> (Hub) reaches the same channel
/// totals as <see cref="BattleStatComposer.Compose"/> on the same fixture, before the engine flips.
///
/// <para>Hub hygiene: this class joins <c>[Collection("AptitudeTuningHub")]</c> (the assembly's own
/// serialized collection for hub-mutating tests) and configures ONLY that hub, in the instance
/// ctor, from the same newest-file glob the collection mates use. Every other hub stays on the
/// assembly bootstrap's ambient values — reconfiguring them with file content here once flapped
/// a dozen unrelated battle/timeline tests by swapping their tuning mid-run.</para>
/// </summary>
[Collection("AptitudeTuningHub")]
public class BattleHubComposeParityTests
{
    public BattleHubComposeParityTests()
    {
        AptitudeTuningHub.Configure(AptitudeTuningLoader.Parse(File.ReadAllText(ShippedAptitudesPath())));
    }

    // Newest shipped aptitudes file — the same resolution GearedCornerTests uses, so collection
    // mates and this class never disagree on content.
    static string ShippedAptitudesPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var tuningDir = Path.Combine(dir.FullName, "data", "tuning");
            if (Directory.Exists(tuningDir))
            {
                var best = Directory.GetFiles(tuningDir, "aptitudes.v*.json")
                    .OrderBy(f => f, StringComparer.Ordinal).LastOrDefault();
                if (best != null) return best;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not locate data/tuning/aptitudes.v*.json above " + AppContext.BaseDirectory);
    }

    static BattleActorSetup BaseSetup() => new()
    {
        Key = "squad:0",
        Side = "squad",
        SpeciesId = "test-species",
        TypeId = 7,
        Level = 12,
        ElementPrimary = ElementTypeId.Fire,
        ElementSecondary = ElementTypeId.Ice,
        TraitIds = new[] { "critical-hunter" },
        MaxHp = BattleRuleset.BaseHp(12),
        Atk = BattleRuleset.BaseAtk(12),
        Defense = BattleRuleset.BaseDefense(12),
        AttackIntervalMs = 1500,
    };

    /// <summary>
    /// Fuse-adopted op divergences (T5 decision, T7 documents): <c>status.resist.{dot,cc,contagion}</c>
    /// are <c>SumIncreased</c> capped at <c>CategoryResistCap</c> (0.95) on the Hub path, while the old
    /// additive fold carried raw aptitude sums (73/30/43 here). Fuse adopts the capped Hub semantics
    /// once — goldens re-bless once at T6 — and this map pins the adopted values so no later change
    /// moves them silently. T7 removes the dead folds; it does not move values.
    /// </summary>
    static readonly IReadOnlyDictionary<string, double> ExpectedAdoptedHubValues =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["status.resist.dot"] = 0.95,
            ["status.resist.cc"] = 0.95,
            ["status.resist.contagion"] = 0.95,
        };

    /// <summary>
    /// The fuse delta matrix: every channel where old Compose and Hub disagree is enumerated, never
    /// silently absorbed. Two benign classes pass: (a) the documented battle-side Contest narrowing
    /// (bounded by half per funded edge — T7 owns composed equality); (b) Hub-extra channels equal
    /// to their registry default (pure defaults the old composer never seeded — reviewed below).
    /// Anything else fails with the full list. Genuine op-divergences (Hub compose kinds the old
    /// additive fold ignores, e.g. capped status resists) go in <c>ExpectedAdoptedHubValues</c> with
    /// their Hub value pinned — fuse adopts Hub semantics once, goldens re-bless once at T6.
    /// </summary>
    static List<string> FuseDiffs(
        ActorDerivedSnapshot oldSnap,
        ActorDerivedSnapshot newSnap,
        AptitudeAllocation? allocation,
        IReadOnlyDictionary<string, double>? expectedAdopted = null)
    {
        var diffs = new List<string>();
        var tuning = AptitudeTuningHub.Tuning;
        var funded = allocation is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : tuning.Edges
                .Where(e => allocation.Share(e.Source) > 0)
                .GroupBy(e => e.Channel)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var registry = DerivedStatRegistry.CreateDefault();

        foreach (var kv in oldSnap.Channels)
        {
            if (expectedAdopted is not null && expectedAdopted.TryGetValue(kv.Key, out var adopted))
            {
                if (newSnap.Get(kv.Key, 0) != adopted)
                    diffs.Add($"ADOPTED-DRIFT {kv.Key}: hub={newSnap.Get(kv.Key, 0)} pinned={adopted}");
                continue;
            }
            var tol = (funded.TryGetValue(kv.Key, out var n) ? n : 0) * 0.5 + 1e-9;
            var diff = Math.Abs(kv.Value - newSnap.Get(kv.Key, 0));
            if (diff > tol)
                diffs.Add($"MATCH-FAIL {kv.Key}: battle={kv.Value} hub={newSnap.Get(kv.Key, 0)} tol={tol}");
        }

        foreach (var kv in newSnap.Channels)
        {
            if (oldSnap.Channels.ContainsKey(kv.Key)) continue;
            if (expectedAdopted is not null && expectedAdopted.ContainsKey(kv.Key)) continue;
            registry.TryResolveChannel(kv.Key, out var def);
            var defValue = def?.DefaultValue ?? 0;
            if (kv.Value != defValue)
                diffs.Add($"HUB-EXTRA-NONDEFAULT {kv.Key}: hub={kv.Value} default={defValue}");
        }

        return diffs;
    }

    [Fact]
    public void Bare_setup_matches_compose_channel_for_channel()
    {
        var setup = BaseSetup();
        var diffs = FuseDiffs(BattleStatComposer.Compose(setup), BattleHubCompose.Compose(setup), null);
        Assert.True(diffs.Count == 0, string.Join("\n", diffs));
    }

    [Fact]
    public void Caller_ChannelMods_fold_matches_compose()
    {
        var setup = BaseSetup() with
        {
            ChannelMods = new[]
            {
                new BattleChannelMod(DerivedStatChannels.CombatPowerOmni, 37),
                new BattleChannelMod(DerivedStatChannels.CombatDefenseOmni, 11),
            }
        };
        var diffs = FuseDiffs(BattleStatComposer.Compose(setup), BattleHubCompose.Compose(setup), null);
        Assert.True(diffs.Count == 0, string.Join("\n", diffs));
    }

    [Fact]
    public void Unknown_channel_throws_on_both_paths()
    {
        var setup = BaseSetup() with
        {
            ChannelMods = new[] { new BattleChannelMod("nope.not.a.channel", 5) }
        };
        Assert.Throws<ArgumentException>(() => BattleStatComposer.Compose(setup));
        Assert.Throws<ArgumentException>(() => BattleHubCompose.Compose(setup));
    }

    [Fact]
    public void HubInputs_match_concatenated_producers()
    {
        // The T5 migration claim, pinned before the engine flips: every producer the Server
        // concats into ChannelMods today reaches identical totals as Hub inputs.
        const int level = 12;
        var ids = AptitudeCatalog.All.Take(2).Select(r => r.Id).ToArray();
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, ids[0], 100)
            + AptitudeAllocation.Single(AllocationScope.UniqueCreature, ids[1], 50);
        var starLoyalty = new StarLoyaltyContribution(Star: 3, Loyalty: 800, Level: level);
        var draughts = new[] { new DraughtMod("draught:ember-flask", DerivedStatChannels.CombatPowerFire, 25) };
        var injuries = new Dictionary<string, int>(StringComparer.Ordinal) { ["squad:0"] = 1 };
        var bound = new[]
        {
            new BoundDerivedAtom(DerivedStatChannels.CombatPowerFire, DerivedModifierOp.Flat, 40L, "equip:weapon:stock"),
        };

        var ladder = new PowerLadder(PowerTuningHub.Tuning);
        var registry = DerivedStatRegistry.CreateDefault();
        var aptitudeMods = AptitudeResolver.ResolveForBattle(allocation, AptitudeTuningHub.Tuning, ladder, level, registry);
        var starBonus = StarLoyaltyBonus.Star(3, level)!.Value;
        var loyaltyBonus = StarLoyaltyBonus.Loyalty(800, level)!.Value;
        var divisor = ExpeditionTuningHub.Tuning.EventRoll.InjuryPowerDivisor;
        var injuryEach = -Math.Max(1, BattleRuleset.BaseAtk(level) / divisor);

        var atk = BattleRuleset.BaseAtk(level);
        var oldSetup = BaseSetup() with
        {
            Atk = atk,
            ChannelMods = aptitudeMods
                .Concat(new[]
                {
                    new BattleChannelMod(DerivedStatChannels.CombatPowerOmni, starBonus.Power),
                    new BattleChannelMod(DerivedStatChannels.CombatDefenseOmni, starBonus.Defense),
                    new BattleChannelMod(DerivedStatChannels.CombatPowerOmni, loyaltyBonus.Power),
                    new BattleChannelMod(DerivedStatChannels.CombatDefenseOmni, loyaltyBonus.Defense),
                    new BattleChannelMod(DerivedStatChannels.CombatPowerFire, 25),
                    new BattleChannelMod(DerivedStatChannels.CombatPowerFire, 40),
                    new BattleChannelMod(DerivedStatChannels.CombatPowerOmni, injuryEach),
                }).ToList()
        };
        var newSetup = BaseSetup() with
        {
            Atk = atk,
            HubInputs = new BattleHubInputs
            {
                Aptitude = allocation,
                BoundAtoms = bound,
                StarLoyalty = starLoyalty,
                Draughts = draughts,
                Injuries = injuries,
            }
        };

        var diffs = FuseDiffs(BattleStatComposer.Compose(oldSetup), BattleHubCompose.Compose(newSetup), allocation, ExpectedAdoptedHubValues);
        Assert.True(diffs.Count == 0, string.Join("\n", diffs));
    }
}
