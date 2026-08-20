using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Shared shader / material / texture cache — vfx-ssot.md §10. Wraps <see cref="OverlayShaderProbe"/>.
/// Texture order stays steal-first, soft-disc fallback; the winner is reported in the probe payload.
/// </summary>
public static class FxResources
{
    /// <summary>Render ordering for overlay particles — one named home for the old magic 80.</summary>
    public const int ParticleSortingOrder = 80;

    static Material? _particleMat;
    static Texture2D? _softDisc;
    static string _matShaderName = "";
    static string _textureSource = "";

    /// <summary>Last texture source used for the particle material ("stolen" | "soft-disc" | "").</summary>
    public static string TextureSource => _textureSource;

    /// <summary>Cached additive particle material, or null when no shipped shader survives stripping.</summary>
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
            var stolen = StealParticleTexture();
            _textureSource = stolen != null ? "stolen" : "soft-disc";
            _particleMat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = stolen ?? SoftDisc()
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

    static Texture? StealParticleTexture()
    {
        try
        {
            foreach (var r in UObject.FindObjectsOfType<ParticleSystemRenderer>())
            {
                if (r == null) continue;
                Material? src = null;
                try { src = r.sharedMaterial; } catch { }
                if (src == null)
                {
                    try { src = r.material; } catch { }
                }
                if (src == null) continue;
                Texture? tex = null;
                try { tex = src.mainTexture; } catch { }
                if (tex != null) return tex;
            }
        }
        catch { }

        return null;
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
