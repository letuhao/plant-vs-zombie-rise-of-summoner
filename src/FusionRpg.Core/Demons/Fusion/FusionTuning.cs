using System.Text.Json;

namespace FusionRpg.Core.Demons.Fusion;

public sealed record FusionCostTuning(long Souls, int ShardCount, int EssenceCount);

public sealed record RecipeCostTuning(long Souls, DemonRarity ShardRarity, int ShardCount, int EssenceCount);

/// <summary>Fusion balance surface (tunables-ssot.md T1) — loaded, not hard-coded. See
/// <see cref="StarPolicy.Configure"/> and <see cref="FusionTuningLoader"/>.</summary>
public sealed record FusionTuning(
    int SchemaVersion, int Version,
    int PerStarPowerMilli, int PerStarDefenseMilli,
    IReadOnlyDictionary<DemonRarity, int> StarCap,
    FusionCostTuning StarMergeCost, FusionCostTuning PromotionCost,
    IReadOnlyDictionary<DemonRarity, RecipeCostTuning> RecipeCost);

public sealed class FusionTuningRejection : Exception
{
    public FusionTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class FusionTuningLoader
{
    public static FusionTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FusionTuningRejection("fusion tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new FusionTuningRejection($"fusion tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var schemaVersion = Int(root, "schemaVersion", "$");
            var version = Int(root, "version", "$");
            var perStarPowerMilli = Int(root, "perStarPowerMilli", "$");
            var perStarDefenseMilli = Int(root, "perStarDefenseMilli", "$");

            var capEl = Obj(root, "starCap", "$");
            var starCap = new Dictionary<DemonRarity, int>();
            foreach (var rarity in Enum.GetValues<DemonRarity>())
                starCap[rarity] = Int(capEl, rarity.ToString().ToLowerInvariant(), "starCap");

            var starMergeCost = Cost(root, "starMergeCost");
            var promotionCost = Cost(root, "promotionCost");

            var recipeEl = Obj(root, "recipeCost", "$");
            var recipeCost = new Dictionary<DemonRarity, RecipeCostTuning>();
            foreach (var rarity in new[] { DemonRarity.Rare, DemonRarity.Epic, DemonRarity.Legendary })
            {
                var key = rarity.ToString().ToLowerInvariant();
                var el = Obj(recipeEl, key, "recipeCost");
                recipeCost[rarity] = new RecipeCostTuning(
                    Souls: Long(el, "souls", $"recipeCost.{key}"),
                    ShardRarity: ParseRarity(Str(el, "shardRarity", $"recipeCost.{key}"), $"recipeCost.{key}.shardRarity"),
                    ShardCount: Int(el, "shardCount", $"recipeCost.{key}"),
                    EssenceCount: Int(el, "essenceCount", $"recipeCost.{key}"));
            }

            return new FusionTuning(schemaVersion, version, perStarPowerMilli, perStarDefenseMilli,
                starCap, starMergeCost, promotionCost, recipeCost);
        }
    }

    static FusionCostTuning Cost(JsonElement root, string key)
    {
        var el = Obj(root, key, "$");
        return new FusionCostTuning(
            Souls: Long(el, "souls", key),
            ShardCount: Int(el, "shardCount", key),
            EssenceCount: Int(el, "essenceCount", key));
    }

    static DemonRarity ParseRarity(string value, string path)
    {
        foreach (var rarity in Enum.GetValues<DemonRarity>())
            if (string.Equals(rarity.ToString(), value, StringComparison.OrdinalIgnoreCase))
                return rarity;
        throw new FusionTuningRejection($"fusion tuning: '{path}' is not a known rarity: '{value}'");
    }

    static JsonElement Obj(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new FusionTuningRejection($"fusion tuning: missing or non-object '{path}.{key}'");
        return el;
    }

    static string Str(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String)
            throw new FusionTuningRejection($"fusion tuning: missing or non-string '{path}.{key}'");
        return el.GetString()!;
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new FusionTuningRejection($"fusion tuning: missing or non-integer '{path}.{key}'");
        return v;
    }

    static long Long(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var v))
            throw new FusionTuningRejection($"fusion tuning: missing or non-integer '{path}.{key}'");
        return v;
    }
}
