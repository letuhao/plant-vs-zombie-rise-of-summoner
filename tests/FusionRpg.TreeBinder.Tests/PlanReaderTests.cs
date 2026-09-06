using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.PassiveTree.State;
using FusionRpg.Tools.TreeBinder;
using Xunit;

namespace FusionRpg.TreeBinder.Tests;

/// <summary>Task D2 — `PlanReader` (tools/TreeBinder), the bridge from `tree-plan`'s committed JSON
/// shape to <see cref="BindInputNode"/>. `affixIds`/`exclusionForm`/`deliberateHole` are read if
/// present and defaulted otherwise, so this reader is forward-compatible with `tree-language`/
/// `tree-plan` the moment they start emitting those fields.</summary>
public class PlanReaderTests
{
    static PassiveTreeTuning Tuning(long treeShareMilli = 1000, long treeBudgetMilli = 1000) =>
        PassiveTreeTuningLoader.Parse($$"""
        {
          "schemaVersion": 1, "version": 1,
          "tierLadder": { "reqScalePoints": 5 },
          "budget": { "treeTotalPoints": 1000, "branchSplitMilli": 500 },
          "treeShareMilli": {{treeShareMilli}}, "treeBudgetMilli": {{treeBudgetMilli}},
          "potency": { "maxNodeShareMilli": 182, "minTerminalWidth": 1, "bandEdgesMilli": [46,91,137,182] },
          "mechanism": { "rampStartMilli": 0, "rampEndMilli": 1000 },
          "archetype": { "rewardSpreadMaxRatioMilli": 6000 },
          "exclusion": { "targetShareMilli": 20 },
          "archetypeAssignment": "ordinal-round-robin",
          "designTarget": { "thetaAllIn": 92 },
          "concentration": { "fmaxMilli": 1200, "wMilli": 500 },
          "soulTrack": { "thetaPerSoulLevelMilli": 1000 },
          "unlockCost": { "firstPoints": 5, "stepPoints": 2 },
          "respec": { "basePrice": 50, "escalationPermille": 500 },
          "gateCounters": { "masteryCurveFirstCount": 23, "masteryCurveStepCount": 23,
            "elementMasteryRatePoints": 4, "statusMasteryRatePoints": 4, "flushIntervalMs": 5000 }
        }
        """);

    [Fact]
    public void A_plain_plan_node_with_no_upstream_fields_defaults_to_empty_affixIds_and_None()
    {
        var json = """{"nodes":[{"id":"skill.might-off-t5-n0","budgetShareMilli":45}]}""";

        var nodes = PlanReader.ReadPlanNodes(json, Tuning());

        var node = Assert.Single(nodes);
        Assert.Equal("skill.might-off-t5-n0", node.NodeId);
        Assert.Equal(45L, node.BudgetShareMilli);
        Assert.Equal(1000L, node.TreeShareMilli);
        Assert.Equal(1000L, node.TreeBudgetMilli);
        Assert.Equal(PlanReader.Branches, node.Branches);
        Assert.Empty(node.AffixIds);
        Assert.Equal(ExclusionForm.None, node.ExclusionForm);
        Assert.False(node.DeliberateHole);
    }

    [Fact]
    public void AffixIds_exclusionForm_and_deliberateHole_are_read_when_present()
    {
        var json = """
        {"nodes":[{"id":"skill.might-off-t7-b3","budgetShareMilli":64,
                   "affixIds":["affix.a","affix.b"],
                   "exclusionForm":"Nullification","deliberateHole":true}]}
        """;

        var node = Assert.Single(PlanReader.ReadPlanNodes(json, Tuning()));

        Assert.Equal(new[] { "affix.a", "affix.b" }, node.AffixIds);
        Assert.Equal(ExclusionForm.Nullification, node.ExclusionForm);
        Assert.True(node.DeliberateHole);
    }

    [Fact]
    public void TreeShareMilli_and_treeBudgetMilli_come_from_the_tuning_file_not_the_plan()
    {
        var json = """{"nodes":[{"id":"skill.x-off-t1-n0","budgetShareMilli":9}]}""";

        var node = Assert.Single(PlanReader.ReadPlanNodes(json, Tuning(treeShareMilli: 700, treeBudgetMilli: 300)));

        Assert.Equal(700L, node.TreeShareMilli);
        Assert.Equal(300L, node.TreeBudgetMilli);
    }

    [Fact]
    public void A_plan_with_no_nodes_array_yields_an_empty_list()
    {
        Assert.Empty(PlanReader.ReadPlanNodes("""{"treeId":"empty"}""", Tuning()));
    }

    [Fact]
    public void Branches_is_the_structural_constant_2_D29()
    {
        Assert.Equal(2L, PlanReader.Branches);
    }
}
