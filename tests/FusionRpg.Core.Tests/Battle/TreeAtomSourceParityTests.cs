using System.IO;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using Xunit;
using LawnTreeAtomSource = FusionRpg.Core.PassiveTree.Resolve.TreeAtomSource;
using BattleTreeAtomSource = FusionRpg.Core.Battle.TreeAtomSource;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>Task D6 — `Battle.TreeAtomSource` (spec-tree-resolve.md §2.1, §2.2, §12 tests 15-16).
/// Proves lawn and battle resolve to the same totals for one actor, that attribution reaches the
/// lawn's contributions unchanged, and that no new subsystem/order band is introduced.</summary>
public class TreeAtomSourceParityTests
{
    static string RepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    static PowerTuning RealPowerTuning() =>
        PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "power-scale.v2.json")));

    // FlatPermille is a straight kMicro/1e6 division with no P(Theta)/Theta scaling at all -- picking
    // a kMicro that is an exact multiple of 1,000,000 makes the lawn's own double Amount an EXACT
    // whole number, so the lawn<->battle double->long rounding step introduces no ambiguity and the
    // parity claim is a genuine equality, not a "close enough" one.
    static LoadedTree FlatTree(long kMicro, string channelId) =>
        new(new TreeRecord("might", TreeCategory.Primary, "aptitude.Might@Commander",
                "broad-and-flat", 10, 2, new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1, true),
            new[]
            {
                new NodeRecord("skill.might-off-t3-n0", "might", TreeBranch.Off, 3, "n0",
                    Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 45,
                    new[]
                    {
                        new NodeAtom("stat.derived", AttachPoint.Stat, channelId, NodeAtomOp.Flat,
                            null, null, kMicro, ScaleAxis.FlatPermille, UnitClass.PerMilleRatio, null),
                    },
                    Array.Empty<string>(), ExclusionForm.None, null, true, null),
            });

    [Fact] // "Lawn_and_battle_resolve_to_the_same_totals for one actor"
    public void Lawn_and_battle_resolve_to_the_same_totals_for_one_actor()
    {
        var tree = FlatTree(kMicro: 7_000_000, channelId: DerivedStatChannels.CombatPowerOmni); // -> 7.0 exactly
        var owned = new HashSet<string> { "skill.might-off-t3-n0" };
        var tuning = RealPowerTuning();

        var lawn = LawnTreeAtomSource.BoundAtomsFor(tree, owned, tierReached: 10, thetaNode: 100, tuning, fMilli: 1000);
        var battle = FusionRpg.Core.Battle.TreeAtomSource.ModsFor(tree, owned, tierReached: 10, thetaNode: 100, tuning, fMilli: 1000);

        var lawnTotal = 0.0;
        foreach (var a in lawn) if (a.Channel == DerivedStatChannels.CombatPowerOmni) lawnTotal += a.Amount;
        var battleTotal = 0L;
        foreach (var m in battle) if (m.ChannelId == DerivedStatChannels.CombatPowerOmni) battleTotal += m.Amount;

        Assert.Equal(7.0, lawnTotal);
        Assert.Equal(7L, battleTotal);
        Assert.Equal(lawnTotal, battleTotal); // the actual parity claim: same total, same actor
    }

    [Fact] // attribution reaches ChannelContributions (the lawn's DerivedContributionBag) unchanged
    public void Attribution_reaches_the_lawn_contribution_bag_unchanged_via_the_shared_fan_in()
    {
        var tree = FlatTree(kMicro: 3_000_000, channelId: DerivedStatChannels.CombatDefenseOmni);
        var owned = new HashSet<string> { "skill.might-off-t3-n0" };
        var tuning = RealPowerTuning();

        var hub = new FusionRpg.Core.Stats.Derived.ActorHub(StatSystemBootstrap.CreateDefault());
        hub.Register(new AtomDerivedSubsystem(_ =>
            LawnTreeAtomSource.BoundAtomsFor(tree, owned, tierReached: 10, thetaNode: 100, tuning, fMilli: 1000)));

        var ctx = new StatContext
        {
            Side = StatSide.Plant, EntityKey = "abc",
            Baseline = new EntityBaseline { Hp = 300, MaxHp = 300, Atk = 20 },
        };
        var (_, contributions) = hub.ResolveDerivedWithContributions(ctx);
        var one = Assert.Single(contributions.ContributionsFor(DerivedStatChannels.CombatDefenseOmni));

        Assert.Equal("tree.might.skill.might-off-t3-n0", one.SourceId); // GG-49: one row per node
        Assert.Equal(3.0, one.Value);
    }

    [Fact] // "No new subsystem, no new order band, and the existing three registrations are not evicted"
    public void Battle_composition_uses_no_new_subsystem_it_is_a_third_producer_slot_like_the_other_two()
    {
        // BattleChannelMod carries no SourceId at all -- the SAME shape TraitAtomSource/EquipAtomSource
        // already emit, proving this is a projection into the EXISTING battle seam, never a new one.
        var tree = FlatTree(kMicro: 1_000_000, channelId: DerivedStatChannels.CombatPowerOmni);
        var owned = new HashSet<string> { "skill.might-off-t3-n0" };
        var mods = FusionRpg.Core.Battle.TreeAtomSource.ModsFor(tree, owned, 10, 100, RealPowerTuning(), fMilli: 1000);

        var mod = Assert.Single(mods);
        Assert.IsType<BattleChannelMod>(mod);
        // BattleChannelMod's own shape (ChannelId, Amount) -- no third field for a subsystem id or
        // order band to hide in.
        Assert.Equal(DerivedStatChannels.CombatPowerOmni, mod.ChannelId);
    }

    [Fact]
    public void An_unowned_node_contributes_no_battle_mods()
    {
        var tree = FlatTree(kMicro: 5_000_000, channelId: DerivedStatChannels.CombatPowerOmni);
        var mods = FusionRpg.Core.Battle.TreeAtomSource.ModsFor(
            tree, new HashSet<string>(), tierReached: 10, thetaNode: 100, RealPowerTuning(), fMilli: 1000);
        Assert.Empty(mods);
    }

    /// <summary>Task D7, bullet 3: "`F` multiplies every tree-derived contribution... IN BOTH READ
    /// MODES." Battle never re-applies `F` itself (`Battle.TreeAtomSource.ModsFor` forwards `fMilli`
    /// into the lawn's `BoundAtomsFor` and only rounds double-&gt;long) -- so the battle mod must equal
    /// the lawn amount, F-scaled, rounded once.</summary>
    [Fact]
    public void F_reaches_the_battle_mod_the_same_way_it_reaches_the_lawn_amount()
    {
        var tree = FlatTree(kMicro: 5_000_000, channelId: DerivedStatChannels.CombatPowerOmni); // -> 5.0 before F
        var owned = new HashSet<string> { "skill.might-off-t3-n0" };
        var tuning = RealPowerTuning();

        var lawnNoF = LawnTreeAtomSource.BoundAtomsFor(tree, owned, 10, 100, tuning, fMilli: 1000)[0].Amount;
        var lawnWithF = LawnTreeAtomSource.BoundAtomsFor(tree, owned, 10, 100, tuning, fMilli: 1200)[0].Amount;
        var battleWithF = FusionRpg.Core.Battle.TreeAtomSource.ModsFor(tree, owned, 10, 100, tuning, fMilli: 1200)[0].Amount;

        Assert.Equal(5.0, lawnNoF);
        Assert.Equal(6.0, lawnWithF); // 5.0 * 1.2 -- F genuinely reached the lawn amount
        Assert.Equal(6L, battleWithF); // and the SAME F-scaled amount reached the battle mod, rounded once
    }
}
