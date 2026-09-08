using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// A-M1 (spec-movement-payload.md §4/AC4): "a Unity type referenced from this module's own source"
/// must fail a REAL guard. <c>scripts/guard-secondary-no-unity.ps1</c> never reaches
/// <c>src/FusionRpg.Core/Actions/Movement</c> — its own <c>$PluginDir</c> is
/// <c>FusionRpg.Core\Effects\Plugins</c>, and its second sweep only enters a file that implements
/// <c>IEffectGrantPlugin</c> (guard-secondary-no-unity.ps1:9,37-51). A test that merely re-ran that
/// script here would pass for the wrong reason — this scans A-M1's own files directly, the same
/// patterns and shape that script already uses, so nothing new is invented, only re-pointed.
/// </summary>
public class MovementPayloadNoUnityGuardTests
{
    // Same closed pattern list guard-secondary-no-unity.ps1:11-18 already uses — this is that guard's
    // rule, applied to a directory that guard does not reach, not a new rule.
    static readonly string[] Patterns =
    {
        "UnityEngine", "HarmonyLib", "StatusExecutor", "EntityStatWriter", "FindObjectsOfType", "CreateZombie",
    };

    static string MovementDir([CallerFilePath] string here = "")
    {
        var testsDir = Path.GetDirectoryName(here)!;                                  // tests/FusionRpg.Guard.Tests
        var repo = Path.GetFullPath(Path.Combine(testsDir, "..", ".."));              // repo root
        return Path.Combine(repo, "src", "FusionRpg.Core", "Actions", "Movement");
    }

    [Fact]
    public void Movement_module_sources_carry_no_Unity_reference()
    {
        var dir = MovementDir();
        Assert.True(Directory.Exists(dir), $"movement module dir not found: {dir}");

        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, "no movement-payload source files found to scan");

        var offences = Scan(dir);
        Assert.True(offences.Count == 0,
            "A-M1's own no-Unity rule violated (spec-movement-payload.md §2, 'a pure, deterministic " +
            "policy class in Core with no Unity reference'):\n" + string.Join("\n", offences));
    }

    [Fact]
    public void Guard_secondary_no_unity_does_NOT_reach_this_directory_proving_this_test_is_load_bearing()
    {
        // §4's own point: a test that merely re-ran guard-secondary-no-unity.ps1 here would pass for
        // the wrong reason, because that script's $PluginDir is Effects\Plugins and it never walks
        // Actions\Movement. Verified directly against the script's own text so this stays true even if
        // the script changes later (AC4's "vacuous" citation, re-checked mechanically rather than by
        // reading the script once and trusting memory).
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "guard-secondary-no-unity.ps1");
        Assert.True(File.Exists(script), "missing " + script);

        var text = File.ReadAllText(script);
        Assert.Contains(@"Effects\Plugins", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"Actions\Movement", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Actions/Movement", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlantedViolation_a_Unity_reference_in_this_modules_own_source_fails_and_names_the_file()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "fusionrpg-movement-nounity-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmp);
            var offenderPath = Path.Combine(tmp, "BadMovementFile.cs");
            File.WriteAllText(offenderPath,
                "using UnityEngine;\nnamespace FusionRpg.Core.Actions.Movement { class Bad { } }\n");

            var offences = Scan(tmp);

            Assert.Single(offences);
            Assert.Contains("BadMovementFile.cs", offences[0], StringComparison.Ordinal);
            Assert.Contains("UnityEngine", offences[0], StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* temp */ }
        }
    }

    static List<string> Scan(string dir)
    {
        var offences = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            var text = File.ReadAllText(file);
            if (string.IsNullOrEmpty(text)) continue;

            foreach (var pattern in Patterns)
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    offences.Add($"{file}: matches /{pattern}/");
        }
        return offences;
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var script = Path.Combine(dir.FullName, "scripts", "guard-secondary-no-unity.ps1");
            if (File.Exists(script)) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root with scripts/guard-secondary-no-unity.ps1");
    }
}
