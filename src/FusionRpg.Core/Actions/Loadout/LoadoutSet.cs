using FusionRpg.Core.Actions;

namespace FusionRpg.Core.Actions.Loadout;

public enum LoadoutRejectionReason
{
    /// <summary>A15 froze the action set at run start (spec-loadout.md §4) — same shape as A11's
    /// discard rule and the shipped equip gate. Checked first: nothing else matters mid-run.</summary>
    MidRun,
    /// <summary>More than <see cref="LoadoutSet.MaxSize"/> entries. Rejects the whole attempt —
    /// never truncates (spec §2: "truncation silently picks a winner and the player never learns which").</summary>
    LoadoutFull,
    /// <summary>The action id repeats within the same attempt.</summary>
    DuplicateInLoadout,
    /// <summary><c>basic</c>/<c>innate</c> actions are never in the equipped set — putting one there
    /// is a category error, not a wasted slot (spec §2).</summary>
    IntrinsicNotEquippable,
    /// <summary>Not in the actor's held pool (A11) or granted set (A15).</summary>
    ActionNotHeld,
}

/// <summary>One rejection, naming the offending action id where one exists (never for
/// <see cref="LoadoutRejectionReason.MidRun"/> or <see cref="LoadoutRejectionReason.LoadoutFull"/>,
/// which are properties of the whole attempt, not of any one entry).</summary>
public readonly record struct LoadoutValidation(bool Ok, LoadoutRejectionReason? Reason, string? ActionId)
{
    public static readonly LoadoutValidation Valid = new(true, null, null);
    public static LoadoutValidation Reject(LoadoutRejectionReason reason, string? actionId = null) => new(false, reason, actionId);
}

/// <summary>
/// T21 (action-todo.md, spec-loadout.md §2): the equipped-skill set — at most 5, every one held, no
/// duplicates, no intrinsic entries. A pure validator: no persistence, no mutation. "Held" and "is
/// this actor mid-run" are Data-layer facts this module cannot read itself — both are injected
/// delegates, the same seam shape T17's <c>CostLedger</c> and T20's <c>UnlockDiscardService</c>
/// already use for the identical reason.
/// </summary>
public static class LoadoutSet
{
    public const int MaxSize = 5;

    /// <summary>
    /// Validates a proposed equipped set as a WHOLE — the first rule broken wins, and nothing about
    /// the attempt is applied when any rule fails (spec §2: "Rejects, never truncates").
    /// </summary>
    public static LoadoutValidation Validate(
        IReadOnlyList<string> actionIds,
        Func<string, bool> isHeld,
        Func<string, ActionKind> kindOf,
        Func<bool> isMidRun)
    {
        if (actionIds is null) throw new ArgumentNullException(nameof(actionIds));
        if (isHeld is null) throw new ArgumentNullException(nameof(isHeld));
        if (kindOf is null) throw new ArgumentNullException(nameof(kindOf));
        if (isMidRun is null) throw new ArgumentNullException(nameof(isMidRun));

        if (isMidRun())
            return LoadoutValidation.Reject(LoadoutRejectionReason.MidRun);

        if (actionIds.Count > MaxSize)
            return LoadoutValidation.Reject(LoadoutRejectionReason.LoadoutFull);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in actionIds)
        {
            if (!seen.Add(id))
                return LoadoutValidation.Reject(LoadoutRejectionReason.DuplicateInLoadout, id);

            // Checked before "held": an actor's own basic/innate IS always "held" in the sense that
            // it is always present, so checking held first would let an intrinsic slip through as
            // valid instead of being named as the category error it actually is.
            var kind = kindOf(id);
            if (kind == ActionKind.Basic || kind == ActionKind.Innate)
                return LoadoutValidation.Reject(LoadoutRejectionReason.IntrinsicNotEquippable, id);

            if (!isHeld(id))
                return LoadoutValidation.Reject(LoadoutRejectionReason.ActionNotHeld, id);
        }

        // Fewer than MaxSize is legal, not padded (spec testing strategy) — no lower-bound check.
        return LoadoutValidation.Valid;
    }
}
