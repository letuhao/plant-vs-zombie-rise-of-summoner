using System.Collections.Concurrent;
using System.Text.Json;
using FusionRpg.CheatCore;
using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Injector.Effects;
using FusionRpg.Injector.Fx;
using FusionRpg.Injector.Host;
using FusionRpg.Injector.Stats;

namespace FusionRpg.Injector;

/// <summary>Main-thread drain for SignalR/HTTP cheat commands from the web/server.</summary>
public static class CheatCommandRunner
{
    static readonly ConcurrentQueue<CommandDto> Pending = new();
    static readonly ConcurrentDictionary<string, byte> SeenIds = new();
    const int SeenCap = 512;

    public static void Enqueue(CommandDto cmd)
    {
        if (cmd == null || string.IsNullOrWhiteSpace(cmd.Name)) return;
        if (!string.IsNullOrWhiteSpace(cmd.Id))
        {
            if (!SeenIds.TryAdd(cmd.Id, 0)) return;
            if (SeenIds.Count > SeenCap)
            {
                // Bound memory: drop oldest-ish by clearing when oversized.
                SeenIds.Clear();
                SeenIds.TryAdd(cmd.Id, 0);
            }
        }
        Pending.Enqueue(cmd);
        try { RpgHost.Log.Info("[cheat-cmd] queued " + cmd.Name + (string.IsNullOrEmpty(cmd.Id) ? "" : " id=" + cmd.Id)); }
        catch { }
    }

    public static void Drain()
    {
        while (Pending.TryDequeue(out var cmd))
        {
            try { Run(cmd); }
            catch (Exception ex) { CheatState.Error("cmd " + cmd.Name + ": " + ex.Message); }
        }
    }

