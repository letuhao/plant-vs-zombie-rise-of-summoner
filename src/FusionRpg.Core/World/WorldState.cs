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

    /// <summary>
    /// Loam upkeep multiplier, per-mille, 1000 = normal (spec-loam-model.md). A declared balance
    /// lever, not a cheat — hashed, replayed, and named in the turn report whenever it is not 1000.
    /// </summary>
    public int UpkeepHandicapMilli { get; init; } = 1000;

    /// <summary>
    /// buff-debuff-scope T12: a standing world-map buff/debuff on this faction, per-mille,
    /// 1000 = no modifier. Follows <see cref="UpkeepHandicapMilli"/>'s exact precedent — hashed via
    /// <c>WorldCanonical.Write</c>, replay-safe, applied by whichever future consumer's own compute
    /// path reads it (resolved during audit: `UpkeepHandicapMilli` itself has no single compute path
    /// — three independent consumers read it, each for its own reason — so this module declares and
    /// hashes the modifier without wiring itself into any consumer ahead of need).
    /// </summary>
    public int ScopeModifierMilli { get; init; } = 1000;
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

    /// <summary>
    /// What this slot carries, if anything (spec-structure-substrate.md). Null on every slot today —
    /// the mechanism ships before any content uses it.
    /// </summary>
    public string? StructureId { get; init; }

    /// <summary>
    /// Null means either no structure or a finished one. A positive count means a structure was just
    /// built and is not yet active (spec-loam-structures.md) — content for a later module, the field
    /// lands now so it hashes and persists before that module needs it.
    /// </summary>
    public int? ConstructionTurnsRemaining { get; init; }
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

    /// <summary>
    /// What this sector is holding right now (spec-loam-model.md) — stored per sector, spent per
    /// connected component (`loam-calc` owns the draw). A plain count, not a per-mille: per-mille
    /// here means rate or fraction, and a stockpile is neither. **`long`, not `int`** — one `long`
    /// operand promotes the whole upkeep expression, so there is no cast to remember and therefore
    /// none to forget; the `int` version silently overflowed into negative upkeep at legal inputs.
    /// </summary>
    public long LoamStock { get; init; }

    /// <summary>
    /// The local strength of the Fracture, per-mille, 1000 = baseline (spec-loam-model.md). The
    /// chaos gradient: the map is not uniformly dangerous, and this field is what says so.
    /// </summary>
    public int FractureIntensityMilli { get; init; } = 1000;

    /// <summary>
    /// What the **template author** decided the player already knows when the world is created —
    /// "you have heard rumours of Frost Mire". It seeds belief at turn zero and is never touched
    /// again. Live, per-faction intel lives in <see cref="WorldState.Intel"/>; this is not it, and
    /// the two are named apart on purpose.
    /// </summary>
    public IntelState AuthoredIntel { get; init; } = IntelState.Unknown;

    public int LastSeenTurn { get; init; }

    /// <summary>Authored map position — stored, because a graph you cannot picture is unusable.</summary>
    public int LayoutX { get; init; }
    public int LayoutY { get; init; }

    /// <summary>Ordered by <see cref="WorldSlot.SlotIndex"/>, contiguous from zero.</summary>
    public IReadOnlyList<WorldSlot> Slots { get; init; } = Array.Empty<WorldSlot>();

    /// <summary>
    /// Set only by a warden bind (spec-loam-texture.md); a sector with a binding is exempt from
    /// `FadePolicy` entirely while it holds — permanent, never released except by capture.
    /// </summary>
    public string? WardenBindingId { get; init; }

    /// <summary>
    /// How many consecutive turns this sector has sat <see cref="SectorPhase.Lost"/> and barren —
    /// the Unmade's spawn clock (spec-loam-texture.md). Zero on any sector that is not currently in
    /// that state; a later module resets it the moment the sector is reclaimed or grows a source.
    /// </summary>
    public int NeglectedTurns { get; init; }

    /// <summary>
    /// world-map W44 (spec-sector-development.md §1): what this sector has accrued toward founding a
    /// legion — a **stock, not a rate**, for the same reason <see cref="LoamStock"/>'s own comment
    /// gives: per-mille means rate or fraction, and a stockpile is neither. `long`, not `int`, for the
    /// same overflow reason `LoamStock` already documents. No hard cap — a throttle, if one is ever
    /// needed, is a configurable soft cap in tuning (ssot-power-scale.md §11), never a silent clamp.
    /// </summary>
    public long RecruitStock { get; init; }

    /// <summary>
    /// A sector-wide project in progress (spec-sector-development.md §3) — raises the whole sector
    /// (development, defense, capacity), never one slot's output, which is what a `WorldSlot.StructureId`
    /// is for. Null means no project. Mirrors `WorldSlot.StructureId`/`ConstructionTurnsRemaining`'s
    /// own shape exactly, one level up.
    /// </summary>
    public string? ProjectId { get; init; }

    /// <summary>Null means either no project or a finished one; must not be set without <see cref="ProjectId"/>.</summary>
    public int? ProjectTurnsRemaining { get; init; }
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

