using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Vfx;
using FusionRpg.Injector.Host;
using FusionRpg.Injector.Hud;
using FusionRpg.Injector.Lawn;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Shader world-space RPG shield bars — follow BodyWorld.
/// Fill length uses <see cref="ShieldBarVisual.DisplayRatio"/> (10% steps); track = max capacity.
/// Uses <see cref="FxResources.ParticleMaterial"/> (same OverlayShaderProbe path as bursts).
/// Never Unity GUI.
/// </summary>
static class ShieldBarPool
{
    const float BarWorldWidth = 0.95f;
    const float BarWorldHeight = 0.12f;
    const float WorldYOffset = -0.35f;
    const int MaxSegments = 3;
    const int Cap = 32;
    const int MaxPips = 3;

    static readonly List<(string? ElementId, long Hp)> ScratchStacks = new(3);
    static readonly List<ShieldBarColor.Stop> ScratchStops = new(3);
    static readonly HashSet<string> SeenThisTick = new(StringComparer.Ordinal);
    static readonly List<Slot> Slots = new();
    static readonly MaterialPropertyBlock Block = new();

    static Mesh? _quad;
    static Texture2D? _solid;
    static Material? _barMat;
    static string _barMatShader = "";
    static bool _shaderWarned;
    static int _worldBars;
    static float _lastAvgRatio;
    static float _lastAvgTrueRatio;
    static bool _shaderOk;
    static string _lastEarly = "never-synced";

    sealed class Slot
    {
        public string OwnerKey = "";
        public GameObject? Root;
        public MeshRenderer? Track;
        public readonly MeshRenderer?[] Segments = new MeshRenderer?[MaxSegments];
        public readonly GameObject?[] Pips = new GameObject?[MaxPips];
        public bool Live;
    }

    public static int WorldBars => _worldBars;
    public static bool ShaderOk => _shaderOk;
    public static float LastAvgRatio => _lastAvgRatio;
    public static string LastEarly => _lastEarly;

    public static Dictionary<string, object> CaptureStatus()
    {
        Effects.EffectRuntime.Ensure();
        var runtime = Effects.EffectRuntime.Bag.ShieldGate?.Runtime;
        var owners = new List<object>();
        var dataOwners = 0;
        var resolved = 0;
        if (runtime != null)
        {
            runtime.VisitOwners((ownerKey, shields) =>
            {
                dataOwners++;
                long hp = 0, max = 0;
                var stacks = 0;
                for (var i = 0; i < shields.Count; i++)
                {
                    hp += shields[i].Hp;
                    max += shields[i].MaxHp;
                    if (shields[i].Hp > 0) stacks++;
                }

                var hex = CombatPtr.Normalize(ownerKey);
                var follow = AnchorResolver.Resolve(hex);
                if (follow != null) resolved++;
                owners.Add(new Dictionary<string, object>
                {
                    ["ownerKey"] = ownerKey,
                    ["ptr"] = hex,
                    ["hp"] = hp,
                    ["maxHp"] = max,
                    ["stackCount"] = stacks,
                    ["hasBody"] = follow != null,
                    ["ratio"] = ShieldBarVisual.DisplayRatio(hp, max),
                    ["trueRatio"] = max > 0 ? (float)hp / max : 0f,
                    ["displayRatio"] = ShieldBarVisual.DisplayRatio(hp, max)
                });
            });
        }

        return new Dictionary<string, object>
        {
            ["enabled"] = OverlaySettings.ShieldBarEnabled,
            ["hasRuntime"] = runtime != null,
            ["hasInstances"] = runtime?.HasAnyInstances() ?? false,
            ["dataOwners"] = dataOwners,
            ["resolvedBodies"] = resolved,
            ["worldBars"] = _worldBars,
            ["fillRatio"] = _lastAvgRatio,
            ["trueRatio"] = _lastAvgTrueRatio,
            ["displayRatio"] = _lastAvgRatio,
            ["shaderOk"] = _shaderOk,
            ["owners"] = owners,
            ["lastDraw"] = new Dictionary<string, object>
            {
                ["early"] = _lastEarly,
                ["drawnOwners"] = _worldBars,
                ["dataOwners"] = dataOwners,
                ["worldBars"] = _worldBars,
                ["fillRatio"] = _lastAvgRatio,
                ["trueRatio"] = _lastAvgTrueRatio,
                ["displayRatio"] = _lastAvgRatio,
                ["shaderOk"] = _shaderOk
            }
        };
    }

