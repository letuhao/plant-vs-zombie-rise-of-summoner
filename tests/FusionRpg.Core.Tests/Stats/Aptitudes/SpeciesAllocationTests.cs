using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.Stats.Aptitudes;

/// <summary>`species-build` T2.1 (module 5, `demon-type-allocation`) — the pure baseline math.
/// Uses the real shipped `aptitudes.v5.json` (same convention as `SpeciesCatalogDiffTests`' own
/// `RepoRoot()` helper) rather than constructing the whole `AptitudeTuning` record inline — only
/// `PointEconomy.AptitudePointsPerThetaMilliByScope[DemonType]` is actually read by this code path.</summary>
public class SpeciesAllocationTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static readonly AptitudeTuning RealTuning = AptitudeTuningLoader.Parse(
        File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "aptitudes.v5.json")));

    static long DemonTypeRate => RealTuning.PointEconomy.AptitudePointsPerThetaMilliByScope[AllocationScope.DemonType];

    static readonly Dictionary<string, long> ThreeWaySplit = new(StringComparer.Ordinal)
    {
        ["Might"] = 500, ["Vigor"] = 300, ["Fortitude"] = 200
    };

    [Fact]
    public void Baseline_at_level_one_is_empty_never_a_ceiling()
    {
        // budget-source's own zero-at-level-1 rule (T0.4): DemonTypeSourceFromLevel(1) = 0.
        var result = SpeciesAllocation.Baseline(ThreeWaySplit, speciesLevel: 1, RealTuning);
        Assert.Same(AptitudeAllocation.Empty, result);
    }

    [Fact]
    public void Baseline_at_level_zero_is_also_empty()
    {
        var result = SpeciesAllocation.Baseline(ThreeWaySplit, speciesLevel: 0, RealTuning);
        Assert.Same(AptitudeAllocation.Empty, result);
    }

    [Fact]
    public void Baseline_scales_the_plans_shares_by_the_demonType_budget()
    {
        const long level = 21; // DemonTypeSourceFromLevel(21) = 20
        var result = SpeciesAllocation.Baseline(ThreeWaySplit, level, RealTuning);
        var budget = 20 * DemonTypeRate;

        Assert.Equal(budget, result.TotalForScope(AllocationScope.DemonType));
        // Largest-remainder rounding, but the ORDER of shares (500:300:200) must still hold at this scale.
        var might = result.PointsAt(AllocationScope.DemonType, "Might");
        var vigor = result.PointsAt(AllocationScope.DemonType, "Vigor");
        var fortitude = result.PointsAt(AllocationScope.DemonType, "Fortitude");
        Assert.True(might > vigor && vigor > fortitude, $"expected Might({might}) > Vigor({vigor}) > Fortitude({fortitude})");
    }

    [Fact]
    public void Baseline_sums_to_exactly_the_budget_including_awkward_remainders()
    {
        // A level chosen so 1000-permille shares against the real rate force a non-round division.
        const long level = 8; // source = 7
        var result = SpeciesAllocation.Baseline(ThreeWaySplit, level, RealTuning);
        var budget = 7 * DemonTypeRate;
        Assert.Equal(budget, result.TotalForScope(AllocationScope.DemonType));
    }

    [Fact]
    public void Baseline_with_no_plan_entry_for_the_species_is_empty()
    {
        var result = SpeciesAllocation.Baseline(new Dictionary<string, long>(), speciesLevel: 50, RealTuning);
        Assert.Same(AptitudeAllocation.Empty, result);
    }

    [Fact]
    public void Baseline_rejects_a_plan_share_naming_an_unknown_aptitude()
    {
        var bad = new Dictionary<string, long>(StringComparer.Ordinal) { ["NotAnAptitude"] = 1000 };
        Assert.Throws<ArgumentException>(() => SpeciesAllocation.Baseline(bad, speciesLevel: 10, RealTuning));
    }

    [Fact]
    public void ScopeKey_is_per_player_and_per_species()
    {
        Assert.Equal("player:1:species:fumeshroom", SpeciesAllocation.ScopeKey(1, "fumeshroom"));
        Assert.NotEqual(
            SpeciesAllocation.ScopeKey(1, "fumeshroom"),
            SpeciesAllocation.ScopeKey(2, "fumeshroom"));
        Assert.NotEqual(
            SpeciesAllocation.ScopeKey(1, "fumeshroom"),
            SpeciesAllocation.ScopeKey(1, "wallnut"));
    }

    [Fact]
    public void Overflow_an_extreme_level_throws_rather_than_wraps()
    {
        Assert.Throws<OverflowException>(() =>
            SpeciesAllocation.Baseline(ThreeWaySplit, speciesLevel: long.MaxValue - 1, RealTuning));
    }
}
