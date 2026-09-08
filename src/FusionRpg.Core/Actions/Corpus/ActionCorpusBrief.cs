namespace FusionRpg.Core.Actions.Corpus;

/// <summary>
/// One entry from `data/seed/actions/committed-round-*.json` — a BRIEF, not a finished row
/// (spec-action-instance-and-grant.md §Objective point 2). Raw strings throughout, matching the JSON
/// wire shape exactly; <see cref="ActionCorpusComposer.Compose"/> is what parses and validates them —
/// keeping this type a plain, unvalidated carrier makes a hand-built test fixture as easy to construct
/// as a real parsed row.
/// </summary>
public sealed record ActionCorpusBrief(
    string Id,
    string Name,
    string Category,
    string Scope,
    string? ScopeKey,
    int RungFloor,
    int RungCeiling,
    IReadOnlyList<string> AtomFamilies,
    string TargetMode,
    string Relation,
    /// <summary>item-content `granted-action-text` (T14): the authored display key for this action's
    /// description (`ssot-presentation.md` §3.6 L3 — a key, never the sentence). Required of every
    /// row in a real corpus file: <see cref="ActionCorpusBriefJson"/> rejects a brief that omits it.
    /// The default exists only so a hand-built test fixture stays constructible.</summary>
    string DescriptionKey = "");
