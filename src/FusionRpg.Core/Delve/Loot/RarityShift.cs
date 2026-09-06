using FusionRpg.Core.Items.Drops;

namespace FusionRpg.Core.Delve.Loot;

/// <summary>
/// `dungeon-loot` D3.14 (spec-dungeon-loot.md §4, "Rung reward columns — the floor and the shift") —
/// the two pure, math-only pieces of the rung reward columns: a FLOOR (removes low rungs entirely) and
/// a WEIGHT SHIFT (moves the default weight column up or down `n` rungs, "never a multiplier"). Both
/// are breadth and ceiling, never `contentScale`/`P(Θ)`/an atom range (spec, verbatim). Composing these
/// into a room's own live table (`RarityShift.Apply`, the spec's own cited orchestrator) is
/// `DelveLoot.RollRoom`'s own remaining job (D3.11's already-named gap) — this file owns only the two
/// functions that math, not the per-table wiring around them.
/// </summary>
public static class RarityShift
{
    /// <summary>
    /// Spec §4, verbatim: "floor = the highest ordinal among the entry's authored `RarityFloor`, the
    /// room kind's, the rung's and — once-domain boss — `domain.onceEntry.bossRarityFloor`." Any source
    /// may be `null` (not authored, or not a once-domain boss); the floor is the strongest of whichever
    /// are present, never an average or a sum. `null` when NONE are present — no floor at all.
    /// </summary>
    public static string? ComposeFloor(IReadOnlyList<RarityRung> ladder, params string?[] floors)
    {
        if (ladder is null) throw new ArgumentNullException(nameof(ladder));
        if (floors is null) throw new ArgumentNullException(nameof(floors));

        var ordinals = floors.Where(f => f is not null).Select(f => RarityDraw.OrdinalOf(ladder, f!)).ToList();
        if (ordinals.Count == 0) return null;

        var maxOrdinal = ordinals.Max();
        return ladder.First(r => r.Ordinal == maxOrdinal).RarityId;
    }

    /// <summary>
    /// Spec §4, verbatim: "`delta[o] = w(o − 10n) − w(o)` per ordinal, bottom `n` rungs zeroed, top
    /// absorbing — the default weight column moved up `n` rungs; the window shifts, nothing is
    /// multiplied." `10` is not hardcoded here — it is the ladder's own real ordinal step, read from
    /// the supplied ladder (10 for the shipped ten-rung ladder, `core.v1.json`), so this stays correct
    /// even if a future ladder spaces ordinals differently.
    ///
    /// <para><b>Verified directly against the spec's own precise test citation</b> (`spec-dungeon-loot.md`
    /// Testing strategy: *"`ToWeightShift(ladder, 1)` sums to zero and zeroes exactly the bottom rung"*):
    /// working the formula by hand for `n=1` over a 10-rung ladder shows `Σ delta[o] = -w(max)` under
    /// the NAIVE "zero outside range" reading alone — for the sum to reach zero as the spec demands,
    /// the TOP ordinal's own delta cannot be the plain formula; it must be the RESIDUAL that makes the
    /// total conserve (`delta[max] = −Σ(every other delta)`), which is exactly what "top absorbing"
    /// means: the top rung keeps its own weight AND gains whatever shifted up into it, rather than the
    /// plain formula overwriting it and losing `w(max)` to a nonexistent ordinal above the ladder.</para>
    ///
    /// <para><b>Assumed, not separately cited:</b> for a NEGATIVE `n` ("shifts down, the ladder's
    /// `very-easy`", spec verbatim), the roles mirror by symmetry — the BOTTOM ordinal becomes the
    /// absorbing edge and the TOP zeroes naturally. The spec's own precise test citation only exercises
    /// `n=1`; this file's own tests prove the conservation property (`Σ delta = 0`) holds for a negative
    /// shift too, as the strongest evidence available that the extrapolation is sound, but the exact
    /// negative-shift boundary is this file's own reasoned choice, not a second literal citation.</para>
    /// </summary>
    public static IReadOnlyDictionary<int, int> ToWeightShift(IReadOnlyList<RarityRung> ladder, int n)
    {
        if (ladder is null) throw new ArgumentNullException(nameof(ladder));
        if (ladder.Count == 0) throw new ArgumentException("ladder must not be empty", nameof(ladder));

        var ordinals = ladder.Select(r => r.Ordinal).OrderBy(o => o).ToList();
        if (n == 0) return ordinals.ToDictionary(o => o, _ => 0); // spec, verbatim: "n = 0 is empty"

        if (ordinals.Count < 2)
            throw new ArgumentException("a shift needs at least two rungs to have a step between them", nameof(ladder));

        var step = ordinals[1] - ordinals[0];
        var minOrdinal = ordinals[0];
        var maxOrdinal = ordinals[^1];
        var byOrdinal = ladder.ToDictionary(r => r.Ordinal, r => r.DropWeightPer100k);

        int W(int x) => x >= minOrdinal && x <= maxOrdinal && byOrdinal.TryGetValue(x, out var w) ? w : 0;

        // The edge opposite the shift's own source direction absorbs the residual (conserves total
        // weight); the other edge zeroes naturally through W's own "0 outside [min,max]" rule.
        var absorbOrdinal = n > 0 ? maxOrdinal : minOrdinal;

        var delta = new Dictionary<int, int>();
        var runningSum = 0;
        foreach (var o in ordinals.Where(o => o != absorbOrdinal))
        {
            var d = checked(W(o - step * n) - W(o));
            delta[o] = d;
            runningSum = checked(runningSum + d);
        }
        delta[absorbOrdinal] = checked(-runningSum);
        return delta;
    }
}
