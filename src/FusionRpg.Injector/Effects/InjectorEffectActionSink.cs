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
                // aura-skill-todo.md Phase 5 / TC2 — DECLARATIVE, and deliberately a no-op here.
                //
                // A `stat.derived` atom is a permanent modifier: the GRANT's presence is the effect.
                // `GrantedDerivedAtomReader` folds it into the actor's derived channels at resolve
                // time, so there is nothing for a sink to do at execute time.
                //
                // It must still be HANDLED rather than fall through to "unknown action", because the
                // compiler marks a triggerless atom `Passive` and `EffectBag.Grant` fires Passive defs
                // on grant. Found live on 2026-08-30: the grant was accepted and overlay-validated,
                // then died at `ERR effect exec ModifyDerivedStat: unknown action`. An earlier comment
                // in this program claimed no sink arm was needed "because nothing executes it" — that
                // was wrong, and only a real lawn run surfaced it.
                EffectActions.ModifyDerivedStat => true,
                _ => throw new InvalidOperationException("unknown action " + item.Action)
            };

            // Per-action fire trace — session-only; in normal play this allocated a dict per
            // executed action per hit (v2 audit §4b.7). Errors below stay unconditional.
            if (DebugRuntime.SessionActive)
            {
                DebugRuntime.Emit("debug.effect.fired", new Dictionary<string, object>
                {
                    ["grantId"] = item.GrantId,
                    ["effectId"] = item.EffectId,
                    ["action"] = item.Action,
                    ["ok"] = ok,
                    ["skipped"] = skipped,
                    ["trigger"] = ctx.Event.Trigger
                });
            }

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
        // Registry first (O(1), runs per FA10 action in combat); scans only on a registry miss
        // so a unit the hooks never saw still takes its delta.
        var zHit = InjectorEntityRegistry.FindZombie(targetPtr);
        if (zHit != null)
        {
            EntityStatWriter.AddZombieHp(zHit, amount, source);
            return true;
        }

        var pHit = InjectorEntityRegistry.FindPlant(targetPtr);
        if (pHit != null)
        {
            EntityStatWriter.AddPlantHp(pHit, amount, source);
            return true;
        }

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
            var zTarget = InjectorEntityRegistry.FindZombie(targetPtr);
            if (zTarget != null)
            {
                DebugActions.ApplyStatusToZombie(zTarget, status, duration, level, method: true);
                n++;
            }
            else
            {
                foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
                {
                    if (z == null) continue;
                    if (!string.Equals(GameDumps.Ptr(z), targetPtr, StringComparison.OrdinalIgnoreCase)) continue;
                    DebugActions.ApplyStatusToZombie(z, status, duration, level, method: true);
                    n++;
                    break;
                }
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

    /// <summary>
    /// E28 fix #3 (spec-param-parity.md §3 row 3): the statuses <c>ApplyStatusToZombie</c> can apply
    /// (<c>method: true</c> switch, <c>DebugActions.cs:867-913</c>) that this sink still cannot clear.
    /// Ember and jala were the E17-documented case — <c>SetEmbered</c>/<c>SetJalaed</c> trigger a
    /// one-shot explosion (<c>EmberExplode</c>/<c>JalaedExplode</c>), not a timed state with a wear-off,
    /// so there is no Unity-side expiry to withdraw at all. Hypno and kelp reflect the same way against
    /// the shipped <c>Assembly-CSharp.dll</c>: no <c>UnMindControl</c>/<c>ClearMindControl</c> or
    /// <c>Unkelp</c>-shaped method exists — only raw settable properties
    /// (<c>isMindControlled</c>, <c>kelpTimes</c>/<c>kelpLayer</c>/<c>kelpSpeed</c>) with no evidence
    /// a bare property flip fully reverses what <c>SetMindControl</c>/<c>SetKelped</c> actually did
    /// (mind control in particular is documented elsewhere as a side-swap, not a flag). Guessing at
    /// that without a live check is exactly the class of defect this module exists to stop shipping —
    /// so these four refuse by name instead, matching ember/jala's own already-established reasoning
    /// rather than inventing a fifth and sixth unverified "fix".
    /// </summary>
    static readonly HashSet<string> UnclearableStatuses = new(StringComparer.Ordinal)
        { "ember", "jala", "hypno", "kelp" };

    static bool ExecClearStatus(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var status = (JsonOverlay.GetString(item.Params, "status") ?? "").ToLowerInvariant();

        if (UnclearableStatuses.Contains(status))
        {
            CheatState.Error(
                $"debug.clear-status: '{status}' has no Unity-side expiry to withdraw " +
                "(DebugActions.cs:886-913) — refusing rather than silently doing nothing");
            return false;
        }

        var targetParam = JsonOverlay.GetString(item.Params, "target");
        var targetPtr = ResolveStatusTargetPtr(ctx);
        var n = 0;

        IEnumerable<Zombie> Targets()
        {
            if (!string.IsNullOrEmpty(targetPtr) &&
                (string.IsNullOrEmpty(targetParam) ||
                 string.Equals(targetParam, "event", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(targetParam, "selected", StringComparison.OrdinalIgnoreCase)))
            {
                // Targeted clear: registry O(1) first, scan only on a miss (v2 audit §4b.7).
                var zTarget = InjectorEntityRegistry.FindZombie(targetPtr);
                if (zTarget != null) return new[] { zTarget };
                return UnityEngine.Object.FindObjectsOfType<Zombie>().Where(z =>
                    z != null &&
                    string.Equals(GameDumps.Ptr(z), targetPtr, StringComparison.OrdinalIgnoreCase));
            }

            var all = UnityEngine.Object.FindObjectsOfType<Zombie>().Where(z => z != null);

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
        var atk = JsonOverlay.GetInt(item.Params, "atk", 0);

        // E28 fix #5 (spec-param-parity.md §3 row 5): count is structural-floored at 1 — zero spawns
        // is not a legal "less of the effect", it is the effect never happening, so an omitted or
        // authored-zero count still spawns once. The sink previously spawned exactly one entity per
        // plan item regardless of what was authored; this loops it. "Stop seq on first failure" (this
        // file's own class doc) applies here too — a failed spawn in the middle of a count stops the
        // rest rather than silently under-delivering without saying so.
        var count = Math.Max(1, JsonOverlay.GetInt(item.Params, "count", 1));
        for (var i = 0; i < count; i++)
        {
            var ok = kind switch
            {
                "zombie" => SpawnZombieOnce(item, typeId, row, atk),
                "plant" => SpawnPlantOnce(item, typeId, row, col, atk),
                "bullet" => SpawnBulletOnce(item, typeId, row),
                _ => throw new InvalidOperationException("SpawnEntity kind " + kind),
            };
            if (!ok) return false;
        }
        return true;
    }

    static bool SpawnZombieOnce(EffectActionPlanItem item, int typeId, int row, int atk)
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
            // E28 fix #5: DebugActions.ApplyAbsoluteProps now reads this for zombies too (Z-ATK).
            ["atk"] = atk > 0 ? atk : null,
            ["source"] = "effect"
        });
        return DebugActions.SpawnZombie(payload);
    }

    static bool SpawnPlantOnce(EffectActionPlanItem item, int typeId, int row, int col, int atk)
    {
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = typeId,
            ["row"] = row,
            ["col"] = col,
            // E28 fix #5: DebugActions.ApplyAbsoluteProps already read this for plants (P-ATK) — it
            // was simply never in the payload the sink built.
            ["atk"] = atk > 0 ? atk : null,
        });
        return DebugActions.SpawnPlant(payload);
    }

    static bool SpawnBulletOnce(EffectActionPlanItem item, int typeId, int row)
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

    static bool ExecBoardAction(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var op = JsonOverlay.GetString(item.Params, "op") ?? "cherry";
        // Normalize CreateCherryBomb → cherry
        if (op.Contains("cherry", StringComparison.OrdinalIgnoreCase)) op = "cherry";
        else if (op.Contains("freeze", StringComparison.OrdinalIgnoreCase)) op = "freeze";
        else if (op.Contains("doom", StringComparison.OrdinalIgnoreCase)) op = "doom";
        else if (op.Contains("fire", StringComparison.OrdinalIgnoreCase)) op = "fireline";

        // E28 fix #2 (spec-param-parity.md §3): `damage` is declared on the kind (AtomKindRegistry.cs)
        // and validated at bind, but never reached this payload — every board.action fired
        // DebugActions.BoardAction's own hardcoded 1800 default regardless of what was authored.
        // `x`/`y` are the mirror defect: DebugActions.BoardAction derives its own `pos` from col/row
        // and never reads an author-supplied x/y (only pos.x/pos.y appear, in its telemetry dicts) —
        // decided 2026-09-03 to delete the two dead keys here rather than declare them on the schema,
        // since declaring params that reach no executor is the exact defect class this module exists
        // to remove.
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["op"] = op,
            ["row"] = JsonOverlay.GetInt(item.Params, "row", CheatState.SpawnRow),
            ["col"] = JsonOverlay.GetInt(item.Params, "col", CheatState.SpawnCol),
            ["damage"] = JsonOverlay.GetInt(item.Params, "damage", 1800),
        });
        DebugActions.BoardAction(payload);
        return true;
    }

    static bool ExecSpawnGrid(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        // E28 fix #6 (spec-param-parity.md §3 row 6): graveType was declared, validated, and
        // never forwarded — DebugActions.SpawnGrid already reads and honours it (DebugActions.cs:382).
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = JsonOverlay.GetInt(item.Params, "gridItemType", 7),
            ["row"] = JsonOverlay.GetInt(item.Params, "row", CheatState.SpawnRow),
            ["col"] = JsonOverlay.GetInt(item.Params, "col", CheatState.SpawnCol),
            ["graveType"] = JsonOverlay.GetInt(item.Params, "graveType", 0),
        });
        DebugActions.SpawnGrid(payload);
        return true;
    }

    static bool ExecClearGrid(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        // E28 fix #4 (spec-param-parity.md §3 row 4): row/col now forward through to
        // DebugActions.ClearGridItem, which already accepts them (DebugActions.cs:639-668) — an atom
        // can target a specific cell instead of only ever colliding on an ambiguous multi-match
        // refusal or an explicit random pick. `selector`'s meaning is otherwise unchanged: "random"
        // still means random; anything else (including an absent selector) leaves the choice to
        // col/row or the single-match fallback the executor already had.
        var selector = JsonOverlay.GetString(item.Params, "selector");
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = JsonOverlay.GetInt(item.Params, "gridItemType", 7),
            ["random"] = string.Equals(selector, "random", StringComparison.OrdinalIgnoreCase),
            ["row"] = JsonOverlay.GetIntOrNull(item.Params, "row"),
            ["col"] = JsonOverlay.GetIntOrNull(item.Params, "col"),
        });
        DebugActions.ClearGrid(payload);
        return true;
    }

    static bool ExecSetBox(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var boxType = JsonOverlay.GetInt(item.Params, "boxType", 1);

        // E28 fix #7 (spec-param-parity.md §3 row 7): cells[] paints every listed cell instead of
        // just one. Each entry is {row, col} — the same shape row/col already carry on this kind,
        // just plural. Only reachable now that AtomCompiler.Plain() preserves array/object structure
        // (was stringifying it) and AtomPushCodec.ToDef unwraps the wire's JsonElement boxing
        // recursively (both fixed this session) — without either, `cells` would arrive as either a
        // literal JSON-text string or a JsonElement that JsonOverlay.GetInt cannot read.
        if (item.Params.TryGetValue("cells", out var cellsRaw) && cellsRaw is List<object?> cells)
        {
            foreach (var cellObj in cells)
            {
                if (cellObj is not Dictionary<string, object?> cell) continue;
                var cellPayload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["boxType"] = boxType,
                    ["row"] = JsonOverlay.GetInt(cell, "row", CheatState.SpawnRow),
                    ["col"] = JsonOverlay.GetInt(cell, "col", CheatState.SpawnCol),
                });
                DebugActions.SetBox(cellPayload);
            }
            return true;
        }

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
