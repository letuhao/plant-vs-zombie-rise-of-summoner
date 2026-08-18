using System.Globalization;
using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Stats;
using FusionRpg.Injector.Stats;
using UnityEngine;

namespace FusionRpg.Injector.Effects;

/// <summary>LIVE sink — executes FA* against Unity / Intent. Stop seq on first failure.</summary>
public sealed class InjectorEffectActionSink : IEffectActionSink
{
    public bool Execute(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        try
        {
            var skipped = false;
            var ok = item.Action switch
            {
                EffectActions.ModifyStat => ExecModifyStat(ctx, item),
                EffectActions.ApplyStatus => ExecApplyStatus(ctx, item),
                EffectActions.ClearStatus => ExecClearStatus(ctx, item),
                EffectActions.SpawnEntity => ExecSpawnEntity(ctx, item),
                EffectActions.BoardAction => ExecBoardAction(ctx, item),
                EffectActions.SpawnGridItem => ExecSpawnGrid(ctx, item),
                EffectActions.ClearGridItem => ExecClearGrid(ctx, item),
                EffectActions.SetBoxType => ExecSetBox(ctx, item),
                EffectActions.Economy => ExecEconomy(ctx, item),
                EffectActions.ApplyResourceDelta => ExecApplyResourceDelta(ctx, item, out skipped),
                _ => throw new InvalidOperationException("unknown action " + item.Action)
            };

            DebugRuntime.Emit("debug.effect.fired", new Dictionary<string, object>
            {
                ["grantId"] = item.GrantId,
                ["effectId"] = item.EffectId,
                ["action"] = item.Action,
                ["ok"] = ok,
                ["skipped"] = skipped,
                ["trigger"] = ctx.Event.Trigger
            });

            if (!ok)
            {
                DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object>
                {
                    ["grantId"] = item.GrantId,
                    ["effectId"] = item.EffectId,
                    ["action"] = item.Action,
                    ["error"] = "executor returned false"
                });
            }

            return ok;
        }
        catch (Exception ex)
        {
            CheatState.Error("effect exec " + item.Action + ": " + ex.Message);
            DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object>
            {
                ["grantId"] = item.GrantId,
                ["effectId"] = item.EffectId,
                ["action"] = item.Action,
                ["error"] = ex.Message
            });
            return false;
        }
    }

    static bool ExecModifyStat(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var p = item.Params;
        var channel = JsonOverlay.GetString(p, "channel") ?? StatChannels.Atk;
        var remove = JsonOverlay.GetBool(p, "remove");
        var sourceId = "effect:" + item.GrantId;
        var ownerKey = StatApplyScope.Normalize(ctx.Grant.OwnerKey);

        if (remove)
        {
            CheatState.Stats.WithdrawSource("effect", sourceId);
            CheatActions.ReapplyLivingForOwner(ownerKey);
            return true;
        }

        var factory = CheatState.Stats.Modifiers;
        var mods = new List<StatModifier>();
        if (p.ContainsKey("flat"))
            mods.Add(factory.Flat("foundation.effect", "effect", sourceId, channel, JsonOverlay.GetDouble(p, "flat"),
                applyOwnerKey: ownerKey));
        if (p.ContainsKey("increased"))
            mods.Add(factory.Increased("foundation.effect", "effect", sourceId, channel,
                JsonOverlay.GetDouble(p, "increased"), applyOwnerKey: ownerKey));
        if (p.ContainsKey("more"))
            mods.Add(factory.More("foundation.effect", "effect", sourceId, channel, JsonOverlay.GetDouble(p, "more"),
                applyOwnerKey: ownerKey));

        if (mods.Count == 0)
            mods.Add(factory.Flat("foundation.effect", "effect", sourceId, channel, 0, applyOwnerKey: ownerKey));

        CheatState.SetToggle("A-APPLY", true, "effect", emitInject: false);
        CheatState.Stats.Upsert(mods);
        CheatActions.ReapplyLivingForOwner(ownerKey);
        return true;
    }

    static bool ExecApplyResourceDelta(EffectExecuteContext ctx, EffectActionPlanItem item, out bool skipped)
    {
        skipped = false;
        var p = item.Params;
        var mode = JsonOverlay.GetString(p, "mode");
        if (!string.IsNullOrEmpty(mode) &&
            !string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase))
        {
            skipped = true;
            return true;
        }

        if (p.ContainsKey("hp") || p.ContainsKey("setHp") || p.ContainsKey("absoluteHp") ||
            p.ContainsKey("EntityFinal.Hp") || p.ContainsKey("entityFinalHp"))
        {
            skipped = true;
            return true;
        }

        var channel = JsonOverlay.GetString(p, "channel") ?? "hp";
        if (!string.Equals(channel, "hp", StringComparison.OrdinalIgnoreCase))
        {
            skipped = true;
            return true;
        }

        var amount = (long)JsonOverlay.GetDouble(p, "amount");
        var targetPtr = JsonOverlay.GetString(p, "targetPtr");
        if (string.IsNullOrEmpty(targetPtr))
            targetPtr = ResolveStatusTargetPtr(ctx);
        if (string.IsNullOrEmpty(targetPtr))
        {
            skipped = true;
            return true;
        }

        var source = "effect.fa10:" + item.GrantId;
        foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
        {
            if (z == null) continue;
            if (!CombatPtr.EqualsPtr(GameDumps.Ptr(z), targetPtr)) continue;
            EntityStatWriter.AddZombieHp(z, amount, source);
            return true;
        }

        foreach (var plant in UnityEngine.Object.FindObjectsOfType<Plant>())
        {
            if (plant == null) continue;
            if (!CombatPtr.EqualsPtr(GameDumps.Ptr(plant), targetPtr)) continue;
            EntityStatWriter.AddPlantHp(plant, amount, source);
            return true;
        }

        skipped = true;
        return true;
    }

    /// <summary>Prefer event TargetPtr; if empty, use ActorPtr (OnSpawn / dealt actor).</summary>
    static string ResolveStatusTargetPtr(EffectExecuteContext ctx)
    {
        if (!string.IsNullOrEmpty(ctx.Event.TargetPtr))
            return ctx.Event.TargetPtr!;
        if (!string.IsNullOrEmpty(ctx.Event.ActorPtr))
            return ctx.Event.ActorPtr!;
        return "";
    }

    static bool ExecApplyStatus(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var status = (JsonOverlay.GetString(item.Params, "status") ?? "butter").ToLowerInvariant();
        var duration = (float)JsonOverlay.GetDouble(item.Params, "duration", 4);
        var level = JsonOverlay.GetInt(item.Params, "level", 1);
        var n = 0;
        var targetPtr = ResolveStatusTargetPtr(ctx);

        if (!string.IsNullOrEmpty(targetPtr))
        {
            foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
            {
                if (z == null) continue;
                if (!string.Equals(GameDumps.Ptr(z), targetPtr, StringComparison.OrdinalIgnoreCase)) continue;
                DebugActions.ApplyStatusToZombie(z, status, duration, level, method: true);
                n++;
                break;
            }

            DebugRuntime.Emit("pvz.status.apply", new Dictionary<string, object>
            {
                ["status"] = status,
                ["duration"] = duration,
                ["level"] = level,
                ["count"] = n,
                ["effect_id"] = item.EffectId,
                ["grant_id"] = item.GrantId,
                ["targetPtr"] = targetPtr
            });
            // Non-empty resolved ptr miss → fail closed (stop seq).
            return n > 0;
        }

        foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
        {
            if (z == null) continue;
            DebugActions.ApplyStatusToZombie(z, status, duration, level, method: true);
            n++;
        }

        DebugRuntime.Emit("pvz.status.apply", new Dictionary<string, object>
        {
            ["status"] = status,
            ["duration"] = duration,
            ["level"] = level,
            ["count"] = n,
            ["effect_id"] = item.EffectId,
            ["grant_id"] = item.GrantId,
            ["targetPtr"] = ""
        });
        return true;
    }

    static bool ExecClearStatus(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var status = (JsonOverlay.GetString(item.Params, "status") ?? "").ToLowerInvariant();
        var targetParam = JsonOverlay.GetString(item.Params, "target");
        var targetPtr = ResolveStatusTargetPtr(ctx);
        var n = 0;

        IEnumerable<Zombie> Targets()
        {
            var all = UnityEngine.Object.FindObjectsOfType<Zombie>().Where(z => z != null);
            if (!string.IsNullOrEmpty(targetPtr) &&
                (string.IsNullOrEmpty(targetParam) ||
                 string.Equals(targetParam, "event", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(targetParam, "selected", StringComparison.OrdinalIgnoreCase)))
            {
                return all.Where(z =>
                    string.Equals(GameDumps.Ptr(z), targetPtr, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(targetParam, "all-zombies", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetParam, "all", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(targetParam))
                return all;

            return all;
        }

        foreach (var z in Targets())
        {
            try
            {
                if (string.IsNullOrEmpty(status) || status == "butter")
                    try { z.UnButtered(); } catch { }
                if (string.IsNullOrEmpty(status) || status is "freeze" or "cold")
                {
                    try { z.Warm(); } catch { }
                    try { z.freezeLevel = 0; } catch { }
                    try { z.freezeSpeed = 1f; } catch { }
                    try { z.coldSpeed = 1f; } catch { }
                }

                if (string.IsNullOrEmpty(status) || status == "poison")
                    try { z.KillDebuff(); } catch { }
                n++;
            }
            catch { }
        }

        DebugRuntime.Emit("pvz.status.clear", new Dictionary<string, object>
        {
            ["status"] = status,
            ["count"] = n,
            ["effect_id"] = item.EffectId,
            ["grant_id"] = item.GrantId,
            ["targetPtr"] = targetPtr ?? ""
        });
        return true;
    }

    static bool ExecSpawnEntity(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var kind = (JsonOverlay.GetString(item.Params, "kind") ?? "zombie").ToLowerInvariant();
        var typeId = JsonOverlay.GetInt(item.Params, "typeId", 0);
        var row = JsonOverlay.GetInt(item.Params, "row", CheatState.SpawnRow);
        var col = JsonOverlay.GetInt(item.Params, "col", CheatState.SpawnCol);

        switch (kind)
        {
            case "zombie":
            {
                var hp = JsonOverlay.GetInt(item.Params, "hp", 0);
                var maxHp = JsonOverlay.GetInt(item.Params, "maxHp", hp);
                var x = (float)JsonOverlay.GetDouble(item.Params, "x", 9.9);
                var mc = JsonOverlay.GetBool(item.Params, "mindControlled");
                var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["typeId"] = typeId,
                    ["row"] = row,
                    ["x"] = x,
                    ["mindControl"] = mc,
                    ["hp"] = hp > 0 ? hp : null,
                    ["maxHp"] = maxHp > 0 ? maxHp : null,
                    ["source"] = "effect"
                });
                return DebugActions.SpawnZombie(payload);
            }
            case "plant":
            {
                var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["typeId"] = typeId,
                    ["row"] = row,
                    ["col"] = col
                });
                return DebugActions.SpawnPlant(payload);
            }
            case "bullet":
            {
                var x = (float)JsonOverlay.GetDouble(item.Params, "x", 400);
                var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["typeId"] = typeId,
                    ["bulletType"] = typeId,
                    ["row"] = row,
                    ["x"] = x
                });
                return DebugActions.SpawnBullet(payload);
            }
            default:
                throw new InvalidOperationException("SpawnEntity kind " + kind);
        }
    }

    static bool ExecBoardAction(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var op = JsonOverlay.GetString(item.Params, "op") ?? "cherry";
        // Normalize CreateCherryBomb → cherry
        if (op.Contains("cherry", StringComparison.OrdinalIgnoreCase)) op = "cherry";
        else if (op.Contains("freeze", StringComparison.OrdinalIgnoreCase)) op = "freeze";
        else if (op.Contains("doom", StringComparison.OrdinalIgnoreCase)) op = "doom";
        else if (op.Contains("fire", StringComparison.OrdinalIgnoreCase)) op = "fireline";

        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["op"] = op,
            ["row"] = JsonOverlay.GetInt(item.Params, "row", CheatState.SpawnRow),
            ["col"] = JsonOverlay.GetInt(item.Params, "col", CheatState.SpawnCol),
            ["x"] = JsonOverlay.GetDouble(item.Params, "x", 0),
            ["y"] = JsonOverlay.GetDouble(item.Params, "y", 0)
        });
        DebugActions.BoardAction(payload);
        return true;
    }

    static bool ExecSpawnGrid(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = JsonOverlay.GetInt(item.Params, "gridItemType", 7),
            ["row"] = JsonOverlay.GetInt(item.Params, "row", CheatState.SpawnRow),
            ["col"] = JsonOverlay.GetInt(item.Params, "col", CheatState.SpawnCol)
        });
        DebugActions.SpawnGrid(payload);
        return true;
    }

    static bool ExecClearGrid(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var selector = JsonOverlay.GetString(item.Params, "selector") ?? "last";
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = JsonOverlay.GetInt(item.Params, "gridItemType", 7),
            ["random"] = string.Equals(selector, "random", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(selector, "last", StringComparison.OrdinalIgnoreCase)
        });
        DebugActions.ClearGrid(payload);
        return true;
    }

    static bool ExecSetBox(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var boxType = JsonOverlay.GetInt(item.Params, "boxType", 1);
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["boxType"] = boxType,
            ["row"] = JsonOverlay.GetInt(item.Params, "row", CheatState.SpawnRow),
            ["col"] = JsonOverlay.GetInt(item.Params, "col", CheatState.SpawnCol)
        });
        DebugActions.SetBox(payload);
        return true;
    }

    static bool ExecEconomy(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var currency = JsonOverlay.GetString(item.Params, "currency") ?? "sun";
        var op = (JsonOverlay.GetString(item.Params, "op") ?? "add").ToLowerInvariant();
        var amount = (float)JsonOverlay.GetDouble(item.Params, "amount", 25);
        var add = op is "add" or "+";
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["which"] = currency,
            ["value"] = amount,
            ["add"] = add
        });
        DebugActions.Economy(payload);
        return true;
    }
}
