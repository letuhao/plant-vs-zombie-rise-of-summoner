using FusionRpg.Core.Vfx;
using Xunit;

namespace FusionRpg.Core.Tests.Vfx;

/// <summary>vfx-v3 M3: per-style motion envelopes — the §4 grammar must be mechanically true.</summary>
public class VfxAuraMathTests
{
    const float Span = 1.2f;
    const float Life = 0.45f;
    const int Count = 6;

    static IEnumerable<BurstParticle> All(VfxAuraStyle style, float phase = 0.7f)
    {
        for (var i = 0; i < Count; i++)
            yield return VfxAuraMath.Particle(style, i, Count, phase, Span, Life);
    }

    [Fact]
    public void Drip_always_falls_from_above()
    {
        foreach (var p in All(VfxAuraStyle.Drip))
        {
            Assert.True(p.VelY < 0f, $"velY={p.VelY}");
            Assert.True(p.PosY > 0f, "drips start above the anchor");
        }
    }

    [Fact]
    public void Rise_always_climbs_and_up_dominates()
    {
        foreach (var p in All(VfxAuraStyle.RiseSparkle))
        {
            Assert.True(p.VelY > 0f);
            Assert.True(p.VelY > MathF.Abs(p.VelX));
        }
    }

    [Fact]
    public void Orbit_stays_on_the_ring()
    {
        foreach (var p in All(VfxAuraStyle.Orbit))
        {
            // x on the r-circle; y is ellipse-squashed and body-offset — check x-extent only
            Assert.True(MathF.Abs(p.PosX) <= Span * 0.45f + 0.001f);
        }
        // and the ring actually rotates with phase
        var a = VfxAuraMath.Particle(VfxAuraStyle.Orbit, 0, Count, 0f, Span, Life);
        var b = VfxAuraMath.Particle(VfxAuraStyle.Orbit, 0, Count, 1f, Span, Life);
        Assert.NotEqual(a.PosX, b.PosX);
    }

    [Fact]
    public void Crackle_stays_in_body_box_and_barely_moves()
    {
        foreach (var p in All(VfxAuraStyle.CrackleJitter))
        {
            Assert.True(MathF.Abs(p.PosX) <= Span * 0.5f + 0.001f);
            Assert.True(MathF.Abs(p.PosY) <= Span * 0.6f + 0.001f);
            Assert.True(MathF.Abs(p.VelX) <= Span * 0.05f + 0.001f);
            Assert.True(p.Energy < Life); // glints are brief
        }
    }

    [Fact]
    public void PulseRing_expands_outward()
    {
        foreach (var p in All(VfxAuraStyle.PulseRing))
        {
            var dot = p.PosX * p.VelX + p.PosY * p.VelY;
            Assert.True(dot > 0f, "velocity must point away from center");
        }
    }

    [Fact]
    public void StreamOut_flows_inward_and_down()
    {
        foreach (var p in All(VfxAuraStyle.StreamOut))
        {
            Assert.True(p.PosX * p.VelX <= 0f, "x-velocity points back toward the host");
            Assert.True(p.VelY < 0f, "drain sinks");
        }
    }

    [Fact]
    public void Deterministic_by_inputs()
    {
        foreach (VfxAuraStyle style in Enum.GetValues<VfxAuraStyle>())
        {
            var a = VfxAuraMath.Particle(style, 3, Count, 0.7f, Span, Life);
            var b = VfxAuraMath.Particle(style, 3, Count, 0.7f, Span, Life);
            Assert.Equal(a, b);
        }
    }
}
