namespace FusionRpg.Core.Vfx;

public enum VfxPrimitiveKind
{
    Floater = 0,
    Burst = 1,
    Flash = 2,
    // Sustained kinds (vfx-v3): lifetime runs apply-cue → expire-cue, not LifeSeconds.
    Aura = 3,
    Tint = 4,
    Marker = 5
}

/// <summary>Aura motion styles — sampled by pure VfxAuraMath (SPEC vfx-v3 §4 grammar).</summary>
public enum VfxAuraStyle
{
    Drip = 0,
    Orbit = 1,
    RiseSparkle = 2,
    CrackleJitter = 3,
    PulseRing = 4,
    StreamOut = 5
}

public enum VfxMarkerShape
{
    Ring = 0,
    Diamond = 1,
    TriangleDown = 2,
    Cross = 3
}

public enum VfxColorSourceKind
{
    /// <summary>Tag palette with element override per vfx-ssot.md §16.4.</summary>
    TagOrElement = 0,
    Fixed = 1
}

public enum VfxLabelSourceKind
{
    None = 0,
    TagAmount = 1,
    Fixed = 2
}

/// <summary>Burst emission pattern — SPEC W4. Radial is the legacy default look.</summary>
public enum VfxBurstShape
{
    Radial = 0,
    Rising = 1,
    Directional = 2
}

/// <summary>One render instruction inside a recipe — data only, no Unity types (vfx-ssot.md §6).</summary>
public sealed class VfxPrimitiveSpec
{
    public VfxPrimitiveKind Kind { get; init; }
    public VfxBurstShape Shape { get; init; } = VfxBurstShape.Radial;
    public VfxColorSourceKind Color { get; init; } = VfxColorSourceKind.TagOrElement;
    /// <summary>Render only when the cue resolved an element color — plain/omni hits skip this
    /// spec (owner call 2026-08-21: normal damage always fires, so its burst carries no signal).</summary>
    public bool RequireElement { get; init; }

    public VfxAuraStyle AuraStyle { get; init; } = VfxAuraStyle.Drip;
    public VfxMarkerShape MarkerShape { get; init; } = VfxMarkerShape.Ring;
    /// <summary>Tint kind only: lerp strength toward FixedRgb (clamped by VfxTintMath.MaxStrength).</summary>
    public float TintStrength { get; init; } = 0.2f;

    public bool IsSustained =>
        Kind is VfxPrimitiveKind.Aura or VfxPrimitiveKind.Tint or VfxPrimitiveKind.Marker;
    public (byte R, byte G, byte B) FixedRgb { get; init; } = (255, 255, 255);
    public VfxLabelSourceKind Label { get; init; } = VfxLabelSourceKind.None;
    public string FixedLabel { get; init; } = "";
    public float LifeSeconds { get; init; }
    public float SizeScale { get; init; } = 1f;
    public int Count { get; init; } = 1;
    public float DelaySeconds { get; init; }
}

/// <summary>Per-recipe rate-limit intervals; grouping keys are fixed by kind (vfx-ssot.md §7).</summary>
public sealed class VfxRateLimit
{
    public float FloaterSeconds { get; init; } = VfxRules.FloaterRateLimitSeconds;
    public float BurstSeconds { get; init; } = VfxRules.BurstRateLimitSeconds;
}

/// <summary>What one cue looks like: an ordered list of primitive specs (vfx-ssot.md §6).</summary>
public sealed class VfxRecipe
{
    public string CueId { get; init; } = "";
    public IReadOnlyList<VfxPrimitiveSpec> Primitives { get; init; } = Array.Empty<VfxPrimitiveSpec>();
    public VfxRateLimit RateLimit { get; init; } = new();

    public bool HasSustained => Primitives.Any(p => p.IsSustained);
    public bool HasMarker => Primitives.Any(p => p.Kind == VfxPrimitiveKind.Marker);
}
