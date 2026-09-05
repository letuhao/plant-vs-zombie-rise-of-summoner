using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>
/// T4.8 step 4, `catalog-runtime`'s own ⛔ acceptance gate (`spec-catalog-runtime.md` §6): "before
/// deleting `DemonSpeciesCatalog.Generated.cs`, both sources exist. A test loads the store-backed
/// catalog and the compiled one and diffs them field by field." Real end to end: real anchors
/// (`pea.json`/`sunflower.json`) → real `SpeciesExpander` → a real temp `RpgStore` →
/// `BuildDemonSpeciesSnapshot()` → `SpeciesDiff.Compare` against the real compiled
/// `DemonSpeciesCatalog.All` (every host in this assembly configures with the compiled default —
/// `ContractTuningTestBootstrap`'s own `[ModuleInitializer]` — so `All` here IS the compiled roster,
/// exactly as every real host reads it today, pre-flip).
///
/// <para><b>What this does NOT claim.</b> `peashooter`/`sunflower` are real overlapping ids between
/// the compiled catalog and the anchor pipeline, so this proves the MECHANISM against real data —
/// but it does not claim the reported differences are "human accepted" (spec §6's own separate,
/// `anchor-emit --diff-legacy`-gated step, not run here). A future full-84-species diff, once T2.11's
/// real classification run lands, is this same mechanism against a bigger input, not new code.</para>
/// </summary>
public class SpeciesCatalogDiffTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static string ReadTuning(params string[] relative) => File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(relative).ToArray()));

    static readonly AptitudeTuning RealAptitudes = AptitudeTuningLoader.Parse(ReadTuning("data", "tuning", "aptitudes.v2.json"));
    static readonly DemonShapeTuning RealShape = DemonShapeTuningLoader.Parse(ReadTuning("data", "tuning", "demon-shape.v1.json"));
    static readonly DemonThreatTuning RealThreat = DemonThreatTuningLoader.Parse(ReadTuning("data", "tuning", "demon-threat.v1.json"));
    static readonly PowerTuning RealPower = PowerTuningLoader.Parse(ReadTuning("data", "tuning", "power-scale.v2.json"));

    /// <summary>Resolves a species' CURRENT real anchor file via `_index.json` rather than a
    /// hardcoded path — found broken live, 2026-09-04 (demon-corpus-self-heal): a species' family
    /// bucket is model-decided and moves across reclassifications, so a test hardcoding
    /// `"pea.json"` breaks the moment the pipeline it exercises does its own job correctly.</summary>
    static AnchorRow RealAnchor(string speciesId)
    {
        var indexPath = Path.Combine(RepoRoot(), "data", "seed", "demons", "species", "_index.json");
        var index = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(indexPath))!;
        var relPath = index[speciesId];
        return AnchorRowReader.ReadAll(ReadTuning("data", "seed", "demons", "species", relPath.Replace('/', Path.DirectorySeparatorChar)))
            .Single(a => a.SpeciesId == speciesId);
    }

    /// <summary>A real temp store with the two real classified anchors on disk imported through the
    /// full, real `SpeciesExpander` -> `RpgStore.ImportSpecies` pipeline — not a hand-built fixture.</summary>
    static IReadOnlyList<DemonSpeciesDef> RealStoreBackedSnapshot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fusionrpg-catalogdiff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new RpgStore(dir);
            store.Init();

            var species = new[]
            {
                SpeciesExpander.Expand(RealAnchor("Peashooter"), RealAptitudes, RealPower, RealShape, RealThreat),
                SpeciesExpander.Expand(RealAnchor("SunFlower"), RealAptitudes, RealPower, RealShape, RealThreat),
            };
            var outcome = store.ImportSpecies(species);
            Assert.True(outcome.IsOk, string.Join("; ", outcome.Errors));

            return store.BuildDemonSpeciesSnapshot();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* temp dir */ }
        }
    }

    [Fact]
    public void The_diff_mechanism_finds_the_real_known_differences_for_peashooter_and_sunflower()
    {
        var storeBacked = RealStoreBackedSnapshot();
        var diffs = SpeciesDiff.Compare(DemonSpeciesCatalog.All, storeBacked);

        // peashooter/sunflower are the two real ids the OLD hash-based generator and the NEW
        // anchor-based pipeline both produced — the only species this diff can run against until
        // T2.11's real classification run covers the rest.
        var peashooterDiffs = diffs.Where(d => d.SpeciesId == "peashooter").Select(d => d.Field).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(peashooterDiffs); // real differences exist — this is not a no-op comparison

        // demonTypeId used to differ by construction — RpgStore.BuildDemonSpeciesSnapshot originally
        // computed it as `GameTypeId + DemonTypeIdFloor` with no side split, unlike the old compiled
        // generator's plant/zombie-split space (60000+/10000+). That was a real, undiscovered bug, not
        // a documented divergence: found running the real catalog-runtime flip 2026-09-05 when a plant
        // and a zombie sharing the same raw GameTypeId (BigWallNut/255, BlackTrainZombie/255)
        // collided on the same DemonTypeId and DemonSpeciesCatalog.Validate correctly refused to
        // start. Fixed by reproducing the old generator's own side split in
        // BuildDemonSpeciesSnapshot — the two formulas are now IDENTICAL, so demonTypeId is no longer
        // a diff for any species whose gameTypeId round-trips unchanged (every species here).
        Assert.DoesNotContain("demonTypeId", peashooterDiffs);
        // traitPool still differs by design (open-vocabulary anchor traits vs. the closed gameplay
        // catalog — BuildDemonSpeciesSnapshot's own doc comment) — this is the real, remaining,
        // documented divergence this test now proves instead.
        Assert.Contains("traitPool", peashooterDiffs);
    }

    [Fact]
    public void Fields_that_genuinely_match_are_never_reported_as_differences()
    {
        // side/baseRarity/deployMode/acquisition are real, independently-verified matches between
        // pea.json's own values and the old compiled entry (both "plant"/"cultivated"/
        // "PlantAvatar"/"Summonable") — the diff must stay silent on a field that actually agrees,
        // proving this isn't a mechanism that just flags everything.
        var storeBacked = RealStoreBackedSnapshot();
        var diffs = SpeciesDiff.Compare(DemonSpeciesCatalog.All, storeBacked);
        var peashooterFields = diffs.Where(d => d.SpeciesId == "peashooter").Select(d => d.Field).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("side", peashooterFields);
        Assert.DoesNotContain("baseRarity", peashooterFields);
        Assert.DoesNotContain("deployMode", peashooterFields);
        Assert.DoesNotContain("acquisition", peashooterFields);
    }

    [Fact]
    public void A_species_present_in_only_one_roster_is_reported_as_coverage_not_a_field_diff()
    {
        var storeBacked = RealStoreBackedSnapshot();
        var (onlyCompiled, onlyStoreBacked) = SpeciesDiff.Coverage(DemonSpeciesCatalog.All, storeBacked);

        // The compiled roster has ~84 species; the store today has only the two real anchors this
        // test imported — so "only in compiled" is large and real, "only in store-backed" is empty.
        Assert.True(onlyCompiled.Count > 2);
        Assert.Empty(onlyStoreBacked);
        Assert.DoesNotContain("peashooter", onlyCompiled); // present in both, not a coverage gap
    }

    [Fact]
    public void Comparing_a_roster_against_itself_finds_nothing()
    {
        var storeBacked = RealStoreBackedSnapshot();
        Assert.Empty(SpeciesDiff.Compare(storeBacked, storeBacked));
    }

    [Fact]
    public void The_store_backed_snapshot_itself_passes_DemonSpeciesCatalog_Validate()
    {
        // The snapshot RpgStore.BuildDemonSpeciesSnapshot() produces must be a legal
        // DemonSpeciesCatalog roster on its own terms — proven by actually calling Configure with
        // it (UseScoped, so this never leaks into a test running beside it) rather than assuming
        // Validate would pass.
        var storeBacked = RealStoreBackedSnapshot();
        using (DemonSpeciesCatalog.UseScoped(storeBacked))
        {
            Assert.Equal(2, DemonSpeciesCatalog.All.Count);
            Assert.True(DemonSpeciesCatalog.IsKnown("peashooter"));
            Assert.True(DemonSpeciesCatalog.IsKnown("sunflower"));
        }

        // Restored to the compiled roster outside the scope — proves UseScoped's own isolation.
        Assert.True(DemonSpeciesCatalog.All.Count > 2);
    }
}
