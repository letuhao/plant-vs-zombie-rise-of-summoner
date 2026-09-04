namespace FusionRpg.Core.Battle.Timeline;

/// <summary>
/// What a player chose, or nothing if the window has not produced a choice yet. Returned by the
/// session layer, which owns the countdown — this module never measures time.
/// </summary>
public readonly record struct PlayerChoice(string ActionId, string? TargetKey)
{
    public bool IsNone => string.IsNullOrEmpty(ActionId);
    public static readonly PlayerChoice None = default;
}

/// <summary>
/// **T6 — the interactive intent source.** A human occupying the `Ready` dwell an actor already has.
///
/// <para><b>It adds an implementation, not an interface.</b> `IIntentSource` is already documented as
/// "the AI-policy seam the auto-resolved modes need, <b>and the player-input seam an interactive mode
/// needs</b>". The kernel's `Ready → Committed` gating, slot contention and `Passed` outcome are all
/// built; an interactive turn is a slower `TryDeclare`, not a new state machine.</para>
///
/// <para><b>Every declaration is recorded, including a timeout.</b> A timeout is a decision at a tick,
/// not a duration to re-measure — see <see cref="DecisionSource.Timeout"/>. That is what makes
/// `(setup, seed, trace)` a complete description of an interactive battle.</para>
///
/// <para><b>Replay reads the trace instead of asking.</b> Constructed with an existing trace, this
/// never consults the player at all: it replays the recorded decision for each actor, so a completed
/// trace reproduces its battle byte-identically and an AFK timeout replays as the same timeout rather
/// than as a fresh countdown that might resolve differently.</para>
/// </summary>
public sealed class InteractiveIntentSource : IIntentSource
{
    readonly IIntentSource _fallback;
    readonly Func<string, long, PlayerChoice>? _ask;
    readonly Func<string, ActionEnvelope?> _envelopeOf;
    readonly DecisionTrace _trace;
    readonly bool _replaying;

    /// <summary>Live: ask the player, fall back to the default action when the window elapses.</summary>
    public InteractiveIntentSource(
        IIntentSource fallback,
        Func<string, long, PlayerChoice> ask,
        Func<string, ActionEnvelope?> envelopeOf,
        DecisionTrace trace)
    {
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
        _envelopeOf = envelopeOf ?? throw new ArgumentNullException(nameof(envelopeOf));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _replaying = false;
    }

    /// <summary>Replay: read the trace, never the player. The battle is reproduced, not replayed live.</summary>
    public InteractiveIntentSource(
        IIntentSource fallback,
        Func<string, ActionEnvelope?> envelopeOf,
        DecisionTrace recorded)
    {
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _envelopeOf = envelopeOf ?? throw new ArgumentNullException(nameof(envelopeOf));
        _trace = recorded ?? throw new ArgumentNullException(nameof(recorded));
        _ask = null;
        _replaying = true;
    }

    public ActionIntent TryDeclare(string actorKey, long nowTick)
    {
        if (_replaying) return Replay(actorKey);

        var choice = _ask!(actorKey, nowTick);
        if (!choice.IsNone && _envelopeOf(choice.ActionId) is { } envelope)
        {
            _trace.Record(nowTick, actorKey, choice.ActionId, choice.TargetKey, DecisionSource.Player);
            return new ActionIntent(choice.ActionId, choice.TargetKey, envelope);
        }

        // The window elapsed (or named an action this actor cannot use): the default action is taken
        // and RECORDED as a timeout decision, so replay takes the same branch without re-timing it.
        var fallback = _fallback.TryDeclare(actorKey, nowTick);
        if (fallback.IsNone) return ActionIntent.None;   // genuinely nothing legal — nothing to record

        _trace.Record(nowTick, actorKey, fallback.ActionId, fallback.TargetKey, DecisionSource.Timeout);
        return fallback;
    }

    ActionIntent Replay(string actorKey)
    {
        if (_trace.NextFor(actorKey) is not { } recorded) return ActionIntent.None;
        return _envelopeOf(recorded.ActionId) is { } envelope
            ? new ActionIntent(recorded.ActionId, recorded.TargetKey, envelope)
            : ActionIntent.None;
    }
}
