using FusionRpg.Core.Delve.Events;
using FusionRpg.Core.Dungeon.Tuning;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Events;

/// <summary>D3.5 (spec-event-deck.md §5): the severity band-shift and the weighted `:outcome` draw.
/// `dropBandOrder`/`weightTable` mirror the real, shipped item-registry content
/// (`data/seed/items/_registry/bands.v1.json:453-459,463-479`: `staple·frequent·occasional·seldom·
/// exceptional`, weights `1000/300/90/25/7`) as LOCAL test fixtures — this module never reads that
/// registry itself (D3.1's own citation: "this module has no business owning" the item registry), so
/// every real caller supplies its own copy the same way these tests do.</summary>
public class OutcomeResolverTests
{
    static readonly IReadOnlyList<string> Order = new[] { "staple", "frequent", "occasional", "seldom", "exceptional" };
    static readonly IReadOnlyDictionary<string, int> Weights = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["staple"] = 1000, ["frequent"] = 300, ["occasional"] = 90, ["seldom"] = 25, ["exceptional"] = 7,
    };

    static readonly DungeonTuning Tuning = DungeonTuningHub.Tuning;
    static readonly DifficultyRungTuning Hard = Tuning.Rungs["hard"]; // real shipped: EventSeverityTier == 2

    static EventOutcomeRow Outcome(string ordinal, string dropBand) => new(ordinal, dropBand, "none", Array.Empty<EventEffectRef>());

    // ---- ShiftDropBand: argument validation ----

    [Fact]
    public void ShiftDropBand_null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => OutcomeResolver.ShiftDropBand(null!, "good", 1, Order));
        Assert.Throws<ArgumentNullException>(() => OutcomeResolver.ShiftDropBand("staple", null!, 1, Order));
        Assert.Throws<ArgumentNullException>(() => OutcomeResolver.ShiftDropBand("staple", "good", 1, null!));
    }

    [Fact]
    public void ShiftDropBand_empty_order_throws()
    {
        Assert.Throws<ArgumentException>(() => OutcomeResolver.ShiftDropBand("staple", "good", 1, Array.Empty<string>()));
    }

    [Fact]
    public void ShiftDropBand_negative_severityTier_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OutcomeResolver.ShiftDropBand("staple", "good", -1, Order));
    }

    [Fact]
    public void ShiftDropBand_unknown_dropBand_throws()
    {
        Assert.Throws<ArgumentException>(() => OutcomeResolver.ShiftDropBand("mythic", "good", 1, Order));
    }

    // ---- ShiftDropBand: the shift itself ----

    [Fact]
    public void Severity_zero_never_shifts_any_ordinal()
    {
        Assert.Equal("staple", OutcomeResolver.ShiftDropBand("staple", "good", 0, Order));
        Assert.Equal("exceptional", OutcomeResolver.ShiftDropBand("exceptional", "bad", 0, Order));
        Assert.Equal("occasional", OutcomeResolver.ShiftDropBand("occasional", "mixed", 0, Order));
    }

    [Fact]
    public void Good_shifts_toward_exceptional_by_exactly_the_tier_step_count()
    {
        Assert.Equal("frequent", OutcomeResolver.ShiftDropBand("staple", "good", 1, Order));
        Assert.Equal("occasional", OutcomeResolver.ShiftDropBand("staple", "good", 2, Order));
        Assert.Equal("seldom", OutcomeResolver.ShiftDropBand("frequent", "good", 2, Order));
    }

    [Fact]
    public void Good_clamps_at_exceptional_never_overflows_past_the_list()
    {
        Assert.Equal("exceptional", OutcomeResolver.ShiftDropBand("seldom", "good", 10, Order));
        Assert.Equal("exceptional", OutcomeResolver.ShiftDropBand("exceptional", "good", 3, Order));
    }

    [Fact]
    public void Bad_shifts_toward_staple_by_exactly_the_tier_step_count()
    {
        Assert.Equal("seldom", OutcomeResolver.ShiftDropBand("exceptional", "bad", 1, Order));
        Assert.Equal("occasional", OutcomeResolver.ShiftDropBand("exceptional", "bad", 2, Order));
        Assert.Equal("frequent", OutcomeResolver.ShiftDropBand("seldom", "bad", 2, Order));
    }

    [Fact]
    public void Bad_clamps_at_staple_never_underflows_before_the_list()
    {
        Assert.Equal("staple", OutcomeResolver.ShiftDropBand("occasional", "bad", 10, Order));
        Assert.Equal("staple", OutcomeResolver.ShiftDropBand("staple", "bad", 3, Order));
    }

    [Fact]
    public void Mixed_and_nothing_never_shift_regardless_of_tier()
    {
        Assert.Equal("frequent", OutcomeResolver.ShiftDropBand("frequent", "mixed", 4, Order));
        Assert.Equal("frequent", OutcomeResolver.ShiftDropBand("frequent", "nothing", 4, Order));
    }

    [Fact]
    public void Sanity_golden_against_the_real_shipped_hard_rungs_own_severity_tier()
    {
        // "hard" (rung 4, the identity row) carries EventSeverityTier == 2 in the real shipped tuning --
        // NOT 0, confirmed by reading data/tuning/dungeon.v1.json directly before writing this test.
        Assert.Equal(2, Hard.EventSeverityTier);
        Assert.Equal("occasional", OutcomeResolver.ShiftDropBand("staple", "good", Hard.EventSeverityTier, Order));
        Assert.Equal("staple", OutcomeResolver.ShiftDropBand("occasional", "bad", Hard.EventSeverityTier, Order));
    }

    // ---- WeightFor ----

    [Fact]
    public void WeightFor_null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => OutcomeResolver.WeightFor(null!, Weights));
        Assert.Throws<ArgumentNullException>(() => OutcomeResolver.WeightFor("staple", null!));
    }

    [Fact]
    public void WeightFor_unknown_band_throws()
    {
        Assert.Throws<ArgumentException>(() => OutcomeResolver.WeightFor("mythic", Weights));
    }

    [Fact]
    public void WeightFor_returns_the_real_shipped_weight_per_band()
    {
        Assert.Equal(1000, OutcomeResolver.WeightFor("staple", Weights));
        Assert.Equal(300, OutcomeResolver.WeightFor("frequent", Weights));
        Assert.Equal(90, OutcomeResolver.WeightFor("occasional", Weights));
        Assert.Equal(25, OutcomeResolver.WeightFor("seldom", Weights));
        Assert.Equal(7, OutcomeResolver.WeightFor("exceptional", Weights));
    }

    // ---- PickOutcome: argument validation ----

    [Fact]
    public void PickOutcome_null_outcomes_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OutcomeResolver.PickOutcome(null!, 0, Order, Weights, 0, 0, seed: 1));
    }

    [Fact]
    public void PickOutcome_empty_outcomes_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            OutcomeResolver.PickOutcome(Array.Empty<EventOutcomeRow>(), 0, Order, Weights, 0, 0, seed: 1));
    }

    [Fact]
    public void PickOutcome_a_single_outcome_always_wins()
    {
        var outcomes = new[] { Outcome("good", "staple") };
        for (ulong seed = 0; seed < 10; seed++)
            Assert.Equal("good", OutcomeResolver.PickOutcome(outcomes, 0, Order, Weights, 0, 0, seed).Ordinal);
    }

    // ---- PickOutcome: determinism and stream namespacing ----

    [Fact]
    public void PickOutcome_is_deterministic_same_seed_same_room_same_pick()
    {
        var outcomes = new[] { Outcome("good", "staple"), Outcome("bad", "exceptional"), Outcome("mixed", "occasional") };
        var a = OutcomeResolver.PickOutcome(outcomes, 1, Order, Weights, 3, 4, seed: 42);
        var b = OutcomeResolver.PickOutcome(outcomes, 1, Order, Weights, 3, 4, seed: 42);
        Assert.Equal(a.Ordinal, b.Ordinal);
    }

    [Fact]
    public void A_different_room_off_the_same_seed_draws_a_different_sequence()
    {
        var outcomes = new[] { Outcome("good", "staple"), Outcome("bad", "staple"), Outcome("mixed", "staple"), Outcome("nothing", "staple") };
        var atRoomA = new List<string>();
        var atRoomB = new List<string>();
        for (ulong seed = 0; seed < 40; seed++)
        {
            atRoomA.Add(OutcomeResolver.PickOutcome(outcomes, 0, Order, Weights, 0, 0, seed).Ordinal);
            atRoomB.Add(OutcomeResolver.PickOutcome(outcomes, 0, Order, Weights, 9, 9, seed).Ordinal);
        }
        Assert.NotEqual(atRoomA, atRoomB);
    }

    // ---- PickOutcome: severity actually moves the odds, not just the label ----

    [Fact]
    public void Higher_severity_makes_a_staple_good_outcome_rarer_to_draw_against_a_staple_bad_outcome()
    {
        // Both authored at "staple" (equal 1000/1000 odds at tier 0). At tier 3, "good" shifts to
        // "seldom" (weight 25) while "bad" stays put via a separate all-staple row acting as ballast --
        // so raising severity should make the pool land on "good" far less often.
        var outcomes = new[] { Outcome("good", "staple"), Outcome("bad", "staple") };

        int GoodWins(int severityTier)
        {
            var wins = 0;
            for (ulong seed = 0; seed < 300; seed++)
                if (OutcomeResolver.PickOutcome(outcomes, severityTier, Order, Weights, 0, 0, seed).Ordinal == "good")
                    wins++;
            return wins;
        }

        var atZero = GoodWins(0);
        var atThree = GoodWins(3);
        Assert.InRange(atZero, 120, 180); // ~50% at equal 1000/1000 weight
        Assert.True(atThree < atZero / 3, $"expected severity to sharply cut good's odds: tier0={atZero} tier3={atThree}");
    }

    [Fact]
    public void Higher_severity_makes_a_staple_bad_outcome_more_frequent_against_a_seldom_good_outcome()
    {
        // "good" authored at "seldom" (weight 25), "bad" authored at "occasional" (weight 90) -- bad
        // already favoured at tier 0. At tier 2, bad shifts down to "staple" (weight 1000), making it
        // overwhelmingly likely -- "more severe consequences" as a measurable odds shift, not just prose.
        var outcomes = new[] { Outcome("good", "seldom"), Outcome("bad", "occasional") };

        int BadWins(int severityTier)
        {
            var wins = 0;
            for (ulong seed = 0; seed < 300; seed++)
                if (OutcomeResolver.PickOutcome(outcomes, severityTier, Order, Weights, 0, 0, seed).Ordinal == "bad")
                    wins++;
            return wins;
        }

        var atZero = BadWins(0);
        var atTwo = BadWins(2);
        Assert.True(atTwo > atZero, $"expected severity to raise bad's odds further: tier0={atZero} tier2={atTwo}");
        Assert.True(atTwo > 270, $"expected bad to be overwhelmingly favoured at tier 2 (1000 vs 7): got {atTwo}/300");
    }

    // ---- PickOutcome: exhaustion reuses EventDeckRefusal ----

    [Fact]
    public void PickOutcome_refuses_via_EventDeckRefusal_when_every_weight_resolves_to_zero()
    {
        // No band in Order carries weight 0 in the real table, so force it via a custom weight table.
        var zeroWeights = new Dictionary<string, int>(StringComparer.Ordinal) { ["staple"] = 0 };
        var outcomes = new[] { Outcome("mixed", "staple") };
        var ex = Assert.Throws<EventDeckRefusal>(() =>
            OutcomeResolver.PickOutcome(outcomes, 0, Order, zeroWeights, 2, 5, seed: 1));
        Assert.Contains("(2,5)", ex.Message);
    }
}
