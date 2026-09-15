namespace FusionRpg.Core.Overlay;

/// <summary>A screen-space rect in device pixels. Plain data — no Unity, so it is testable alone.</summary>
public readonly record struct RiftMenuRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
}

/// <summary>
/// The tombstone's placement, derived from <see cref="RiftMenuTuning"/> and the current screen size.
///
/// Decision 17 replaced the old hand-derived *hit box*: hit-testing now belongs to the game's own
/// uGUI rect/raycast (the affordance is a real <c>Button</c> in the menu hierarchy). What remains
/// here is **placement only** — where to put it, with a device-pixel floor so it stays hittable.
/// The floor is the one structural rule worth keeping: a fraction of a small window can round to a
/// sliver, and a sliver is not clickable.
///
/// Pure and Unity-free (the <c>OverlaySwitchLayout</c> precedent): the injector passes the screen
/// size in, tests pass whatever they like.
/// </summary>
public static class RiftMenuPlacement
{
    /// <summary>
    /// The affordance rect for a screen of <paramref name="screenW"/> x <paramref name="screenH"/>.
    /// Fractions scale with the screen; the device-pixel floor is applied so the result never collapses
    /// to an unclickable sliver on a small window. The result is always non-negative and never larger
    /// than the screen; a degenerate (non-positive) size yields the floor at the origin — there is no
    /// screen to be on, but the rect is still well-formed rather than negative.
    /// </summary>
    public static RiftMenuRect Resolve(float screenW, float screenH)
    {
        var t = OverlayTuningHub.Tuning.RiftMenu;
        var min = t.MinDevicePx;

        var w = screenW > 0f ? screenW * t.WidthFraction : min;
        var h = screenH > 0f ? screenH * t.HeightFraction : min;
        if (w < min) w = min;
        if (h < min) h = min;

        // The floor must not push the box past a screen smaller than the floor itself.
        if (screenW > 0f && w > screenW) w = screenW;
        if (screenH > 0f && h > screenH) h = screenH;

        var x = screenW > 0f ? screenW * t.AnchorCenterX - w / 2f : 0f;
        var y = screenH > 0f ? screenH * t.AnchorCenterY - h / 2f : 0f;

        // Clamp fully on-screen (a placement rule, not a magnitude cap).
        if (screenW > 0f) x = Math.Clamp(x, 0f, screenW - w);
        if (screenH > 0f) y = Math.Clamp(y, 0f, screenH - h);

        return new RiftMenuRect(x, y, w, h);
    }
}
