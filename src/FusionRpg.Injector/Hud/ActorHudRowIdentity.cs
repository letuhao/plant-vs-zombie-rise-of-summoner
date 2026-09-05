using FusionRpg.Core.Hud;
using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>Row 0 — tier letter, level digits, role pip from snapshot identity.</summary>
static class ActorHudRowIdentity
{
    public static void Sync(
        ActorHudPool.HudSlot slot,
        ActorHudIdentity identity,
        Material mat,
        float span,
        float rowY)
    {
        var letter = ActorHudDisplayTokens.TierLetter(identity.Tier);
        var hasLetter = !string.IsNullOrEmpty(letter);
        var hasBand = identity.LevelBand is int;
        var roleOn = !string.Equals(identity.Role, "vanilla", StringComparison.OrdinalIgnoreCase);
        var show = hasLetter || hasBand || roleOn || identity.Flags.Count > 0;

        if (!show)
        {
            SetActive(slot.TierFrame, false);
            SetActive(slot.LevelBadge, false);
            SetActive(slot.RolePip, false);
            ActorHudPool.HideLabel(slot.TierLabel);
            ActorHudPool.HideLabel(slot.LevelLabel);
            return;
        }

        var size = Mathf.Clamp(span * 0.12f, 0.08f, 0.22f);

        // No blank Normal tier frame — only draw when a letter exists and the glyph backend works.
        if (hasLetter)
        {
            var tierColor = identity.Tier switch
            {
                ActorHudTier.Unique => new Color(1f, 0.78f, 0.32f, 0.95f),
                ActorHudTier.Elite => new Color(0.72f, 0.55f, 0.86f, 0.95f),
                ActorHudTier.Boss => new Color(0.64f, 0.24f, 0.24f, 0.95f),
                _ => new Color(0.55f, 0.55f, 0.6f, 0.75f),
            };
            var labeled = ActorHudPool.PlaceLabel(slot.TierLabel, 0f, rowY, letter, size * 0.22f, Color.white);
            if (labeled)
            {
                SetActive(slot.TierFrame, true);
                ActorHudPool.PlaceQuad(slot.TierFrame, -size * 0.5f, size, size, mat, tierColor);
            }
            else
            {
                SetActive(slot.TierFrame, false);
                ActorHudPool.HideLabel(slot.TierLabel);
            }
        }
        else
        {
            SetActive(slot.TierFrame, false);
            ActorHudPool.HideLabel(slot.TierLabel);
        }

        if (identity.LevelBand is int band)
        {
            var badgeW = size * 1.1f;
            var badgeX = hasLetter ? size * 0.6f : -badgeW * 0.5f;
            var labeled = ActorHudPool.PlaceLabel(
                slot.LevelLabel,
                badgeX + badgeW * 0.5f,
                rowY,
                band.ToString(),
                size * 0.18f,
                Color.white);
            // Never draw mute level badge without digits (Melon TextMesh-stripped defect).
            if (labeled)
            {
                SetActive(slot.LevelBadge, true);
                ActorHudPool.PlaceQuad(slot.LevelBadge, badgeX, badgeW, size * 0.8f, mat,
                    new Color(0.15f, 0.15f, 0.2f, 0.9f));
            }
            else
            {
                SetActive(slot.LevelBadge, false);
                ActorHudPool.HideLabel(slot.LevelLabel);
            }
        }
        else
        {
            SetActive(slot.LevelBadge, false);
            ActorHudPool.HideLabel(slot.LevelLabel);
        }

        if (roleOn)
        {
            SetActive(slot.RolePip, true);
            var pip = size * 0.75f;
            var roleX = hasLetter ? -size * 1.4f : -size * 0.9f;
            ActorHudPool.PlaceQuad(slot.RolePip, roleX, pip, pip, mat,
                new Color(0.35f, 0.65f, 0.95f, 0.9f));
        }
        else
        {
            SetActive(slot.RolePip, false);
        }
    }

    static void SetActive(MeshRenderer? mr, bool on)
    {
        if (mr == null) return;
        try { mr.gameObject.SetActive(on); } catch { }
    }
}
