using System.Text.Json;

namespace FusionRpg.Core.World.StructureSeed;

/// <summary>
/// base-defense `structure-catalog-import` (module 25, spec-structure-catalog-import.md). The C#
/// reader for the committed JSON corpus `structure-corpus` (module 24) authored under
/// `data/seed/structures/&lt;role&gt;/&lt;id&gt;.json`, in the shared
/// `{"kind","_meta","entries":[{"id","anchor","_provenance","magnitudes"?}]}` shape
/// `seedsmith.corpus.Corpus.load`'s own Python-side loader already defines (Law 1: one shape, not
/// a second ad-hoc one per language).
///
/// <para><b>Only a row with a real <c>magnitudes</c> block is catalog-loadable</b> — see this
/// module's own spec correction 1 for why: the anchor is identity/ordinals only (structure-schema's
/// own "no numbers at all" rule), and a row's real per-structure numbers (cost, yield, etc.) live in
/// the sibling <c>magnitudes</c> key instead. A row with no <c>magnitudes</c> is registered here
/// (so callers can still see its identity/ordinals exist) but <see cref="StructureCorpusRow.IsCatalogLoadable"/>
/// is false for it, and `StructureCatalog`'s own `Configure` skips it when building `StructureDef`s.</para>
/// </summary>
public sealed record StructureMagnitudes(
    string StructureKind,
    long Cost,
    int YieldMultiplierMilli,
    int BuildTurns,
    long CapacityBonus,
    long FlatYieldPerTurn,
    long ConstructRubbleCost,
    long ConstructIronworkCost,
    int MaterialTier,
    bool BlocksMovement,
    bool BlocksLineOfFire,
    string ObstacleKind,
    int CoverPowerMilli,
    int CoverRadius,
    int EntryStaminaMultiplierMilli,
    int? VisionRangeTiles,
    string? ContainerId);

public sealed record StructureCorpusRow(
    string StructureId,
    string Name,
    string Role,
    string RequiredSlotKind,
    string StrengthBand,
    IReadOnlyList<string> AcquisitionPaths,
    bool ControlPoint,
    StructureMagnitudes? Magnitudes)
{
    public bool IsCatalogLoadable => Magnitudes is not null;
}

/// <summary>Thrown for a corpus file that fails to parse or is missing a field this reader
/// requires — a startup error, never a silent skip, matching `StructureCatalog.Validate`'s own
/// loud-over-silent stance for every other catalog rule.</summary>
public sealed class StructureCorpusLoadException : Exception
{
    public StructureCorpusLoadException(string path, string reason)
        : base($"{path}: {reason}") { }
}

public sealed class StructureCorpus
{
    public IReadOnlyList<StructureCorpusRow> Rows { get; }

    StructureCorpus(IReadOnlyList<StructureCorpusRow> rows) => Rows = rows;

    /// <summary>Walks every `*.json` file under `root`, treating each as one seed file with a
    /// single-entry `entries` array (`structure-corpus`'s own one-row-per-file convention). A file
    /// whose top level is not `{"kind": "structure-anchor", ...}` is silently not corpus content —
    /// the same "not every JSON file under a seed root is a seed file" rule
    /// `seedsmith.corpus.Corpus.load`'s own Python sibling already applies.</summary>
    public static StructureCorpus Load(string root)
    {
        var rows = new List<StructureCorpusRow>();
        if (!Directory.Exists(root)) return new StructureCorpus(rows);

        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root2 = doc.RootElement;
            if (!root2.TryGetProperty("kind", out var kindEl) || kindEl.GetString() != "structure-anchor")
                continue;
            if (!root2.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in entries.EnumerateArray())
                rows.Add(ParseRow(entry, path));
        }

        return new StructureCorpus(rows);
    }

    static StructureCorpusRow ParseRow(JsonElement entry, string path)
    {
        if (!entry.TryGetProperty("id", out var idEl))
            throw new StructureCorpusLoadException(path, "an entry has no 'id'");
        var id = idEl.GetString() ?? throw new StructureCorpusLoadException(path, "'id' is null");

        if (!entry.TryGetProperty("anchor", out var anchor))
            throw new StructureCorpusLoadException(path, $"{id}: no 'anchor' object");

        var name = RequireString(entry, "name", path, id);
        var role = RequireString(anchor, "role", path, id);
        var requiredSlotKind = RequireString(anchor, "requiredSlotKind", path, id);
        var strengthBand = RequireString(anchor, "strengthBand", path, id);
        var controlPoint = RequireBool(anchor, "controlPoint", path, id);
        var acquisitionPaths = anchor.GetProperty("acquisitionPaths").EnumerateArray()
            .Select(e => e.GetString() ?? "").ToList();

        StructureMagnitudes? magnitudes = null;
        if (entry.TryGetProperty("magnitudes", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            magnitudes = new StructureMagnitudes(
                StructureKind: RequireString(m, "structureKind", path, id),
                Cost: RequireLong(m, "cost", path, id),
                YieldMultiplierMilli: RequireInt(m, "yieldMultiplierMilli", path, id),
                BuildTurns: RequireInt(m, "buildTurns", path, id),
                CapacityBonus: RequireLong(m, "capacityBonus", path, id),
                FlatYieldPerTurn: RequireLong(m, "flatYieldPerTurn", path, id),
                ConstructRubbleCost: RequireLong(m, "constructRubbleCost", path, id),
                ConstructIronworkCost: RequireLong(m, "constructIronworkCost", path, id),
                MaterialTier: RequireInt(m, "materialTier", path, id),
                BlocksMovement: RequireBool(m, "blocksMovement", path, id),
                BlocksLineOfFire: RequireBool(m, "blocksLineOfFire", path, id),
                ObstacleKind: RequireString(m, "obstacleKind", path, id),
                CoverPowerMilli: RequireInt(m, "coverPowerMilli", path, id),
                CoverRadius: RequireInt(m, "coverRadius", path, id),
                EntryStaminaMultiplierMilli: RequireInt(m, "entryStaminaMultiplierMilli", path, id),
                VisionRangeTiles: m.GetProperty("visionRangeTiles").ValueKind == JsonValueKind.Null
                    ? null
                    : m.GetProperty("visionRangeTiles").GetInt32(),
                ContainerId: m.TryGetProperty("containerId", out var cid) && cid.ValueKind == JsonValueKind.String
                    ? cid.GetString()
                    : null);
        }

        return new StructureCorpusRow(id, name, role, requiredSlotKind, strengthBand, acquisitionPaths, controlPoint, magnitudes);
    }

    static string RequireString(JsonElement obj, string key, string path, string id) =>
        obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : throw new StructureCorpusLoadException(path, $"{id}: '{key}' is missing or not a string");

    static bool RequireBool(JsonElement obj, string key, string path, string id) =>
        obj.TryGetProperty(key, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            ? v.GetBoolean()
            : throw new StructureCorpusLoadException(path, $"{id}: '{key}' is missing or not a bool");

    static int RequireInt(JsonElement obj, string key, string path, string id) =>
        obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32()
            : throw new StructureCorpusLoadException(path, $"{id}: '{key}' is missing or not a number");

    static long RequireLong(JsonElement obj, string key, string path, string id) =>
        obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt64()
            : throw new StructureCorpusLoadException(path, $"{id}: '{key}' is missing or not a number");
}
