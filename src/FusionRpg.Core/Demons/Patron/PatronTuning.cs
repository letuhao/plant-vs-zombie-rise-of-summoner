using System.Text.Json;

namespace FusionRpg.Core.Demons.Patron;

/// <summary>Patron balance surface (tunables-ssot.md T1) — loaded, not hard-coded. See
/// <see cref="PatronPolicy.Configure"/> and <see cref="PatronTuningLoader"/>.</summary>
public sealed record PatronTuning(
    int SchemaVersion, int Version,
    long SwitchCostSouls, int AuraClampMilli, int PerStarMilli, int KillSoulCap,
    IReadOnlyDictionary<DemonRarity, int> RarityBaseMilli);

public sealed class PatronTuningRejection : Exception
{
    public PatronTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class PatronTuningLoader
{
    public static PatronTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new PatronTuningRejection("patron tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new PatronTuningRejection($"patron tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var schemaVersion = Int(root, "schemaVersion", "$");
            var version = Int(root, "version", "$");
            var switchCostSouls = Long(root, "switchCostSouls", "$");
            var auraClampMilli = Int(root, "auraClampMilli", "$");
            var perStarMilli = Int(root, "perStarMilli", "$");
            var killSoulCap = Int(root, "killSoulCap", "$");

            var rEl = Obj(root, "rarityBaseMilli", "$");
            var rarityBase = new Dictionary<DemonRarity, int>();
            foreach (var rarity in Enum.GetValues<DemonRarity>())
                rarityBase[rarity] = Int(rEl, rarity.ToString().ToLowerInvariant(), "rarityBaseMilli");

            return new PatronTuning(schemaVersion, version, switchCostSouls, auraClampMilli,
                perStarMilli, killSoulCap, rarityBase);
        }
    }

    static JsonElement Obj(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new PatronTuningRejection($"patron tuning: missing or non-object '{path}.{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new PatronTuningRejection($"patron tuning: missing or non-integer '{path}.{key}'");
        return v;
    }

    static long Long(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var v))
            throw new PatronTuningRejection($"patron tuning: missing or non-integer '{path}.{key}'");
        return v;
    }
}
