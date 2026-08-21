using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Contracts;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// Contracts (spec-demon-contracts.md): binding slots and loyalty. Every read settles first — the
/// tribute clock is lazy, so "look at your contracts" is also "bring the books up to date".
/// </summary>
public static class ContractEndpoints
{
    public static void MapContracts(this WebApplication app)
    {
        var g = app.MapGroup("/api/contracts");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            store.SettleContracts(playerId);
            return Results.Ok(ProjectState(store, playerId));
        });

        g.MapPost("/bind", async (ContractRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();

            var (ok, reason, _) = store.BindContract(pid, body.InstanceId ?? "");
            if (!ok) return Refusal(reason);
            await NotifyAsync(hub, pid);
            return Results.Ok(ProjectState(store, pid));
        });

        g.MapPost("/release", async (ContractRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();

            var (ok, reason, _) = store.ReleaseContract(pid, body.InstanceId ?? "");
            if (!ok) return Refusal(reason);
            await NotifyAsync(hub, pid);
            return Results.Ok(ProjectState(store, pid));
        });

        g.MapPost("/ritual", async (ContractRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            var corrError = ValidateCorrelation(body.CorrelationId);
            if (corrError != null) return corrError;

            var (ok, reason, _) = store.PerformRitual(pid, body.InstanceId ?? "", body.CorrelationId!);
            if (!ok) return Refusal(reason);
            await NotifyAsync(hub, pid);
            return Results.Ok(ProjectState(store, pid));
        });

        g.MapPost("/slots/buy", async (ContractRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            var corrError = ValidateCorrelation(body.CorrelationId);
            if (corrError != null) return corrError;

            var (ok, reason, _) = store.BuyContractSlot(pid, body.CorrelationId!);
            if (!ok) return Refusal(reason);
            await NotifyAsync(hub, pid);
            return Results.Ok(ProjectState(store, pid));
        });
    }

    /// <summary>SIM clock hook: settles as if <c>days</c> had passed, so tribute and decay are
    /// testable without waiting for real midnights. Travelling forward is safe — a later real
    /// settle sees a future stamp and computes zero elapsed days.</summary>
    public static void MapContractTest(this RouteGroupBuilder test)
    {
        test.MapPost("/contracts/settle", (ContractSettleTestRequest body, RpgStore store) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            var result = store.SettleContracts(pid, DateTimeOffset.UtcNow.AddDays(Math.Max(0, body.Days)));
            return Results.Ok(new
            {
                daysSettled = result.DaysSettled,
                soulsPaid = result.SoulsPaid,
                demonsDecayed = result.DemonsDecayed,
                state = ProjectState(store, pid)
            });
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

    /// <summary>A price the player cannot meet is a conflict, not malformed input.</summary>
    static IResult Refusal(string reason) => reason is "souls.insufficient"
        ? Results.Conflict(new { reason })
        : Results.BadRequest(new { reason });

    static async Task NotifyAsync(IHubContext<RpgHub> hub, long playerId)
    {
        try
        {
            await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("ContractsUpdated", new { playerId });
            await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("SoulsUpdated", new { playerId });
        }
        catch
        {
            // best-effort: the write is durable, the next read reconciles
        }
    }

    public static object ProjectState(RpgStore store, long playerId)
    {
        var state = store.GetContractState(playerId);
        var purchased = state?.PurchasedSlots ?? 0;
        var contracts = store.ListContracts(playerId);
        var rarities = store.ListDemonRoster(playerId).Items
            .ToDictionary(s => s.Profile.InstanceId, s => s.Profile.Rarity, StringComparer.Ordinal);

        var rows = contracts.Select(c =>
        {
            DemonRarityIds.TryParse(
                rarities.TryGetValue(c.InstanceId, out var r) ? r : "common", out var rarity);
            return new
            {
                instanceId = c.InstanceId,
                bound = c.Bound,
                loyalty = c.Loyalty,
                rank = c.Rank.ToString().ToLowerInvariant(),
                rankBonusMilli = ContractPolicy.RankBonusMilli(c.Rank),
                personality = c.Personality.ToId(),
                upkeepPerDay = ContractPolicy.UpkeepPerDay(rarity, c.Personality),
                deployable = c.Deployable
            };
        }).OrderBy(c => c.instanceId, StringComparer.Ordinal).ToList();

        return new
        {
            capacity = new
            {
                used = rows.Count(c => c.bound),
                total = ContractPolicy.Capacity(purchased),
                purchasedSlots = purchased,
                nextSlotPrice = ContractPolicy.NextSlotPrice(purchased),
                canBuy = ContractPolicy.CanBuySlot(purchased),
                maxSlots = ContractPolicy.MaxSlots
            },
            dailyTribute = rows.Where(c => c.bound).Sum(c => (long)c.upkeepPerDay),
            deployFloor = ContractPolicy.DeployFloor,
            loyaltyMax = ContractPolicy.LoyaltyMax,
            contracts = rows
        };
    }

    public sealed class ContractRequest
    {
        public long? PlayerId { get; set; }
        public string? InstanceId { get; set; }
        public string? CorrelationId { get; set; }
    }

    public sealed class ContractSettleTestRequest
    {
        public long? PlayerId { get; set; }
        public int Days { get; set; }
    }
}
