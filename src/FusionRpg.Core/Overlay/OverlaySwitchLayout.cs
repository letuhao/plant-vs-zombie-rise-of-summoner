namespace FusionRpg.Core.Overlay;

/// <summary>Where the in-game overlay button sits, in device pixels.</summary>
public readonly struct OverlayButtonRect
{
    public OverlayButtonRect(float x, float y, float width, float height)
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
/// Button geometry, kept out of the Unity file so the DPI behaviour is testable. IMGUI works in
/// device pixels, so a button sized for 1080p is half the physical size on a 4K display — the
/// scale below keeps it hittable without letting it grow into the lawn.
/// </summary>
public static class OverlaySwitchLayout
{
    public const float BaseButtonW = 72f;
    public const float BaseButtonH = 28f;
    public const float BaseMargin = 16f;

    /// <summary>Height the base sizes were chosen against.</summary>
    public const float ReferenceHeight = 1080f;

    /// <summary>Never shrink: the button is already small, and a 720p target would be unhittable.</summary>
    public const float MinScale = 1f;

    public const float MaxScale = 3f;

    public static float ScaleFor(int screenHeight)
    {
        if (screenHeight <= 0) return MinScale;
        var scale = screenHeight / ReferenceHeight;
        if (scale < MinScale) return MinScale;
        return scale > MaxScale ? MaxScale : scale;
    }

    /// <summary>Bottom-right corner, scaled, and always fully on screen even for a degenerate size.</summary>
    public static OverlayButtonRect BottomRight(int screenWidth, int screenHeight)
    {
        var scale = ScaleFor(screenHeight);
        var w = BaseButtonW * scale;
        var h = BaseButtonH * scale;
        var margin = BaseMargin * scale;

        // A screen too small to hold the button at all still gets a positive, on-screen rect.
        var x = screenWidth - w - margin;
        var y = screenHeight - h - margin;
        if (x < 0) x = 0;
        if (y < 0) y = 0;

        return new OverlayButtonRect(x, y, w, h);
    }
}
