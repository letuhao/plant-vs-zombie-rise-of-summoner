namespace FusionRpg.Core.Power;

/// <summary>A diverging contest is not a tuning choice (SSOT §5.1) — thrown when WfMilli != WaMilli.</summary>
public sealed class PowerWeightInvalid : Exception
{
    public long WaMilli { get; }
    public long WfMilli { get; }

    public PowerWeightInvalid(long waMilli, long wfMilli)
        : base($"power index: WfMilli ({wfMilli}) must equal WaMilli ({waMilli}) — realmsAdvanced diverging between actor and content sides breaks the contest (SSOT §5.1)")
    {
        WaMilli = waMilli;
        WfMilli = wfMilli;
    }
}

/// <summary>Wm is null until the world program supplies it (SSOT §9.1) — thrown at first ContentIndex use, never guessed.</summary>
public sealed class PowerWeightMissing : Exception
{
    public string Weight { get; }

    public PowerWeightMissing(string weight) : base($"power index: weight '{weight}' is not loaded — content-side Θ cannot be composed without it")
    {
        Weight = weight;
    }
}

/// <summary>
/// The actor-side raw ladder inputs (SSOT §5) for one identity — daveLevel/realmsAdvanced/pvzRuns.
/// A plain snapshot record; hydration policy (how it gets populated, cached, invalidated) belongs to
/// each host (spec-power-index.md §2.5), not to this type.
/// </summary>
public sealed record ActorLadderSnapshot(int DaveLevel, int RealmsAdvanced, int PvzRuns)
{
    public static readonly ActorLadderSnapshot Empty = new(0, 0, 0);
}

/// <summary>
/// The weighted sum that produces Θ (ssot-power-scale.md §5, spec-power-index.md §2.1) — pure, no
/// I/O, no magnitude computed here (PS-3: this module produces the index only; <see cref="PowerLadder"/>
/// turns Θ into P(Θ)). <see cref="ActorExplain"/> and <see cref="ContentExplain"/> are the single code
/// path both the index and the report share, so the two cannot drift (spec-power-index.md §2.4).
/// </summary>
public static class PowerIndexComposer
{
    /// <summary>Wf must equal Wa exactly (SSOT §5.1) — call once per loaded tuning, not per request.</summary>
    public static void ValidateWeights(PowerWeightsTuning weights)
    {
        if (weights.WfMilli != weights.WaMilli)
            throw new PowerWeightInvalid(weights.WaMilli, weights.WfMilli);
    }

    public static PowerAxisReport ActorExplain(PowerTuning tuning, ActorLadderSnapshot snapshot)
    {
        ValidateWeights(tuning.Weights);
        var w = tuning.Weights;
        return BuildReport(
            ("dave", w.WdMilli, ClampNonNegative(snapshot.DaveLevel)),
            ("realmsAdvanced", w.WaMilli, ClampNonNegative(snapshot.RealmsAdvanced)),
            ("pvzRuns", w.WrMilli, ClampNonNegative(snapshot.PvzRuns)));
    }

    public static PowerAxisReport ContentExplain(PowerTuning tuning, ContentContext ctx)
    {
        ValidateWeights(tuning.Weights);
        var w = tuning.Weights;
        if (w.WmMilli is not { } wmMilli)
            throw new PowerWeightMissing("Wm");

        return BuildReport(
            ("zombossLevel", w.WzMilli, ClampNonNegative(ctx.ZombossLevel)),
            ("dangerBand", wmMilli, ClampNonNegative(ctx.DangerBand)),
            ("worldTier", w.WwMilli, ClampNonNegative(ctx.WorldTier)),
            ("realmsAdvanced", w.WfMilli, ClampNonNegative(ctx.RealmsAdvanced)));
    }

    // A missing progression row is absence, not corruption (spec-power-index.md §5) — clamp, never throw.
    static long ClampNonNegative(int raw) => raw < 0 ? 0 : raw;

    static PowerAxisReport BuildReport(params (string AxisId, long WeightMilli, long RawValue)[] axes)
    {
        var milli = new long[axes.Length];
        long totalMilli = 0;
        checked
        {
            for (int i = 0; i < axes.Length; i++)
            {
                milli[i] = axes[i].WeightMilli * axes[i].RawValue;
                totalMilli += milli[i];
            }
        }

        int total = checked((int)RoundHalfAwayFromZero(totalMilli, 1000));

        var contributions = new PowerAxisContribution[axes.Length];
        for (int i = 0; i < axes.Length; i++)
        {
            // Rounded ONCE, at the sum (totalMilli), never per-axis (spec-power-index.md §2.1, §6
            // Always) — sharePermille and Whole below are display-only breakdowns of that one sum,
            // and never feed back into Total.
            int sharePermille = totalMilli == 0
                ? 0
                : (int)RoundHalfAwayFromZero(checked(milli[i] * 1000), totalMilli);
            long whole = RoundHalfAwayFromZero(milli[i], 1000);
            contributions[i] = new PowerAxisContribution(axes[i].AxisId, milli[i], whole, sharePermille);
        }

        return new PowerAxisReport(total, contributions);
    }

    static long RoundHalfAwayFromZero(long numerator, long denominator)
    {
        long q = numerator / denominator;
        long r = numerator % denominator;
        if (r == 0) return q;
        long twiceR = checked(Math.Abs(r) * 2);
        bool roundsUp = twiceR >= Math.Abs(denominator);
        bool negative = (numerator < 0) != (denominator < 0);
        if (!roundsUp) return q;
        return negative ? q - 1 : q + 1;
    }
}
