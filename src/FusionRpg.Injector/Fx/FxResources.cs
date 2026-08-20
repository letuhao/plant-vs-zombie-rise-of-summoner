using UnityEngine;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Shared shader / material / texture cache — vfx-ssot.md §10. Wraps <see cref="OverlayShaderProbe"/>.
/// Texture is ALWAYS the generated soft disc: the v1 "steal a vanilla particle texture" idea
/// grabbed arbitrary scene imagery (electric/lightning sheets — LIVE finding 2026-08-21) and is gone.
/// </summary>
public static class FxResources
{
    /// <summary>Render ordering for overlay particles — one named home for the old magic 80.</summary>
    public const int ParticleSortingOrder = 80;

    static Material? _particleMat;
    static Texture2D? _softDisc;
    static string _matShaderName = "";

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

    static Texture2D SoftDisc()
    {
        if (_softDisc != null) return _softDisc;
        const int size = 64;
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
