using System.Text.Json;
using FusionRpg.Core.PassiveTree.Binding;
using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.PassiveTree.State;

namespace FusionRpg.Tools.TreeBinder;

/// <summary>
/// Reads one `tree-plan` output file (`data/seed/passive-tree/plan/&lt;treeId&gt;.v1.json`, B1) into
/// this module's own <see cref="BindInputNode"/> shape. `affixIds`/`exclusionForm`/`deliberateHole`
/// are `tree-language`'s and `tree-plan`'s own fields (task D2's own scope: this module reads what
/// upstream emits, never invents it) — read here if present, defaulted otherwise, so this reader is
/// forward-compatible with those stages the moment they start emitting them, with no change needed
/// here. `budgetShareMilli` is read as given (R4) — never recomputed from a tier weight.
/// </summary>
public static class PlanReader
{
    // branches = 2 is D29's structural fact (10 tiers x 2 branches), never a tunable --
    // spec-tree-binder.md §3.3's own worked example fixes it the same way.
    public const long Branches = 2;

    public static List<BindInputNode> ReadPlanNodes(string planJson, PassiveTreeTuning treeTuning)
    {
        using var doc = JsonDocument.Parse(planJson);
        var nodesEl = doc.RootElement.TryGetProperty("nodes", out var n) ? n : default;
        var result = new List<BindInputNode>();
        if (nodesEl.ValueKind != JsonValueKind.Array) return result;

        foreach (var nodeEl in nodesEl.EnumerateArray())
        {
            var nodeId = nodeEl.GetProperty("id").GetString()!;
            var budgetShareMilli = nodeEl.GetProperty("budgetShareMilli").GetInt64();

            var affixIds = nodeEl.TryGetProperty("affixIds", out var aiEl) && aiEl.ValueKind == JsonValueKind.Array
                ? aiEl.EnumerateArray().Select(e => e.GetString()!).ToList()
                : new List<string>();

            var exclusionFormStr = nodeEl.TryGetProperty("exclusionForm", out var efEl) ? efEl.GetString() : "None";
            Enum.TryParse<ExclusionForm>(exclusionFormStr, ignoreCase: true, out var exclusionForm);

            var deliberateHole = nodeEl.TryGetProperty("deliberateHole", out var dhEl) && dhEl.ValueKind == JsonValueKind.True;

            result.Add(new BindInputNode(nodeId, treeTuning.TreeShareMilli, treeTuning.TreeBudgetMilli,
                budgetShareMilli, Branches, affixIds, exclusionForm, deliberateHole));
        }
        return result;
    }
}
