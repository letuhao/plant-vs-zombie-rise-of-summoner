using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Actions;

/// <summary>
/// One row per action (spec-action-model.md §2). Timing and cooldown are the existing
/// <see cref="ActionEnvelope"/> — not a second copy — folded in as `A5`'s three envelope gaps land.
///
/// <para>Range (<see cref="MinRange"/>/<see cref="MaxRange"/>/<see cref="RangeChannel"/>) lives here,
/// not on <see cref="ActionTargetSpec"/> — spec-targeting.md: it gates whether the action may be
/// used at all (`A4`) as well as which targets qualify, so it is not a property of the target rule
/// alone.</para>
/// </summary>
public sealed record ActionRow
{
    // ---- identity --------------------------------------------------------------------------
    public string ActionId { get; init; } = "";
    public string Name { get; init; } = "";
    public ActionKind Kind { get; init; } = ActionKind.Skill;

    /// <summary>Indexes `A12`'s rung table. Never a magnitude — the table holds the multipliers.</summary>
    public int Rung { get; init; }

    public IReadOnlyList<ActionTag> Tags { get; init; } = Array.Empty<ActionTag>();
    public bool Enabled { get; init; } = true;
    public long Revision { get; init; }

    // ---- grant -----------------------------------------------------------------------------
    public bool Grantable { get; init; }
    public bool DefaultAttackEligible { get; init; }

    // ---- effects ---------------------------------------------------------------------------
    public string ContainerId { get; init; } = "";

    // ---- timing / cooldown — the existing envelope, reused verbatim -----------------------
    public ActionEnvelope Envelope { get; init; } = ActionEnvelope.NoOp;

    // ---- targeting ---------------------------------------------------------------------------
    public ActionTargetSpec Targeting { get; init; } = new();

    /// <summary>With no board, every range check passes — not an error, not empty (spec-targeting.md §4).</summary>
    public int MinRange { get; init; }
    public int MaxRange { get; init; }

    /// <summary>Which derived channel range reads, mirroring `ActionEnvelope.SpeedChannel`'s shape.</summary>
    public string? RangeChannel { get; init; }

    public bool RequiresLineOfSight { get; init; }

    // ---- usability -----------------------------------------------------------------------------
    /// <summary>Raw predicate JSON — compiled through E3, not here (A4).</summary>
    public string? ConditionsJson { get; init; }
}

/// <summary>
/// `(action_id, resource_id, amount_spec, when)` — spec-action-model.md §3. A table, not columns,
/// because one action may cost several resources.
/// </summary>
public sealed record ActionCostRow(
    string ActionId, string ResourceId, ValueSpec AmountSpec, ActionCostTiming When);

/// <summary>
/// `(action_id, atom_id, scope)` — spec-action-model.md §4. Rows are optional; an atom with no row
/// defaults to <see cref="ActionEffectScope.EachTarget"/>.
/// </summary>
public sealed record ActionScopeRow(string ActionId, string AtomId, ActionEffectScope Scope);

/// <summary>
/// `rpg_action_grant(owner_kind, owner_key, action_id, source, grant_role, ...)` — spec-action-model.md
/// §5. A NEW table, not `effect_binding`: a granted action has no instance and no rolls.
/// </summary>
public sealed record ActionGrantRow(
    OwnerKind OwnerKind, string OwnerKey, string ActionId, string Source, string GrantRole = "");

/// <summary>
/// The source of an actor's three basics plus its innate (spec-action-model.md §1, §9). A narrow,
/// action-program-owned table keyed on an opaque `species_key` the caller supplies — deliberately not
/// a join into the generated demon-species catalog, which this program does not own and must not edit.
/// </summary>
public sealed record SpeciesBasicsRow(
    string SpeciesKey, string AttackActionId, string GuardActionId, string MoveActionId, string? InnateActionId);
