using FusionRpg.Injector.Host;
using FusionRpg.Injector.Lawn;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Short-lived additive particle burst at a lawn cell (or unit world pos).
/// Uses a Fusion-included particle shader plus a soft disc texture — not a solid quad.
/// Presentation only — HP stays on FA10. No vanilla cherry/chili spawn.
/// Number popups stay IMGUI in <see cref="DamageFxOverlay"/>.
/// </summary>
public static class OverlayWorldFx
{
    public const int Cap = 24;
    public const float DefaultLifeSeconds = 0.55f;
    const int BurstCount = 28;

    static readonly List<Burst> Live = new();
    static Material? _mat;
    static Texture2D? _softDisc;
    static string _matShaderName = "";

    public static void SpawnAtCell(int col, int row) =>
        SpawnAtCell(col, row, new Color(1f, 0.5f, 0.08f, 0.95f));

    public static void SpawnAtCell(int col, int row, Color color, float lifeSeconds = DefaultLifeSeconds)
    {
        col = LawnCoords.ClampCol(col);
        row = LawnCoords.ClampRow(row);
        Vector2 center;
        try { center = LawnCoords.CellCenter(col, row); }
        catch
        {
            EmitSkipped(col, row, "cell-center");
            return;
        }

        var size = EstimateCellSize(col, row);
        Spawn(new Vector3(center.x, center.y, 0f), HitFlashColor(color), lifeSeconds, size, col, row, "cell");
    }

    public static void SpawnAtWorld(Vector3 world, Color color, float lifeSeconds = DefaultLifeSeconds)
    {
        var size = EstimateCellSize(CheatState.SpawnCol, CheatState.SpawnRow);
        Spawn(world, HitFlashColor(color), lifeSeconds, size, -1, -1, "world");
    }

    public static void Tick(float dt)
    {
        if (dt < 0f) dt = 0f;
        for (var i = Live.Count - 1; i >= 0; i--)
        {
            var b = Live[i];
            b.Age += dt;
            if (b.Age < b.Life && b.Go != null) continue;
            try
            {
                if (b.Go != null) UObject.Destroy(b.Go);
            }
            catch { }
            Live.RemoveAt(i);
        }
    }

    public static Color HitFlashColor(Color floater)
    {
        if (floater.r > 0.85f && floater.g > 0.85f && floater.b > 0.85f)
            return new Color(1f, 0.5f, 0.08f, 0.95f);
        return floater;
    }

    static void Spawn(Vector3 world, Color color, float lifeSeconds, Vector2 size, int col, int row, string source)
    {
        if (lifeSeconds <= 0f) lifeSeconds = DefaultLifeSeconds;
        if (!EnsureMaterial())
        {
            EmitSkipped(col, row, "no-shader");
            return;
        }

        while (Live.Count >= Cap)
        {
            try
            {
                if (Live[0].Go != null) UObject.Destroy(Live[0].Go);
            }
            catch { }
            Live.RemoveAt(0);
        }

        if (!TrySpawnParticles(world, color, size, lifeSeconds))
        {
            EmitSkipped(col, row, "particle-fail");
            return;
        }

        DebugRuntime.Emit("debug.fx.world.shown", new Dictionary<string, object>
        {
            ["col"] = col,
            ["row"] = row,
            ["source"] = source,
            ["shader"] = _matShaderName,
            ["mode"] = "particle",
            ["count"] = Live.Count
        });
    }

    static bool TrySpawnParticles(Vector3 world, Color color, Vector2 cellSize, float life)
    {
        try
        {
            var go = new GameObject("FusionRpgOverlayBurst");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.position = world;
            var ps = go.AddComponent<ParticleSystem>();
            if (ps == null)
            {
                UObject.Destroy(go);
                return false;
            }

            try
            {
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && _mat != null)
                {
                    renderer.sharedMaterial = _mat;
                    renderer.material = _mat;
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.sortingOrder = 80;
                }
            }
            catch { }

            ConfigureAndEmit(ps, color, cellSize, life);
            Live.Add(new Burst { Go = go, Life = life + 0.2f, Age = 0f });
            return true;
        }
        catch (Exception ex)
        {
            try { RpgHost.Log.Warning("[fx.world] particle: " + ex.Message); }
            catch { }
            return false;
        }
    }

    static void ConfigureAndEmit(ParticleSystem ps, Color color, Vector2 cellSize, float life)
    {
        var span = Mathf.Max(0.35f, Mathf.Min(cellSize.x, cellSize.y));
        try { ps.playOnAwake = false; } catch { }
        try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
        try
        {
            var main = ps.main;
            main.startColor = color;
            main.startLifetime = life;
            main.startSpeed = span * 1.6f;
        }
        catch { }

        try { ps.Play(); } catch { }

        var c32 = (Color32)color;
        for (var i = 0; i < BurstCount; i++)
        {
            var ang = (i / (float)BurstCount) * Mathf.PI * 2f;
            var rad = span * (0.04f + 0.12f * (i % 5) / 5f);
            var pos = new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, 0f);
            var speed = span * (0.8f + (i % 7) * 0.18f);
            var vel = new Vector3(Mathf.Cos(ang) * speed, Mathf.Sin(ang) * speed, 0f);
            var size = span * (0.16f + (i % 4) * 0.08f);
            var energy = life * (0.55f + (i % 5) * 0.08f);
            try { ps.Emit(pos, vel, size, energy, c32); }
            catch { break; }
        }
    }

    static void EmitSkipped(int col, int row, string reason)
    {
        DebugRuntime.Emit("debug.fx.world.skipped", new Dictionary<string, object>
        {
            ["col"] = col,
            ["row"] = row,
            ["reason"] = reason,
            ["drawShader"] = OverlayShaderProbe.DrawShaderName()
        });
    }

    static bool EnsureMaterial()
    {
        var shader = OverlayShaderProbe.DrawShader();
        var name = OverlayShaderProbe.DrawShaderName();
        if (shader == null || string.IsNullOrEmpty(name))
        {
            _mat = null;
            _matShaderName = "";
            return false;
        }

        if (_mat != null && string.Equals(_matShaderName, name, StringComparison.Ordinal))
            return true;

        try
        {
            var tex = StealParticleTexture() ?? SoftDisc();
            _mat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = tex
            };
            try { _mat.SetColor("_Color", Color.white); } catch { }
            try { _mat.SetColor("_TintColor", new Color(1f, 1f, 1f, 0.6f)); } catch { }
            _matShaderName = name;
            return true;
        }
        catch
        {
            _mat = null;
            _matShaderName = "";
            return false;
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

    static Vector2 EstimateCellSize(int col, int row)
    {
        try
        {
            var a = LawnCoords.CellCenter(col, row);
            var col2 = col >= LawnCoords.LastCol ? Math.Max(0, col - 1) : col + 1;
            var row2 = row >= LawnCoords.LastRow ? Math.Max(0, row - 1) : row + 1;
            var b = LawnCoords.CellCenter(col2, row);
            var c = LawnCoords.CellCenter(col, row2);
            var w = Mathf.Abs(b.x - a.x);
            var h = Mathf.Abs(c.y - a.y);
            if (w < 0.05f) w = 1f;
            if (h < 0.05f) h = 1f;
            return new Vector2(w, h);
        }
        catch
        {
            return Vector2.one;
        }
    }

    sealed class Burst
    {
        public GameObject? Go;
        public float Life;
        public float Age;
    }
}
