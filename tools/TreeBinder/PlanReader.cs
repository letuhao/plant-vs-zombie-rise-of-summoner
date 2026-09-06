using System.Text.Json;
using FusionRpg.Core.PassiveTree.Binding;
using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.PassiveTree.State;

namespace FusionRpg.Tools.TreeBinder;

/// <summary>
/// Reads one `tree-plan` output file (`data/seed/passive-tree/plan/&lt;treeId&gt;.v1.json`, B1) into
/// this module's own <see cref="BindInputNode"/> shape. `deliberateHole` is `tree-plan`'s own field
/// (read here if present, defaulted otherwise — see <see cref="BindInputNode.DeliberateHole"/>'s own
/// doc comment). `budgetShareMilli` is read as given (R4) — never recomputed from a tier weight.
///
/// <b>`affixIds`/`exclusionForm` are `tree-language`'s fields, never `tree-plan`'s</b> — found real
/// 2026-09-06, the day this program's generation pipeline (H9) first produced real content for more
/// than one tree: `spec-tree-binder.md` §3.1's own IN table has ALWAYS said `affixIds[]` arrives
/// "from tree-language", and `spec-tree-language.md` §6.4's own diagram has ALWAYS shown
/// `plan/&lt;treeId&gt;.json` and `nodes/&lt;treeId&gt;.json` as two separate files, the second being
/// tree-language's own committed output — but this reader's ORIGINAL one-argument overload (kept
/// below, unchanged, for the plan-only fixtures `PlanReaderTests.cs` already covers) only ever read
/// the plan file, so a real run against real generated content refused every node
/// ("`affixIds` must be 1..3, got 0") even after 379 real nodes existed with real chosen affixes —
/// confirmed by actually running `tools/TreeBinder` against the real committed corpus, not assumed.
/// `ReadPlanNodesWithSeed` is the fix: it reads the plan exactly as before, then overlays each
/// matching node's real `affixIds`/`exclusion.form` from `nodes/&lt;treeId&gt;.json` where that node
/// has been generated. A node the language stage has not yet accepted keeps the plan-only defaults
/// (empty `affixIds`, `ExclusionForm.None`) and is correctly refused with its unspent budget reported
/// — the same partial-corpus behavior §7.2 already designs for, now reachable by a REAL cause
/// (not-yet-generated) instead of only a hypothetical one.
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

    /// <summary>The real production path (`Program.cs`'s own CLI run): the plan read exactly as
    /// `ReadPlanNodes` already does, with each node's `affixIds`/`exclusionForm` overridden from
    /// `seedJson` (`nodes/&lt;treeId&gt;.json`'s own content) wherever that node id has been
    /// generated. `seedJson` is `null` for a tree tree-language has not touched at all yet (the
    /// whole tree correctly refuses, exactly as `ReadPlanNodes` alone already did).</summary>
    public static List<BindInputNode> ReadPlanNodesWithSeed(string planJson, string? seedJson,
                                                            PassiveTreeTuning treeTuning)
    {
        var nodes = ReadPlanNodes(planJson, treeTuning);
        if (string.IsNullOrEmpty(seedJson)) return nodes;

        var overrides = ReadSeedOverrides(seedJson);
        return nodes.ConvertAll(node => overrides.TryGetValue(node.NodeId, out var seed)
            ? node with { AffixIds = seed.AffixIds, ExclusionForm = seed.ExclusionForm }
            : node);
    }

    static Dictionary<string, (IReadOnlyList<string> AffixIds, ExclusionForm ExclusionForm)> ReadSeedOverrides(
        string seedJson)
    {
        using var doc = JsonDocument.Parse(seedJson);
        var nodesEl = doc.RootElement.TryGetProperty("nodes", out var n) ? n : default;
        var result = new Dictionary<string, (IReadOnlyList<string>, ExclusionForm)>(StringComparer.Ordinal);
        if (nodesEl.ValueKind != JsonValueKind.Array) return result;

        foreach (var nodeEl in nodesEl.EnumerateArray())
        {
            var nodeId = nodeEl.GetProperty("id").GetString()!;
            var affixIds = nodeEl.TryGetProperty("affixIds", out var aiEl) && aiEl.ValueKind == JsonValueKind.Array
                ? aiEl.EnumerateArray().Select(e => e.GetString()!).ToList()
                : new List<string>();

            var exclusionFormStr = nodeEl.TryGetProperty("exclusion", out var exEl) &&
                                   exEl.TryGetProperty("form", out var formEl)
                ? formEl.GetString() : "None";
            Enum.TryParse<ExclusionForm>(exclusionFormStr, ignoreCase: true, out var exclusionForm);

            result[nodeId] = (affixIds, exclusionForm);
        }
        return result;
    }
}
