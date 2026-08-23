using System.Text.Json;

namespace FusionRpg.Core.Expeditions;

public sealed record ExpeditionTierNumbers(int DurationMinutes, int TickCount, int BattleCount, int SquadSlots);

public sealed record ExpeditionEventRollTuning(
    int QuietCeilMilli, int FoundSoulsCeilMilli, int WildCeilMilli, int WildJoinMilli,
    int ShinyDie, int InjuryPowerDivisor);

/// <summary>Expeditions balance surface (tunables-ssot.md T1) — loaded, not hard-coded. Tier ids/
/// names/hasBossWave stay in <see cref="ExpeditionTierCatalog"/> (schema); their numbers, and
/// <see cref="ExpeditionResolver"/>'s event-roll bands, live here. See
/// <see cref="ExpeditionTuningHub.Configure"/> and <see cref="ExpeditionTuningLoader"/>.</summary>
public sealed record ExpeditionTuning(
    int SchemaVersion, int Version,
    IReadOnlyDictionary<string, ExpeditionTierNumbers> Tiers,
    ExpeditionEventRollTuning EventRoll);

public sealed class ExpeditionTuningRejection : Exception
{
    public ExpeditionTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class ExpeditionTuningLoader
{
    static readonly string[] TierIds = { "scout-30m", "forage-4h", "hunt-8h", "warpath-20h" };

    public static ExpeditionTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ExpeditionTuningRejection("expeditions tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new ExpeditionTuningRejection($"expeditions tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var tiersEl = Obj(root, "tiers", "$");
            var tiers = new Dictionary<string, ExpeditionTierNumbers>(StringComparer.Ordinal);
            foreach (var tierId in TierIds)
            {
                var t = Obj(tiersEl, tierId, "tiers");
                tiers[tierId] = new ExpeditionTierNumbers(
                    DurationMinutes: Int(t, "durationMinutes", $"tiers.{tierId}"),
                    TickCount: Int(t, "tickCount", $"tiers.{tierId}"),
                    BattleCount: Int(t, "battleCount", $"tiers.{tierId}"),
                    SquadSlots: Int(t, "squadSlots", $"tiers.{tierId}"));
            }

            var e = Obj(root, "eventRoll", "$");
            var eventRoll = new ExpeditionEventRollTuning(
                QuietCeilMilli: Int(e, "quietCeilMilli", "eventRoll"),
                FoundSoulsCeilMilli: Int(e, "foundSoulsCeilMilli", "eventRoll"),
                WildCeilMilli: Int(e, "wildCeilMilli", "eventRoll"),
                WildJoinMilli: Int(e, "wildJoinMilli", "eventRoll"),
                ShinyDie: Int(e, "shinyDie", "eventRoll"),
                InjuryPowerDivisor: Int(e, "injuryPowerDivisor", "eventRoll"));

            return new ExpeditionTuning(Int(root, "schemaVersion", "$"), Int(root, "version", "$"),
                tiers, eventRoll);
        }
    }

    static JsonElement Obj(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new ExpeditionTuningRejection($"expeditions tuning: missing or non-object '{path}.{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new ExpeditionTuningRejection($"expeditions tuning: missing or non-integer '{path}.{key}'");
        return v;
    }
}

/// <summary>Single configuration point covering <see cref="ExpeditionTierCatalog"/> and
/// <see cref="ExpeditionResolver"/>, which share one <c>expeditions.v{n}.json</c>.</summary>
public static class ExpeditionTuningHub
{
    static ExpeditionTuning? _tuning;

    public static void Configure(ExpeditionTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    public static ExpeditionTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "ExpeditionTuningHub.Configure(...) has not run. Every expedition rule reads data/tuning/" +
        "expeditions.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");
}
