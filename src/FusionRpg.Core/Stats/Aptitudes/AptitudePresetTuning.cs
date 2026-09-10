using System.Text.Json;

namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>aptitude-sheet AS-3.1 — soft caps for the build-preset library
/// (<c>data/tuning/aptitude-presets.v{n}.json</c>). Separate from <see cref="AptitudeTuning"/> so the
/// huge edges file is not republished for a library-size knob (E8).</summary>
public sealed record AptitudePresetTuning(
    int SchemaVersion,
    int Version,
    /// <summary>Soft max presets per player — refuse create with a named reason past this (E8).
    /// Not a progression ceiling on PointBudget.</summary>
    long SoftMaxPresets,
    /// <summary>Optional default for a new row's abs max in the editor; materialize ignores unset abs.</summary>
    long DefaultRowAbsMax);

public sealed class AptitudePresetTuningRejection : Exception
{
    public AptitudePresetTuningRejection(string message) : base(message) { }
}

public static class AptitudePresetTuningHub
{
    static AptitudePresetTuning? _tuning;

    public static void Configure(AptitudePresetTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    public static AptitudePresetTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "AptitudePresetTuningHub.Configure(...) has not run. Preset soft caps read " +
        "data/tuning/aptitude-presets.v{n}.json (tunables-ssot.md §7.2) — there is no built-in default.");
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class AptitudePresetTuningLoader
{
    public static AptitudePresetTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new AptitudePresetTuningRejection("aptitude preset tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            throw new AptitudePresetTuningRejection($"aptitude preset tuning: not valid JSON — {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var softMax = Long(root, "softMaxPresets");
            if (softMax <= 0)
                throw new AptitudePresetTuningRejection("aptitude preset tuning: softMaxPresets must be > 0");
            return new AptitudePresetTuning(
                SchemaVersion: Int(root, "schemaVersion"),
                Version: Int(root, "version"),
                SoftMaxPresets: softMax,
                DefaultRowAbsMax: Long(root, "defaultRowAbsMax"));
        }
    }

    static int Int(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new AptitudePresetTuningRejection($"aptitude preset tuning: missing or non-int '{key}'");
        return v;
    }

    static long Long(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var v))
            throw new AptitudePresetTuningRejection($"aptitude preset tuning: missing or non-long '{key}'");
        return v;
    }
}
