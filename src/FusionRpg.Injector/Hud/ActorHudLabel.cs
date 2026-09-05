using UnityEngine;
#if FUSIONRPG_MELON
using Il2CppTMPro;
#endif

namespace FusionRpg.Injector.Hud;

/// <summary>
/// World-space HUD glyph. Melon Il2Cpp has no TextMesh class pointer (LIVE 2026-09-06 —
/// empty badge quads); use TextMeshPro there. BepInEx keeps TextMesh.
/// </summary>
public sealed class ActorHudLabel
{
    readonly GameObject _go;
#if FUSIONRPG_MELON
    readonly TextMeshPro? _tmp;
#else
    readonly TextMesh? _tm;
#endif

    ActorHudLabel(GameObject go
#if FUSIONRPG_MELON
        , TextMeshPro? tmp
#else
        , TextMesh? tm
#endif
        )
    {
        _go = go;
#if FUSIONRPG_MELON
        _tmp = tmp;
#else
        _tm = tm;
#endif
    }

    public bool IsReady =>
#if FUSIONRPG_MELON
        _tmp != null;
#else
        _tm != null;
#endif

    public static ActorHudLabel? Create(GameObject root, string name)
    {
        try
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(root.transform, false);
            go.SetActive(false);

#if FUSIONRPG_MELON
            TextMeshPro? tmp = null;
            try
            {
                tmp = go.AddComponent<TextMeshPro>();
            }
            catch (Exception ex)
            {
                try { Host.RpgHost.Log.Warning("[actor-hud] TextMeshPro: " + ex.Message); } catch { }
            }

            if (tmp == null)
            {
                try { UnityEngine.Object.Destroy(go); } catch { }
                return null;
            }

            try
            {
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.richText = false;
                tmp.fontSize = 8f;
                tmp.color = Color.white;
                var r = go.GetComponent<MeshRenderer>();
                if (r != null) r.sortingOrder = Fx.FxResources.ParticleSortingOrder + 4;
            }
            catch { }

            return new ActorHudLabel(go, tmp);
#else
            TextMesh? tm = null;
            try { tm = go.AddComponent<TextMesh>(); }
            catch (Exception ex)
            {
                try { Host.RpgHost.Log.Warning("[actor-hud] TextMesh: " + ex.Message); } catch { }
            }

            if (tm == null)
            {
                try { UnityEngine.Object.Destroy(go); } catch { }
                return null;
            }

            try
            {
                tm.characterSize = 0.04f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = 32;
                tm.color = Color.white;
                var r = go.GetComponent<MeshRenderer>();
                if (r != null) r.sortingOrder = Fx.FxResources.ParticleSortingOrder + 4;
            }
            catch { }

            return new ActorHudLabel(go, tm);
#endif
        }
        catch (Exception ex)
        {
            try { Host.RpgHost.Log.Warning("[actor-hud] label create: " + ex.Message); } catch { }
            return null;
        }
    }

    public bool TryPlace(float localX, float localY, string text, float charSize, Color color)
    {
        if (!IsReady) return false;
        try
        {
            _go.SetActive(true);
#if FUSIONRPG_MELON
            _tmp!.text = text;
            _tmp.color = color;
            // World TMP: fontSize is point-ish; scale the transform for lawn-sized glyphs.
            _tmp.fontSize = 36f;
            var s = Mathf.Clamp(charSize * 2.2f, 0.035f, 0.12f);
            _tmp.transform.localPosition = new Vector3(localX, localY, -0.02f);
            _tmp.transform.localScale = new Vector3(s, s, s);
#else
            _tm!.text = text;
            _tm.color = color;
            _tm.characterSize = charSize;
            _tm.anchor = TextAnchor.MiddleCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.fontSize = 32;
            _tm.transform.localPosition = new Vector3(localX, localY, -0.01f);
            _tm.transform.localScale = Vector3.one;
#endif
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Hide()
    {
        try { _go.SetActive(false); } catch { }
    }
}
