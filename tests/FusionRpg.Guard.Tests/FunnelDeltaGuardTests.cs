using System.Diagnostics;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// Runs scripts/guard-funnel-delta.ps1 so CI/local test runs
/// keep Secondary plugins off TakeDamage / SetHp / Bag.Grant.
/// </summary>
public class FunnelDeltaGuardTests
{
    [Fact]
    public void DeployPlay_invokes_funnel_delta_guard_and_throws_on_failure()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "scripts", "deploy-play.ps1");
        Assert.True(File.Exists(path), "missing " + path);
        var text = File.ReadAllText(path);
        Assert.Contains("guard-funnel-delta.ps1", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("funnel delta guard failed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_script_exits_zero()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "guard-funnel-delta.ps1");
        Assert.True(File.Exists(script), "missing " + script);

        var (exit, stdout, stderr) = RunScript(script, repoRoot);
        Assert.True(exit == 0,
            $"guard failed exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("FUNNEL DELTA GUARD OK", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_script_exits_nonzero_when_plugin_calls_TakeDamage()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "guard-funnel-delta.ps1");
        var fixture = Path.Combine(Path.GetTempPath(), "fusionrpg-funnel-td-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pluginDir = Path.Combine(fixture, "src", "FusionRpg.Core", "Effects", "Plugins");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllText(
                Path.Combine(pluginDir, "BadDamagePlugin.cs"),
                "namespace X { class Bad { void Hit() { z.TakeDamage(10); } } }\n");

            var (exit, stdout, stderr) = RunScript(script, fixture);
            Assert.True(exit != 0,
                $"expected fail exit, got 0\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.Contains("FUNNEL DELTA GUARD FAILED", stdout, StringComparison.Ordinal);
            Assert.Contains("TakeDamage", stdout, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(fixture, recursive: true); } catch { /* temp */ }
        }
    }

    [Fact]
    public void Guard_script_exits_nonzero_when_plugin_calls_Bag_Grant()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "guard-funnel-delta.ps1");
        var fixture = Path.Combine(Path.GetTempPath(), "fusionrpg-funnel-grant-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pluginDir = Path.Combine(fixture, "src", "FusionRpg.Core", "Effects", "Plugins");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllText(
                Path.Combine(pluginDir, "BadGrantPlugin.cs"),
                "namespace X { class Bad { void G(EffectPluginContext ctx) { ctx.Bag.Grant(dto); } } }\n");

            var (exit, stdout, stderr) = RunScript(script, fixture);
            Assert.True(exit != 0,
                $"expected fail exit, got 0\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.Contains("FUNNEL DELTA GUARD FAILED", stdout, StringComparison.Ordinal);
            Assert.Contains("Bag.Grant", stdout, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(fixture, recursive: true); } catch { /* temp */ }
        }
    }

    [Fact]
    public void Guard_script_exits_zero_when_injector_TakeDamage_is_not_a_plugin()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "guard-funnel-delta.ps1");
        var fixture = Path.Combine(Path.GetTempPath(), "fusionrpg-funnel-hot-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(fixture, "src", "FusionRpg.Core", "Effects", "Plugins"));
            Directory.CreateDirectory(Path.Combine(fixture, "src", "FusionRpg.Injector"));
            File.WriteAllText(
                Path.Combine(fixture, "src", "FusionRpg.Injector", "GameHooks.cs"),
                "namespace FusionRpg.Injector { class GameHooks { void Hit() { z.TakeDamage(1); } } }\n");

            var (exit, stdout, stderr) = RunScript(script, fixture);
            Assert.True(exit == 0,
                $"expected OK exit, got {exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.Contains("FUNNEL DELTA GUARD OK", stdout, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(fixture, recursive: true); } catch { /* temp */ }
        }
    }

    [Fact]
    public void EffectFunnel_fa10_params_have_no_absolute_hp_keys()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "src", "FusionRpg.Core", "Effects", "EffectFunnel.cs");
        Assert.True(File.Exists(path), "missing " + path);
        var text = File.ReadAllText(path);
        Assert.Contains("EffectActions.ApplyResourceDelta", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"setHp\"]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"absoluteHp\"]", text, StringComparison.Ordinal);
    }

    static (int Exit, string Stdout, string Stderr) RunScript(string script, string root)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Root \"{root}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        Assert.True(p.WaitForExit(60_000), "guard script timed out");
        return (p.ExitCode, stdout, stderr);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var scripts = Path.Combine(dir.FullName, "scripts", "guard-secondary-no-unity.ps1");
            if (File.Exists(scripts)) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root with scripts/guard-secondary-no-unity.ps1");
    }
}
