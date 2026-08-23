using FusionRpg.Core.Effects;
using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Atoms;

/// <summary>
/// The proof E11's `MigrationParityTests` did not carry (completeness-audit.md B2, E23): every
/// `effect-*.json` scenario, run through the real <see cref="EffectScenarioRunner"/> against a catalog
/// built from <c>data/seed/atoms/fx-*.json</c> — <see cref="AtomCompiler.Compile"/> then
/// <see cref="AtomPushCodec.ToDef"/>, both already shipped (E7, E19) — instead of
/// <see cref="EffectSeedCatalog.CreateAll"/>.
///
/// <para><b>What this closes that DTO comparison did not.</b> `MigrationParityTests` proves the
/// compiled <c>EffectDefDto</c> shapes match the hand-written defs' <c>.ToDto()</c> output —
/// structural equality, never loaded into a live <c>EffectBag</c> or run against a scenario's actual
/// event sequence and golden. This is the same 19 fixtures, the same goldens, run for real with the
/// compiled-then-converted catalog as the only difference from
/// <see cref="EffectScenarioRunnerTests.Offline_scenario_passes"/>. If it passes, swapping
/// <c>EffectSeedCatalog.CreateAll()</c> for this catalog at the five call sites is proven safe, not
/// merely argued safe from a RuntimeId choice.</para>
/// </summary>
public class EffectCatalogExecutionParityTests
{
    public static IEnumerable<object[]> ScenarioFiles()
    {
        var dir = FindScenariosDir();
        foreach (var path in Directory.GetFiles(dir, "effect-*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            yield return new object[] { path };
    }

    static List<EffectDef>? _cachedCatalog;

    static List<EffectDef> CompiledCatalog()
    {
        if (_cachedCatalog is not null) return _cachedCatalog;

        var dir = Path.Combine(RepoRoot(), "data", "seed", "atoms");
        var files = Directory.GetFiles(dir, "fx-*.json", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (f, File.ReadAllText(f)))
            .ToArray();

        var collected = AtomSeedFile.Collect(files);
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));

        var compiled = AtomCompiler.Compile(collected.Content.Atoms, RuntimeId.Lawn, 1, hostIsPlanner: true);
        Assert.Empty(compiled.Rejected);
        Assert.Empty(compiled.Runtime);

        _cachedCatalog = compiled.Defs.Select(AtomPushCodec.ToDef).ToList();
        return _cachedCatalog;
    }

    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public void Every_effect_scenario_passes_against_the_compiled_atom_catalog(string path)
    {
        var goldenRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));
        var result = EffectScenarioRunner.RunFile(path, goldenRoot, catalog: CompiledCatalog());

        Assert.True(result.Ok, $"{result.Id}: {result.Error}");
    }

    [Fact]
    public void The_compiled_catalog_has_the_same_sixteen_ids_as_EffectSeedCatalog()
    {
        var seeded = EffectSeedCatalog.CreateAll().Select(d => d.EffectId).OrderBy(x => x, StringComparer.Ordinal);
        var compiled = CompiledCatalog().Select(d => d.EffectId).OrderBy(x => x, StringComparer.Ordinal);

        Assert.Equal(seeded, compiled);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed", "atoms"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("data/seed/atoms");
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
