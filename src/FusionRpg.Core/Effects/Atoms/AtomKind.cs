namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// The seam an atom kind hooks into. Five, guarded by ADR — this is the list that keeps the
/// vocabulary finite and auditable.
/// </summary>
public enum AttachPoint
{
    Stat,
    Resource,
    Status,
    Shield,
    Board,
}

/// <summary>
/// How well one runtime can execute a kind. **Four states, not a boolean** — the audited matrix
/// has more than yes/no in it, and collapsing <see cref="PlanOnly"/> into <see cref="Full"/>
/// would make sim silently accept bindings it cannot execute.
/// </summary>
public enum RuntimeState
{
    /// <summary>No consumer. Binding here is a rejection.</summary>
    None = 0,

    /// <summary>Executes end to end.</summary>
    Full,

    /// <summary>Executes only through a named side path — e.g. status.apply in battle, setup only.</summary>
    Partial,

    /// <summary>Produces a plan, applies nothing. Accepted only by a host that declares itself a planner.</summary>
    PlanOnly,
}

/// <summary>Per-runtime support. Audited fact, re-verified against code before a cell changes.</summary>
public readonly record struct RuntimeSupportMatrix(RuntimeState Lawn, RuntimeState Battle, RuntimeState Sim)
{
    public RuntimeState For(RuntimeId runtime) => runtime switch
    {
        RuntimeId.Lawn => Lawn,
        RuntimeId.Battle => Battle,
        RuntimeId.Sim => Sim,
        _ => RuntimeState.None,
    };
}

public enum RuntimeId
{
    Lawn,
    Battle,
    Sim,
}

/// <summary>
/// The 8 triggers an atom's <c>when</c> may name. E1 owns this because E4 validates against it at
/// load and nothing else did. <c>OnTimer</c> is included even though `effect-system.md` never gave
/// it an FT number — it is real in code and in `effect-data.md`. <c>OnActivate</c> is the eighth,
/// added A18b (spec-on-activate-trigger.md) — a cross-program vocabulary change, reviewed via that
/// spec, not a unilateral addition.
/// </summary>
public static class AtomTriggers
{
    public const string OnSpawn = "OnSpawn";
    public const string OnDamageDealt = "OnDamageDealt";
    public const string OnDamageTaken = "OnDamageTaken";
    public const string OnDeath = "OnDeath";
    public const string OnGranted = "OnGranted";
    public const string OnRemoved = "OnRemoved";
    public const string OnTimer = "OnTimer";
    public const string OnActivate = "OnActivate";

    public static readonly string[] All =
        { OnSpawn, OnDamageDealt, OnDamageTaken, OnDeath, OnGranted, OnRemoved, OnTimer, OnActivate };

    /// <summary>The four that fire from a board event.</summary>
    public static readonly string[] Events = { OnSpawn, OnDamageDealt, OnDamageTaken, OnDeath };

    /// <summary>
    /// Grant attach / detach. These are <b>runtime lifecycle states, not authorable triggers</b>
    /// (definitions.md §14.2). A permanent modifier is applied when granted and reverted when
    /// removed — the bag injects the revert itself. Letting content name only the OnGranted half
    /// was how a permanent buff could leak, so no kind carries these.
    /// </summary>
    public static readonly string[] Lifecycle = { OnGranted, OnRemoved };

    /// <summary>
    /// An actor's own decision to act, independent of any board event or grant lifecycle — the third
    /// category `OnActivate` starts (A18b). Not a board event (no target has necessarily been
    /// damaged, spawned, or killed) and not a lifecycle transition (the grant that owns this atom was
    /// already bound, possibly turns ago, at loadout compile — A18a).
    /// </summary>
    public static readonly string[] Actions = { OnActivate };

    /// <summary>A permanent modifier declares no trigger at all — it is not event-driven.</summary>
    public static readonly string[] None = Array.Empty<string>();

    public static bool IsKnown(string? trigger) =>
        trigger is not null && Array.Exists(All, t => string.Equals(t, trigger, StringComparison.Ordinal));
}

/// <summary>
/// Which power categories a kind can contribute to — where a price lands, never how it is computed.
/// The cost function itself is E9's; a kind carries no hook, because a singular "magnitude" cannot
/// describe a kind holding several value specs (spawn.entity uses count as a multiplier and
/// hp/atk as the spawned body).
/// </summary>
[Flags]
public enum PowerCategory
{
    None = 0,
    Offense = 1,
    Survivability = 2,
    Control = 4,
    Utility = 8,
    Economy = 16,
}

/// <summary>
/// One kind: the mechanism, never a magnitude and never a content id. Adding a kind is a reviewed
/// code change because a kind without an executor is dead on arrival — the same rule that keeps
/// <c>status.expose.*</c> from being repeated.
/// </summary>
public sealed record AtomKind(
    string KindId,
    AttachPoint Attach,
    ParamSchema Params,
    RuntimeSupportMatrix Support,
    IReadOnlyList<string> Triggers,
    PowerCategory Categories,
    string Note = "",
    // A18e (spec-battle-live-stat-modifiers.md §4): every kind before stat.modify was either
    // Triggers.Count == 0 (no trigger allowed, none required -- the permanent-modifier case) or
    // Count > 0 (some triggers allowed, one required -- AtomRowValidator.ValidateWhen's own "mirror
    // case" inference). stat.modify's OnActivate widen needed a THIRD case neither binary covered:
    // triggers allowed, but still not required, since "permanent, no-trigger" must keep working
    // exactly as it did when Triggers was empty. Defaults false so every other kind's existing
    // Count>0-implies-required inference is completely unchanged.
    bool TriggerOptional = false)
{
    public RuntimeState SupportIn(RuntimeId runtime) => Support.For(runtime);

    /// <summary>True when this kind may carry that trigger. Unknown triggers are never "allowed".</summary>
    public bool AllowsTrigger(string? trigger) =>
        trigger is not null && Triggers.Contains(trigger, StringComparer.Ordinal);
}
