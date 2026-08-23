using System.Text.Json;

namespace FusionRpg.Core.Combat;

/// <summary>Combat balance surface (tunables-ssot.md T1) — loaded, not hard-coded. See
/// <see cref="CombatPolicy.Configure"/> and <see cref="CombatTuningLoader"/>. LastCol/LastRow are
/// board geometry (Lawn.LawnCoordMath), not balance, and are not part of this file.</summary>
public sealed record CombatTuning(
    int SchemaVersion, int Version,
    int ProcDepthLimit, int DefaultMaxTargets,
    int AreaDefaultSquareSize, int AreaDefaultRectangleWidth, int AreaDefaultRectangleHeight,
    int DotDefaultPeriodMs, int DotDefaultDurationMs);

public sealed class CombatTuningRejection : Exception
{
    public CombatTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class CombatTuningLoader
{
    public static CombatTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new CombatTuningRejection("combat tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new CombatTuningRejection($"combat tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            return new CombatTuning(
                SchemaVersion: Int(root, "schemaVersion", "$"),
                Version: Int(root, "version", "$"),
                ProcDepthLimit: Int(root, "procDepthLimit", "$"),
                DefaultMaxTargets: Int(root, "defaultMaxTargets", "$"),
                AreaDefaultSquareSize: Int(root, "areaDefaultSquareSize", "$"),
                AreaDefaultRectangleWidth: Int(root, "areaDefaultRectangleWidth", "$"),
                AreaDefaultRectangleHeight: Int(root, "areaDefaultRectangleHeight", "$"),
                DotDefaultPeriodMs: Int(root, "dotDefaultPeriodMs", "$"),
                DotDefaultDurationMs: Int(root, "dotDefaultDurationMs", "$"));
        }
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new CombatTuningRejection($"combat tuning: missing or non-integer '{path}.{key}'");
        return v;
    }
}
