namespace FusionRpg.Core.Vfx;

/// <summary>
/// Per-particle emission math for sustained auras — SPEC vfx-v3 M3. Pure; deterministic by
/// (style, index, count, phase). The §4 grammar: Drip = DoT, Orbit = passive affliction/link,
/// RiseSparkle = buff, CrackleJitter = armor/electric, PulseRing = active mark, StreamOut = drain.
/// Reuses <see cref="BurstParticle"/>; the injector pool maps floats onto Unity vectors.
/// </summary>
public static class VfxAuraMath
{
    public static BurstParticle Particle(
        VfxAuraStyle style, int index, int count, float phase, float span, float life)
    {
        if (count < 1) count = 1;
        if (index < 0) index = 0;
        if (span < 0.2f) span = 0.2f;
        var u = (index % count + 0.5f) / count;          // stratified 0..1
        var jitter = (index * 7 + (int)(phase * 13f)) % 11 / 11f; // deterministic variety
        var size = span * (0.10f + index % 3 * 0.05f);
        var energy = life * (0.8f + index % 3 * 0.1f);

        switch (style)
        {
            case VfxAuraStyle.Orbit:
            {
                var ang = phase * 2.2f + u * MathF.PI * 2f;
                var r = span * 0.45f;
                return new BurstParticle(
                    MathF.Cos(ang) * r, MathF.Sin(ang) * r * 0.6f + span * 0.1f,
                    -MathF.Sin(ang) * span * 0.25f, MathF.Cos(ang) * span * 0.15f + span * 0.08f,
                    size, energy);
            }
            case VfxAuraStyle.RiseSparkle:
            {
                var x = (u - 0.5f) * span * 0.7f;
                return new BurstParticle(
                    x, -span * 0.2f + jitter * span * 0.2f,
                    (jitter - 0.5f) * span * 0.15f, span * (0.45f + jitter * 0.3f),
                    size * 0.8f, energy);
            }
            case VfxAuraStyle.CrackleJitter:
            {
                var x = (u - 0.5f) * span;
                var y = (jitter - 0.5f) * span * 1.2f;
                return new BurstParticle(
                    x, y,
                    (u - 0.5f) * span * 0.05f, (jitter - 0.5f) * span * 0.05f,
                    size * 0.6f, energy * 0.45f);
            }
            case VfxAuraStyle.PulseRing:
            {
                var ang = u * MathF.PI * 2f;
                var r0 = span * 0.15f;
                return new BurstParticle(
                    MathF.Cos(ang) * r0, MathF.Sin(ang) * r0 * 0.5f,
                    MathF.Cos(ang) * span * 0.8f, MathF.Sin(ang) * span * 0.4f,
                    size * 0.7f, energy * 0.7f);
            }
            case VfxAuraStyle.StreamOut:
            {
                var ang = u * MathF.PI * 2f;
                var r = span * 0.5f;
                return new BurstParticle(
                    MathF.Cos(ang) * r, MathF.Sin(ang) * r * 0.7f + span * 0.15f,
                    -MathF.Cos(ang) * span * 0.35f, -MathF.Abs(MathF.Sin(ang)) * span * 0.3f - span * 0.1f,
                    size * 0.8f, energy);
            }
            default: // Drip
            {
                var x = (u - 0.5f) * span * 0.8f;
                return new BurstParticle(
                    x, span * (0.3f + jitter * 0.3f),
                    (jitter - 0.5f) * span * 0.08f, -span * (0.35f + jitter * 0.25f),
                    size, energy);
            }
        }
    }
}
