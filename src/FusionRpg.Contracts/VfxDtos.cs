namespace FusionRpg.Contracts;

/// <summary>
/// Semantic VFX cue — presentation only, never writes gameplay state (vfx-ssot.md §5).
/// Carries what happened, never how it looks; visuals live in the Core VfxCatalog.
/// </summary>
public sealed class VfxCueDto
{
    public string CueId { get; set; } = "";

    // Anchor: precedence TargetPtr > Cell > World (vfx-ssot.md §5).
    public string? TargetPtr { get; set; }
    public int? Col { get; set; }
    public int? Row { get; set; }
    public float? WorldX { get; set; }
    public float? WorldY { get; set; }

    public long Amount { get; set; }
    public DamageFxTag? Tag { get; set; }

    /// <summary>Element coloring payload (vfx-ssot.md §16); reuses the combat contract type.</summary>
    public List<ElementPayloadComponentDto>? Elements { get; set; }

    public float ScaleMul { get; set; } = 1f;
    public float LifeMul { get; set; } = 1f;
}
