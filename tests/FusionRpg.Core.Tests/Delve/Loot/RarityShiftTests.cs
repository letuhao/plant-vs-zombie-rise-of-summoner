using FusionRpg.Core.Delve.Loot;
using FusionRpg.Core.Items.Drops;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Loot;

/// <summary>D3.14 (spec-dungeon-loot.md §4, "Rung reward columns — the floor and the shift"). A small,
/// hand-built 5-rung ladder (ordinals 10-50, weights 1000/300/90/25/7 — the real shipped dropBand
/// weight ladder's own numbers, reused here only as recognizable, hand-verifiable values) rather than
/// the real item-corpus ladder, so every expected delta is computed by hand, not approximated.</summary>
public class RarityShiftTests
{
    static RarityRung Rung(string id, int ordinal, int weight) => new(id, ordinal, 0, 0, 0, 0, weight);

    static readonly IReadOnlyList<RarityRung> Ladder = new[]
    {
        Rung("staple", 10, 1000),
        Rung("frequent", 20, 300),
        Rung("occasional", 30, 90),
        Rung("seldom", 40, 25),
        Rung("exceptional", 50, 7),
    };

    static int NewWeight(IReadOnlyDictionary<int, int> shift, int ordinal, int oldWeight) => oldWeight + shift[ordinal];

    // ---- ComposeFloor ----

    [Fact]
    public void ComposeFloor_null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => RarityShift.ComposeFloor(null!, "staple"));
        Assert.Throws<ArgumentNullException>(() => RarityShift.ComposeFloor(Ladder, null!));
    }

    [Fact]
    public void All_null_floors_compose_to_no_floor_at_all()
    {
        Assert.Null(RarityShift.ComposeFloor(Ladder, null, null, null));
    }

    [Fact]
    public void A_single_floor_passes_through()
    {
        Assert.Equal("occasional", RarityShift.ComposeFloor(Ladder, "occasional"));
    }

    [Fact]
    public void The_highest_ordinal_among_several_floors_wins_never_an_average()
    {
        Assert.Equal("seldom", RarityShift.ComposeFloor(Ladder, "staple", "seldom", "occasional", null));
    }

    [Fact]
    public void Equal_floors_compose_to_the_same_floor()
    {
        Assert.Equal("frequent", RarityShift.ComposeFloor(Ladder, "frequent", "frequent"));
    }

    // ---- ToWeightShift: argument validation ----

    [Fact]
    public void ToWeightShift_null_or_empty_ladder_throws()
    {
        Assert.Throws<ArgumentNullException>(() => RarityShift.ToWeightShift(null!, 1));
        Assert.Throws<ArgumentException>(() => RarityShift.ToWeightShift(Array.Empty<RarityRung>(), 1));
    }

    [Fact]
    public void A_single_rung_ladder_with_a_nonzero_shift_throws()
    {
        var oneRung = new[] { Rung("staple", 10, 1000) };
        Assert.Throws<ArgumentException>(() => RarityShift.ToWeightShift(oneRung, 1));
    }

    [Fact]
    public void A_single_rung_ladder_at_n_zero_does_not_throw()
    {
        var oneRung = new[] { Rung("staple", 10, 1000) };
        var shift = RarityShift.ToWeightShift(oneRung, 0);
        Assert.Equal(0, shift[10]);
    }

    // ---- ToWeightShift: n = 0 is empty (spec, verbatim) ----

    [Fact]
    public void N_zero_is_empty_every_ordinal_maps_to_zero()
    {
        var shift = RarityShift.ToWeightShift(Ladder, 0);
        Assert.Equal(5, shift.Count);
        Assert.All(shift.Values, d => Assert.Equal(0, d));
    }

    // ---- ToWeightShift: n = 1, the spec's own precisely-cited case ----

    [Fact]
    public void N_one_sums_to_zero()
    {
        var shift = RarityShift.ToWeightShift(Ladder, 1);
        Assert.Equal(0, shift.Values.Sum());
    }

    [Fact]
    public void N_one_zeroes_exactly_the_bottom_rung()
    {
        var shift = RarityShift.ToWeightShift(Ladder, 1);
        Assert.Equal(0, NewWeight(shift, 10, 1000));
    }

    [Fact]
    public void N_one_moves_every_middle_rungs_own_weight_up_by_one_step()
    {
        var shift = RarityShift.ToWeightShift(Ladder, 1);
        Assert.Equal(1000, NewWeight(shift, 20, 300));  // frequent now carries staple's own old weight
        Assert.Equal(300, NewWeight(shift, 30, 90));    // occasional now carries frequent's
        Assert.Equal(90, NewWeight(shift, 40, 25));     // seldom now carries occasional's
    }

    [Fact]
    public void N_one_the_top_rung_absorbs_rather_than_losing_its_own_weight()
    {
        // "Top absorbing": exceptional keeps ITS OWN old weight (7) AND gains what shifted up from
        // seldom (25) -- 32, not a plain 25-only replacement (which would lose the old 7 to nowhere).
        var shift = RarityShift.ToWeightShift(Ladder, 1);
        Assert.Equal(7 + 25, NewWeight(shift, 50, 7));
    }

    // ---- ToWeightShift: n = 2 generalizes the same property ----

    [Fact]
    public void N_two_also_sums_to_zero()
    {
        var shift = RarityShift.ToWeightShift(Ladder, 2);
        Assert.Equal(0, shift.Values.Sum());
    }

    [Fact]
    public void N_two_zeroes_the_bottom_two_rungs()
    {
        var shift = RarityShift.ToWeightShift(Ladder, 2);
        Assert.Equal(0, NewWeight(shift, 10, 1000));
        Assert.Equal(0, NewWeight(shift, 20, 300));
        Assert.Equal(1000, NewWeight(shift, 30, 90)); // occasional now carries staple's own old weight (2 steps up)
    }

    // ---- ToWeightShift: negative n (this file's own reasoned, not separately spec-cited, extrapolation) ----

    [Fact]
    public void Negative_n_also_sums_to_zero()
    {
        var shift = RarityShift.ToWeightShift(Ladder, -1);
        Assert.Equal(0, shift.Values.Sum());
    }

    [Fact]
    public void Negative_n_zeroes_the_top_rung_by_symmetry()
    {
        var shift = RarityShift.ToWeightShift(Ladder, -1);
        Assert.Equal(0, NewWeight(shift, 50, 7));
    }

    [Fact]
    public void Negative_n_the_bottom_rung_absorbs_by_symmetry()
    {
        var shift = RarityShift.ToWeightShift(Ladder, -1);
        // staple keeps its own 1000 AND gains what shifted down from frequent (300).
        Assert.Equal(1000 + 300, NewWeight(shift, 10, 1000));
    }

    [Fact]
    public void Negative_n_moves_every_middle_rungs_own_weight_down_by_one_step()
    {
        var shift = RarityShift.ToWeightShift(Ladder, -1);
        Assert.Equal(90, NewWeight(shift, 20, 300));  // frequent now carries occasional's own old weight
        Assert.Equal(25, NewWeight(shift, 30, 90));   // occasional now carries seldom's
        Assert.Equal(7, NewWeight(shift, 40, 25));    // seldom now carries exceptional's
    }
}
