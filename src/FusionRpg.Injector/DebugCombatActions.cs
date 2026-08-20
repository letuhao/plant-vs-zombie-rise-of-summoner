using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Injector.Effects;
using FusionRpg.Injector.Stats;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector;

/// <summary>Debug combat probe / silence / snapshot — LIVE prove helpers.</summary>
public static class DebugCombatActions
{
    public static void SilenceVanilla(JsonElement p)
    {
        var plant = !p.TryGetProperty("plant", out var pl) || pl.ValueKind != JsonValueKind.False;
        if (!plant)
        {
            DebugRuntime.Emit("debug.combat.silence-vanilla", new Dictionary<string, object> { ["ok"] = true, ["plant"] = false });
            return;
        }

        CheatState.SetFloat("A-P-ATK%", 0, "debug");
        CheatState.SetFloat("P-ATK", 0, "debug");
        CheatState.SetToggle("A-APPLY", true, "debug", emitInject: false);
        CheatActions.PushScalesNow();

        var reapplied = 0;
        try
        {
            foreach (var plantObj in UObject.FindObjectsOfType<Plant>())
            {
                if (plantObj == null) continue;
                try
                {
                    if (plantObj.thePlantHealth <= 0) continue;
                }
                catch { continue; }

                GameHooks.Applied.Remove(plantObj.Pointer);
                EntityApply.RunPlant(plantObj, "debug.silence-vanilla", includeAbsolute: true);
                reapplied++;
            }
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.combat.silence-vanilla: " + ex.Message);
        }

        DebugRuntime.Emit("debug.combat.silence-vanilla", new Dictionary<string, object>
        {
            ["ok"] = true,
            ["plant"] = true,
            ["reapplied"] = reapplied
        });
    }

