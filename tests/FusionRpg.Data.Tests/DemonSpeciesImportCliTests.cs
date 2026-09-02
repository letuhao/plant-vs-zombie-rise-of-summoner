using System.Diagnostics;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// T4.6's own CLI-level acceptance line: "a stale generated tree refuses." A real, cold
/// `dotnet run` of `tools/DemonSpeciesImport`, the same pattern `AtomImporter.Tests`'
/// `RealColdProcessTests.cs` already established — `SpeciesImportStoreTests.cs` covers the DAL half
/// (`RpgStore.ImportSpecies`) directly; this covers the pre-flight only the CLI itself runs.
/// </summary>
public class DemonSpeciesImportCliTests : IDisposable
{
    readonly string _dbDir;

    public DemonSpeciesImportCliTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), "fusionrpg-species-import-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dbDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dbDir, recursive: true); } catch { /* temp dir */ }
    }

    static (int ExitCode, string Stdout, string Stderr) Run(string repoRoot, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "DemonSpeciesImport")}\" -- {args}",
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
        Assert.True(exited, "DemonSpeciesImport did not exit within 120s");
        return (proc.ExitCode, stdout, stderr);
    }

    [Fact]
    public void A_real_import_against_the_real_committed_tree_succeeds_and_writes_a_real_store()
    {
        var repoRoot = FindRepoRoot();

        var (exitCode, stdout, stderr) = Run(repoRoot, $"--db \"{_dbDir}\"");

        Assert.True(exitCode == 0, $"expected exit 0, got {exitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("written", stdout, StringComparison.Ordinal);

        var store = new RpgStore(_dbDir);
        store.Init();
        Assert.NotEmpty(store.ListSpeciesIds());
        Assert.Contains("Peashooter", store.ListSpeciesIds());
    }

    [Fact]
    public void A_stale_committed_file_refuses_the_whole_import_and_writes_nothing()
    {
        var repoRoot = FindRepoRoot();
        var realOutDir = Path.Combine(repoRoot, "data", "generated", "demons");
        var scratchOutDir = Path.Combine(_dbDir, "stale-generated");
        Directory.CreateDirectory(scratchOutDir);

        // A committed tree that does not match what the real anchors would re-derive to — every
        // real generated file, but with a body no re-derivation could ever produce.
        foreach (var file in Directory.GetFiles(realOutDir, "*.json"))
            File.WriteAllText(Path.Combine(scratchOutDir, Path.GetFileName(file)), "{\"stale\":true}\n");

        var (exitCode, stdout, stderr) = Run(repoRoot, $"--db \"{_dbDir}\" --out \"{scratchOutDir}\"");

        Assert.Equal(1, exitCode);
        Assert.Contains("stale", stderr, StringComparison.OrdinalIgnoreCase);

        var store = new RpgStore(_dbDir);
        store.Init();
        Assert.Empty(store.ListSpeciesIds()); // the whole import refused — nothing written
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tools", "DemonSpeciesImport"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
