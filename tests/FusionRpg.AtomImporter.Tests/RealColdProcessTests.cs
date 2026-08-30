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
            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            var exited = proc.WaitForExit(120_000);

            Assert.True(exited, "AtomImporter did not exit within 120s");
            Assert.True(proc.ExitCode == 0,
                $"expected exit 0, got {proc.ExitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.DoesNotContain("type initializer", stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(dbDir, recursive: true); } catch { /* temp dir */ }
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
