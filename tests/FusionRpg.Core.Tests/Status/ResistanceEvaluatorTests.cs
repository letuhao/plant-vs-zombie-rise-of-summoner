using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Status;

public class ResistanceEvaluatorTests
{
    static readonly ResistanceEvaluator Eval = new();

    static StatusApplyRequest Req(string statusId = "wither") => new(
        statusId,
        HostPtr: "Z1",
        AttackerPtr: "P1",
        BaseMagnitude: 20,
        BaseDuration: 5000);

    [Fact]
    public void Neutral_stub_tier_power_contributes_to_delta()
    {
        // T3.1 (power-plan.md, ResistFromPowerRatio 0->1.0): this test used to assert delta==1.0 for
        // two IDENTICAL actors -- it encoded the bug (attacker's tier power counted, defender's did
        // not, so an even match never contested at zero). Now asserts 0.0, the correct value.
        var attacker = ActorDerivedSnapshot.StubNeutral();
        var defender = ActorDerivedSnapshot.StubNeutral();
        var delta = ResistanceEvaluator.ComputeDelta("wither", StatusL2bCategory.Dot, attacker, defender);
        Assert.Equal(0.0, delta, 3);
        Assert.Equal(1.0, ResistanceEvaluator.ComputeNetFactor(delta));
    }

