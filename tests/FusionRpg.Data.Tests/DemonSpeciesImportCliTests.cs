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

    /// <summary>
    /// `dotnet run`'s own implicit build races every OTHER concurrent `dotnet build`/`dotnet run`
    /// touching the SAME shared `FusionRpg.Core.dll`/`FusionRpg.Data.dll` output — a transient
    /// MSBuild file-lock (`MSB3026`/`MSB3027`, "is being used by another process"), not a defect in
    /// this CLI or this test. Confirmed real, not hypothetical: reproduced 2026-09-06 under heavy
    /// multi-session load, both failures' own captured stdout showing exactly this. `dotnet`'s own
    /// fixed phrase for "the implicit build failed" — <c>"The build failed. Fix the build errors"</c>
    /// — is the reliable signal to retry on: it can ONLY come from the CLI's own build step, never
    /// from `DemonSpeciesImport`'s own business logic (a build that fails never lets the app start,
    /// so the app's real stdout/stderr, e.g. "written"/"stale", can never contain it either).
    ///
    /// <para><b>A second, more serious defect found while adding the retry above (2026-09-06):</b> the
    /// pre-existing code called <c>proc.StandardOutput.ReadToEnd()</c> then
    /// <c>proc.StandardError.ReadToEnd()</c> <i>before</i> <c>WaitForExit</c> — the classic .NET
    /// process-redirection deadlock (learn.microsoft.com/dotnet/api/system.diagnostics.process.standardoutput):
    /// if the child fills its OS stderr pipe buffer (e.g. a build spewing many MSB3026 retry warnings)
    /// while this thread is still blocked reading stdout, the child blocks writing to a full pipe
    /// nobody is draining, stdout never reaches EOF because the child never exits, and the whole test
    /// hangs forever — reproduced for real the same day (a 17-minute run that never reached the
    /// second test). Fixed by draining both streams asynchronously via
    /// <c>OutputDataReceived</c>/<c>ErrorDataReceived</c>, the standard fix for this exact hazard.</para>
    /// </summary>
    static (int ExitCode, string Stdout, string Stderr) Run(string repoRoot, string args)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                // The test host already built the tool's dependency graph.  A child `dotnet run`
                // must not perform an implicit restore here: this environment deliberately denies
                // the user NuGet.Config, and the restore adds no coverage to a CLI behaviour test.
                Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "DemonSpeciesImport")}\" --no-restore --no-build -- {args}",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var stdout = new System.Text.StringBuilder();
            var stderr = new System.Text.StringBuilder();
            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            var exited = proc.WaitForExit(120_000);
            Assert.True(exited, "DemonSpeciesImport did not exit within 120s");
            // Drains any output still in flight after the process handle reports exited — otherwise a
            // race can read a truncated tail (WaitForExit(int) does not itself guarantee the async
            // stream callbacks have all fired yet).
            proc.WaitForExit();

            var stdoutText = stdout.ToString();
            var isTransientBuildFailure = proc.ExitCode != 0 &&
                stdoutText.Contains("The build failed. Fix the build errors", StringComparison.Ordinal);
            if (!isTransientBuildFailure || attempt >= maxAttempts)
                return (proc.ExitCode, stdoutText, stderr.ToString());

            Thread.Sleep(TimeSpan.FromSeconds(5 * attempt));
        }
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
