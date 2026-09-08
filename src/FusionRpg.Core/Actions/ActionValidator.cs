using FusionRpg.Core.Actions.Movement;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Actions;

/// <summary>
/// Judges the four `rpg_action*` tables before they reach storage (spec-action-model.md §9). Same
/// law as the atom program's validators: a bad row is rejected whole, with its id and reason, never
/// coerced into something valid.
///
/// <para>Every lookup is a function the caller supplies — this stays free of I/O, exactly like
/// <c>ContainerValidator</c>.</para>
/// </summary>
public static class ActionValidator
{
    static readonly HashSet<string> ResourceIds =
        new(DerivedStatChannels.ResourceIds, StringComparer.Ordinal);

    /// <param name="containerAtomIds">
    /// Atom ids the row's `container_id` actually holds, or <c>null</c> if the container itself is
    /// unknown. Supplied by the store so this stays free of I/O.
    /// </param>
    /// <param name="boardAvailable">
    /// Whether a battle board exists. False until `A10` lands — every action but an `Area` one stays
    /// usable regardless; an `Area` action is rejected loudly rather than silently (A1 §8).
    /// </param>
    public static ActionRejection ValidateAction(
        ActionRow row, IReadOnlyCollection<string>? containerAtomIds, bool boardAvailable)
    {
        if (string.IsNullOrWhiteSpace(row.ActionId))
            return Fail(row.ActionId, ActionRejectionReason.BadActionId, "action_id is empty");

        if (containerAtomIds is null)
            return Fail(row.ActionId, ActionRejectionReason.UnknownContainer, row.ContainerId);

        if (row.MinRange > row.MaxRange)
            return Fail(row.ActionId, ActionRejectionReason.InvalidRange,
                $"min_range {row.MinRange} > max_range {row.MaxRange}");

        if (row.Targeting.Mode == ActionTargetMode.Area && !boardAvailable)
            return Fail(row.ActionId, ActionRejectionReason.AreaRequiresBoard,
                "an Area action needs cells to enumerate, and no board exists yet");

        return ActionRejection.Ok;
    }

    public static ActionRejection ValidateCost(ActionCostRow cost)
    {
        if (!ResourceIds.Contains(cost.ResourceId))
            return Fail(cost.ActionId, ActionRejectionReason.UnknownResource, cost.ResourceId);

        var spec = cost.AmountSpec.Validate();
        if (!spec.IsOk)
            return Fail(cost.ActionId, ActionRejectionReason.BadValueSpec, spec.Detail);

        return ActionRejection.Ok;
    }

    /// <param name="containerAtomIds">The atom ids the scope row's action's container holds.</param>
    public static ActionRejection ValidateScope(ActionScopeRow scope, IReadOnlyCollection<string> containerAtomIds)
    {
        if (!containerAtomIds.Contains(scope.AtomId))
            return Fail(scope.ActionId, ActionRejectionReason.ScopeAtomNotInContainer, scope.AtomId);

        return ActionRejection.Ok;
    }

    /// <param name="lookupAction">Resolves an action id against the loaded catalog.</param>
    public static ActionRejection ValidateGrant(ActionGrantRow grant, Func<string, ActionRow?> lookupAction)
    {
        var owner = OwnerScope.Validate(grant.OwnerKind, grant.OwnerKey, out _);
        if (!owner.IsOk)
            return Fail(grant.ActionId, ActionRejectionReason.BadOwnerKey, owner.Detail);

        var action = lookupAction(grant.ActionId);
        if (action is null)
            return Fail(grant.ActionId, ActionRejectionReason.UnknownContainer, "grant names an unknown action");

        // A basic is intrinsic on every actor already — a grant naming one would double-count it,
        // and worse, would make "is this intrinsic" depend on whether a grant happened to exist.
        if (action.Kind == ActionKind.Basic)
            return Fail(grant.ActionId, ActionRejectionReason.BasicCollision,
                $"'{grant.ActionId}' is a basic action and is never granted");

        if (!action.Grantable)
            return Fail(grant.ActionId, ActionRejectionReason.ActionNotGrantable, grant.ActionId);

        return ActionRejection.Ok;
    }

