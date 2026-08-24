using System.Text.Json;

namespace FusionRpg.Core.World.Ai;

public sealed record FrontierRulesTuning(int RecoverAtMilli, int ExploreTurns, long SeveranceThresholdCost);

public sealed record ThreatMapTuning(int StaleDecayPerTurn, int MaxSpreadHops, int ProximityFalloffPerHop);

public sealed record ValueWeightsTuning(int Yield, int Strategic, int Defensibility, int Cost, int Risk, int Curiosity);

public sealed record ValueMapTuning(
    int OptimismMilli, int OverextensionPenaltyMilli, int HabitabilityPenaltyMilli, ValueWeightsTuning DefaultWeights);

/// <summary>AI-commander balance surface (tunables-ssot.md T1) — spec-ai-commander.md.</summary>
public sealed record WorldAiTuning(
    int SchemaVersion, int Version,
    FrontierRulesTuning FrontierRules, ThreatMapTuning ThreatMap, ValueMapTuning ValueMap);

public sealed class WorldAiTuningRejection : Exception
{
    public WorldAiTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class WorldAiTuningLoader
{
    public static WorldAiTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new WorldAiTuningRejection("ai tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new WorldAiTuningRejection($"ai tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var frontier = Obj(root, "frontierRules");
            var threat = Obj(root, "threatMap");
            var value = Obj(root, "valueMap");
            var weightsEl = Obj(value, "defaultWeights");

            return new WorldAiTuning(
                SchemaVersion: Int(root, "schemaVersion", "$"),
                Version: Int(root, "version", "$"),
                FrontierRules: new FrontierRulesTuning(
                    RecoverAtMilli: Int(frontier, "recoverAtMilli", "frontierRules"),
                    ExploreTurns: Int(frontier, "exploreTurns", "frontierRules"),
                    SeveranceThresholdCost: Long(frontier, "severanceThresholdCost", "frontierRules")),
                ThreatMap: new ThreatMapTuning(
                    StaleDecayPerTurn: Int(threat, "staleDecayPerTurn", "threatMap"),
                    MaxSpreadHops: Int(threat, "maxSpreadHops", "threatMap"),
                    ProximityFalloffPerHop: Int(threat, "proximityFalloffPerHop", "threatMap")),
                ValueMap: new ValueMapTuning(
                    OptimismMilli: Int(value, "optimismMilli", "valueMap"),
                    OverextensionPenaltyMilli: Int(value, "overextensionPenaltyMilli", "valueMap"),
                    HabitabilityPenaltyMilli: Int(value, "habitabilityPenaltyMilli", "valueMap"),
                    DefaultWeights: new ValueWeightsTuning(
                        Yield: Int(weightsEl, "yield", "valueMap.defaultWeights"),
                        Strategic: Int(weightsEl, "strategic", "valueMap.defaultWeights"),
                        Defensibility: Int(weightsEl, "defensibility", "valueMap.defaultWeights"),
                        Cost: Int(weightsEl, "cost", "valueMap.defaultWeights"),
                        Risk: Int(weightsEl, "risk", "valueMap.defaultWeights"),
                        Curiosity: Int(weightsEl, "curiosity", "valueMap.defaultWeights"))));
        }
    }

    static JsonElement Obj(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new WorldAiTuningRejection($"ai tuning: missing or non-object '{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new WorldAiTuningRejection($"ai tuning: missing or non-integer '{path}.{key}'");
        return v;
    }

    static long Long(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var v))
            throw new WorldAiTuningRejection($"ai tuning: missing or non-integer '{path}.{key}'");
        return v;
    }
}

/// <summary>Holds one ai.v{n}.json load for FrontierRulesPolicy/ThreatMap/ValueMap (tunables-ssot.md §7.2).</summary>
public static class WorldAiPolicy
{
    static WorldAiTuning? _tuning;

    public static void Configure(WorldAiTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    public static WorldAiTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "WorldAiPolicy.Configure(...) has not run. The AI commander reads data/tuning/ai.v{n}.json " +
        "(tunables-ssot.md T5) — there is no built-in default to fall back to.");

    public static ValueWeights DefaultWeights => new()
    {
        Yield = Tuning.ValueMap.DefaultWeights.Yield,
        Strategic = Tuning.ValueMap.DefaultWeights.Strategic,
        Defensibility = Tuning.ValueMap.DefaultWeights.Defensibility,
        Cost = Tuning.ValueMap.DefaultWeights.Cost,
        Risk = Tuning.ValueMap.DefaultWeights.Risk,
        Curiosity = Tuning.ValueMap.DefaultWeights.Curiosity
    };
}
