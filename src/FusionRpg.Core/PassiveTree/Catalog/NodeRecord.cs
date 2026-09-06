namespace FusionRpg.Core.PassiveTree.Catalog;

/// <summary>D10's two branches.</summary>
public enum TreeBranch
{
    Off,
    Def,
}

/// <summary>Ideal §3.5's requirement: a magnitude-only deep tier is measurably worthless to a
/// focused build. The plan guarantees deep tiers carry mechanisms; the catalog carries the label
/// so `tree-review` and the sweep can count them.</summary>
public enum NodeClass
{
    Magnitude,
    Mechanism,
}

/// <summary>D40 — all three exclusion forms are kept and reachable. The form is mechanical, not
/// flavour: three consumers branch on it (`tree-resolve` decides contribution,
/// `tree-surface` renders a nullified node inert, and this loader validates the
/// form/`excludeProps` pairing), so it cannot live only in authored text.</summary>
public enum ExclusionForm
{
    None,
    Reroute,
    Precedence,
    Nullification,
}

/// <summary>
/// One per node — 40 per tree, everywhere including species (spec-tree-catalog.md §2.2).
/// `budgetShareMilli` is the plan's own emitted share, copied through VERBATIM and never
/// recomputed (R4) — it is the number the potency ceiling is checked against, never `kMicro`
/// (§2.5's dimensional-error correction).
/// </summary>
public sealed record NodeRecord(
    string NodeId,
    string TreeId,
    TreeBranch Branch,
    int Tier,
    string NodeKey,
    IReadOnlyList<string> PrereqNodeIds,
    NodeClass NodeClass,
    IReadOnlyList<string> AffixIds,
    int BudgetShareMilli,
    IReadOnlyList<NodeAtom> Atoms,
    IReadOnlyList<string> ExcludeProps,
    ExclusionForm ExclusionForm,
    string? TagsJson,
    bool Enabled,
    int? RetiredAtRevision);
