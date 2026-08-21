using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.World;

/// <summary>
/// Authored starter maps. Wave 1 ships exactly one — the generator (wave 4) replaces this with
/// templates, budgets, and guard bands once the loop can tell us whether a map is any good.
///
/// `first-light` is hand-placed, so the seed is recorded rather than consumed: same template, same
/// world, every time. When generation lands, the seed starts doing work and this contract does not
/// change.
/// </summary>
public static class WorldTemplateCatalog
{
    public const string FirstLightId = "first-light";

    public static IReadOnlyList<string> All { get; } = new[] { FirstLightId };

    public static bool IsKnown(string? templateId) =>
        templateId != null && All.Contains(templateId, StringComparer.Ordinal);

    public static WorldState Build(string templateId, ulong seed, string worldId = "world-1") =>
        templateId switch
        {
            FirstLightId => WorldValidation.Validate(FirstLight(seed, worldId)),
            _ => throw new ArgumentException($"Unknown world template id '{templateId}'.")
        };

    // Faction ids
    const string Dave = "dave";
    const string Zomboss = "zomboss";
    const string Wild = "wild";

    // Guard encounter ids are opaque in wave 1 — the combat stream owns what they mean.
    const string GuardLight = "guard-light";
    const string GuardMedium = "guard-medium";
    const string GuardHeavy = "guard-heavy";