    /// <summary>Called from VfxDirector.Tick — sync bars to live shield owners.</summary>
    public static void TickSync()
    {
        if (!OverlaySettings.ShieldBarEnabled)
        {
            if (_worldBars > 0) StopAll();
            _lastEarly = "disabled";
            return;
        }

        var runtime = Effects.EffectRuntime.Bag.ShieldGate?.Runtime;
        if (runtime == null || !runtime.HasAnyInstances())
        {
            if (_worldBars > 0) StopAll();
            _lastEarly = runtime == null ? "no-runtime" : "no-instances";
            return;
        }

        var mat = EnsureBarMaterial();
        if (mat == null)
        {
            if (!_shaderWarned)
            {
                _shaderWarned = true;
                try
                {
                    DebugRuntime.Emit("debug.fx.skipped", new Dictionary<string, object>
                    {
                        ["cueId"] = "shield.bar",
                        ["reason"] = VfxSkipReasons.NoShader
                    });
                }
                catch { }
            }

            if (_worldBars > 0) StopAll();
            _shaderOk = false;
            _lastEarly = "no-shader";
            return;
        }

        _shaderOk = true;
        SeenThisTick.Clear();
        var live = 0;
        var displaySum = 0f;
        var trueSum = 0f;

        runtime.VisitOwners((ownerKey, shields) =>
        {
            try
            {
                long hp = 0, max = 0;
                ScratchStacks.Clear();
                for (var i = 0; i < shields.Count; i++)
                {
                    var s = shields[i];
                    hp += s.Hp;
                    max += s.MaxHp;
                    if (s.Hp > 0)
                    {
                        var el = s.Element is { } e ? e.ToElementId() : "none";
                        ScratchStacks.Add((el, s.Hp));
                    }
                }

                if (hp <= 0 || max <= 0 || ScratchStacks.Count == 0) return;
                if (!ShieldBarColor.TryBuildStops(ScratchStacks, ScratchStops)) return;

                var hex = CombatPtr.Normalize(ownerKey);
                var follow = AnchorResolver.Resolve(hex);
                if (follow == null) return;

                Vector3 world;
                try { world = BarAnchorWorld(follow); }
                catch { return; }

                var trueRatio = Mathf.Clamp01((float)hp / max);
                var displayRatio = ShieldBarVisual.DisplayRatio(hp, max);
                var key = StatApplyScope.Normalize(ownerKey);
                SeenThisTick.Add(key);
                if (!ApplySlot(key, world, displayRatio, ScratchStops, ScratchStacks.Count, mat))
                    return;
                live++;
                displaySum += displayRatio;
                trueSum += trueRatio;
            }
            catch { }
        });

        // Release owners that vanished this tick.
        for (var i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            if (!slot.Live) continue;
            if (SeenThisTick.Contains(slot.OwnerKey)) continue;
            Release(slot);
        }

        _worldBars = live;
        _lastAvgRatio = live > 0 ? displaySum / live : 0f;
        _lastAvgTrueRatio = live > 0 ? trueSum / live : 0f;
        _lastEarly = live > 0 ? "ok" : "no-body";
    }

    public static void StopAll()
    {
        foreach (var s in Slots) Release(s);
        _worldBars = 0;
        _lastAvgRatio = 0f;
        _lastAvgTrueRatio = 0f;
    }

