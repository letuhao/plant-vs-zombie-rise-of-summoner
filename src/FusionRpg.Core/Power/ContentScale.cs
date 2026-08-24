namespace FusionRpg.Core.Power;

/// <summary>
/// content-scale (T3.4, spec-content-scale.md §2.1): the ratio that makes the same authored item
/// worth more when it drops deeper. Applied exactly once, inside <c>Instantiator</c> — nowhere else
/// (§2.2; <c>power-guard</c>, wave 4, scans for a second multiplication).
///
/// <para><c>contentScale(Θc) = PowerLadder.Value(Θc) / pinValue</c> — <c>pinValue</c> read from
/// <see cref="PowerTuning"/>, never a literal (audit F5: an earlier draft hardcoded <c>680</c> and
/// even listed changing it as ask-first, violating PS-7 in the program that declares it).</para>
/// </summary>
public static class ContentScale
{
    /// <summary>contentScale in per-mille — 1000 means ×1.000 (the pin, Θc=20).</summary>
    public static long Milli(int thetaContent, PowerTuning tuning)
    {
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        var valueWhole = new PowerLadder(tuning).Value(thetaContent);
        return checked(valueWhole * 1000L / tuning.Curve.PinValue);
    }

    /// <summary>Applies a per-mille content scale to one rolled magnitude — round half away from
    /// zero, once, matching every other milli→whole conversion in this module.</summary>
    public static int Apply(int rolledValue, long contentScaleMilli)
    {
        long numerator = checked((long)rolledValue * contentScaleMilli);
        long q = numerator / 1000;
        long r = numerator % 1000;
        if (r == 0) return checked((int)q);
        long twiceR = checked(Math.Abs(r) * 2);
        bool roundsUp = twiceR >= 1000;
        if (!roundsUp) return checked((int)q);
        bool negative = numerator < 0;
        return checked((int)(negative ? q - 1 : q + 1));
    }
}
