using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>
/// Lightweight presentation settings panel (F7). Does not host gameplay cheats.
/// </summary>
public static class OverlaySettingsGui
{
    const float PanelW = 280f;
    const float PanelH = 196f;

    public static void Draw()
    {
        try
        {
            if (!OverlaySettings.SettingsOpen) return;
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
            var x = 16f;
            var y = 16f;
            GUI.Box(new Rect(x, y, PanelW, PanelH), "Overlay Settings");
            var cy = y + 28f;
            var on = OverlaySettings.ShieldBarEnabled;
            var next = GUI.Toggle(new Rect(x + 12f, cy, PanelW - 24f, 22f), on, " Shield bar");
            if (next != on)
                OverlaySettings.ShieldBarEnabled = next;
            cy += 28f;
            var btn = OverlaySettings.OverlayButtonEnabled;
            var btnNext = GUI.Toggle(new Rect(x + 12f, cy, PanelW - 24f, 22f), btn, " Web UI button");
            if (btnNext != btn)
                OverlaySettings.OverlayButtonEnabled = btnNext;
            cy += 28f;
            var pause = OverlaySettings.PauseWhileAway;
            var pauseNext = GUI.Toggle(new Rect(x + 12f, cy, PanelW - 24f, 22f), pause, " Pause while away");
            if (pauseNext != pause)
                OverlaySettings.PauseWhileAway = pauseNext;
            cy += 28f;
            GUI.Label(new Rect(x + 12f, cy, PanelW - 24f, 20f),
                "Toggle bar: " + OverlaySettings.ShieldBarHotKey);
            cy += 22f;
            GUI.Label(new Rect(x + 12f, cy, PanelW - 24f, 20f),
                "This panel: " + OverlaySettings.SettingsHotKey);
            cy += 28f;
            if (GUI.Button(new Rect(x + 12f, cy, 80f, 24f), "Close"))
                OverlaySettings.SettingsOpen = false;
        }
        catch { }
    }
}
