namespace FusionRpg.Core.Hud;

/// <summary>Maps status effective magnitude to lawn display band — not raw numbers (GG-60).</summary>
public static class MagnitudeBandDisplay
{
    // Structural display thresholds — not balance tunables; v1 omits config row until a pass asks.
    const double MidThreshold = 10.0;
    const double HighThreshold = 30.0;

    public static MagnitudeBand FromEffectiveMagnitude(double magnitude)
    {
        if (double.IsNaN(magnitude) || magnitude < MidThreshold)
            return MagnitudeBand.Low;
        if (magnitude < HighThreshold)
            return MagnitudeBand.Mid;
        return MagnitudeBand.High;
    }
}
