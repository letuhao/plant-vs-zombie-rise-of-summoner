using System.Diagnostics;
using Xunit;

namespace FusionRpg.AtomImporter.Tests;

/// <summary>A real, cold `dotnet run` of the tool as a genuinely separate process — every other test
/// in this project runs in-process inside the test host, which already has every tuning hub
/// configured globally (`ContractTuningTestBootstrap`'s `[ModuleInitializer]`), so none of them could
/// ever catch a missing `Configure(...)` call the standalone binary itself needs. Found for real
/// 2026-08-30 running an actual deploy: `RpgStore`'s static ctor (T2's `ComposeKindRegistry`) needs
/// `DerivedStatPolicy.Tuning`, and this tool never configured it — the in-process test suite was
/// 21/21 green the whole time this was broken.</summary>
public class RealColdProcessTests
{
    [Fact]
    public void A_real_cold_run_against_a_fresh_db_does_not_crash_on_RpgStore_s_static_ctor()
    {
        var repoRoot = FindRepoRoot();
        var dbDir = Path.Combine(Path.GetTempPath(), "fusionrpg-atomimporter-cold-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dbDir);
        try
        {
            var (exitCode, stdout, stderr) = RunCold(repoRoot, dbDir);

            Assert.True(exitCode == 0,
                $"expected exit 0, got {exitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.DoesNotContain("type initializer", stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(dbDir, recursive: true); } catch { /* temp dir */ }
        }
    }

    /// <summary>
    /// `dotnet run`'s own implicit build races every OTHER concurrent `dotnet build`/`dotnet run`
    /// touching the SAME shared output DLLs — a transient MSBuild file-lock, not a defect in this
    /// tool or this test. Confirmed real, not hypothetical, under heavy multi-session load
    /// (2026-09-06, `CreatureSpeciesImportCliTests.cs`'s own identical fix, same day). `dotnet`'s own
    /// fixed phrase for "the implicit build failed" is the reliable retry signal: it can only come
    /// from the CLI's own build step, never from AtomImporter's own business logic.
    ///
    /// <para><b>A second, more serious defect found the same day:</b> the pre-existing code called
    /// <c>proc.StandardOutput.ReadToEnd()</c> then <c>proc.StandardError.ReadToEnd()</c> before
    /// <c>WaitForExit</c> — the classic .NET process-redirection deadlock: if the child fills its OS
    /// stderr pipe buffer while this thread is still blocked reading stdout, the child blocks writing
    /// to a full pipe nobody is draining and the whole test hangs forever (reproduced for real the
    /// same day in `CreatureSpeciesImportCliTests.cs`'s identical pattern — a 17-minute run that never
    /// finished). Fixed by draining both streams asynchronously via
    /// <c>OutputDataReceived</c>/<c>ErrorDataReceived</c>, the standard fix for this exact hazard.</para>
    /// </summary>
    static (int ExitCode, string Stdout, string Stderr) RunCold(string repoRoot, string dbDir)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "AtomImporter")}\" -c Release -- --db \"{dbDir}\"",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
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
            Assert.True(exited, "AtomImporter did not exit within 120s");
            proc.WaitForExit(); // drain any async callbacks still in flight after the exit signal

            var stdoutText = stdout.ToString();
            var isTransientBuildFailure = proc.ExitCode != 0 &&
                stdoutText.Contains("The build failed. Fix the build errors", StringComparison.Ordinal);
            if (!isTransientBuildFailure || attempt >= maxAttempts)
                return (proc.ExitCode, stdoutText, stderr.ToString());

            Thread.Sleep(TimeSpan.FromSeconds(5 * attempt));
        }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tools", "AtomImporter"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
