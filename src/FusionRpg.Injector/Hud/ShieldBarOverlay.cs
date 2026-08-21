using FusionRpg.Injector.Fx;

namespace FusionRpg.Injector.Hud;

/// <summary>
/// Status helper for debug.shield.bar-status — drawing lives in <see cref="ShieldBarPool"/> (world VFX).
/// </summary>
public static class ShieldBarOverlay
{
    /// <summary>Retired — shield bar is world VFX under VfxDirector, not OnGUI.</summary>
    public static void Draw() { }

    public static Dictionary<string, object> CaptureStatus() => ShieldBarPool.CaptureStatus();
}
