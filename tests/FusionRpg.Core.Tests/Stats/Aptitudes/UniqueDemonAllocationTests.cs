using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.Stats.Aptitudes;

/// <summary>passive-tree G7 (spec-species-tree.md §8.1 point 2) — the `UniqueDemon`-scope twin of
/// <see cref="SpeciesAllocationTests"/>, one test per test there, same shared-tuning convention
/// (`SpeciesAllocationTests`'s own `RepoRoot()`/real-`aptitudes.v7.json` precedent) — only
/// `PointEconomy.AptitudePointsPerThetaMilliByScope[UniqueDemon]` is actually read by this code
/// path (the sibling table D55 did not touch).</summary>
public class UniqueDemonAllocationTests
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
        File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "aptitudes.v7.json")));

    static long UniqueDemonRate => RealTuning.PointEconomy.AptitudePointsPerThetaMilliByScope[AllocationScope.UniqueDemon];

    static readonly Dictionary<string, long> ThreeWaySplit = new(StringComparer.Ordinal)
    {
        ["Might"] = 500, ["Vigor"] = 300, ["Fortitude"] = 200
    };

    [Fact]
    public void Baseline_at_level_one_is_empty_never_a_ceiling()
    {
        // A freshly-created specimen (RpgStore.CreateUniqueActor starts every row at level 1) must
        // read exactly zero, or every roster entry that has never fought would own free points.
        var result = UniqueDemonAllocation.Baseline(ThreeWaySplit, specimenLevel: 1, RealTuning);
        Assert.Same(AptitudeAllocation.Empty, result);
    }

    [Fact]
    public void Baseline_at_level_zero_is_also_empty()
    {
        var result = UniqueDemonAllocation.Baseline(ThreeWaySplit, specimenLevel: 0, RealTuning);
        Assert.Same(AptitudeAllocation.Empty, result);
    }

    [Fact]
    public void Baseline_scales_the_plans_shares_by_the_uniqueDemon_budget()
    {
        const long level = 21; // UniqueDemonSourceFromLevel(21) = 20
        var result = UniqueDemonAllocation.Baseline(ThreeWaySplit, level, RealTuning);
        var budget = 20 * UniqueDemonRate;

        Assert.Equal(budget, result.TotalForScope(AllocationScope.UniqueDemon));
        // Largest-remainder rounding, but the ORDER of shares (500:300:200) must still hold at this scale.
        var might = result.PointsAt(AllocationScope.UniqueDemon, "Might");
        var vigor = result.PointsAt(AllocationScope.UniqueDemon, "Vigor");
        var fortitude = result.PointsAt(AllocationScope.UniqueDemon, "Fortitude");
        Assert.True(might > vigor && vigor > fortitude, $"expected Might({might}) > Vigor({vigor}) > Fortitude({fortitude})");
    }

    [Fact]
    public void Baseline_sums_to_exactly_the_budget_including_awkward_remainders()
    {
        // A level chosen so 1000-permille shares against the real rate force a non-round division.
        const long level = 8; // source = 7
        var result = UniqueDemonAllocation.Baseline(ThreeWaySplit, level, RealTuning);
        var budget = 7 * UniqueDemonRate;
        Assert.Equal(budget, result.TotalForScope(AllocationScope.UniqueDemon));
    }

    [Fact]
    public void Baseline_with_no_plan_entry_for_the_specimen_is_empty()
    {
        var result = UniqueDemonAllocation.Baseline(new Dictionary<string, long>(), specimenLevel: 50, RealTuning);
        Assert.Same(AptitudeAllocation.Empty, result);
    }

    [Fact]
    public void Baseline_rejects_a_plan_share_naming_an_unknown_aptitude()
    {
        var bad = new Dictionary<string, long>(StringComparer.Ordinal) { ["NotAnAptitude"] = 1000 };
        Assert.Throws<ArgumentException>(() => UniqueDemonAllocation.Baseline(bad, specimenLevel: 10, RealTuning));
    }

    [Fact]
    public void Overflow_an_extreme_level_throws_rather_than_wraps()
    {
        Assert.Throws<OverflowException>(() =>
            UniqueDemonAllocation.Baseline(ThreeWaySplit, specimenLevel: long.MaxValue - 1, RealTuning));
    }
}
