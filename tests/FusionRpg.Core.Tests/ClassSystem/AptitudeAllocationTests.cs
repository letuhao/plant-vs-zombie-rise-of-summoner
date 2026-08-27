using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.ClassSystem;

/// <summary>class-system-todo.md P1.2 (AptitudeAllocation + AllocationScope) and P1.3 (DominantPosture).</summary>
public class AptitudeAllocationTests
{
    [Fact]
    public void EmptyAllocationHasAllZeroShares_neverOneTwelfth()
    {
        foreach (var apt in AptitudeCatalog.All)
        {
            Assert.Equal(0, AptitudeAllocation.Empty.Total(apt.Id));
            Assert.Equal(0.0, AptitudeAllocation.Empty.Share(apt.Id));
        }
        Assert.Equal(0, AptitudeAllocation.Empty.GrandTotal());
    }

    [Fact]
    public void AdditionIsCommutative()
    {
        var a = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 30);
        var b = AptitudeAllocation.Single(AllocationScope.UniqueDemon, "Vigor", 70);

        var ab = a + b;
        var ba = b + a;

        foreach (var apt in AptitudeCatalog.All)
            Assert.Equal(ab.Total(apt.Id), ba.Total(apt.Id));
        Assert.Equal(ab.GrandTotal(), ba.GrandTotal());
    }

    [Fact]
    public void AdditionIsAssociative()
    {
        var a = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 10);
        var b = AptitudeAllocation.Single(AllocationScope.DemonType, "Might", 20);
        var c = AptitudeAllocation.Single(AllocationScope.Aspect, "Might", 30);

        var left = (a + b) + c;
        var right = a + (b + c);
        Assert.Equal(60, left.Total("Might"));
        Assert.Equal(60, right.Total("Might"));
    }

    [Fact]
    public void SharesSumToOneWhenNonEmpty()
    {
        var alloc = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 25)
                  + AptitudeAllocation.Single(AllocationScope.Aspect, "Vigor", 25)
                  + AptitudeAllocation.Single(AllocationScope.UniqueDemon, "Focus", 50);

        var sum = alloc.Shares().Values.Sum();
        Assert.Equal(1.0, sum, 12);
    }

    [Fact]
    public void ScopesSumBeforeShare()
    {
        // Two DIFFERENT scopes both fund Might, nothing else funded anywhere -- share must read 100%
        // off the SUM across scopes, not off either scope considered alone.
        var alloc = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 50)
                  + AptitudeAllocation.Single(AllocationScope.UniqueDemon, "Might", 50);

        Assert.Equal(100, alloc.Total("Might"));
        Assert.Equal(1.0, alloc.Share("Might"));
        Assert.Equal(0, alloc.Total("Vigor"));
    }

    [Fact]
    public void ExactAtOneBillion()
    {
        // "exact at Theta = 10^9" -- long arithmetic stays exact far past this; the share ratio is a
        // bounded [0,1] double by design (CLAUDE.md: bounded ratios are exempt from the long-only rule).
        const long huge = 1_000_000_000L;
        var alloc = AptitudeAllocation.Single(AllocationScope.Commander, "Might", huge)
                  + AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", huge);

        Assert.Equal(huge, alloc.Total("Might"));
        Assert.Equal(huge, alloc.Total("Vigor"));
        Assert.Equal(2 * huge, alloc.GrandTotal());
        Assert.Equal(0.5, alloc.Share("Might"), 12);
    }

    [Fact]
    public void OverflowThrowsNeverClamps()
    {
        // Same (scope, aptitude) key on both sides -- the merge path actually adds, rather than just
        // inserting a second independent key.
        var a = AptitudeAllocation.Single(AllocationScope.Commander, "Might", long.MaxValue);
        var b = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 1);
        Assert.Throws<OverflowException>(() => a + b);
    }

    [Fact]
    public void NegativePointsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AptitudeAllocation.Single(AllocationScope.Commander, "Might", -1));
    }

    [Fact]
    public void UnknownAptitudeIdRejects()
    {
        Assert.Throws<ArgumentException>(() => AptitudeAllocation.Single(AllocationScope.Commander, "NotAnAptitude", 10));
    }

    [Fact]
    public void ZeroPointsSingleIsEmpty()
    {
        var alloc = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 0);
        Assert.Equal(0, alloc.GrandTotal());
    }

    // ── DominantPosture (P1.3) ──────────────────────────────────────────────────────────────────

    [Fact]
    public void DominantPosture_picksTheClearLeader()
    {
        // Three Force aptitudes funded, nothing else -- Force must lead.
        var alloc = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 40)
                  + AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", 40)
                  + AptitudeAllocation.Single(AllocationScope.Commander, "Onslaught", 20);
        Assert.Equal(Posture.Force, DominantPosture.Of(alloc));
    }

    [Fact]
    public void DominantPosture_tieReturnsNull()
    {
        var alloc = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 50)     // Force
                  + AptitudeAllocation.Single(AllocationScope.Commander, "Agility", 50);   // Finesse
        Assert.Null(DominantPosture.Of(alloc));
    }

    [Fact]
    public void DominantPosture_emptyReturnsNull()
    {
        Assert.Null(DominantPosture.Of(AptitudeAllocation.Empty));
    }

    [Fact]
    public void DominantPosture_isNeverAField()
    {
        // Structural: DominantPosture exposes only a static read, no instance state, no setter --
        // there is nothing an allocation type could hold that would make this a stored field instead
        // of a derived value.
        var t = typeof(DominantPosture);
        Assert.True(t.IsAbstract && t.IsSealed); // static class
        Assert.Empty(t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
    }
}
