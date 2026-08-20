namespace FusionRpg.Core.Vfx;

public enum VfxPrimitiveKind
{
    Floater = 0,
    Burst = 1,
    Flash = 2
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
}