    /// <param name="lookupAction">Resolves an action id against the loaded catalog.</param>
    public static ActionRejection ValidateSpeciesBasics(SpeciesBasicsRow row, Func<string, ActionRow?> lookupAction)
    {
        if (string.IsNullOrWhiteSpace(row.AttackActionId)
            || string.IsNullOrWhiteSpace(row.GuardActionId)
            || string.IsNullOrWhiteSpace(row.MoveActionId))
            return Fail(row.SpeciesKey, ActionRejectionReason.MissingSpeciesBasic,
                $"species '{row.SpeciesKey}' is missing one of attack/guard/move");

        var check = CheckBasicSlot(row.SpeciesKey, "attack", row.AttackActionId, lookupAction);
        if (!check.IsOk) return check;
        check = CheckBasicSlot(row.SpeciesKey, "guard", row.GuardActionId, lookupAction);
        if (!check.IsOk) return check;
        check = CheckBasicSlot(row.SpeciesKey, "move", row.MoveActionId, lookupAction);
        if (!check.IsOk) return check;

        if (!string.IsNullOrWhiteSpace(row.InnateActionId))
        {
            var innate = lookupAction(row.InnateActionId!);
            if (innate is null)
                return Fail(row.SpeciesKey, ActionRejectionReason.UnknownContainer,
                    $"species '{row.SpeciesKey}' innate '{row.InnateActionId}' does not exist");
            if (innate.Kind != ActionKind.Innate)
                return Fail(row.SpeciesKey, ActionRejectionReason.BasicKindMismatch,
                    $"species '{row.SpeciesKey}' innate slot names '{row.InnateActionId}', which is not kind=innate");
        }

        return ActionRejection.Ok;
    }

    /// <summary>
    /// A-M1 (spec-movement-payload.md §2, §5 AC6): a compiled `category = Movement` action whose
    /// container carries no bound effect atom is refused, naming the action id and the reason. Runs
    /// against a <see cref="CompiledAction"/>, not a row — <see cref="MovementPayloadPolicy.HasStandalonePayload"/>
    /// reads <see cref="CompiledAction.Scopes"/>, which only exists post-compile, so this is a second,
    /// standalone entry point rather than a stage inside <see cref="ActionCompiler.Compile"/> — the
    /// same shape <see cref="ValidateGrant"/>/<see cref="ValidateSpeciesBasics"/> already use for a
    /// check that needs more than the three tables <c>Compile</c> itself reads. A non-Movement action
    /// always passes: invariant 9 (standalone-first) is a Movement-only rule, never a general one.
    /// </summary>
    public static ActionRejection ValidateMovementPayload(CompiledAction action, MovementPayloadPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(policy);

        if (action.Category != ActionCategory.Movement)
            return ActionRejection.Ok;

        return policy.HasStandalonePayload(action)
            ? ActionRejection.Ok
            : Fail(action.ActionId, ActionRejectionReason.MovementActionHasNoStandalonePayload,
                "a movement action must do something with the game closed");
    }

    static ActionRejection CheckBasicSlot(
        string speciesKey, string slot, string actionId, Func<string, ActionRow?> lookupAction)
    {
        var action = lookupAction(actionId);
        if (action is null)
            return Fail(speciesKey, ActionRejectionReason.UnknownContainer,
                $"species '{speciesKey}' {slot} '{actionId}' does not exist");
        if (action.Kind != ActionKind.Basic)
            return Fail(speciesKey, ActionRejectionReason.BasicKindMismatch,
                $"species '{speciesKey}' {slot} slot names '{actionId}', which is not kind=basic");
        return ActionRejection.Ok;
    }

    static ActionRejection Fail(string id, ActionRejectionReason reason, string detail) =>
        ActionRejection.Fail(reason, $"{id}: {detail}");
}
