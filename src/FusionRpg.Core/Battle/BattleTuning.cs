using System.Text.Json;

namespace FusionRpg.Core.Battle;

public sealed record TraitMagnitudes(
    int BerserkRampHalfMilli = 0, int BerserkRampQuarterMilli = 0, int RegenPerRoundMilli = 0,
    int OnKillHealMilli = 0, int GuardShareMilli = 0, int InitiativeBonusMilli = 0,
    int DeathRefusalCharges = 0, int RetreatBelowMilli = 0, int SoulLootBonusMilli = 0,
    int SpecimenXpBonusMilli = 0, int EssenceProcMilli = 0, int EssenceRiderMilli = 0);

/// <summary>Battle balance surface (tunables-ssot.md T1) — round/affinity constants plus every
/// trait's magnitudes. Trait ids/mechanisms stay in <see cref="TraitBattleCatalog"/> (schema).</summary>
public sealed record BattleTuning(
    int SchemaVersion, int Version,
    int RoundDurationMs, int MaxRounds,
    int PrimaryAffinityDivisor, int SecondaryAffinityDivisor,
    IReadOnlyDictionary<string, TraitMagnitudes> Traits)
{
    public TraitMagnitudes TraitOf(string traitId) =>
        Traits.TryGetValue(traitId, out var m) ? m : new TraitMagnitudes();
}

public sealed class BattleTuningRejection : Exception
{
    public BattleTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class BattleTuningLoader
{
    public static BattleTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new BattleTuningRejection("battle tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new BattleTuningRejection($"battle tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var ruleset = Obj(root, "ruleset");
            var composer = Obj(root, "statComposer");
            var traitsEl = Obj(root, "traits");

            var traits = new Dictionary<string, TraitMagnitudes>(StringComparer.Ordinal);
            foreach (var prop in traitsEl.EnumerateObject())
            {
                var t = prop.Value;
                traits[prop.Name] = new TraitMagnitudes(
                    BerserkRampHalfMilli: IntOr(t, "berserkRampHalfMilli"),
                    BerserkRampQuarterMilli: IntOr(t, "berserkRampQuarterMilli"),
                    RegenPerRoundMilli: IntOr(t, "regenPerRoundMilli"),
                    OnKillHealMilli: IntOr(t, "onKillHealMilli"),
                    GuardShareMilli: IntOr(t, "guardShareMilli"),
                    InitiativeBonusMilli: IntOr(t, "initiativeBonusMilli"),
                    DeathRefusalCharges: IntOr(t, "deathRefusalCharges"),
                    RetreatBelowMilli: IntOr(t, "retreatBelowMilli"),
                    SoulLootBonusMilli: IntOr(t, "soulLootBonusMilli"),
                    SpecimenXpBonusMilli: IntOr(t, "specimenXpBonusMilli"),
                    EssenceProcMilli: IntOr(t, "essenceProcMilli"),
                    EssenceRiderMilli: IntOr(t, "essenceRiderMilli"));
            }

            return new BattleTuning(
                SchemaVersion: Int(root, "schemaVersion"),
                Version: Int(root, "version"),
                RoundDurationMs: Int(ruleset, "roundDurationMs"),
                MaxRounds: Int(ruleset, "maxRounds"),
                PrimaryAffinityDivisor: Int(composer, "primaryAffinityDivisor"),
                SecondaryAffinityDivisor: Int(composer, "secondaryAffinityDivisor"),
                Traits: traits);
        }
    }

    static JsonElement Obj(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new BattleTuningRejection($"battle tuning: missing or non-object '{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new BattleTuningRejection($"battle tuning: missing or non-integer '{key}'");
        return v;
    }

    /// <summary>Trait rows only carry the fields they use — absent means the def's own zero default.</summary>
    static int IntOr(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v)
            ? v : 0;
}

/// <summary>Fans one battle.v{n}.json load out to every class that reads it (tunables-ssot.md §7.2 —
/// hosts inject once; TraitBattleCatalog/BattleRuleset/BattleStatComposer stay independently testable).</summary>
public static class BattleTuningHub
{
    public static void Configure(BattleTuning tuning)
    {
        TraitBattleCatalog.Configure(tuning);
        BattleRuleset.Configure(tuning);
        BattleStatComposer.Configure(tuning);
    }
}
