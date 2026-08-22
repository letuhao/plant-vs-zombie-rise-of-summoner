namespace FusionRpg.Contracts;

/// <summary>
/// Wire shapes for the world map (spec-world-model.md §Server). Read-only projections: the seed is
/// deliberately absent — it is the input to every future roll, and a client that knows it can
/// predict outcomes the server has not committed yet.
/// </summary>
public sealed record WorldHeaderDto
{
    public string WorldId { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public int CurrentTurn { get; init; }
    public string State { get; init; } = "";
    public string CreatedUtc { get; init; } = "";
    public long Revision { get; init; }
}

public sealed record WorldFactionDto
{
    public string FactionId { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed record WorldSlotDto
{
    public int SlotIndex { get; init; }
    public string SlotTypeId { get; init; } = "";
    public string? Element { get; init; }
    public string State { get; init; } = "";
    public string? OwnerFactionId { get; init; }
    public string? GuardWaveId { get; init; }
    public string GuardState { get; init; } = "";
}

/// <summary>
/// A force as the viewer believes it to be. Exact when they were standing on the ground with it; a
/// band when they only glimpsed it from next door — an exact figure from a distance would make fog
/// cosmetic, and a bare "something is there" gives nobody anything to decide with.
/// </summary>
public sealed record WorldForceDto
{
    public string EntityId { get; init; } = "";
    public string OwnerFactionId { get; init; } = "";
    public string Kind { get; init; } = "";
    public bool Exact { get; init; }

    /// <summary>Meaningful only when <see cref="Exact"/>; zero otherwise.</summary>
    public long Strength { get; init; }

    public string BandName { get; init; } = "";

    /// <summary>What to plan against when being wrong would be fatal.</summary>
    public long BandCeiling { get; init; }
}

public sealed record WorldSectorDto
{
    public string SectorId { get; init; } = "";
    public string TypeId { get; init; } = "";
    public string? Climate { get; init; }
    public int DangerBand { get; init; }
    public string Phase { get; init; } = "";
    public string? OwnerFactionId { get; init; }
    public int StabilityMilli { get; init; }
    public int PressureMilli { get; init; }
    public int DepletionMilli { get; init; }
    public int DevelopmentLevel { get; init; }
    public string Intel { get; init; } = "";
    public int LastSeenTurn { get; init; }
    public int LayoutX { get; init; }
    public int LayoutY { get; init; }
    /// <summary>Turns since this was last seen. Zero while it is in sight.</summary>
    public int IntelAge { get; init; }

    /// <summary>Empty unless the viewer has actually stood here — a glimpse sees no slots.</summary>
    public IReadOnlyList<WorldSlotDto> Slots { get; init; } = Array.Empty<WorldSlotDto>();

    /// <summary>What the viewer believes is here. Empty where it can see nothing.</summary>
    public IReadOnlyList<WorldForceDto> Forces { get; init; } = Array.Empty<WorldForceDto>();

    /// <summary>
    /// What losing this would cost the viewer's own empire — the lifeline overlay
    /// (world-topology). Zero for anything the viewer does not hold, because it is computed over
    /// their holdings and tells them nothing about anyone else's.
    /// </summary>
    public long LifelineCost { get; init; }

    /// <summary>True when losing this sector would cut the viewer's territory in two.</summary>
    public bool Lifeline { get; init; }
}

public sealed record WorldLaneDto
{
    public string LaneId { get; init; } = "";
    public string FromSectorId { get; init; } = "";
    public string ToSectorId { get; init; } = "";
    public string TypeId { get; init; } = "";
    public int Length { get; init; }
    public int Width { get; init; }
    public int HazardMilli { get; init; }
    public int WardLevel { get; init; }
    public string State { get; init; } = "";
}

public sealed record WorldEntityMemberDto
{
    public string? InstanceId { get; init; }
    public string SpeciesId { get; init; } = "";
    public int Level { get; init; }
    public int Hp { get; init; }
    public int Wounds { get; init; }
}

public sealed record WorldEntityDto
{
    public string EntityId { get; init; } = "";
    public string Kind { get; init; } = "";
    public string OwnerFactionId { get; init; } = "";
    public string? AtSectorId { get; init; }
    public string? OnLaneId { get; init; }
    public string? OnLaneTowardSectorId { get; init; }
    public int LaneProgressMilli { get; init; }
    public string Stance { get; init; } = "";
    public int MovementRemaining { get; init; }
    public bool Routed { get; init; }
    public IReadOnlyList<WorldEntityMemberDto> Members { get; init; } = Array.Empty<WorldEntityMemberDto>();
}

/// <summary>
/// The map as one faction knows it. `Sectors` carries belief; `Entities` carries **only the viewer's
/// own forces**, in full, because you always know what you brought. Everything anyone else has is in
/// its sector's <see cref="WorldSectorDto.Forces"/> at whatever detail it was seen.
/// </summary>
public sealed record WorldStateDto
{
    public string WorldId { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public int CurrentTurn { get; init; }
    public IReadOnlyList<WorldFactionDto> Factions { get; init; } = Array.Empty<WorldFactionDto>();
    public IReadOnlyList<WorldSectorDto> Sectors { get; init; } = Array.Empty<WorldSectorDto>();
    public IReadOnlyList<WorldLaneDto> Lanes { get; init; } = Array.Empty<WorldLaneDto>();
    public IReadOnlyList<WorldEntityDto> Entities { get; init; } = Array.Empty<WorldEntityDto>();
}

/// <summary>One order on the wire. Mirrors the Core command shape, kept flat for the FE.</summary>
public sealed class WorldCommandRequest
{
    public string? CommandId { get; set; }
    public string? Kind { get; set; }
    public string? EntityId { get; set; }
    public string? SectorId { get; set; }
    public int? SlotIndex { get; set; }

    /// <summary>The posture a `stance` order asks for: march, scout or hold.</summary>
    public string? Stance { get; set; }

    public List<string>? LanePath { get; set; }
}

/// <summary>
/// A commander's orders for the current turn. `CommanderId` may be omitted — the server then files
/// them for the world's player faction, which is what an FE almost always means.
/// </summary>
public sealed class SubmitWorldCommandsRequest
{
    public string? CommanderId { get; set; }
    public List<WorldCommandRequest>? Commands { get; set; }
}

public sealed record WorldCommandResultDto
{
    public string CommandId { get; init; } = "";
    public bool Ok { get; init; }
    public string Reason { get; init; } = "";
    public bool Replayed { get; init; }
}

/// <summary>One commander's end-turn. The turn advances only when the last of them commits.</summary>
public sealed class CommitWorldTurnRequest
{
    public string? CommanderId { get; set; }
}

public sealed record WorldTurnCommitDto
{
    public bool Ok { get; init; }
    public string Reason { get; init; } = "";

    /// <summary>True on the commit that actually stepped the world — at most one per turn.</summary>
    public bool Advanced { get; init; }

    public string? StateHash { get; init; }

    /// <summary>The turn now open for orders.</summary>
    public int CurrentTurn { get; init; }
}

/// <summary>One line of a turn report, as the map view plays it back.</summary>
public sealed record WorldTurnEntryDto
{
    public string Phase { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed record WorldTurnReportDto
{
    public int Turn { get; init; }
    public string StateHash { get; init; } = "";
    public IReadOnlyList<string> Phases { get; init; } = Array.Empty<string>();
    public IReadOnlyList<WorldTurnEntryDto> Entries { get; init; } = Array.Empty<WorldTurnEntryDto>();
}

/// <summary>SIM-only creation request.</summary>
public sealed class CreateWorldRequest
{
    public long? PlayerId { get; set; }
    public string? WorldId { get; set; }
    public string? TemplateId { get; set; }

    /// <summary>Sent as a string: a ulong seed does not survive JavaScript's number type.</summary>
    public string? Seed { get; set; }
}
