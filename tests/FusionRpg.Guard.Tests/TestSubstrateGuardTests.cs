using System.Diagnostics;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// Runs scripts/guard-test-substrate.ps1 and proves it both passes on the current tree and FAILS on
/// a planted violation. Standard: docs/contributing/testing-standard.md. Added 2026-09-12 with the
/// data-test-substrate program (the leak that hid 65.5 GB of temp dirs).
/// </summary>
public class TestSubstrateGuardTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "guard-test-substrate.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root with scripts/guard-test-substrate.ps1");
    }

    static (int exit, string stdout, string stderr) RunGuard(string root)
    {
        var script = Path.Combine(root, "scripts", "guard-test-substrate.ps1");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Root \"{root}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        return ExternalProcess.Run(psi, 120_000, "test-substrate guard timed out");
    }

    [Fact]
    public void Guard_exits_zero_on_the_current_tree()
    {
        var root = RepoRoot();
        var (exit, stdout, stderr) = RunGuard(root);
        Assert.True(exit == 0, $"guard failed exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("TEST SUBSTRATE GUARD OK", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_fails_on_a_planted_swallowed_delete_and_temp_store()
    {
        var root = RepoRoot();
        var probe = Path.Combine(root, "tests", "FusionRpg.Data.Tests", "ZzGuardProbeTests.cs");
        try
        {
            File.WriteAllText(probe, """
                using Xunit;
                namespace FusionRpg.Data.Tests;
                public class ZzGuardProbeTests
                {
                    [Fact]
                    public void Swallowed()
                    {
                        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "probe");
                        System.IO.Directory.CreateDirectory(dir);
                        new RpgStore(dir).Init();
                        try { System.IO.Directory.Delete(dir, true); } catch { /* temp */ }
                    }
                }
                """);

            var (exit, stdout, _) = RunGuard(root);
            Assert.True(exit != 0, $"expected a failing exit, got 0\nstdout:\n{stdout}");
            Assert.Contains("TEST SUBSTRATE GUARD FAILED", stdout, StringComparison.Ordinal);
            Assert.Contains("swallowed-delete", stdout, StringComparison.Ordinal);
            Assert.Contains("temp-store", stdout, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(probe)) File.Delete(probe);
        }
    }

    [Fact]
    public void Guard_does_not_flag_a_rethrow_cleanup()
    {
        var root = RepoRoot();
        var probe = Path.Combine(root, "tests", "FusionRpg.Data.Tests", "ZzGuardCleanProbeTests.cs");
        try
        {
            File.WriteAllText(probe, """
                using System.IO;
                using Xunit;
                namespace FusionRpg.Data.Tests;
                public class ZzGuardCleanProbeTests
                {
                    [Fact]
                    public void Clean()
                    {
                        var dir = Path.Combine(Path.GetTempPath(), "probe");
                        Directory.CreateDirectory(dir);
                        try { Directory.Delete(dir, recursive: true); }
                        catch (IOException ex) { throw new InvalidOperationException("cleanup failed", ex); }
                    }
                }
                """);

            var (exit, stdout, _) = RunGuard(root);
            Assert.True(exit == 0, $"a rethrow cleanup must not be flagged\nstdout:\n{stdout}");
        }
        finally
        {
            if (File.Exists(probe)) File.Delete(probe);
        }
    }

    [Fact]
    public void DeployPlay_invokes_test_substrate_guard_and_throws_on_failure()
    {
        var root = RepoRoot();
        var text = File.ReadAllText(Path.Combine(root, "scripts", "deploy-play.ps1"));
        Assert.Contains("guard-test-substrate.ps1", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test-substrate guard failed", text, StringComparison.Ordinal);
    }
}
