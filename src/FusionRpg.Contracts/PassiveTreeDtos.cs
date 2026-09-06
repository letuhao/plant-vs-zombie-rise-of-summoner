using System.Text.Json.Serialization;

namespace FusionRpg.Contracts;

/// <summary>
/// Task I2's wire shape (spec-tree-surface.md §10, §12) — the DTO twin of
/// <c>FusionRpg.Core.PassiveTree.Resolve.TreeResolveReport</c>, one per tree in the shared corpus.
/// A plain mirror, field for field: this module never recomputes anything the report already
/// resolved (the report's own success criterion 8), it only gives the resolved record a wire shape.
/// <c>GateState</c> is the lowercase string form ("wired"/"unproduced") of
/// <c>TreeGateState</c> — spec-tree-surface.md §9.1 rule 5 reads it as a field, never re-derives it
/// from a zero, so the wire form keeps exactly the two values and nothing else.
/// </summary>
public sealed class ExcludedNodeDto
{
    [JsonPropertyName("nodeId")] public string NodeId { get; set; } = "";
    [JsonPropertyName("form")] public string Form { get; set; } = "";
    [JsonPropertyName("winnerNodeId")] public string WinnerNodeId { get; set; } = "";
    [JsonPropertyName("isInert")] public bool IsInert { get; set; }
}

/// <summary>
/// Task I6's structural addition (spec-tree-surface.md §2.3, §9) — the catalog's own STRUCTURE for
/// one node (which (branch, tier) slot it occupies), never its authored copy: `tree-language`
/// (H1-H2) has not run for any shared tree yet, so no node anywhere in the corpus has a player-facing
/// name or effect sentence today (passive-tree-todo.md line ~1053: "the CLI honestly reports all 40
/// ... since tree-language hasn't emitted them"). Level 2's lattice needs real (branch, tier, nodeId)
/// identity for all 40 cells -- owned AND not-yet-owned -- to mount the fixed grid GG-61 asks for;
/// <see cref="TreeResolveReportDto"/> alone only ever names OWNED nodes (contributing/invalid/excluded),
/// which cannot describe a cell nobody has bought yet. This is a plain mirror of
/// <c>FusionRpg.Core.PassiveTree.Catalog.NodeRecord</c>'s own structural fields, nothing resolved or
/// computed here.
/// </summary>
public sealed class TreeNodeSummaryDto
{
    [JsonPropertyName("nodeId")] public string NodeId { get; set; } = "";
    [JsonPropertyName("branch")] public string Branch { get; set; } = "";
    [JsonPropertyName("tier")] public int Tier { get; set; }
    [JsonPropertyName("nodeClass")] public string NodeClass { get; set; } = "";
}

public sealed class TreeResolveReportDto
{
    [JsonPropertyName("treeId")] public string TreeId { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("gateState")] public string GateState { get; set; } = "";
    [JsonPropertyName("tierReached")] public int TierReached { get; set; }
    [JsonPropertyName("tiers")] public int Tiers { get; set; }
    [JsonPropertyName("aptitudePoints")] public long AptitudePoints { get; set; }
    /// <summary>Task I8 (spec-tree-surface.md §7.2 part 2) — this tree's OWN aptitude allocation,
    /// before any stance-mate's credit (D28's `base(i)` in `CrossUnlock.Gate(i) = base(i) +
    /// credit(i)`). Exposed as its own field rather than left for the client to reverse-engineer from
    /// `AptitudePoints`/`LenderTreeId`: two same-stance trees that mutually lend to each other (each
    /// the other's `LenderTreeId`) make that reverse derivation genuinely UNDERDETERMINED — more than
    /// one `(base, credit)` split is consistent with the same pair of wire values. `AptitudePoints -
    /// OwnAptitudePoints` is the credited (lent) amount, meaningful only when `LenderTreeId` is set.
    /// Zero for any tree with no aptitude-gate producer (`gateState: "unproduced"`, §9.1).</summary>
    [JsonPropertyName("ownAptitudePoints")] public long OwnAptitudePoints { get; set; }
    [JsonPropertyName("contributingNodeIds")] public List<string> ContributingNodeIds { get; set; } = new();
    [JsonPropertyName("invalidNodeIds")] public List<string> InvalidNodeIds { get; set; } = new();
    [JsonPropertyName("lenderTreeId")] public string? LenderTreeId { get; set; }
    [JsonPropertyName("herfindahlMilli")] public long HerfindahlMilli { get; set; }
    [JsonPropertyName("focusMilli")] public long FocusMilli { get; set; }
    [JsonPropertyName("excludedNodes")] public List<ExcludedNodeDto> ExcludedNodes { get; set; } = new();
    /// <summary>I6 — every ENABLED node's (branch, tier) slot, retired nodes excluded exactly like
    /// <see cref="TreeResolveReport.Build"/> already excludes them from its own bookkeeping. Ordered
    /// by (tier, branch) so a client never has to re-sort the lattice.</summary>
    [JsonPropertyName("nodes")] public List<TreeNodeSummaryDto> Nodes { get; set; } = new();
}

