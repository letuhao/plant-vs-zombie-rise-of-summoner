using System.Text.Json;
using FusionRpg.Core.PassiveTree.Binding;

namespace FusionRpg.Tools.TreeBinder;

/// <summary>
/// Serializes one tree's <see cref="BinderRunReport"/> into the binder's own committed output shape
/// (`data/generated/passive-tree/&lt;treeId&gt;.json`, task D2). Deliberately NOT the full
/// `tree-catalog` `TreeRecord`/`NodeRecord` shape -- assembling that additionally needs tier, branch,
/// `nodeKey`, prereqs and tags, none of which this run report carries (those are `tree-plan`'s and
/// `tree-catalog`'s own fields). This is the binder's own audit trail: what it bound, what it
/// refused, and the run verdict -- stable and re-readable by <c>--check</c>.
/// </summary>
public static class ReportWriter
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(string treeId, BinderRunReport report)
    {
        var shape = new
        {
            treeId,
            verdict = report.Verdict.ToString(),
            totalUnspentBudgetShareMilli = report.TotalUnspentBudgetShareMilli,
            bound = report.Bound.Select(b => new
            {
                nodeId = b.NodeId,
                atoms = b.Atoms.Select(a => new
                {
                    kindId = a.KindId,
                    channelId = a.ChannelId,
                    op = a.Op.ToString(),
                    kMicro = a.KMicro,
                    unitClass = a.UnitClass.ToString(),
                    scaleAxis = a.ScaleAxis.ToString(),
                }),
            }),
            refused = report.Refused.Select(r => new
            {
                nodeId = r.NodeId,
                unspentBudgetShareMilli = r.UnspentBudgetShareMilli,
                deliberateHole = r.DeliberateHole,
                reason = r.Reason,
            }),
        };
        return JsonSerializer.Serialize(shape, Options);
    }
}