    // T3.1's red test (spec-status-contest.md §5): matched pair at every Theta must contest at
    // delta=0. Originally written and proven "even under the still-un-retired ProgressionPowerCurve"
    // (T3.1 landed before T3.2); updated here for T3.2 ("retire the curve") to construct
    // ProgressionPower = Theta directly, matching the new progression.power = Theta rule -- the
    // property this test proves (matched pair -> delta=0) is unaffected by which wave built it.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(50)]
    [InlineData(1000)]
    public void MatchedPair_ContestsAtDeltaZero_AtEveryTheta(int theta)
    {
        var matched = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, theta),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0)
        });

        var delta = ResistanceEvaluator.ComputeDelta("wither", StatusL2bCategory.Dot, matched, matched);
        Assert.Equal(0.0, delta, 3);
        Assert.Equal(1.0, ResistanceEvaluator.ComputeNetFactor(delta));
    }

    [Fact]
    public void Delta_IsAntisymmetric()
    {
        // T3.2: Theta directly (12 vs 10), not curve-transformed -- see the note above.
        var stronger = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 12.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0)
        });
        var weaker = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 10.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0)
        });

        var ahead = ResistanceEvaluator.ComputeDelta("wither", StatusL2bCategory.Dot, stronger, weaker);
        var behind = ResistanceEvaluator.ComputeDelta("wither", StatusL2bCategory.Dot, weaker, stronger);
        Assert.Equal(ahead, -behind, 3);
    }

    // T3.2's own red test (spec-status-contest.md S5, power-todo.md T3.2): matched pair at Theta=12
    // must flip from the OLD curve's netFactor=4096 to the NEW linear formula's netFactor=1.0. T3.1
    // already fixed the delta=0 half (ResistFromPowerRatio); this proves the SECOND half --
    // ComputeNetFactor's own formula no longer produces a curve-shaped blowout for ANY delta, not
    // just for the specific matched-pair case T3.1 covers.
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-2)]
    [InlineData(25)]
    public void NetFactor_IsLinearInDelta_NotACliff(double delta)
    {
        var expected = 1.0 + delta / StatusPolicy.NetFactorScale;
        Assert.Equal(Math.Clamp(expected, StatusPolicy.MinNetFactor, StatusPolicy.MaxNetFactor),
            ResistanceEvaluator.ComputeNetFactor(delta), 6);
    }

    [Fact]
    public void RedTest_MatchedPairAtTheta12_NetFactorFlips4096To1()
    {
        // The exact SSOT S6.0 scenario, both fixes landed: was netFactor=4096 (T3.1: delta=0 fixes
        // this already, independent of T3.2) -- this test's OWN point is that ComputeNetFactor(0)
        // is 1.0 via the general linear formula now, with NO delta==0 special case in the source
        // (asserted directly below, not just by the numeric outcome, which a reintroduced special
        // case would also satisfy).
        Assert.Equal(1.0, ResistanceEvaluator.ComputeNetFactor(0.0));

        var source = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRootForThisFile(), "src", "FusionRpg.Core", "Status", "ResistanceEvaluator.cs"));
        var netFactorBody = System.Text.RegularExpressions.Regex.Match(source, @"ComputeNetFactor\(double delta\)([\s\S]*?)\n    \}").Groups[1].Value;
        Assert.DoesNotContain("1e-9", netFactorBody);
        Assert.DoesNotContain("Abs(delta)", netFactorBody);
    }

    static string RepoRootForThisFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "data", "tuning"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new System.IO.DirectoryNotFoundException("data/tuning");
    }

    [Fact]
    public void Neutral_stub_p_apply_near_half()
    {
        var result = Eval.Evaluate(
            Req(),
            ActorDerivedSnapshot.StubNeutral(),
            ActorDerivedSnapshot.StubNeutral(),
            new FixedStatusRng(0.0));
        Assert.True(result.Applied);
        Assert.InRange(result.PApply, 0.49, 0.51);
        Assert.Equal(1.0, result.NetFactor);
    }

    [Fact]
    public void Delta_negative_ten_potency_floor_skips_roll()
    {
        // T3.2: the floor is no longer "any negative delta" (the old Clamp(delta,0,Max) cliff) -- it
        // is delta <= -NetFactorScale (-10). StatusResistOmni raised 5.0 -> 10.0 so this test's own
        // setup actually produces delta=-10 (matching its name) under the new linear formula:
        // totalPower=1 (attacker StubNeutral), totalResist=1*1.0(ResistFromPowerRatio)+10=11, delta=-10.
        var attacker = ActorDerivedSnapshot.StubNeutral();
        var defender = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusResistOmni, 10.0)
        });
        var result = Eval.Evaluate(Req(), attacker, defender, new FixedStatusRng(0.0));
        Assert.False(result.Applied);
        Assert.Equal(StatusResistReason.PotencyFloor, result.ResistReason);
        Assert.Equal(0, result.PApply);
    }

    [Fact]
    public void Omni_resist_1M_vs_power_100_potency_floor()
    {
        var attacker = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPower(statusId: "rot"), 100)
        });
        var defender = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusResistOmni, 1_000_000)
        });
        var result = Eval.Evaluate(Req("rot"), attacker, defender, new FixedStatusRng(0.0));
        Assert.False(result.Applied);
        Assert.Equal(StatusResistReason.PotencyFloor, result.ResistReason);
    }

    [Theory]
    [InlineData(-1500, 0.01)]
    [InlineData(0, 0.50)]
    [InlineData(50, 0.62)]
    [InlineData(1500, 0.99)]
    public void Golden_apply_chance_table(double delta, double expectedApprox)
    {
        var scale = 100.0;
        var p = ResistanceEvaluator.Sigmoid(delta / scale);
        Assert.InRange(p, expectedApprox - 0.02, expectedApprox + 0.02);
    }

    // T3.2 (audit F4): netFactor = 1 + delta/NetFactorScale, not a raw clamp(delta). -10 and 0 are
    // unchanged coincidentally (both formulas floor at exactly 0, and agree at exactly 1.0, for
    // these two inputs) -- 50 moves from the old raw-delta value (50, a 50x multiplier -- the exact
    // cliff audit F4 named) to 1+50/10=6.0, still a large but no longer absurd swing.
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 1.0)]
    [InlineData(50, 6.0)]
    public void Golden_potency_table(double delta, double expectedNet)
    {
        Assert.Equal(expectedNet, ResistanceEvaluator.ComputeNetFactor(delta));
    }

    [Fact]
    public void Complete_immunity_blocks_before_roll()
    {
        var defender = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.StatusImmune("poison"), 1.0)
        });
        var result = Eval.Evaluate(
            Req("poison") with { ImmunityTags = new[] { "poison" } },
            ActorDerivedSnapshot.StubNeutral(),
            defender,
            new FixedStatusRng(0.0));
        Assert.False(result.Applied);
        Assert.Equal(StatusResistReason.Immunity, result.ResistReason);
    }

    [Fact]
    public void Attacker_less_uses_zero_power()
    {
        // T3.1: genuinely unchanged, but not for the reason the spec originally claimed, and not by
        // accident either. First pass: assumed unchanged. Second pass (direct computation): found
        // AttackerLess() zeroes only the ATTACKER's channels, so a normal StubNeutral() defender's
        // own tier power now counts as resist same as any real actor -- delta = 0 - 1*1.0 = -1.
        // Third pass (running BattleStatusTests): -1 sends every scripted DoT/CC to netFactor's
        // MinNetFactor floor (0.0) -- Dot_kills_through_rounds went Victory -> Stalemate. That is a
        // real defect, not "expected golden movement" -- an attacker-less application has no real
        // attacker side to contest tier power WITH, so the contest is symmetric-exclude, not
        // one-sided. ComputeDelta's attackerLess parameter (added here) restores 0.0, correctly this
        // time: proven by the full battle suite passing again, not just this unit re-passing.
        var attackerLess = ActorDerivedSnapshot.AttackerLess();
        var defender = ActorDerivedSnapshot.StubNeutral();
        var delta = ResistanceEvaluator.ComputeDelta("blight", StatusL2bCategory.Contagion, attackerLess, defender, attackerLess: true);
        Assert.Equal(0.0, delta, 3);
    }

    [Fact]
    public void Grant_chance_combines_with_p_apply()
    {
        var result = Eval.Evaluate(
            Req() with { GrantChance = 0.5 },
            ActorDerivedSnapshot.StubNeutral(),
            ActorDerivedSnapshot.StubNeutral(),
            new FixedStatusRng(0.0));
        Assert.True(result.Applied);
        Assert.InRange(result.PFinal, 0.24, 0.26);
    }

    // T3.2 ("retire the curve"): Progression_power_curve_feeds_delta deleted here, not updated --
    // it tested that ProgressionPowerCurve.PowerFromLevel's OUTPUT correctly fed ComputeDelta, and
    // the curve it exercised no longer exists (ProgressionPowerCurve.cs deleted the same commit).
    // Its coverage intent -- "a power value correctly reaches delta" -- is already proven by
    // MatchedPair_ContestsAtDeltaZero_AtEveryTheta and Delta_IsAntisymmetric below, now exercising
    // progression.power = Theta directly.
}

