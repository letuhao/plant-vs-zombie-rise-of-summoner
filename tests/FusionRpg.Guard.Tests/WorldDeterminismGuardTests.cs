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

    /// <summary>
    /// W26 (spec-ai-commander.md §Boundaries): a policy reads belief, never the truth.
    ///
    /// This is the one rule in the module that no behavioural test can cover. An AI that consulted
    /// `WorldState` would not give *wrong* answers — it would give suspiciously good ones, and every
    /// test asserting it plays well would pass. The only way to catch a right answer arrived at by
    /// cheating is to make the source of the cheat unmentionable.
    /// </summary>
    [Fact]
    public void Nothing_under_World_Ai_may_read_the_world_itself()
    {
        var violations = new List<string>();

        foreach (var file in WorldSourceFiles())
        {
            if (!file.Replace('\\', '/').Contains("/World/Ai/", StringComparison.Ordinal)) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (ReadsTheWorldItself(lines[i]))
                    violations.Add($"{Path.GetFileName(file)}:{i + 1} -> {lines[i].Trim()}");
        }

        Assert.True(violations.Count == 0,
            "a faction policy must read IWorldView, never the world itself:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// One line's verdict, factored out so the rule can be proven directly rather than only by
    /// planting a violation in the tree and remembering to take it out again.
    /// </summary>
    static bool ReadsTheWorldItself(string line)
    {
        // A comment explaining *why* the type is out of bounds is the one place naming it is not
        // only allowed but wanted — every file under World/Ai/ says so at the top.
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return false;

        return line.Contains("WorldState", StringComparison.Ordinal);
    }

    [Fact]
    public void The_belief_only_guard_would_actually_catch_a_violation()
    {
        // Seen to fail, so it is known to work.
        Assert.True(ReadsTheWorldItself("    static WorldState? Cheat;"));
        Assert.True(ReadsTheWorldItself("        var truth = world.Sectors; // WorldState leaked in"));

        // And seen not to fire on the things it must not: the doc comments, and a type whose name
        // merely starts the same way.
        Assert.False(ReadsTheWorldItself("/// never touches WorldState — see the spec"));
        Assert.False(ReadsTheWorldItself("// WorldState is deliberately unreachable from here"));
        Assert.False(ReadsTheWorldItself("        var view = new BelievedWorldView(...);"));
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
