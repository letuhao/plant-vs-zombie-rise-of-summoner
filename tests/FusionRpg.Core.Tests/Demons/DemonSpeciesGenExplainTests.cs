using System.Diagnostics;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>
/// T4.5's own `--explain` acceptance line: "names every input for one species." A real, cold
/// `dotnet run` of the tool, same pattern `RealColdProcessTests.cs` (AtomImporter.Tests) already
/// established — every other test in this file's neighbourhood exercises `SpeciesExpander`
/// in-process, which proves the MATH but never proves the CLI itself actually prints the audit
/// trail a balance question would be answered from.
/// </summary>
public class DemonSpeciesGenExplainTests
{
    [Fact]
    public void Explain_names_every_real_input_for_a_real_species()
    {
        var repoRoot = FindRepoRoot();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "DemonSpeciesGen")}\" -- --explain Peashooter",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        var exited = proc.WaitForExit(120_000);

        Assert.True(exited, "DemonSpeciesGen --explain did not exit within 120s");
        Assert.True(proc.ExitCode == 0, $"expected exit 0, got {proc.ExitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}");

        // Every real input this species' derivation reads, named in the transcript — the audit trail
        // the spec's own §8 promises ("a balance question gets answered without reading the code").
        foreach (var mustName in new[]
        {
            "speciesId", "rarity", "threatBand", "thetaOffset", "speciesBaseTheta", "theta", "pTheta",
            "aptitudePrimary", "aptitudeSecondary", "attackTempo", "attackIntervalMs", "reach",
            "rangeCells", "variants", "variantCount", "magnitudes",
        })
            Assert.Contains(mustName, stdout, StringComparison.Ordinal);

        Assert.Contains("Peashooter", stdout, StringComparison.Ordinal);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tools", "DemonSpeciesGen"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
