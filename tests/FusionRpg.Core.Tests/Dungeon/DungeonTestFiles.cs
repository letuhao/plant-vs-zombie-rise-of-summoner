using System.IO;

namespace FusionRpg.Core.Tests.Dungeon;

/// <summary>Locates the repo root from the test binary's output directory — the
/// <c>EligibilityAxisTests.FindRepoRoot</c> shape, reused so every dungeon test reads the real,
/// shipped registry and tuning files rather than a hand-transcribed copy (tunables-ssot.md §7.2:
/// "the balance surface is the file", not a fixture that can drift from it).</summary>
public static class DungeonTestFiles
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed", "dungeon"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }

    public static string RegistryDir() => Path.Combine(RepoRoot(), "data", "seed", "dungeon", "_registry");
    public static string DungeonTuningPath() => Path.Combine(RepoRoot(), "data", "tuning", "dungeon.v1.json");
    public static string EncounterTuningPath() => Path.Combine(RepoRoot(), "data", "tuning", "encounter.v1.json");
}
