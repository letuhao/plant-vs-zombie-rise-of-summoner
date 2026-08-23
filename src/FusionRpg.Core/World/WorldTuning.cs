using System.Text.Json;

namespace FusionRpg.Core.World;

public sealed record WorldSizeNodeRange(int Min, int Max);

public sealed record StrengthBandTuning(long Floor, long Ceiling, long Midpoint);

public sealed record PlaceholderBattleTuning(
    int DefenderBonusMilli, int WipeoutRatioMilli, int RoutWoundMilli, int GuardWoundMilli);

public sealed record WorldCalendarTuning(
    int DaysPerWeek, int WeeksPerMonth,
    int SpecialWeekChanceMilli, int SpecialMonthChanceMilli, int PlagueChanceMilli);

/// <summary>World balance surface (tunables-ssot.md T1) — loaded, not hard-coded. Row ids/names/
/// structural flags stay in their C# catalogs; only the numeric fields live here. See
/// <see cref="WorldTuningHub.Configure"/> and <see cref="WorldTuningLoader"/>.</summary>
public sealed record WorldTuning(
    int SchemaVersion, int Version,
    IReadOnlyDictionary<string, int> LaneCostMultiplierMilli,
    IReadOnlyDictionary<string, WorldSizeNodeRange> WorldSizeNodes,
    IReadOnlyList<StrengthBandTuning> StrengthBands,
    PlaceholderBattleTuning PlaceholderBattle,
    WorldCalendarTuning Calendar);

public sealed class WorldTuningRejection : Exception
{
    public WorldTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class WorldTuningLoader
{
    public static WorldTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new WorldTuningRejection("world tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new WorldTuningRejection($"world tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var schemaVersion = Int(root, "schemaVersion", "$");
            var version = Int(root, "version", "$");

            var laneEl = Obj(root, "laneCostMultiplierMilli", "$");
            var lanes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var prop in laneEl.EnumerateObject())
                lanes[prop.Name] = RequireInt(prop.Value, $"laneCostMultiplierMilli.{prop.Name}");

            var sizeEl = Obj(root, "worldSizeNodes", "$");
            var sizes = new Dictionary<string, WorldSizeNodeRange>(StringComparer.Ordinal);
            foreach (var prop in sizeEl.EnumerateObject())
            {
                var path = $"worldSizeNodes.{prop.Name}";
                sizes[prop.Name] = new WorldSizeNodeRange(
                    Int(prop.Value, "min", path), Int(prop.Value, "max", path));
            }

            var bandsEl = Require(root, "strengthBands", "$");
            if (bandsEl.ValueKind != JsonValueKind.Array)
                throw new WorldTuningRejection("world tuning: 'strengthBands' must be an array");
            var bands = new List<StrengthBandTuning>();
            var i = 0;
            foreach (var el in bandsEl.EnumerateArray())
            {
                var path = $"strengthBands[{i}]";
                bands.Add(new StrengthBandTuning(
                    Long(el, "floor", path), Long(el, "ceiling", path), Long(el, "midpoint", path)));
                i++;
            }

            var pb = Obj(root, "placeholderBattle", "$");
            var placeholderBattle = new PlaceholderBattleTuning(
                DefenderBonusMilli: Int(pb, "defenderBonusMilli", "placeholderBattle"),
                WipeoutRatioMilli: Int(pb, "wipeoutRatioMilli", "placeholderBattle"),
                RoutWoundMilli: Int(pb, "routWoundMilli", "placeholderBattle"),
                GuardWoundMilli: Int(pb, "guardWoundMilli", "placeholderBattle"));

            var cal = Obj(root, "calendar", "$");
            var calendar = new WorldCalendarTuning(
                DaysPerWeek: Int(cal, "daysPerWeek", "calendar"),
                WeeksPerMonth: Int(cal, "weeksPerMonth", "calendar"),
                SpecialWeekChanceMilli: Int(cal, "specialWeekChanceMilli", "calendar"),
                SpecialMonthChanceMilli: Int(cal, "specialMonthChanceMilli", "calendar"),
                PlagueChanceMilli: Int(cal, "plagueChanceMilli", "calendar"));

            return new WorldTuning(schemaVersion, version, lanes, sizes, bands, placeholderBattle, calendar);
        }
    }

    static JsonElement Require(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el))
            throw new WorldTuningRejection($"world tuning: missing '{path}.{key}'");
        return el;
    }

    static JsonElement Obj(JsonElement parent, string key, string path)
    {
        var el = Require(parent, key, path);
        if (el.ValueKind != JsonValueKind.Object)
            throw new WorldTuningRejection($"world tuning: '{path}.{key}' must be an object");
        return el;
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el))
            throw new WorldTuningRejection($"world tuning: missing '{path}.{key}'");
        return RequireInt(el, $"{path}.{key}");
    }

    static int RequireInt(JsonElement el, string path)
    {
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new WorldTuningRejection($"world tuning: '{path}' must be an integer");
        return v;
    }

    static long Long(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var v))
            throw new WorldTuningRejection($"world tuning: missing or non-integer '{path}.{key}'");
        return v;
    }
}

/// <summary>
/// Single configuration point for every world catalog/policy this tuning file feeds — mirrors
/// <c>ContractPolicy.Configure</c> / <c>LoamPolicy.Configure</c>, but one call covers five files
/// (<see cref="LaneTypeCatalog"/>, <see cref="WorldSizeCatalog"/>, <see cref="Intel.StrengthBandCatalog"/>,
/// <see cref="Turn.PlaceholderBattleResolver"/>, <see cref="Turn.TurnCalendar"/>) since they all read
/// one <c>world.v{n}.json</c>.
/// </summary>
public static class WorldTuningHub
{
    static WorldTuning? _tuning;

    public static void Configure(WorldTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    public static WorldTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "WorldTuningHub.Configure(...) has not run. Every world catalog/policy number reads " +
        "data/tuning/world.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");
}
