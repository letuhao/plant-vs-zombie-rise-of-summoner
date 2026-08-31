using FusionRpg.Core.Hud;
using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>Row 2 — status token quads + overflow pip from snapshot strip.</summary>
static class ActorHudRowStatuses
{
    public static void Sync(
        ActorHudPool.HudSlot slot,
        IReadOnlyList<ActorHudStatusToken> statuses,
        int overflow,
        Material mat,
        float span,
        int maxTokens)
    {
        var showCount = Math.Min(statuses.Count, maxTokens);
        var tokenSize = Mathf.Clamp(span * 0.1f, 0.06f, 0.18f);
        var gap = tokenSize * 1.15f;
        var startX = -(showCount - 1) * gap * 0.5f;

        for (var i = 0; i < slot.StatusTokens.Length; i++)
        {
            var mr = slot.StatusTokens[i];
            if (i >= showCount)
            {
                SetActive(mr, false);
                continue;
            }

            var token = statuses[i];
            var rgb = StatusRgb(token.Id);
            var tint = new Color(rgb.r, rgb.g, rgb.b, 0.92f);
            if (token.Cc)
                tint = Color.Lerp(tint, new Color(1f, 0.45f, 0.2f), 0.35f);

            SetActive(mr, true);
            ActorHudPool.PlaceQuad(mr, startX + i * gap - tokenSize * 0.5f, tokenSize, tokenSize, mat, tint);
        }

        if (overflow > 0 && slot.OverflowPip != null)
        {
            SetActive(slot.OverflowPip, true);
            var ox = startX + showCount * gap;
            ActorHudPool.PlaceQuad(slot.OverflowPip, ox, tokenSize * 0.9f, tokenSize * 0.7f, mat,
                new Color(0.2f, 0.2f, 0.25f, 0.9f));
        }
        else
        {
            SetActive(slot.OverflowPip, false);
        }
    }

    static (float r, float g, float b) StatusRgb(string id)
    {
        var h = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(id));
        var r = 0.35f + (h & 0xFF) / 512f;
        var g = 0.35f + ((h >> 8) & 0xFF) / 512f;
        var b = 0.35f + ((h >> 16) & 0xFF) / 512f;
        return (Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b));
    }

    static void SetActive(MeshRenderer? mr, bool on)
    {
        if (mr == null) return;
        try { mr.gameObject.SetActive(on); } catch { }
    }
}
