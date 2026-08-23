using System.Text.Json;

namespace FusionRpg.Core.World.Loam;

public sealed record LoamUpkeepTuning(
    long SeepPerTurn, long LoamCapacity, long BaseUpkeepPerSector,
    long GarrisonUpkeepPerMember, long DevelopmentUpkeepPerLevel, long DangerUpkeepPerBand);

public sealed record LoamFadeTuning(
    int RecoveryMilli, int BaseDecayMilli, int DecayPerDeficitUnitMilli,
    long DecayScaleDivisor, int MaxDecayMilli, int AbandonmentHorizonTurns);

public sealed record LoamLegionSupplyTuning(long CarryPerBearer, long BurnPerMember);

public sealed record LoamStructuresTuning(
    int WellYieldMultiplierMilli, long WellCostMilli, long WaystationCostMilli,
    int WellBuildTurns, int WaystationBuildTurns, int WaystationRangeHops,
    long GranaryCostMilli, long GranaryCapacityBonus, int GranaryBuildTurns);

public sealed record LoamTextureTuning(
    int ContagionPressurePerTurn, int MaxPressureMilli, int PressureDecayPerTurn,
    int SurgeDecayMultiplierMilli, int UnmadeSpawnAfterTurns, long UnmadeMemberHp, int UnmadeMemberCount);

/// <summary>Loam balance surface (tunables-ssot.md T1) — loaded, not hard-coded. See
/// <see cref="LoamPolicy.Configure"/> and <see cref="LoamTuningLoader"/>.</summary>
public sealed record LoamTuning(
    int SchemaVersion, int Version,
    LoamUpkeepTuning Upkeep, LoamFadeTuning Fade, LoamLegionSupplyTuning LegionSupply,
    LoamStructuresTuning Structures, LoamTextureTuning Texture);

public sealed class LoamTuningRejection : Exception
{
    public LoamTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2) — the host reads
/// <c>data/tuning/loam.v{n}.json</c> and calls <see cref="Parse"/>.</summary>
public static class LoamTuningLoader
{
    public static LoamTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new LoamTuningRejection("loam tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new LoamTuningRejection($"loam tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var schemaVersion = Int(root, "schemaVersion", "$");
            var version = Int(root, "version", "$");

            var u = Obj(root, "upkeep", "$");
            var upkeep = new LoamUpkeepTuning(
                SeepPerTurn: Long(u, "seepPerTurn", "upkeep"),
                LoamCapacity: Long(u, "loamCapacity", "upkeep"),
                BaseUpkeepPerSector: Long(u, "baseUpkeepPerSector", "upkeep"),
                GarrisonUpkeepPerMember: Long(u, "garrisonUpkeepPerMember", "upkeep"),
                DevelopmentUpkeepPerLevel: Long(u, "developmentUpkeepPerLevel", "upkeep"),
                DangerUpkeepPerBand: Long(u, "dangerUpkeepPerBand", "upkeep"));

            var f = Obj(root, "fade", "$");
            var fade = new LoamFadeTuning(
                RecoveryMilli: Int(f, "recoveryMilli", "fade"),
                BaseDecayMilli: Int(f, "baseDecayMilli", "fade"),
                DecayPerDeficitUnitMilli: Int(f, "decayPerDeficitUnitMilli", "fade"),
                DecayScaleDivisor: Long(f, "decayScaleDivisor", "fade"),
                MaxDecayMilli: Int(f, "maxDecayMilli", "fade"),
                AbandonmentHorizonTurns: Int(f, "abandonmentHorizonTurns", "fade"));

            var ls = Obj(root, "legionSupply", "$");
            var legionSupply = new LoamLegionSupplyTuning(
                CarryPerBearer: Long(ls, "carryPerBearer", "legionSupply"),
                BurnPerMember: Long(ls, "burnPerMember", "legionSupply"));

            var s = Obj(root, "structures", "$");
            var structures = new LoamStructuresTuning(
                WellYieldMultiplierMilli: Int(s, "wellYieldMultiplierMilli", "structures"),
                WellCostMilli: Long(s, "wellCostMilli", "structures"),
                WaystationCostMilli: Long(s, "waystationCostMilli", "structures"),
                WellBuildTurns: Int(s, "wellBuildTurns", "structures"),
                WaystationBuildTurns: Int(s, "waystationBuildTurns", "structures"),
                WaystationRangeHops: Int(s, "waystationRangeHops", "structures"),
                GranaryCostMilli: Long(s, "granaryCostMilli", "structures"),
                GranaryCapacityBonus: Long(s, "granaryCapacityBonus", "structures"),
                GranaryBuildTurns: Int(s, "granaryBuildTurns", "structures"));

            var t = Obj(root, "texture", "$");
            var texture = new LoamTextureTuning(
                ContagionPressurePerTurn: Int(t, "contagionPressurePerTurn", "texture"),
                MaxPressureMilli: Int(t, "maxPressureMilli", "texture"),
                PressureDecayPerTurn: Int(t, "pressureDecayPerTurn", "texture"),
                SurgeDecayMultiplierMilli: Int(t, "surgeDecayMultiplierMilli", "texture"),
                UnmadeSpawnAfterTurns: Int(t, "unmadeSpawnAfterTurns", "texture"),
                UnmadeMemberHp: Long(t, "unmadeMemberHp", "texture"),
                UnmadeMemberCount: Int(t, "unmadeMemberCount", "texture"));

            return new LoamTuning(schemaVersion, version, upkeep, fade, legionSupply, structures, texture);
        }
    }

    static JsonElement Obj(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new LoamTuningRejection($"loam tuning: missing or non-object '{path}.{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new LoamTuningRejection($"loam tuning: missing or non-integer '{path}.{key}'");
        return v;
    }

    static long Long(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var v))
            throw new LoamTuningRejection($"loam tuning: missing or non-integer '{path}.{key}'");
        return v;
    }
}
