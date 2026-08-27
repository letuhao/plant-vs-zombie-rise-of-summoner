using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace FusionRpg.Core.Tests.Balance;

/// <summary>class-system-todo.md Checkpoint 8 — <c>tools/DominanceBaseline</c> reproduces
/// _baseline-dominance.json's dominanceMatrix/dominantCorners fields via the SHIPPED
/// FusionRpg.Core.DominanceGuard/TerminationGuard (the same production resolver TerminationGuard.Assert
/// uses), reading the LIVE data/tuning/aptitudes.v*.json config automatically — unlike
/// tools/CombatSim's trinity command, no internal copy, no concurrent-edit hazard. Runs the real
/// `dotnet run` invocation (same cold-start-fixture pattern as ProveAptitudeJsonEmitTests/
/// ResidualFitLoopTests) rather than re-implementing the tool's own logic in the test.</summary>
public class DominanceBaselineTests
{
    [Fact]
    public void DefaultInvocation_onTheLiveShippedConfig_emitsATwelveByTwelveMatrix()
    {
        var (exit, stdout, stderr) = Run("--theta 100");
        Assert.True(exit == 0, $"exit {exit}\n{stdout}\n{stderr}");

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        var names = root.GetProperty("dominanceMatrix").GetProperty("names");
        var wins = root.GetProperty("dominanceMatrix").GetProperty("wins");
        var unending = root.GetProperty("dominanceMatrix").GetProperty("unending");

        Assert.Equal(12, names.GetArrayLength());
        Assert.Equal(12, wins.GetArrayLength());
        Assert.Equal(12, unending.GetArrayLength());
        foreach (var row in wins.EnumerateArray()) Assert.Equal(12, row.GetArrayLength());
        foreach (var row in unending.EnumerateArray()) Assert.Equal(12, row.GetArrayLength());
    }

    [Fact]
    public void DefaultInvocation_onTheLiveShippedConfig_matchesP85sOwnAlreadyRecordedFinding()
    {
        // class-residual-2026-08-27.md's P8.5 section: "no absolute dominant corner (0/66 pairs
        // unending, matching P8.3); Retribution is the new near-dominant corner (wins 10 of 11, loses
        // only to Pierce)". This test proves the SHIPPED tool reproduces that same headline finding via
        // an independent invocation, not a re-statement of the earlier ad-hoc measurement.
        var (exit, stdout, stderr) = Run("--theta 100");
        Assert.True(exit == 0, $"exit {exit}\n{stdout}\n{stderr}");

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.Empty(root.GetProperty("dominantCorners").EnumerateArray());

        var names = root.GetProperty("dominanceMatrix").GetProperty("names").EnumerateArray()
            .Select(e => e.GetString()!).ToArray();
        var wins = root.GetProperty("dominanceMatrix").GetProperty("wins");
        var unending = root.GetProperty("dominanceMatrix").GetProperty("unending");
        var retribution = Array.IndexOf(names, "Retribution");
        var pierce = Array.IndexOf(names, "Pierce");
        Assert.True(retribution >= 0 && pierce >= 0);

        var retributionRow = wins[retribution].EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var winsAgainst = retributionRow.Where((w, j) => j != retribution && w > 0.5).Count();
        Assert.Equal(10, winsAgainst); // wins 10 of the other 11
        Assert.True(retributionRow[pierce] < 0.01, $"expected Retribution to lose to Pierce, got {retributionRow[pierce]}");

        for (var i = 0; i < 12; i++)
        for (var j = 0; j < 12; j++)
        {
            if (i == j) continue;
            Assert.False(unending[i][j].GetBoolean(), $"unexpected unending pair at [{i},{j}] ({names[i]} vs {names[j]})");
        }
    }

    [Fact]
    public void Run_isDeterministic_identicalInputProducesIdenticalMatrix()
    {
        // No RNG anywhere in DominanceGuard.Measure/TerminationGuard.Assert (both pure closed-form) —
        // two invocations against the live config must be byte-for-byte identical.
        var (exit1, stdout1, _) = Run("--theta 100");
        var (exit2, stdout2, _) = Run("--theta 100");
        Assert.Equal(0, exit1);
        Assert.Equal(0, exit2);
        Assert.Equal(stdout1, stdout2);
    }

    static (int Exit, string Stdout, string Stderr) Run(string args)
    {
        var repoRoot = FindRepoRoot();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "DominanceBaseline")}\" -c Release --no-build -- {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = repoRoot
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        Assert.True(p.WaitForExit(120_000), "DominanceBaseline invocation timed out");
        return (p.ExitCode, stdout, stderr);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
