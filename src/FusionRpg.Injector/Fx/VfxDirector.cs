using FusionRpg.Contracts;
using FusionRpg.Core.Vfx;
using FusionRpg.Injector.Host;
using FusionRpg.Injector.Lawn;
using UnityEngine;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// The single production IVfxSink — vfx-ssot.md §8. Play() only enqueues and is callable from any
/// thread; all Unity work happens in Tick/Draw on the main thread. Hosts call exactly Tick + Draw.
/// Never throws into the loop; every skip emits debug.fx.skipped with an enumerated reason.
/// </summary>
public static class VfxDirector
{
    static readonly object Gate = new();
    static readonly Queue<VfxCueDto> Pending = new();
    static readonly HashSet<string> Muted = new(StringComparer.OrdinalIgnoreCase);
    static readonly VfxCatalog Catalog = CreateCatalog();
    static readonly VfxAdmission Admission = new(Catalog);
    static readonly List<Floater> Floaters = new();
    static readonly List<Flash> Flashes = new();
    static double _now;
    static Camera? _cam;

    public static readonly IVfxSink Sink = new SinkAdapter();

    sealed class SinkAdapter : IVfxSink
    {
        public void Play(VfxCueDto cue) => VfxDirector.Play(cue);
    }

    static VfxCatalog CreateCatalog()
    {
        var c = new VfxCatalog();
        c.ReplaceAll(VfxSeedCatalog.CreateAll());
        return c;
    }

    public static IReadOnlyList<string> CueIds => Catalog.Ids;