    static bool ApplySlot(
        string ownerKey,
        Vector3 world,
        float ratio,
        List<ShieldBarColor.Stop> stops,
        int stackCount,
        Material mat)
    {
        var slot = FindLive(ownerKey) ?? TakeIdle() ?? CreateSlot();
        if (slot?.Root == null) return false;

        slot.OwnerKey = ownerKey;
        slot.Live = true;
        try
        {
            slot.Root.SetActive(true);
            slot.Root.transform.position = world;
        }
        catch { return false; }

        // Track = full max width, centered on root (root is sprite-centered).
        PlaceRenderer(slot.Track, -BarWorldWidth * 0.5f, BarWorldWidth, BarWorldHeight, mat,
            new Color(0.08f, 0.08f, 0.1f, 0.85f));

        var fillW = BarWorldWidth * ratio;
        var x = -BarWorldWidth * 0.5f;
        for (var i = 0; i < MaxSegments; i++)
        {
            var seg = slot.Segments[i];
            if (i >= stops.Count || fillW < 0.01f)
            {
                SetActive(seg, false);
                continue;
            }

            var stop = stops[i];
            var w = fillW * Math.Max(0f, stop.EndU - stop.StartU);
            if (w < 0.01f)
            {
                SetActive(seg, false);
                continue;
            }

            PlaceRenderer(seg, x, w, BarWorldHeight * 0.85f, mat,
                new Color(stop.R / 255f, stop.G / 255f, stop.B / 255f, 0.95f));
            x += w;
        }

        // Stack pips — small soft-disc quads above the bar (not GUI text).
        var pipN = Math.Clamp(stackCount, 0, MaxPips);
        var pipY = BarWorldHeight * 0.9f;
        var pipStart = -((pipN - 1) * 0.12f) * 0.5f;
        for (var i = 0; i < MaxPips; i++)
        {
            var pip = slot.Pips[i];
            if (pip == null) continue;
            if (i >= pipN)
            {
                try { pip.SetActive(false); } catch { }
                continue;
            }

            try
            {
                pip.SetActive(true);
                pip.transform.localPosition = new Vector3(pipStart + i * 0.12f, pipY, 0f);
                pip.transform.localScale = new Vector3(0.08f, 0.08f, 1f);
                var mr = pip.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var rgb = i < stops.Count
                        ? (stops[i].R, stops[i].G, stops[i].B)
                        : ShieldBarColor.UntypedRgb;
                    Tint(mr, mat, new Color(rgb.Item1 / 255f, rgb.Item2 / 255f, rgb.Item3 / 255f, 0.95f));
                }
            }
            catch { }
        }

