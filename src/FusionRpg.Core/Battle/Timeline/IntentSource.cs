namespace FusionRpg.Core.Battle.Timeline;

/// <summary>
/// What an actor has decided to do. The kernel treats this as opaque beyond its timing envelope —
/// what the action *does* belongs to the combat action program.
/// </summary>
public sealed record ActionIntent(string ActionId, string? TargetKey, ActionEnvelope Envelope);

/// <summary>
/// Where intents come from. One interface serves both masters: it is the AI-policy seam the
/// auto-resolved modes need, and the player-input seam an interactive mode needs.
///
/// Returning null means "no legal action right now" — which the kernel must handle explicitly.
/// Gating `Ready -> Committed` on an intent while having no defined behavior for its absence is a
/// deadlock: under next-event advance an actor holding a slot with nothing scheduled drains the
/// queue and stops the clock.
/// </summary>
public interface IIntentSource
{
    ActionIntent? TryDeclare(string actorKey, long nowTick);
}

/// <summary>What the kernel decided to do with an actor that reached <see cref="TurnState.Ready"/>.</summary>
public enum SeatOutcome
{
    /// <summary>Intent declared and a slot taken — the actor is committing.</summary>
    Seated,
    /// <summary>No slot free. The actor stays Ready and contends again when one frees.</summary>
    NoSlot,
    /// <summary>No legal intent. The actor takes no slot and reschedules — never blocks the clock.</summary>
    Passed,
    /// <summary>Crowd-controlled — a derived read from StatusRuntime, never stored on the machine.</summary>
    Incapacitated
}

public readonly record struct SeatResult(SeatOutcome Outcome, ActionIntent? Intent);
