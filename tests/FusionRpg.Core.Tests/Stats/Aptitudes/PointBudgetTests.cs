using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.Stats.Aptitudes;

/// <summary>class-system-todo.md P6.1 — <see cref="PointBudget"/> (spec-point-economy.md, read in
/// full this session). Table in §7: tests 1-5 covered here — the ones that are actually
/// <see cref="PointBudget"/>'s/<see cref="AptitudeAllocation"/>'s own concern. Tests 6-9 (respec,
/// persistence, the two-currency rule) belong to `RespecPolicy`/`AllocationStore`, P6.2/P6.3,
/// unbuilt — spec-point-economy.md §5's own project-structure listing keeps them in separate files
/// for the same reason `balance-guard` keeps `TerminationGuard`/`DominanceGuard` in separate
/// files: the standing/scope difference is the design, not an implementation detail.</summary>
public class PointBudgetTests
{
    static string FindShippedAptitudesTuningPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "tuning", "aptitudes.v2.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not locate data/tuning/aptitudes.v2.json above " + AppContext.BaseDirectory);
    }

    static AptitudeTuning ShippedTuning() =>
        AptitudeTuningLoader.Parse(File.ReadAllText(FindShippedAptitudesTuningPath()));

    [Fact]
    public void Four_scopes_sum_to_the_effective_allocation()
    {
        // spec-point-economy.md §2: "An actor's allocation is the SUM of four" and share is taken on
        // the sum, never per scope (§2, spec-primary-stats.md §6 rule 4) — the same invariant
        // Total/Share/GrandTotal already implement (Phase 1); this locks it in from point-economy's
        // own testing table so a future change to either side is caught from both directions.
        var allocation =
            AptitudeAllocation.Single(AllocationScope.Commander, "Might", 10)
            + AptitudeAllocation.Single(AllocationScope.DemonType, "Might", 20)
            + AptitudeAllocation.Single(AllocationScope.Aspect, "Might", 30)
            + AptitudeAllocation.Single(AllocationScope.UniqueDemon, "Might", 40)
            + AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", 100);

        Assert.Equal(100, allocation.Total("Might")); // 10+20+30+40, all four scopes summed
        Assert.Equal(200, allocation.GrandTotal()); // Might 100 + Vigor 100

        // Share is taken on the SUM (100/200 = 0.5), never per scope (e.g. NOT 10/100 for commander
        // alone) — a per-scope share would let one scope's 100%-in-one-aptitude allocation outweigh
        // another scope's broad spread, which is exactly the wrong number spec-primary-stats.md §6
        // rule 4 refuses.
        Assert.Equal(0.5, allocation.Share("Might"), precision: 12);
    }

    [Fact]
    public void Each_scope_draws_from_its_own_budget()
    {
        // spec-point-economy.md §7 test 2: overspending one scope cannot be covered by another.
        var tuning = ShippedTuning();
        var commanderRate = tuning.PointEconomy.AptitudePointsPerThetaMilliByScope[AllocationScope.Commander];
        var commanderBudget = PointBudget.PointsFor(AllocationScope.Commander, sourceValue: 100, tuning);
        Assert.Equal(100 * commanderRate, commanderBudget);

        // Spend OVER the commander budget, while leaving demonType entirely unspent (a huge surplus
        // there, if scopes could cover each other).
        var overspentCommander = AptitudeAllocation.Single(AllocationScope.Commander, "Might", commanderBudget + 1);

        var check = PointBudget.CheckScope(AllocationScope.Commander, overspentCommander, sourceValue: 100, tuning);

        Assert.False(check.WithinBudget);
        Assert.Equal(commanderBudget + 1, check.Spent);
        Assert.Equal(commanderBudget, check.Budget);
        // The unspent demonType scope has no bearing on this check at all -- CheckScope never reads
        // any scope but the one it was asked about, so there is no code path for a surplus elsewhere
        // to "cover" this shortfall.
    }

    [Fact]
    public void Rates_are_ordered_commander_smallest_unique_largest()
    {
        // ⛔ RENAMED (species-build T0.3, audit A1) from "Commander_budget_is_smallest_...". Holding
        // the source constant on purpose ONLY proves the RATE table's own ordering (3 < 4 <= 4 < 6) --
        // it does NOT prove the BUDGET ordering the old name claimed, because the four scopes' real
        // sources are in different UNITS (an index vs an accumulation). That gap is exactly what let
        // the DemonType-source defect (almanac XP, a 176x inversion at species L12) ship undetected --
        // this test kept passing straight through it. See Real_budgets_are_ordered_at_representative_sources
        // below for the test that actually proves the budget claim.
        var tuning = ShippedTuning();
        const long sameSourceValue = 100; // isolates the RATE ordering from any per-scope source difference.

        var commander = PointBudget.PointsFor(AllocationScope.Commander, sameSourceValue, tuning);
        var demonType = PointBudget.PointsFor(AllocationScope.DemonType, sameSourceValue, tuning);
        var aspect = PointBudget.PointsFor(AllocationScope.Aspect, sameSourceValue, tuning);
        var uniqueDemon = PointBudget.PointsFor(AllocationScope.UniqueDemon, sameSourceValue, tuning);

        Assert.True(commander < demonType, $"commander ({commander}) must be < demonType ({demonType})");
        Assert.True(demonType <= aspect, $"demonType ({demonType}) must be <= aspect ({aspect})");
        Assert.True(aspect < uniqueDemon, $"aspect ({aspect}) must be < uniqueDemon ({uniqueDemon})");
    }

    [Fact]
    public void Real_budgets_are_ordered_at_representative_sources()
    {
        // species-build T0.3, audit A1 -- the test the old (renamed) one above could not be, because
        // it held the source constant. Each scope is fed a REPRESENTATIVE VALUE IN ITS OWN UNITS,
        // drawn from this repo's own already-recorded ordinary-play numbers, and the ordering is
        // asserted on the resulting BUDGETS -- the claim the class actually needs to hold.
        //
        // Sources, all picked at the SAME representative "mid-game milestone" magnitude -- the whole
        // point of species-build's audit A1 fix is that species level is now an INDEX comparable in
        // scale to Theta_player and specimen level, unlike the old "almanac XP" accumulation (2,640 at
        // species L12) that was never comparable to anything:
        //   commander:   Theta_player = 20        (ssot-power-scale.md's own pin, "P(20) = 680")
        //   demonType:   species level 21 -> DemonTypeSourceFromLevel(21) = 20
        //   uniqueDemon: specimen level 20         (rpg-progression.md's own balance note:
        //                                            "L12-20 after 20 matches" -- the range's own top)
        //
        // Aspect is deliberately EXCLUDED. Its real source, `element_mastery`, does not exist yet --
        // it is owned by the demon program's `aspect-scope` module, itself reverted and not authorized
        // to build (decisions.md, "Demon program" row). Inventing a value for it here would decide the
        // very ordering this test exists to prove, which is the same "fabricated source" defect this
        // module exists to fix -- so this test asserts commander < demonType < uniqueDemon over real
        // sources only, and leaves Aspect's own ordering proof to whoever builds that tier for real.
        var tuning = ShippedTuning();

        const long thetaPlayer = 20;
        const long speciesLevel = 21;
        const long specimenLevel = 20;

        var commanderBudget = PointBudget.PointsFor(AllocationScope.Commander, thetaPlayer, tuning);
        var demonTypeBudget = PointBudget.PointsFor(
            AllocationScope.DemonType, PointBudget.DemonTypeSourceFromLevel(speciesLevel), tuning);
        var uniqueDemonBudget = PointBudget.PointsFor(AllocationScope.UniqueDemon, specimenLevel, tuning);

        Assert.True(commanderBudget < demonTypeBudget,
            $"commander ({commanderBudget}) must be < demonType ({demonTypeBudget}) at real sources");
        Assert.True(demonTypeBudget < uniqueDemonBudget,
            $"demonType ({demonTypeBudget}) must be < uniqueDemon ({uniqueDemonBudget}) at real sources");
    }

    [Fact]
    public void DemonTypeSourceFromLevel_isZero_atLevelZeroAndLevelOne()
    {
        // species-build T0.4 -- an unrecorded actor's progression defaults to Level = 1
        // (RpgStore.Progression.cs's own DefaultPlayerDtoUnlocked), so a never-levelled species must
        // carry EXACTLY ZERO points or every battle/expedition golden would move the moment
        // `demon-type-allocation`'s compose-at-read baseline lands.
        Assert.Equal(0, PointBudget.DemonTypeSourceFromLevel(0));
        Assert.Equal(0, PointBudget.DemonTypeSourceFromLevel(1));
        Assert.Equal(1, PointBudget.DemonTypeSourceFromLevel(2));
        Assert.Equal(11, PointBudget.DemonTypeSourceFromLevel(12));
    }

    [Fact]
    public void PointsFor_demonType_atLevelZeroOrOne_isZeroBudget()
    {
        // The composed proof: PointsFor(DemonType, DemonTypeSourceFromLevel(level)) is zero for a
        // never-levelled species, at any real DemonType rate.
        var tuning = ShippedTuning();
        Assert.Equal(0, PointBudget.PointsFor(AllocationScope.DemonType, PointBudget.DemonTypeSourceFromLevel(0), tuning));
        Assert.Equal(0, PointBudget.PointsFor(AllocationScope.DemonType, PointBudget.DemonTypeSourceFromLevel(1), tuning));
    }

    [Fact]
    public void No_cap_on_an_aptitude()
    {
        // PS-8 (AGENTS.md, CLAUDE.md): no hard progression ceiling. A budget an actor earns more of is
        // not a cap (spec-point-economy.md §2.2) -- an enormous source value must produce a
        // proportionally enormous budget, never clamp.
        var tuning = ShippedTuning();
        const long enormousSourceValue = 10_000_000_000; // far past any Theta a real player reaches today.

        var budget = PointBudget.PointsFor(AllocationScope.UniqueDemon, enormousSourceValue, tuning);
        var rate = tuning.PointEconomy.AptitudePointsPerThetaMilliByScope[AllocationScope.UniqueDemon];

        Assert.Equal(enormousSourceValue * rate, budget); // exact, not clamped to any ceiling.

        // The spend side is equally uncapped -- AptitudeAllocation.Single accepts the same enormous
        // figure without throwing (Phase 1's own PS-8 guarantee; confirmed here from point-economy's
        // own testing table, not re-derived from AptitudeAllocation's own tests alone).
        var allocation = AptitudeAllocation.Single(AllocationScope.UniqueDemon, "Might", budget);
        Assert.Equal(budget, allocation.Total("Might"));
    }

    [Fact]
    public void Budget_is_exact_at_high_theta()
    {
        // spec-point-economy.md §6's own worked example: "int overflows near Theta = 715,000,000 at 3
        // points/Theta" -- reachable, not hypothetical, once Theta is genuinely uncapped (PS-8). Picks
        // a source value comfortably past that threshold and checks the result is the EXACT product,
        // not an int-truncated or wrapped one.
        var tuning = ShippedTuning();
        const long farPastIntOverflow = 1_000_000_000; // int.MaxValue is ~2.147B; 3x this alone exceeds it.
        var rate = tuning.PointEconomy.AptitudePointsPerThetaMilliByScope[AllocationScope.Commander];

        var budget = PointBudget.PointsFor(AllocationScope.Commander, farPastIntOverflow, tuning);

        // Computed independently in decimal (not through the long multiplication under test) so a
        // silently-wrapped int result would disagree with this expectation, not coincidentally match it.
        var expected = (long)((decimal)farPastIntOverflow * rate);
        Assert.Equal(expected, budget);
        Assert.True(budget > int.MaxValue, $"expected the exact product to exceed int.MaxValue, got {budget}");
    }

    [Fact]
    public void PointsFor_nullTuning_throws()
    {
        Assert.Throws<ArgumentNullException>(() => PointBudget.PointsFor(AllocationScope.Commander, 100, null!));
    }

    [Fact]
    public void PointsFor_negativeSourceValue_throws()
    {
        var tuning = ShippedTuning();
        Assert.Throws<ArgumentOutOfRangeException>(() => PointBudget.PointsFor(AllocationScope.Commander, -1, tuning));
    }

    [Fact]
    public void PointsFor_zeroSourceValue_isZeroBudget_notRejected()
    {
        // A fresh actor with no progression yet (Theta_player=0, a brand-new demon type, etc.) is
        // ordinary, not an error -- only a NEGATIVE source value is a validation failure.
        var tuning = ShippedTuning();
        Assert.Equal(0, PointBudget.PointsFor(AllocationScope.Commander, 0, tuning));
    }

    [Fact]
    public void CheckScope_nullAllocation_throws()
    {
        var tuning = ShippedTuning();
        Assert.Throws<ArgumentNullException>(() => PointBudget.CheckScope(AllocationScope.Commander, null!, 100, tuning));
    }
}
