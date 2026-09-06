using FusionRpg.Contracts;
using FusionRpg.Core.Dungeon.Tuning;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// party-dungeon D2.23 (spec-delve-attrition.md §7) — the priced escape from `Recovering`. The rest
/// of the delve surface (`start`, `{delveId}`, `quests`) belongs to `domain-catalog`/`delve-stage`,
/// unbuilt as of this task; this file is only the one endpoint D2.23 itself names.
/// </summary>
public static class DelveEndpoints
{
    public static void MapDelve(this WebApplication app)
    {
        var g = app.MapGroup("/api/delve");

        g.MapPost("/recovery-ritual", async (RecoveryRitualRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            var corrError = ValidateCorrelation(body.CorrelationId);
            if (corrError != null) return corrError;

            var (ok, reason, actor) = store.TryPerformRecoveryRitual(
                pid, body.InstanceId ?? "", body.CorrelationId!, DungeonTuningHub.Tuning);
            if (!ok) return Refusal(reason);
            await NotifyAsync(hub, pid);
            return Results.Ok(new { actor });
        });
    }

    static IResult? ValidateCorrelation(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return Results.BadRequest(new { reason = "correlation.missing" });
        return correlationId.Trim().Length > 64
            ? Results.BadRequest(new { reason = "correlation.toolong" })
            : null;
    }

    /// <summary>A price the player cannot meet is a conflict, not malformed input — same mapping
    /// `ContractEndpoints.Refusal` already uses for the identical "insufficient souls" shape.</summary>
    static IResult Refusal(string reason) => reason switch
    {
        "souls.insufficient" => Results.Conflict(new { reason }),
        "not_found" or "recovery.missing" or "rung.unknown" => Results.NotFound(new { reason }),
        _ => Results.BadRequest(new { reason }),
    };

    static async Task NotifyAsync(IHubContext<RpgHub> hub, long playerId)
    {
        try
        {
            await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("DelveUpdated", new { playerId });
            await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("SoulsUpdated", new { playerId });
        }
        catch
        {
            // best-effort: the write is durable, the next read reconciles
        }
    }

    public sealed class RecoveryRitualRequest
    {
        public long? PlayerId { get; set; }
        public string? InstanceId { get; set; }
        public string? CorrelationId { get; set; }
    }
}
