using System.Linq;
using System.Text.RegularExpressions;
using FusionRpg.Contracts;
using FusionRpg.Core.PassiveTree;
using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.PassiveTree.Resolve;
using FusionRpg.Core.PassiveTree.State;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// passive-tree-todo.md I2 — "the wire": GET returns the resolve report, POST takes one whole
/// allocation, never a per-node call (spec-tree-surface.md §10, §12). Copies `AptitudeEndpoints.cs`'s
/// own shape verbatim (GET-state / POST-allocate / broadcast-to-both-groups) rather than inventing a
/// second one — the todo names that file as the exact pattern to follow.
///
/// <para><b>Scope, stated rather than assumed.</b> This endpoint resolves the SHARED corpus only (the
/// 39 non-species trees) — a species bloodline is Level 0b's own pinned-to-the-creature read
/// (spec-tree-surface.md §3: "never enters a browse"), a different task (I5) with a different key
/// shape (per-creature, not per-player). Loading all 840 species trees (33,600 nodes) into one
/// player-level response would also be exactly the un-windowed volume defect §3's own callout warns
/// against for `DemonsPage.tsx`. `Commander` scope only, matching `AptitudeEndpoints`'s own stated
/// scope decision for the same reason: the other three scopes need a specimen picker this surface
/// does not have yet.</para>
///
/// <para><b>Gate-quantity resolution, the one real ambiguity this task closes with a stated default.</b>
/// Of the 39 shared trees, only the 12 Primary ones have a producer today — their `gateQuantity` reads
/// `"aptitude.&lt;Id&gt;@Commander"`, resolved against this player's own `AptitudeAllocation`. The
/// other 27 (`element_mastery`, `status_applied.&lt;id&gt;`) have no points-producing pipeline yet —
/// `gate-counters`' raw counters exist (`RpgStore.GateCounters.cs`) but the count-to-points mastery
/// curve does not (spec-tree-surface.md §9.1, D37: "a WAIT, not a permanent hole"). Every such tree
/// resolves `GateState = Unproduced`, base points `0`, exactly as spec-tree-resolve.md §3.3 already
/// states — never a workaround, never a private formula.</para>
/// </summary>
public static class PassiveTreeEndpoints
{
    // "aptitude.Might@Commander" -- the only gate-quantity shape with a real producer today (§9.1's
    // own table: 12 Primary paths, Commander scope, "shipped and wired"). Anything else -- including
    // a well-formed "aptitude.X@DemonType" this surface simply doesn't serve yet -- resolves
    // Unproduced rather than guessing at a scope this endpoint was never asked to support.
    static readonly Regex AptitudeGatePattern = new(@"^aptitude\.(?<id>[A-Za-z]+)@Commander$", RegexOptions.Compiled);

