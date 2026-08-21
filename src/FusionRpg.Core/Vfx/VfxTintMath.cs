namespace FusionRpg.Core.Vfx;

/// <summary>
/// Sustained tint composition — vfx-v3 M4. Pure: layers lerp the base toward each status
/// color, strength hard-clamped to <see cref="MaxStrength"/> so a unit never loses identity.
/// </summary>
public static class VfxTintMath
{
    public const float MaxStrength = 0.35f;

    public static (byte R, byte G, byte B) Composite(
        (byte R, byte G, byte B) baseRgb,
        IEnumerable<((byte R, byte G, byte B) Rgb, float Strength)> layers)
    {
        float r = baseRgb.R, g = baseRgb.G, b = baseRgb.B;
        foreach (var (rgb, strength) in layers)
        {
            var s = Math.Clamp(strength, 0f, MaxStrength);
            r += (rgb.R - r) * s;
            g += (rgb.G - g) * s;
            b += (rgb.B - b) * s;
        }

        return (Clamp(r), Clamp(g), Clamp(b));
    }

    static byte Clamp(float v) => v <= 0f ? (byte)0 : v >= 255f ? (byte)255 : (byte)Math.Round(v);
}
