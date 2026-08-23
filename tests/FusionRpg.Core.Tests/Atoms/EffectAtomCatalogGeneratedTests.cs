using FusionRpg.Core.Effects;
using Xunit;

namespace FusionRpg.Core.Tests.Atoms;

/// <summary>
/// The checked-in generated file (E23/E11 Step 4) — <c>EffectAtomCatalog.Generated.cs</c>, produced by
/// <c>tools/ElementEnumGen --effect-emit</c> from <c>data/seed/atoms/fx-*.json</c> — proven identical
/// to the hand-written <c>EffectSeedCatalog</c> it replaces at the five call sites, both by DTO shape
/// and by real scenario execution, before anything is repointed at it.
/// </summary>
public class EffectAtomCatalogGeneratedTests
{
    public static IEnumerable<object[]> ScenarioFiles()
    {
        var dir = FindScenariosDir();
        foreach (var path in Directory.GetFiles(dir, "effect-*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            yield return new object[] { path };
    }

    [Fact]
    public void Has_the_same_sixteen_ids_as_the_retired_hand_written_catalog()
    {
        var seeded = EffectSeedCatalog.CreateAll().Select(d => d.EffectId).OrderBy(x => x, StringComparer.Ordinal);
        var generated = EffectAtomCatalog.CreateAll().Select(d => d.EffectId).OrderBy(x => x, StringComparer.Ordinal);

        Assert.Equal(seeded, generated);
    }

    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public void Every_effect_scenario_passes_against_the_generated_catalog(string path)
    {
        var goldenRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));
        var result = EffectScenarioRunner.RunFile(path, goldenRoot, catalog: EffectAtomCatalog.CreateAll());

        Assert.True(result.Ok, $"{result.Id}: {result.Error}");
    }

    static string FindScenariosDir()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "fixtures", "effects", "scenarios");
            if (Directory.Exists(candidate)) return candidate;
            var up = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "fixtures", "effects", "scenarios"));
            if (Directory.Exists(up)) return up;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new DirectoryNotFoundException("fixtures/effects/scenarios");
    }
}
