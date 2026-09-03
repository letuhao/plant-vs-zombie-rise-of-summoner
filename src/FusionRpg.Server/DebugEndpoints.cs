using System.Reflection;
using System.Text.Json;
using FusionRpg.CheatCore;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>Server-side mirror of debug session (injector holds the live arms).</summary>
public static class DebugSessionState
{
    public static bool Active;
    public static string ScenarioId = "";
    public static readonly Dictionary<string, object?> Arms = new(StringComparer.OrdinalIgnoreCase);

    public static object Snapshot() => new
    {
        sessionActive = Active,
        scenarioId = ScenarioId,
        arms = Arms
    };
}

public static class DebugEndpoints
{
    /// <summary>E33 (spec-activation-edge.md §2.1): every `public const string` on <paramref name="t"/>,
    /// in declaration order — the source of truth `/effects/contract` publishes from, so the endpoint
    /// cannot drift from the class it names without a code change to <paramref name="t"/> itself.</summary>
    static string[] PublicConstStrings(Type t) =>
        t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

    public static void MapDebug(this WebApplication app)
    {
        var g = app.MapGroup("/api/debug");

        g.MapPost("/session/start", async (JsonElement? body, EventIngest ingest, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var b = BodyOrEmpty(body);
            var scenarioId = b.TryGetProperty("scenarioId", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()!
                : Guid.NewGuid().ToString("N")[..12];
            DebugSessionState.Active = true;
            DebugSessionState.ScenarioId = scenarioId;
            DebugSessionState.Arms.Clear();
            ingest.Enqueue(new EventEnvelope
            {
                T = DateTime.UtcNow.ToString("o"),
                Kind = "debug.session.start",
                Payload = new Dictionary<string, object> { ["scenarioId"] = scenarioId }
            });
            await Send(hub, inbox, "debug.session", new { op = "start", scenarioId });
            return Results.Ok(new { ok = true, scenarioId });
        });

        g.MapPost("/session/end", async (EventIngest ingest, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var id = DebugSessionState.ScenarioId;
            DebugSessionState.Active = false;
            DebugSessionState.Arms.Clear();
            ingest.Enqueue(new EventEnvelope
            {
                T = DateTime.UtcNow.ToString("o"),
                Kind = "debug.session.end",
                Payload = new Dictionary<string, object> { ["scenarioId"] = id }
            });
            await Send(hub, inbox, "debug.session", new { op = "end" });
            DebugSessionState.ScenarioId = "";
            return Results.Ok(new { ok = true });
        });

        g.MapGet("/session", () => Results.Ok(DebugSessionState.Snapshot()));
        g.MapGet("/snapshot", async (IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            await Send(hub, inbox, "debug.snapshot", new { });
            return Results.Ok(new
            {
                ok = true,
                server = DebugSessionState.Snapshot(),
                note = "injector emits debug.snapshot (nested match = MatchSnapshot observe); poll GET /api/debug/events?kinds=debug.snapshot"
            });
        });

        g.MapGet("/events", (RpgStore store, int limit = 200, long afterId = 0, string? kinds = null, string? scenarioId = null) =>
        {
            var items = store.ListEvents(Math.Clamp(limit, 1, 500), afterId);
            if (!string.IsNullOrWhiteSpace(kinds))
            {
                var set = new HashSet<string>(kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);
                items = items.Where(e => set.Contains(e.Kind)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(scenarioId))
            {
                items = items.Where(e => PayloadHasScenario(e.Payload, scenarioId)).ToList();
            }
            return Results.Ok(new { items });
        });

        g.MapGet("/scenarios", () => Results.Ok(new { items = DebugScenarios.AllIds }));

        g.MapPost("/scenario/{id}", async (string id, JsonElement? body, EventIngest ingest, IHubContext<RpgHub> hub, InjectorCommandInbox inbox, EffectGrantSession grants) =>
        {
            var b = BodyOrEmpty(body);
            var scenarioId = b.ValueKind == JsonValueKind.Object
                             && b.TryGetProperty("scenarioId", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()!
                : (DebugSessionState.Active ? DebugSessionState.ScenarioId : Guid.NewGuid().ToString("N")[..12]);
            try
            {
                var steps = DebugScenarios.Expand(id, scenarioId);
                DebugSessionState.Active = true;
                DebugSessionState.ScenarioId = scenarioId;
                EffectGrantSessionRecorder.ApplyDebugSteps(
                    grants,
                    steps.Select(st => (st.Name, (object?)st.Payload)));
                ingest.Enqueue(new EventEnvelope
                {
                    T = DateTime.UtcNow.ToString("o"),
                    Kind = "debug.scenario.start",
                    Payload = new Dictionary<string, object> { ["id"] = id, ["scenarioId"] = scenarioId, ["steps"] = steps.Count }
                });
                // P0: one injector command; steps run sequentially on Unity main-thread drain.
                await Send(hub, inbox, "debug.run-steps", new
                {
                    scenarioId,
                    id,
                    steps = steps.Select(st => new { name = st.Name, payload = st.Payload }).ToList()
                });
                return Results.Ok(new { ok = true, id, scenarioId, steps = steps.Count, command = "debug.run-steps" });
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        g.MapPost("/lawn/quick-start", async (JsonElement? body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox, EffectGrantSession grants) =>
        {
            var b = BodyOrEmpty(body);
            var levelNumber = IntProp(b, "levelNumber", 1);
            var scenarioId = StrProp(b, "scenario") ?? "lab-overlay";
            var timeoutSec = IntProp(b, "timeoutSec", 45);

            if (!store.InjectorConnected)
                return Results.Conflict(new { ok = false, error = "injector not connected — start the game with the FusionRpg injector loaded" });

            var entered = false;
            var boardStart = FindLatestLiveBoardStart(store);

            if (boardStart is null)
            {
                store.MergeCheatField("DEBUG-LEVEL-ENTRY", true, null);
                await Send(hub, inbox, "cheat.toggle", new { id = "DEBUG-LEVEL-ENTRY", enabled = true });

                var beforeEnter = store.GetMaxEventId();
                await Send(hub, inbox, "debug.enter-level", new { levelType = 0, levelNumber, id = 0, name = "" });

                var ackTimeoutSec = Math.Min(timeoutSec, 20);
                var enterAck = await PollForKind(store, beforeEnter, "debug.level.enter", TimeSpan.FromSeconds(ackTimeoutSec));
                if (enterAck is null)
                    return Results.Conflict(new { ok = false, error = $"debug.level.enter did not ack within {ackTimeoutSec}s" });

                var ackOk = PayloadBool(enterAck.Payload, "ok");
                if (!ackOk)
                {
                    var err = PayloadString(enterAck.Payload, "error") ?? "enter-level rejected";
                    if (!err.Contains("board already live", StringComparison.OrdinalIgnoreCase))
                        return Results.Conflict(new { ok = false, error = err });
                    // "board already live" — fall through and use the board that's already there.
                }
                else
                {
                    boardStart = await PollForKind(store, beforeEnter, "board.start", TimeSpan.FromSeconds(timeoutSec));
                    if (boardStart is null)
                        return Results.Conflict(new { ok = false, error = $"enter-level ok but no board.start within {timeoutSec}s — check main menu state" });
                    entered = true;
                }

                // The INJECTOR has just said a board is live, and it holds the actual Board object —
                // it outranks this server's event-log heuristic. So drop the session filter here:
                // a SERVER restart makes the injector re-Hello from the SAME game process with the
                // SAME live board, which would otherwise look "stale" to the session rule below and
                // 409 a perfectly good lawn. Found live 2026-08-30, immediately after the session rule
                // itself was added — the fix for one false positive created a false negative.
                if (boardStart is null)
                    boardStart = FindLatestLiveBoardStart(store, trustInjectorLiveBoard: true);
                if (boardStart is null)
                    return Results.Conflict(new { ok = false, error = "enter-level reported board already live, but no live board.start was found" });
            }

            var levelType = PayloadString(boardStart.Payload, "levelType") ?? "";
            if (BadLevelTypes.Contains(levelType))
                return Results.Conflict(new { ok = false, error = $"refusing lab on levelType={levelType} — open Adventure/Challenge day lawn, not Explore/Travel" });

            await Send(hub, inbox, "debug.wave-freeze", new { enabled = true });

            var scenarioCorrelation = Guid.NewGuid().ToString("N")[..12];
            IReadOnlyList<DebugScenarioStep> steps;
            try { steps = DebugScenarios.Expand(scenarioId, scenarioCorrelation); }
            catch (ArgumentException ex) { return Results.NotFound(new { ok = false, error = ex.Message }); }

            var beforeScenario = store.GetMaxEventId();
            DebugSessionState.Active = true;
            DebugSessionState.ScenarioId = scenarioCorrelation;
            EffectGrantSessionRecorder.ApplyDebugSteps(grants, steps.Select(st => (st.Name, (object?)st.Payload)));
            await Send(hub, inbox, "debug.run-steps", new
            {
                scenarioId = scenarioCorrelation,
                id = scenarioId,
                steps = steps.Select(st => new { name = st.Name, payload = st.Payload }).ToList()
            });

            var runDone = await PollForKind(store, beforeScenario, "debug.run-steps.done", TimeSpan.FromSeconds(timeoutSec));
            if (runDone is null)
                return Results.Conflict(new { ok = false, error = $"scenario '{scenarioId}' steps did not complete within {timeoutSec}s" });

            EventEnvelope? snapshot = null;
            var beforeSnapshot = store.GetMaxEventId();
            var snapshotDeadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < snapshotDeadline && snapshot is null)
            {
                await Send(hub, inbox, "debug.effect.board-snapshot", new { });
                await Task.Delay(400);
                snapshot = FindKindAfter(store, beforeSnapshot, "debug.effect.board-snapshot");
            }

            string? targetPtr = null;
            string? plantPtr = null;
            if (snapshot?.Payload is JsonElement snapEl && snapEl.ValueKind == JsonValueKind.Object
                && snapEl.TryGetProperty("entities", out var entitiesEl) && entitiesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ent in entitiesEl.EnumerateArray())
                {
                    var living = ent.TryGetProperty("living", out var l) && l.ValueKind == JsonValueKind.True;
                    if (!living || !ent.TryGetProperty("ptr", out var ptrEl) || ptrEl.ValueKind != JsonValueKind.String) continue;
                    var side = ent.TryGetProperty("side", out var s) ? s.GetString() : null;
                    if (side == "zombie" && targetPtr is null) targetPtr = ptrEl.GetString();
                    if (side == "plant" && plantPtr is null) plantPtr = ptrEl.GetString();
                }
            }

            return Results.Ok(new
            {
                ok = true,
                entered,
                levelType,
                scenario = scenarioId,
                targetPtr,
                plantPtr,
                note = snapshot is null ? "no board snapshot arrived — targetPtr/plantPtr unavailable" : null
            });
        });

        MapPost(g, "/reset-board", "debug.reset-board");
        MapPost(g, "/clear-plants", "debug.clear-plants");
        MapPost(g, "/clear-zombies", "debug.clear-zombies");
        MapPost(g, "/spawn-plant", "debug.spawn-plant");
        MapPost(g, "/spawn-zombie", "debug.spawn-zombie");
        MapPost(g, "/spawn-bullet", "debug.spawn-bullet");
        MapPost(g, "/set-mods", "debug.set-mods");
        MapPost(g, "/reset-mods", "debug.reset-mods");
        MapPost(g, "/reapply", "debug.reapply");
        MapPost(g, "/board-stats", "debug.board-stats");
        MapPost(g, "/apply-status", "debug.apply-status");
        MapPost(g, "/apply-status-float", "debug.apply-status-float");
        MapPost(g, "/clear-status", "debug.clear-status");
        MapPost(g, "/kill", "debug.kill");
        MapPost(g, "/kill-plant", "debug.kill-plant");
        MapPost(g, "/wave-freeze", "debug.wave-freeze");
        MapPost(g, "/stress-fill", "debug.stress-fill");
        MapPost(g, "/stress-clear", "debug.stress-clear");
        MapPost(g, "/ensure-sun", "debug.ensure-sun");
        MapPost(g, "/enter-level", "debug.enter-level");
        MapPost(g, "/select", "debug.select");
        MapPost(g, "/spawn-cell", "debug.spawn-cell");
        MapPost(g, "/disarm", "debug.disarm");
        MapPost(g, "/economy", "debug.economy");
        MapPost(g, "/board-config", "debug.board-config");
        MapPost(g, "/board-action", "debug.board-action");
        MapPost(g, "/spawn-grid", "debug.spawn-grid");
        MapPost(g, "/clear-grid", "debug.clear-grid");
        MapPost(g, "/set-box", "debug.set-box");
        MapPost(g, "/grid-query", "debug.grid-query");
        MapPost(g, "/ice-road", "debug.ice-road");
        g.MapPost("/effect/grant", async (JsonElement? body, IHubContext<RpgHub> hub, InjectorCommandInbox inbox, EffectGrantSession grants) =>
        {
            var b = BodyOrEmpty(body);
            var dto = EffectGrantSessionRecorder.TryParseGrant(b);
            if (dto == null || string.IsNullOrWhiteSpace(dto.EffectId))
                return Results.BadRequest(new { error = "effectId required" });
            EffectGrantSessionRecorder.NormalizeGrantDefaults(dto);
            grants.Upsert(dto);
            await Send(hub, inbox, "debug.effect.grant", dto);
            return Results.Ok(new { ok = true, queued = inbox.Count, grantId = dto.GrantId });
        });
        g.MapPost("/effect/withdraw", async (JsonElement? body, IHubContext<RpgHub> hub, InjectorCommandInbox inbox, EffectGrantSession grants) =>
        {
            var b = BodyOrEmpty(body);
            EffectGrantSessionRecorder.ApplyDebugCommand(grants, "debug.effect.withdraw", b);
            await Send(hub, inbox, "debug.effect.withdraw", b);
            return Results.Ok(new { ok = true, queued = inbox.Count });
        });
        g.MapPost("/effect/clear", async (JsonElement? body, IHubContext<RpgHub> hub, InjectorCommandInbox inbox, EffectGrantSession grants) =>
        {
            EffectGrantSessionRecorder.ApplyDebugCommand(grants, "debug.effect.clear", BodyOrEmpty(body));
            await Send(hub, inbox, "debug.effect.clear", BodyOrEmpty(body));
            return Results.Ok(new { ok = true, queued = inbox.Count });
        });
        MapPost(g, "/effect/list", "debug.effect.list");
        MapPost(g, "/effect/fire-synthetic", "debug.effect.fire-synthetic");
        MapPost(g, "/effect/enqueue-delta", "debug.effect.enqueue-delta");
        MapPost(g, "/effect/board-snapshot", "debug.effect.board-snapshot");
        MapPost(g, "/effect/dots", "debug.effect.dots");
        MapPost(g, "/effect/counters", "debug.effect.counters");
        MapPost(g, "/status", "debug.status");
        g.MapGet("/status", async (IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            await Send(hub, inbox, "debug.status", new { });
            return Results.Ok(new
            {
                ok = true,
                note = "injector emits debug.status; poll GET /api/debug/events?kinds=debug.status,debug.status.resisted"
            });
        });
        g.MapGet("/actor-derived", async (string? ptr, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            await Send(hub, inbox, "debug.actor-derived", new { ptr = ptr ?? "" });
            return Results.Ok(new
            {
                ok = true,
                note = "injector emits debug.actor-derived; poll GET /api/debug/events?kinds=debug.actor-derived"
            });
        });
        g.MapPost("/actor-derived", async (JsonElement? body, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var b = BodyOrEmpty(body);
            await Send(hub, inbox, "debug.actor-derived", b);
            return Results.Ok(new { ok = true, queued = inbox.Count, command = "debug.actor-derived" });
        });
        MapPost(g, "/combat/pin-element", "debug.combat.pin-element");
        MapPost(g, "/combat/silence-vanilla", "debug.combat.silence-vanilla");
        MapPost(g, "/combat/probe", "debug.combat.probe");
        MapPost(g, "/combat/snapshot", "debug.combat.snapshot");
        MapPost(g, "/shield/grant", "debug.shield.grant");
        MapPost(g, "/shield/clear", "debug.shield.clear");
        MapPost(g, "/shield/demo", "debug.shield.demo");
        MapPost(g, "/shield/demo-all", "debug.shield.demo-all");
        MapPost(g, "/shield/snapshot", "debug.shield.snapshot");
        MapPost(g, "/shield/bar-status", "debug.shield.bar-status");
        g.MapPost("/status/apply", async (JsonElement? body, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var b = BodyOrEmpty(body);
            await Send(hub, inbox, "debug.status.apply", b);
            return Results.Ok(new { ok = true, queued = inbox.Count, command = "debug.status.apply" });
        });
        MapPost(g, "/fx/probe-shaders", "debug.fx.probe-shaders");
        MapPost(g, "/fx/world-flash", "debug.fx.world-flash");
        MapPost(g, "/fx/play", "debug.fx.play");
        MapPost(g, "/fx/list", "debug.fx.list");
        MapPost(g, "/fx/mute", "debug.fx.mute");
        MapPost(g, "/fx/unmute", "debug.fx.unmute");
        MapPost(g, "/fx/state", "debug.fx.state");

        g.MapPost("/effects/reload", async (IHubContext<RpgHub> hub, InjectorCommandInbox inbox, EffectGrantSession grants) =>
        {
            EffectGrantSessionRecorder.ApplyDebugCommand(grants, "effects.reload", default);
            await Send(hub, inbox, "effects.reload", new
            {
                contractVersion = FoundationContractVersion.Current
            });
            return Results.Ok(new
            {
                ok = true,
                contractVersion = FoundationContractVersion.Current,
                command = "effects.reload"
            });
        });

        g.MapGet("/effects/session-grants", (EffectGrantSession grants) =>
            Results.Ok(new { count = grants.Count, grants = grants.Snapshot() }));

        // E33 (spec-activation-edge.md §2.1, §2.1a): both arrays used to be hand-copied and had
        // drifted from their own source classes — `triggers` was missing OnActivate, `actions` was
        // missing GrantShield and ModifyDerivedStat, all three real (GrantShield has a live executor;
        // ModifyDerivedStat is declarative-by-design but still part of the published vocabulary). A
        // published-but-not-declared or declared-but-not-published constant is exactly "a published
        // list that lies", the defect this endpoint exists to not repeat — so both arrays are now
        // reflected off their own class's public const fields, which is what makes "every constant,
        // and no others" true by construction rather than by someone remembering to edit two lists in
        // sync. E34 grows EffectTriggers to 13 and needs no edit here for that to stay correct; E35/
        // E36/E37 grow EffectActions the same way.
        g.MapGet("/effects/contract", () => Results.Ok(new
        {
            contractVersion = FoundationContractVersion.Current,
            frozen = true,
            triggers = PublicConstStrings(typeof(EffectTriggers)),
            actions = PublicConstStrings(typeof(EffectActions))
        }));

        // T5.7 / `dev-reforge` (spec-dev-reforge.md, effect-pipeline module 10; also
        // spec-player-materialise.md §6, A4): re-derive a player's whole species roster against the
        // CURRENT catalog, same world seed — a debug-only shortcut for observing a retuned affix
        // without a new profile. Pure DAL, no injector round trip. Gated the same way every other
        // `/api/debug/*` route is: Program.cs only calls `app.MapDebug()` on a loopback bind (or
        // FUSIONRPG_DEBUG_REMOTE=1) — this endpoint lives in the SAME route group, not a second gate.
        g.MapPost("/reforge-world", (JsonElement? body, RpgStore store, EventIngest ingest) =>
        {
            var b = BodyOrEmpty(body);
            var playerId = b.ValueKind == JsonValueKind.Object
                && b.TryGetProperty("playerId", out var p) && p.TryGetInt64(out var pid)
                ? pid : store.GetCurrentPlayerId();
            var thetaContent = IntProp(b, "thetaContent", 0);

            // "before" — the revision this player's roster was last rolled against, read BEFORE the
            // reforge touches anything (0 for a player with no roster yet). spec-dev-reforge.md's own
            // guardrail: log before/after so a dev can see what a retune actually changed.
            var beforeRows = store.ListPlayerSpecies(playerId);
            var catalogRevisionBefore = beforeRows.Count == 0 ? 0 : beforeRows.Max(r => r.CatalogRevision);

            var outcome = store.ReforgePlayerSpecies(playerId, thetaContent, PowerTuningHub.Tuning);
            if (!outcome.IsOk)
                return Results.Conflict(new { ok = false, error = outcome.Rejection.ToString() });

            ingest.Enqueue(new EventEnvelope
            {
                T = DateTime.UtcNow.ToString("o"),
                Kind = "debug.reforge-world",
                PlayerId = playerId,
                Payload = new Dictionary<string, object>
                {
                    ["catalogRevisionBefore"] = catalogRevisionBefore,
                    ["catalogRevisionAfter"] = outcome.CatalogRevision,
                    ["reforged"] = outcome.Written,
                    ["unchanged"] = outcome.AlreadyPresent,
                }
            });

            return Results.Ok(new
            {
                ok = true,
                playerId,
                catalogRevisionBefore,
                catalogRevisionAfter = outcome.CatalogRevision,
                reforged = outcome.Written,
                unchanged = outcome.AlreadyPresent,
                elapsedMs = outcome.ElapsedMs
            });
        });

        g.MapPost("/spawn-extra", async (JsonElement? body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var result = await AcceptDebugSpawnExtra(BodyOrEmpty(body), store, hub, inbox, reasonDefault: "debug");
            return result;
        });

        g.MapPost("/fire-spawn-extra", async (JsonElement? body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var b = BodyOrEmpty(body);
            // Ensure typeId default for fire helper
            if (b.ValueKind != JsonValueKind.Object || !b.TryGetProperty("typeId", out _))
            {
                b = JsonSerializer.SerializeToElement(new { typeId = DebugScenarios.BasicZombieTypeId });
            }
            return await AcceptDebugSpawnExtra(b, store, hub, inbox, reasonDefault: "debug.fire");
        });

        g.MapPost("/arm/{kind}", async (string kind, JsonElement? body, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var payload = JsonSerializer.SerializeToElement(MergeKind(BodyOrEmpty(body), kind));
            DebugSessionState.Arms[kind] = true;
            await Send(hub, inbox, "debug.arm", payload);
            return Results.Ok(new { ok = true, kind });
        });
    }

    /// <summary>Same accept path as POST /api/pvz-intent/spawn-extra (fact + command when newly inserted).</summary>
    public static async Task<IResult> AcceptDebugSpawnExtra(
        JsonElement body,
        RpgStore store,
        IHubContext<RpgHub> hub,
        InjectorCommandInbox inbox,
        string reasonDefault)
    {
        var playerId = store.GetCurrentPlayerId();
        if (!store.PlayerExists(playerId)) return Results.NotFound(new { error = "no current player" });

        var typeId = body.TryGetProperty("typeId", out var t) && t.TryGetInt32(out var ti) ? ti : DebugScenarios.BasicZombieTypeId;
        var corr = body.TryGetProperty("correlationId", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()!
            : Guid.NewGuid().ToString("N");
        int? row = body.TryGetProperty("row", out var r) && r.TryGetInt32(out var rv) ? rv : null;
        int? col = body.TryGetProperty("col", out var colEl) && colEl.TryGetInt32(out var cv) ? cv : null;
        var reason = body.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String
            ? reasonEl.GetString()!
            : reasonDefault;
        var side = body.TryGetProperty("side", out var sideEl) && sideEl.ValueKind == JsonValueKind.String
            ? sideEl.GetString()!.Trim().ToLowerInvariant()
            : "zombie";

        try
        {
            var (rollup, inserted) = store.RecordExtraSpawnIntent(playerId, corr, typeId, reason, side);
            if (inserted)
            {
                var activity = store.GetPvzActivityRollup(playerId);
                if (activity is not null)
                    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("PvzActivityUpdated", activity);

                await Send(hub, inbox, "pvz.spawn.extra", new
                {
                    typeId,
                    col,
                    row = row ?? 2,
                    reason,
                    correlationId = corr,
                    side,
                    playerId,
                    source = "extra"
                }, cmdId: corr);
            }

            return Results.Ok(new { ok = true, correlationId = corr, inserted, rollup, queued = inbox.Count });
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    static JsonElement BodyOrEmpty(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object } b ? b : JsonSerializer.SerializeToElement(new { });

    // ---- lawn/quick-start helpers (centralizes what setup-lab-run.ps1 + tools/live_test/lawn.py
    // each separately hand-rolled — see .claude/skills/live-lawn-quick-start/SKILL.md) ----

    static readonly HashSet<string> BadLevelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Explore", "TravelAdvanture", "Travel", "IZ"
    };

    static int IntProp(JsonElement obj, string name, int fallback) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var el) && el.TryGetInt32(out var v)
            ? v : fallback;

    static string? StrProp(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() : null;

    static bool PayloadBool(object? payload, string name) =>
        payload is JsonElement el && el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    static string? PayloadString(object? payload, string name) =>
        payload is JsonElement el && el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    /// <summary>Newest `board.start` with no later `board.end` — in-process port of
    /// `setup-lab-run.ps1`'s `Get-LatestBoardStart`/`Test-BoardStillLive` (external, HTTP-bound,
    /// forced into a paging/binary-search shape) and `lawn.py`'s `latest_board_start`/
    /// `board_still_live` (same idea, Python). One direct scan is sufficient here since the caller
    /// already holds the store in-process — no HTTP round trip to approximate.</summary>
    /// <summary>Internal for <c>FusionRpg.Server.Tests</c> — the stale-board rule below has cost two
    /// sessions and now has a regression test.</summary>
    /// <param name="trustInjectorLiveBoard">
    /// Set only when the injector has just reported "board already live". It holds the real Board and
    /// outranks this event-log heuristic, so the injector-session filter below is skipped — otherwise a
    /// SERVER restart (same game, same board, fresh Hello) would read as stale.
    /// </param>
    internal static EventEnvelope? FindLatestLiveBoardStart(RpgStore store, bool trustInjectorLiveBoard = false)
    {
        var max = store.GetMaxEventId();
        if (max <= 0) return null;
        const int window = 2000;
        var after = Math.Max(0, max - window);
        var items = store.ListEvents(window, after);
        var starts = items.Where(e => e.Kind == "board.start").ToList();
        if (starts.Count == 0) return null;
        var latestStart = starts[^1];
        var endedAfter = items.Any(e => e.Kind == "board.end" && e.Id > latestStart.Id);
        if (endedAfter) return null;

        // A board.start is only "live" if it belongs to the CURRENT injector session.
        //
        // A `board.end` is written on a clean exit. Kill the game mid-match -- a crash, a redeploy, or
        // an assistant tool call whose process tree is reaped -- and none is ever written, so that row
        // stays "live" forever. `quick-start` then reports `entered:false` with null targetPtr/plantPtr
        // and every probe afterwards runs against a board that does not exist.
        //
        // This false positive has now cost two separate sessions (2026-08-30, twice: once mistaken for
        // an `attackDamage` regression, once blocking A5 entirely), which is why it is fixed here rather
        // than documented again. `injector.hello` is emitted once per injector startup, so any
        // board.start older than the newest one belongs to a game process that is gone.
        if (trustInjectorLiveBoard) return latestStart;

        var lastHello = items.LastOrDefault(e => e.Kind == "injector.hello");
        if (lastHello is not null && latestStart.Id < lastHello.Id) return null;

        return latestStart;
    }

    static EventEnvelope? FindKindAfter(RpgStore store, long afterId, string kind)
    {
        var items = store.ListEvents(500, afterId);
        return items.LastOrDefault(e => e.Kind == kind);
    }

    static async Task<EventEnvelope?> PollForKind(RpgStore store, long afterId, string kind, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var found = FindKindAfter(store, afterId, kind);
            if (found is not null) return found;
            await Task.Delay(300);
        }
        return null;
    }

    static object MergeKind(JsonElement body, string kind)
    {
        var dict = new Dictionary<string, object?> { ["kind"] = kind };
        if (body.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in body.EnumerateObject())
            {
                if (prop.NameEquals("kind")) continue;
                dict[prop.Name] = prop.Value.Clone();
            }
        }
        return dict;
    }

    static void MapPost(RouteGroupBuilder g, string path, string cmdName)
    {
        g.MapPost(path, async (JsonElement? body, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var b = BodyOrEmpty(body);
            await Send(hub, inbox, cmdName, b);
            return Results.Ok(new { ok = true, queued = inbox.Count });
        });
    }

    static bool PayloadHasScenario(object? payload, string scenarioId)
    {
        if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty("scenarioId", out var s) && s.ValueKind == JsonValueKind.String)
            return string.Equals(s.GetString(), scenarioId, StringComparison.Ordinal);
        return false;
    }

    static async Task Send(IHubContext<RpgHub> hub, InjectorCommandInbox inbox, string name, object payload, string? cmdId = null)
    {
        var cmd = new CommandDto { Name = name, Payload = payload, Id = cmdId ?? Guid.NewGuid().ToString("N") };
        inbox.Enqueue(cmd);
        try { await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("Command", cmd); }
        catch { /* inbox poll */ }
    }
}
