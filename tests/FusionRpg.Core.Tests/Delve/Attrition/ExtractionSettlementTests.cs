using FusionRpg.Core.Battle;
using FusionRpg.Core.Delve.Attrition;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Attrition;

/// <summary>D2.21 (spec-delve-attrition.md §8) — `ExtractionSettlement.PartyStands`/`.IsWiped`: "a
/// party stands while one member has hp &gt; 0 and is not downed... every party of the raid has no
/// standing member" (verbatim).</summary>
public class ExtractionSettlementTests
{
    static DelveMemberState Member(long hp, bool downed = false) => new(
        InstanceId: Guid.NewGuid().ToString(),
        Pools: new Dictionary<string, long> { ["hp"] = hp, ["stamina"] = 0, ["hunger"] = 0, ["spirit"] = 0, ["qi"] = 0, ["poise"] = 0 },
        Statuses: Array.Empty<BattleStatusSpec>(), Shield: null, NerveStacks: 0, Downed: downed, DownedOnce: false);

    // ---- PartyStands ----

    [Fact]
    public void A_party_with_one_live_undowned_member_stands()
    {
        var party = new[] { Member(hp: 0, downed: true), Member(hp: 500) };
        Assert.True(ExtractionSettlement.PartyStands(party));
    }

    [Fact]
    public void A_party_where_every_member_is_at_zero_hp_does_not_stand()
    {
        var party = new[] { Member(hp: 0), Member(hp: 0) };
        Assert.False(ExtractionSettlement.PartyStands(party));
    }

    [Fact]
    public void Positive_hp_alone_is_not_enough_downed_still_does_not_stand()
    {
        // The spec's own two-part rule: hp > 0 AND not downed. A member revived to positive hp but
        // whose `Downed` flag has not yet cleared must not count as standing.
        var party = new[] { Member(hp: 500, downed: true) };
        Assert.False(ExtractionSettlement.PartyStands(party));
    }

    [Fact]
    public void An_empty_party_does_not_stand()
    {
        Assert.False(ExtractionSettlement.PartyStands(Array.Empty<DelveMemberState>()));
    }

    [Fact]
    public void PartyStands_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExtractionSettlement.PartyStands(null!));
    }

    [Fact]
    public void PartyStands_a_member_missing_the_hp_pool_throws_by_name()
    {
        var badMember = Member(hp: 500) with { Pools = new Dictionary<string, long> { ["stamina"] = 0 } };
        var ex = Assert.Throws<ArgumentException>(() => ExtractionSettlement.PartyStands(new[] { badMember }));
        Assert.Contains(badMember.InstanceId, ex.Message);
    }

    // ---- IsWiped ----

    [Fact]
    public void A_raid_is_wiped_when_every_party_has_no_standing_member()
    {
        var parties = new IReadOnlyList<DelveMemberState>[]
        {
            new[] { Member(hp: 0), Member(hp: 0, downed: true) },
            new[] { Member(hp: 0) },
        };
        Assert.True(ExtractionSettlement.IsWiped(parties));
    }

    [Fact]
    public void A_raid_is_not_wiped_while_even_one_party_still_stands()
    {
        var parties = new IReadOnlyList<DelveMemberState>[]
        {
            new[] { Member(hp: 0), Member(hp: 0, downed: true) }, // this party is wiped...
            new[] { Member(hp: 500) },                            // ...but this one still stands
        };
        Assert.False(ExtractionSettlement.IsWiped(parties));
    }

    [Fact]
    public void A_single_solo_party_wipes_on_its_own_state_alone()
    {
        Assert.True(ExtractionSettlement.IsWiped(new IReadOnlyList<DelveMemberState>[] { new[] { Member(hp: 0) } }));
        Assert.False(ExtractionSettlement.IsWiped(new IReadOnlyList<DelveMemberState>[] { new[] { Member(hp: 500) } }));
    }

    [Fact]
    public void IsWiped_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExtractionSettlement.IsWiped(null!));
    }

    [Fact]
    public void IsWiped_zero_parties_throws_rather_than_guessing()
    {
        Assert.Throws<ArgumentException>(() => ExtractionSettlement.IsWiped(Array.Empty<IReadOnlyList<DelveMemberState>>()));
    }
}

/// <summary>D2.22 (spec-delve-attrition.md §7, §9) — `ExtractionSettlement.Decide`: per-member
/// Retire/Recover(n)/Roster, and `won`, decided together and pure.</summary>
public class ExtractionSettlementDecideTests
{
    const int RecoveryDelves = 2; // risk.downedRecoveryDelves's own real starting shape

    // ---- Outcome: the §7 truth table over downedOnce x permadeathApplies ----

    [Fact]
    public void Never_downed_is_always_Roster_with_zero_recovery_regardless_of_the_rung()
    {
        var notPermadeath = ExtractionSettlement.Decide(downedOnce: false, permadeathApplies: false, downedRecoveryDelves: RecoveryDelves, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: false);
        var permadeath = ExtractionSettlement.Decide(downedOnce: false, permadeathApplies: true, downedRecoveryDelves: RecoveryDelves, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: false);

        Assert.Equal(SettlementOutcome.Roster, notPermadeath.Outcome);
        Assert.Equal(0, notPermadeath.RecoverDelves);
        Assert.Equal(SettlementOutcome.Roster, permadeath.Outcome);
        Assert.Equal(0, permadeath.RecoverDelves);
    }

