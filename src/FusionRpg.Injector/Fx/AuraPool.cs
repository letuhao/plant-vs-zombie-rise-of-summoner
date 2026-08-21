using FusionRpg.Core.Vfx;
using FusionRpg.Injector.Host;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Fx;

/// <summary>
/// Leased looping particle systems for sustained auras and markers — vfx-v3 M3/M5.
/// Pool cap = tracker cap (a live set always gets a slot). Every LIVE lesson applies:
/// emission module disabled, explicit per-particle colors, pulsed manual emission
/// (≤ AuraMaxParticles live per lease), per-tick position follow (no Il2Cpp parenting).
/// </summary>
static class AuraPool
{
    public sealed class Lease
    {
        internal GameObject? Go;
        internal ParticleSystem? Ps;
        internal float PulseAccum;
        internal float Phase;
        internal bool Active;
    }

    static readonly List<Lease> Slots = new();

    /// <summary>Lease a slot rendering with the given cached material (soft disc or marker shape).</summary>
    public static Lease? Take(Material? material)
    {
        if (material == null) return null;
        Slots.RemoveAll(s => s.Go == null && !s.Active);
        foreach (var s in Slots)
        {
            if (s.Active || s.Go == null) continue;
            if (!Rebind(s, material)) continue;
            s.PulseAccum = 0f;
            s.Phase = 0f;
            s.Active = true;
            return s;
        }

        if (Slots.Count >= VfxSustainedRules.GlobalCap) return null;
        var created = Create(material);
        if (created == null) return null;
        Slots.Add(created);
        created.Active = true;
        return created;
    }

    public static void Release(Lease? lease)
    {
        if (lease == null) return;
        lease.Active = false;
        try { lease.Ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
    }

    public static void StopAll()
    {
        foreach (var s in Slots) Release(s);
    }

    public static int ActiveCount()
    {
        var n = 0;
        foreach (var s in Slots)
        {
            if (s.Active) n++;
        }

        return n;
    }

    /// <summary>Advance one lease: follow the anchor and pulse-emit via pure VfxAuraMath.</summary>
    public static void Pulse(
        Lease lease, Vector3 world, VfxAuraStyle style,
        (byte R, byte G, byte B) rgb, float span, float dt)
    {
        if (!lease.Active || lease.Ps == null || lease.Go == null) return;
        try { lease.Go.transform.position = world; } catch { return; }
        lease.PulseAccum += dt;
        if (lease.PulseAccum < VfxSustainedRules.AuraPulseSeconds) return;
        lease.PulseAccum = 0f;
        lease.Phase += VfxSustainedRules.AuraPulseSeconds;

        var live = 0;
        try { live = lease.Ps.particleCount; } catch { }
        var budget = VfxSustainedRules.AuraMaxParticles - live;
        if (budget <= 0) return;
        var emit = Math.Min(2, budget);
        var c32 = new Color32(rgb.R, rgb.G, rgb.B, 235);
        var baseIndex = (int)(lease.Phase * 10f) % 6;
        for (var i = 0; i < emit; i++)
        {
            var p = VfxAuraMath.Particle(style, baseIndex + i, 6, lease.Phase, span, 0.45f);
            try
            {
                lease.Ps.Emit(
                    new Vector3(p.PosX, p.PosY, 0f),
                    new Vector3(p.VelX, p.VelY, 0f),
                    p.Size, p.Energy, c32);
            }
            catch { break; }
        }
    }

    /// <summary>
    /// Marker mode: keep exactly one bobbing particle alive above the anchor (vfx-v3 M5).
    /// Energy slightly outlasts the pulse interval so the badge reads continuous.
    /// </summary>
    public static void PulseSingle(
        Lease lease, Vector3 world, (byte R, byte G, byte B) rgb,
        float size, float yOffset, float dt)
    {
        if (!lease.Active || lease.Ps == null || lease.Go == null) return;
        try { lease.Go.transform.position = world; } catch { return; }
        lease.PulseAccum += dt;
        if (lease.PulseAccum < VfxSustainedRules.AuraPulseSeconds) return;
        lease.PulseAccum = 0f;
        lease.Phase += VfxSustainedRules.AuraPulseSeconds;

        var live = 0;
        try { live = lease.Ps.particleCount; } catch { }
        if (live >= 2) return;
        var bob = MathF.Sin(lease.Phase * 3f) * size * 0.15f;
        var c32 = new Color32(rgb.R, rgb.G, rgb.B, 235);
        try
        {
            lease.Ps.Emit(
                new Vector3(0f, yOffset + bob, 0f),
                Vector3.zero,
                size,
                VfxSustainedRules.AuraPulseSeconds + 0.15f,
                c32);
        }
        catch { }
    }

    static bool Rebind(Lease lease, Material material)
    {
        try
        {
            var renderer = lease.Go!.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return true;
        }
        catch
        {
            return false;
        }
    }

    static Lease? Create(Material material)
    {
        try
        {
            var go = new GameObject("FusionRpgVfxAura");
            go.hideFlags = HideFlags.HideAndDontSave;
            var ps = go.AddComponent<ParticleSystem>();
            if (ps == null)
            {
                UObject.Destroy(go);
                return null;
            }

            try { ps.playOnAwake = false; } catch { }
            // LIVE lesson: the default emission module rate-emits white particles once Play() runs.
            try
            {
                var emission = ps.emission;
                emission.enabled = false;
                emission.rateOverTime = 0f;
            }
            catch { }
            try { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); } catch { }
            try
            {
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.sortingOrder = FxResources.ParticleSortingOrder;
                }
            }
            catch { }
            try { ps.Play(); } catch { }

            return new Lease { Go = go, Ps = ps };
        }
        catch (Exception ex)
        {
            try { RpgHost.Log.Warning("[vfx.aura] create: " + ex.Message); } catch { }
            return null;
        }
    }
}
