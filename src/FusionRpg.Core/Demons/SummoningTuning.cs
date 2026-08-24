using System.Text.Json;

namespace FusionRpg.Core.Demons;

public sealed record BannerTuning(int CostPerPull, int CostPerTen, double FocusWeightMultiplier);

public sealed record RollerTuning(
    int EpicHardPity, int LegendarySoftStart, int LegendaryHardPity, int LegendaryBasePerMille,
    int LegendaryRampPerMille, int EpicPerMille, int RarePerMille, int ShinyOneIn);

/// <summary>Summoning balance surface (tunables-ssot.md T1) — spec-demon-summoning.md. Banner
/// ids/HasElementFocus stay in <see cref="SummonBannerCatalog"/> (schema); costs/pity/rarity moved.</summary>
public sealed record SummoningTuning(
    int SchemaVersion, int Version,
    IReadOnlyDictionary<string, BannerTuning> Banners, RollerTuning Roller);

public sealed class SummoningTuningRejection : Exception
{
    public SummoningTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class SummoningTuningLoader
{
    public static SummoningTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new SummoningTuningRejection("summoning tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new SummoningTuningRejection($"summoning tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var bannersEl = Obj(root, "banners");
            var rollerEl = Obj(root, "roller");

            var banners = new Dictionary<string, BannerTuning>(StringComparer.Ordinal);
            foreach (var prop in bannersEl.EnumerateObject())
            {
                var b = prop.Value;
                banners[prop.Name] = new BannerTuning(
                    CostPerPull: Int(b, "costPerPull", prop.Name),
                    CostPerTen: Int(b, "costPerTen", prop.Name),
                    FocusWeightMultiplier: Double(b, "focusWeightMultiplier", prop.Name));
            }

            var roller = new RollerTuning(
                EpicHardPity: Int(rollerEl, "epicHardPity", "roller"),
                LegendarySoftStart: Int(rollerEl, "legendarySoftStart", "roller"),
                LegendaryHardPity: Int(rollerEl, "legendaryHardPity", "roller"),
                LegendaryBasePerMille: Int(rollerEl, "legendaryBasePerMille", "roller"),
                LegendaryRampPerMille: Int(rollerEl, "legendaryRampPerMille", "roller"),
                EpicPerMille: Int(rollerEl, "epicPerMille", "roller"),
                RarePerMille: Int(rollerEl, "rarePerMille", "roller"),
                ShinyOneIn: Int(rollerEl, "shinyOneIn", "roller"));

            return new SummoningTuning(
                SchemaVersion: Int(root, "schemaVersion", "$"),
                Version: Int(root, "version", "$"),
                Banners: banners, Roller: roller);
        }
    }

    static JsonElement Obj(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new SummoningTuningRejection($"summoning tuning: missing or non-object '{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new SummoningTuningRejection($"summoning tuning: missing or non-integer '{path}.{key}'");
        return v;
    }

    static double Double(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number)
            throw new SummoningTuningRejection($"summoning tuning: missing or non-number '{path}.{key}'");
        return el.GetDouble();
    }
}

/// <summary>Fans one summoning.v{n}.json load out to both classes that read it (tunables-ssot.md §7.2).</summary>
public static class SummoningTuningHub
{
    public static void Configure(SummoningTuning tuning)
    {
        SummonBannerCatalog.Configure(tuning);
        SummonRoller.Configure(tuning);
    }
}
