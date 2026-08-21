using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.World;

/// <summary>How far along a sector's life it is (spec-world-model.md §The sector's life).</summary>
public enum SectorPhase
{
    Unknown,
    Explored,
    Contested,
    Held,
    Developed,
    Besieged,
    Lost
}

/// <summary>What the player knows versus what is true — the gap is the drama, so it is stored.</summary>
public enum IntelState
{
    Unknown,
    Rumored,
    Scouted,
    Watched
}

public enum SlotState
{
    Intact,
    Claimed,
    Depleted,
    Ruined
}

/// <summary>A slot's guard defends the thing, not the ground — cleared by a `clear` order.</summary>
public enum GuardState
{
    Intact,
    Cleared
}

public enum LaneState
{
    Open,
    Severed
}

/// <summary>
/// Every mobile on the map answers the same three questions — where, whose, made of what — so they
/// share one shape instead of one table per creature class.
/// </summary>
public enum WorldEntityKind
{
    Legion,
    Warband,
    Guard,
    Caravan,
    Warlord
}

public sealed record WorldFaction
{
    public string FactionId { get; init; } = "";
    public WorldFactionKind Kind { get; init; }
    public string Name { get; init; } = "";

    /// <summary>AI policy id; null for the human commander.</summary>
    public string? PolicyId { get; init; }
}

public sealed record WorldSlot
{
    public int SlotIndex { get; init; }
    public string SlotTypeId { get; init; } = "";

    /// <summary>Overrides the sector's climate — a fire vein in an ice sector is a contested pocket.</summary>
    public ElementTypeId? Element { get; init; }

    public SlotState State { get; init; } = SlotState.Intact;
    public string? OwnerFactionId { get; init; }

    /// <summary>
    /// Opaque encounter id (wave 1, owner decision): the combat stream owns what it means and how
    /// hard it is. Null means unguarded.
    /// </summary>
    public string? GuardWaveId { get; init; }

    public GuardState GuardState { get; init; } = GuardState.Cleared;
}

public sealed record WorldSector
{
    public string SectorId { get; init; } = "";
    public string TypeId { get; init; } = "";

    /// <summary>The era climate. Null only for the homeworld, which the fracture never touched.</summary>
    public ElementTypeId? Climate { get; init; }

    public int DangerBand { get; init; }
    public SectorPhase Phase { get; init; } = SectorPhase.Unknown;
    public string? OwnerFactionId { get; init; }

    // Living-world state, integer per-mille.
    public int StabilityMilli { get; init; }
    public int PressureMilli { get; init; }
    public int DepletionMilli { get; init; }
    public int DevelopmentLevel { get; init; }

    public IntelState Intel { get; init; } = IntelState.Unknown;
    public int LastSeenTurn { get; init; }

    /// <summary>Authored map position — stored, because a graph you cannot picture is unusable.</summary>
    public int LayoutX { get; init; }
    public int LayoutY { get; init; }

    /// <summary>Ordered by <see cref="WorldSlot.SlotIndex"/>, contiguous from zero.</summary>
    public IReadOnlyList<WorldSlot> Slots { get; init; } = Array.Empty<WorldSlot>();
}

public sealed record WorldLane
{
    public string LaneId { get; init; } = "";
    public string FromSectorId { get; init; } = "";
    public string ToSectorId { get; init; } = "";
    public string TypeId { get; init; } = "";

    /// <summary>March distance in per-mille units of a turn's base movement.</summary>
    public int Length { get; init; } = 1000;

    /// <summary>How large a force crosses at once — the knob that makes a chokepoint real.</summary>
    public int Width { get; init; } = 1000;

    public int HazardMilli { get; init; }
    public int WardLevel { get; init; }
    public string? GateKeyId { get; init; }
    public LaneState State { get; init; } = LaneState.Open;
}

public sealed record WorldEntityMember
{
    /// <summary>Roster specimen (`rpg_unique_actors`); null for non-player forces and guards.</summary>
    public string? InstanceId { get; init; }
    public string SpeciesId { get; init; } = "";
    public int Level { get; init; } = 1;
    public int Hp { get; init; }
    public int Wounds { get; init; }
}

public sealed record WorldEntity
{
    public string EntityId { get; init; } = "";
    public WorldEntityKind Kind { get; init; }
    public string OwnerFactionId { get; init; } = "";

    /// <summary>At a sector, or on a lane — never both, never neither.</summary>
    public string? AtSectorId { get; init; }

    public string? OnLaneId { get; init; }

    /// <summary>Progress along <see cref="OnLaneId"/> in per-mille, so a march resumes exactly.</summary>
    public int LaneProgressMilli { get; init; }

    public string Stance { get; init; } = "march";
    public int MovementRemaining { get; init; }
    public IReadOnlyList<WorldEntityMember> Members { get; init; } = Array.Empty<WorldEntityMember>();
}

/// <summary>
/// The whole map, in memory. Every collection is in stable id order — determinism starts at the
/// model, before the engine is involved.
/// </summary>
public sealed record WorldState
{
    public string WorldId { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public ulong Seed { get; init; }
    public int CurrentTurn { get; init; }

    public IReadOnlyList<WorldFaction> Factions { get; init; } = Array.Empty<WorldFaction>();
    public IReadOnlyList<WorldSector> Sectors { get; init; } = Array.Empty<WorldSector>();
    public IReadOnlyList<WorldLane> Lanes { get; init; } = Array.Empty<WorldLane>();
    public IReadOnlyList<WorldEntity> Entities { get; init; } = Array.Empty<WorldEntity>();
}
