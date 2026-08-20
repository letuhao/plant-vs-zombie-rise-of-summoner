using FusionRpg.Core.Vfx;
using FusionRpg.Injector.Host;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Pooled additive particle bursts — vfx-ssot.md §8.4. Fixed pool of <see cref="VfxRules.BurstCap"/>
/// ParticleSystems reused via Clear+Emit; no per-burst instantiate/destroy. Slot stealing preserves
/// the drop-oldest policy. Scene unload may kill pool objects; dead slots rebuild lazily.
/// </summary>
static class BurstPool
{
    const int BurstParticles = 28;

    sealed class Slot
    {
        public GameObject? Go;
        public ParticleSystem? Ps;
        public float Age;
        public float Life;
        public bool Live;
    }

    static readonly List<Slot> Slots = new();

    /// <summary>Spawn a burst; false with a reason when resources or the pool fail.</summary>
    public static bool Spawn(
        Vector3 world,
        VfxColorPlan plan,
        VfxPrimitiveSpec spec,
        Vector2 cellSize,
        float lifeMul,
        float scaleMul,
        out string failReason)
    {
        failReason = "";
        var mat = FxResources.ParticleMaterial();
        if (mat == null)
        {
            failReason = VfxSkipReasons.NoShader;
            return false;
        }

        var slot = TakeSlot(mat);
        if (slot?.Ps == null)
        {
            failReason = VfxSkipReasons.ParticleFail;
            return false;
        }

        var life = spec.LifeSeconds * (lifeMul > 0f ? lifeMul : 1f);
        try
        {
            slot.Go!.transform.position = world;
            var ps = slot.Ps;
            try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
            try
            {
                var main = ps.main;
                main.startLifetime = life;
            }
            catch { }
            try { ps.Play(); } catch { }

            var span = Mathf.Max(0.35f, Mathf.Min(cellSize.x, cellSize.y)) *
                       (spec.SizeScale > 0f ? spec.SizeScale : 1f) *
                       (scaleMul > 0f ? scaleMul : 1f);
            var count = Math.Min(spec.Count > 1 ? spec.Count : BurstParticles, 64);
            var fixedColor = spec.Color == VfxColorSourceKind.Fixed;
            var flat = fixedColor ? spec.FixedRgb : plan.BurstRgb;
            for (var i = 0; i < count; i++)
            {
                var rgb = !fixedColor && plan.Hybrid
                    ? ElementFxPalette.ParticleColor(plan.HybridComponents, i, count)
                    : flat;
                var c32 = new Color32(rgb.R, rgb.G, rgb.B, 242);
                var ang = i / (float)count * Mathf.PI * 2f;
                var rad = span * (0.04f + 0.12f * (i % 5) / 5f);
                var pos = new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, 0f);
                var speed = span * (0.8f + i % 7 * 0.18f);
                var vel = new Vector3(Mathf.Cos(ang) * speed, Mathf.Sin(ang) * speed, 0f);
                var size = span * (0.16f + i % 4 * 0.08f);
                var energy = life * (0.55f + i % 5 * 0.08f);
                try { ps.Emit(pos, vel, size, energy, c32); }
                catch { break; }
            }

            slot.Age = 0f;
            slot.Life = life + 0.2f;
            slot.Live = true;
            return true;
        }
        catch (Exception ex)
        {
            try { RpgHost.Log.Warning("[vfx.burst] " + ex.Message); } catch { }
            failReason = VfxSkipReasons.ParticleFail;
            slot.Live = false;
            return false;
        }
    }

    public static void Tick(float dt)
    {
        if (dt < 0f) dt = 0f;
        foreach (var s in Slots)
        {
            if (!s.Live) continue;
            s.Age += dt;
            if (s.Age < s.Life && s.Go != null) continue;
            Quiet(s);
        }
    }

    public static void StopAll()
    {
        foreach (var s in Slots) Quiet(s);
    }

    public static int LiveCount()
    {
        var n = 0;
        foreach (var s in Slots)
        {
            if (s.Live) n++;
        }

        return n;
    }

    static void Quiet(Slot s)
    {
        s.Live = false;
        s.Age = 0f;
        try
        {
            if (s.Ps != null) s.Ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        catch { }
    }

    static Slot? TakeSlot(Material mat)
    {
        // Prune slots whose objects a scene unload destroyed; they rebuild below.
        Slots.RemoveAll(s => s.Go == null && (s.Live || Slots.Count >= VfxRules.BurstCap));

        foreach (var s in Slots)
        {
            if (!s.Live && s.Go != null) return s;
        }

        if (Slots.Count < VfxRules.BurstCap)
        {
            var created = CreateSlot(mat);
            if (created != null) Slots.Add(created);
            return created;
        }

        // Pool exhausted: steal the oldest live slot (drop-oldest, vfx-ssot.md §7).
        Slot? oldest = null;
        foreach (var s in Slots)
        {
            if (s.Go == null) continue;
            if (oldest == null || s.Age > oldest.Age) oldest = s;
        }

        if (oldest != null) Quiet(oldest);
        return oldest;
    }

    static Slot? CreateSlot(Material mat)
    {
        try
        {
            var go = new GameObject("FusionRpgVfxBurst");
            go.hideFlags = HideFlags.HideAndDontSave;
            var ps = go.AddComponent<ParticleSystem>();
            if (ps == null)
            {
                UObject.Destroy(go);
                return null;
            }

            try { ps.playOnAwake = false; } catch { }
            try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
            try
            {
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = mat;
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.sortingOrder = FxResources.ParticleSortingOrder;
                }
            }
            catch { }

            return new Slot { Go = go, Ps = ps };
        }
        catch (Exception ex)
        {
            try { RpgHost.Log.Warning("[vfx.burst] create: " + ex.Message); } catch { }
            return null;
        }
    }
}