/// <summary>
/// The whole-actor projection `PassiveTreeEndpoints`'s GET returns — one <see cref="TreeResolveReportDto"/>
/// per SHARED-corpus tree (`category != Species`; a species bloodline is Level 0b's own pinned read,
/// spec-tree-surface.md §3, never a member of this general actor-level list) plus the raw owned
/// node -> soul-level dictionary `SaveTreeNodeState` already persists, since that dictionary IS the
/// edit surface a POST resubmits (the same "whole allocation" shape, mirroring `AptitudeEndpoints`'s
/// own `shares` field).
///
/// <para><b>The skill-points wallet (task I3, spec-tree-surface.md §4.1) — the second of three
/// currencies the surface names.</b> Aptitude points already round-trip per-tree via
/// <see cref="TreeResolveReportDto.AptitudePoints"/>, and the player's overall unspent aptitude
/// pool is <c>/api/aptitudes/{playerId}</c>'s own <c>budget - spent</c> (the SAME wallet primary
/// stats spend from, D28's whole point). Souls are <c>/api/souls/{playerId}</c>'s own balance. Skill
/// points had no wire shape anywhere before this task: <c>PointBudget.SkillPointsFor</c> (D34) and
/// <c>TreeUnlockCost</c> (D25/D36) were both already shipped and already tested, but zero production
/// caller combined them into a number a player could read — an inert path, not a missing feature.
/// These three fields are that first caller, mirroring `AptitudesState`'s own budget/spent shape
/// rather than inventing a new one.</para>
/// </summary>
public sealed class PassiveTreeStateDto
{
    [JsonPropertyName("playerId")] public long PlayerId { get; set; }
    [JsonPropertyName("catalogRevision")] public long CatalogRevision { get; set; }
    [JsonPropertyName("soulLevelByNodeId")] public Dictionary<string, long> SoulLevelByNodeId { get; set; } = new();
    [JsonPropertyName("trees")] public List<TreeResolveReportDto> Trees { get; set; } = new();
    [JsonPropertyName("skillPointsBudget")] public long SkillPointsBudget { get; set; }
    [JsonPropertyName("skillPointsSpent")] public long SkillPointsSpent { get; set; }
    [JsonPropertyName("skillPointsAvailable")] public long SkillPointsAvailable { get; set; }
    /// <summary>I6 (spec-tree-surface.md §9's own worked distance example) — `PassiveTreeTuning.
    /// TierLadder.ReqScalePoints`, the ONE global constant `req(t) = reqScalePoints * t*(t+1)/2`
    /// needs (`TierGate.Reached`'s own formula, mirrored client-side rather than re-derived: "one
    /// power ladder, no private curves"). A single tree-wide value, not per-tree, because every tree
    /// shares the same ladder (D26/D29) -- carried once here rather than repeated on every report.</summary>
    [JsonPropertyName("tierReqScalePoints")] public long TierReqScalePoints { get; set; }
    /// <summary>Task I8 (spec-tree-surface.md §5.2) — `PassiveTreeTuning.UnlockCost`'s own `(first,
    /// step)` pair, mirrored client-side so a Plan preview can reproduce `TreeUnlockCost.Cumulative`
    /// EXACTLY (the same "one power ladder, no private curves" reason `TierReqScalePoints` above was
    /// added for) rather than a client re-derivation of the rising-price curve from the two already-
    /// wired totals (`SkillPointsSpent`/`SkillPointsBudget`), which is impossible in general: those two
    /// numbers alone under-determine `(first, step)`.</summary>
    [JsonPropertyName("unlockCostFirstPoints")] public long UnlockCostFirstPoints { get; set; }
    [JsonPropertyName("unlockCostStepPoints")] public long UnlockCostStepPoints { get; set; }
    /// <summary>
    /// Task I9 (spec-tree-surface.md §6) -- <c>PassiveTreeTuning.Concentration</c>'s own two dial
    /// values (`FmaxMilli`/`WMilli`), mirrored client-side for exactly one purpose: a DRAFT preview
    /// (a pending, uncommitted Plan) needs to show "what Focus would become" (§5.1 item 2, §6: "it
    /// moves while you edit"), and the only way to reproduce <see cref="Concentration.HerfindahlMilli"/>
    /// / <see cref="Concentration.BlendMilli"/> / <see cref="Concentration.FmaxAppliedMilli"/> exactly
    /// for a hypothetical allocation is with the REAL tuning dial -- the same "one power ladder, no
    /// private curves" reason <see cref="TierReqScalePoints"/> and <see cref="UnlockCostFirstPoints"/>/
    /// <see cref="UnlockCostStepPoints"/> already carry their own dial values rather than being
    /// hand-typed client-side. A hardcoded 1200/500 would silently go stale the day a balance pass
    /// changes either one, breaking §6's own promise ("a dial change moves the line with no FE edit")
    /// for exactly this one line.
    ///
    /// <para><b>The COMMITTED Focus line never reads these fields.</b> Every
    /// <see cref="TreeResolveReportDto"/> already carries its own already-resolved
    /// <c>HerfindahlMilli</c>/<c>FocusMilli</c>, computed server-side once over the actor's whole
    /// allocation -- that per-tree pair is the only source for the committed reading, exactly as
    /// <see cref="TreeResolveReportDto.HerfindahlMilli"/>'s own doc comment already states. These two
    /// fields exist solely so a NON-committed draft can be previewed with the real formula.</para>
    /// </summary>
    [JsonPropertyName("concentrationFmaxMilli")] public long ConcentrationFmaxMilli { get; set; }
    [JsonPropertyName("concentrationWMilli")] public long ConcentrationWMilli { get; set; }
}

/// <summary>
/// The POST body — one WHOLE allocation, never a per-node call (spec-tree-surface.md §4 rule 3: "the
/// step edits the draft, not the server. Commit stays one whole-allocation POST"). <c>Nodes</c> is
/// exactly <c>SaveTreeNodeState</c>'s own <c>IReadOnlyDictionary&lt;string, long&gt;</c> shape
/// (node id -> soul level) — a full replace, matching the store's own delete-then-insert contract.
/// </summary>
public sealed class AllocateTreeNodesRequest
{
    [JsonPropertyName("playerId")] public long? PlayerId { get; set; }
    [JsonPropertyName("nodes")] public Dictionary<string, long>? Nodes { get; set; }
}
