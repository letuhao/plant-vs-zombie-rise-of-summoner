using System.Text.RegularExpressions;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// W8 (spec-turn-engine.md §Determinism): the world simulation's purity, enforced as a source scan
/// rather than as a convention someone remembers.
///
/// A wall-clock read or a `System.Random` inside the turn path would not fail a test — it would
/// quietly make replays disagree between machines, weeks later, with no obvious cause. Cheaper to
/// forbid the symbols outright.
/// </summary>
public class WorldDeterminismGuardTests
{
    static readonly string[] BannedSymbols =
    {
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow",
        "Environment.TickCount",
        "Stopwatch",
        "System.Random",
        "new Random("
    };

    [Fact]
    public void The_world_simulation_reads_no_clock_and_rolls_no_unowned_dice()
    {
        var violations = new List<string>();

        foreach (var file in WorldSourceFiles())
        {
            var text = File.ReadAllText(file);
            foreach (var banned in BannedSymbols)
            {
                var index = text.IndexOf(banned, StringComparison.Ordinal);
                if (index < 0) continue;

                var line = text.Take(index).Count(c => c == '\n') + 1;
                violations.Add($"{Path.GetFileName(file)}:{line} → {banned}");
            }
        }

        Assert.True(violations.Count == 0,
            "world simulation purity violated (no wall clock, no unowned RNG):\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Game_affecting_world_state_carries_no_floating_point()
    {
        // Integer or fixed-point only: a float in stored state is a cross-machine hash difference
        // waiting to happen.
        var floats = new Regex(@"\b(double|float|decimal)\s+\w+\s*[;={)]", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var file in WorldSourceFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (floats.IsMatch(lines[i]))
                    violations.Add($"{Path.GetFileName(file)}:{i + 1} → {lines[i].Trim()}");
        }

        Assert.True(violations.Count == 0,
            "world state must stay integer/fixed-point:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void The_guard_would_actually_catch_a_violation()
    {
        // A guard nobody has seen fail is a guard nobody knows works.
        const string sample = "var now = DateTime.UtcNow; // sneaked in";
        Assert.Contains(BannedSymbols, banned => sample.Contains(banned, StringComparison.Ordinal));
    }

    [Fact]
    public void The_scan_actually_finds_the_world_sources()
    {
        var files = WorldSourceFiles().ToList();
        Assert.True(files.Count >= 8, $"expected the world module's sources, found {files.Count}");
        Assert.Contains(files, f => f.EndsWith("TurnEngine.cs", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("WorldState.cs", StringComparison.Ordinal));
    }

    static IEnumerable<string> WorldSourceFiles()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "FusionRpg.Core", "World");
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FusionRpg.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
