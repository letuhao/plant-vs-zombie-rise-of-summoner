using System.Diagnostics;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// End-to-end contract tests for the path-owned local verification workflow. These invoke only
/// plan/static-script modes; they never select an application-wide test suite.
/// </summary>
[Trait("VerificationId", "guard.verification-boundaries")]
public sealed class VerificationBoundaryWorkflowTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "verify-change.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root with scripts/verify-change.ps1");
    }

    private static (int Exit, string Stdout, string Stderr) RunPowerShell(string arguments)
    {
        var root = RepoRoot();
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass {arguments}",
            WorkingDirectory = root,
            CreateNoWindow = true
        };
        return ExternalProcess.Run(psi, 120_000, "verification-boundary script timed out");
    }

    private static (int Exit, string Stdout, string Stderr) RunBoundaryGuard(string root) =>
        RunPowerShell($"-File scripts/guard-verification-boundaries.ps1 -Root \"{root}\"");

    [Fact]
    public void Planner_selects_only_the_socket_group_for_source_and_its_test()
    {
        var (exit, stdout, stderr) = RunPowerShell(
            "-Command \"& .\\scripts\\verify-change.ps1 -Paths @('src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs','tests/FusionRpg.Data.Tests/Items/ItemSocketStoreTests.cs') -AllowUnscoped -PlanOnly -Format json\"");

        Assert.True(exit == 0, $"plan failed exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("data.item-socket", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("FusionRpg.Core.Tests", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("FusionRpg.Server.Tests", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_accepts_an_explicit_path_within_the_active_session_scope()
    {
        var (exit, stdout, stderr) = RunPowerShell(
            "-File scripts/verify-change.ps1 -Paths tests/FusionRpg.Guard.Tests/VerificationBoundaryWorkflowTests.cs -Session verification-boundaries-20260913-6f31 -PlanOnly");

        Assert.True(exit == 0, $"session-scoped plan failed exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("guard-verification-boundary-tests", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_rejects_a_path_outside_the_active_session_scope_before_testing()
    {
        var (exit, stdout, stderr) = RunPowerShell(
            "-File scripts/verify-change.ps1 -Paths src/FusionRpg.Core/MatchTracker.cs -Session verification-boundaries-20260913-6f31 -PlanOnly");

        Assert.True(exit != 0, "out-of-session plan unexpectedly succeeded");
        Assert.Contains("outside session scope", stdout + stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Planner_refuses_an_unmapped_path_instead_of_selecting_a_broad_suite()
    {
        var (exit, stdout, stderr) = RunPowerShell(
            "-File scripts/verify-change.ps1 -Paths docs/DESIGN-GATE.md -AllowUnscoped -PlanOnly");

        Assert.True(exit != 0, "unmapped path unexpectedly selected a test scope");
        Assert.Contains("VERIFICATION BOUNDARY MISSING", stdout + stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_can_select_a_registered_deleted_path_without_reading_the_filesystem()
    {
        var (exit, stdout, stderr) = RunPowerShell(
            "-File scripts/verify-change.ps1 -DeletedPaths src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs -AllowUnscoped -PlanOnly");

        Assert.True(exit == 0, $"deleted-path plan failed exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("data-item-socket", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_requires_a_session_unless_an_explicit_maintainer_override_is_given()
    {
        var (exit, stdout, stderr) = RunPowerShell(
            "-File scripts/verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs -PlanOnly");

        Assert.True(exit != 0, "unscoped agent plan unexpectedly succeeded");
        Assert.Contains("-Session is required", stdout + stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Test_fast_requires_an_explicit_scope()
    {
        var (exit, stdout, stderr) = RunPowerShell("-File scripts/test-fast.ps1");

        Assert.True(exit != 0, "no-argument test-fast unexpectedly selected a broad default");
        Assert.Contains("path-scoped test requires -Project", stdout + stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Integrity_guard_passes_on_the_current_registry()
    {
        var (exit, stdout, stderr) = RunBoundaryGuard(RepoRoot());

        Assert.True(exit == 0, $"guard failed exit={exit}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("VERIFICATION BOUNDARY GUARD OK", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Integrity_guard_rejects_a_selector_that_matches_no_test_trait()
    {
        var root = Path.Combine(Path.GetTempPath(), "verification-boundary-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "scripts"));
            Directory.CreateDirectory(Path.Combine(root, "src", "Fake"));
            Directory.CreateDirectory(Path.Combine(root, "tests", "Fake"));
            File.WriteAllText(Path.Combine(root, "src", "Fake", "Sample.cs"), "namespace Fake; public sealed class Sample { }");
            File.WriteAllText(Path.Combine(root, "tests", "Fake", "Fake.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(Path.Combine(root, "scripts", "fake-guard.ps1"), "exit 0");
            File.WriteAllText(Path.Combine(root, "scripts", "verification-boundaries.v1.json"), """
                {
                  "schemaVersion": 1,
                  "projects": { "fake": "tests/Fake/Fake.csproj" },
                  "guards": { "fake": "scripts/fake-guard.ps1" },
                  "boundaries": [
                    { "id": "fake-boundary", "kind": "owner", "paths": ["src/Fake/**"], "project": "fake", "verificationId": "fake.missing", "guards": ["fake"], "level": "focused" }
                  ]
                }
                """);

            var (exit, stdout, stderr) = RunBoundaryGuard(root);
            Assert.True(exit != 0, "guard accepted a zero-match VerificationId");
            Assert.Contains("VerificationId has no matching test trait: fake.missing", stdout + stderr, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Ci_runs_the_static_integrity_guard_without_filtering_its_full_test_projects()
    {
        var ci = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "ci.yml"));

        Assert.Contains("name: Verification-boundary integrity", ci, StringComparison.Ordinal);
        Assert.Contains(".\\scripts\\guard-verification-boundaries.ps1", ci, StringComparison.Ordinal);
    }
}
