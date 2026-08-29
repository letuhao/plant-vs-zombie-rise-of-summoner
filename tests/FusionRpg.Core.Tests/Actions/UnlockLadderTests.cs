using System.Runtime.CompilerServices;
using FusionRpg.Core.Actions.Unlock;
using Xunit;

namespace FusionRpg.Core.Tests.Actions;

/// <summary>
/// T19 (action-todo.md, spec-unlock-ladder.md §1): the ratchet. The chance table is the load-bearing
/// assertion — computed independently (Python, exact floating point, rounded once at the very end)
/// against the shipped tuning row, not copied from the spec's own prose table, so a rounding bug in
/// either the spec or a naive per-step implementation would show up as a mismatch here.
/// </summary>
public class UnlockLadderTests
{
    static string TuningPath([CallerFilePath] string here = "")
    {
        var testsDir = Path.GetDirectoryName(here)!;                            // tests/.../Actions
        var repo = Path.GetFullPath(Path.Combine(testsDir, "..", "..", ".."));   // repo root
        return Path.Combine(repo, "data", "tuning", "action-unlock.v1.json");
    }

    static readonly UnlockTuning Shipped = UnlockTuningLoader.Parse(File.ReadAllText(TuningPath()));

    [Theory]
    [InlineData(0, 500)]   // earn 1
    [InlineData(9, 158)]   // earn 10
    [InlineData(10, 139)]  // earn 11
    [InlineData(19, 44)]   // earn 20
    [InlineData(24, 23)]   // earn 25
    [InlineData(39, 3)]    // earn 40
    [InlineData(49, 1)]    // earn 50 -- AT the floor (0.095% rounds under 0.1%, floor clamps it up)
    public void ChanceMatchesTheSpecTableAtEachNamedEarnCount(long earnCountBeforeRoll, int expectedMilli)
    {
        Assert.Equal(expectedMilli, UnlockLadder.ChanceMilli(earnCountBeforeRoll, Shipped));
    }

    [Fact]
    public void ChanceNeverFallsBelowTheFloorNoMatterHowLargeEarnCountGrows()
    {
        Assert.Equal(Shipped.FloorMilli, UnlockLadder.ChanceMilli(1000, Shipped));
        Assert.Equal(Shipped.FloorMilli, UnlockLadder.ChanceMilli(1_000_000, Shipped));
        Assert.Equal(Shipped.FloorMilli, UnlockLadder.ChanceMilli(long.MaxValue / 2, Shipped));
    }

    [Fact]
    public void ChanceIsMonotonicallyNonIncreasing()
    {
        var previous = UnlockLadder.ChanceMilli(0, Shipped);
        for (long n = 1; n <= 60; n++)
        {
            var current = UnlockLadder.ChanceMilli(n, Shipped);
            Assert.True(current <= previous, $"chance rose from {previous} to {current} at earnCount {n}");
            previous = current;
        }
    }

    [Fact]
    public void FloorZeroIsRejectedAtLoadNamingPS8()
    {
        var json = """{ "p1Milli": 500, "deltaMilli": 880, "floorMilli": 0, "cap": 10, "discardTaxCoeffMilli": 100 }""";
        var ex = Assert.Throws<UnlockTuningRejection>(() => UnlockTuningLoader.Parse(json));
        Assert.Contains("PS-8", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RungIsDerivedFromEarnCountAloneNeverOccupancy()
    {
        // "A planted occupancy-keyed implementation fails": UnlockLadder.Rung takes ONLY earnCount --
        // there is no slot/position parameter it could even read, so an occupancy-keyed variant is
        // not a bug this signature can express, let alone hide.
        Assert.Equal(1, UnlockLadder.Rung(1, Shipped));
        Assert.Equal(5, UnlockLadder.Rung(5, Shipped));
        Assert.Equal(10, UnlockLadder.Rung(10, Shipped));   // at cap
        Assert.Equal(10, UnlockLadder.Rung(11, Shipped));   // past cap -- clamped, "arrives at the top rung"
        Assert.Equal(10, UnlockLadder.Rung(1000, Shipped)); // arbitrarily far past cap -- still clamped
    }

    [Fact]
    public void RungOfZeroEarnsIsZero()
    {
        Assert.Equal(0, UnlockLadder.Rung(0, Shipped));
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(0)]
    [InlineData(-1)]
    public void P1OutOfRangeIsRejectedAtLoad(int badP1)
    {
        var json = $$"""{ "p1Milli": {{badP1}}, "deltaMilli": 880, "floorMilli": 1, "cap": 10, "discardTaxCoeffMilli": 100 }""";
        Assert.Throws<UnlockTuningRejection>(() => UnlockTuningLoader.Parse(json));
    }

    [Theory]
    [InlineData(1000)] // never decays
    [InlineData(0)]
    public void DeltaOutOfRangeIsRejectedAtLoad(int badDelta)
    {
        var json = $$"""{ "p1Milli": 500, "deltaMilli": {{badDelta}}, "floorMilli": 1, "cap": 10, "discardTaxCoeffMilli": 100 }""";
        Assert.Throws<UnlockTuningRejection>(() => UnlockTuningLoader.Parse(json));
    }

    [Fact]
    public void CapBelowOneIsRejectedAtLoad()
    {
        var json = """{ "p1Milli": 500, "deltaMilli": 880, "floorMilli": 1, "cap": 0, "discardTaxCoeffMilli": 100 }""";
        Assert.Throws<UnlockTuningRejection>(() => UnlockTuningLoader.Parse(json));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DiscardTaxCoeffZeroOrBelowIsRejectedAtLoad(int badCoeff)
    {
        var json = $$"""{ "p1Milli": 500, "deltaMilli": 880, "floorMilli": 1, "cap": 10, "discardTaxCoeffMilli": {{badCoeff}} }""";
        Assert.Throws<UnlockTuningRejection>(() => UnlockTuningLoader.Parse(json));
    }
}
