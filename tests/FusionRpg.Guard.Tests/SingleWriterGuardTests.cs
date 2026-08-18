using System.Diagnostics;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// Runs scripts/guard-single-writer.ps1 under dotnet test so CI/local test runs
/// enforce the EntityStatWriter-only combat field invariant.
/// </summary>
public class SingleWriterGuardTests
{
    [Fact]
    public void Guard_script_exits_zero()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "guard-single-writer.ps1");
        Assert.True(File.Exists(script), "missing " + script);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Root \"{repoRoot}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        Assert.True(p.WaitForExit(60_000), "guard script timed out");
        Assert.True(p.ExitCode == 0,
            $"guard failed exit={p.ExitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("SINGLE-WRITER GUARD OK", stdout, StringComparison.Ordinal);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var scripts = Path.Combine(dir.FullName, "scripts", "guard-single-writer.ps1");
            if (File.Exists(scripts)) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root with scripts/guard-single-writer.ps1");
    }
}
