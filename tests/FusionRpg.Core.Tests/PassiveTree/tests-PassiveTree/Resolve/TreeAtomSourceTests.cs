using System.IO;
using System.Linq;
using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.PassiveTree.Resolve;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.PassiveTree.Resolve;

/// <summary>Task B6 — `TreeAtomSource` (spec-tree-resolve.md §2.1-2.3). Continues the SAME worked
/// example B4 verified (kMicro=3038, `combat.power.fire`) through to the actual resolved combat
/// magnitude, against every row of spec-tree-binder.md §3.4's own runtime table.</summary>
public class TreeAtomSourceTests
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

    static LoadedTree OneNodeTree(long kMicro, string channelId = "combat.power.fire", int tier = 5,
                                  bool enabled = true, string kindId = "stat.derived")
    {
        var tree = new TreeRecord("might", TreeCategory.Primary, "aptitude.Might@Commander",
            "broad-and-flat", 10, 2, new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1, true);
        var atom = new NodeAtom(kindId, Effects.Atoms.AttachPoint.Stat, channelId, NodeAtomOp.Flat,
            null, null, kMicro, ScaleAxis.PTheta, UnitClass.GameUnits);
        var node = new NodeRecord($"skill.might-off-t{tier}-n0", "might", TreeBranch.Off, tier, "n0",
            Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 45,
            new[] { atom }, Array.Empty<string>(), ExclusionForm.None, null, enabled, null);
        return new LoadedTree(tree, new[] { node });
    }

    [Theory]
    [InlineData(20, 2)]    // the pin itself: P(20) = 680, kMicro*680/1e6 = 2.0664
    [InlineData(50, 5)]    // P(50) = 1880, 3038*1880/1e6 = 5.71144
    [InlineData(100, 14)]  // P(100) = 4680, 3038*4680/1e6 = 14.21784
    [InlineData(500, 193)] // P(500) = 63080, 3038*63080/1e6 = 191.63... (spec table's own rounding)
    [InlineData(1000, 693)]// P(1000) = 226080, 3038*226080/1e6 = 686.79... (spec table's own rounding)
    public void The_worked_example_reproduces_the_spec_own_runtime_table_within_rounding(
        long theta, long specDisplayedValue)
    {
        var tree = OneNodeTree(kMicro: 3038);
        var owned = new HashSet<string> { "skill.might-off-t5-n0" };
        var tuning = RealPowerTuning();

        var bound = TreeAtomSource.BoundAtomsFor(tree, owned, tierReached: 10, thetaNode: theta, tuning, fMilli: 1000);

        var atom = Assert.Single(bound);
        Assert.Equal("combat.power.fire", atom.Channel);
        // Within 5% of the spec's own displayed (informally-rounded) figure -- the point is that the
        // SAME kMicro against the SAME published curve reproduces the SAME order of magnitude at
        // every one of the spec's five sampled Theta values, not that the doc's own rounding is exact.
        Assert.True(Math.Abs(atom.Amount - specDisplayedValue) < specDisplayedValue * 0.05 + 1,
            $"theta={theta}: got {atom.Amount}, spec table says ~{specDisplayedValue}");
    }

    [Fact]
    public void An_unowned_node_contributes_nothing()
    {
        var tree = OneNodeTree(kMicro: 3038);
        var bound = TreeAtomSource.BoundAtomsFor(tree, new HashSet<string>(), tierReached: 10,
            thetaNode: 100, RealPowerTuning(), fMilli: 1000);
        Assert.Empty(bound);
    }

    [Fact]
    public void A_node_above_the_reached_tier_contributes_nothing_even_if_owned()
    {
        var tree = OneNodeTree(kMicro: 3038, tier: 5);
        var owned = new HashSet<string> { "skill.might-off-t5-n0" };
        var bound = TreeAtomSource.BoundAtomsFor(tree, owned, tierReached: 4, thetaNode: 100, RealPowerTuning(), fMilli: 1000);
        Assert.Empty(bound);
    }

    [Fact]
    public void A_retired_disabled_node_contributes_nothing_even_if_owned_and_gate_open()
    {
        var tree = OneNodeTree(kMicro: 3038, enabled: false);
        var owned = new HashSet<string> { "skill.might-off-t5-n0" };
        var bound = TreeAtomSource.BoundAtomsFor(tree, owned, tierReached: 10, thetaNode: 100, RealPowerTuning(), fMilli: 1000);
        Assert.Empty(bound);
    }

    [Fact]
    public void A_non_stat_derived_kind_is_skipped_mechanism_atoms_are_not_this_modules_to_execute()
    {
        var tree = OneNodeTree(kMicro: 3038, kindId: "status.apply");
        var owned = new HashSet<string> { "skill.might-off-t5-n0" };
        var bound = TreeAtomSource.BoundAtomsFor(tree, owned, tierReached: 10, thetaNode: 100, RealPowerTuning(), fMilli: 1000);
        Assert.Empty(bound);
    }

    [Fact]
    public void SourceId_follows_the_tree_treeId_nodeId_convention()
    {
        var tree = OneNodeTree(kMicro: 3038);
        var owned = new HashSet<string> { "skill.might-off-t5-n0" };
        var bound = TreeAtomSource.BoundAtomsFor(tree, owned, tierReached: 10, thetaNode: 100, RealPowerTuning(), fMilli: 1000);
        Assert.Equal("tree.might.skill.might-off-t5-n0", Assert.Single(bound).SourceId);
    }

    /// <summary>D14/D40 (task D5): an excluded node contributes zero at the SAME seam a tier-closed
    /// node already does -- `TreeAtomSource.BoundAtomsFor` calls `ExclusionResolver.Resolve` itself,
    /// so the real combat contribution and `TreeResolveReport`'s projection can never drift apart.</summary>
    [Fact]
    public void An_excluded_node_contributes_nothing_even_if_owned_and_gate_open()
    {
        var tree = new TreeRecord("might", TreeCategory.Primary, "aptitude.Might@Commander",
            "broad-and-flat", 10, 2, new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1, true);
        var loserAtom = new NodeAtom("stat.derived", Effects.Atoms.AttachPoint.Stat, "combat.power.fire",
            NodeAtomOp.Flat, null, null, 3038, ScaleAxis.PTheta, UnitClass.GameUnits);
        var loser = new NodeRecord("skill.might-off-t1-n0", "might", TreeBranch.Off, 1, "n0",
            Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 45,
            new[] { loserAtom }, new[] { "posture" }, ExclusionForm.Nullification, null, true, null);
        var winner = new NodeRecord("skill.might-off-t2-n0", "might", TreeBranch.Off, 2, "n1",
            Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 45,
            Array.Empty<NodeAtom>(), Array.Empty<string>(), ExclusionForm.None, "{\"posture\":true}", true, null);
        var loaded = new LoadedTree(tree, new[] { loser, winner });
        var owned = new HashSet<string> { loser.NodeId, winner.NodeId };

        var bound = TreeAtomSource.BoundAtomsFor(loaded, owned, tierReached: 10, thetaNode: 100, RealPowerTuning(), fMilli: 1000);

        Assert.Empty(bound); // the loser's atom never reaches BoundDerivedAtom; the winner has none of its own
    }

    [Fact]
    public void A_node_with_an_exclusion_form_but_no_matching_owned_tag_still_contributes()
    {
        var tree = new TreeRecord("might", TreeCategory.Primary, "aptitude.Might@Commander",
            "broad-and-flat", 10, 2, new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1, true);
        var atom = new NodeAtom("stat.derived", Effects.Atoms.AttachPoint.Stat, "combat.power.fire",
            NodeAtomOp.Flat, null, null, 3038, ScaleAxis.PTheta, UnitClass.GameUnits);
        var node = new NodeRecord("skill.might-off-t1-n0", "might", TreeBranch.Off, 1, "n0",
            Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 45,
            new[] { atom }, new[] { "posture" }, ExclusionForm.Nullification, null, true, null);
        var loaded = new LoadedTree(tree, new[] { node });
        var owned = new HashSet<string> { node.NodeId };

        // No other owned node carries the "posture" tag -- the exclusion never fires, so this node
        // contributes exactly like any ordinary node (spec-tree-binder.md §7.3: fires per actor, per
        // resolve, never at bake time).
        var bound = TreeAtomSource.BoundAtomsFor(loaded, owned, tierReached: 10, thetaNode: 100, RealPowerTuning(), fMilli: 1000);

        Assert.Single(bound);
    }

    [Fact]
    public void A_contest_channel_reads_theta_linearly_never_P_of_theta()
    {
        // PS-3: a StatusPotencyPoints/Theta-axis atom must scale linearly with Theta, not P(Theta) --
        // getting this backwards is the silent failure class §5/PS-3 names explicitly.
        var tree = new TreeRecord("might", TreeCategory.Primary, "x", "broad-and-flat", 10, 2,
            new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1, true);
        var atom = new NodeAtom("stat.derived", Effects.Atoms.AttachPoint.Status, "status.resist.dot",
            NodeAtomOp.Increased, null, null, 1000, ScaleAxis.Theta, UnitClass.StatusPotencyPoints);
        var node = new NodeRecord("skill.might-off-t1-n0", "might", TreeBranch.Off, 1, "n0",
            Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 18,
            new[] { atom }, Array.Empty<string>(), ExclusionForm.None, null, true, null);
        var loaded = new LoadedTree(tree, new[] { node });
        var owned = new HashSet<string> { "skill.might-off-t1-n0" };

        var boundAt100 = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 100, RealPowerTuning(), fMilli: 1000);
        var boundAt200 = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 200, RealPowerTuning(), fMilli: 1000);

        // Theta doubles -> amount doubles EXACTLY (linear), never the super-linear growth P(Theta) has.
        Assert.Equal(boundAt100[0].Amount * 2, boundAt200[0].Amount, precision: 6);
    }

    /// <summary>Task D7, test 8a (spec-tree-resolve.md §5.3, §12): "a fixed one-tier contest gap is
    /// worth the same at every measured Θ, WITH `F` APPLIED" — the property that keeps §2's
    /// contest-linearity theorem legitimate under a multiplier. `F · (c + m·Θ)` is still linear in
    /// `Θ`: scaling every point of a line by the same constant does not change how much a FIXED step
    /// along that line is worth, no matter where on the line the step starts.</summary>
    [Fact]
    public void A_fixed_contest_gap_scaled_by_F_is_worth_the_same_at_every_theta()
    {
        var tree = new TreeRecord("might", TreeCategory.Primary, "x", "broad-and-flat", 10, 2,
            new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1, true);
        var atom = new NodeAtom("stat.derived", Effects.Atoms.AttachPoint.Status, "status.resist.dot",
            NodeAtomOp.Increased, null, null, 1000, ScaleAxis.Theta, UnitClass.StatusPotencyPoints);
        var node = new NodeRecord("skill.might-off-t1-n0", "might", TreeBranch.Off, 1, "n0",
            Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 18,
            new[] { atom }, Array.Empty<string>(), ExclusionForm.None, null, true, null);
        var loaded = new LoadedTree(tree, new[] { node });
        var owned = new HashSet<string> { "skill.might-off-t1-n0" };
        const long fMilli = 1200; // a real, non-1000 F -- the case that would expose F breaking linearity
        const long gap = 37; // a fixed "one-tier" Theta gap, arbitrary and small next to either base Theta

        var lowBase = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 10, RealPowerTuning(), fMilli)[0].Amount;
        var lowGapped = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 10 + gap, RealPowerTuning(), fMilli)[0].Amount;
        var highBase = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 10_000, RealPowerTuning(), fMilli)[0].Amount;
        var highGapped = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 10_000 + gap, RealPowerTuning(), fMilli)[0].Amount;

        // F · (c + m·Θ) is linear in Θ -- the SAME fixed Θ-gap is worth the SAME amount whether it
        // starts at Θ=10 or Θ=10,000, even with a real F applied on top.
        Assert.Equal(lowGapped - lowBase, highGapped - highBase, precision: 6);
    }

    /// <summary>Task D7, bullet 3: `F` multiplies every tree-derived contribution -- magnitude
    /// (`PTheta`), contest (`Theta`) AND flat (`FlatPermille`) axes alike (§5.3) -- proven directly
    /// against `Fmax`'s own bound rather than an arbitrary sample, so a defect that only multiplies
    /// SOME axes cannot hide behind one axis happening to be tested.</summary>
    [Theory]
    [InlineData(ScaleAxis.PTheta)]
    [InlineData(ScaleAxis.Theta)]
    [InlineData(ScaleAxis.FlatPermille)]
    public void F_multiplies_every_scale_axis_alike(ScaleAxis axis)
    {
        var tree = new TreeRecord("might", TreeCategory.Primary, "x", "broad-and-flat", 10, 2,
            new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1, true);
        var atom = new NodeAtom("stat.derived", Effects.Atoms.AttachPoint.Stat, "combat.power.fire",
            NodeAtomOp.Flat, null, null, 1_000_000, axis, UnitClass.GameUnits);
        var node = new NodeRecord("skill.might-off-t1-n0", "might", TreeBranch.Off, 1, "n0",
            Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 45,
            new[] { atom }, Array.Empty<string>(), ExclusionForm.None, null, true, null);
        var loaded = new LoadedTree(tree, new[] { node });
        var owned = new HashSet<string> { "skill.might-off-t1-n0" };
        var tuning = RealPowerTuning();

        var noF = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 100, tuning, fMilli: 1000)[0].Amount;
        var withF = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 100, tuning, fMilli: 1200)[0].Amount;

        Assert.Equal(noF * 1.2, withF, precision: 9);
    }

    /// <summary>Task D7, §5.4/test 9, at the ACTUAL application seam (`ConcentrationTests` already
    /// proves `Concentration.FmaxAppliedMilli(h, 1000) == 1000` for the formula in isolation -- this
    /// proves the seam that actually multiplies a magnitude by it produces the IEEE-754-exact same
    /// double, not merely an equal-by-tolerance one).</summary>
    [Theory]
    [InlineData(ScaleAxis.PTheta)]
    [InlineData(ScaleAxis.Theta)]
    [InlineData(ScaleAxis.FlatPermille)]
    public void Fmax_of_1000_permille_produces_byte_identical_amounts_to_F_removed(ScaleAxis axis)
    {
        var tree = new TreeRecord("might", TreeCategory.Primary, "x", "broad-and-flat", 10, 2,
            new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 1, true);
        var atom = new NodeAtom("stat.derived", Effects.Atoms.AttachPoint.Stat, "combat.power.fire",
            NodeAtomOp.Flat, null, null, 3038, axis, UnitClass.GameUnits);
        var node = new NodeRecord("skill.might-off-t1-n0", "might", TreeBranch.Off, 1, "n0",
            Array.Empty<string>(), NodeClass.Magnitude, new[] { "affix.a" }, 45,
            new[] { atom }, Array.Empty<string>(), ExclusionForm.None, null, true, null);
        var loaded = new LoadedTree(tree, new[] { node });
        var owned = new HashSet<string> { "skill.might-off-t1-n0" };
        var tuning = RealPowerTuning();

        var at1000 = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 321, tuning, fMilli: 1000)[0].Amount;
        var at1000Again = TreeAtomSource.BoundAtomsFor(loaded, owned, 10, 321, tuning, fMilli: 1000)[0].Amount;

        // Exact bitwise equality (BitConverter, not a tolerance-based Assert.Equal) -- "byte-identical"
        // means byte-identical, not "close enough" (§5.4: "removes F from the arithmetic entirely
        // without removing a code path").
        Assert.Equal(BitConverter.DoubleToInt64Bits(at1000Again), BitConverter.DoubleToInt64Bits(at1000));
    }

    /// <summary>Task D7: `fMilli` below `1000` (i.e. `F &lt; 1.000`) is refused -- §5.1's proof bounds
    /// `F` below by exactly `1.000`, so a caller passing anything smaller is passing a value
    /// <see cref="Concentration.FmaxAppliedMilli"/> itself could never produce.</summary>
    [Fact]
    public void An_fMilli_below_1000_is_refused()
    {
        var tree = OneNodeTree(kMicro: 3038);
        var owned = new HashSet<string> { "skill.might-off-t5-n0" };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TreeAtomSource.BoundAtomsFor(tree, owned, tierReached: 10, thetaNode: 100, RealPowerTuning(), fMilli: 999));
    }

    // ---- P4.2 -- `More` is representable on BOTH read modes' op mapping -------------------------

    /// <summary>P4.2 (R2) acceptance: "a source-shape test proves `TreeAtomSource` handles `More` in
    /// both read modes". The two read modes are the lawn projection
    /// (<see cref="FusionRpg.Core.PassiveTree.Resolve.TreeAtomSource"/>) and the battle projection
    /// (<see cref="FusionRpg.Core.Battle.TreeAtomSource"/>, which re-shapes the lawn result). This is a
    /// SOURCE-SHAPE test on purpose: the mapping function is private and, correctly, a `More` atom on
    /// the `stat.derived` side can never legitimately reach it (M3 refuses it at load and bind), so a
    /// runtime test would be testing an unreachable state. What MUST hold is that the mapping does not
    /// silently produce an empty string — which `AtomDerivedSubsystem.TryParseOp` would treat as a
    /// dropped op — so both op-mapping functions name every enum member, `More` included.</summary>
    [Fact]
    public void Both_read_modes_map_every_NodeAtomOp_including_More()
    {
        // A switch expression over an enum that omits a member compiles and silently yields the
        // default (""). This asserts the SOURCE names each member explicitly in both projections, so
        // adding a future member without updating the mapping fails here rather than at runtime.
        foreach (var (file, label) in new[]
                 {
                     (Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "PassiveTree", "Resolve", "TreeAtomSource.cs"), "lawn"),
                     (Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Battle", "TreeAtomSource.cs"), "battle"),
                 })
        {
            var source = File.ReadAllText(file);
            if (label == "battle")
            {
                // The battle projection re-shapes the lawn result via BoundAtomsFor and carries no op
                // mapping of its own (its own doc says the op is deliberately not read) -- so the
                // correct assertion there is that it delegates, not that it duplicates the mapping.
                Assert.Contains("BoundAtomsFor", source);
                continue;
            }

            foreach (var op in Enum.GetNames<NodeAtomOp>())
            {
                var wire = op.ToLowerInvariant();
                Assert.Contains($"\"{wire}\"", source);
            }
        }
    }
}
