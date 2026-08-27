using System.Diagnostics;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// Runs scripts/guard-class-system.ps1 (class-system-todo.md V2) — G1 aptitude ids collision-free,
/// G2 every edge channel registered, G3 no aptitude reaches atk twice, G4 every null unitClass carries
/// a note, G5 at most one AptitudeReadFunctions, G6 DominantPosture never called from a resolve path,
/// G7 Balance/Analytic's damage-computing files reference a shipped combat symbol (added P4.1).
/// Mirrors StatTaxonomyGuardTests'/PowerGuardTests' fixture shape.
///
/// <para>G2/G3 key off the SHIPPED <c>data/tuning/aptitudes.v*.json</c> (P2.1, not yet built) — on the
/// real tree today the guard reports "nothing to check" for those two rules rather than failing on an
/// absence, so their planted-violation tests supply that file inside the fixture to prove the rule
/// itself works once the file exists.</para>
/// </summary>
public class ClassSystemGuardTests
{
    [Fact]
    public void ClassSystemGuard_script_exitsOneOnTheRealTree_onlyG3_permanentlyByDesign()
    {
        // class-system-todo.md P1.5/P1.6 (reader census) closed G4's 29 real-tree findings on
        // 2026-08-26 -- every null unitClass now carries a note. class-system-plan.md decision 12
        // (2026-08-27): G3 (Might/Ferocity feed both combat.power.* and progression.bonus.atk) is a
        // deliberate, PERMANENT forward-looking safeguard for battle-adoption's own transition, not a
        // same-day defect -- the shipped tuning file is not edited to silence it. So the real tree's
        // guard exit is 1, not 0, and stays that way until battle-adoption ships or the design changes.
        // What this test actually proves: exactly G3 fails, nothing else regresses.
        var (exit, stdout, stderr) = Run(FindRepoRoot(), FindRepoRoot());
        Assert.True(exit == 1, $"expected exit=1 (G3 only), got exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("G3 Might:", stdout, StringComparison.Ordinal);
        Assert.Contains("G3 Ferocity:", stdout, StringComparison.Ordinal);
        foreach (var otherRule in new[] { "G1 ", "G2 ", "G4 ", "G5 ", "G6 ", "G7 " })
            Assert.DoesNotContain(otherRule, stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void DeployPlay_invokes_class_system_guard_and_throws_on_failure()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "scripts", "deploy-play.ps1");
        Assert.True(File.Exists(path), "missing " + path);
        var text = File.ReadAllText(path);
        Assert.Contains("guard-class-system.ps1", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CLASS-SYSTEM guard failed", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void G1_fails_on_a_duplicate_aptitude_id()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, """
                {"entries":[
                  {"id":"Might","posture":"force","ordinal":0},
                  {"id":"Might","posture":"finesse","ordinal":1}
                ]}
                """);
            WriteCatalog(fixture, MinimalCatalog());

            var (exit, stdout, _) = Run(fixture, FindRepoRoot());
            Assert.True(exit != 0, "expected G1 to fail on a duplicate id");
            Assert.Contains("G1 Might", stdout, StringComparison.Ordinal);
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G1_fails_when_an_aptitude_id_collides_with_a_channel_family()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, """
                {"entries":[
                  {"id":"combat.power","posture":"force","ordinal":0}
                ]}
                """);
            WriteCatalog(fixture, MinimalCatalog());

            var (exit, stdout, _) = Run(fixture, FindRepoRoot());
            Assert.True(exit != 0, "expected G1 to fail on a channel-family collision");
            Assert.Contains("G1 combat.power", stdout, StringComparison.Ordinal);
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G2_fails_on_an_edge_channel_not_in_the_catalog()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            WriteShippedTuning(fixture, "aptitudes.v1.json", """
                {"edges":[{"channel":"combat.totallyInvented.omni","source":"Might","kMilli":1000}]}
                """);

            var (exit, stdout, _) = Run(fixture, FindRepoRoot());
            Assert.True(exit != 0, "expected G2 to fail on an unregistered edge channel");
            Assert.Contains("G2 Might -> combat.totallyInvented.omni", stdout, StringComparison.Ordinal);
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G2_passes_on_an_edge_channel_that_resolves_by_family_prefix()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog()); // declares family "combat.power"
            WriteShippedTuning(fixture, "aptitudes.v1.json", """
                {"edges":[{"channel":"combat.power.omni","source":"Might","kMilli":1000}]}
                """);

            var (exit, stdout, stderr) = Run(fixture, FindRepoRoot());
            Assert.True(exit == 0, $"expected pass, got exit={exit}\n{stdout}\n{stderr}");
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G3_fails_when_one_source_feeds_both_power_and_bonus_atk()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            WriteShippedTuning(fixture, "aptitudes.v1.json", """
                {"edges":[
                  {"channel":"combat.power.omni","source":"Might","kMilli":2000},
                  {"channel":"progression.bonus.atk","source":"Might","kMilli":1000}
                ]}
                """);

            var (exit, stdout, _) = Run(fixture, FindRepoRoot());
            Assert.True(exit != 0, "expected G3 to fail on a double-counted atk source");
            Assert.Contains("G3 Might", stdout, StringComparison.Ordinal);
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G4_fails_on_a_null_unitClass_with_no_note()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, """
                {"entries":[
                  {"family":"combat.mystery","statClass":"Pool","cap":null,"unitClass":null}
                ]}
                """);

            var (exit, stdout, _) = Run(fixture, FindRepoRoot());
            Assert.True(exit != 0, "expected G4 to fail on an unnoted null unitClass");
            Assert.Contains("G4 combat.mystery", stdout, StringComparison.Ordinal);
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G4_passes_when_the_null_unitClass_carries_a_note()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, """
                {"entries":[
                  {"family":"combat.mystery","statClass":"Pool","cap":null,"unitClass":null,
                   "unitClassNote":"documented reason"}
                ]}
                """);

            var (exit, stdout, stderr) = Run(fixture, FindRepoRoot());
            Assert.True(exit == 0, $"expected pass, got exit={exit}\n{stdout}\n{stderr}");
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G5_fails_on_two_AptitudeReadFunctions_implementations()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            WriteSrcFile(fixture, "One.cs", "namespace X { public static class AptitudeReadFunctions { } }");
            WriteSrcFile(fixture, "Two.cs", "namespace Y { public static class AptitudeReadFunctions { } }");

            var (exit, stdout, _) = Run(fixture, FindRepoRoot());
            Assert.True(exit != 0, "expected G5 to fail on a duplicate implementation");
            Assert.Contains("G5:", stdout, StringComparison.Ordinal);
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G5_passes_on_a_single_AptitudeReadFunctions_implementation()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            WriteSrcFile(fixture, "One.cs", "namespace X { public static class AptitudeReadFunctions { } }");

            var (exit, stdout, stderr) = Run(fixture, FindRepoRoot());
            Assert.True(exit == 0, $"expected pass, got exit={exit}\n{stdout}\n{stderr}");
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G6_fails_when_a_resolve_shaped_file_calls_DominantPosture()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            WriteSrcFile(fixture, "AptitudeResolve.cs",
                "class AptitudeResolve { void Go() { var p = DominantPosture.Of(alloc); } }");

            var (exit, stdout, _) = Run(fixture, FindRepoRoot());
            Assert.True(exit != 0, "expected G6 to fail on a resolve-path call to DominantPosture");
            Assert.Contains("G6 ", stdout, StringComparison.Ordinal);
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G6_passes_when_only_a_non_resolve_file_calls_DominantPosture()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            WriteSrcFile(fixture, "ActorSheetView.cs",
                "class ActorSheetView { void Render() { var p = DominantPosture.Of(alloc); } }");

            var (exit, stdout, stderr) = Run(fixture, FindRepoRoot());
            Assert.True(exit == 0, $"expected pass, got exit={exit}\n{stdout}\n{stderr}");
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G7_fails_when_StrikeMixture_has_no_shipped_combat_symbol_reference()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            // A re-derived sigmoid -- no reference to CombatProbability/ClampedContest/
            // OverlayCombatCalculator/etc. -- exactly the "second combat SSOT" spec-deterministic-
            // core.md §2 forbids.
            WriteAnalyticFile(fixture, "StrikeMixture.cs",
                "class StrikeMixture { static double Sigmoid(double d) => 1.0 / (1.0 + Math.Exp(-d)); }");

            var (exit, stdout, stderr) = Run(fixture, FindRepoRoot());
            Assert.True(exit == 1, $"expected fail, got exit={exit}\n{stdout}\n{stderr}");
            Assert.Contains("G7 ", stdout, StringComparison.Ordinal);
            Assert.Contains("StrikeMixture.cs", stdout, StringComparison.Ordinal);
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G7_passes_when_StrikeMixture_calls_a_shipped_combat_symbol()
    {
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            WriteAnalyticFile(fixture, "StrikeMixture.cs",
                "class StrikeMixture { static double Hit(double d) => CombatProbability.Sigmoid(d, 100); }");

            var (exit, stdout, stderr) = Run(fixture, FindRepoRoot());
            Assert.True(exit == 0, $"expected pass, got exit={exit}\n{stdout}\n{stderr}");
        }
        finally { Cleanup(fixture); }
    }

    [Fact]
    public void G7_ignores_pure_statistics_files_that_call_no_combat_symbol()
    {
        // FirstPassage/Race are generic probability math over numbers StrikeMixture already produced
        // -- they legitimately reference no combat symbol at all, and G7 must not false-positive here.
        var fixture = NewFixture();
        try
        {
            WriteAptitudeRoster(fixture, SingleAptitudeRoster("Might"));
            WriteCatalog(fixture, MinimalCatalog());
            WriteAnalyticFile(fixture, "FirstPassage.cs",
                "class FirstPassage { static double Mean(double h, double mu) => h / mu; }");

            var (exit, stdout, stderr) = Run(fixture, FindRepoRoot());
            Assert.True(exit == 0, $"expected pass, got exit={exit}\n{stdout}\n{stderr}");
        }
        finally { Cleanup(fixture); }
    }

    // ---- fixture plumbing --------------------------------------------------------------------------

    static string MinimalCatalog() => """
        {"entries":[
          {"family":"combat.power","statClass":"Contest","counterpart":"combat.defense","cap":null,"unitClass":"GameUnits"}
        ]}
        """;

    static string SingleAptitudeRoster(string id) => $$"""
        {"entries":[{"id":"{{id}}","posture":"force","ordinal":0}]}
        """;

    static string NewFixture()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fusionrpg-classsystemguard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "data", "seed", "aptitudes"));
        Directory.CreateDirectory(Path.Combine(dir, "data", "seed", "derived-stats"));
        return dir;
    }

    static void WriteAptitudeRoster(string fixtureRoot, string json) =>
        File.WriteAllText(Path.Combine(fixtureRoot, "data", "seed", "aptitudes", "roster.json"), json);

    static void WriteCatalog(string fixtureRoot, string json) =>
        File.WriteAllText(Path.Combine(fixtureRoot, "data", "seed", "derived-stats", "catalog.json"), json);

    static void WriteShippedTuning(string fixtureRoot, string fileName, string json)
    {
        var dir = Path.Combine(fixtureRoot, "data", "tuning");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), json);
    }

    static void WriteSrcFile(string fixtureRoot, string fileName, string csharp)
    {
        var dir = Path.Combine(fixtureRoot, "src");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), csharp);
    }

    static void WriteAnalyticFile(string fixtureRoot, string fileName, string csharp)
    {
        var dir = Path.Combine(fixtureRoot, "src", "FusionRpg.Core", "Balance", "Analytic");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), csharp);
    }

    /// <summary>Runs guard-class-system.ps1 FROM the real repo (so PowerShell itself is found) but
    /// pointed AT the fixture directory via -Root, exactly like StatTaxonomyGuardTests'/PowerGuardTests'
    /// own pattern.</summary>
    static (int Exit, string Stdout, string Stderr) Run(string fixtureRoot, string repoRoot)
    {
        var script = Path.Combine(repoRoot, "scripts", "guard-class-system.ps1");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Root \"{fixtureRoot}\"",
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

    static void Cleanup(string fixture)
    {
        try { Directory.Delete(fixture, recursive: true); } catch { /* temp */ }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var script = Path.Combine(dir.FullName, "scripts", "guard-class-system.ps1");
            if (File.Exists(script)) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root with scripts/guard-class-system.ps1");
    }
}