    public static IReadOnlyList<string> MutedIds
    {
        get
        {
            lock (Gate) return Muted.OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    public static void SetMuted(string cueId, bool muted)
    {
        if (string.IsNullOrWhiteSpace(cueId)) return;
        lock (Gate)
        {
            if (muted) Muted.Add(cueId);
            else Muted.Remove(cueId);
        }
    }

    /// <summary>Thread-safe enqueue only — no Unity API calls here (vfx-ssot.md §5).</summary>
    public static void Play(VfxCueDto? cue)
    {
        if (cue == null) return;
        lock (Gate)
        {
            while (Pending.Count >= VfxRules.CueQueueCap) Pending.Dequeue();
            Pending.Enqueue(cue);
        }
    }

    public static void Tick(float dt)
    {
        if (dt < 0f) dt = 0f;
        _now += dt;
        // Idle early-out: with nothing queued or live the frame costs one lock + three counts.
        bool queued;
        lock (Gate) queued = Pending.Count > 0;
        if (!queued && Floaters.Count == 0 && Flashes.Count == 0 && BurstPool.LiveCount() == 0)
            return;
        DrainQueue();
        TickFloaters(dt);
        BurstPool.Tick(dt);
        TickFlashes(dt);
    }

    /// <summary>Match end / scene teardown — vfx-ssot.md §8.1.</summary>
    public static void ClearAll()
    {
        lock (Gate) Pending.Clear();
        Floaters.Clear();
        RestoreAndClearFlashes();
        BurstPool.StopAll();
        Admission.Clear();
    }

    static void DrainQueue()
    {
        List<VfxCueDto>? drained = null;
        lock (Gate)
        {
            if (Pending.Count > 0)
            {
                drained = new List<VfxCueDto>(Pending);
                Pending.Clear();
            }
        }

        if (drained == null) return;
        Admission.BeginTick(_now);
        var master = CheatState.On("SYS-DAMAGE-FX");
        var elementOn = CheatState.On("SYS-ELEMENT-FX");
        foreach (var cue in drained)
        {
            try
            {
                Spawn(cue, master, elementOn);
            }
            catch (Exception ex)
            {
                EmitSkipped(cue, VfxSkipReasons.ParticleFail);
                try { RpgHost.Log.Warning("[vfx] spawn: " + ex.Message); } catch { }
            }
        }
    }

    static void Spawn(VfxCueDto cue, bool master, bool elementOn)
    {
        bool IsMuted(string id)
        {
            lock (Gate) return Muted.Contains(id);
        }

        var decision = Admission.Decide(cue, _now, master, IsMuted);
        if (!decision.Admitted)
        {
            EmitSkipped(cue, decision.Reason);
            return;
        }

        var plan = VfxColorPlan.For(cue.Tag, cue.Elements, elementOn, cue.Amount);

        Transform? follow = null;
        var world = Vector3.zero;
        var hasWorld = false;
        int sizeCol = CheatState.SpawnCol, sizeRow = CheatState.SpawnRow;
        if (!string.IsNullOrWhiteSpace(cue.TargetPtr))
        {
            follow = AnchorResolver.Resolve(cue.TargetPtr);
            if (follow == null)
            {
                EmitSkipped(cue, VfxSkipReasons.Missing);
                return;
            }

            try
            {
                world = LawnCoords.BodyWorld(follow);
                hasWorld = true;
            }
            catch { }
        }
        else if (cue.Col.HasValue && cue.Row.HasValue)
        {
            sizeCol = LawnCoords.ClampCol(cue.Col.Value);
            sizeRow = LawnCoords.ClampRow(cue.Row.Value);
            try
            {
                var c = LawnCoords.CellCenter(sizeCol, sizeRow);
                world = new Vector3(c.x, c.y, 0f);
                hasWorld = true;
            }
            catch
            {
                EmitSkipped(cue, VfxSkipReasons.Missing);
                return;
            }
        }
        else if (cue.WorldX.HasValue && cue.WorldY.HasValue)
        {
            world = new Vector3(cue.WorldX.Value, cue.WorldY.Value, 0f);
            hasWorld = true;
        }
        else
        {
            EmitSkipped(cue, VfxSkipReasons.Missing);
            return;
        }

        var cellSize = EstimateCellSize(sizeCol, sizeRow);
        var kinds = new List<string>(decision.SpecIndices.Count);
        string? failReason = null;
        var elementGated = false;
        foreach (var idx in decision.SpecIndices)
        {
            var spec = decision.Recipe!.Primitives[idx];
            if (spec.RequireElement && !plan.ElementColored)
            {
                elementGated = true;
                continue;
            }
            switch (spec.Kind)
            {
                case VfxPrimitiveKind.Floater:
                    if (follow == null || spec.Label == VfxLabelSourceKind.None) break;
                    SpawnFloater(cue, plan, spec, follow);
                    kinds.Add("floater");
                    break;
                case VfxPrimitiveKind.Burst:
                    if (!hasWorld) break;
                    if (BurstPool.Spawn(world, plan, spec, cellSize, cue.LifeMul, cue.ScaleMul, out var reason))
                        kinds.Add("burst");
                    else
                        failReason = reason;
                    break;
                case VfxPrimitiveKind.Flash:
                    if (follow == null) break;
                    if (SpawnFlash(follow, plan, spec)) kinds.Add("flash");
                    break;
            }
        }

        if (kinds.Count == 0)
        {
            EmitSkipped(cue, failReason ?? (elementGated ? VfxSkipReasons.NoElement : VfxSkipReasons.ParticleFail));
            return;
        }

        EmitShown(cue, plan, kinds);
    }

    static void SpawnFloater(VfxCueDto cue, VfxColorPlan plan, VfxPrimitiveSpec spec, Transform follow)
    {
        while (Floaters.Count >= VfxRules.FloaterCap) Floaters.RemoveAt(0);
        var label = spec.Label == VfxLabelSourceKind.Fixed ? spec.FixedLabel : plan.Label;
        Floaters.Add(new Floater
        {
            Plan = plan,
            Label = label,
            Follow = follow,
            Age = -Math.Max(0f, spec.DelaySeconds),
            Life = spec.LifeSeconds * (cue.LifeMul > 0f ? cue.LifeMul : 1f)
        });
    }

    static bool SpawnFlash(Transform follow, VfxColorPlan plan, VfxPrimitiveSpec spec)
    {
        try
        {
            var sr = follow.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return false;
            foreach (var f in Flashes)
            {
                // Re-trigger resets the timer but keeps the originally captured color — §8.5.
                if (f.Sr == sr)
                {
                    f.Age = 0f;
                    f.Life = spec.LifeSeconds;
                    return true;
                }
            }

            var rgb = spec.Color == VfxColorSourceKind.Fixed ? spec.FixedRgb : plan.Rgb;
            var original = sr.color;
            var tint = Color.Lerp(original, new Color(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f, original.a), 0.65f);
            sr.color = tint;
            Flashes.Add(new Flash { Sr = sr, Original = original, Age = 0f, Life = spec.LifeSeconds });
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void TickFloaters(float dt)
    {
        for (var i = Floaters.Count - 1; i >= 0; i--)
        {
            var f = Floaters[i];
            f.Age += dt;
            if (f.Age >= f.Life || f.Follow == null)
                Floaters.RemoveAt(i);
        }
    }

    static void TickFlashes(float dt)
    {
        for (var i = Flashes.Count - 1; i >= 0; i--)
        {
            var f = Flashes[i];
            f.Age += dt;
            if (f.Age < f.Life) continue;
            RestoreFlash(f);
            Flashes.RemoveAt(i);
        }
    }

    static void RestoreAndClearFlashes()
    {
        foreach (var f in Flashes) RestoreFlash(f);
        Flashes.Clear();
    }

    static void RestoreFlash(Flash f)
    {
        try
        {
            if (f.Sr != null) f.Sr.color = f.Original;
        }
        catch { }
    }

    public static void Draw()
    {
        try
        {
            var e = Event.current;
            if (e == null || e.type != EventType.Repaint) return;
        }
        catch
        {
            return;
        }

        if (Floaters.Count == 0) return;
        // Camera resolves only here — with live floaters, on Repaint — never in Tick.
        // Re-resolve when the cached camera is destroyed OR merely disabled: scene switches
        // often disable the old MainCamera without destroying it, and Camera.main tracks
        // the enabled one. Stale-but-alive must not stick.
        var stale = _cam == null;
        if (!stale)
        {
            try { stale = !_cam!.isActiveAndEnabled; } catch { stale = true; }
        }
        if (stale)
        {
            try { _cam = Camera.main; } catch { _cam = null; }
        }
        var cam = _cam;
        if (cam == null) return;

        // IL2CPP interop has no GUIStyle copy ctor — mutate the shared label style, restore after.
        var skin = GUI.skin;
        if (skin == null) return;
        var style = skin.label;
        var oldSize = style.fontSize;
        var oldFontStyle = style.fontStyle;
        var oldAlign = style.alignment;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        try
        {
            foreach (var f in Floaters)
            {
                if (f.Follow == null || f.Age < 0f) continue;
                Vector3 world;
                try { world = LawnCoords.BodyWorld(f.Follow); }
                catch { continue; }
                var t = f.Life > 0f ? Mathf.Clamp01(f.Age / f.Life) : 1f;
                if (!LawnCoords.TryWorldToGui(cam, world, t, out var gui)) continue;

                var alpha = Core.Effects.DamageFxFloaterRules.Alpha(t);
                style.fontSize = (int)Math.Round(20f * f.Plan.FontScaleAt(t));
                var rect = new Rect(gui.x - 80f, gui.y - 16f, 160f, 32f);
                // Shadow pass first — readability on bright lawns (SPEC W3).
                GUI.color = new Color(0f, 0f, 0f, alpha);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), f.Label, style);
                var rgb = f.Plan.ColorAt(t);
                GUI.color = new Color(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f, alpha);
                GUI.Label(rect, f.Label, style);
            }
        }
        finally
        {
            style.fontSize = oldSize;
            style.fontStyle = oldFontStyle;
            style.alignment = oldAlign;
            GUI.color = Color.white;
        }
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

    static void EmitShown(VfxCueDto cue, VfxColorPlan plan, List<string> kinds)
    {
        DebugRuntime.Emit("debug.fx.shown", new Dictionary<string, object>
        {
            ["cueId"] = cue.CueId,
            ["ptr"] = cue.TargetPtr ?? "",
            ["col"] = cue.Col ?? -1,
            ["row"] = cue.Row ?? -1,
            ["amount"] = cue.Amount,
            ["tag"] = cue.Tag?.ToString() ?? "",
            ["label"] = plan.Label,
            ["rgb"] = RgbHex(plan.Rgb),
            ["hybrid"] = plan.Hybrid,
            ["elements"] = cue.Elements?.Count ?? 0,
            ["primitives"] = kinds,
            ["floaters"] = Floaters.Count,
            ["bursts"] = BurstPool.LiveCount()
        });
    }

    static void EmitSkipped(VfxCueDto cue, string reason)
    {
        DebugRuntime.Emit("debug.fx.skipped", new Dictionary<string, object>
        {
            ["cueId"] = cue.CueId,
            ["ptr"] = cue.TargetPtr ?? "",
            ["col"] = cue.Col ?? -1,
            ["row"] = cue.Row ?? -1,
            ["amount"] = cue.Amount,
            ["tag"] = cue.Tag?.ToString() ?? "",
            ["reason"] = reason
        });
    }

    static string RgbHex((byte R, byte G, byte B) c) =>
        "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

    sealed class Floater
    {
        public VfxColorPlan Plan = new();
        public string Label = "";
        public Transform? Follow;
        public float Age;
        public float Life = VfxRules.FloaterLifeSeconds;
    }

    sealed class Flash
    {
        public SpriteRenderer? Sr;
        public Color Original;
        public float Age;
        public float Life;
    }
}
