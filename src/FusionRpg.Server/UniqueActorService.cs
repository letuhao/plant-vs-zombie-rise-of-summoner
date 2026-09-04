using FusionRpg.Contracts;
using FusionRpg.Core.Match;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>Cold UniqueActor FSM orchestration (W4–W5). Pending GC via Injector command.</summary>
public sealed class UniqueActorService
{
    public static readonly TimeSpan DefaultDeployTimeout = TimeSpan.FromSeconds(30);

    readonly RpgStore _store;
    readonly InjectorCommandInbox _inbox;
    readonly IHubContext<RpgHub> _hub;

    public UniqueActorService(RpgStore store, InjectorCommandInbox inbox, IHubContext<RpgHub> hub)
    {
        _store = store;
        _inbox = inbox;
        _hub = hub;
    }

    public UniqueActorDto Create(long playerId, string side, int typeId) =>
        _store.CreateUniqueActor(playerId, side, typeId);

    public UniqueActorDto? Get(string instanceId) => _store.GetUniqueActor(instanceId);

    public UniqueActorListDto List(long playerId) => _store.ListUniqueActors(playerId);

    public (bool Ok, string Reason, UniqueActorDto? Actor) Retire(string instanceId) =>
        _store.TryRetireUniqueActor(instanceId);

    public UniqueEquipmentListDto? GetEquipment(string instanceId) =>
        _store.GetUniqueEquipment(instanceId);

    /// <summary>Roster-only equip. Rebuilds mods_json grants from stub catalog.</summary>
    public (bool Ok, string Reason, UniqueEquipmentListDto? Equipment) PutEquipment(
        string instanceId, string slot, string? itemId)
    {
        var actor = _store.GetUniqueActor(instanceId);
        if (actor is null) return (false, "not_found", null);
        if (!string.Equals(actor.Phase, UniqueActorPhases.Roster, StringComparison.Ordinal))
            return (false, "phase.not_roster", null);
        try
        {
            var eq = _store.UpsertUniqueEquipment(instanceId, slot, itemId);
            return (true, "", eq);
        }
        catch (ArgumentException ex)
        {
            if (string.Equals(ex.ParamName, "itemId", StringComparison.Ordinal))
                return (false, ex.Message.StartsWith("slot_mismatch", StringComparison.Ordinal) ? "slot_mismatch" : "unknown_item", null);
            return (false, "bad_slot", null);
        }
        catch (InvalidOperationException)
        {
            return (false, "not_found", null);
        }
    }

    public (bool Ok, string Reason, UniqueEquipmentListDto? Equipment) ClearEquipment(
        string instanceId, string slot) =>
        PutEquipment(instanceId, slot, "");

    public (bool Ok, string Reason, UniqueActorDto? Actor) AwardXp(
        string instanceId, long delta, string? reason) =>
        _store.AwardUniqueActorXp(instanceId, delta, reason);


    public (bool Ok, string Reason, UniqueActorDto? Actor) FailDeploy(string instanceId)
    {
        var before = _store.GetUniqueActor(instanceId);
        var result = _store.TryFailUniqueDeploy(instanceId);
        if (result.Ok)
            _ = NotifyBindingClearAsync(before?.InstanceId ?? instanceId, before?.DeployCorrelationId);
        return result;
    }

    /// <summary>W5-D: expire stuck Deploying rows → Roster + Injector Pending GC.</summary>
    public int FailExpiredDeploys(TimeSpan? timeout = null, DateTimeOffset? utcNow = null)
    {
        var failed = _store.FailExpiredUniqueDeploys(timeout ?? DefaultDeployTimeout, utcNow);
        foreach (var (id, corr) in failed)
            _ = NotifyBindingClearAsync(id, corr);
        return failed.Count;
    }

    /// <summary>
    /// Roster → Deploying + enqueue pvz.spawn.extra with instanceId + correlationId + loadout.
    /// Idempotent on same correlationId.
    /// </summary>
    public async Task<UniqueActorDeployResultDto> DeployAsync(
        string instanceId,
        string? correlationId,
        int? col,
        int? row,
        string? matchKey,
        string? loadoutJson = null,
        CancellationToken ct = default)
    {
        var actor = _store.GetUniqueActor(instanceId);
        if (actor is null)
            return Fail("not_found", correlationId ?? "");

        var corr = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim();

        var begin = _store.TryBeginUniqueDeploy(instanceId, corr, matchKey);
        if (!begin.Ok)
            return new UniqueActorDeployResultDto
            {
                Ok = false,
                Reason = begin.Reason,
                Queued = false,
                CorrelationId = corr,
                Actor = begin.Actor
            };

        if (!begin.Queued)
        {
            return new UniqueActorDeployResultDto
            {
                Ok = true,
                Reason = "",
                Queued = false,
                CorrelationId = corr,
                Actor = begin.Actor
            };
        }

        var side = begin.Actor!.Side;
        var typeId = begin.Actor.TypeId;
        try
        {
            _store.RecordExtraSpawnIntent(begin.Actor.PlayerId, corr, typeId, "unique-deploy", side);
        }
        catch
        {
            /* activity rollup optional for unique path */
        }

        var effectiveLoadout = UniqueLoadoutMerge.Merge(loadoutJson, _store.GetUniqueStatModsJson(instanceId));

        await SendInjectorCommand(_hub, _inbox, new CommandDto
        {
            Id = corr,
            Name = "pvz.spawn.extra",
            Payload = new
            {
                typeId,
                col,
                row,
                reason = "unique-deploy",
                correlationId = corr,
                side,
                playerId = begin.Actor.PlayerId,
                instanceId,
                source = "extra",
                loadoutJson = effectiveLoadout
            }
        }).ConfigureAwait(false);

        return new UniqueActorDeployResultDto
        {
            Ok = true,
            Reason = "",
            Queued = true,
            CorrelationId = corr,
            Actor = _store.GetUniqueActor(instanceId)
        };
    }

    public void ObserveEvents(IReadOnlyList<EventEnvelope> batch)
    {
        if (batch.Count == 0) return;
        var mapped = new List<(string Kind, string? MatchKey, string PayloadJson)>(batch.Count);
        foreach (var e in batch)
        {
            if (string.IsNullOrWhiteSpace(e.Kind)) continue;
            mapped.Add((e.Kind, e.MatchKey, RpgStore.PayloadToJson(e.Payload)));
        }
        _store.ObserveUniqueActorEvents(mapped);
    }

    Task NotifyBindingClearAsync(string? instanceId, string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) && string.IsNullOrWhiteSpace(correlationId))
            return Task.CompletedTask;
        return SendInjectorCommand(_hub, _inbox, new CommandDto
        {
            Id = "ubc-" + (correlationId ?? instanceId ?? Guid.NewGuid().ToString("N")),
            Name = "unique.binding.clear",
            Payload = new { instanceId, correlationId }
        });
    }

    static UniqueActorDeployResultDto Fail(string reason, string corr) => new()
    {
        Ok = false,
        Reason = reason,
        Queued = false,
        CorrelationId = corr
    };

    static async Task SendInjectorCommand(IHubContext<RpgHub> hub, InjectorCommandInbox inbox, CommandDto cmd)
    {
        inbox.Enqueue(cmd);
        try
        {
            await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("Command", cmd).ConfigureAwait(false);
        }
        catch
        {
            /* inbox poll is the reliable path */
        }
    }
}
