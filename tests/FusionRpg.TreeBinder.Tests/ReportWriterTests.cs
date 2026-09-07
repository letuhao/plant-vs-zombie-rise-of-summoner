using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.PassiveTree.Binding;
using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.PassiveTree.State;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Tools.TreeBinder;
using Xunit;

namespace FusionRpg.TreeBinder.Tests;

/// <summary>Task D2 (and its 2026-09-07 real-corpus fix) — `ReportWriter` (tools/TreeBinder), the
/// binder's own committed run output (`data/generated/passive-tree/&lt;treeId&gt;.json`). Proves the
/// shape is BOTH the original audit trail (verdict, total unspent budget, bound atoms, refused slots)
/// AND the real `tree-catalog` `TreeRecord`/`NodeRecord` shape `PassiveTreeCatalogLoader` actually
/// reads — the gap a real live-boot proof found: nothing had ever written a catalog document this
/// loader could accept, only test fixtures ever hand-built one.</summary>
public class ReportWriterTests
{
    static TreeCatalogMeta Meta(string archetype = "broad-and-flat") =>
        new("primary", "aptitude.Might@Commander", archetype, 10, 2, new[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 }, 1);

    static BindInputNode Input(string nodeId, TreeBranch branch = TreeBranch.Off, int tier = 5,
        string nodeKey = "n0", NodeClass nodeClass = NodeClass.Magnitude,
        IReadOnlyList<string>? affixIds = null, ExclusionForm exclusionForm = ExclusionForm.None,
        IReadOnlyList<string>? excludeProps = null) =>
        new(nodeId, 1000, 500, 9, 2, affixIds ?? new[] { "atom.might" }, exclusionForm,
            DeliberateHole: false, branch, tier, nodeKey, nodeClass, excludeProps);

    [Fact]
    public void Serialize_carries_verdict_bound_and_refused_with_unspent_budget()
    {
        var bound = new BoundNode("skill.might-off-t5-n0", new[]
        {
            new NodeAtom("stat.derived", AttachPoint.Stat, "combat.power.fire", NodeAtomOp.Flat,
                null, null, 3038L, ScaleAxis.PTheta, UnitClass.GameUnits),
        });
        var refused = new RefusedSlot("skill.might-off-t7-b3", "18th atom kind (D16)", 64L, DeliberateHole: false);
        var report = BinderRunReport.From(new[] { bound }, new[] { refused });
        var input = Input("skill.might-off-t5-n0");

        var json = ReportWriter.Serialize("might", Meta(), new[] { input }, report);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("might", root.GetProperty("treeId").GetString());
        Assert.Equal("Fail", root.GetProperty("verdict").GetString());
        Assert.Equal(64L, root.GetProperty("totalUnspentBudgetShareMilli").GetInt64());

        var boundEl = Assert.Single(root.GetProperty("bound").EnumerateArray());
        Assert.Equal("skill.might-off-t5-n0", boundEl.GetProperty("nodeId").GetString());
        var atomEl = Assert.Single(boundEl.GetProperty("atoms").EnumerateArray());
        Assert.Equal(3038L, atomEl.GetProperty("kMicro").GetInt64());
        Assert.Equal("combat.power.fire", atomEl.GetProperty("channelId").GetString());

        var refusedEl = Assert.Single(root.GetProperty("refused").EnumerateArray());
        Assert.Equal("skill.might-off-t7-b3", refusedEl.GetProperty("nodeId").GetString());
        Assert.Equal(64L, refusedEl.GetProperty("unspentBudgetShareMilli").GetInt64());
        Assert.False(refusedEl.GetProperty("deliberateHole").GetBoolean());
    }

