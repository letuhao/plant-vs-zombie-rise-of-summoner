using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Actions;

/// <summary>One cost row after the rung's curve-scale has already been applied to its bounds
/// (spec-action-catalog.md §2: "ValueSpec → curve-scaled bounds").</summary>
public sealed record CompiledActionCost(string ResourceId, ValueSpec ScaledAmount, ActionCostTiming When);

/// <summary>
/// T30 (spec-action-catalog.md §2): the runtime form the battle engine actually uses — everything an
/// <see cref="ActionRow"/> carries as raw JSON or an uncompiled spec, replaced by its compiled shape.
/// <b>Nothing here is re-derived per resolve</b>: this record is built once at load
/// (<see cref="ActionCompiler.Compile"/>) and cached (<see cref="ActionCatalog"/>) — "the battle path
/// meets no JSON, no dictionary, and no string comparison" (§ Success criteria).
/// </summary>
public sealed record CompiledAction(
    string ActionId,
    ActionKind Kind,
    int Rung,
    IReadOnlyList<ActionTag> Tags,
    bool Enabled,
    long Revision,
    bool Grantable,
    bool DefaultAttackEligible,
    string ContainerId,
    ActionEnvelope Envelope,
    CompiledTargetSpec Targeting,
    int MinRange,
    int MaxRange,
    string? RangeChannel,
    bool RequiresLineOfSight,
    ICompiledPredicate Condition,
    IReadOnlyList<CompiledActionCost> Costs,
    IReadOnlyList<ActionScopeRow> Scopes);