    /// <summary>
    /// Six sectors teaching the whole wave: home, two claimable neighbours (one fire, one ice), a
    /// no-base waste in the middle, a rich prize, and a nexus behind it.
    /// </summary>
    static WorldState FirstLight(ulong seed, string worldId) => new()
    {
        WorldId = worldId,
        TemplateId = FirstLightId,
        Seed = seed,
        CurrentTurn = 0,

        Factions = new WorldFaction[]
        {
            new() { FactionId = Dave, Kind = WorldFactionKind.Player, Name = "Dave" },
            new() { FactionId = Wild, Kind = WorldFactionKind.Wild, Name = "Wild", PolicyId = "stand-fast" },
            new() { FactionId = Zomboss, Kind = WorldFactionKind.Zomboss, Name = "Dr. Zomboss", PolicyId = "stand-fast" }
        }.OrderBy(f => f.FactionId, StringComparer.Ordinal).ToList(),

        Sectors = new WorldSector[]
        {
            new()
            {
                SectorId = "ash-waste", TypeId = "barren", Climate = ElementTypeId.Earth, DangerBand = 2,
                Phase = SectorPhase.Unknown, Intel = IntelState.Rumored, LayoutX = 4, LayoutY = 0,
                Slots = new WorldSlot[]
                {
                    new() { SlotIndex = 0, SlotTypeId = "wildland" },
                    new() { SlotIndex = 1, SlotTypeId = "hazard" },
                    new() { SlotIndex = 2, SlotTypeId = "material-seam", GuardWaveId = GuardMedium, GuardState = GuardState.Intact }
                }
            },
            new()
            {
                SectorId = "black-gate", TypeId = "nexus", Climate = ElementTypeId.Dark, DangerBand = 3,
                Phase = SectorPhase.Unknown, Intel = IntelState.Unknown, LayoutX = 6, LayoutY = 1,
                Slots = new WorldSlot[]
                {
                    new() { SlotIndex = 0, SlotTypeId = "seat", GuardWaveId = GuardHeavy, GuardState = GuardState.Intact },
                    new() { SlotIndex = 1, SlotTypeId = "wildland" },
                    new() { SlotIndex = 2, SlotTypeId = "spire", GuardWaveId = GuardMedium, GuardState = GuardState.Intact }
                }
            },
            new()
            {
                SectorId = "ember-hollow", TypeId = "stable", Climate = ElementTypeId.Fire, DangerBand = 1,
                Phase = SectorPhase.Unknown, Intel = IntelState.Scouted, LayoutX = 2, LayoutY = -1,
                Slots = new WorldSlot[]
                {
                    new() { SlotIndex = 0, SlotTypeId = "seat" },
                    new() { SlotIndex = 1, SlotTypeId = "wildland" },
                    new() { SlotIndex = 2, SlotTypeId = "essence-deposit", Element = ElementTypeId.Fire, GuardWaveId = GuardLight, GuardState = GuardState.Intact },
                    new() { SlotIndex = 3, SlotTypeId = "lair", GuardWaveId = GuardLight, GuardState = GuardState.Intact }
                }
            },
            new()
            {
                SectorId = "frost-mire", TypeId = "stable", Climate = ElementTypeId.Ice, DangerBand = 1,
                Phase = SectorPhase.Unknown, Intel = IntelState.Rumored, LayoutX = 2, LayoutY = 1,
                Slots = new WorldSlot[]
                {
                    new() { SlotIndex = 0, SlotTypeId = "seat" },
                    new() { SlotIndex = 1, SlotTypeId = "wildland" },
                    new() { SlotIndex = 2, SlotTypeId = "essence-deposit", Element = ElementTypeId.Ice, GuardWaveId = GuardLight, GuardState = GuardState.Intact }
                }
            },
            new()
            {
                SectorId = "homeworld", TypeId = "homeworld", Climate = null, DangerBand = 0,
                Phase = SectorPhase.Held, OwnerFactionId = Dave, Intel = IntelState.Watched,
                StabilityMilli = 1000, LayoutX = 0, LayoutY = 0,
                Slots = new WorldSlot[]
                {
                    new() { SlotIndex = 0, SlotTypeId = "seat", State = SlotState.Claimed, OwnerFactionId = Dave },
                    new() { SlotIndex = 1, SlotTypeId = "wildland", State = SlotState.Claimed, OwnerFactionId = Dave },
                    new() { SlotIndex = 2, SlotTypeId = "market", State = SlotState.Claimed, OwnerFactionId = Dave }
                }
            },
            new()
            {
                SectorId = "verdant-shelf", TypeId = "rich", Climate = ElementTypeId.Earth, DangerBand = 3,
                Phase = SectorPhase.Unknown, Intel = IntelState.Unknown, LayoutX = 6, LayoutY = -1,
                Slots = new WorldSlot[]
                {
                    new() { SlotIndex = 0, SlotTypeId = "seat" },
                    new() { SlotIndex = 1, SlotTypeId = "wildland" },
                    new() { SlotIndex = 2, SlotTypeId = "shard-vein", GuardWaveId = GuardHeavy, GuardState = GuardState.Intact },
                    new() { SlotIndex = 3, SlotTypeId = "essence-deposit", Element = ElementTypeId.Earth, GuardWaveId = GuardMedium, GuardState = GuardState.Intact }
                }
            }
        }.OrderBy(s => s.SectorId, StringComparer.Ordinal).ToList(),

        // Penny's one open lane first, then the frontier beyond it.
        Lanes = new WorldLane[]
        {
            new() { LaneId = "l-ash-black", FromSectorId = "ash-waste", ToSectorId = "black-gate", TypeId = "rift", Length = 1200, Width = 800, HazardMilli = 100 },
            new() { LaneId = "l-ash-verdant", FromSectorId = "ash-waste", ToSectorId = "verdant-shelf", TypeId = "rift", Length = 1100, Width = 1000 },
            new() { LaneId = "l-ember-ash", FromSectorId = "ember-hollow", ToSectorId = "ash-waste", TypeId = "ley", Length = 1000, Width = 600 },
            new() { LaneId = "l-frost-ash", FromSectorId = "frost-mire", ToSectorId = "ash-waste", TypeId = "rift", Length = 1000, Width = 1000 },
            new() { LaneId = "l-home-ember", FromSectorId = "homeworld", ToSectorId = "ember-hollow", TypeId = "corridor", Length = 800, Width = 1000 },
            new() { LaneId = "l-home-frost", FromSectorId = "homeworld", ToSectorId = "frost-mire", TypeId = "rift", Length = 900, Width = 1000 }
        }.OrderBy(l => l.LaneId, StringComparer.Ordinal).ToList(),

        Entities = new WorldEntity[]
        {
            new()
            {
                EntityId = "e-dave-legion-1", Kind = WorldEntityKind.Legion, OwnerFactionId = Dave,
                AtSectorId = "homeworld", Stance = "march", MovementRemaining = 1000,
                Members = new WorldEntityMember[]
                {
                    new() { SpeciesId = "peashooterzombie", Level = 1, Hp = 110 },
                    new() { SpeciesId = "conezombie", Level = 1, Hp = 110 },
                    new() { SpeciesId = "paperzombie", Level = 1, Hp = 110 }
                }
            },
            new()
            {
                EntityId = "e-wild-pack-1", Kind = WorldEntityKind.Warband, OwnerFactionId = Wild,
                AtSectorId = "ash-waste", Stance = "hold", MovementRemaining = 0,
                Members = new WorldEntityMember[]
                {
                    new() { SpeciesId = "normalzombie", Level = 2, Hp = 140 },
                    new() { SpeciesId = "flagzombie", Level = 2, Hp = 140 }
                }
            }
        }.OrderBy(e => e.EntityId, StringComparer.Ordinal).ToList()
    };
}
