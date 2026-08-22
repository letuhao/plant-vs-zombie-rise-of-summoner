using FusionRpg.Core.Overlay;
using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>
/// The in-game "switch to the web UI" button. One action only — it is not a cheats surface.
///
/// Interactive IMGUI cannot use the Repaint-only gate the floaters use: the control has to be drawn
/// on the Layout and mouse passes too or it never receives input. Same event filter as
/// <see cref="OverlaySettingsGui"/>, and the cost is held down to one cached rect and one button.
/// </summary>
public static class OverlaySwitchGui
{
    static Rect _rect;
    static int _rectW = -1;
    static int _rectH = -1;

    public static void Draw()
    {
        try
        {
            if (!OverlaySwitch.ButtonVisible) return;
            var e = Event.current;
            if (e == null) return;
            if (e.type != EventType.Repaint
                && e.type != EventType.Layout
                && e.type != EventType.MouseDown
                && e.type != EventType.MouseUp)
                return;
        }
        catch
        {
            return;
        }

        try
        {
            EnsureLayout();
            if (GUI.Button(_rect, "RPG"))
                OverlaySwitch.RequestToggle();
        }
        catch { }
    }

    /// <summary>Bottom-right, clear of the seed bank and the shovel slot. Recomputed only on resize.</summary>
    static void EnsureLayout()
    {
        var w = Screen.width;
        var h = Screen.height;
        if (w == _rectW && h == _rectH) return;

        _rectW = w;
        _rectH = h;
        var r = OverlaySwitchLayout.BottomRight(w, h); // scales with the display; see Core tests
        _rect = new Rect(r.X, r.Y, r.Width, r.Height);
    }
}
