using FusionRpg.Contracts;
using FusionRpg.Core.Aura;
using FusionRpg.Core.Commanders;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// commander-surface P1: persisted default lawn commander + empire list for the web FE. Single route
/// group for default GET/POST and roster GET (spec-default-persistence, spec-commander-list-api).
/// </summary>
public static class CommanderEndpoints
{
    public static void MapCommanders(this WebApplication app)
    {
        var g = app.MapGroup("/api/commanders");

        g.MapGet("/{playerId:long}/default", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(new DefaultLawnCommanderResponse
            {
                DefaultLawnCommanderId = store.GetDefaultLawnCommanderId(playerId),
            });
        });

        g.MapPost("/default", async (SetDefaultLawnCommanderRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.CommanderId))
                return Results.BadRequest(new { reason = "commander.missing" });

            var (ok, reason) = store.SetDefaultLawnCommanderId(pid, body.CommanderId);
            if (!ok) return Results.BadRequest(new { reason });

            await BroadcastBestEffort(hub, pid);
            return Results.Ok(new DefaultLawnCommanderResponse
            {
                DefaultLawnCommanderId = store.GetDefaultLawnCommanderId(pid),
            });
        });

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(ProjectList(store, playerId));
        });
    }

    internal static CommanderListResponse ProjectList(RpgStore store, long playerId)
    {
        var defaultId = store.GetDefaultLawnCommanderId(playerId);
        var runtime = AuraRuntimeEndpoints.ResolveRuntimeForEndpoints(playerId, store);
        var equipped = (store.GetLoadout(AuraRuntimeEndpoints.DaveOwnerScope(playerId)) ?? Array.Empty<string>())
            .Where(AuraContentCatalog.IsKnown)
            .ToList();
        var active = equipped.Where(id => runtime.ActiveAuraIds.Contains(id)).ToList();
        var firstActive = active.Count > 0 ? active[0] : null;

        var rows = new List<CommanderListRowDto>();
        foreach (var commander in PlayerEmpireCommanders.ForPlayer(playerId))
        {
            var stableId = commander.ToStableId();
            string? activeAuraId = null;
            string? activeAuraName = null;
            if (commander == CommanderId.Dave && firstActive is not null)
            {
                activeAuraId = firstActive;
                activeAuraName = firstActive;
            }

            rows.Add(new CommanderListRowDto
            {
                Id = stableId,
                DisplayName = PlayerEmpireCommanders.DisplayName(commander),
                IsDefault = string.Equals(stableId, defaultId, StringComparison.Ordinal),
                ActiveAuraId = activeAuraId,
                ActiveAuraName = activeAuraName,
                LocationStub = null,
                LegionStub = null,
            });
        }

        return new CommanderListResponse
        {
            DefaultLawnCommanderId = defaultId,
            Commanders = rows,
        };
    }

    static async Task BroadcastBestEffort(IHubContext<RpgHub> hub, long playerId)
    {
        try { await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("CommandersUpdated", new { playerId }); }
        catch { /* best-effort; GET reflects durable state */ }
        try { await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("CommandersUpdated", new { playerId }); }
        catch { /* best-effort; injector re-syncs at session start regardless */ }
    }
}
