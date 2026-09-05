using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// `species-build` T2.3 (module 5, `demon-type-allocation`) — spec-demon-type-allocation.md's own
/// named risk: "composition lives behind a single named entry point that returns the effective
/// allocation, and `LoadAllocation` is not called directly by any consumer of species allocation." A
/// caller that reads `LoadAllocation` directly and forgets to compose the baseline gets a silently
/// inert species (the same silent-zero shape this codebase has already been bitten by once). Text-scan
/// guard, matching `DalGuardTests`' own established rigor for this repo's boundary guards.
/// </summary>
public class SpeciesAllocationSeamTests
{
    [Fact]
    public void No_file_other_than_RpgStoreAptitudes_calls_LoadAllocation_with_the_DemonType_scope()
    {
        var repoRoot = FindRepoRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        var violations = new List<string>();

        foreach (var file in Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            // RpgStore.Aptitudes.cs IS the one named entry point (EffectiveSpeciesAllocation) — it is
            // allowed, and required, to call LoadAllocation(DemonType, ...) internally.
            if (Path.GetFileName(file) == "RpgStore.Aptitudes.cs") continue;

            var text = File.ReadAllText(file);
            if (text.Contains("LoadAllocation(AllocationScope.DemonType", StringComparison.Ordinal))
                violations.Add(file);
        }

        Assert.True(violations.Count == 0,
            "LoadAllocation(AllocationScope.DemonType, ...) called directly outside " +
            "RpgStore.Aptitudes.cs -- route species allocation reads through " +
            "RpgStore.EffectiveSpeciesAllocation instead. Offending file(s): " +
            string.Join(", ", violations));
    }

    [Fact]
    public void AptitudeEndpoints_species_routes_use_EffectiveSpeciesAllocation_not_LoadAllocation()
    {
        // The one real production consumer today (the species read/write endpoints) — asserted by
        // name rather than only by the negative scan above, so this guard fails loudly if that file
        // is ever refactored to bypass the entry point rather than just adding a NEW bypassing file.
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "src", "FusionRpg.Server", "AptitudeEndpoints.cs");
        Assert.True(File.Exists(path), "missing " + path);
        var text = File.ReadAllText(path);
        Assert.Contains("EffectiveSpeciesAllocation", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadAllocation(AllocationScope.DemonType", text, StringComparison.Ordinal);
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
