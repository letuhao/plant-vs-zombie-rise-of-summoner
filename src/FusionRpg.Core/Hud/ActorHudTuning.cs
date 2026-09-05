using System.Text.Json;

namespace FusionRpg.Core.Hud;

public sealed record ActorHudTuning(
    int SchemaVersion,
    int Version,
    int StatusStripMax,
    bool HpSliverEnabled,
    int BadgeMax,
    string AnchorKind,
    double WorldYOffset,
    double BarWorldWidth,
    double BarWorldHeight,
    double RowOffsetIdentity,
    double RowOffsetResources,
    double RowOffsetStatuses,
    int MaxStackPips,
    int? EliteTierThreshold,
    double MagnitudeMidThreshold,
    double MagnitudeHighThreshold);

public sealed class ActorHudTuningRejection : Exception
{
    public ActorHudTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class ActorHudTuningLoader
{
    public static ActorHudTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ActorHudTuningRejection("actor-hud tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            throw new ActorHudTuningRejection($"actor-hud tuning: not valid JSON — {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            return new ActorHudTuning(
                SchemaVersion: Int(root, "schemaVersion", "$"),
                Version: Int(root, "version", "$"),
                StatusStripMax: Int(root, "statusStripMax", "$"),
                HpSliverEnabled: Bool(root, "hpSliverEnabled", "$"),
                BadgeMax: Int(root, "badgeMax", "$"),
                AnchorKind: AnchorBody(root, "anchorKind", "$"),
                WorldYOffset: Double(root, "worldYOffset", "$"),
                BarWorldWidth: Double(root, "barWorldWidth", "$"),
                BarWorldHeight: Double(root, "barWorldHeight", "$"),
                RowOffsetIdentity: Double(root, "rowOffsetIdentity", "$"),
                RowOffsetResources: Double(root, "rowOffsetResources", "$"),
                RowOffsetStatuses: Double(root, "rowOffsetStatuses", "$"),
                MaxStackPips: Int(root, "maxStackPips", "$"),
                EliteTierThreshold: OptionalInt(root, "eliteTierThreshold", "$"),
                MagnitudeMidThreshold: Double(root, "magnitudeMidThreshold", "$"),
                MagnitudeHighThreshold: Double(root, "magnitudeHighThreshold", "$"));
        }
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new ActorHudTuningRejection($"actor-hud tuning: missing or non-integer '{path}.{key}'");
        return v;
    }

    static int? OptionalInt(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el))
            return null;
        if (el.ValueKind == JsonValueKind.Null)
            return null;
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new ActorHudTuningRejection($"actor-hud tuning: non-integer '{path}.{key}'");
        return v;
    }

    static bool Bool(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new ActorHudTuningRejection($"actor-hud tuning: missing or non-boolean '{path}.{key}'");
        return el.GetBoolean();
    }

    static double Double(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number)
            throw new ActorHudTuningRejection($"actor-hud tuning: missing or non-number '{path}.{key}'");
        return el.GetDouble();
    }

    static string Str(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String)
            throw new ActorHudTuningRejection($"actor-hud tuning: missing or non-string '{path}.{key}'");
        var s = el.GetString();
        if (string.IsNullOrWhiteSpace(s))
            throw new ActorHudTuningRejection($"actor-hud tuning: empty '{path}.{key}'");
        return s.Trim();
    }

    /// <summary>Unity Band B SSOT is Body only — reject other kinds at parse time.</summary>
    static string AnchorBody(JsonElement parent, string key, string path)
    {
        var s = Str(parent, key, path);
        if (!string.Equals(s, "body", StringComparison.OrdinalIgnoreCase))
            throw new ActorHudTuningRejection(
                $"actor-hud tuning: '{path}.{key}' must be 'body' (got '{s}') — crown/other anchors are not supported");
        return "body";
    }
}

public static class ActorHudTuningHub
{
    static ActorHudTuning? _tuning;

    public static void Configure(ActorHudTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    public static ActorHudTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "ActorHudTuningHub.Configure(...) has not run. Hosts read data/tuning/actor-hud.v{n}.json " +
        "(tunables-ssot.md T5) — there is no built-in default to fall back to.");
}
