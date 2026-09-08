using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Core.Tests.Demons.Fusion;

/// <summary>
/// The real, full anchor corpus (~829 species) expanded through the exact production pipeline
/// (anchor -> `SpeciesExpander` -> temp `RpgStore` -> `BuildDemonSpeciesSnapshot`) and built ONCE
/// for every test in this folder that needs it — `FusionRecipeDistributionIndexTests` (T8.1) and
/// `FusionRecipeReconcileTests` (T8.3) both need the identical real-corpus snapshot, and repeating
/// the ~800-species import per test class would multiply real disk + expansion work for no reason.
/// `SpeciesCatalogDiffTests.cs`'s own `RealStoreBackedSnapshot()` is NOT reused here — that helper
/// imports exactly 2 named species for a narrowly-scoped diff claim, a different shape of fixture
/// entirely.
/// </summary>
internal static class RealCorpusFixture
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

    static string ReadTuning(params string[] relative) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(relative).ToArray()));

    static readonly AptitudeTuning RealAptitudes = AptitudeTuningLoader.Parse(ReadTuning("data", "tuning", "aptitudes.v2.json"));
    static readonly DemonShapeTuning RealShape = DemonShapeTuningLoader.Parse(ReadTuning("data", "tuning", "demon-shape.v1.json"));
    static readonly DemonThreatTuning RealThreat = DemonThreatTuningLoader.Parse(ReadTuning("data", "tuning", "demon-threat.v1.json"));
    static readonly PowerTuning RealPower = PowerTuningLoader.Parse(ReadTuning("data", "tuning", "power-scale.v2.json"));

    public static readonly IReadOnlyList<DemonSpeciesDef> Snapshot = Build();

    static IReadOnlyList<DemonSpeciesDef> Build()
    {
        var seedRoot = Path.Combine(RepoRoot(), "data", "seed", "demons", "species");
        var anchors = new List<AnchorRow>();
        foreach (var file in Directory.GetFiles(seedRoot, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).StartsWith('_')) continue;
            anchors.AddRange(AnchorRowReader.ReadAll(File.ReadAllText(file)));
        }

        var species = new List<ConcreteSpecies>();
        foreach (var anchor in anchors)
        {
            if (SpeciesExpander.UnresolvedFields(anchor).Count > 0) continue; // same skip species-import/the CLI tools both apply
            species.Add(SpeciesExpander.Expand(anchor, RealAptitudes, RealPower, RealShape, RealThreat));
        }
        Assert.True(species.Count > 700, $"expected the real corpus to resolve well over 700 species, got {species.Count} — seed data may have moved");

        var dir = Path.Combine(Path.GetTempPath(), "fusionrpg-realcorpus-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new RpgStore(dir);
            store.Init();
            var outcome = store.ImportSpecies(species);
            Assert.True(outcome.IsOk, string.Join("; ", outcome.Errors));
            return store.BuildDemonSpeciesSnapshot();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* temp dir */ }
        }
    }
}
