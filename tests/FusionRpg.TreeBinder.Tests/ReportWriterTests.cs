using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.PassiveTree.Binding;
using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Tools.TreeBinder;
using Xunit;

namespace FusionRpg.TreeBinder.Tests;

/// <summary>Task D2 — `ReportWriter` (tools/TreeBinder), the binder's own committed run output
/// (`data/generated/passive-tree/&lt;treeId&gt;.json`). Proves the shape is complete (verdict, total
/// unspent budget, bound atoms, refused slots with their unspent share and deliberate-hole flag) and
/// stable (`--check`'s byte-identical regeneration depends on it).</summary>
public class ReportWriterTests
{
    [Fact]
    public void Serialize_carries_verdict_bound_and_refused_with_unspent_budget()
    {
        var bound = new BoundNode("skill.might-off-t5-n0", new[]
        {
            new NodeAtom("stat.derived", AttachPoint.Stat, "combat.power.fire", NodeAtomOp.Flat,
                null, null, 3038L, ScaleAxis.PTheta, UnitClass.GameUnits, null),
        });
        var refused = new RefusedSlot("skill.might-off-t7-b3", "18th atom kind (D16)", 64L, DeliberateHole: false);
        var report = BinderRunReport.From(new[] { bound }, new[] { refused });

        var json = ReportWriter.Serialize("might", report);
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

        var first = ReportWriter.Serialize("might", report);
        var second = ReportWriter.Serialize("might", report);

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_deliberate_hole_is_carried_through_the_serialized_shape()
    {
        var refused = new RefusedSlot("skill.a", "known hole", 20L, DeliberateHole: true);
        var report = BinderRunReport.From(Array.Empty<BoundNode>(), new[] { refused });

        var json = ReportWriter.Serialize("might", report);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("Pass", doc.RootElement.GetProperty("verdict").GetString());
        var refusedEl = Assert.Single(doc.RootElement.GetProperty("refused").EnumerateArray());
        Assert.True(refusedEl.GetProperty("deliberateHole").GetBoolean());
    }
}
