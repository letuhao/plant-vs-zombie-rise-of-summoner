using System.Text.Json;

namespace FusionRpg.Core.Progression;

public sealed record XpCurveParams(double First, double Step);

public sealed record XpAwardsTuning(double Kill, double Defeat, double Mower, double PlantPlace, double ZombieSpawn);

/// <summary>Progression balance surface (tunables-ssot.md T1) — RpgXpCurve/RpgXpAwards.</summary>
public sealed record ProgressionTuning(
    int SchemaVersion, int Version,
    XpCurveParams PlantCurve, XpCurveParams ZombieCurve, XpCurveParams PlayerCurve,
    XpAwardsTuning Awards);

public sealed class ProgressionTuningRejection : Exception
{
    public ProgressionTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class ProgressionTuningLoader
{
    public static ProgressionTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ProgressionTuningRejection("progression tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new ProgressionTuningRejection($"progression tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var curve = Obj(root, "xpCurve");
            var awards = Obj(root, "awards");

            return new ProgressionTuning(
                SchemaVersion: Int(root, "schemaVersion"),
                Version: Int(root, "version"),
                PlantCurve: Curve(curve, "plant"),
                ZombieCurve: Curve(curve, "zombie"),
                PlayerCurve: Curve(curve, "player"),
                Awards: new XpAwardsTuning(
                    Kill: Double(awards, "kill"),
                    Defeat: Double(awards, "defeat"),
                    Mower: Double(awards, "mower"),
                    PlantPlace: Double(awards, "plantPlace"),
                    ZombieSpawn: Double(awards, "zombieSpawn")));
        }
    }

    static XpCurveParams Curve(JsonElement parent, string key)
    {
        var el = Obj(parent, key);
        return new XpCurveParams(Double(el, "first"), Double(el, "step"));
    }

    static JsonElement Obj(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new ProgressionTuningRejection($"progression tuning: missing or non-object '{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new ProgressionTuningRejection($"progression tuning: missing or non-integer '{key}'");
        return v;
    }

    static double Double(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number)
            throw new ProgressionTuningRejection($"progression tuning: missing or non-number '{key}'");
        return el.GetDouble();
    }
}

/// <summary>Fans one progression.v{n}.json load out to both classes that read it (tunables-ssot.md §7.2).</summary>
public static class ProgressionTuningHub
{
    public static void Configure(ProgressionTuning tuning)
    {
        RpgXpCurve.Configure(tuning);
        RpgXpAwards.Configure(tuning);
    }
}
