using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.World.Intel;

/// <summary>
/// A force as somebody else remembers it. Exact if they stood on the ground with it; a
/// <see cref="StrengthBandCatalog">band</see> if they only glimpsed it from next door.
/// </summary>
public sealed record RememberedForce
{
    public string EntityId { get; init; } = "";
    public string OwnerFactionId { get; init; } = "";
    public WorldEntityKind Kind { get; init; }

    /// <summary>True when the observer stood in the sector and counted.</summary>
    public bool Exact { get; init; }

    /// <summary>Meaningful only when <see cref="Exact"/>.</summary>
    public long Strength { get; init; }

    public int BandIndex { get; init; }

    /// <summary>What a decision should plan against: the exact figure, or the band's ceiling.</summary>
    public long Defensive => Exact ? Strength : StrengthBandCatalog.ByIndex(BandIndex).Ceiling;

    /// <summary>What a decision should expect: the exact figure, or the band's midpoint.</summary>
    public long Offensive => Exact ? Strength : StrengthBandCatalog.ByIndex(BandIndex).Midpoint;
}

/// <summary>A slot as it was last seen. Only ever recorded from a survey — a glimpse sees no slots.</summary>
public sealed record RememberedSlot
{
    public int SlotIndex { get; init; }
    public string SlotTypeId { get; init; } = "";
    public ElementTypeId? Element { get; init; }

    /// <summary>
    /// Which encounter dens here, as far as the observer could tell. Kept because `GuardState`
    /// alone cannot distinguish "cleared, and something used to live here" from "never guarded" —
    /// and because someone who walked the ground would know what they were looking at.
    /// </summary>
    public string? GuardWaveId { get; init; }

    /// <summary>Whether it has been claimed, worked out, or ruined — readable from the ground.</summary>
    public SlotState State { get; init; } = SlotState.Intact;

    public GuardState GuardState { get; init; }
}

/// <summary>
/// One sector as one faction last saw it (spec-world-intel.md §Remembering).
///
/// Deliberately not the whole sector record: remembering the truth exactly would make fog cosmetic.
/// What is here is what an observer could plausibly carry away and report.
/// </summary>
public sealed record IntelSnapshot
{
    public string SectorId { get; init; } = "";

    /// <summary>The turn this was taken. The whole ladder is derived from it.</summary>
    public int LastSeenTurn { get; init; }

    /// <summary>How well it was seen when the snapshot was taken — a glimpse carries no slots.</summary>
    public SectorSight Detail { get; init; }

    public string? OwnerFactionId { get; init; }
    public SectorPhase Phase { get; init; }
    public ElementTypeId? Climate { get; init; }
    public int DangerBand { get; init; }

    /// <summary>
    /// How built-up it was when you last stood on it. Zero for a glimpse — you read development off
    /// the ground, not from one sector away — and the wire says so rather than reporting a bare zero
    /// that looks identical to undeveloped.
    /// </summary>
    public int DevelopmentLevel { get; init; }

    /// <summary>Ordered by slot index. Empty for a glimpse.</summary>
    public IReadOnlyList<RememberedSlot> Slots { get; init; } = Array.Empty<RememberedSlot>();

    /// <summary>Ordered by entity id.</summary>
    public IReadOnlyList<RememberedForce> Forces { get; init; } = Array.Empty<RememberedForce>();
}

/// <summary>
/// Everything one faction believes about the map. Ordered by sector id, and holding only sectors it
/// has actually seen — an absent entry *is* "never heard of it".
/// </summary>
public sealed record FactionIntel
{
    public string FactionId { get; init; } = "";
    public IReadOnlyList<IntelSnapshot> Sectors { get; init; } = Array.Empty<IntelSnapshot>();

    public IntelSnapshot? Of(string sectorId)
    {
        foreach (var snapshot in Sectors)
            if (string.Equals(snapshot.SectorId, sectorId, StringComparison.Ordinal))
                return snapshot;

        return null;
    }
}

/// <summary>
/// The four-state ladder, **derived** from `(lastSeenTurn, currentTurn, seenThisTurn)` rather than
/// stored — so it cannot drift from the data underneath it.
/// </summary>
public static class IntelLadder
{
    /// <summary>Memory counts as fresh for this long before it decays to rumour.</summary>
    public const int FreshTurns = 5;

    public static IntelState StateOf(IntelSnapshot? snapshot, int currentTurn, bool seenThisTurn)
    {
        if (seenThisTurn) return IntelState.Watched;
        if (snapshot is null) return IntelState.Unknown;

        var age = currentTurn - snapshot.LastSeenTurn;
        return age <= FreshTurns ? IntelState.Scouted : IntelState.Rumored;
    }

    /// <summary>How many turns stale, floored at zero. What the map stamps on a remembered card.</summary>
    public static int AgeOf(IntelSnapshot snapshot, int currentTurn) =>
        Math.Max(0, currentTurn - snapshot.LastSeenTurn);
}
