using FusionRpg.Core.Combat;
using FusionRpg.Core.Hud;
using FusionRpg.Core.Vfx;
using FusionRpg.Injector.Effects;
using FusionRpg.Injector.Fx;
using FusionRpg.Injector.Host;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Hud;

/// <summary>
/// World-space Band B HUD — reads <see cref="ActorHudCache"/> snapshots only (actor-hud-unity spec).
/// Placement via <see cref="UnitFrameResolver"/> crown anchor.
/// </summary>
public static class ActorHudPool
{
    // Structural pool cap — at capacity CreateSlot returns null and HUD is omitted (no eviction).
    const int Cap = 96;
    const int MaxShieldSegments = 4;
    const int MaxStatusTokens = 3;

    static readonly HashSet<string> SeenThisTick = new(StringComparer.Ordinal);
    static readonly List<HudSlot> Slots = new();
    static readonly MaterialPropertyBlock Block = new();

    static Mesh? _quad;
    static Texture2D? _solid;
    static Material? _mat;
    static string _matShader = "";
    static int _worldHud;
    static int _shieldBarsDrawn;
    static float _lastAvgRatio;
    static float _lastAvgTrueRatio;
    static bool _shaderOk;
    static string _lastEarly = "never-synced";

    public static int WorldBars => _worldHud;
    public static int ShieldBarsDrawn => _shieldBarsDrawn;
    public static bool ShaderOk => _shaderOk;
    public static float LastAvgRatio => _lastAvgRatio;
    public static float LastAvgTrueRatio => _lastAvgTrueRatio;
    public static string LastEarly => _lastEarly;

    public sealed class HudSlot
    {
        public string OwnerKey = "";
        public GameObject? Root;
        public MeshRenderer? TierFrame;
        public MeshRenderer? LevelBadge;
        public MeshRenderer? RolePip;
        public MeshRenderer? ShieldTrack;
        public readonly MeshRenderer?[] ShieldSegments = new MeshRenderer?[MaxShieldSegments];
        public readonly MeshRenderer?[] StatusTokens = new MeshRenderer?[MaxStatusTokens];
        public MeshRenderer? OverflowPip;
        public bool Live;
    }

    public static void TickSync()
    {
        SeenThisTick.Clear();
        _worldHud = 0;
        _shieldBarsDrawn = 0;
        var displaySum = 0f;
        var trueSum = 0f;

        if (!OverlaySettings.ShieldBarEnabled)
            _lastEarly = "disabled";

        ActorHudTuning? tuning = null;
        try { tuning = ActorHudTuningHub.Tuning; }
        catch { }

        Material? mat = null;
        if (tuning != null)
        {
            try { mat = EnsureMaterial(); }
            catch { }
        }

        if (mat == null)
        {
            _shaderOk = false;
            if (OverlaySettings.ShieldBarEnabled)
                _lastEarly = "no-shader";
        }
        else
        {
            _shaderOk = true;
            try
            {
                InjectorEntityRegistry.Resync(UnityEngine.Time.frameCount);
                InjectorEntityRegistry.VisitPlants(p =>
                    SyncEntity(p.Pointer.ToString("X"), mat, tuning!, ref displaySum, ref trueSum));
                InjectorEntityRegistry.VisitZombies(z =>
                    SyncEntity(z.Pointer.ToString("X"), mat, tuning!, ref displaySum, ref trueSum));
            }
            catch { }
        }

        for (var i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            if (!slot.Live) continue;
            if (SeenThisTick.Contains(slot.OwnerKey)) continue;
            Release(slot);
        }

        _lastAvgRatio = _shieldBarsDrawn > 0 ? displaySum / _shieldBarsDrawn : 0f;
        _lastAvgTrueRatio = _shieldBarsDrawn > 0 ? trueSum / _shieldBarsDrawn : 0f;
        if (OverlaySettings.ShieldBarEnabled && _shaderOk)
            _lastEarly = _shieldBarsDrawn > 0 ? "ok" : "no-body";
    }