    static void Run(CommandDto cmd)
    {
        var name = cmd.Name.Trim().ToLowerInvariant();
        if (name is "reload-stats" or "stats.reload")
        {
            if (RpgHost.Client != null)
                CheatState.PullFromServer(RpgHost.Client.Stats);
            return;
        }
        if (name is "pvz.stats.reload")
        {
            if (RpgHost.Client != null)
                _ = RpgHost.Client.RefreshPvzStatsAsync();
            return;
        }
        if (name is "pvz.spawn.extra")
        {
            var p = PayloadJson(cmd.Payload);
            var typeId = IntProp(p, "typeId", CheatState.ManualTypeId);
            int? row = null;
            int? col = null;
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("row", out var rowEl) && rowEl.ValueKind == JsonValueKind.Number && rowEl.TryGetInt32(out var rv))
                row = rv;
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("col", out var colEl) && colEl.ValueKind == JsonValueKind.Number && colEl.TryGetInt32(out var cv))
                col = cv;
            var reason = Str(p, "reason");
            var corr = Str(p, "correlationId");
            var side = Str(p, "side") ?? "zombie";
            var instanceId = Str(p, "instanceId");
            var loadoutJson = LoadoutJsonFromPayload(p);
            CheatActions.SpawnExtra(side, typeId, col, row, reason, corr, instanceId, loadoutJson);
            return;
        }
        if (name is "unique.binding.clear")
        {
            var p = PayloadJson(cmd.Payload);
            Match.MatchHost.TryClearUniquePending(Str(p, "instanceId"), Str(p, "correlationId"));
            return;
        }
        if (name is "effects.reload")
        {
            Effects.EffectRuntime.ReplaceCatalog(FusionRpg.Core.Effects.EffectSeedCatalog.CreateAll());
            return;
        }
        if (name is "effects.grants.apply")
        {
            RunEffectsGrantsApply(PayloadJson(cmd.Payload));
            return;
        }
        if (name is "cheat.set" or "cheat.apply-snapshot")
        {
            var json = PayloadJson(cmd.Payload);
            TakeProbeContext(json, "web");
            if (json.ValueKind != JsonValueKind.Undefined && json.ValueKind != JsonValueKind.Null)
                CheatState.ApplySnapshot(json);
            CheatState.Note("web: applied cheat snapshot");
            CheatState.EmitInject("web", "action", action: "apply-snapshot");
            return;
        }
        if (name is "cheat.probe-begin")
        {
            var p = PayloadJson(cmd.Payload);
            var probeId = Str(p, "probeId") ?? Guid.NewGuid().ToString("N");
            var packId = Str(p, "packId");
            CheatState.BeginProbe(probeId, packId);
            CheatState.EmitInject("pack", "pack-step", action: "probe-begin", id: packId);
            return;
        }
        if (name is "cheat.probe-end")
        {
            CheatState.EndProbe(Str(PayloadJson(cmd.Payload), "reason") ?? "web");
            return;
        }
        if (name is "cheat.probe-run")
        {
            RunLocalPack(PayloadJson(cmd.Payload));
            return;
        }
        if (name is "cheat.toggle")
        {
            var p = PayloadJson(cmd.Payload);
            TakeProbeContext(p, "web");
            var id = Str(p, "id") ?? "";
            var on = p.TryGetProperty("enabled", out var en) && en.GetBoolean();
            var src = Str(p, "source") ?? (string.IsNullOrEmpty(CheatState.ActivePackId) ? "web" : "pack");
            if (!string.IsNullOrEmpty(id)) CheatState.SetToggle(id, on, src);
            return;
        }
        if (name is "cheat.clear-field")
        {
            var p = PayloadJson(cmd.Payload);
            TakeProbeContext(p, "web");
            var id = Str(p, "id") ?? "";
            var src = Str(p, "source") ?? "web";
            if (!string.IsNullOrEmpty(id))
            {
                CheatState.ClearField(id, src);
                if (id.StartsWith("A-", StringComparison.Ordinal))
                    CheatActions.PushScalesNow();
            }
            return;
        }
        if (name is "cheat.set-float")
        {
            var p = PayloadJson(cmd.Payload);
            TakeProbeContext(p, "web");
            var id = Str(p, "id") ?? "";
            var v = p.TryGetProperty("value", out var fv) ? fv.GetDouble() : 0d;
            var src = Str(p, "source") ?? (string.IsNullOrEmpty(CheatState.ActivePackId) ? "web" : "pack");
            if (!string.IsNullOrEmpty(id))
            {
                CheatState.SetFloat(id, v, src);
                // Tab A floats must affect living entities — set-float alone only updated registry.
                if (id.StartsWith("A-", StringComparison.Ordinal))
                    CheatActions.PushScalesNow();
            }
            return;
        }
        if (name is "cheat.action")
        {
            var p = PayloadJson(cmd.Payload);
            TakeProbeContext(p, "web");
            var action = Str(p, "action") ?? "";
            var src = Str(p, "source") ?? (string.IsNullOrEmpty(CheatState.ActivePackId) ? "web" : "pack");
            if (!string.IsNullOrEmpty(action))
                CheatState.EmitActionInject(action, src);
            RunAction(action, p);
            return;
        }
        if (name.StartsWith("debug.", StringComparison.Ordinal))
        {
            RunDebug(name, PayloadJson(cmd.Payload));
            return;
        }
        if (name.StartsWith("cheat.", StringComparison.Ordinal))
            RunAction(name["cheat.".Length..], PayloadJson(cmd.Payload));
    }

    static void RunDebug(string name, JsonElement p)
    {
        switch (name)
        {
            case "debug.run-steps":
                RunDebugSteps(p);
                break;
            case "debug.session":
                if (string.Equals(Str(p, "op"), "end", StringComparison.OrdinalIgnoreCase))
                    DebugRuntime.EndSession();
                else
                    DebugRuntime.StartSession(Str(p, "scenarioId"));
                break;
            case "debug.spawn-plant":
                DebugActions.SpawnPlant(p);
                break;
            case "debug.spawn-zombie":
                DebugActions.SpawnZombie(p);
                break;
            case "debug.spawn-bullet":
                DebugActions.SpawnBullet(p);
                break;
            case "debug.reset-mods":
                DebugActions.ResetMods();
                break;
            case "debug.set-mods":
                DebugActions.SetMods(p);
                break;
            case "debug.reapply":
                CheatActions.ReapplyAllLiving();
                break;
            case "debug.apply-status":
                DebugActions.ApplyStatus(p, method: true);
                break;
            case "debug.apply-status-float":
                DebugActions.ApplyStatus(p, method: false);
                break;
            case "debug.clear-status":
                DebugActions.ClearStatus(p);
                break;
            case "debug.arm":
                DebugRuntime.ApplyPayloadArm(p);
                DebugRuntime.Emit("debug.arm", new Dictionary<string, object>
                {
                    ["kind"] = Str(p, "kind") ?? ""
                });
                break;
            case "debug.disarm":
                DebugRuntime.DisarmAll();
                DebugRuntime.Emit("debug.disarm", new Dictionary<string, object>());
                break;
            case "debug.kill":
                DebugActions.Kill(p, plants: false);
                break;
            case "debug.kill-plant":
                DebugActions.Kill(p, plants: true);
                break;
            case "debug.wave-freeze":
                DebugActions.WaveFreeze(p.TryGetProperty("enabled", out var en) && en.ValueKind != JsonValueKind.False);
                break;
            case "debug.ensure-sun":
                DebugActions.EnsureSun(p.TryGetProperty("value", out var sv) && sv.TryGetSingle(out var sf) ? sf : 9999f);
                break;
            case "debug.select":
                DebugActions.Select(p);
                break;
            case "debug.spawn-cell":
                if (p.TryGetProperty("col", out var c) && c.TryGetInt32(out var col)) CheatState.SpawnCol = col;
                if (p.TryGetProperty("row", out var r) && r.TryGetInt32(out var row)) CheatState.SpawnRow = row;
                break;
            case "debug.reset-board":
                CheatActions.DeleteAllPlants();
                CheatActions.DeleteAllZombies(); // also clears DeadZombies
                break;
            case "debug.clear-plants":
                CheatActions.DeleteAllPlants();
                break;
            case "debug.clear-zombies":
                CheatActions.DeleteAllZombies();
                break;
            case "debug.snapshot":
                DebugRuntime.Emit("debug.snapshot", DebugRuntime.Snapshot());
                break;
            case "debug.board-stats":
            {
                var payload = DebugRuntime.BoardEntityStats();
                if (p.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind == JsonValueKind.String)
                {
                    var tag = tagEl.GetString();
                    if (!string.IsNullOrEmpty(tag))
                        payload["tag"] = tag!;
                }

                DebugRuntime.Emit("debug.board-stats", payload);
                break;
            }
            case "debug.economy":
                DebugActions.Economy(p);
                break;
            case "debug.board-config":
                DebugActions.BoardConfigApply(p);
                break;
            case "debug.board-action":
                DebugActions.BoardAction(p);
                break;
            case "debug.spawn-grid":
                DebugActions.SpawnGrid(p);
                break;
            case "debug.clear-grid":
                DebugActions.ClearGrid(p);
                break;
            case "debug.set-box":
                DebugActions.SetBox(p);
                break;
            case "debug.grid-query":
                DebugActions.GridQuery(p);
                break;
            case "debug.ice-road":
                DebugActions.IceRoad(p);
                break;
            case "debug.effect.grant":
                RunEffectGrant(p);
                break;
            case "debug.effect.withdraw":
                Effects.EffectRuntime.Withdraw(Str(p, "grantId") ?? "");
                break;
            case "debug.effect.clear":
                Effects.EffectRuntime.ClearAll("debug");
                break;
            case "debug.effect.list":
            {
                var snap = Effects.EffectRuntime.Snapshot();
                DebugRuntime.Emit("debug.effect.list", new Dictionary<string, object>
                {
                    ["contractVersion"] = snap.ContractVersion,
                    ["defs"] = snap.Defs.Count,
                    ["grants"] = snap.Grants.Count,
                    ["grantIds"] = snap.Grants.Select(g => g.GrantId).ToList()
                });
                break;
            }
            case "debug.effect.fire-synthetic":
            {
                var selected = CheatState.SelectedPtr == IntPtr.Zero
                    ? null
                    : CheatState.SelectedPtr.ToString("X");
                var actor = Str(p, "actorPtr") ?? selected;
                var target = Str(p, "targetPtr") ?? selected;
                var ev = new EffectEventDto
                {
                    Trigger = Str(p, "trigger") ?? EffectTriggers.OnDamageDealt,
                    MatchKey = GameHooks.MatchKey,
                    Side = Str(p, "side") ?? "plant",
                    ActorPtr = actor,
                    TargetPtr = target,
                    TypeId = p.TryGetProperty("typeId", out var tid) && tid.TryGetInt32(out var t) ? t : null,
                    TargetTypeId = p.TryGetProperty("targetTypeId", out var ttid) && ttid.TryGetInt32(out var tt) ? tt : null,
                    Tick = Effects.EffectRuntime.NextTick(),
                    ScenarioId = DebugRuntime.ScenarioId
                };
                var plan = Effects.EffectRuntime.FireSynthetic(ev);
                DebugRuntime.Emit("debug.effect.synthetic", new Dictionary<string, object>
                {
                    ["trigger"] = plan.Trigger,
                    ["actions"] = plan.Actions.Count,
                    ["skipped"] = plan.Skipped
                });
                break;
            }
            case "debug.effect.enqueue-delta":
                RunEnqueueDelta(p);
                break;
            case "debug.effect.board-snapshot":
                EmitBoardSnapshot();
                break;
            case "debug.effect.dots":
                EmitStatus(includeLegacyDots: true);
                break;
            case "debug.effect.counters":
                EmitStatus(includeLegacyCounters: true);
                break;
            case "debug.status":
                EmitStatus();
                break;
            case "debug.status.apply":
                ApplyStatusL2(p);
                break;
            case "debug.actor-derived":
                HandleActorDerived(p);
                break;
            case "debug.fx.probe-shaders":
                RunShaderProbe();
                break;
            case "debug.fx.world-flash":
                RunWorldFlash(p);
                break;
            default:
                CheatState.Error("unknown debug cmd: " + name);
                break;
        }
    }

    /// <summary>Execute scenario steps sequentially on the Unity main-thread drain (no same-frame fan-out race).</summary>
    static void RunDebugSteps(JsonElement p)
    {
        if (!p.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
        {
            CheatState.Error("debug.run-steps: missing steps[]");
            return;
        }

        var scenarioId = Str(p, "scenarioId");
        if (!string.IsNullOrEmpty(scenarioId) && !DebugRuntime.SessionActive)
            DebugRuntime.StartSession(scenarioId);

        var n = 0;
        foreach (var step in steps.EnumerateArray())
        {
            var name = Str(step, "name") ?? "";
            if (string.IsNullOrEmpty(name)) continue;
            if (string.Equals(name, "debug.run-steps", StringComparison.Ordinal))
            {
                CheatState.Error("debug.run-steps: nested run-steps ignored");
                continue;
            }

            var payload = step.TryGetProperty("payload", out var pl) ? pl : default;
            try
            {
                if (string.Equals(name, "pvz.spawn.extra", StringComparison.Ordinal))
                {
                    var typeId = payload.TryGetProperty("typeId", out var t) && t.TryGetInt32(out var ti) ? ti : 0;
                    int? row = payload.TryGetProperty("row", out var r) && r.TryGetInt32(out var rv) ? rv : null;
                    int? col = payload.TryGetProperty("col", out var c) && c.TryGetInt32(out var cv) ? cv : null;
                    var reason = Str(payload, "reason") ?? "debug";
                    var corr = Str(payload, "correlationId") ?? Guid.NewGuid().ToString("N");
                    var side = Str(payload, "side") ?? "zombie";
                    var instanceId = Str(payload, "instanceId");
                    var loadoutJson = LoadoutJsonFromPayload(payload);
                    CheatActions.SpawnExtra(side, typeId, col, row, reason, corr, instanceId, loadoutJson);
                }
                else if (name.StartsWith("debug.", StringComparison.Ordinal))
                    RunDebug(name, payload);
                else
                    CheatState.Error("debug.run-steps: unknown step " + name);
                n++;
            }
            catch (Exception ex)
            {
                CheatState.Error("debug.run-steps step " + name + ": " + ex.Message);
            }
        }

        DebugRuntime.Emit("debug.run-steps.done", new Dictionary<string, object>
        {
            ["scenarioId"] = scenarioId ?? DebugRuntime.ScenarioId ?? "",
            ["steps"] = n
        });
    }

    /// <summary>F8 / local: apply a named pack without server fan-out.</summary>
    public static void RunPackLocal(string packId)
    {
        var pack = ProbePacks.Get(packId);
        if (pack == null)
        {
            CheatState.Error("unknown pack " + packId);
            return;
        }
        var probeId = Guid.NewGuid().ToString("N");
        CheatState.BeginProbe(probeId, pack.Id);
        CheatState.EmitInject("pack", "pack-step", action: "probe-begin", id: pack.Id);
        foreach (var step in pack.Steps)
            ApplyStep(step, "pack");
        CheatState.Note($"pack {pack.Id} applied probeId={probeId}");
    }

    static void RunLocalPack(JsonElement p)
    {
        var packId = Str(p, "packId") ?? "";
        var probeId = Str(p, "probeId") ?? Guid.NewGuid().ToString("N");
        var pack = ProbePacks.Get(packId);
        if (pack == null)
        {
            CheatState.Error("unknown pack " + packId);
            return;
        }
        CheatState.BeginProbe(probeId, pack.Id);
        CheatState.EmitInject("pack", "pack-step", action: "probe-begin", id: pack.Id);
        foreach (var step in pack.Steps)
            ApplyStep(step, "pack");
    }

    static void ApplyStep(ProbeStep step, string source)
    {
        switch (step.Op.ToLowerInvariant())
        {
            case "toggle":
                if (!string.IsNullOrEmpty(step.Id))
                    CheatState.SetToggle(step.Id!, step.Enabled ?? true, source);
                break;
            case "set-float":
                if (!string.IsNullOrEmpty(step.Id))
                    CheatState.SetFloat(step.Id!, step.Value ?? 0, source);
                break;
            case "action":
                if (!string.IsNullOrEmpty(step.Action))
                {
                    CheatState.EmitActionInject(step.Action!, source);
                    var dict = new Dictionary<string, object?> { ["action"] = step.Action };
                    if (step.Which != null) dict["which"] = step.Which;
                    if (step.Value is { } v) dict["value"] = v;
                    if (step.Add is { } a) dict["add"] = a;
                    var json = JsonSerializer.Serialize(dict);
                    using var doc = JsonDocument.Parse(json);
                    RunAction(step.Action!, doc.RootElement.Clone());
                }
                break;
        }
    }

    static void TakeProbeContext(JsonElement p, string defaultSource)
    {
        if (p.ValueKind != JsonValueKind.Object) return;
        var probeId = Str(p, "probeId");
        if (!string.IsNullOrEmpty(probeId))
        {
            var packId = Str(p, "packId") ?? CheatState.ActivePackId;
            if (!string.Equals(CheatState.ActiveProbeId, probeId, StringComparison.Ordinal))
                CheatState.BeginProbe(probeId!, packId);
            else
            {
                CheatState.ActiveProbeUtc = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(packId)) CheatState.ActivePackId = packId;
            }
        }
        if (p.TryGetProperty("correlationId", out var c) && c.ValueKind == JsonValueKind.String)
            CheatState.ActiveCorrelationId = c.GetString();
        _ = defaultSource;
    }

    static void RunAction(string action, JsonElement p)
    {
        switch (action.ToLowerInvariant())
        {
            case "reapply":
            case "reapply-living":
                CheatActions.ReapplyAllLiving();
                break;
            case "push-now":
            case "push-scales":
                CheatActions.PushScalesNow();
                break;
            case "apply-plants":
                CheatActions.ApplyAbsolutesToSelectedOrAll("plant");
                break;
            case "apply-zombies":
                CheatActions.ApplyAbsolutesToSelectedOrAll("zombie");
                break;
            case "spawn-plant":
                CheatActions.SpawnPlant(IntProp(p, "type", CheatState.ManualTypeId));
                break;
            case "spawn-zombie":
                CheatActions.SpawnZombie(IntProp(p, "type", CheatState.ManualTypeId), BoolProp(p, "mindControl", false));
                break;
            case "delete-plants":
                CheatActions.DeleteAllPlants();
                break;
            case "delete-zombies":
            case "kill-zombies":
                CheatActions.KillAllZombies();
                break;
            case "hypno-all":
                CheatActions.HypnoAll();
                break;
            case "oneshot":
                CheatActions.OneShotSelected();
                break;
            case "economy":
                CheatActions.SetEconomy(
                    p.TryGetProperty("which", out var w) ? w.GetString() ?? "sun" : "sun",
                    p.TryGetProperty("value", out var v) ? (float)v.GetDouble() : 0,
                    BoolProp(p, "add", false));
                break;
            case "board-config":
                CheatActions.ApplyBoardConfig();
                break;
            case "load-board-config":
                CheatActions.LoadBoardConfigIntoCheats();
                break;
            case "summon":
                CheatActions.SummonWave(IntProp(p, "wave", 1));
                break;
            case "huge-wave":
                CheatActions.HugeWave();
                break;
            case "wave-timer":
                CheatActions.SetWaveTimer(p.TryGetProperty("value", out var wt) ? (float)wt.GetDouble() : 5f);
                break;
            case "recipes":
                CheatActions.DumpRecipes();
                break;
            case "reinforce":
                CheatActions.ReinforceSelected();
                break;
            case "set-zombie-hp":
                CheatActions.SetSelectedZombieHealth(IntProp(p, "hp", 27000));
                break;
            case "spawn-pet":
                CheatActions.SpawnPet(IntProp(p, "type", 0));
                break;
            case "spawn-grid":
                CheatActions.SpawnGrid(IntProp(p, "type", 0));
                break;
            case "spawn-bucket":
                CheatActions.SpawnBucket(IntProp(p, "type", 0));
                break;
            case "present":
                CheatActions.TriggerPresent();
                break;
            case "travel-buff":
                CheatActions.TravelBuffStub();
                break;
            case "clear-selection":
                CheatState.ClearSelection();
                break;
            case "clear-failed":
                SpawnCatalog.ClearFailed();
                break;
            case "clear-field":
            {
                var cid = Str(p, "id") ?? "";
                CheatState.ClearField(cid, Str(p, "source") ?? "web");
                if (cid.StartsWith("A-", StringComparison.Ordinal))
                    CheatActions.PushScalesNow();
                break;
            }
            case "reset-all":
                CheatState.ResetAll();
                CheatActions.PushScalesNow();
                break;
            case "reset-group":
                CheatState.ResetGroup(p.TryGetProperty("prefix", out var pref) ? pref.GetString() ?? "A-" : "A-");
                CheatActions.PushScalesNow();
                break;
            case "push-stats":
                CheatActions.PushStatsToServer();
                break;
            case "pull-stats":
                CheatActions.PullStatsFromServer();
                break;
            case "set-spawn-cell":
                if (p.TryGetProperty("col", out var col)) CheatState.SpawnCol = col.GetInt32();
                if (p.TryGetProperty("row", out var row)) CheatState.SpawnRow = row.GetInt32();
                break;
            case "save-persist":
                CheatState.PersistEnabled = true;
                CheatState.MaybeSave();
                break;
            case "reload-persist":
                CheatState.TryLoad();
                break;
            case "run-pack":
                RunPackLocal(Str(p, "packId") ?? "");
                break;
            default:
                if (!CheatActionNames.IsKnown(action))
                    CheatState.Error("unknown action " + action);
                else
                    CheatState.Error("unhandled known action " + action);
                break;
        }
    }

    static JsonElement PayloadJson(object? payload)
    {
        if (payload is JsonElement el) return el;
        if (payload == null) return default;
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch { return default; }
    }

    static void RunEffectsGrantsApply(JsonElement p)
    {
        try
        {
            if (p.ValueKind != JsonValueKind.Object ||
                !p.TryGetProperty("grants", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                CheatState.Error("effects.grants.apply: missing grants[]");
                return;
            }

            var applied = 0;
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                RunEffectGrant(item);
                applied++;
            }

            DebugRuntime.Emit("debug.effect.rehydrated", new Dictionary<string, object>
            {
                ["count"] = applied,
                ["grants"] = Effects.EffectRuntime.Snapshot().Grants.Count
            });
        }
        catch (Exception ex)
        {
            CheatState.Error("effects.grants.apply: " + ex.Message);
            DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object> { ["error"] = ex.Message });
        }
    }

    static void RunEnqueueDelta(JsonElement p)
    {
        try
        {
            if (HasAbsoluteHpKeys(p))
            {
                CheatState.Error("debug.effect.enqueue-delta: absolute HP rejected");
                DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object>
                {
                    ["error"] = "absolute-hp",
                    ["command"] = "debug.effect.enqueue-delta"
                });
                return;
            }

            var mode = Str(p, "mode");
            if (!string.IsNullOrWhiteSpace(mode) &&
                !string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase))
            {
                CheatState.Error("debug.effect.enqueue-delta: mode-set rejected");
                DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object>
                {
                    ["error"] = "mode-set",
                    ["command"] = "debug.effect.enqueue-delta"
                });
                return;
            }

            var ptr = ResolveDeltaTargetPtr(p);
            if (string.IsNullOrWhiteSpace(ptr) && !HasCellAnchor(p))
            {
                CheatState.Error("debug.effect.enqueue-delta: missing target");
                DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object>
                {
                    ["error"] = "missing-target",
                    ["command"] = "debug.effect.enqueue-delta"
                });
                return;
            }

            var amount = LongProp(p, "amount", 0);
            DamageFxTag? tag = null;
            var tagRaw = Str(p, "tag");
            if (!string.IsNullOrWhiteSpace(tagRaw) &&
                Enum.TryParse<DamageFxTag>(tagRaw, ignoreCase: true, out var parsed))
                tag = parsed;

            Effects.EffectRuntime.Ensure();
            Effects.EffectRuntime.FreezeBoard();
            var funnel = Effects.EffectRuntime.Bag.Funnel;
            if (funnel == null)
            {
                CheatState.Error("debug.effect.enqueue-delta: funnel missing");
                return;
            }

            var hasTargetSpec = p.TryGetProperty("target", out var tEl) && tEl.ValueKind == JsonValueKind.Object;
            var ok = true;
            var resolved = 0;
            if (hasTargetSpec)
            {
                var overlay = JsonElementToOverlay(p);
                var packet = DamagePacketBuilder.FromOverlay(overlay, new EffectEventDto
                {
                    TargetPtr = ptr,
                    ActorPtr = ptr,
                    Tick = Effects.EffectRuntime.NextTick()
                }, grantId: "debug.enqueue-delta", pluginId: "debug");
                Effects.EffectRuntime.BindSelectedTarget(packet);
                resolved = CombatDamageDispatcher.DispatchInstant(
                    packet,
                    Effects.EffectRuntime.Bag.BoardSnapshot,
                    new EffectEventDto { TargetPtr = ptr, ActorPtr = ptr },
                    funnel,
                    Effects.EffectRuntime.Bag.CombatPolicy,
                    Effects.EffectRuntime.Bag.CombatRng,
                    Effects.EffectRuntime.Bag.CombatMath);
                ok = resolved > 0 || amount == 0;
            }
            else if (amount != 0)
            {
                ok &= funnel.EnqueueMutation("entity:" + ptr, amount, pluginId: "debug");
                resolved = ok ? 1 : 0;
            }

            if (tag.HasValue)
            {
                ok &= funnel.EnqueuePresent(new DamageFxDto
                {
                    TargetPtr = ptr,
                    Amount = amount,
                    Tag = tag.Value,
                    Fx = Str(p, "fx") ?? "float"
                });
            }

            funnel.Flush();
            DebugRuntime.Emit("debug.effect.enqueue-delta", new Dictionary<string, object>
            {
                ["ptr"] = ptr,
                ["amount"] = amount,
                ["tag"] = tag?.ToString() ?? "",
                ["ok"] = ok,
                ["resolved"] = resolved
            });
            if (hasTargetSpec)
            {
                DebugRuntime.Emit("debug.combat.packet", new Dictionary<string, object>
                {
                    ["fa10"] = resolved,
                    ["source"] = "enqueue-delta"
                });
            }
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.effect.enqueue-delta: " + ex.Message);
            DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["command"] = "debug.effect.enqueue-delta"
            });
        }
    }

    static bool HasAbsoluteHpKeys(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return false;
        foreach (var prop in p.EnumerateObject())
        {
            if (prop.Name.Equals("setHp", StringComparison.OrdinalIgnoreCase) ||
                prop.Name.Equals("absoluteHp", StringComparison.OrdinalIgnoreCase) ||
                prop.Name.Equals("hp", StringComparison.OrdinalIgnoreCase) ||
                prop.Name.Equals("EntityFinal.Hp", StringComparison.OrdinalIgnoreCase) ||
                prop.Name.Equals("entityFinalHp", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static bool HasCellAnchor(JsonElement p)
    {
        if (!p.TryGetProperty("target", out var t) || t.ValueKind != JsonValueKind.Object)
            return false;
        if (!t.TryGetProperty("anchor", out var a) || a.ValueKind != JsonValueKind.Object)
            return false;
        return a.TryGetProperty("row", out _) && a.TryGetProperty("col", out _);
    }

    static void EmitBoardSnapshot()
    {
        Effects.EffectRuntime.FreezeBoard();
        var ents = Effects.EffectRuntime.Bag.BoardSnapshot.Entities
            .Select(e => new Dictionary<string, object>
            {
                ["ptr"] = e.Ptr,
                ["side"] = e.Side,
                ["typeId"] = e.TypeId,
                ["col"] = e.Col,
                ["row"] = e.Row,
                ["living"] = e.Living,
                ["mindControlled"] = e.MindControlled
            })
            .ToList();
        DebugRuntime.Emit("debug.effect.board-snapshot", new Dictionary<string, object>
        {
            ["count"] = ents.Count,
            ["entities"] = ents,
            ["selectedPtr"] = Effects.EffectRuntime.Bag.SelectedPtr ?? ""
        });
    }

    static void HandleActorDerived(JsonElement p)
    {
        Effects.EffectRuntime.Ensure();
        var ptr = Str(p, "ptr");
        if (string.IsNullOrWhiteSpace(ptr) && CheatState.SelectedPtr != IntPtr.Zero)
            ptr = CheatState.SelectedPtr.ToString("X");

        var profile = Str(p, "profile") ?? Str(p, "derivedProfile");
        Dictionary<string, double>? overlay = null;
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("channels", out var ch) && ch.ValueKind == JsonValueKind.Object)
            overlay = ReadDoubleMap(ch);
        else if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("derived", out var d) && d.ValueKind == JsonValueKind.Object)
            overlay = ReadDoubleMap(d);

        if (!string.IsNullOrWhiteSpace(profile) || (overlay != null && overlay.Count > 0))
        {
            if (string.IsNullOrWhiteSpace(ptr))
            {
                CheatState.Error("debug.actor-derived: ptr required to pin");
                return;
            }

            try
            {
                InjectorDerivedOverride.PinProfile(ptr, profile, overlay);
            }
            catch (Exception ex)
            {
                CheatState.Error("debug.actor-derived pin: " + ex.Message);
                return;
            }
        }

        EmitActorDerived(ptr);
    }

    static Dictionary<string, double> ReadDoubleMap(JsonElement obj)
    {
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.TryGetDouble(out var v))
                map[prop.Name] = v;
            else if (prop.Value.TryGetInt64(out var l))
                map[prop.Name] = l;
        }

        return map;
    }

    static void ApplyStatusL2(JsonElement p)
    {
        Effects.EffectRuntime.Ensure();
        Effects.EffectRuntime.FreezeBoard();
        var statusId = Str(p, "statusId") ?? Str(p, "status");
        if (string.IsNullOrWhiteSpace(statusId))
        {
            CheatState.Error("debug.status.apply: statusId required");
            return;
        }

        var hostPtr = Str(p, "hostPtr") ?? Str(p, "targetPtr");
        if (string.IsNullOrWhiteSpace(hostPtr) && CheatState.SelectedPtr != IntPtr.Zero)
            hostPtr = CheatState.SelectedPtr.ToString("X");
        if (string.IsNullOrWhiteSpace(hostPtr))
        {
            CheatState.Error("debug.status.apply: hostPtr required");
            return;
        }

        var attackerPtr = Str(p, "attackerPtr");
        var durationMs = IntProp(p, "durationMs", 4000);
        var amount = LongProp(p, "amount", 0);
        var now = DateTimeOffset.UtcNow;
        var runtime = Effects.EffectRuntime.Status;
        StatusDef def;
        try
        {
            def = runtime.Catalog.GetRequired(statusId);
        }
        catch (UnknownStatusIdException ex)
        {
            CheatState.Error("debug.status.apply: " + ex.Message);
            return;
        }

        var input = new StatusApplyInput(
            statusId,
            hostPtr,
            attackerPtr,
            GrantId: "debug-status-apply",
            BaseMagnitude: amount,
            BaseDuration: durationMs,
            PeriodMs: IntProp(p, "periodMs", 1000),
            DurationMs: durationMs,
            TickBudget: IntProp(p, "tickBudget", 1),
            GrantChance: 1.0,
            AttackerLess: string.IsNullOrWhiteSpace(attackerPtr));
        var outcome = runtime.Apply(input, Effects.EffectRuntime.Bag.StatusRng, now);
        if (outcome.Applied
            && def.PayloadKinds.Contains(StatusPayloadKind.UnityCc))
        {
            var durationSec = (float)Math.Max(0.1, (outcome.Instance?.EffectiveDuration ?? durationMs) / 1000.0);
            foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
            {
                if (z == null) continue;
                if (!string.Equals(GameDumps.Ptr(z), hostPtr, StringComparison.OrdinalIgnoreCase)) continue;
                DebugActions.ApplyStatusToZombie(z, statusId, durationSec, 1, method: true);
                break;
            }
        }

        EmitStatus();
    }

    static void EmitActorDerived(string? ptr)
    {
        Effects.EffectRuntime.Ensure();
        if (string.IsNullOrWhiteSpace(ptr))
        {
            CheatState.Error("debug.actor-derived: ptr required");
            return;
        }

        var key = ptr.Trim();
        var derived = InjectorStatusBridge.ResolveDerived(key, attackerLess: false);
        var channels = derived.Channels
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        DebugRuntime.Emit("debug.actor-derived", new Dictionary<string, object>
        {
            ["ptr"] = key,
            ["tierPower"] = derived.TierPower,
            ["channels"] = channels
        });
    }

    static void EmitStatus(bool includeLegacyDots = false, bool includeLegacyCounters = false)
    {
        Effects.EffectRuntime.Ensure();
        var now = DateTimeOffset.UtcNow;
        var status = Effects.EffectRuntime.Status;
        var instances = status.AllInstances()
            .Select(i => new Dictionary<string, object>
            {
                ["instanceId"] = i.InstanceId,
                ["statusId"] = i.StatusId,
                ["hostPtr"] = i.HostPtr,
                ["attackerPtr"] = i.AttackerPtr ?? "",
                ["grantId"] = i.GrantId,
                ["kind"] = i.Kind.ToString(),
                ["effectiveMagnitude"] = i.EffectiveMagnitude,
                ["periodMs"] = i.PeriodMs,
                ["nextMs"] = (long)Math.Max(0, (i.NextPulse - now).TotalMilliseconds),
                ["endMs"] = (long)Math.Max(0, (i.ExpiresAt - now).TotalMilliseconds),
                ["pulsesFired"] = i.PulsesFired,
                ["tickBudget"] = i.TickBudget,
                ["hopDepth"] = i.HopDepth,
                ["spreadChance"] = i.SpreadChance
            })
            .ToList();
        var resisted = status.ResistedEvents
            .Select(ev => new Dictionary<string, object>
            {
                ["statusId"] = ev.StatusId,
                ["hostPtr"] = ev.HostPtr,
                ["attackerPtr"] = ev.AttackerPtr ?? "",
                ["grantId"] = ev.GrantId,
                ["reason"] = ev.Reason.ToString(),
                ["delta"] = ev.Delta,
                ["at"] = ev.At.ToString("o")
            })
            .ToList();
        DebugRuntime.Emit("debug.status", new Dictionary<string, object>
        {
            ["count"] = instances.Count,
            ["instances"] = instances,
            ["resistedCount"] = resisted.Count,
            ["resisted"] = resisted
        });

        if (includeLegacyDots)
            EmitDotsLegacy(now);
        if (includeLegacyCounters)
            EmitCountersLegacy();
    }

    static void EmitDotsLegacy(DateTimeOffset now)
    {
        _ = now;
        DebugRuntime.Emit("debug.effect.dots", new Dictionary<string, object>
        {
            ["count"] = 0,
            ["items"] = new List<Dictionary<string, object>>(),
            ["note"] = "legacy scheduler removed; see debug.status"
        });
    }

    static void EmitCountersLegacy()
    {
        var meters = Effects.EffectRuntime.Status.CounterSnapshot()
            .Select(kv => new Dictionary<string, object> { ["key"] = kv.Key, ["hits"] = kv.Value })
            .ToList();
        DebugRuntime.Emit("debug.effect.counters", new Dictionary<string, object>
        {
            ["count"] = meters.Count,
            ["meters"] = meters,
            ["note"] = meters.Count == 0 ? "legacy CounterProcState removed; bond meters on StatusRuntime" : ""
        });
    }

    static void EmitDots()
    {
        EmitStatus(includeLegacyDots: true);
    }

    static void EmitCounters()
    {
        EmitStatus(includeLegacyCounters: true);
    }

    static void RunShaderProbe()
    {
        var payload = OverlayShaderProbe.ToEventPayload(force: true);
        DebugRuntime.Emit("debug.fx.shader-probe", payload);
        var found = payload.TryGetValue("foundCount", out var n) ? n : 0;
        var draw = payload.TryGetValue("drawShader", out var s) ? s : "";
        CheatState.Note("shader probe found=" + found + " draw=" + draw);
    }

    static void RunWorldFlash(JsonElement p)
    {
        var col = IntProp(p, "col", CheatState.SpawnCol);
        var row = IntProp(p, "row", CheatState.SpawnRow);
        OverlayWorldFx.SpawnAtCell(col, row);
        CheatState.Note("world flash cell=" + col + "," + row);
    }

    static string? ResolveDeltaTargetPtr(JsonElement p)
    {
        var raw = Str(p, "targetPtr");
        if (string.IsNullOrWhiteSpace(raw) ||
            string.Equals(raw, "selected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "entity:selected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "entity:select", StringComparison.OrdinalIgnoreCase))
        {
            if (CheatState.SelectedPtr == IntPtr.Zero) return null;
            return CheatState.SelectedPtr.ToString("X");
        }

        raw = CombatPtr.Normalize(raw);
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    static void RunEffectGrant(JsonElement p)
    {
        try
        {
            Dictionary<string, object?>? overlay = null;
            if (p.TryGetProperty("overlay", out var ov) && ov.ValueKind == JsonValueKind.Object)
            {
                overlay = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in ov.EnumerateObject())
                    overlay[prop.Name] = UnwrapJson(prop.Value);
            }

            var ownerKey = Str(p, "ownerKey") ?? EffectOwnerKeys.Match;
            if (string.Equals(ownerKey, "entity:selected", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ownerKey, "entity:select", StringComparison.OrdinalIgnoreCase))
            {
                if (CheatState.SelectedPtr == IntPtr.Zero)
                {
                    CheatState.Error("debug.effect.grant: entity:selected but no SelectedPtr");
                    return;
                }

                ownerKey = EffectOwnerKeys.Entity(CheatState.SelectedPtr.ToString("X"));
            }

            ownerKey = StatApplyScope.Normalize(ownerKey);
            if (StatApplyScope.IsInstanceOwnerKey(ownerKey))
            {
                CheatState.Error("debug.effect.grant: instance: forbidden in Hot; bind to entity:{ptr}");
                return;
            }

            var ownerKind = Str(p, "ownerKind");
            if (string.IsNullOrWhiteSpace(ownerKind))
            {
                if (ownerKey.StartsWith("entity:", StringComparison.Ordinal)) ownerKind = "entity";
                else if (ownerKey.StartsWith("plant:", StringComparison.Ordinal)) ownerKind = "plant";
                else if (ownerKey.StartsWith("zombie:", StringComparison.Ordinal)) ownerKind = "zombie";
                else if (ownerKey.StartsWith("player:", StringComparison.Ordinal)) ownerKind = "player";
                else ownerKind = "match";
            }

            var grantId = Str(p, "grantId");
            if (!FusionRpg.Core.Effects.EffectGrantSessionRecorder.TryValidateHotGrantId(grantId, out var grantIdErr))
            {
                CheatState.Error("debug.effect.grant: " + grantIdErr);
                return;
            }

            var dto = new EffectGrantDto
            {
                GrantId = grantId!,
                EffectId = Str(p, "effectId") ?? "",
                OwnerKind = ownerKind,
                OwnerKey = ownerKey,
                PluginId = Str(p, "pluginId") ?? "debug",
                Priority = p.TryGetProperty("priority", out var pr) && pr.TryGetInt32(out var pi) ? pi : 0,
                Overlay = overlay
            };
            if (string.IsNullOrWhiteSpace(dto.EffectId))
            {
                CheatState.Error("debug.effect.grant: effectId required");
                return;
            }

            Effects.EffectRuntime.Grant(dto);
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.effect.grant: " + ex.Message);
            DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object> { ["error"] = ex.Message });
        }
    }

    static Dictionary<string, object?> JsonElementToOverlay(JsonElement p)
    {
        var overlay = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (p.ValueKind != JsonValueKind.Object) return overlay;
        foreach (var prop in p.EnumerateObject())
            overlay[prop.Name] = UnwrapJson(prop.Value);
        return overlay;
    }

    static object? UnwrapJson(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Object => el.EnumerateObject()
            .ToDictionary(x => x.Name, x => UnwrapJson(x.Value), StringComparer.OrdinalIgnoreCase),
        JsonValueKind.Array => el.EnumerateArray().Select(UnwrapJson).ToList(),
        _ => el.GetRawText()
    };

    static string? Str(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    static string? LoadoutJsonFromPayload(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;
        if (p.TryGetProperty("loadoutJson", out var s) && s.ValueKind == JsonValueKind.String)
            return s.GetString();
        if (p.TryGetProperty("loadout", out var obj) && obj.ValueKind is JsonValueKind.Object or JsonValueKind.String)
            return obj.ValueKind == JsonValueKind.String ? obj.GetString() : obj.GetRawText();
        return null;
    }

    static int IntProp(JsonElement p, string name, int fallback) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var el) && el.TryGetInt32(out var i) ? i : fallback;

    static long LongProp(JsonElement p, string name, long fallback)
    {
        if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty(name, out var el)) return fallback;
        if (el.TryGetInt64(out var l)) return l;
        if (el.TryGetInt32(out var i)) return i;
        if (el.TryGetDouble(out var d)) return (long)d;
        return fallback;
    }

    static bool BoolProp(JsonElement p, string name, bool fallback) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var el) ? el.GetBoolean() : fallback;
}
