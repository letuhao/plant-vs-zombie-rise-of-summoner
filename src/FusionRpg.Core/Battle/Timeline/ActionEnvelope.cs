namespace FusionRpg.Core.Battle.Timeline;

/// <summary>Cooldown families. No global cooldown — no mechanic in this game needs a GCD.</summary>
public enum CooldownClass
{
    None,
    /// <summary>Shared across a named group; the group is <see cref="ActionEnvelope.CooldownKey"/>.</summary>
    Category,
    Specific
}

/// <summary>When a cooldown starts counting. Three games, three answers — so it is declared.</summary>
public enum CooldownStart
{
    Commit,
    Resolve,
    RecoveryEnd
}

/// <summary>What can break a committed action before it resolves.</summary>
public enum Interruptible
{
    Never,
    OnCC,
    OnDamage
}

/// <summary>When the action's request is snapshotted.</summary>
public enum Commitment
{
    /// <summary>Snapshotted at commit. If the target is gone at resolve, the action fizzles.</summary>
    EarlyBound,
    /// <summary>Re-read at resolve — the action-game convention.</summary>
    LateBound,
    /// <summary>Early-bound, but retargets instead of fizzling when the target is gone.</summary>
    EarlyBoundWithFallback
}

/// <summary>
/// The timing envelope of an action — the seam the combat action program plugs into. It says
/// *when* an action occupies the timeline and *how* it is scheduled; it says nothing about what
/// the action does. Damage, targeting shapes, and effects belong to the next program.
///
/// Naming note: <see cref="TimeCostTicks"/> is a **time** quantum feeding readiness. It is not a
/// resource cost — affordability is a selectability question and lives in the intent source. The
/// field was called `Cost` in an earlier draft, which invited reading it as mana.
/// </summary>
public sealed record ActionEnvelope
{
    /// <summary>Identity. Without it a report cannot name what fired.</summary>
    public string ActionId { get; init; } = "noop";

    /// <summary>Pre-speed time quantum consumed before the actor is ready again.</summary>
    public long TimeCostTicks { get; init; }

    /// <summary>
    /// Which derived channel readiness divides by. Naming it makes "moves fast, attacks slowly"
    /// expressible; hardcoding `turn.speed` would not.
    /// </summary>
    public string SpeedChannel { get; init; } = DerivedTurnChannels.Speed;

    public long WindupTicks { get; init; }

    /// <summary>Single hit at wind-up end. Genuinely immutable, and shared rather than re-allocated.</summary>
    static readonly IReadOnlyList<long> SingleResolve =
        new System.Collections.ObjectModel.ReadOnlyCollection<long>(new long[] { 0 });

    /// <summary>
    /// Offsets from the end of wind-up at which the action applies. Default is a single hit at 0;
    /// a multi-hit combo declares several rather than being modelled as several actions (which
    /// would triple its economy, cooldown, and slot accounting).
    ///
    /// The default is a <c>ReadOnlyCollection</c>, not a bare array: <see cref="NoOp"/> is a shared
    /// static, and an <c>IReadOnlyList</c> backed by <c>long[]</c> can be cast back and mutated —
    /// which would corrupt it for every caller at once.
    /// </summary>
    public IReadOnlyList<long> ResolveOffsets { get; init; } = SingleResolve;

    public long RecoveryTicks { get; init; }

    public CooldownClass Class { get; init; } = CooldownClass.None;
    public string? CooldownKey { get; init; }
    public long CooldownTicks { get; init; }
    public CooldownStart StartsAt { get; init; } = CooldownStart.Resolve;

    /// <summary>
    /// False for movement and periodic pulses. Without this every mid-action actor holds a slot,
    /// so at `W = 1` only one actor could ever move and a slot-free regeneration tick would be
    /// inexpressible.
    /// </summary>
    public bool SlotConsuming { get; init; } = true;

    /// <summary>
    /// Scheduling override for always-first effects. Part of the sort key, so it cannot be
    /// retrofitted later without moving every golden.
    /// </summary>
    public int PriorityBand { get; init; }

    public Interruptible Interruptible { get; init; } = Interruptible.OnCC;

    /// <summary>Per-mille of accrued readiness returned when an interrupt breaks this action.</summary>
    public int InterruptRefundMilli { get; init; }

    public Commitment Commitment { get; init; } = Commitment.LateBound;

    /// <summary>
    /// The zero-length action. Proves FSM plumbing, and nothing else — with every field at zero,
    /// wind-up, commitment, and slot contention are all unobservable, so it must never be the only
    /// thing the seam is validated against.
    /// </summary>
    public static readonly ActionEnvelope NoOp = new();
}
