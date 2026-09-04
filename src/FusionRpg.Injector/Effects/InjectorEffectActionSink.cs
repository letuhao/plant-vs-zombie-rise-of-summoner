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
                EffectActions.ModifyMatch => ExecModifyMatch(ctx, item),
                EffectActions.WaveControl => ExecWaveControl(ctx, item),
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

        // E39 (spec-plant-side-status.md §2b): item.Params["targetPtr"] first, same precedence as
        // ExecApplyResourceDelta above. StatusEffectBridge's FA10 DoT/contagion producer
        // (StatusEffectBridge.cs TryApplyFromGrant) writes one plan item per resolved host ptr and
        // stamps each item's own targetPtr; a directly-authored status.apply atom carries no
        // targetPtr in its own ParamSchema (AtomKindRegistry.cs status.apply row — E1 refused one),
        // so ResolveStatusTargetPtr(ctx), the event's own ptr, is the fallback for that shape.
        var targetPtr = JsonOverlay.GetString(item.Params, "targetPtr");
        if (string.IsNullOrEmpty(targetPtr))
            targetPtr = ResolveStatusTargetPtr(ctx);

        // G5 CLOSED HERE (spec-plant-side-status.md §2b — E1 left this open explicitly for
        // "whoever guards that loop"; E39 is that owner). This used to fall through to
        // `foreach (var z in FindObjectsOfType<Zombie>()) ApplyStatusToZombie(...)` on an empty
        // ptr — every living zombie, unconditionally, with no way to tell "this meant board-wide"
        // from "the resolve just failed". That loop is DELETED, not merely made unreachable: an
        // empty resolved ptr refuses.
        if (string.IsNullOrEmpty(targetPtr))
        {
            DebugRuntime.Emit("pvz.status.apply", new Dictionary<string, object>
            {
                ["status"] = status,
                ["duration"] = duration,
                ["level"] = level,
                ["count"] = 0,
                ["effect_id"] = item.EffectId,
                ["grant_id"] = item.GrantId,
                ["targetPtr"] = "",
                ["side"] = "",
                ["reason"] = "status-no-target"
            });
            return false;
        }

        // §2a: registry first, side second — O(1) both times, falling back to the same two scans
        // ExecApplyResourceDelta already had above (no NEW FindObjectsOfType scan on this path).
        var target = ResolveStatusTarget(targetPtr);
        string side;
        bool ok;
        string? reason = null;

        switch (target)
        {
            case Zombie z:
                side = "zombie";
                DebugActions.ApplyStatusToZombie(z, status, duration, level, method: true);
                ok = true;
                break;
            case Plant p:
                side = "plant";
                ok = DebugActions.ApplyStatusToPlant(p, status, duration, level, out reason);
                break;
            default:
                // §2a rule 3: a resolved ptr matching neither side is a real failure, not the
                // silent "n stays 0" shape this replaced.
                side = "";
                ok = false;
                reason = "status-target-not-found";
                break;
        }

        var dump = new Dictionary<string, object>
        {
            ["status"] = status,
            ["duration"] = duration,
            ["level"] = level,
            ["count"] = ok ? 1 : 0,
            ["effect_id"] = item.EffectId,
            ["grant_id"] = item.GrantId,
            ["targetPtr"] = targetPtr,
            ["side"] = side
        };
        if (reason != null)
            dump["reason"] = reason;
        DebugRuntime.Emit("pvz.status.apply", dump);
        return ok;
    }

    /// <summary>
    /// E39 (spec-plant-side-status.md §2a): registry first, side second. Same four-step shape
    /// <see cref="ExecApplyResourceDelta"/> already uses above — O(1) <see cref="InjectorEntityRegistry"/>
    /// lookups for both sides, then the SAME two miss-path scans that already existed there (no new
    /// <c>FindObjectsOfType</c> scan is added anywhere on this module's path — the spec's own "No new
    /// scan" rule, §2a). Returns a live <see cref="Zombie"/> or <see cref="Plant"/>, or null when the
    /// ptr matches neither — the caller treats null as a real failure, never a silent success.
    /// </summary>
    static object? ResolveStatusTarget(string targetPtr)
    {
        var zHit = InjectorEntityRegistry.FindZombie(targetPtr);
        if (zHit != null) return zHit;

        var pHit = InjectorEntityRegistry.FindPlant(targetPtr);
        if (pHit != null) return pHit;

        foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
        {
            if (z == null) continue;
            if (!CombatPtr.EqualsPtr(GameDumps.Ptr(z), targetPtr)) continue;
            return z;
        }

        foreach (var plant in UnityEngine.Object.FindObjectsOfType<Plant>())
        {
            if (plant == null) continue;
            if (!CombatPtr.EqualsPtr(GameDumps.Ptr(plant), targetPtr)) continue;
            return plant;
        }

        return null;
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

        var targetParam = (JsonOverlay.GetString(item.Params, "target") ?? "").ToLowerInvariant();
        var targetPtr = ResolveStatusTargetPtr(ctx);

        // E39 (spec-plant-side-status.md §2b): status.clear's own declared `target` string
        // (status.apply carries none — E1 refused one, §2b re-states why). "all" / "all-zombies" /
        // "all-plants" are new here. "" / "event" / "selected" now mean "the event's resolved ptr,
        // either side" and REFUSE on an empty ptr instead of the old fallthrough default (the foot
        // of the pre-E39 Targets() helper returned every zombie for ANY unrecognised or empty
        // target) — the same G5-shaped silent board-wide clear status.apply had, closed the same
        // way, right here, since E39 owns that closure module-wide.
        List<object> targets;
        string mode;
        switch (targetParam)
        {
            case "all":
                targets = CollectBothSides(zombies: true, plants: true);
                mode = "all";
                break;
            case "all-zombies":
                targets = CollectBothSides(zombies: true, plants: false);
                mode = "all-zombies";
                break;
            case "all-plants":
                targets = CollectBothSides(zombies: false, plants: true);
                mode = "all-plants";
                break;
            case "":
            case "event":
            case "selected":
                mode = "single";
                if (string.IsNullOrEmpty(targetPtr))
                {
                    DebugRuntime.Emit("pvz.status.clear", new Dictionary<string, object>
                    {
                        ["status"] = status,
                        ["count"] = 0,
                        ["effect_id"] = item.EffectId,
                        ["grant_id"] = item.GrantId,
                        ["targetPtr"] = "",
                        ["side"] = "",
                        ["reason"] = "status-no-target"
                    });
                    return false;
                }

                var single = ResolveStatusTarget(targetPtr);
                if (single == null)
                {
                    DebugRuntime.Emit("pvz.status.clear", new Dictionary<string, object>
                    {
                        ["status"] = status,
                        ["count"] = 0,
                        ["effect_id"] = item.EffectId,
                        ["grant_id"] = item.GrantId,
                        ["targetPtr"] = targetPtr,
                        ["side"] = "",
                        ["reason"] = "status-target-not-found"
                    });
                    return false;
                }

                targets = new List<object> { single };
                break;
            default:
                // No closed vocabulary for this string (AtomKindRegistry.cs declares `target` as a
                // bare ParamKind.String with no Vocabulary list) — refuse rather than guess what an
                // unrecognised target meant, matching the "refuse instead of fake" posture everywhere
                // else on this path.
                CheatState.Error("debug.clear-status: unrecognised target '" + targetParam + "'");
                return false;
        }

        var n = 0;
        string? refuseReason = null;
        var side = mode switch
        {
            "all-zombies" => "zombie",
            "all-plants" => "plant",
            "all" => "both",
            _ => ""
        };

        foreach (var t in targets)
        {
            switch (t)
            {
                case Zombie z:
                    ClearZombieStatus(z, status);
                    n++;
                    if (mode == "single") side = "zombie";
                    break;
                case Plant p:
                    // §2c: only `butter` (Plant.butterP) has a confirmed plant-side write. In
                    // single-target mode, a SPECIFICALLY named other status aimed at this one
                    // resolved plant refuses — never a silent no-op count, mirroring
                    // ExecApplyStatus's own posture (acceptance criterion 6: apply/clear stay
                    // symmetric). In a broadcast, an inapplicable arm is simply skipped, the same
                    // way the zombie arms already skip whichever status is not theirs.
                    if (mode == "single" && !string.IsNullOrEmpty(status) && status != "butter")
                    {
                        refuseReason = "status-side-unsupported";
                        side = "plant";
                        break;
                    }

                    ClearPlantStatus(p, status);
                    n++;
                    if (mode == "single") side = "plant";
                    break;
            }
        }

        var dump = new Dictionary<string, object>
        {
            ["status"] = status,
            ["count"] = n,
            ["effect_id"] = item.EffectId,
            ["grant_id"] = item.GrantId,
            ["targetPtr"] = targetPtr ?? "",
            ["side"] = side
        };
        if (refuseReason != null)
            dump["reason"] = refuseReason;
        DebugRuntime.Emit("pvz.status.clear", dump);
        return refuseReason == null;
    }

    static List<object> CollectBothSides(bool zombies, bool plants)
    {
        var list = new List<object>();
        if (zombies)
            foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
                if (z != null) list.Add(z);
        if (plants)
            foreach (var p in UnityEngine.Object.FindObjectsOfType<Plant>())
                if (p != null) list.Add(p);
        return list;
    }

    static void ClearZombieStatus(Zombie z, string status)
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
        }
        catch { }
    }

    /// <summary>E39 §2c: the plant-side counterpart to <see cref="ClearZombieStatus"/> — only
    /// `butter` has anywhere to write. Anything else reaching here (a broadcast's inapplicable arm)
    /// is correctly a no-op, not a bug: this plant never had that status in the first place, since
    /// <see cref="DebugActions.ApplyStatusToPlant"/> already refused to apply it.</summary>
    static void ClearPlantStatus(Plant p, string status)
    {
        if (string.IsNullOrEmpty(status) || status == "butter")
            try { p.butterP = 0; } catch { }
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
                "bullet" => SpawnBulletOnce(item, typeId, row, atk),
                // E40 (spec-spawn-non-grid.md §2a): widened, not a new opcode -- FA4 already carries
                // count/row/col/typeId, and every arm below reuses them exactly as declared on
                // AtomKindRegistry's spawn.entity schema.
                "pet" => SpawnPetOnce(typeId, row, col),
                "bucket" => SpawnBucketOnce(typeId, row, col),
                "mower" => SpawnMowerOnce(item, typeId, row),
                // coin: AtomKindRegistry.Validate refuses kind:"coin" at LOAD (spec §3 -- the
                // unverified CreateItem.SetCoin call-safety question), so a bound atom can never reach
                // this arm. It still needs one: the round-trip guard proving every domain value has an
                // executor arm (spec §4) must find "coin" here, and a future lift of the load-time
                // refusal should not also require a sink edit. Throwing (not returning false) makes an
                // unreachable-arm regression loud instead of a silent false.
                "coin" => throw new InvalidOperationException(
                    "SpawnEntity kind 'coin' reached the sink -- AtomKindRegistry.Validate should have " +
                    "refused it at load (spec-spawn-non-grid.md §3); this arm is a documented dead end, " +
                    "never a live path"),
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

    // E37 (spec-projectile-control.md §2a): the wiring gap this module closes — the sink used to
    // forward only typeId/bulletType/row/x and silently drop damage/y/moveWay/fromType, which is
    // exactly why a spawned bullet always priced at zero (CostFunction.cs's SpawnBody: hp==0 && atk==0)
    // and why an authored moveWay/fromType never reached SetBullet. DebugActions.SpawnBullet itself is
    // UNCHANGED — it already read every one of these keys (DebugActions.cs:143-154); only the sink's
    // own payload was incomplete.
    static bool SpawnBulletOnce(EffectActionPlanItem item, int typeId, int row, int atk)
    {
        var x = (float)JsonOverlay.GetDouble(item.Params, "x", 400);
        // y (kind=bullet only, §2a): SetBullet's own y argument.
        var y = (float)JsonOverlay.GetDouble(item.Params, "y", 0);

        // moveWay is a STRING in the atom (the swept BulletMoveWay member name, unrenamed) and an INT
        // in DebugActions.SpawnBullet's payload (BulletType's own moveWay argument is the enum itself,
        // and the JSON wire carries it as the enum's underlying ordinal) — this is where the two
        // meet. AtomKindRegistry's own Vocabulary check already refused anything the sweep did not
        // find, so a successful atom load guarantees this Enum.TryParse succeeds; the guard here is
        // defence in depth, not the real refusal.
        int? moveWayOrdinal = null;
        var moveWayName = JsonOverlay.GetString(item.Params, "moveWay");
        if (!string.IsNullOrEmpty(moveWayName)
            && System.Enum.TryParse<BulletMoveWay>(moveWayName, ignoreCase: false, out var moveWay))
            moveWayOrdinal = (int)moveWay;

        // fromType (kind=bullet only, §2a): Bullet.fromType (PlantType) — DebugActions.SpawnBullet
        // already reads it as an int and casts.
        var fromType = JsonOverlay.GetIntOrNull(item.Params, "fromType");

        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = typeId,
            ["bulletType"] = typeId,
            ["row"] = row,
            ["x"] = x,
            ["y"] = y,
            // atk -> damage at the payload boundary: the pricing path reads "atk" (CostFunction.cs),
            // DebugActions.SpawnBullet reads "damage" — the sink is where the two names meet, the same
            // translation §2a describes. Omitted (not zero) when unauthored, matching the zombie/plant
            // arms' own "atk > 0 ? atk : null" shape immediately above.
            ["damage"] = atk > 0 ? atk : (int?)null,
            ["moveWay"] = moveWayOrdinal,
            ["fromType"] = fromType,
        });
        return DebugActions.SpawnBullet(payload);
    }

    // E40 (spec-spawn-non-grid.md §2a): pet/bucket both place at a cell -- same payload shape as
    // SpawnPlantOnce (typeId/row/col), routed to their own DebugActions entry point because
    // MiniPet.SetPet / ItemManager.SetBucket are different game calls from CreatePlant.Instance.SetPlant.
    // col travels in the payload explicitly (not left to DebugActions' own CheatState.SpawnCol
    // fallback) so an authored col always reaches the placement -- dropping it here would silently
    // fall back to whatever CheatState.SpawnCol last held, the exact class of bug G1 exists to refuse.
    static bool SpawnPetOnce(int typeId, int row, int col)
    {
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = typeId,
            ["row"] = row,
            ["col"] = col,
        });
        return DebugActions.SpawnPet(payload);
    }

    static bool SpawnBucketOnce(int typeId, int row, int col)
    {
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = typeId,
            ["row"] = row,
            ["col"] = col,
        });
        return DebugActions.SpawnBucket(payload);
    }

    // E40: CreateMower.SetMower(MowerType, float x, int row) places by x/row, not col -- x is read
    // straight from item.Params the same way SpawnBulletOnce reads it (row/col/typeId/atk are already
    // parsed once in ExecSpawnEntity above; x is not, because only bullet needed it before this
    // module). Unauthored x defaults to 0 -- there is no in-repo precedent for "the" mower x (this
    // call site is UNCALLED before E40, per the spec's own §1 table), so 0 is a neutral placeholder,
    // not a researched game value; content authoring a mower spawn should supply x explicitly.
    static bool SpawnMowerOnce(EffectActionPlanItem item, int typeId, int row)
    {
        var x = (float)JsonOverlay.GetDouble(item.Params, "x", 0);
        var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["typeId"] = typeId,
            ["row"] = row,
            ["x"] = x,
        });
        return DebugActions.SpawnMower(payload);
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

    /// <summary>
    /// E35 (spec-match-modify.md §2.5): match.modify's own executor. The only writer for this kind is
    /// <c>CheatState</c> + <c>CheatActions.ApplyBoardConfig</c> — never a second `board.config` writer
    /// — which is what lets it inherit `BoardConfigLocked`'s re-application across boards
    /// (`GameHooks.cs`'s `Board.Awake` handler) and the `board.modifiers` publication for free.
    ///
    /// <para>The atom-layer magnitude is per-mille for the eight ratio fields and integer ms for the
    /// two intervals; both divide by 1000 exactly once, right here, at the boundary into
    /// `CheatState`'s own real-value convention (`E-ZH` etc already hold the actual ratio/seconds —
    /// `ApplyBoardConfig` applies no further scaling). `zombieStartAmmor` is the one true `long`
    /// magnitude and skips the division entirely: it travels through `JsonOverlay.GetLong` (never
    /// `GetDouble`, which would silently lose precision above 2^53) and `CheatState.SetLong` — no
    /// `float` hop anywhere on this path (§2.3).</para>
    ///
    /// <para>Records the written id in <see cref="MatchModifyWrites"/> so
    /// <c>EffectRuntime.NotifyMatchEnd</c> can restore ONLY what a live grant actually wrote (§2.6).
    /// Last-write-wins within a match: two atoms naming the same `field` do not stack — the executor
    /// assigns, it never accumulates, a consequence of the kind's own set-only semantics (§2.6).</para>
    /// </summary>
    static bool ExecModifyMatch(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        var field = JsonOverlay.GetString(item.Params, "field");
        var cheatId = MatchModifyCheatId(field);
        if (cheatId is null)
        {
            // E29's own not-yet-landed registry check aside, AtomKindRegistry.Validate's Vocabulary
            // loop already refuses an unrecognised `field` at bind time — this arm is defence in
            // depth, a named refusal rather than a silent no-op if it is ever reached anyway.
            return false;
        }

        if (string.Equals(field, "zombieStartAmmor", StringComparison.Ordinal))
            CheatState.SetLong(cheatId, JsonOverlay.GetLong(item.Params, "amount"), "effect");
        else
            CheatState.SetFloat(cheatId, JsonOverlay.GetDouble(item.Params, "amount") / 1000.0, "effect");

        MatchModifyWrites.Record(cheatId);
        CheatActions.ApplyBoardConfig();
        return true;
    }

    /// <summary>
    /// E36 (spec-wave-control.md §2.2/§2.3): wave.control's own executor. The <c>ChainDepth</c>
    /// recursion guard comes FIRST, before any op runs — <c>summon</c>/<c>huge</c> cause zombie
    /// spawns, which re-emit <c>zombie.place</c> -> OnSpawn and, through E34, <c>wave.spawn</c> ->
    /// OnWave; an atom bound to either and itself invoking wave.control would summon forever on the
    /// Unity main thread, and that is the one failure mode in this whole module that cannot be
    /// diagnosed after the fact. Returning <c>false</c> here is a real failure in this sink's "stop
    /// seq on first failure" convention (this file's own class doc), which is exactly what stops the
    /// loop before it starts.
    ///
    /// <para>All four ops call an existing <c>CheatActions</c>/<c>DebugActions</c> entry point —
    /// no new host write (§3). <c>timerMs</c> is integer ms, divided by 1000 exactly once, right
    /// here, at the boundary into <c>CheatActions.SetWaveTimer</c>'s own float-seconds convention.
    /// <c>hold</c> is the only op with state (<c>F-WAVE-FREEZE</c>), cleared by
    /// <c>EffectRuntime.NotifyMatchEnd</c> so a bound hold can never outlive its match (§2.5).</para>
    /// </summary>
    static bool ExecWaveControl(EffectExecuteContext ctx, EffectActionPlanItem item)
    {
        if (ctx.Event.ChainDepth > 0)
        {
            CheatState.Error(
                "wave.control refused at ChainDepth " + ctx.Event.ChainDepth + " -- summon/huge " +
                "re-emit the events that could re-trigger this same atom (spec-wave-control.md §2.3)");
            return false;
        }

        var op = JsonOverlay.GetString(item.Params, "op");
        switch (op)
        {
            case "summon":
                CheatActions.SummonWave(JsonOverlay.GetInt(item.Params, "wave", 1));
                return true;
            case "huge":
                CheatActions.HugeWave();
                return true;
            case "setTimer":
                CheatActions.SetWaveTimer(JsonOverlay.GetInt(item.Params, "timerMs", 0) / 1000f);
                return true;
            case "hold":
                DebugActions.WaveFreeze(JsonOverlay.GetBool(item.Params, "enabled"));
                return true;
            default:
                // AtomKindRegistry.Validate's own op vocabulary already refuses this at bind time --
                // defence in depth, a named refusal rather than a silent no-op if reached anyway.
                CheatState.Error("wave.control: unrecognised op '" + op + "'");
                return false;
        }
    }

    /// <summary>`field` -> its real `E-*` cheat id (`CheatActions.cs:641-664`, re-verified against the
    /// live switch before this module shipped).</summary>
    static string? MatchModifyCheatId(string? field) => field switch
    {
        "zombieHealthMultiplier" => "E-ZH",
        "zombieDamageMultiplier" => "E-ZD",
        "zombieSpeedMultiplier" => "E-ZS",
        "zombieCountMultiplier" => "E-ZC",
        "zombieStartAmmor" => "E-ZARM",
        "plantModifyMin" => "E-PMIN",
        "plantModifyMax" => "E-PMAX",
        "zombieModifyMin" => "E-ZMIN",
        "zombieModifyMax" => "E-ZMAX",
        "waveInterval" => "E-WAVE-I",
        "conveyInterval" => "E-CONV-I",
        _ => null,
    };
}
