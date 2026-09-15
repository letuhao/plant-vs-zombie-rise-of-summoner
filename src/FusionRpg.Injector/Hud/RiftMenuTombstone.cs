#if FUSIONRPG_MELON
using FusionRpg.Core.Overlay;
using FusionRpg.Injector.Host;
using UnityEngine;

namespace FusionRpg.Injector.Hud;

/// <summary>
/// The clickable Rift tombstone, **inside the game's own menu hierarchy** (map Decisions 17 + 18).
///
/// Decision 17 — attach, never drive: this builds a real uGUI node under the live <c>MainMenu</c>
/// transform. It never calls a <c>UIMgr</c> navigation method and never changes the engine's menu
/// state; the game's own canvas/layout/raycast system owns hit-testing and z-order.
///
/// Decision 18 — one rendering system: the Rift art rides a <see cref="Sprite"/> on the node's
/// <c>Image</c>, and the same node is the button's graphic. The IMGUI menu painter is retired for the
/// menu (the in-match "RPG" button is match chrome and deliberately stays IMGUI).
///
/// The anchor is the live <c>MainMenu</c> transform, not a scene path: Task 1 verified the reference
/// mod's 3.8.1 paths (<c>Grave/…</c>) do not exist in 3.9, while <c>MainMenu</c> exposes real menu
/// nodes to order against. Every failure logs a named warning and leaves the game untouched.
/// </summary>
static class RiftMenuTombstone
{
    const string NodeName = "FusionRpgRiftTombstone";

    static GameObject? _node;

    /// <summary>
    /// Builds the affordance under <paramref name="menu"/>. Idempotent: a rebuild reuses the existing
    /// node rather than stacking a second one (the presence patch can re-run on scene reloads).
    /// </summary>
    internal static void Attach(Transform menu)
    {
        try
        {
            if (menu == null)
            {
                RpgHost.Log.Warning("[rift] tombstone not attached: no menu transform");
                return;
            }

            if (_node != null && _node) return;

            var go = new GameObject(NodeName);
            go.transform.SetParent(menu, worldPositionStays: false);

            var rect = go.AddComponent<RectTransform>();
            var n = RiftMenuPlacement.ResolveNormalized();
            // Stretch-anchored to the menu, sized and centred from the tuning group. Normalized
            // anchoring is what makes the node inherit the game canvas's scaling (Decision 18).
            rect.anchorMin = new Vector2(n.MinX, n.MinY);
            rect.anchorMax = new Vector2(n.MaxX, n.MaxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.sprite = RiftMenuArt.MainSprite();
            image.preserveAspect = true;
            image.raycastTarget = true;   // the game's own canvas raycasts to us

            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(new System.Action(OnClick));

            AttachMiniCompanion(go);

            OrderBelowPrimaryButtons(menu);

            _node = go;
            RpgHost.Log.Info("[rift] tombstone attached to the main menu (uGUI, observe-and-attach)");
        }
        catch (Exception ex)
        {
            RpgHost.Log.Warning("[rift] tombstone attach failed: " + ex.Message);
        }
    }

    /// <summary>
    /// The companion icon, positioned from the reviewed Core composition
    /// (<c>RiftMenuOverlayLayout.MiniCompanion</c>) so the uGUI move keeps the authored two-piece art.
    /// It is a decorative child with no raycast, so it never competes with the clickable node.
    /// </summary>
    static void AttachMiniCompanion(GameObject parent)
    {
        var sprite = RiftMenuArt.MiniSprite();
        if (sprite == null) return; // art absent — the primary node is still usable

        var composition = RiftMenuOverlayLayout.MiniCompanionInParent(
            RiftMenuPlacement.ResolveNormalized().ToOverlayRect());

        var mini = new GameObject("FusionRpgRiftMini");
        mini.transform.SetParent(parent.transform, worldPositionStays: false);
        var miniRect = mini.AddComponent<RectTransform>();
        miniRect.anchorMin = new Vector2(composition.X, composition.Y);
        miniRect.anchorMax = new Vector2(composition.X + composition.Width, composition.Y + composition.Height);
        miniRect.offsetMin = Vector2.zero;
        miniRect.offsetMax = Vector2.zero;

        var miniImage = mini.AddComponent<UnityEngine.UI.Image>();
        miniImage.sprite = sprite;
        miniImage.preserveAspect = true;
        miniImage.raycastTarget = false; // decorative only; the parent owns the click
    }

    /// <summary>Clears the cached node when the menu object goes away, so a later build re-attaches.</summary>
    internal static void OnMenuGone() => _node = null;

    static void OnClick()
    {
        // The SAME request path the in-match button and the hotkey use — no second toggle path.
        try { OverlaySwitch.RequestToggle(); }
        catch (Exception ex) { RpgHost.Log.Warning("[rift] tombstone click failed: " + ex.Message); }
    }

    /// <summary>
    /// Sibling order is how an in-hierarchy element avoids eating a host click: the game's own primary
    /// buttons stay ahead of us. We also clear <c>raycastTarget</c> on nothing we did not add — the
    /// reference cleared two labels it created a conflict with, and we add no label at all, so this is
    /// limited to ordering ourselves last.
    /// </summary>
    static void OrderBelowPrimaryButtons(Transform menu)
    {
        if (_node == null || !_node) return;
        // SetAsFirstSibling draws us behind the game's own buttons in uGUI's back-to-front order,
        // so a click the game wants goes to the game.
        _node.transform.SetAsFirstSibling();
    }
}
#endif
