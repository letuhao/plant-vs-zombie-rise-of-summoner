using System.Text.Json;
using FusionRpg.Core.PassiveTree.Binding;
using FusionRpg.Core.PassiveTree.Catalog;

namespace FusionRpg.Tools.TreeBinder;

/// <summary>
/// Serializes one tree's <see cref="BinderRunReport"/> into the binder's own committed output
/// (`data/generated/passive-tree/&lt;treeId&gt;.json`, task D2) — the REAL `tree-catalog`
/// `TreeRecord`/`NodeRecord` shape (spec-tree-catalog.md §2.1/§2.2), per `spec-tree-binder.md`'s own
/// unambiguous Project structure table ("THIS is what ships") and its own line "`tree-catalog` owns
/// the on-disk record shape; this module writes it and never redefines it."
///
/// <para><b>2026-09-07 real-corpus finding, root-caused and fixed here.</b> This class previously
/// wrote only its OWN narrow verdict/bound/refused report — deliberately, per its own prior doc
/// comment, on the stated (and at the time genuinely true) reasoning that a bare
/// <see cref="BinderRunReport"/> "does not carry tier, branch, nodeKey, prereqs and tags." The FIRST
/// real attempt to import a real committed file through `PassiveTreeCatalogLoader`
/// (`PassiveTreeImportRunner`'s own live-boot proof) refused every one of 42 real trees:
/// <c>"category token '' is outside the five-value map ... — R7"</c> — the loader expects the full
/// catalog record this class never wrote. The missing identity fields are NOT actually absent from
/// this module's own inputs, only from the bare run report: `PlanReader.ReadPlanNodesWithSeed`
/// already parses `branch`/`tier`/`nodeKey`/`nodeClass`/`excludeProps` per node (now carried on
/// <see cref="BindInputNode"/> itself, see its own doc comment), and the new
/// <see cref="PlanReader.ReadTreeMeta"/> reads the tree-level `category`/`gateQuantity`/
/// `shapeArchetype`/`nodesPerTier`/`catalogVersion` from the SAME plan document `Program.cs` already
/// has open. This class now takes both alongside the run report and emits the real record.</para>
///
/// <para>Only BOUND nodes are written into `nodes[]` — a not-yet-generated or refused-at-bind-time
/// node is OMITTED, never written with placeholder content that would trip
/// `PassiveTreeCatalogLoader`'s own "affixIds must be 1..3" refusal for the whole tree. This mirrors
/// `tree-binder`'s own already-established "a partially generated tree binds every already-accepted
/// node" philosophy (H9) one layer up: a partial corpus produces a partial (but individually valid)
/// catalog, never a poisoned one. `prereqNodeIds` is always `[]` — confirmed by grep that nothing in
/// this program's real design populates or reads it anywhere outside the catalog schema itself (the
/// tier gate ladder, not a per-node link graph, is what actually gates progression here); `tagsJson`
/// and `retiredAtRevision` are `null` for the same reason (D22's tag payoff and node retirement are
/// both real future features this module correctly does not invent content for).</para>
///
/// <para>The original verdict/bound/refused shape is kept ALONGSIDE the new catalog fields, not
/// replaced — extra top-level keys are silently ignored by `PassiveTreeCatalogLoader`'s own
/// unstrict, field-by-field parse, so this remains the same real audit trail (what bound, what was
/// refused and why, the run verdict) the original author built, now also a real, loadable
/// catalog.</para>
/// </summary>
public static class ReportWriter
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Serialize(string treeId, TreeCatalogMeta meta,
        IReadOnlyList<BindInputNode> inputNodes, BinderRunReport report)
    {
        var inputsById = inputNodes.ToDictionary(n => n.NodeId, StringComparer.Ordinal);

        // 2026-09-07 real round-trip finding: `PassiveTreeCatalogLoader.LoadAtom` requires
        // `attachPoint` (a required enum field -- refuses "unknown attachPoint ''" when absent) and
        // reads `trigger`/`whenJson` too; the original writer never serialized any of the three,
        // only ever exercised as a hand-typed test fixture until this real round-trip test existed.
        static object SerializeAtom(NodeAtom a) => new
        {
            kindId = a.KindId,
            attachPoint = a.AttachPoint.ToString(),
            channelId = a.ChannelId,
            op = a.Op.ToString(),
            trigger = a.Trigger,
            whenJson = a.WhenJson,
            kMicro = a.KMicro,
            unitClass = a.UnitClass.ToString(),
            scaleAxis = a.ScaleAxis.ToString(),
        };

        var shape = new
        {
            treeId,
            category = meta.Category,
            gateQuantity = meta.GateQuantity,
            shapeArchetype = meta.ShapeArchetype,
            tiers = meta.Tiers,
            branches = meta.Branches,
            nodesPerTier = meta.NodesPerTier,
            catalogVersion = meta.CatalogVersion,
            enabled = true,
            nodes = report.Bound.Select(b =>
            {
                var input = inputsById[b.NodeId];
                return new
                {
                    id = b.NodeId,
                    treeId,
                    branch = input.Branch.ToString(),
                    tier = input.Tier,
                    nodeKey = input.NodeKey,
                    prereqNodeIds = Array.Empty<string>(),
                    nodeClass = input.NodeClass.ToString(),
                    affixIds = input.AffixIds,
                    budgetShareMilli = input.BudgetShareMilli,
                    atoms = b.Atoms.Select(SerializeAtom),
                    excludeProps = input.ExcludeProps,
                    exclusionForm = input.ExclusionForm.ToString(),
                    tagsJson = (string?)null,
                    enabled = true,
                    retiredAtRevision = (int?)null,
                };
            }),
            verdict = report.Verdict.ToString(),
            totalUnspentBudgetShareMilli = report.TotalUnspentBudgetShareMilli,
            bound = report.Bound.Select(b => new
            {
                nodeId = b.NodeId,
                atoms = b.Atoms.Select(SerializeAtom),
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
