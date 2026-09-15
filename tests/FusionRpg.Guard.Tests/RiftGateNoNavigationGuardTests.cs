using System.Text.RegularExpressions;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// Map Decision 17: <b>switch the WebView; never fight the PVZ engine.</b> No rift-gate code may call a
/// <c>UIMgr</c> navigation method (or any engine navigation action) to change what the player sees.
/// The injector may **read** menu presence and **attach** a UI affordance to an existing screen; it may
/// not drive the engine's menu state.
///
/// This is a rule a reviewer would otherwise have to hold in their head every time they touch the
/// menu. It goes red the moment someone reaches for the obvious-but-forbidden shortcut — the exact
/// shortcut that a documented live hazard (<c>DebugActions.cs:1495-1496</c>) and a mid-run breaker
/// make unsafe. A comment cannot enforce that; this can.
///
/// Scope: the rift-gate sources only. The pre-existing observe-only patches on <c>UIMgr</c> live in
/// <c>GameCaptureHooks.cs</c> and are deliberately NOT in scope — they observe, and the rule is about
/// *calling*, not about patching what already exists.
/// </summary>
public class RiftGateNoNavigationGuardTests
{
    /// <summary>The engine navigation methods rift-gate must never call.</summary>
    static readonly string[] ForbiddenNavCalls =
    {
        "EnterMainMenu", "BackToMenu", "EnterPauseMenu", "BackToGame",
    };

    /// <summary>
    /// Rift-gate sources, discovered rather than listed so a new file is covered the day it is added.
    /// The program's own files are recognisable by name; scanning the whole injector would flag the
    /// sanctioned read-only debug surface (DebugActions drives navigation on purpose — it is the
    /// debug API, not rift-gate).
    /// </summary>
    static IEnumerable<string> RiftGateSources()
    {
        var root = FindRepoRoot();
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)) continue;

            var name = Path.GetFileName(file);
            if (name.StartsWith("Rift", StringComparison.Ordinal) ||
                name is "MenuPresenceHook.cs" ||
                name.Contains("RiftMenu", StringComparison.Ordinal))
                yield return file;
        }
    }

    [Fact]
    public void No_rift_gate_source_calls_a_UI_navigation_method()
    {
        var files = RiftGateSources().ToList();

        // Anchor the guard on positive evidence it scanned something: a guard that quietly finds no
        // files passes forever and protects nothing.
        Assert.True(files.Count >= 2,
            $"expected at least the presence hook + a rift source, found {files.Count}");

        var offenders = new List<string>();
        foreach (var file in files)
        {
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.StartsWith("///", StringComparison.Ordinal)) continue;

                foreach (var nav in ForbiddenNavCalls)
                {
                    // A CALL: `UIMgr.EnterMainMenu(`, `.BackToMenu(` — not a mere mention in a
                    // comment or a doc string (those are stripped above).
                    if (Regex.IsMatch(line, $@"\.{nav}\s*\("))
                        offenders.Add($"{Path.GetFileName(file)}: {line}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Decision 17 forbids calling a UIMgr navigation method from rift-gate code " +
            "(read presence; never drive the engine's menu state): " + string.Join(" | ", offenders));
    }

    /// <summary>
    /// The forbidden methods must still exist somewhere in the injector — otherwise this guard would
    /// pass merely because the names were renamed. Anchored on the debug surface that legitimately
    /// drives navigation (the Game-Injector-Debug scope), so the grep target is proven live.
    /// </summary>
    [Fact]
    public void The_forbidden_method_names_are_still_real_call_sites_elsewhere()
    {
        var root = FindRepoRoot();
        var debugActions = Path.Combine(root, "src", "FusionRpg.Injector", "DebugActions.cs");
        Assert.True(File.Exists(debugActions), "missing " + debugActions);

        var text = File.ReadAllText(debugActions);
        Assert.Contains("EnterMainMenu", text, StringComparison.Ordinal);
        Assert.Contains("BackToMenu", text, StringComparison.Ordinal);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
