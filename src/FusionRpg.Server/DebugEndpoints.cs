using System.Text.Json;
using FusionRpg.CheatCore;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
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
        MapPost(g, "/ensure-sun", "debug.ensure-sun");
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

        g.MapGet("/effects/contract", () => Results.Ok(new
        {
            contractVersion = FoundationContractVersion.Current,
            frozen = true,
            triggers = new[]
            {
                EffectTriggers.OnSpawn, EffectTriggers.OnDamageDealt, EffectTriggers.OnDamageTaken,
                EffectTriggers.OnDeath, EffectTriggers.OnGranted, EffectTriggers.OnRemoved,
                EffectTriggers.OnTimer
            },
            actions = new[]
            {
                EffectActions.ModifyStat, EffectActions.ApplyStatus, EffectActions.ClearStatus,
                EffectActions.SpawnEntity, EffectActions.BoardAction, EffectActions.SpawnGridItem,
                EffectActions.ClearGridItem, EffectActions.SetBoxType, EffectActions.Economy,
                EffectActions.ApplyResourceDelta
            }
        }));

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
