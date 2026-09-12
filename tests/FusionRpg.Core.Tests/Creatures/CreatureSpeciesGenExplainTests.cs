using System.Diagnostics;
using System.Linq;
using FusionRpg.Core.Tests.TestSupport;
using Xunit;

namespace FusionRpg.Core.Tests.Creatures;

/// <summary>
/// T4.5's own `--explain` acceptance line: "names every input for one species." A real, cold
/// `dotnet run` of the tool, same pattern `RealColdProcessTests.cs` (AtomImporter.Tests) already
/// established — every other test in this file's neighbourhood exercises `SpeciesExpander`
/// in-process, which proves the MATH but never proves the CLI itself actually prints the audit
/// trail a balance question would be answered from.
/// </summary>
public class CreatureSpeciesGenExplainTests
{
    [Fact]
    public void Explain_names_every_real_input_for_a_real_species()
    {
        var repoRoot = FindRepoRoot();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "CreatureSpeciesGen")}\" --no-restore -- --explain Peashooter",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var (exitCode, stdout, stderr) = ExternalProcess.Run(psi, 120_000, "CreatureSpeciesGen --explain did not exit within 120s");
        Assert.True(exitCode == 0, $"expected exit 0, got {exitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}");

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

    /// <summary>
    /// T2.7's own "small future export step" (spec-anchor-emit.md §6, `legacy_diff.py`'s own
    /// docstring) — closed 2026-09-02. A real, cold run against the real shipped catalog, proving
    /// the file `legacy_diff.py` needs actually gets produced, in the shape it expects, not just
    /// that the tool exits 0.
    /// </summary>
    [Fact]
    public void ExportLegacy_writes_every_shipped_species_in_the_legacy_diff_shape()
    {
        var repoRoot = FindRepoRoot();
        var outPath = Path.Combine(Path.GetTempPath(), "fusionrpg-legacy-export-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "CreatureSpeciesGen")}\" --no-restore -- --export-legacy \"{outPath}\"",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var (exitCode, stdout, stderr) = ExternalProcess.Run(psi, 120_000, "CreatureSpeciesGen --export-legacy did not exit within 120s");
            Assert.True(exitCode == 0, $"expected exit 0, got {exitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(outPath), "export wrote no file");

            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(outPath));
            var rows = doc.RootElement;
            Assert.Equal(System.Text.Json.JsonValueKind.Array, rows.ValueKind);
            Assert.True(rows.GetArrayLength() >= 80, $"expected the real ~84-species catalog, got {rows.GetArrayLength()}");

            var pea = rows.EnumerateArray().Single(r => r.GetProperty("id").GetString() == "peashooter");
            Assert.True(pea.TryGetProperty("elementPrimary", out _));
            Assert.True(pea.TryGetProperty("deployMode", out _));
            Assert.Equal(System.Text.Json.JsonValueKind.Array, pea.GetProperty("acquisition").ValueKind);
            Assert.Equal(System.Text.Json.JsonValueKind.Array, pea.GetProperty("variants").ValueKind);
        }
        finally
        {
            try { File.Delete(outPath); } catch { /* temp */ }
        }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tools", "CreatureSpeciesGen"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
