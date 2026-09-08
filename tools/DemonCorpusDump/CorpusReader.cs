using FusionRpg.Core.Demons.Generation;
using FusionRpg.Data;

namespace FusionRpg.Tools.DemonCorpusDump;

/// <summary>
/// Reads the whole almanac_seed/spawn_stats/recipes surface through <see cref="RpgStore"/> and
/// shapes it into a <see cref="DumpPayload"/>. Split out of <c>Program.cs</c> so a test can call it
/// against a small in-memory store (spec-corpus-dump.md's own testing strategy) without spawning a
/// process — the same split <c>DemonCorpusEmit</c>/<c>DemonCorpusBuilder</c> already use.
/// </summary>
public static class CorpusReader
{
    /// <summary>
    /// The whole table, not the catalog's opinion of it. `DemonCorpusEmit` walked
    /// `DemonSpeciesCatalog.All` and could therefore never see a species the C# generator had not
    /// already picked — the defect this module exists to fix (spec-corpus-dump.md).
    /// </summary>
    public static DumpPayload BuildPayload(RpgStore store)
    {
        var plantRows = store.ListAlmanacSeed("plant").Select(ToDumpRow).ToList();
        var zombieRows = store.ListAlmanacSeed("zombie").Select(ToDumpRow).ToList();

        var baselines = store.ListSpawnBaselines()
            .Select(b => new DumpSpawnBaseline(b.Side, b.TypeId, b.StatsJson, b.CapturedUtc))
            .ToList();

        var recipes = store.ListRecipes()
            .Select(r => new DumpRecipe(r.ParentA, r.ParentAName, r.ParentB, r.ParentBName, r.Result, r.ResultName))
            .ToList();

        return new DumpPayload(plantRows, zombieRows, baselines, recipes);
    }

    /// <summary>The store's own <c>max(RebuiltUtc)</c> over every exported almanac row — never wall-clock time (spec §2).</summary>
    public static string CapturedUtc(DumpPayload payload) =>
        payload.PlantAlmanac.Concat(payload.ZombieAlmanac)
            .Select(r => r.RebuiltUtc)
            .DefaultIfEmpty("")
            .Max(StringComparer.Ordinal)!;

    static DumpAlmanacRow ToDumpRow(AlmanacSeedDto a) => new(
        Side: a.Side,
        TypeId: a.TypeId,
        TypeName: a.TypeName,
        DisplayName: a.DisplayName,
        FlavorInfo: a.FlavorInfo,
        FlavorIntroduce: a.FlavorIntroduce,
        SunCost: a.SunCost,
        CooldownSec: a.CooldownSec,
        CostStatus: a.CostStatus,
        Hp: a.Hp,
        Attack: a.Attack,
        Armor: a.Armor,
        ArmorMax: a.ArmorMax,
        StatsObserved: a.StatsObserved,
        ContractVersion: a.ContractVersion,
        RebuiltUtc: a.RebuiltUtc,
        Enrichment: a.Enrichment is null ? null : new DumpEnrichment(
            a.Enrichment.Qualities, a.Enrichment.UnlockCondition, a.Enrichment.TypeClass,
            a.Enrichment.WeaknessesText, a.Enrichment.DamageVsText, a.Enrichment.Description, a.Enrichment.Source));
}