    [Fact]
    public void DownedOnce_on_a_permadeath_rung_Retires_with_zero_recovery()
    {
        var result = ExtractionSettlement.Decide(downedOnce: true, permadeathApplies: true, downedRecoveryDelves: RecoveryDelves, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: false);

        Assert.Equal(SettlementOutcome.Retire, result.Outcome);
        Assert.Equal(0, result.RecoverDelves);
    }

    [Fact]
    public void DownedOnce_below_the_permadeath_gate_Recovers_for_the_tunable_count()
    {
        var result = ExtractionSettlement.Decide(downedOnce: true, permadeathApplies: false, downedRecoveryDelves: RecoveryDelves, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: false);

        Assert.Equal(SettlementOutcome.Recover, result.Outcome);
        Assert.Equal(RecoveryDelves, result.RecoverDelves);
    }

    [Fact]
    public void RevivedThenExtractedOnAPermadeathRungIsStillRetired()
    {
        // R3, verbatim: "the revive lets it finish the run, not escape the rule." Modeled here as
        // downedOnce=true regardless of the member's CURRENT (post-revive) hp/downed state -- Decide
        // never even takes a current-hp parameter, so there is no way for a revive to hide from it.
        var result = ExtractionSettlement.Decide(downedOnce: true, permadeathApplies: true, downedRecoveryDelves: RecoveryDelves, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: true);
        Assert.Equal(SettlementOutcome.Retire, result.Outcome);
    }

    // ---- Won: the §9 truth table, independent of Outcome ----

    [Theory]
    [InlineData(true, true, false, false, true)]   // extracted + boss killed + not afflicted -> won
    [InlineData(true, false, true, false, true)]   // extracted + half route + not afflicted -> won
    [InlineData(true, true, true, false, true)]    // both win conditions -> still won
    [InlineData(false, true, true, false, false)]  // not extracted (a wipe) -> never won, whatever else
    [InlineData(true, false, false, false, false)] // extracted but neither win condition -> not won
    [InlineData(true, true, false, true, false)]   // extracted + boss killed but afflicted -> not won
    [InlineData(true, false, true, true, false)]   // extracted + half route but afflicted -> not won
    public void Won_matches_the_spec_truth_table(bool extracted, bool bossKilled, bool routeHalf, bool afflicted, bool expectedWon)
    {
        var result = ExtractionSettlement.Decide(downedOnce: false, permadeathApplies: false, downedRecoveryDelves: RecoveryDelves, afflicted: afflicted, extracted: extracted, bossKilled: bossKilled, routeAtLeastHalfCleared: routeHalf);
        Assert.Equal(expectedWon, result.Won);
    }

    [Fact]
    public void Won_is_independent_of_downedOnce_and_the_permadeath_gate()
    {
        // A member can be Retired/Recovering and still have "won" the delve for loyalty purposes --
        // these are two separate axes, never coupled.
        var retiredButWon = ExtractionSettlement.Decide(downedOnce: true, permadeathApplies: true, downedRecoveryDelves: RecoveryDelves, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: false);
        Assert.True(retiredButWon.Won);
    }

    // ---- validation ----

    [Fact]
    public void A_nonPositive_recovery_count_throws_only_when_it_would_actually_be_used()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExtractionSettlement.Decide(downedOnce: true, permadeathApplies: false, downedRecoveryDelves: 0, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: false));

        // Roster and Retire never read the count -- a caller that has not resolved the tunable for a
        // member who will not Recover must not be punished for it.
        var rosterOutcome = ExtractionSettlement.Decide(downedOnce: false, permadeathApplies: false, downedRecoveryDelves: 0, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: false);
        var retireOutcome = ExtractionSettlement.Decide(downedOnce: true, permadeathApplies: true, downedRecoveryDelves: 0, afflicted: false, extracted: true, bossKilled: true, routeAtLeastHalfCleared: false);
        Assert.Equal(SettlementOutcome.Roster, rosterOutcome.Outcome);
        Assert.Equal(SettlementOutcome.Retire, retireOutcome.Outcome);
    }

    // ---- the verify line's own headline: a loyalty double-apply test ----

    [Fact]
    public void Deciding_the_same_inputs_twice_never_produces_a_different_answer()
    {
        // Decide is pure: a caller that (by accident, a retry, or a race) invokes it twice for the
        // same member gets the IDENTICAL settlement both times -- so "loyalty is applied once" can
        // never be undermined by Decide itself returning a different `Won` on a second call. The
        // actual once-per-delve ENFORCEMENT is the store call site's own job (D2.23); this is the
        // half of that guarantee this pure function can make on its own.
        var first = ExtractionSettlement.Decide(downedOnce: true, permadeathApplies: false, downedRecoveryDelves: RecoveryDelves, afflicted: true, extracted: true, bossKilled: true, routeAtLeastHalfCleared: true);
        var second = ExtractionSettlement.Decide(downedOnce: true, permadeathApplies: false, downedRecoveryDelves: RecoveryDelves, afflicted: true, extracted: true, bossKilled: true, routeAtLeastHalfCleared: true);

        Assert.Equal(first, second); // MemberSettlement is a record -- structural equality
    }
}
