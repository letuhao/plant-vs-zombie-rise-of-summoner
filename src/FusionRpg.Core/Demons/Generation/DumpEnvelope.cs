namespace FusionRpg.Core.Demons.Generation;

/// <summary>
/// Record shapes for `demon-seed` module 1 (`corpus-dump`, spec-corpus-dump.md). Held here, next
/// to the game code rather than inside the console tool, so a test can construct and compare them
/// without spawning a process — the same split `DemonCorpusEmit`/`DemonCorpusBuilder` already use.
/// </summary>
public static class DumpFormat
{
    /// <summary>Bumped by hand when the on-disk shape changes. Ask-first per spec-corpus-dump.md.</summary>
    public const int Version = 1;
}

public sealed record DumpAlmanacRow(
    string Side,
    int TypeId,
    string? TypeName,
    string? DisplayName,
    string? FlavorInfo,
    string? FlavorIntroduce,
    int? SunCost,
    double? CooldownSec,
    string CostStatus,
    int? Hp,
    int? Attack,
    int? Armor,
    int? ArmorMax,
    bool StatsObserved,
    int ContractVersion,
    string RebuiltUtc,
    DumpEnrichment? Enrichment);

public sealed record DumpEnrichment(
    string[]? Qualities,
    string? UnlockCondition,
    string? TypeClass,
    string? WeaknessesText,
    string? DamageVsText,
    string? Description,
    string Source);

public sealed record DumpSpawnBaseline(
    string Side,
    int TypeId,
    string StatsJson,
    string CapturedUtc);

public sealed record DumpRecipe(
    int ParentA,
    string? ParentAName,
    int ParentB,
    string? ParentBName,
    int Result,
    string? ResultName);

/// <summary>Everything <c>corpus-dump</c> exports, already sorted the way the writer requires it.</summary>
public sealed record DumpPayload(
    IReadOnlyList<DumpAlmanacRow> PlantAlmanac,
    IReadOnlyList<DumpAlmanacRow> ZombieAlmanac,
    IReadOnlyList<DumpSpawnBaseline> SpawnBaselines,
    IReadOnlyList<DumpRecipe> Recipes);

/// <summary>
/// The committed envelope (`_manifest.json`). <see cref="CapturedUtc"/> is the store's own
/// <c>max(RebuiltUtc)</c> — never wall-clock time (spec-corpus-dump.md §2).
/// </summary>
public sealed record DumpManifest(
    int DumpFormatVersion,
    string CapturedUtc,
    string ContentHash,
    int PlantCount,
    int ZombieCount,
    int BaselineCount,
    int RecipeCount);