    [Fact]
    public void Serialize_is_byte_identical_for_the_same_report_twice_the_check_gate_depends_on_this()
    {
        var report = BinderRunReport.From(Array.Empty<BoundNode>(), Array.Empty<RefusedSlot>());

        var first = ReportWriter.Serialize("might", Meta(), Array.Empty<BindInputNode>(), report);
        var second = ReportWriter.Serialize("might", Meta(), Array.Empty<BindInputNode>(), report);

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_deliberate_hole_is_carried_through_the_serialized_shape()
    {
        var refused = new RefusedSlot("skill.a", "known hole", 20L, DeliberateHole: true);
        var report = BinderRunReport.From(Array.Empty<BoundNode>(), new[] { refused });

        var json = ReportWriter.Serialize("might", Meta(), Array.Empty<BindInputNode>(), report);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("Pass", doc.RootElement.GetProperty("verdict").GetString());
        var refusedEl = Assert.Single(doc.RootElement.GetProperty("refused").EnumerateArray());
        Assert.True(refusedEl.GetProperty("deliberateHole").GetBoolean());
    }

    // -- 2026-09-07 real-corpus finding: the catalog shape itself --------------------------------

    [Fact]
    public void Serialize_writes_the_real_tree_record_fields()
    {
        var report = BinderRunReport.From(Array.Empty<BoundNode>(), Array.Empty<RefusedSlot>());
        var json = ReportWriter.Serialize("might", Meta(), Array.Empty<BindInputNode>(), report);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("primary", root.GetProperty("category").GetString());
        Assert.Equal("aptitude.Might@Commander", root.GetProperty("gateQuantity").GetString());
        Assert.Equal("broad-and-flat", root.GetProperty("shapeArchetype").GetString());
        Assert.Equal(10, root.GetProperty("tiers").GetInt32());
        Assert.Equal(2, root.GetProperty("branches").GetInt32());
        Assert.Equal(10, root.GetProperty("nodesPerTier").GetArrayLength());
        Assert.Equal(1, root.GetProperty("catalogVersion").GetInt32());
        Assert.True(root.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Only_bound_nodes_are_written_into_the_nodes_array_never_a_refused_or_ungenerated_one()
    {
        var bound = new BoundNode("skill.might-off-t5-n0", new[]
        {
            new NodeAtom("stat.derived", AttachPoint.Stat, "combat.power.fire", NodeAtomOp.Flat,
                null, null, 3038L, ScaleAxis.PTheta, UnitClass.GameUnits),
        });
        var refused = new RefusedSlot("skill.might-off-t9-n1", "not yet generated", 40L, DeliberateHole: false);
        var report = BinderRunReport.From(new[] { bound }, new[] { refused });
        var inputs = new[] { Input("skill.might-off-t5-n0"), Input("skill.might-off-t9-n1", affixIds: Array.Empty<string>()) };

        var json = ReportWriter.Serialize("might", Meta(), inputs, report);
        using var doc = JsonDocument.Parse(json);
        var nodesEl = doc.RootElement.GetProperty("nodes");

        var nodeEl = Assert.Single(nodesEl.EnumerateArray());
        Assert.Equal("skill.might-off-t5-n0", nodeEl.GetProperty("id").GetString());
    }

    [Fact]
    public void A_bound_node_carries_its_full_catalog_identity()
    {
        var bound = new BoundNode("skill.might-def-t3-n1", new[]
        {
            new NodeAtom("stat.derived", AttachPoint.Stat, "combat.power.fire", NodeAtomOp.Flat,
                null, null, 3038L, ScaleAxis.PTheta, UnitClass.GameUnits),
        });
        var report = BinderRunReport.From(new[] { bound }, Array.Empty<RefusedSlot>());
        var input = Input("skill.might-def-t3-n1", branch: TreeBranch.Def, tier: 3, nodeKey: "n1",
            nodeClass: NodeClass.Mechanism, affixIds: new[] { "atom.might", "atom.ferocity" },
            exclusionForm: ExclusionForm.Reroute, excludeProps: new[] { "posture" });

        var json = ReportWriter.Serialize("might", Meta(), new[] { input }, report);
        using var doc = JsonDocument.Parse(json);
        var nodeEl = Assert.Single(doc.RootElement.GetProperty("nodes").EnumerateArray());

        Assert.Equal("skill.might-def-t3-n1", nodeEl.GetProperty("id").GetString());
        Assert.Equal("might", nodeEl.GetProperty("treeId").GetString());
        Assert.Equal("Def", nodeEl.GetProperty("branch").GetString());
        Assert.Equal(3, nodeEl.GetProperty("tier").GetInt32());
        Assert.Equal("n1", nodeEl.GetProperty("nodeKey").GetString());
        Assert.Equal(0, nodeEl.GetProperty("prereqNodeIds").GetArrayLength());
        Assert.Equal("Mechanism", nodeEl.GetProperty("nodeClass").GetString());
        Assert.Equal(2, nodeEl.GetProperty("affixIds").GetArrayLength());
        Assert.Equal(9, nodeEl.GetProperty("budgetShareMilli").GetInt64());
        var excludeProps = Assert.Single(nodeEl.GetProperty("excludeProps").EnumerateArray());
        Assert.Equal("posture", excludeProps.GetString());
        Assert.Equal("Reroute", nodeEl.GetProperty("exclusionForm").GetString());
        Assert.Equal(JsonValueKind.Null, nodeEl.GetProperty("tagsJson").ValueKind);
        Assert.True(nodeEl.GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, nodeEl.GetProperty("retiredAtRevision").ValueKind);
    }

    [Fact]
    public void The_real_output_round_trips_cleanly_through_PassiveTreeCatalogLoader()
    {
        // The regression this whole fix exists for: a live-boot proof against the real committed
        // corpus refused every tree with "category token '' is outside the five-value map ... R7"
        // because nothing had ever written a document this loader could load. This proves the loader
        // accepts THIS writer's own real output with zero refusals -- not a hand-typed fixture.
        var bound = new BoundNode("skill.might-off-t5-n0", new[]
        {
            new NodeAtom("stat.derived", AttachPoint.Stat, "combat.power.fire", NodeAtomOp.Flat,
                null, null, 3038L, ScaleAxis.PTheta, UnitClass.GameUnits),
        });
        var report = BinderRunReport.From(new[] { bound }, Array.Empty<RefusedSlot>());
        var input = Input("skill.might-off-t5-n0");
        var json = ReportWriter.Serialize("might", Meta(), new[] { input }, report);

        var tuning = PassiveTreeTuningLoader.Parse(File.ReadAllText(
            Path.Combine(FindRepoRoot(), "data", "tuning", "passive-tree.v1.json")));
        var (loaded, importReport) = PassiveTreeCatalogLoader.Load(json, tuning);

        Assert.True(importReport.IsOk, string.Join("; ", importReport.Refusals));
        Assert.NotNull(loaded);
        Assert.Equal(TreeCategory.Primary, loaded!.Tree.Category);
        var node = Assert.Single(loaded.Nodes);
        Assert.Equal("skill.might-off-t5-n0", node.NodeId);
        var atom = Assert.Single(node.Atoms);
        Assert.Equal(3038L, atom.KMicro);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not locate the repo root (AGENTS.md not found upward)");
    }
}
