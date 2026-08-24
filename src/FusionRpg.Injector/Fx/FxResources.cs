using FusionRpg.Core.Vfx;
using UnityEngine;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Shared shader / material / texture cache — vfx-ssot.md §10. Wraps <see cref="OverlayShaderProbe"/>.
/// Texture is ALWAYS the generated soft disc: the v1 "steal a vanilla particle texture" idea
/// grabbed arbitrary scene imagery (electric/lightning sheets — LIVE finding 2026-08-21) and is gone.
/// </summary>
public static class FxResources
{
    /// <summary>Render ordering for overlay particles — one named home for the old magic 80.
    /// Config-backed (tunables-ssot.md T1) — data/tuning/vfx.v1.json's render.particleSortingOrder.</summary>
    public static int ParticleSortingOrder => VfxTuningHub.Tuning.Render.ParticleSortingOrder;

    static Material? _particleMat;
    static Texture2D? _softDisc;
    static string _matShaderName = "";
    static readonly Dictionary<Core.Vfx.VfxMarkerShape, Material> MarkerMats = new();
    static readonly Dictionary<Core.Vfx.VfxMarkerShape, Texture2D> MarkerTextures = new();
    static string _markerShaderName = "";

    /// <summary>Cached particle material, or null when no shipped shader survives stripping.</summary>
    public static Material? ParticleMaterial()
    {
        var shader = OverlayShaderProbe.DrawShader();
        var name = OverlayShaderProbe.DrawShaderName();
        if (shader == null || string.IsNullOrEmpty(name))
        {
            _particleMat = null;
            _matShaderName = "";
            return null;
        }

        if (_particleMat != null && string.Equals(_matShaderName, name, StringComparison.Ordinal))
            return _particleMat;

        try
        {
            _particleMat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = SoftDisc()
            };
            try { _particleMat.SetColor("_Color", Color.white); } catch { }
            try { _particleMat.SetColor("_TintColor", new Color(1f, 1f, 1f, 0.6f)); } catch { }
            _matShaderName = name;
            return _particleMat;
        }
        catch
        {
            _particleMat = null;
            _matShaderName = "";
            return null;
        }
    }

    /// <summary>Cached material for a procedurally generated marker shape (vfx-v3 M5).</summary>
    public static Material? MarkerMaterial(Core.Vfx.VfxMarkerShape shape)
    {
        var shader = OverlayShaderProbe.DrawShader();
        var name = OverlayShaderProbe.DrawShaderName();
        if (shader == null || string.IsNullOrEmpty(name)) return null;
        if (!string.Equals(_markerShaderName, name, StringComparison.Ordinal))
        {
            MarkerMats.Clear();
            _markerShaderName = name;
        }

        if (MarkerMats.TryGetValue(shape, out var cached) && cached != null) return cached;
        try
        {
            var mat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = MarkerTexture(shape)
            };
            try { mat.SetColor("_Color", Color.white); } catch { }
            MarkerMats[shape] = mat;
            return mat;
        }
        catch
        {
            return null;
        }
    }

    static Texture2D MarkerTexture(Core.Vfx.VfxMarkerShape shape)
    {
        if (MarkerTextures.TryGetValue(shape, out var cached) && cached != null) return cached;
        // Config-backed (tunables-ssot.md T1) — data/tuning/vfx.v1.json's render.particleTextureSize.
        var size = VfxTuningHub.Tuning.Render.ParticleTextureSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "FusionRpgMarker" + shape
        };
        var c = (size - 1) * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var nx = (x - c) / c; // -1..1
                var ny = (y - c) / c;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, ShapeAlpha(shape, nx, ny)));
            }
        }

        tex.Apply(false, true);
        MarkerTextures[shape] = tex;
        return tex;
    }

    static float ShapeAlpha(Core.Vfx.VfxMarkerShape shape, float nx, float ny)
    {
        // Config-backed (tunables-ssot.md T1) — data/tuning/vfx.v1.json's render.markerEdgeSoftness.
        var edge = (float)VfxTuningHub.Tuning.Render.MarkerEdgeSoftness;
        switch (shape)
        {
            case Core.Vfx.VfxMarkerShape.Diamond:
            {
                var d = MathF.Abs(nx) + MathF.Abs(ny);
                return Mathf.Clamp01((0.8f - d) / edge);
            }
            case Core.Vfx.VfxMarkerShape.TriangleDown:
            {
                // apex at (0, -0.8), base from (-0.7, 0.6) to (0.7, 0.6)
                if (ny > 0.6f || ny < -0.8f) return 0f;
                var halfWidth = 0.7f * ((ny + 0.8f) / 1.4f);
                return Mathf.Clamp01((halfWidth - MathF.Abs(nx)) / edge);
            }
            case Core.Vfx.VfxMarkerShape.Cross:
            {
                if (MathF.Abs(nx) > 0.8f || MathF.Abs(ny) > 0.8f) return 0f;
                var arm = MathF.Min(MathF.Abs(nx), MathF.Abs(ny));
                return Mathf.Clamp01((0.18f - arm) / edge);
            }
            default: // Ring
            {
                var d = MathF.Sqrt(nx * nx + ny * ny);
                return Mathf.Clamp01((0.12f - MathF.Abs(d - 0.66f)) / edge);
            }
        }
    }

    static Texture2D SoftDisc()
    {
        if (_softDisc != null) return _softDisc;
        // Config-backed (tunables-ssot.md T1) — data/tuning/vfx.v1.json's render.particleTextureSize.
        var size = VfxTuningHub.Tuning.Render.ParticleTextureSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "FusionRpgSoftDisc"
        };
        var cx = (size - 1) * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - cx) / cx;
                var dy = (y - cx) / cx;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply(false, true);
        _softDisc = tex;
        return tex;
    }
}
