using System.Text.Json;

namespace FusionRpg.Core.Progression;

/// <summary>
/// XP ladder parameters. <c>long</c>, not <c>double</c> (CLAUDE.md numeric rule — XP is a persisted
/// magnitude): the config carries whole numbers today and <see cref="ProgressionTuningLoader"/>
/// rejects a fractional one rather than silently truncating it.
/// </summary>
public sealed record XpCurveParams(long First, long Step);

/// <summary>Award deltas in whole XP — same integer rule as <see cref="XpCurveParams"/>.</summary>
public sealed record XpAwardsTuning(long Kill, long Defeat, long Mower, long PlantPlace, long ZombieSpawn);

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
                    Kill: Long(awards, "kill"),
                    Defeat: Long(awards, "defeat"),
                    Mower: Long(awards, "mower"),
                    PlantPlace: Long(awards, "plantPlace"),
                    ZombieSpawn: Long(awards, "zombieSpawn")));
        }
    }

    static XpCurveParams Curve(JsonElement parent, string key)
    {
        var el = Obj(parent, key);
        return new XpCurveParams(Long(el, "first"), Long(el, "step"));
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

    /// <summary>
    /// A whole-number reader that accepts JSON's `80` and `80.0` alike but REFUSES `80.5`. XP is an
    /// integer magnitude end to end (CLAUDE.md: `long` for any magnitude, never a persisted `double`),
    /// so a fractional tuning value is a balance mistake to report, not a value to round away.
    /// </summary>
    static long Long(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number)
            throw new ProgressionTuningRejection($"progression tuning: missing or non-number '{key}'");
        if (el.TryGetInt64(out var exact)) return exact;

        var raw = el.GetDouble();
        if (double.IsNaN(raw) || double.IsInfinity(raw) || raw != Math.Floor(raw))
            throw new ProgressionTuningRejection(
                $"progression tuning: '{key}' = {raw} is not a whole number — XP is an integer magnitude");
        if (raw < long.MinValue || raw > long.MaxValue)
            throw new ProgressionTuningRejection($"progression tuning: '{key}' = {raw} is out of range for long");
        return (long)raw;
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
