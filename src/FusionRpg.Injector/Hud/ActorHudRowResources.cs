using FusionRpg.Core.Hud;
using FusionRpg.Core.Vfx;
using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>Row 1 — element-colored shield segments + stack pips (no runtime reads).</summary>
static class ActorHudRowResources
{
    static readonly List<(string? ElementId, long Hp)> ScratchStacks = new(4);
    static readonly List<ShieldBarColor.Stop> ScratchStops = new(4);

    // Structural pip spacing/size — legacy shield-bar presentation, not balance dials (tunables-ssot §1).
    const float PipGap = 0.12f;
    const float PipSize = 0.08f;

    public static bool Sync(
        ActorHudPool.HudSlot slot,
        ActorHudShield? shield,
        Material mat,
        float barWidth,
        float barHeight,
        float rowY,
        int maxStackPips)
    {
        if (!OverlaySettings.ShieldBarEnabled)
        {
            HideShield(slot);
            return false;
        }

        if (shield == null || shield.Max <= 0 || shield.Hp <= 0)
        {
            HideShield(slot);
            return false;
        }

        ScratchStacks.Clear();
        for (var i = 0; i < shield.Stacks.Count; i++)
        {
            var s = shield.Stacks[i];
            ScratchStacks.Add((s.Element, s.Hp));
        }

        if (!ShieldBarColor.TryBuildStops(ScratchStacks, ScratchStops))
        {
            HideShield(slot);
            return false;
        }

        var ratio = ShieldBarVisual.DisplayRatio(shield.Hp, shield.Max);
        SetActive(slot.ShieldTrack, true);
        ActorHudPool.PlaceQuad(slot.ShieldTrack, -barWidth * 0.5f, barWidth, barHeight, mat,
            new Color(0.08f, 0.08f, 0.1f, 0.85f));

        var fillW = barWidth * ratio;
        var x = -barWidth * 0.5f;
        for (var i = 0; i < slot.ShieldSegments.Length; i++)
        {
            var seg = slot.ShieldSegments[i];
            if (i >= ScratchStops.Count || fillW < 0.01f)
            {
                SetActive(seg, false);
                continue;
            }

            var stop = ScratchStops[i];
            var w = fillW * Math.Max(0f, stop.EndU - stop.StartU);
            if (w < 0.01f)
            {
                SetActive(seg, false);
                continue;
            }

            SetActive(seg, true);
            ActorHudPool.PlaceQuad(seg, x, w, barHeight * 0.85f, mat,
                new Color(stop.R / 255f, stop.G / 255f, stop.B / 255f, 0.95f));
            x += w;
        }

        // Stack pips above the bar (legacy shield-bar presentation pattern).
        var pipCap = Math.Clamp(maxStackPips, 0, slot.StackPips.Length);
        var pipN = Math.Clamp(ScratchStacks.Count, 0, pipCap);
        var pipY = rowY + barHeight * 0.9f;
        var pipStart = -((pipN - 1) * PipGap) * 0.5f;
        for (var i = 0; i < slot.StackPips.Length; i++)
        {
            var mr = slot.StackPips[i];
            if (mr == null) continue;
            if (i >= pipN)
            {
                SetActive(mr, false);
                continue;
            }

            try
            {
                mr.gameObject.SetActive(true);
                mr.transform.localPosition = new Vector3(pipStart + i * PipGap, pipY, 0f);
                mr.transform.localScale = new Vector3(PipSize, PipSize, 1f);
                if (ScratchStops.Count > 0)
                {
                    var stop = ScratchStops[Math.Min(i, ScratchStops.Count - 1)];
                    ActorHudPool.ApplyTint(mr, mat,
                        new Color(stop.R / 255f, stop.G / 255f, stop.B / 255f, 0.95f));
                }
            }
            catch { }
        }

        return true;
    }

    static void HideShield(ActorHudPool.HudSlot slot)
    {
        SetActive(slot.ShieldTrack, false);
        for (var i = 0; i < slot.ShieldSegments.Length; i++)
            SetActive(slot.ShieldSegments[i], false);
        for (var i = 0; i < slot.StackPips.Length; i++)
            SetActive(slot.StackPips[i], false);
    }

    static void SetActive(MeshRenderer? mr, bool on)
    {
        if (mr == null) return;
        try { mr.gameObject.SetActive(on); } catch { }
    }
}
