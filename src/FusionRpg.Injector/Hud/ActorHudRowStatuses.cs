using FusionRpg.Core.Hud;
using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>Row 2 — status token quads + catalog hudToken labels + overflow pip.</summary>
static class ActorHudRowStatuses
{
    public static void Sync(
        ActorHudPool.HudSlot slot,
        IReadOnlyList<ActorHudStatusToken> statuses,
        int overflow,
        Material mat,
        float span,
        float rowY,
        int maxTokens)
    {
        var showCount = Math.Min(statuses.Count, Math.Min(maxTokens, slot.StatusTokens.Length));
        var tokenSize = Mathf.Clamp(span * 0.1f, 0.06f, 0.18f);
        var gap = tokenSize * 1.15f;
        var startX = -(showCount - 1) * gap * 0.5f;

        for (var i = 0; i < slot.StatusTokens.Length; i++)
        {
            var mr = slot.StatusTokens[i];
            var label = slot.StatusLabels[i];
            if (i >= showCount)
            {
                SetActive(mr, false);
                ActorHudPool.HideLabel(label);
                continue;
            }

            var token = statuses[i];
            var resolved = ActorHudDisplayTokens.ResolveStatus(token.Id);
            var tint = ParseCatalogColor(resolved.Color);
            if (token.Cc)
                tint = Color.Lerp(tint, new Color(1f, 0.45f, 0.2f), 0.35f);

            var cx = startX + i * gap;
            var labeled = ActorHudPool.PlaceLabel(
                label,
                cx,
                rowY,
                resolved.HudToken,
                tokenSize * 0.2f,
                Color.white);
            // Mute status chip without a token glyph is unreadable — skip the quad too.
            if (labeled)
            {
                SetActive(mr, true);
                ActorHudPool.PlaceQuad(mr, cx - tokenSize * 0.5f, tokenSize, tokenSize, mat, tint);
            }
            else
            {
                SetActive(mr, false);
                ActorHudPool.HideLabel(label);
            }
        }

        if (overflow > 0 && slot.OverflowPip != null)
        {
            var ox = startX + showCount * gap;
            var labeled = ActorHudPool.PlaceLabel(
                slot.OverflowLabel,
                ox + tokenSize * 0.45f,
                rowY,
                "+" + overflow,
                tokenSize * 0.16f,
                Color.white);
            if (labeled)
            {
                SetActive(slot.OverflowPip, true);
                ActorHudPool.PlaceQuad(slot.OverflowPip, ox, tokenSize * 0.9f, tokenSize * 0.7f, mat,
                    new Color(0.2f, 0.2f, 0.25f, 0.9f));
            }
            else
            {
                SetActive(slot.OverflowPip, false);
                ActorHudPool.HideLabel(slot.OverflowLabel);
            }
        }
        else
        {
            SetActive(slot.OverflowPip, false);
            ActorHudPool.HideLabel(slot.OverflowLabel);
        }
    }

    /// <summary>Parse authored #RRGGBB (or #RGB) from status-catalog; muted fallback matches Core placeholder.</summary>
    static Color ParseCatalogColor(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex) && ColorUtility.TryParseHtmlString(hex.Trim(), out var c))
        {
            c.a = 0.92f;
            return c;
        }

        // #a89880 — ActorHudDisplayTokens.UnknownStatus.Color
        return new Color(168f / 255f, 152f / 255f, 128f / 255f, 0.92f);
    }

    static void SetActive(MeshRenderer? mr, bool on)
    {
        if (mr == null) return;
        try { mr.gameObject.SetActive(on); } catch { }
    }
}
