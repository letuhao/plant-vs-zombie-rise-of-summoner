using FusionRpg.Core.Overlay;
using FusionRpg.Injector.Host;
using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>
/// Draws the presentation-only Rift in the idle main-menu sky. It owns no gameplay state and has
/// no input: a board appearing hides it immediately, so it can never cover lawn play or HUD chrome.
/// </summary>
public static class RiftMenuOverlay
{
    const string MainAssetRelativePath = "assets/onboarding/rift-portal-pvz-style.png";
    const string MiniAssetRelativePath = "assets/onboarding/rift-icon-pvz-style-64.png";
    static Texture2D? _mainTexture;
    static bool _mainLoadAttempted;
    static Texture2D? _miniTexture;
    static bool _miniLoadAttempted;

    public static void Draw()
    {
        try
        {
            var current = Event.current;
            if (current == null || current.type != EventType.Repaint || GameHooks.Board != null) return;

            var mainTexture = GetTexture(ref _mainTexture, ref _mainLoadAttempted, MainAssetRelativePath, "FusionRpgRiftMenuOverlay");
            if (mainTexture == null) return;

            var layout = RiftMenuOverlayLayout.TopMiddleLeft(Screen.width, Screen.height);
            if (layout.Width <= 0f || layout.Height <= 0f) return;

            var time = Time.unscaledTime;
            DrawMainRift(mainTexture, layout, time);

            var miniTexture = GetTexture(ref _miniTexture, ref _miniLoadAttempted, MiniAssetRelativePath, "FusionRpgRiftMiniOverlay");
            if (miniTexture != null) DrawMiniRift(miniTexture, layout, time);
        }
        catch
        {
            // A missing presentation asset must never affect menus or the game loop.
        }
    }

    static void DrawMainRift(Texture texture, RiftOverlayRect layout, float time)
    {
        var pulse = (Mathf.Sin(time * 0.92f) + 1f) * 0.5f;
        var wobbleDegrees = Mathf.Sin(time * 0.47f) * 0.8f;
        var baseRect = ToUnityRect(layout);

        // Transparent expanded passes form an intermittent aura using the authored alpha edge;
        // no shader or scene object is needed, keeping this strictly menu presentation.
        DrawTinted(texture, Expand(baseRect, 1.13f + pulse * 0.035f), wobbleDegrees * 0.45f, new Color(0.43f, 0.16f, 0.82f, 0.13f));
        DrawTinted(texture, Expand(baseRect, 1.055f + pulse * 0.02f), wobbleDegrees * 0.75f, new Color(0.69f, 1f, 0.28f, 0.16f));
        DrawTinted(texture, Expand(baseRect, 1f + pulse * 0.012f), wobbleDegrees, new Color(1f, 1f, 1f, 1f));
    }

    static void DrawMiniRift(Texture texture, RiftOverlayRect primary, float time)
    {
        var pulse = (Mathf.Sin(time * 1.12f + 1.7f) + 1f) * 0.5f;
        var companion = RiftMenuOverlayLayout.MiniCompanion(primary);
        var baseRect = ToUnityRect(companion);
        baseRect.y += primary.Height * Mathf.Sin(time * 1.35f + 0.4f) * 0.012f;
        var wobbleDegrees = Mathf.Sin(time * 0.74f + 0.9f) * 1.2f;

        DrawTinted(texture, Expand(baseRect, 1.19f + pulse * 0.04f), -wobbleDegrees * 0.35f, new Color(0.55f, 0.18f, 0.9f, 0.12f));
        DrawTinted(texture, Expand(baseRect, 1.08f + pulse * 0.025f), -wobbleDegrees * 0.65f, new Color(0.72f, 1f, 0.31f, 0.13f));
        DrawTinted(texture, baseRect, -wobbleDegrees, new Color(1f, 1f, 1f, 0.88f));
    }

    static void DrawTinted(Texture texture, Rect rect, float degrees, Color tint)
    {
        var priorColor = GUI.color;
        var priorMatrix = GUI.matrix;
        try
        {
            GUI.color = tint;
            GUIUtility.RotateAroundPivot(degrees, rect.center);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, alphaBlend: true);
        }
        finally
        {
            GUI.matrix = priorMatrix;
            GUI.color = priorColor;
        }
    }

    static Rect ToUnityRect(RiftOverlayRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    static Rect Expand(Rect rect, float scale)
    {
        var width = rect.width * scale;
        var height = rect.height * scale;
        return new Rect(rect.center.x - width / 2f, rect.center.y - height / 2f, width, height);
    }

    static Texture2D? GetTexture(ref Texture2D? texture, ref bool loadAttempted, string relativePath, string textureName)
    {
        if (texture != null) return texture;
        if (loadAttempted) return null;
        loadAttempted = true;

        try
        {
            var path = Path.Combine(RpgHost.PluginDir, relativePath);
            if (!File.Exists(path))
            {
                RpgHost.Log.Warning($"[rift] menu art unavailable: {path}");
                return null;
            }

            var loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!ImageConversion.LoadImage(loadedTexture, File.ReadAllBytes(path), markNonReadable: true))
            {
                UnityEngine.Object.Destroy(loadedTexture);
                RpgHost.Log.Warning($"[rift] menu art is not a readable PNG: {path}");
                return null;
            }

            texture = loadedTexture;
            return texture;
        }
        catch (Exception ex)
        {
            RpgHost.Log.Warning("[rift] menu art load failed: " + ex.Message);
            return null;
        }
    }
}
