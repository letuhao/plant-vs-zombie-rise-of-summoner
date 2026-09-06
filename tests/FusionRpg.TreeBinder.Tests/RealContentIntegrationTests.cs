using System.IO;
using System.Linq;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.PassiveTree.Binding;
using FusionRpg.Core.PassiveTree.State;
using FusionRpg.Core.Power;
using FusionRpg.Tools.TreeBinder;
using Xunit;

namespace FusionRpg.TreeBinder.Tests;

/// <summary>
/// Task D2 — end-to-end wiring against the REAL committed content (`data/tuning/power-scale.v2.json`,
/// `data/tuning/passive-tree.v1.json`, `data/seed/passive-tree/plan/might.v1.json`), the same files
/// `tools/TreeBinder`'s `Program.cs` reads. `tree-language` has not shipped yet, so the real plan's
/// nodes carry no `affixIds` today -- this test pins that HONEST state (every node refused, verdict
/// `Fail`) rather than fabricating content that does not exist upstream. The moment `tree-language`
/// starts writing `affixIds` into the plan, this test's own assertions will need updating -- which is
/// the point: it will fail LOUDLY, not silently pass on stale expectations.
/// </summary>
public class RealContentIntegrationTests
{
    static string RepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    [Fact]
    public void Real_might_plan_reads_40_nodes_with_todays_honest_all_refused_state()
    {
        var root = RepoRoot();
        var treeTuning = PassiveTreeTuningLoader.Parse(
            File.ReadAllText(Path.Combine(root, "data", "tuning", "passive-tree.v1.json")));
        var powerTuning = PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(root, "data", "tuning", "power-scale.v2.json")));

        var planJson = File.ReadAllText(Path.Combine(root, "data", "seed", "passive-tree", "plan", "might.v1.json"));
        var nodes = PlanReader.ReadPlanNodes(planJson, treeTuning);

        Assert.Equal(40, nodes.Count);
        Assert.Equal(1000L, nodes[0].TreeShareMilli); // data/tuning/passive-tree.v1.json's real value
        Assert.Equal(1000L, nodes[0].TreeBudgetMilli);
        Assert.All(nodes, n => Assert.Empty(n.AffixIds)); // tree-language has not shipped yet

        var report = TreeBinderRun.BindTree(nodes,
            new Dictionary<string, AffixRow>(), new Dictionary<string, AtomRow>(), powerTuning);

        Assert.Empty(report.Bound);
        Assert.Equal(40, report.Refused.Count);
        Assert.Equal(RunVerdict.Fail, report.Verdict);
        Assert.All(report.Refused, r => Assert.Contains("affixIds must be 1..3, got 0", r.Reason));

        // The unspent total is the tree's own full budget column -- nothing was priced.
        var expectedTotal = nodes.Sum(n => n.BudgetShareMilli);
        Assert.Equal(expectedTotal, report.TotalUnspentBudgetShareMilli);
    }

    [Fact]
    public void ReportWriter_serializes_the_real_run_and_reparses_cleanly()
    {
        var root = RepoRoot();
        var treeTuning = PassiveTreeTuningLoader.Parse(
            File.ReadAllText(Path.Combine(root, "data", "tuning", "passive-tree.v1.json")));
        var powerTuning = PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(root, "data", "tuning", "power-scale.v2.json")));
        var planJson = File.ReadAllText(Path.Combine(root, "data", "seed", "passive-tree", "plan", "might.v1.json"));
        var nodes = PlanReader.ReadPlanNodes(planJson, treeTuning);

        var report = TreeBinderRun.BindTree(nodes,
            new Dictionary<string, AffixRow>(), new Dictionary<string, AtomRow>(), powerTuning);
        var json = ReportWriter.Serialize("might", report);

        using var doc = System.Text.Json.JsonDocument.Parse(json); // never throws
        Assert.Equal("Fail", doc.RootElement.GetProperty("verdict").GetString());
    }
}
