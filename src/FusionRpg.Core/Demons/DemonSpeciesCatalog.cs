using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Demons;

/// <summary>One demon species — generated from captured game data, output checked in (spec-demon-core.md).</summary>
public sealed record DemonSpeciesDef
{
    // Core is net6 / C# 10 — no `required`; Validate() rejects unset/invalid fields instead.
    public string SpeciesId { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>Linked capture side ("plant" | "zombie") — portrait/body source.</summary>
    public string Side { get; init; } = "";
    /// <summary>The PvZ type id whose art/dumps this species wears.</summary>
    public int GameTypeId { get; init; }
    /// <summary>Demon type id in the disjoint id space (≥ 10000) — used by web-battle events.</summary>
    public int DemonTypeId { get; init; }
    public ElementTypeId ElementPrimary { get; init; }
    public ElementTypeId? ElementSecondary { get; init; }
    public DemonRarity BaseRarity { get; init; }
    public DemonDeployMode DeployMode { get; init; }
    public DemonAcquisition Acquisition { get; init; }
    public IReadOnlyList<string> Variants { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TraitPool { get; init; } = Array.Empty<string>();
}

public static partial class DemonSpeciesCatalog
{
    /// <summary>Disjoint id-space floor: web-battle events must never collide with PvZ type ids.</summary>
    public const int DemonTypeIdFloor = 10_000;

    public static readonly IReadOnlyList<string> KnownVariants = new[]
    {
        "normal", "ancient", "mutated", "corrupted", "blessed", "cursed", "shiny"
    };

    static IReadOnlyList<DemonSpeciesDef>? _all;
    static Dictionary<string, DemonSpeciesDef>? _byId;

    /// <summary>Generated seed roster (DemonSpeciesCatalog.Generated.cs), validated on first touch.</summary>
    public static IReadOnlyList<DemonSpeciesDef> All => _all ??= Validate(GeneratedSpecies);

    public static bool IsKnown(string? speciesId) =>
        speciesId != null && ByIdMap().ContainsKey(speciesId);

    public static DemonSpeciesDef Get(string speciesId) =>
        ByIdMap().TryGetValue(speciesId, out var def)
            ? def
            : throw new ArgumentException($"Unknown demon species id '{speciesId}'.");

    static Dictionary<string, DemonSpeciesDef> ByIdMap()
    {
        if (_byId == null)
        {
            _ = All;
            _byId = All.ToDictionary(s => s.SpeciesId, StringComparer.Ordinal);
        }

        return _byId;
    }

    /// <summary>Catalog discipline — a bad species is a startup error, never a runtime surprise.</summary>
    public static IReadOnlyList<DemonSpeciesDef> Validate(IReadOnlyList<DemonSpeciesDef> species)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenTypeIds = new HashSet<int>();
        foreach (var s in species)
        {
            if (string.IsNullOrWhiteSpace(s.SpeciesId) || s.SpeciesId != s.SpeciesId.Trim().ToLowerInvariant())
                throw new InvalidOperationException($"Species id '{s.SpeciesId}' must be non-empty lower-kebab.");
            if (!seenIds.Add(s.SpeciesId))
                throw new InvalidOperationException($"Duplicate species id '{s.SpeciesId}'.");
            if (s.DemonTypeId < DemonTypeIdFloor)
                throw new InvalidOperationException($"Species '{s.SpeciesId}' demonTypeId {s.DemonTypeId} below floor {DemonTypeIdFloor}.");
            if (!seenTypeIds.Add(s.DemonTypeId))
                throw new InvalidOperationException($"Duplicate demonTypeId {s.DemonTypeId} ('{s.SpeciesId}').");
            if (s.Side is not ("plant" or "zombie"))
                throw new InvalidOperationException($"Species '{s.SpeciesId}' side must be plant|zombie.");
            if (s.Acquisition == DemonAcquisition.None)
                throw new InvalidOperationException($"Species '{s.SpeciesId}' has no acquisition flags.");
            if (s.ElementSecondary is { } sec && sec == s.ElementPrimary)
                throw new InvalidOperationException($"Species '{s.SpeciesId}' primary == secondary element.");
            foreach (var v in s.Variants)
                if (!KnownVariants.Contains(v, StringComparer.Ordinal))
                    throw new InvalidOperationException($"Species '{s.SpeciesId}' unknown variant '{v}'.");
            foreach (var t in s.TraitPool)
                if (!DemonTraitCatalog.IsKnown(t))
                    throw new InvalidOperationException($"Species '{s.SpeciesId}' unknown trait '{t}'.");
        }

        return species;
    }
}
