namespace FusionRpg.Core.World.Turn;

/// <summary>
/// What a commander may order. Kinds are added by the module that implements them — movement adds
/// its own in W9 — so this list is the contract between the store, the wire, and the engine.
/// </summary>
public static class WorldCommandKinds
{
    /// <summary>Do nothing this turn. The default an absent or idle commander submits.</summary>
    public const string StandFast = "stand-fast";

    /// <summary>March a legion along an ordered lane path (world-movement).</summary>
    public const string Move = "move";

    /// <summary>Attack the guard holding one slot of the sector the legion stands in.</summary>
    public const string Clear = "clear";

    /// <summary>Take the sector the legion stands in, once nothing is left defending it.</summary>
    public const string Claim = "claim";

    /// <summary>Change a legion's posture — march, scout or hold (world-movement).</summary>
    public const string Stance = "stance";

    public static readonly IReadOnlyList<string> All = new[] { StandFast, Move, Clear, Claim, Stance };

    public static bool IsKnown(string? kind) =>
        kind != null && All.Contains(kind, StringComparer.Ordinal);
}

/// <summary>
/// One order, as plain data. Every commander — the human, Zomboss, a clan — submits this same
/// shape through the same path, which is what keeps the AI honest and the engine ignorant of who
/// is playing.
///
/// Payload fields are optional and typed rather than a JSON blob: the set is small, the store
/// serializes it, and a typo becomes a compile error instead of a runtime surprise.
/// </summary>
public sealed record WorldCommand
{
    /// <summary>The faction issuing the order.</summary>
    public string CommanderId { get; init; } = "";

    /// <summary>Unique per commander per turn — the idempotency key for submission.</summary>
    public string CommandId { get; init; } = "";

    public string Kind { get; init; } = "";

    /// <summary>Subject of the order, when it has one.</summary>
    public string? EntityId { get; init; }

    /// <summary>Required by `clear`: the sector the order is about, so a stale client is caught.</summary>
    public string? SectorId { get; init; }

    public int? SlotIndex { get; init; }

    /// <summary>The posture a `stance` order is asking for.</summary>
    public string? Stance { get; init; }

    /// <summary>Ordered lane ids for a march (W9).</summary>
    public IReadOnlyList<string> LanePath { get; init; } = Array.Empty<string>();
}