    public static void StopAll()
    {
        foreach (var s in Slots) Release(s);
        _worldHud = 0;
        _shieldBarsDrawn = 0;
        _lastAvgRatio = 0f;
        _lastAvgTrueRatio = 0f;
        _lastEarly = "never-synced";
    }

    /// <summary>Immediate slot release on entity die — avoids one-frame ghost HUD.</summary>
    public static void ReleaseOwner(string? ptrHex)
    {
        var key = CombatPtr.Normalize(ptrHex);
        if (string.IsNullOrEmpty(key)) return;
        var slot = FindLive(key);
        if (slot != null) Release(slot);
    }

    static void SyncEntity(string ptrHex, Material mat, ActorHudTuning tuning, ref float displaySum, ref float trueSum)
    {
        ActorHudSnapshot? snapshot;
        try { snapshot = ActorHudCache.GetOrBuild(ptrHex); }
        catch { return; }

        if (snapshot == null || !ShouldShow(snapshot))
            return;

        var key = CombatPtr.Normalize(ptrHex);
        if (string.IsNullOrEmpty(key)) return;

        Transform? follow;
        try { follow = AnchorResolver.Resolve(key); }
        catch { return; }
        if (follow == null) return;

        VfxUnitFrame frame;
        try { frame = UnitFrameResolver.Resolve(follow); }
        catch { return; }

        var crown = frame.World(VfxAnchorKind.Crown);
        var span = frame.Span();
        var barW = Mathf.Clamp(span * 0.55f, 0.35f, 1.2f);
        var barH = Mathf.Clamp(span * 0.06f, 0.04f, 0.12f);

        var slot = FindLive(key) ?? TakeIdle() ?? CreateSlot();
        if (slot?.Root == null) return;

        slot.OwnerKey = key;
        slot.Live = true;
        SeenThisTick.Add(key);

        try
        {
            slot.Root.SetActive(true);
            slot.Root.transform.position = crown;
        }
        catch { return; }

        var yIdentity = span * (float)tuning.RowOffsetIdentity;
        var yResources = span * (float)tuning.RowOffsetResources;
        var yStatuses = span * (float)tuning.RowOffsetStatuses;

        SetRowLocalY(slot.TierFrame, yIdentity);
        SetRowLocalY(slot.LevelBadge, yIdentity);
        SetRowLocalY(slot.RolePip, yIdentity);
        SetRowLocalY(slot.ShieldTrack, yResources);
        for (var i = 0; i < slot.ShieldSegments.Length; i++)
            SetRowLocalY(slot.ShieldSegments[i], yResources);
        for (var i = 0; i < slot.StatusTokens.Length; i++)
            SetRowLocalY(slot.StatusTokens[i], yStatuses);
        SetRowLocalY(slot.OverflowPip, yStatuses);

        ActorHudRowIdentity.Sync(slot, snapshot.Identity, mat, span);
        if (ActorHudRowResources.Sync(slot, snapshot.Resources?.Shield, mat, barW, barH))
        {
            _shieldBarsDrawn++;
            var shield = snapshot.Resources!.Shield!;
            if (shield.Max > 0)
            {
                displaySum += ShieldBarVisual.DisplayRatio(shield.Hp, shield.Max);
                trueSum += Mathf.Clamp01((float)shield.Hp / shield.Max);
            }
        }
        ActorHudRowStatuses.Sync(
            slot,
            snapshot.Statuses,
            snapshot.Overflow.StatusCount,
            mat,
            span,
            tuning.StatusStripMax);

        _worldHud++;
    }

    static bool ShouldShow(ActorHudSnapshot s) =>
        s.Identity.Tier != ActorHudTier.Normal
        || s.Identity.LevelBand is not null
        || s.Statuses.Count > 0
        || s.Overflow.StatusCount > 0
        || (OverlaySettings.ShieldBarEnabled
            && s.Resources?.Shield is { Max: > 0, Hp: > 0 });

