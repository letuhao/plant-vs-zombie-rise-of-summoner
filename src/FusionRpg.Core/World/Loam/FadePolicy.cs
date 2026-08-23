namespace FusionRpg.Core.World.Loam;

/// <summary>
/// Shortfall lowers <c>StabilityMilli</c>, scaled to how deep it runs; surplus raises it, always
/// more slowly than a shortfall could ever lower it (spec-loam-calc.md #5). Symmetric rates would
/// make a sector oscillate on the boundary and turn a dramatic mechanic into a flickering number,
/// so recovery is fixed and slow while decay grows — capped — with the deficit.
/// </summary>
public static class FadePolicy
{
    /// <summary>The new <c>StabilityMilli</c>, given the current value and a component's balance.</summary>
    public static int Apply(int currentStabilityMilli, long balance) =>
        balance < 0
            ? Math.Max(0, currentStabilityMilli - DecayFor(-balance))
            : Math.Min(1000, currentStabilityMilli + LoamPolicy.RecoveryMilli);

    /// <summary>
    /// How much a deficit of this size decays stability this turn — floored at
    /// <see cref="LoamPolicy.BaseDecayMilli"/> so even a one-unit shortfall costs something, ceilinged
    /// at <see cref="LoamPolicy.MaxDecayMilli"/> so no single turn can zero a sector outright.
    /// </summary>
    public static int DecayFor(long deficitMagnitude)
    {
        // Everything stays `long` until the final clamp so a huge deficit cannot overflow its way
        // past the ceiling instead of hitting it — the same overflow-by-construction discipline as
        // `LoamUpkeep`.
        long scaled = LoamPolicy.BaseDecayMilli
                      + deficitMagnitude / LoamPolicy.DecayScaleDivisor * LoamPolicy.DecayPerDeficitUnitMilli;

        return (int)Math.Max(0, Math.Min(LoamPolicy.MaxDecayMilli, scaled));
    }
}