    public static void Probe(JsonElement p)
    {
        try
        {
            Match.SpawnAdmit.EnsureMatchReadyForDebugSpawn();
            Effects.EffectRuntime.Ensure();
            Effects.EffectRuntime.FreezeBoard();

            var targetPtr = Str(p, "targetPtr") ?? Str(p, "ptr");
            if (string.IsNullOrWhiteSpace(targetPtr) && CheatState.SelectedPtr != IntPtr.Zero)
                targetPtr = CheatState.SelectedPtr.ToString("X");
            if (string.IsNullOrWhiteSpace(targetPtr))
            {
                CheatState.Error("debug.combat.probe: targetPtr required");
                return;
            }

            targetPtr = CombatPtr.Normalize(targetPtr);
            var actorPtr = Str(p, "actorPtr");
            if (!string.IsNullOrWhiteSpace(actorPtr))
                actorPtr = CombatPtr.Normalize(actorPtr);

            if (p.TryGetProperty("pinTargetElement", out var pinEl) && pinEl.ValueKind == JsonValueKind.String)
                InjectorElementOverride.PinParse(targetPtr, pinEl.GetString(), null);
            else if (p.TryGetProperty("pinTargetElement", out var pinObj) && pinObj.ValueKind == JsonValueKind.Object)
            {
                var prim = pinObj.TryGetProperty("primary", out var pr) ? pr.GetString() : null;
                var sec = pinObj.TryGetProperty("secondary", out var se) ? se.GetString() : null;
                if (prim == null && pinObj.TryGetProperty("elementPrimary", out var ep))
                    prim = ep.GetString();
                if (sec == null && pinObj.TryGetProperty("elementSecondary", out var es))
                    sec = es.GetString();
                InjectorElementOverride.PinParse(targetPtr, prim, sec);
            }

            if (p.TryGetProperty("pinActorProfile", out var prof) && prof.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(actorPtr))
            {
                InjectorDerivedOverride.PinProfile(actorPtr, prof.GetString(), null);
            }

            if (p.TryGetProperty("pinActorChannels", out var ch) && ch.ValueKind == JsonValueKind.Object
                && !string.IsNullOrWhiteSpace(actorPtr))
            {
                var map = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var prop in ch.EnumerateObject())
                {
                    if (prop.Value.TryGetDouble(out var v))
                        map[prop.Name] = v;
                }

                if (map.Count > 0)
                    InjectorDerivedOverride.PinProfile(actorPtr, null, map);
            }

            var amount = LongProp(p, "amount", -100);
            var seed = IntProp(p, "seed", 1);
            bool? forceHit = null;
            bool? forceCrit = null;
            if (p.TryGetProperty("forceMiss", out var fm) && fm.ValueKind == JsonValueKind.True)
                forceHit = false;
            else if (p.TryGetProperty("forceHit", out var fh) && fh.ValueKind == JsonValueKind.True)
                forceHit = true;
            if (p.TryGetProperty("forceCrit", out var fc) && fc.ValueKind == JsonValueKind.True)
                forceCrit = true;
            else if (p.TryGetProperty("forceCrit", out var fcf) && fcf.ValueKind == JsonValueKind.False)
                forceCrit = false;

            var overlay = JsonOverlayFromProbe(p, amount, targetPtr, actorPtr);
            var packet = DamagePacketBuilder.FromOverlay(overlay, new EffectEventDto
            {
                TargetPtr = targetPtr,
                ActorPtr = actorPtr ?? "",
                Tick = Effects.EffectRuntime.NextTick()
            }, grantId: "debug.combat.probe", pluginId: "debug");

            var passThrough = amount > 0
                              || packet.ElementPayload == null
                              || packet.ElementPayload.Count == 0
                              || !OverlayCombatFeature.Enabled;
            if (passThrough)
            {
                // Heal / no payload / OVERLAY-COMBAT off — pass-through (no overlay breakdown).
                var funnel = Effects.EffectRuntime.Bag.Funnel;
                if (funnel == null)
                {
                    CheatState.Error("debug.combat.probe: funnel missing");
                    return;
                }

                var ok = amount == 0 || funnel.EnqueueMutation("entity:" + targetPtr, amount, pluginId: "debug");
                funnel.Flush();
                var passDump = new Dictionary<string, object>
                {
                    ["source"] = "probe-passthrough",
                    ["targetPtr"] = targetPtr,
                    ["actorPtr"] = actorPtr ?? "",
                    ["amount"] = amount,
                    ["ok"] = ok,
                    ["seed"] = seed,
                    ["hit"] = false,
                    ["crit"] = false,
                    ["finalSignedDelta"] = amount,
                    ["overlay"] = false,
                    ["overlayCombat"] = OverlayCombatFeature.Enabled
                };
                CombatDebugObservability.RememberProbe(passDump);
                DebugRuntime.Emit("debug.combat.probe", passDump);
                return;
            }

            var components = OverlayCombatCalculator.ParseComponents(packet.ElementPayload);
            var attackerLess = string.IsNullOrWhiteSpace(actorPtr);
            var attacker = InjectorCombatBridge.ResolveActor(actorPtr, attackerLess);
            var defender = InjectorCombatBridge.ResolveActor(targetPtr, attackerLess: false);

            var request = new OverlayCombatRequest
            {
                BaseOverlayDamage = Math.Abs(amount),
                Components = components,
                Attacker = attacker,
                Defender = defender,
                ForceHit = forceHit,
                ForceCrit = forceCrit
            };

            var calc = new OverlayCombatCalculator();
            var (delta, breakdown) = calc.Compute(request, new SeededCombatRng(seed));

            InjectorCombatBridge.EmitOverlayBreakdown(
                breakdown,
                packet,
                targetPtr,
                extras: new Dictionary<string, object>
                {
                    ["source"] = "probe",
                    ["seed"] = seed,
                    ["forced"] = new Dictionary<string, object>
                    {
                        ["hit"] = forceHit.HasValue ? forceHit.Value : (object)"",
                        ["miss"] = forceHit == false,
                        ["crit"] = forceCrit.HasValue ? forceCrit.Value : (object)""
                    },
                    ["defenderElements"] = ElementDump(defender.ElementTypes),
                    ["attackerElements"] = ElementDump(attacker.ElementTypes)
                });

            var funnel2 = Effects.EffectRuntime.Bag.Funnel;
            var applied = 0;
            if (funnel2 != null && delta != 0)
            {
                if (funnel2.EnqueueMutation("entity:" + targetPtr, delta, pluginId: "debug"))
                    applied = 1;
                funnel2.Flush();
            }

            var probeDump = new Dictionary<string, object>
            {
                ["source"] = "probe",
                ["targetPtr"] = targetPtr,
                ["actorPtr"] = actorPtr ?? "",
                ["amount"] = amount,
                ["seed"] = seed,
                ["hit"] = breakdown.Hit,
                ["crit"] = breakdown.Crit,
                ["matchupBonus"] = breakdown.MatchupBonus,
                ["weightedDelta"] = breakdown.WeightedDelta,
                ["powerAdjustedDamage"] = breakdown.PowerAdjustedDamage,
                ["finalSignedDelta"] = breakdown.FinalSignedDelta,
                ["pHitFinal"] = breakdown.PHitFinal,
                ["pCritFinal"] = breakdown.PCritFinal,
                ["critMultiplierFinal"] = breakdown.CritMultiplierFinal,
                ["fa10"] = applied,
                ["overlay"] = true,
                ["defenderElements"] = ElementDump(defender.ElementTypes),
                ["attackerElements"] = ElementDump(attacker.ElementTypes)
            };
            CombatDebugObservability.RememberProbe(probeDump);
            DebugRuntime.Emit("debug.combat.probe", probeDump);
        }
        catch (Exception ex)
        {
            CheatState.Error("debug.combat.probe: " + ex.Message);
            DebugRuntime.Emit("debug.combat.probe", new Dictionary<string, object>
            {
                ["ok"] = false,
                ["error"] = ex.Message
            });
        }
    }

    public static void Snapshot(JsonElement p)
    {
        Effects.EffectRuntime.Ensure();
        var board = InjectorBoardSnapshot.Capture();
        var ents = board.Entities
            .Where(e => e != null && e.Living)
            .Select(e => new Dictionary<string, object>
            {
                ["ptr"] = e.Ptr ?? "",
                ["side"] = e.Side ?? "",
                ["typeId"] = e.TypeId,
                ["row"] = e.Row,
                ["col"] = e.Col,
                ["living"] = e.Living
            })
            .ToList();

        var ptrs = new List<string>();
        if (p.TryGetProperty("ptrs", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                    ptrs.Add(CombatPtr.Normalize(el.GetString()));
            }
        }

        if (ptrs.Count == 0)
        {
            foreach (var e in ents)
            {
                var ptr = e["ptr"] as string;
                if (!string.IsNullOrWhiteSpace(ptr))
                    ptrs.Add(ptr!);
            }
        }

        var actors = new List<Dictionary<string, object>>();
        foreach (var ptr in ptrs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var snap = InjectorCombatBridge.ResolveActor(ptr, attackerLess: false);
            var channels = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var id in DerivedStatChannels.AllCombatChannelIds)
            {
                if (snap.Derived.TryGet(id, out var v) && Math.Abs(v) > 1e-9)
                    channels[id] = v;
            }

            actors.Add(new Dictionary<string, object>
            {
                ["ptr"] = ptr,
                ["elementPrimary"] = snap.ElementTypes.Primary?.ToString() ?? "",
                ["elementSecondary"] = snap.ElementTypes.Secondary?.ToString() ?? "",
                ["channels"] = channels
            });
        }

        var dump = new Dictionary<string, object>
        {
            ["entities"] = ents,
            ["actors"] = actors,
            ["lastOverlay"] = CombatDebugObservability.LastOverlay ?? new Dictionary<string, object>(),
            ["lastProbe"] = CombatDebugObservability.LastProbe ?? new Dictionary<string, object>(),
            ["recentOverlays"] = CombatDebugObservability.RecentOverlays(),
            ["scenarioId"] = DebugRuntime.ScenarioId ?? "",
            ["matchKey"] = GameHooks.MatchKey ?? "",
            ["overlayCombat"] = OverlayCombatFeature.Enabled
        };
        DebugRuntime.Emit("debug.combat.snapshot", dump);
    }

    static Dictionary<string, object> ElementDump(ActorElementTypes types) =>
        new()
        {
            ["primary"] = types.Primary?.ToString() ?? "",
            ["secondary"] = types.Secondary?.ToString() ?? "",
            ["neutral"] = types.IsNeutral
        };

    static Dictionary<string, object?> JsonOverlayFromProbe(
        JsonElement p, long amount, string targetPtr, string? actorPtr)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["amount"] = amount,
            ["targetPtr"] = targetPtr
        };
        if (!string.IsNullOrWhiteSpace(actorPtr))
            dict["actorPtr"] = actorPtr;
        if (p.TryGetProperty("elementPayload", out var ep) && ep.ValueKind == JsonValueKind.Array)
        {
            var list = new List<object?>();
            foreach (var item in ep.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var m = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (item.TryGetProperty("element", out var el) && el.ValueKind == JsonValueKind.String)
                    m["element"] = el.GetString();
                if (item.TryGetProperty("weight", out var w) && w.TryGetDouble(out var wv))
                    m["weight"] = wv;
                else
                    m["weight"] = 1.0;
                list.Add(m);
            }

            if (list.Count > 0)
                dict["elementPayload"] = list;
        }

        return dict;
    }

    static string? Str(JsonElement p, string name) =>
        p.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    static long LongProp(JsonElement p, string name, long dflt) =>
        p.TryGetProperty(name, out var e) && e.TryGetInt64(out var v) ? v : dflt;

    static int IntProp(JsonElement p, string name, int dflt) =>
        p.TryGetProperty(name, out var e) && e.TryGetInt32(out var v) ? v : dflt;
}
