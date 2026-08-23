namespace FusionRpg.Core.World;

/// <summary>
/// A category, not a building (spec-structure-substrate.md §New state). One value this wave —
/// enough to express both a Well and a Waystation, which `loam-structures` adds next.
/// </summary>
public enum StructureKind
{
    LoamSource,

    /// <summary>Raises capacity — a real third thing a structure can do (spec-loam-texture.md).</summary>
    Storage
}

/// <summary>One structure type. Mirrors <see cref="SlotTypeDef"/>'s own shape exactly.</summary>
public sealed record StructureDef
{
    public string StructureId { get; init; } = "";
    public string Name { get; init; } = "";
    public StructureKind Kind { get; init; }

    /// <summary>What kind of slot this structure must sit on.</summary>
    public SlotKind RequiredSlotKind { get; init; }

    /// <summary>Upfront build cost, spent from the building legion's own <c>CarriedLoam</c>.</summary>
    public long CostMilli { get; init; }

    /// <summary>
    /// Per-mille, 1000 = unchanged. A Well multiplies its Rootbed's existing yield; a Waystation
    /// multiplies its Seat's zero — one field expresses both (spec-structure-substrate.md).
    /// </summary>
    public int YieldMultiplierMilli { get; init; } = 1000;

    /// <summary>
    /// How many `Production` passes construction takes, decrementing to zero, before the structure
    /// is active (spec-loam-structures.md). Zero means never under construction — instant.
    /// </summary>
    public int BuildTurns { get; init; }

    /// <summary>Read only by `Storage`-kind structures — how much a granary raises a sector's cap by (spec-loam-texture.md).</summary>
    public long CapacityBonus { get; init; }
}

/// <summary>
/// What can be built onto a slot (spec-structure-substrate.md). Deliberately content-light this
/// wave: one placeholder row proves the mechanism before `loam-structures` adds Well and Waystation.
/// </summary>
public static class StructureCatalog
{
    static IReadOnlyList<StructureDef>? _all;
    static Dictionary<string, StructureDef>? _byId;

    public static IReadOnlyList<StructureDef> All => _all ??= Validate(Seed);

    public static bool IsKnown(string? structureId) =>
        structureId != null && ByIdMap().ContainsKey(structureId);

    public static StructureDef Get(string structureId) =>
        ByIdMap().TryGetValue(structureId, out var def)
            ? def
            : throw new ArgumentException($"Unknown structure id '{structureId}'.");

    static readonly IReadOnlyList<StructureDef> Seed = new StructureDef[]
    {
        new()
        {
            StructureId = "loam-source-placeholder",
            Name = "Loam Source (placeholder)",
            Kind = StructureKind.LoamSource,
            RequiredSlotKind = SlotKind.Rootbed,
            CostMilli = 0,
            YieldMultiplierMilli = 1000
        },
        new()
        {
            StructureId = "well",
            Name = "Well",
            Kind = StructureKind.LoamSource,
            RequiredSlotKind = SlotKind.Rootbed,
            CostMilli = Loam.LoamPolicy.WellCostMilli,
            YieldMultiplierMilli = Loam.LoamPolicy.WellYieldMultiplierMilli,
            BuildTurns = Loam.LoamPolicy.WellBuildTurns
        },
        new()
        {
            StructureId = "waystation",
            Name = "Waystation",
            Kind = StructureKind.LoamSource,
            RequiredSlotKind = SlotKind.Seat,
            CostMilli = Loam.LoamPolicy.WaystationCostMilli,
            // A Seat's own base yield is already zero, so the multiplier is irrelevant here —
            // 1000 (unchanged) rather than a special case in LoamProduction's formula.
            YieldMultiplierMilli = 1000,
            BuildTurns = Loam.LoamPolicy.WaystationBuildTurns
        },
        new()
        {
            StructureId = "granary",
            Name = "Granary",
            Kind = StructureKind.Storage,
            RequiredSlotKind = SlotKind.Wildland,
            CostMilli = Loam.LoamPolicy.GranaryCostMilli,
            // Unused for Storage-kind structures — a granary does not produce, it raises capacity.
            YieldMultiplierMilli = 1000,
            BuildTurns = Loam.LoamPolicy.GranaryBuildTurns,
            CapacityBonus = Loam.LoamPolicy.GranaryCapacityBonus
        }
    };

    static Dictionary<string, StructureDef> ByIdMap()
    {
        if (_byId == null)
        {
            _ = All;
            _byId = All.ToDictionary(s => s.StructureId, StringComparer.Ordinal);
        }

        return _byId;
    }

    /// <summary>Catalog discipline — a bad structure row is a startup error, never a runtime surprise.</summary>
    public static IReadOnlyList<StructureDef> Validate(IReadOnlyList<StructureDef> structures)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in structures)
        {
            WorldIds.RequireKebab(s.StructureId, "Structure id");
            if (!seenIds.Add(s.StructureId))
                throw new InvalidOperationException($"Duplicate structure id '{s.StructureId}'.");
            if (string.IsNullOrWhiteSpace(s.Name))
                throw new InvalidOperationException($"Structure '{s.StructureId}' has no display name.");
            if (s.CostMilli < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has negative cost.");
            if (s.YieldMultiplierMilli < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has a negative yield multiplier.");
        }

        return structures;
    }
}
