namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>
/// The one shared read formula (ssot-power-scale.md §4.6, Rule PS-3; class-system-map.md §SS2d) —
/// every consumer that turns an aptitude point share into a channel value calls in here: the real
/// resolver (<c>AptitudeSubsystem</c>, P2.4/P2.5) and the closed-form predictor (Phase 4) share this
/// implementation rather than each carrying its own copy of the arithmetic. `guard-class-system.ps1`
/// G5 fails the build if a second <c>class AptitudeReadFunctions</c> appears anywhere under `src/`.
///
/// <para><b>Contest</b> is Θ-free: <c>k · share^γ · spanPoints</c>. Its result is a bounded contest
/// point value (0..spanPoints), not a game magnitude — <c>double</c> throughout is the PS-8 bounded-
/// ratio exemption, not a violation of CLAUDE.md's long-magnitude rule.</para>
///
/// <para><b>Magnitude</b> reads the ladder: <c>k · share^γ · P(Θ)</c>. Its result IS a magnitude, so
/// it never returns anything but a `long`, and only ever throws (never wraps) when the true answer
/// does not fit one. `share^γ` is the sole unavoidable floating-point step — a real-exponent power of
/// a bounded [0,1] ratio has no pure-integer form — so it is collapsed to a per-mille `long`
/// immediately (bounded [0,1000], since `x^γ ∈ [0,1]` for `x ∈ [0,1]`, `γ > 0`) and never touched as a
/// `double` again. The widening multiply uses `decimal`, not `checked long`: two independent per-mille
/// factors (`k`, `share^γ`) compound against a `pTheta` that legitimately reaches into the quintillions
/// (CLAUDE.md's long-magnitude ceiling), and a `long*long*long` chain overflows on the *intermediate*
/// product even when the true final answer fits comfortably — the same "multiply first, divide last"
/// trap <see cref="Core.Power.PowerLadder"/>'s `TriangularMilli` already documents for a single
/// per-mille factor. `decimal` (96-bit exact integer precision, ~7.9e28 range) has enough headroom for
/// the full three-way product and throws its own <see cref="OverflowException"/> if it does not — never
/// silently wraps — so this still "throws, never wraps" end to end while never spuriously rejecting an
/// input whose true answer was always representable.</para>
/// </summary>
public static class AptitudeReadFunctions
{
    /// <summary>Contest read: <c>k · share^γ · spanPoints</c>. Θ-free (PS-3) — bounded ratio, exempt
    /// from the long-magnitude rule (PS-8).</summary>
    public static double Contest(long kMilli, double share, long shareExponentMilli, long spanPointsMilli)
    {
        ValidateShare(share);
        if (kMilli < 0) throw new ArgumentOutOfRangeException(nameof(kMilli), kMilli, "kMilli must not be negative");
        if (shareExponentMilli <= 0) throw new ArgumentOutOfRangeException(nameof(shareExponentMilli), shareExponentMilli, "shareExponentMilli must be positive");
        if (spanPointsMilli < 0) throw new ArgumentOutOfRangeException(nameof(spanPointsMilli), spanPointsMilli, "spanPointsMilli must not be negative");

        var gamma = shareExponentMilli / 1000.0;
        var k = kMilli / 1000.0;
        var span = spanPointsMilli / 1000.0;
        return k * Math.Pow(share, gamma) * span;
    }

    /// <summary>Magnitude read: <c>k · share^γ · P(Θ)</c>. Reads the ladder (PS-3) — always `long`,
    /// throws (never wraps) when the true answer would not fit one.</summary>
    public static long Magnitude(long kMilli, double share, long shareExponentMilli, long pTheta)
    {
        ValidateShare(share);
        if (kMilli < 0) throw new ArgumentOutOfRangeException(nameof(kMilli), kMilli, "kMilli must not be negative");
        if (shareExponentMilli <= 0) throw new ArgumentOutOfRangeException(nameof(shareExponentMilli), shareExponentMilli, "shareExponentMilli must be positive");
        if (pTheta < 0) throw new ArgumentOutOfRangeException(nameof(pTheta), pTheta, "pTheta must not be negative");

        var gamma = shareExponentMilli / 1000.0;
        var sharePow = Math.Pow(share, gamma); // in [0,1] for share in [0,1], gamma > 0 — the one float step
        var sharePowMilli = (long)Math.Round(sharePow * 1000.0, MidpointRounding.AwayFromZero);

        // decimal (96-bit exact integer precision) is the widening type for this multiply, not long:
        // see the class doc for why a long*long*long chain here overflows spuriously. decimal division
        // and Math.Round are exact for these magnitudes, so this is the single rounding step, done once,
        // last, by the two per-mille factors' combined scale (1000 * 1000).
        decimal rawMicro = (decimal)kMilli * sharePowMilli * pTheta;
        decimal rounded = Math.Round(rawMicro / 1_000_000m, MidpointRounding.AwayFromZero);

        if (rounded > long.MaxValue || rounded < long.MinValue)
            throw new OverflowException($"aptitude magnitude overflow: kMilli={kMilli} share={share} pTheta={pTheta}");
        return (long)rounded;
    }

    static void ValidateShare(double share)
    {
        if (double.IsNaN(share) || share < 0.0 || share > 1.0)
            throw new ArgumentOutOfRangeException(nameof(share), share, "share must be in [0,1]");
    }
}
