using FusionRpg.Core.Combat;
using FusionRpg.Core.Vfx;
using FusionRpg.Injector.Effects;
using FusionRpg.Injector.Fx;

namespace FusionRpg.Injector.Hud;

/// <summary>Tick entry for world Actor HUD — called from <see cref="Fx.VfxDirector"/>.</summary>
public static class ActorHudDirector
{
    public static void TickSync()
    {
        try { ActorHudPool.TickSync(); } catch { }
    }

    public static void StopAll()
    {
        try { ActorHudPool.StopAll(); } catch { }
    }

    /// <summary>Debug snapshot for <c>debug.shield.bar-status</c> — runtime owner walk + HUD draw metrics.</summary>
    public static Dictionary<string, object> CaptureStatus()
    {
        EffectRuntime.Ensure();
        var runtime = EffectRuntime.Bag.ShieldGate?.Runtime;
        var owners = new List<object>();
        var dataOwners = 0;
        var resolved = 0;
        if (runtime != null)
        {
            runtime.VisitOwners((ownerKey, shields) =>
            {
                dataOwners++;
                long hp = 0, max = 0;
                var stacks = 0;
                for (var i = 0; i < shields.Count; i++)
                {
                    hp += shields[i].Hp;
                    max += shields[i].MaxHp;
                    if (shields[i].Hp > 0) stacks++;
                }

                var hex = CombatPtr.Normalize(ownerKey);
                var follow = AnchorResolver.Resolve(hex);
                if (follow != null) resolved++;
                owners.Add(new Dictionary<string, object>
                {
                    ["ownerKey"] = ownerKey,
                    ["ptr"] = hex,
                    ["hp"] = hp,
                    ["maxHp"] = max,
                    ["stackCount"] = stacks,
                    ["hasBody"] = follow != null,
                    ["ratio"] = ShieldBarVisual.DisplayRatio(hp, max),
                    ["trueRatio"] = max > 0 ? (float)hp / max : 0f,
                    ["displayRatio"] = ShieldBarVisual.DisplayRatio(hp, max)
                });
            });
        }

        var hudSlots = ActorHudPool.WorldBars;
        var shieldBars = ActorHudPool.ShieldBarsDrawn;
        var fillRatio = ActorHudPool.LastAvgRatio;
        var trueRatio = ActorHudPool.LastAvgTrueRatio;
        return new Dictionary<string, object>
        {
            ["enabled"] = OverlaySettings.ShieldBarEnabled,
            ["hasRuntime"] = runtime != null,
            ["hasInstances"] = runtime?.HasAnyInstances() ?? false,
            ["dataOwners"] = dataOwners,
            ["resolvedBodies"] = resolved,
            ["hudSlots"] = hudSlots,
            ["shieldBars"] = shieldBars,
            ["worldBars"] = shieldBars,
            ["fillRatio"] = fillRatio,
            ["trueRatio"] = trueRatio,
            ["displayRatio"] = fillRatio,
            ["shaderOk"] = ActorHudPool.ShaderOk,
            ["owners"] = owners,
            ["lastDraw"] = new Dictionary<string, object>
            {
                ["early"] = ActorHudPool.LastEarly,
                ["drawnOwners"] = shieldBars,
                ["dataOwners"] = dataOwners,
                ["hudSlots"] = hudSlots,
                ["shieldBars"] = shieldBars,
                ["worldBars"] = shieldBars,
                ["fillRatio"] = fillRatio,
                ["trueRatio"] = trueRatio,
                ["displayRatio"] = fillRatio,
                ["shaderOk"] = ActorHudPool.ShaderOk
            }
        };
    }
}
