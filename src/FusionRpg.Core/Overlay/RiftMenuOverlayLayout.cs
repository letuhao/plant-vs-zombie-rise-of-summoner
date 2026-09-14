namespace FusionRpg.Core.Overlay;

/// <summary>Responsive, non-interactive Rift art placement for the game's main-menu composition.</summary>
public readonly struct RiftOverlayRect
{
    public RiftOverlayRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
}

/// <summary>
/// Percentage-only placement for the 1254×1254 Rift source. The visual is top-middle, biased left
/// to occupy the open sky between the left menu and the right grave rather than obscuring either.
/// </summary>
public static class RiftMenuOverlayLayout
{
    public const float SourceWidth = 1254f;
    public const float SourceHeight = 1254f;
    public const float AspectRatio = SourceWidth / SourceHeight;

    public const float CenterXPercent = 0.42f;
    public const float TopPercent = 0.06f;
    public const float WidthPercent = 0.17f;
    public const float MaxHeightPercent = 0.34f;

    // The companion is an echo caught inside the primary tear, rather than a second navigation
    // element. Keeping its anchor in the primary rect makes both pieces responsive as one unit.
    public const float MiniSourceWidth = 64f;
    public const float MiniSourceHeight = 64f;
    public const float MiniAspectRatio = MiniSourceWidth / MiniSourceHeight;
    public const float MiniAnchorXPercent = 0.76f;
    public const float MiniAnchorYPercent = 0.74f;
    public const float MiniWidthPrimaryPercent = 0.18f;

    public static RiftOverlayRect TopMiddleLeft(int screenWidth, int screenHeight)
    {
        var width = Math.Max(0, screenWidth) * WidthPercent;
        var maxWidthFromHeight = Math.Max(0, screenHeight) * MaxHeightPercent * AspectRatio;
        width = Math.Min(width, maxWidthFromHeight);
        var height = AspectRatio <= 0f ? 0f : width / AspectRatio;
        var x = Math.Max(0, screenWidth) * CenterXPercent - width / 2f;
        var y = Math.Max(0, screenHeight) * TopPercent;
        return new RiftOverlayRect(Math.Max(0f, x), Math.Max(0f, y), width, height);
    }

    /// <summary>
    /// Places the small Rift icon within the primary art's lower-right interior. Its source ratio
    /// is applied before its display height is calculated, so later non-square icon replacements
    /// do not silently stretch.
    /// </summary>
    public static RiftOverlayRect MiniCompanion(RiftOverlayRect primary)
    {
        var width = Math.Max(0f, primary.Width) * MiniWidthPrimaryPercent;
        var height = MiniAspectRatio <= 0f ? 0f : width / MiniAspectRatio;
        var centerX = primary.X + Math.Max(0f, primary.Width) * MiniAnchorXPercent;
        var centerY = primary.Y + Math.Max(0f, primary.Height) * MiniAnchorYPercent;
        return new RiftOverlayRect(centerX - width / 2f, centerY - height / 2f, width, height);
    }
}
