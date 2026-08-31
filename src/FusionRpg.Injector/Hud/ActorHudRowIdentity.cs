using FusionRpg.Core.Hud;
using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>Row 0 — tier frame, level badge, role pip from snapshot identity.</summary>
static class ActorHudRowIdentity
{
    public static void Sync(
        ActorHudPool.HudSlot slot,
        ActorHudIdentity identity,
        Material mat,
        float span)
    {
        var show = identity.Tier != ActorHudTier.Normal
            || identity.LevelBand is not null
            || identity.Flags.Count > 0
            || !string.Equals(identity.Role, "vanilla", StringComparison.OrdinalIgnoreCase);

        if (!show)
        {
            SetActive(slot.TierFrame, false);
            SetActive(slot.LevelBadge, false);
            SetActive(slot.RolePip, false);
            return;
        }

        var size = Mathf.Clamp(span * 0.12f, 0.08f, 0.22f);
        var tierColor = identity.Tier switch
        {
            ActorHudTier.Unique => new Color(1f, 0.78f, 0.32f, 0.95f),
            ActorHudTier.Elite => new Color(0.72f, 0.55f, 0.86f, 0.95f),
            ActorHudTier.Boss => new Color(0.64f, 0.24f, 0.24f, 0.95f),
            _ => new Color(0.55f, 0.55f, 0.6f, 0.75f),
        };

        SetActive(slot.TierFrame, true);
        ActorHudPool.PlaceQuad(slot.TierFrame, -size * 0.5f, size, size, mat, tierColor);

        if (identity.LevelBand is int band)
        {
            var badgeW = size * 1.1f;
            SetActive(slot.LevelBadge, true);
            ActorHudPool.PlaceQuad(slot.LevelBadge, size * 0.6f, badgeW, size * 0.8f, mat,
                new Color(0.15f, 0.15f, 0.2f, 0.9f));
        }
        else
        {
            SetActive(slot.LevelBadge, false);
        }

        var roleOn = !string.Equals(identity.Role, "vanilla", StringComparison.OrdinalIgnoreCase);
        if (roleOn)
        {
            SetActive(slot.RolePip, true);
            var pip = size * 0.75f;
            ActorHudPool.PlaceQuad(slot.RolePip, -size * 1.4f, pip, pip, mat,
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