    static void SetRowLocalY(MeshRenderer? mr, float y)
    {
        if (mr == null) return;
        try
        {
            var t = mr.transform;
            var p = t.localPosition;
            t.localPosition = new Vector3(p.x, y, p.z);
        }
        catch { }
    }

    internal static void PlaceQuad(
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
            t.localPosition = new Vector3(localX + width * 0.5f, t.localPosition.y, 0f);
            t.localScale = new Vector3(width, height, 1f);
            if (mr.sharedMaterial != mat) mr.sharedMaterial = mat;
            Block.Clear();
            Block.SetColor("_Color", tint);
            try { Block.SetColor("_TintColor", tint); } catch { }
            mr.SetPropertyBlock(Block);
            try { mr.sortingOrder = Fx.FxResources.ParticleSortingOrder + 2; } catch { }
        }
        catch { }
    }

    static HudSlot? FindLive(string ownerKey)
    {
        for (var i = 0; i < Slots.Count; i++)
        {
            var s = Slots[i];
            if (s.Live && string.Equals(s.OwnerKey, ownerKey, StringComparison.Ordinal))
                return s;
        }

        return null;
    }

    static HudSlot? TakeIdle()
    {
        for (var i = 0; i < Slots.Count; i++)
        {
            var s = Slots[i];
            if (!s.Live && s.Root != null) return s;
        }

        return null;
    }

    static void Release(HudSlot slot)
    {
        slot.Live = false;
        slot.OwnerKey = "";
        try { if (slot.Root != null) slot.Root.SetActive(false); } catch { }
    }

    static HudSlot? CreateSlot()
    {
        if (Slots.Count >= Cap) return null;
        var mat = EnsureMaterial();
        var mesh = EnsureQuad();
        if (mat == null || mesh == null) return null;

        try
        {
            var root = new GameObject("FusionRpgActorHud");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            var slot = new HudSlot { Root = root };
            slot.TierFrame = MakeChild(root, "tier", mesh, mat);
            slot.LevelBadge = MakeChild(root, "lvl", mesh, mat);
            slot.RolePip = MakeChild(root, "role", mesh, mat);
            slot.ShieldTrack = MakeChild(root, "shieldTrack", mesh, mat);
            for (var i = 0; i < MaxShieldSegments; i++)
                slot.ShieldSegments[i] = MakeChild(root, "shieldSeg" + i, mesh, mat);
            for (var i = 0; i < MaxStatusTokens; i++)
                slot.StatusTokens[i] = MakeChild(root, "status" + i, mesh, mat);
            slot.OverflowPip = MakeChild(root, "overflow", mesh, mat);

            Slots.Add(slot);
            return slot;
        }
        catch (Exception ex)
        {
            try { RpgHost.Log.Warning("[actor-hud] create: " + ex.Message); } catch { }
            return null;
        }
    }

    static MeshRenderer? MakeChild(GameObject root, string name, Mesh mesh, Material mat)
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
            try { mr.sortingOrder = Fx.FxResources.ParticleSortingOrder + 2; } catch { }
        }

        go.SetActive(false);
        return mr;
    }

    static Mesh? EnsureQuad()
    {
        if (_quad != null) return _quad;
        try
        {
            var m = new Mesh { name = "FusionRpgActorHudQuad", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            _quad = m;
            return m;
        }
        catch { return null; }
    }

    static Material? EnsureMaterial()
    {
        var baseMat = Fx.FxResources.ParticleMaterial();
        if (baseMat == null) return null;
        var shaderName = OverlayShaderProbe.DrawShaderName() ?? "";
        if (_mat != null && string.Equals(_matShader, shaderName, StringComparison.Ordinal))
            return _mat;
        try
        {
            _mat = new Material(baseMat)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = SolidWhite(),
                name = "FusionRpgActorHudMat"
            };
            _matShader = shaderName;
            return _mat;
        }
        catch { return null; }
    }

    static Texture2D SolidWhite()
    {
        if (_solid != null) return _solid;
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "FusionRpgActorHudSolid"
        };
        t.SetPixel(0, 0, Color.white);
        t.Apply(false, true);
        _solid = t;
        return t;
    }
}
