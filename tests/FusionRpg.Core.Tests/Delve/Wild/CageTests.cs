using FusionRpg.Core.Delve.Wild;
using FusionRpg.Core.Dungeon.Registry;
using FusionRpg.Core.Tests.Dungeon;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Wild;

/// <summary>D4.8 (spec-wild-room.md §7) — `Cage`: the structural draw, occupant eligibility, the
/// one-band-toward-eager disposition shift, and the fight/threaten-less talk tree.</summary>
public class CageTests
{
    static CageTests()
    {
        var json = File.ReadAllText(Path.Combine(DungeonTestFiles.RegistryDir(), "disposition.v1.json"));
        DispositionCatalog.Configure(DispositionCatalog.Parse(json));
    }

    static readonly WildTalkEligibility AllEligible = new(
        CaptureOnly: false, NoFreeSlot: false, DeltaBandIsFarAbove: false,
        UnbankedMeetsSoulsFloor: true, SpiritPoolMeetsFloor: true,
        HoldsOfferableSupply: true, HasReleasableContractAboveFloor: true);

    // ---- IsCageRoom ----

    [Fact]
    public void A_roll_under_the_threshold_is_a_cage_room()
    {
        Assert.True(Cage.IsCageRoom(rolledMilli: 149, cageMilli: 150));
    }

    [Fact]
    public void A_roll_exactly_at_the_threshold_is_not_a_cage_room()
    {
        Assert.False(Cage.IsCageRoom(rolledMilli: 150, cageMilli: 150));
    }

    // ---- OccupantEligible ----

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void OccupantEligible_never_a_capture_only_species_never_the_top_rung(bool captureOnly, bool isTopRung, bool expected)
    {
        Assert.Equal(expected, Cage.OccupantEligible(captureOnly, isTopRung));
    }

    // ---- OccupantDispositionBase ----

    [Fact]
    public void A_hostile_occupant_shifts_one_band_toward_eager_to_wary()
    {
        Assert.Equal("wary", Cage.OccupantDispositionBase("hostile"));
    }

    [Fact]
    public void An_eager_occupant_stays_eager_the_rail_clamps_it_does_not_go_negative()
    {
        Assert.Equal("eager", Cage.OccupantDispositionBase("eager"));
    }

    // ---- Offered: section 2's tree without fight and threaten ----

    [Fact]
    public void Offered_never_contains_fight_or_threaten_at_step_1()
    {
        var offered = Cage.Offered(1, maxSteps: 2, AllEligible);
        Assert.DoesNotContain(WildVerb.Fight, offered);
        Assert.DoesNotContain(WildVerb.Threaten, offered);
    }

    [Fact]
    public void Offered_still_carries_flatter_and_every_eligible_offer_and_leave()
    {
        var offered = Cage.Offered(1, maxSteps: 2, AllEligible);
        Assert.Contains(WildVerb.Flatter, offered);
        Assert.Contains(WildVerb.OfferSouls, offered);
        Assert.Contains(WildVerb.OfferSpirit, offered);
        Assert.Contains(WildVerb.OfferSupply, offered);
        Assert.Contains(WildVerb.OfferContract, offered);
        Assert.Contains(WildVerb.Leave, offered);
    }

    [Fact]
    public void Offered_never_contains_fight_even_when_TalkTree_would_have_offered_it_at_step_2()
    {
        var offered = Cage.Offered(2, maxSteps: 2, AllEligible);
        Assert.DoesNotContain(WildVerb.Fight, offered);
        Assert.Contains(WildVerb.Leave, offered);
    }
}
