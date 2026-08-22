namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// When a number resolves. Three policies for the four roll moments — moment 3 (bind / equip)
/// deliberately has none, because binding never rolls: if a value would change at equip, it is
/// <see cref="OnApply"/>.
/// </summary>
public enum RollPolicy
{
    /// <summary>Never resolves — the number is the number. <c>Min == Max</c>.</summary>
    Fixed = 0,

    /// <summary>Moment 2 — the item drops and the value freezes forever, off the instance roll seed.</summary>
    OnInstantiate,

    /// <summary>Moment 4 — resolves every time the atom applies. `100-200 fire damage on hit`.</summary>
    OnApply,
}

/// <summary>
/// Every number an atom can carry. Anywhere a magnitude could go in an atom's params, a ValueSpec
/// can go.
///
/// <para><b>Units are not interchangeable.</b> <c>+10 hp</c> is ten hit points; <c>+10 fire power</c>
/// is ten <i>resolver points</i> on a sigmoid scale where CritRateScale is 100.0, so ten points is
/// 0.1 sigmoid units. Tier bands are authored per channel family and never copied across — see
/// definitions.md §2 and E9's normalize(magnitude, referenceScale), which exists precisely so a
/// coefficient table does not price these two alike and land an order of magnitude out.</para>
///
/// <para>Integer throughout: magnitudes are int, curve multipliers are per-mille, and interpolation
/// rounds half away from zero exactly once.</para>
/// </summary>
/// <param name="Min">Inclusive lower bound.</param>
/// <param name="Max">Inclusive upper bound.</param>
/// <param name="Roll">When the value resolves.</param>
/// <param name="CurveId">Optional <c>effect_curve</c> row scaling Min and Max before any roll.</param>
public readonly record struct ValueSpec(int Min, int Max, RollPolicy Roll, string? CurveId = null)
{
    /// <summary>A single number that never rolls.</summary>
    public static ValueSpec Of(int value) => new(value, value, RollPolicy.Fixed);

    /// <summary>An inclusive range rolled on every apply.</summary>
    public static ValueSpec Range(int min, int max) => new(min, max, RollPolicy.OnApply);

    public bool IsFixed => Roll == RollPolicy.Fixed;

    /// <summary>
    /// Shape validation only. An unknown <see cref="CurveId"/> is E4's rejection at load, not this
    /// module's — one owner, not two.
    /// </summary>
    public AtomRejection Validate()
    {
        if (Min > Max)
            return AtomRejection.Fail(AtomRejectionReason.BadValueSpec, $"min {Min} > max {Max}");

        // "fixed" means one number. A fixed spec with a spread is an authoring mistake that would
        // otherwise silently resolve to Min forever.
        if (Roll == RollPolicy.Fixed && Min != Max)
            return AtomRejection.Fail(AtomRejectionReason.BadValueSpec,
                $"fixed value spec has a range [{Min}, {Max}] — use OnInstantiate or OnApply");

        if (CurveId is { Length: 0 })
            return AtomRejection.Fail(AtomRejectionReason.BadCurve, "curveId is empty");

        return AtomRejection.Ok;
    }

    /// <summary>
    /// Resolve to a number. <paramref name="rng"/> is required only for rolling policies; a Fixed
    /// spec never touches it, so a caller with no stream can still resolve constants.
    /// </summary>
    public int Resolve(IAtomRandom? rng)
    {
        if (Min == Max) return Min;

        if (Roll == RollPolicy.Fixed)
            return Min; // validation rejects this shape; resolve stays total rather than throwing hot.

        if (rng is null)
            throw new InvalidOperationException(
                $"ValueSpec [{Min}, {Max}] with roll {Roll} needs a stream; none was supplied.");

        return rng.NextInclusive(Min, Max);
    }

    /// <summary>
    /// Apply a curve multiplier to both bounds <b>before</b> any roll, so the inclusive-bounds
    /// guarantee still holds after scaling.
    /// </summary>
    public ValueSpec Scaled(int multiplierMilli) =>
        this with
        {
            Min = CurveTable.ApplyMilli(Min, multiplierMilli),
            Max = CurveTable.ApplyMilli(Max, multiplierMilli),
        };
}
