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
    public IReadOnlyList<WorldSlotDto> Slots { get; init; } = Array.Empty<WorldSlotDto>();
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
    public int LaneProgressMilli { get; init; }
    public string Stance { get; init; } = "";
    public int MovementRemaining { get; init; }
    public IReadOnlyList<WorldEntityMemberDto> Members { get; init; } = Array.Empty<WorldEntityMemberDto>();
}

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

/// <summary>SIM-only creation request.</summary>
public sealed class CreateWorldRequest
{
    public long? PlayerId { get; set; }
    public string? WorldId { get; set; }
    public string? TemplateId { get; set; }

    /// <summary>Sent as a string: a ulong seed does not survive JavaScript's number type.</summary>
    public string? Seed { get; set; }
}
