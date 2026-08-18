using System.Diagnostics;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// Runs scripts/guard-dal.ps1, asserts Sqlite package ownership, and that deploy-play wires the DAL gate (Slice E).
/// </summary>
public class DalGuardTests
{
    [Fact]
    public void DeployPlay_invokes_dal_guard_and_throws_on_failure()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "scripts", "deploy-play.ps1");
        Assert.True(File.Exists(path), "missing " + path);
        var text = File.ReadAllText(path);
        Assert.Contains("guard-dal.ps1", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DAL guard failed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DalGuard_script_exits_zero()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "guard-dal.ps1");
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
        Assert.Contains("DAL GUARD OK", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void DalGuard_script_exits_nonzero_when_Sqlite_outside_Data()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "guard-dal.ps1");
        Assert.True(File.Exists(script), "missing " + script);

        var fixture = Path.Combine(Path.GetTempPath(), "fusionrpg-guard-fail-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "src", "FusionRpg.Data"));
            var badDir = Path.Combine(fixture, "src", "FusionRpg.Server");
            Directory.CreateDirectory(badDir);
            File.WriteAllText(
                Path.Combine(badDir, "BadSql.cs"),
                "using Microsoft.Data.Sqlite;\nnamespace X { class Bad { SqliteConnection C; } }\n");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Root \"{fixture}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            Assert.True(p.WaitForExit(60_000), "guard script timed out");
            Assert.True(p.ExitCode != 0,
                $"expected fail exit, got 0\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.Contains("DAL GUARD FAILED", stdout, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(fixture, recursive: true); } catch { /* temp */ }
        }
    }

    [Fact]
    public void Server_csproj_has_no_Sqlite_PackageReference()
    {
        var repoRoot = FindRepoRoot();
        var csproj = Path.Combine(repoRoot, "src", "FusionRpg.Server", "FusionRpg.Server.csproj");
        Assert.True(File.Exists(csproj), "missing " + csproj);
        var text = File.ReadAllText(csproj);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Data_csproj_references_Sqlite()
    {
        var repoRoot = FindRepoRoot();
        var csproj = Path.Combine(repoRoot, "src", "FusionRpg.Data", "FusionRpg.Data.csproj");
        Assert.True(File.Exists(csproj), "missing " + csproj);
        var text = File.ReadAllText(csproj);
        Assert.Contains("Microsoft.Data.Sqlite", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Server_csproj_ProjectReferences_Data()
    {
        var repoRoot = FindRepoRoot();
        var csproj = Path.Combine(repoRoot, "src", "FusionRpg.Server", "FusionRpg.Server.csproj");
        Assert.True(File.Exists(csproj), "missing " + csproj);
        var text = File.ReadAllText(csproj);
        Assert.Contains("FusionRpg.Data", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProjectReference", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Injector_and_Core_csproj_have_no_Sqlite()
    {
        var repoRoot = FindRepoRoot();
        foreach (var rel in new[]
                 {
                     Path.Combine("src", "FusionRpg.Injector", "FusionRpg.Injector.csproj"),
                     Path.Combine("src", "FusionRpg.Core", "FusionRpg.Core.csproj")
                 })
        {
            var csproj = Path.Combine(repoRoot, rel);
            Assert.True(File.Exists(csproj), "missing " + csproj);
            var text = File.ReadAllText(csproj);
            Assert.DoesNotContain("Microsoft.Data.Sqlite", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sqlite", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var scripts = Path.Combine(dir.FullName, "scripts", "guard-dal.ps1");
            if (File.Exists(scripts)) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root with scripts/guard-dal.ps1");
    }
}
