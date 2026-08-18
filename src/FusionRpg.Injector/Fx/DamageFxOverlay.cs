using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Injector.Lawn;
using UnityEngine;

namespace FusionRpg.Injector.Fx;

/// <summary>IMGUI floaters plus optional world flash. Does not write HP or call TakeDamage.</summary>
public static class DamageFxOverlay
{
    public const int Cap = DamageFxFloaterRules.Cap;
    public const float LifeSeconds = DamageFxFloaterRules.LifeSeconds;

    static readonly List<Floater> Items = new();

    public static readonly IDamageFxSink Sink = new Adapter();

    sealed class Adapter : IDamageFxSink
    {
        public void Show(DamageFxDto fx) => DamageFxOverlay.Show(fx);
    }

    public static void Show(DamageFxDto fx)
    {
        if (fx == null) return;
        if (!CheatState.On("SYS-DAMAGE-FX")) return;

        Transform? follow = TryResolve(fx.TargetPtr);
        if (follow == null)
        {
            DebugRuntime.Emit("debug.fx.skipped", new Dictionary<string, object>
            {
                ["ptr"] = fx.TargetPtr ?? "",
                ["amount"] = fx.Amount,
                ["tag"] = fx.Tag.ToString(),
                ["reason"] = "missing"
            });
            return;
        }

        while (DamageFxFloaterRules.AtCap(Items.Count))
            Items.RemoveAt(0);

        var rgb = DamageFxPalette.Rgb(fx.Tag);
        var label = DamageFxPalette.Label(fx.Tag, fx.Amount);
        var color = new Color(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f, 1f);
        Items.Add(new Floater
        {
            Label = label,
            Color = color,
            Follow = follow,
            Age = 0f
        });
        try
        {
            OverlayWorldFx.SpawnAtWorld(LawnCoords.BodyWorld(follow), color);
        }
        catch
        {
            // World flash is optional; never throw into Funnel Flush.
        }
        DebugRuntime.Emit("debug.fx.shown", new Dictionary<string, object>
        {
            ["ptr"] = fx.TargetPtr ?? "",
            ["amount"] = fx.Amount,
            ["tag"] = fx.Tag.ToString(),
            ["label"] = label
        });
    }

    public static void Tick(float dt)
    {
        if (dt < 0f) dt = 0f;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var f = Items[i];
            f.Age += dt;
            if (DamageFxFloaterRules.Expired(f.Age) || f.Follow == null)
                Items.RemoveAt(i);
        }
    }

    public static void Draw()
    {
        if (Items.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;

        var skin = GUI.skin;
        if (skin == null) return;
        var style = skin.label;
        var oldSize = style.fontSize;
        var oldFontStyle = style.fontStyle;
        var oldAlign = style.alignment;
        style.fontSize = 20;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        try
        {
            foreach (var f in Items)
            {
                if (f.Follow == null) continue;
                Vector3 world;
                try { world = LawnCoords.BodyWorld(f.Follow); }
                catch { continue; }
                var t = DamageFxFloaterRules.T(f.Age);
                if (!LawnCoords.TryWorldToGui(cam, world, t, out var gui)) continue;

                var c = f.Color;
                c.a = DamageFxFloaterRules.Alpha(t);
                GUI.color = c;
                GUI.Label(new Rect(gui.x - 80f, gui.y - 16f, 160f, 32f), f.Label, style);
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

    static Transform? TryResolve(string? ptr)
    {
        if (string.IsNullOrWhiteSpace(ptr)) return null;
        try
        {
            foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
            {
                if (z == null) continue;
                if (string.Equals(GameDumps.Ptr(z), ptr, StringComparison.OrdinalIgnoreCase))
                    return z.transform;
            }

            foreach (var plant in UnityEngine.Object.FindObjectsOfType<Plant>())
            {
                if (plant == null) continue;
                if (string.Equals(GameDumps.Ptr(plant), ptr, StringComparison.OrdinalIgnoreCase))
                    return plant.transform;
            }
        }
        catch
        {
            // Missing camera / destroyed objects — skip, never throw into Flush.
        }

        return null;
    }

    sealed class Floater
    {
        public string Label = "";
        public Color Color;
        public Transform? Follow;
        public float Age;
    }
}