        return true;
    }

    /// <summary>
    /// Lane Y from BodyWorld; X from sprite renderer bounds center.
    /// Zombie/plant pivots are often left of the art — transform.x alone mis-centers the bar.
    /// </summary>
    static Vector3 BarAnchorWorld(Transform follow)
    {
        var body = LawnCoords.BodyWorld(follow);
        var x = body.x;
        try
        {
            var r = follow.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                var b = r.bounds;
                if (b.size.x > 0.01f)
                    x = b.center.x;
            }
        }
        catch { }

        return new Vector3(x, body.y + WorldYOffset, body.z);
    }

    static void PlaceRenderer(
        MeshRenderer? mr,
        float localX,
        float width,
        float height,
        Material mat,
        Color tint)
    {
        if (mr == null) return;
        try
        {
            mr.gameObject.SetActive(true);
            var t = mr.transform;
            // Local space under root: root is already at the sprite center.
            t.localPosition = new Vector3(localX + width * 0.5f, 0f, 0f);
            t.localScale = new Vector3(width, height, 1f);
            Tint(mr, mat, tint);
        }
        catch { }
    }

    static void Tint(MeshRenderer mr, Material mat, Color tint)
    {
        try
        {
            if (mr.sharedMaterial != mat) mr.sharedMaterial = mat;
            Block.Clear();
            Block.SetColor("_Color", tint);
            try { Block.SetColor("_TintColor", tint); } catch { }
            mr.SetPropertyBlock(Block);
            try { mr.sortingOrder = FxResources.ParticleSortingOrder; } catch { }
        }
        catch { }
    }

    static void SetActive(MeshRenderer? mr, bool on)
    {
        if (mr == null) return;
        try { mr.gameObject.SetActive(on); } catch { }
    }

    static Slot? FindLive(string ownerKey)
    {
        for (var i = 0; i < Slots.Count; i++)
        {
            var s = Slots[i];
            if (s.Live && string.Equals(s.OwnerKey, ownerKey, StringComparison.Ordinal))
                return s;
        }

        return null;
    }

    static Slot? TakeIdle()
    {
        for (var i = 0; i < Slots.Count; i++)
        {
            var s = Slots[i];
            if (!s.Live && s.Root != null) return s;
        }

        return null;
    }

    static void Release(Slot slot)
    {
        slot.Live = false;
        slot.OwnerKey = "";
        try { if (slot.Root != null) slot.Root.SetActive(false); } catch { }
    }

    static Slot? CreateSlot()
    {
        if (Slots.Count >= Cap) return null;
        var mat = EnsureBarMaterial();
        if (mat == null) return null;
        var mesh = EnsureQuad();
        if (mesh == null) return null;

        try
        {
            var root = new GameObject("FusionRpgShieldBar");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            var slot = new Slot { Root = root };
            slot.Track = MakeQuadChild(root, "track", mesh, mat);
            for (var i = 0; i < MaxSegments; i++)
                slot.Segments[i] = MakeQuadChild(root, "seg" + i, mesh, mat);
            for (var i = 0; i < MaxPips; i++)
            {
                var pip = new GameObject("pip" + i);
                pip.hideFlags = HideFlags.HideAndDontSave;
                pip.transform.SetParent(root.transform, false);
                var filter = pip.AddComponent<MeshFilter>();
                var mr = pip.AddComponent<MeshRenderer>();
                if (filter != null) filter.sharedMesh = mesh;
                if (mr != null)
                {
                    mr.sharedMaterial = mat;
                    try { mr.sortingOrder = FxResources.ParticleSortingOrder + 1; } catch { }
                }

                pip.SetActive(false);
                slot.Pips[i] = pip;
            }

            Slots.Add(slot);
            return slot;
        }
        catch (Exception ex)
        {
            try { RpgHost.Log.Warning("[vfx.shield-bar] create: " + ex.Message); } catch { }
            return null;
        }
    }

    static MeshRenderer? MakeQuadChild(GameObject root, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(root.transform, false);
        var filter = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        if (filter != null) filter.sharedMesh = mesh;
        if (mr != null)
        {
            mr.sharedMaterial = mat;
            try { mr.sortingOrder = FxResources.ParticleSortingOrder; } catch { }
        }

        go.SetActive(false);
        return mr;
    }

    static Mesh? EnsureQuad()
    {
        if (_quad != null) return _quad;
        try
        {
            var m = new Mesh { name = "FusionRpgShieldBarQuad", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            _quad = m;
            return m;
        }
        catch
        {
            return null;
        }
    }

    static Material? EnsureBarMaterial()
    {
        var baseMat = FxResources.ParticleMaterial();
        if (baseMat == null) return null;
        var shaderName = OverlayShaderProbe.DrawShaderName() ?? "";
        if (_barMat != null && string.Equals(_barMatShader, shaderName, StringComparison.Ordinal))
            return _barMat;
        try
        {
            var mat = new Material(baseMat)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = SolidWhite(),
                name = "FusionRpgShieldBarMat"
            };
            _barMat = mat;
            _barMatShader = shaderName;
            return mat;
        }
        catch
        {
            return baseMat;
        }
    }

    static Texture2D SolidWhite()
    {
        if (_solid != null) return _solid;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "FusionRpgShieldBarSolid"
        };
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply(false, true);
        _solid = tex;
        return tex;
    }
}