public class StatusCategoryRegistryTests
{
    [Theory]
    [InlineData("wither", StatusL2bCategory.Dot)]
    [InlineData("butter", StatusL2bCategory.Cc)]
    [InlineData("blight", StatusL2bCategory.Contagion)]
    public void Known_ids_map_to_category(string statusId, string category)
    {
        Assert.Equal(category, StatusCategoryRegistry.GetRequiredCategory(statusId));
    }

    [Fact]
    public void All_twenty_one_ids_registered()
    {
        Assert.Equal(21, StatusCategoryRegistry.AllStatusIds.Count);
    }
}

public class StatusCatalogTests
{
    [Fact]
    public void Bootstrap_registers_21_ids()
    {
        var catalog = StatusCatalogBootstrap.CreateDefault();
        Assert.Equal(21, catalog.All().Count);
    }

    [Fact]
    public void Unknown_statusId_rejects()
    {
        var catalog = StatusCatalogBootstrap.CreateDefault();
        Assert.Throws<UnknownStatusIdException>(() => catalog.GetRequired("not_a_status"));
    }

    [Fact]
    public void Elemental_family_mutex_defs_exist()
    {
        var catalog = StatusCatalogBootstrap.CreateDefault();
        Assert.Equal("elemental", catalog.GetRequired("freeze").Family);
        Assert.Equal(StatusStacking.Replace, catalog.GetRequired("freeze").Stacking);
    }
}