    public static void MapPassiveTree(this WebApplication app)
    {
        var g = app.MapGroup("/api/passive-tree");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store, IPowerIndexProvider powerIndex) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(ProjectState(store, powerIndex, playerId));
        });

        g.MapPost("/allocate", (AllocateTreeNodesRequest body, RpgStore store, IPowerIndexProvider powerIndex, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (body.Nodes is null) return Results.BadRequest(new { reason = "nodes.missing" });
            foreach (var soulLevel in body.Nodes.Values)
                if (soulLevel < 0)
                    return Results.BadRequest(new { reason = "nodes.negativeSoulLevel" });

            // One whole-allocation write, exactly `SaveTreeNodeState`'s own full delete-then-insert
            // contract (spec-tree-surface.md §4 rule 3) -- never a per-node endpoint call.
            store.SaveTreeNodeState(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(pid), body.Nodes);

            _ = BroadcastBestEffort(hub, pid);
            return Results.Ok(ProjectState(store, powerIndex, pid));
        });
    }

    static async Task BroadcastBestEffort(IHubContext<RpgHub> hub, long playerId)
    {
        // Both groups -- the exact mechanism AptitudeEndpoints.cs:76-89 already ships and the todo
        // names by line number: a WebGroup-only send left an injector-side cache stale until its next
        // reconnect (found live, 2026-08-30). A tree allocation reaches the same two listeners.
        try { await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("PassiveTreeUpdated", new { playerId }); }
        catch { /* best-effort; the allocation is durable and the next GET reflects it */ }
        try { await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("PassiveTreeUpdated", new { playerId }); }
        catch { /* best-effort; the injector re-syncs at its own next session start regardless */ }
    }

    static PassiveTreeStateDto ProjectState(RpgStore store, IPowerIndexProvider powerIndex, long playerId)
    {
        var tuning = PassiveTreeTuningHub.Tuning;
        var scopeKey = AptitudeEndpoints.ScopeKey(playerId);

        // §3: species trees are Level 0b's own pinned read, never a member of this general list --
        // filter to the shared corpus before paying for the heavier node/atom queries.
        var sharedTreeIds = store.ListTreeCatalogTrees()
            .Where(t => t.Category != TreeCategory.Species)
            .Select(t => t.TreeId)
            .ToList();
        var trees = store.LoadTreeCatalog(sharedTreeIds);

        // R3: retired/unknown node ids contribute zero and cost nothing to hold -- resolve reads the
        // LIVE-only projection, never the raw owned dictionary (TreeStateReconciler's own contract).
        var classified = store.LoadAndClassifyTreeState(AllocationScope.Commander, scopeKey);
        var owned = TreeStateReconciler.LiveOnly(classified);

        var allocation = store.LoadAllocation(AllocationScope.Commander, scopeKey);

        // baseByTree/stanceGroupByTree cover every aptitude-gated tree in the shared corpus -- built
        // once, fed to CrossUnlock for every tree's own lender lookup (D28: "its whole posture comes
        // along for free"). A tree with no producer yet is simply absent from both maps, which is
        // exactly CrossUnlock's own documented "no stance group -> credit 0" default.
        var baseByTree = new Dictionary<string, long>(StringComparer.Ordinal);
        var stanceGroupByTree = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var tree in trees)
        {
            if (!TryParseAptitudeGate(tree.Tree.GateQuantity, out var aptitudeId)) continue;
            baseByTree[tree.Tree.TreeId] = allocation.PointsAt(AllocationScope.Commander, aptitudeId);
            stanceGroupByTree[tree.Tree.TreeId] = AptitudeCatalog.Get(aptitudeId).Posture.ToString();
        }

        // H/F: computed ONCE over the actor's WHOLE allocation (every tree the actor owns anything in,
        // shared or species alike -- Concentration.cs's own contract is "the final allocation," not
        // "the shared corpus"), then handed to every tree's report unchanged (TreeResolveReport's own
        // "plain, already-resolved value" rule). TreeNodeSet parses treeId from the node id itself, so
        // this needs no species catalog load at all.
        var selfSpent = TreeNodeSet.SelfSpent(owned);
        var hNodesMilli = Concentration.HerfindahlMilli(selfSpent.Values.Select(s => s.NodeCount).ToList());
        var hSoulsMilli = Concentration.HerfindahlMilli(selfSpent.Values.Select(s => s.SoulLevels).ToList());
        var hMilli = Concentration.BlendMilli(hNodesMilli, hSoulsMilli, tuning.Concentration.WMilli);
        var focusMilli = Concentration.FmaxAppliedMilli(hMilli, tuning.Concentration.FmaxMilli);

        var reports = new List<TreeResolveReportDto>(trees.Count);
        foreach (var tree in trees)
        {
            var treeId = tree.Tree.TreeId;
            var isWired = TryParseAptitudeGate(tree.Tree.GateQuantity, out _);
            var gateState = isWired ? TreeGateState.Wired : TreeGateState.Unproduced;

            // gate(i) = base(i) + credit(i) -- "you have" on the tier row (§7.2), own contribution
            // plus at most one lender's, never a sum of lenders (CrossUnlock's own D28 rule).
            var gatePoints = CrossUnlock.Gate(treeId, baseByTree, stanceGroupByTree);
            var lenderTreeId = CrossUnlock.Lender(treeId, baseByTree, stanceGroupByTree);
            var tierReached = TierGate.Reached(gatePoints, tree.Tree.Tiers, tuning.TierLadder.ReqScalePoints);

            var ownedInThisTree = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in tree.Nodes)
                if (owned.ContainsKey(node.NodeId))
                    ownedInThisTree.Add(node.NodeId);

            var report = TreeResolveReport.Build(tree, gateState, tierReached, gatePoints,
                ownedInThisTree, lenderTreeId, hMilli, focusMilli);

            reports.Add(new TreeResolveReportDto
            {
                TreeId = report.TreeId,
                Category = tree.Tree.Category.ToString(),
                GateState = report.GateState == TreeGateState.Wired ? "wired" : "unproduced",
                TierReached = report.TierReached,
                Tiers = tree.Tree.Tiers,
                AptitudePoints = report.AptitudePoints,
                // I8: this tree's own base spend, before any stance-mate's credit -- straight off the
                // same `baseByTree` map `CrossUnlock.Gate`/`Lender` above already read, never a second
                // computation. Zero for a tree absent from the map (no aptitude-gate producer, §9.1).
                OwnAptitudePoints = baseByTree.GetValueOrDefault(treeId, 0),
                ContributingNodeIds = report.ContributingNodeIds.ToList(),
                InvalidNodeIds = report.InvalidNodeIds.ToList(),
                LenderTreeId = report.LenderTreeId,
                HerfindahlMilli = report.HerfindahlMilli,
                FocusMilli = report.FocusMilli,
                ExcludedNodes = report.ExcludedNodes.Select(e => new ExcludedNodeDto
                {
                    NodeId = e.NodeId,
                    Form = e.Form.ToString(),
                    WinnerNodeId = e.WinnerNodeId,
                    IsInert = e.IsInert
                }).ToList(),
                // I6: structural (branch, tier) identity for EVERY enabled node, not only owned ones --
                // TreeResolveReport itself only ever names owned nodes, which cannot describe an
                // available or locked cell nobody has bought yet. Retired nodes excluded, same as
                // TreeResolveReport.Build's own rule.
                Nodes = tree.Nodes
                    .Where(n => n.Enabled)
                    .OrderBy(n => n.Tier)
                    .ThenBy(n => n.Branch)
                    .ThenBy(n => n.NodeId, StringComparer.Ordinal)
                    .Select(n => new TreeNodeSummaryDto
                    {
                        NodeId = n.NodeId,
                        Branch = n.Branch.ToString(),
                        Tier = n.Tier,
                        NodeClass = n.NodeClass.ToString()
                    }).ToList()
            });
        }

        // Skill-points wallet (task I3, spec-tree-surface.md §4.1) -- the "buy a trait" currency,
        // distinct from the aptitude points above ("open a tier") and souls ("deepen a trait", read
        // separately from /api/souls). `PointBudget.SkillPointsFor` (D34) and `TreeUnlockCost` (D25/
        // D36) are both pre-existing, pre-tested pure functions this endpoint is simply the first
        // production caller of -- an inert path being wired, not new math. `owned.Count` is exactly
        // OwnershipRules' "count of valid owned nodes" (LiveOnly already excludes retired/invalid
        // rows), and it spans every Commander-scope tree, matching `NodesOnOtherTreesCount = true`.
        var theta = (long)powerIndex.ActorIndex(new StatContext { PlayerId = playerId });
        var skillBudget = PointBudget.SkillPointsFor(AllocationScope.Commander, theta, AptitudeTuningHub.Tuning);
        var skillSpent = TreeUnlockCost.Cumulative(owned.Count, tuning.UnlockCost.FirstPoints, tuning.UnlockCost.StepPoints);
        var skillAvailable = TreeUnlockCost.Available(skillBudget, owned.Count, tuning.UnlockCost.FirstPoints, tuning.UnlockCost.StepPoints);

        return new PassiveTreeStateDto
        {
            PlayerId = playerId,
            CatalogRevision = store.GetTreeCatalogRevision(),
            SoulLevelByNodeId = new Dictionary<string, long>(owned, StringComparer.Ordinal),
            Trees = reports,
            SkillPointsBudget = skillBudget,
            SkillPointsSpent = skillSpent,
            SkillPointsAvailable = skillAvailable,
            TierReqScalePoints = tuning.TierLadder.ReqScalePoints,
            // I8 (spec-tree-surface.md §5.2): the (first, step) pair itself, so a client-side Plan
            // preview can reproduce TreeUnlockCost.Cumulative exactly for a HYPOTHETICAL owned count
            // -- the two already-wired totals above are a single (count, cumulative) sample and cannot
            // be inverted back into (first, step) in general.
            UnlockCostFirstPoints = tuning.UnlockCost.FirstPoints,
            UnlockCostStepPoints = tuning.UnlockCost.StepPoints,
            // I9 (spec-tree-surface.md §6): the concentration dial itself, so a DRAFT preview can
            // mirror H/F exactly for a hypothetical allocation -- the committed reading above never
            // uses these, it is `hMilli`/`focusMilli` per tree, computed once, already.
            ConcentrationFmaxMilli = tuning.Concentration.FmaxMilli,
            ConcentrationWMilli = tuning.Concentration.WMilli
        };
    }

    static bool TryParseAptitudeGate(string gateQuantity, out string aptitudeId)
    {
        var match = AptitudeGatePattern.Match(gateQuantity);
        if (match.Success && AptitudeCatalog.IsAptitudeId(match.Groups["id"].Value))
        {
            aptitudeId = match.Groups["id"].Value;
            return true;
        }
        aptitudeId = "";
        return false;
    }
}
