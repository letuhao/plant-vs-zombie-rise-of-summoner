using FusionRpg.Contracts;
using FusionRpg.Core.Effects;

namespace FusionRpg.Core.Vfx;

/// <summary>
/// Color/label precedence for one cue — vfx-ssot.md §16.4. Pure and computed once per cue:
/// semantic tags keep the tag palette; element payload colors plain and crit damage;
/// crit distinctness moves to font size (crit-orange collides with fire).
/// </summary>
public sealed class VfxColorPlan
{
    public (byte R, byte G, byte B) Rgb { get; init; } = (255, 255, 255);
    public bool Hybrid { get; init; }
    public IReadOnlyList<ElementPayloadComponentDto> HybridComponents { get; init; } =
        Array.Empty<ElementPayloadComponentDto>();
    public float FontScale { get; init; } = 1f;
    public string Label { get; init; } = "";

    /// <summary>Burst tint: near-white floater color renders as the legacy orange hit flash.</summary>
    public (byte R, byte G, byte B) BurstRgb =>
        Rgb.R > 217 && Rgb.G > 217 && Rgb.B > 217 ? VfxSeedCatalog.ProbeOrange : Rgb;

    /// <summary>Floater color at life t — cycles hue for hybrid, constant otherwise.</summary>
    public (byte R, byte G, byte B) ColorAt(float t) =>
        Hybrid ? ElementFxPalette.HybridColorAt(HybridComponents, t) : Rgb;

    public static VfxColorPlan For(
        DamageFxTag? tag,
        IReadOnlyList<ElementPayloadComponentDto>? elements,
        bool elementFxOn,
        long amount)
    {
        var t = tag ?? DamageFxTag.Neutral;
        var label = DamageFxPalette.Label(t, amount);
        var semantic = t is DamageFxTag.Dodge or DamageFxTag.Block or DamageFxTag.Null
            or DamageFxTag.Absorb or DamageFxTag.Reflect or DamageFxTag.Heal
            or DamageFxTag.Weak or DamageFxTag.Resist;
        var concrete = elementFxOn && !semantic
            ? ElementFxPalette.Concrete(elements)
            : new List<ElementPayloadComponentDto>();

        if (concrete.Count == 0)
            return new VfxColorPlan { Rgb = DamageFxPalette.Rgb(t), Label = label };

        var fontScale = t == DamageFxTag.Crit ? VfxRules.CritFontScale : 1f;
        if (concrete.Count == 1)
        {
            return new VfxColorPlan
            {
                Rgb = ElementFxPalette.Rgb(concrete[0].Element),
                Label = label,
                FontScale = fontScale
            };
        }

        return new VfxColorPlan
        {
            Rgb = ElementFxPalette.HybridColorAt(concrete, 0f),
            Hybrid = true,
            HybridComponents = concrete,
            Label = label,
            FontScale = fontScale
        };
    }
}
