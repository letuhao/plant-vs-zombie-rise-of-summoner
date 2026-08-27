using System.Diagnostics;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// Runs scripts/gate-class-system-phase9.ps1 (class-system-todo.md P9.0) — the mechanical readiness
/// gate for Phase 9 ("tune on real data"). Nobody decides "we are ready" by eye: the gate wraps
/// scripts/audit-reader-census.py (P8.4) and reports READY only when the census itself says every
/// aptitude-fed family has a reader and _meta.measurable's own prose still agrees with a fresh run.
///
/// <para>audit-reader-census.py has no -Root override (its paths are hardcoded to the real repo tree,
/// by design — see its own module boundary, spec-residual-fit.md §5 "ships no src/ code"). Rather than
/// extending that already-shipped, already-tested script's scope to support a synthetic fixture tree,
/// the gate itself exposes a narrower test seam (-CensusJsonPath) that swaps in a canned census report
/// shaped exactly like audit-reader-census.py's own --json output. This proves the gate's own readiness
/// arithmetic in both directions without touching the census script or needing a fully-built game.</para>
/// </summary>
public class ClassSystemPhase9ReadinessGateTests
{
    [Fact]
    public void Gate_exitsOneOnTheRealTree_todayNotReady()
    {
        // class-system-plan.md §0.1 / Phase 9 header: real data cannot exist until every mechanism
        // (actions, passives, skills, items) does, so this MUST report NOT READY today — that is this
        // task's own explicit acceptance line, not a defect this test should ever expect to go green
        // before the rest of the game is built.
        var (exit, stdout, stderr) = Run(FindRepoRoot(), censusJsonPath: null);
        Assert.True(exit == 1, $"expected exit=1 (not ready today), got exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("PHASE 9 READINESS GATE: NOT READY", stdout, StringComparison.Ordinal);
        Assert.Contains("aptitude-fed families still have no reader", stdout, StringComparison.Ordinal);
        // Matches the live _meta.measurable roster (P8.4) — if this list ever shrinks to empty, the
        // gate is supposed to flip to READY, not silently keep failing.
        Assert.Contains("resource.efficiency", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_exitsZero_whenCensusReportsZeroReaderLessFamilies()
    {
        var fixtureJson = WriteCensusFixture(new
        {
            families_total = 48,
            families_with_reader = 48,
            families_without_reader = 0,
            edges_total = 486,
            edges_unmapped = Array.Empty<string>(),
            edges_reserved = 0,
            edges_reserved_pct = 0.0,
            reader_less_families = Array.Empty<string>()
        });
        try
        {
            var (exit, stdout, stderr) = Run(FindRepoRoot(), fixtureJson);
            Assert.True(exit == 0, $"expected exit=0 (fully ready), got exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.Contains("PHASE 9 READINESS GATE: READY", stdout, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(fixtureJson);
        }
    }

    [Fact]
    public void Gate_exitsOne_whenCensusReportsReaderLessFamilies_andNamesThemInTheReport()
    {
        var fixtureJson = WriteCensusFixture(new
        {
            families_total = 10,
            families_with_reader = 8,
            families_without_reader = 2,
            edges_total = 100,
            edges_unmapped = Array.Empty<string>(),
            edges_reserved = 7,
            edges_reserved_pct = 7.0,
            reader_less_families = new[] { "planted.familyOne", "planted.familyTwo" }
        });
        try
        {
            var (exit, stdout, stderr) = Run(FindRepoRoot(), fixtureJson);
            Assert.True(exit == 1, $"expected exit=1 (planted gap), got exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.Contains("PHASE 9 READINESS GATE: NOT READY", stdout, StringComparison.Ordinal);
            Assert.Contains("planted.familyOne", stdout, StringComparison.Ordinal);
            Assert.Contains("planted.familyTwo", stdout, StringComparison.Ordinal);
            Assert.Contains("2 of 10", stdout, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(fixtureJson);
        }
    }

    [Fact]
    public void Gate_notWiredIntoDeployPlay_becauseNotReadyIsExpectedForALongTime()
    {
        // Deliberate: unlike guard-class-system.ps1 (a code-invariant guard, wired in and meant to stay
        // green), this gate is EXPECTED to report NOT READY until the whole game is built (class-system-
        // plan.md §0.1). Wiring it into deploy-play.ps1's "throw on failure" convention would break every
        // deploy today for an honestly-expected state, not a regression — so it must not be wired there.
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "scripts", "deploy-play.ps1");
        Assert.True(File.Exists(path), "missing " + path);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("gate-class-system-phase9.ps1", text, StringComparison.OrdinalIgnoreCase);
    }

    static string WriteCensusFixture(object payload)
    {
        var path = Path.Combine(Path.GetTempPath(), $"phase9-census-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(payload));
        return path;
    }

    /// <summary>Runs gate-class-system-phase9.ps1 FROM the real repo (so PowerShell itself is found).
    /// When censusJsonPath is null, exercises the real python-backed path against the live tree;
    /// otherwise swaps in the fixture via -CensusJsonPath. Mirrors ClassSystemGuardTests' own Run().</summary>
    static (int Exit, string Stdout, string Stderr) Run(string repoRoot, string? censusJsonPath)
    {
        var script = Path.Combine(repoRoot, "scripts", "gate-class-system-phase9.ps1");
        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"";
        if (censusJsonPath is not null) args += $" -CensusJsonPath \"{censusJsonPath}\"";
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        Assert.True(p.WaitForExit(60_000), "gate script timed out");
        return (p.ExitCode, stdout, stderr);
    }

    static void Cleanup(string fixturePath)
    {
        try { File.Delete(fixturePath); } catch { /* temp */ }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var script = Path.Combine(dir.FullName, "scripts", "gate-class-system-phase9.ps1");
            if (File.Exists(script)) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root with scripts/gate-class-system-phase9.ps1");
    }
}