/// <summary>
/// Whether a member fights or carries (spec-loam-legions.md). Fighter is the default so every
/// member ever built before this field existed reads as one — the shape supply already assumed.
/// </summary>
public enum WorldEntityMemberRole
{
    Fighter,
    Bearer
}

public sealed record WorldEntityMember
{
    /// <summary>Roster specimen (`rpg_unique_actors`); null for non-player forces and guards.</summary>
    public string? InstanceId { get; init; }
    public string SpeciesId { get; init; } = "";
    public int Level { get; init; } = 1;
    public long Hp { get; init; }
    public int Wounds { get; init; }
    public WorldEntityMemberRole Role { get; init; } = WorldEntityMemberRole.Fighter;
}

public sealed record WorldEntity
{
    public string EntityId { get; init; } = "";
    public WorldEntityKind Kind { get; init; }
    public string OwnerFactionId { get; init; } = "";

    /// <summary>At a sector, or on a lane — never both, never neither.</summary>
    public string? AtSectorId { get; init; }

    public string? OnLaneId { get; init; }

    /// <summary>
    /// Which end of <see cref="OnLaneId"/> the legion is marching toward. A lane has a stored
    /// direction but is travelled both ways, so progress alone is ambiguous — without this, a march
    /// that ran out of budget travelling against the lane's direction would resume the other way.
    /// </summary>
    public string? OnLaneTowardSectorId { get; init; }

    /// <summary>
    /// Progress toward <see cref="OnLaneTowardSectorId"/> in per-mille, so a march resumes exactly.
    /// Always 0 for a legion standing in a sector.
    /// </summary>
    public int LaneProgressMilli { get; init; }

    public string Stance { get; init; } = "march";
    public int MovementRemaining { get; init; }

    /// <summary>
    /// Beaten in the field. A routed force keeps the ground it is on and loses exactly one turn of
    /// orders: the turn after the battle its commands are dropped, and the same turn clears the
    /// flag. Stored rather than derived — nothing else in the world remembers that a fight happened.
    /// </summary>
    public bool Routed { get; init; }
    public IReadOnlyList<WorldEntityMember> Members { get; init; } = Array.Empty<WorldEntityMember>();

    /// <summary>
    /// Entity-level pool, not per-member (spec-loam-legions.md): members carry as a crew, not as
    /// individual sacks. Zero for every entity built before this module.
    /// </summary>
    public long CarriedLoam { get; init; }
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

    /// <summary>
    /// What each faction believes, ordered by faction id (world-intel).
    ///
    /// The one piece of state here that is **not** derivable from the rest: belief is an
    /// accumulation of history, not a function of the world as it stands now, so it is stored,
    /// hashed and replayed like anything else. A faction with no entry believes nothing.
    /// </summary>
    public IReadOnlyList<Intel.FactionIntel> Intel { get; init; } = Array.Empty<Intel.FactionIntel>();
}
