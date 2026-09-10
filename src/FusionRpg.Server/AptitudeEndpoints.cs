using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Power;
using FusionRpg.Core.Progression;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// Player aptitude allocate surfaces: commander (Mode C), species GET, UniqueDemon GET/POST (Mode A —
/// aptitude-sheet <c>unique-allocate</c>). Commander-only scope decision in historical class-system docs is
/// superseded for UniqueActor sheets; this file owns all three HTTP surfaces.
/// </summary>
public static class AptitudeEndpoints
{
    public static void MapAptitudes(this WebApplication app)
    {
        var g = app.MapGroup("/api/aptitudes");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store, IPowerIndexProvider powerIndex) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(ProjectState(store, powerIndex, playerId));
        });

        g.MapPost("/allocate", (AllocateAptitudesRequest body, RpgStore store, IPowerIndexProvider powerIndex, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (body.Shares is null) return Results.BadRequest(new { reason = "shares.missing" });

            AptitudeAllocation allocation;
            try
            {
                allocation = body.Shares.Aggregate(AptitudeAllocation.Empty,
                    (acc, kv) => acc + AptitudeAllocation.Single(AllocationScope.Commander, kv.Key, kv.Value));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { reason = "aptitudes.unknownid", detail = ex.Message });
            }

            var theta = (long)powerIndex.ActorIndex(new StatContext { PlayerId = pid });
            var check = PointBudget.CheckScope(AllocationScope.Commander, allocation, theta, AptitudeTuningHub.Tuning);
            if (!check.WithinBudget)
                return Results.Conflict(new { reason = "aptitudes.overbudget", spent = check.Spent, budget = check.Budget });

            store.SaveAllocation(AllocationScope.Commander, ScopeKey(pid), allocation);

            _ = BroadcastBestEffort(hub, new AptitudesUpdatedDto(pid, "commander", null, null));
            return Results.Ok(ProjectState(store, powerIndex, pid));
        });

        g.MapGet("/unique/{instanceId}", (string instanceId, RpgStore store, IPowerIndexProvider powerIndex) =>
        {
            var actor = store.GetUniqueActor(instanceId);
            if (actor is null) return Results.NotFound();
            return Results.Ok(ProjectUniqueState(store, powerIndex, actor));
        });

        g.MapPost("/unique/allocate", (AllocateUniqueAptitudesRequest body, RpgStore store, IPowerIndexProvider powerIndex, IHubContext<RpgHub> hub) =>
        {
            if (string.IsNullOrWhiteSpace(body.InstanceId))
                return Results.BadRequest(new { reason = "instanceId.missing" });
            if (body.Shares is null) return Results.BadRequest(new { reason = "shares.missing" });

            var actor = store.GetUniqueActor(body.InstanceId);
            if (actor is null) return Results.NotFound();

            AptitudeAllocation allocation;
            try
            {
                allocation = body.Shares.Aggregate(AptitudeAllocation.Empty,
                    (acc, kv) => acc + AptitudeAllocation.Single(AllocationScope.UniqueDemon, kv.Key, kv.Value));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { reason = "aptitudes.unknownid", detail = ex.Message });
            }

            var source = PointBudget.UniqueDemonSourceFromLevel(actor.Level);
            var check = PointBudget.CheckScope(AllocationScope.UniqueDemon, allocation, source, AptitudeTuningHub.Tuning);
            if (!check.WithinBudget)
                return Results.Conflict(new { reason = "aptitudes.overbudget", spent = check.Spent, budget = check.Budget });

            store.SaveAllocation(AllocationScope.UniqueDemon, actor.InstanceId, allocation);

            _ = BroadcastBestEffort(hub, new AptitudesUpdatedDto(actor.PlayerId, "unique", actor.InstanceId, null));
            return Results.Ok(ProjectUniqueState(store, powerIndex, actor));
        });

        // species GET — writes go through SpeciesBuildEndpoints respec only.
        g.MapGet("/species/{playerId:long}/{speciesId}", (long playerId, string speciesId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            if (!DemonSpeciesCatalog.IsKnown(speciesId))
                return Results.BadRequest(new { reason = "species.unknown" });
            return Results.Ok(ProjectSpeciesState(store, playerId, speciesId));
        });
    }

    /// <summary>Shared AptitudesUpdated emitter (aptitude-sheet live-bus). Sole path for commander /
    /// unique / species / preset-activate broadcasts.</summary>
    public static async Task BroadcastBestEffort(IHubContext<RpgHub> hub, AptitudesUpdatedDto dto)
    {
        // camelCase anonymous shape — FE + injector already parse playerId; scope keys additive (S10/live-bus).
        var payload = new
        {
            playerId = dto.PlayerId,
            scope = dto.Scope,
            instanceId = dto.InstanceId,
            speciesId = dto.SpeciesId
        };
        try { await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("AptitudesUpdated", payload); }
        catch { /* best-effort */ }
        try { await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("AptitudesUpdated", payload); }
        catch { /* best-effort */ }
    }

    public static string ScopeKey(long playerId) => $"player:{playerId}";

    static object ProjectUniqueState(RpgStore store, IPowerIndexProvider powerIndex, UniqueActorDto actor)
    {
        var allocation = store.LoadAllocation(AllocationScope.UniqueDemon, actor.InstanceId);
        var source = PointBudget.UniqueDemonSourceFromLevel(actor.Level);
        var check = PointBudget.CheckScope(AllocationScope.UniqueDemon, allocation, source, AptitudeTuningHub.Tuning);
        var leftover = check.Budget - check.Spent;
        if (leftover < 0) leftover = 0;

        return new
        {
            instanceId = actor.InstanceId,
            playerId = actor.PlayerId,
            specimenLevel = actor.Level,
            budget = check.Budget,
            spent = check.Spent,
            leftover,
            withinBudget = check.WithinBudget,
            shares = AptitudeCatalog.All.ToDictionary(
                a => a.Id, a => allocation.PointsAt(AllocationScope.UniqueDemon, a.Id), StringComparer.Ordinal)
        };
    }

    static object ProjectState(RpgStore store, IPowerIndexProvider powerIndex, long playerId)
    {
        var allocation = store.LoadAllocation(AllocationScope.Commander, ScopeKey(playerId));
        var theta = (long)powerIndex.ActorIndex(new StatContext { PlayerId = playerId });
        var check = PointBudget.CheckScope(AllocationScope.Commander, allocation, theta, AptitudeTuningHub.Tuning);

        var species = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        foreach (var speciesId in store.ListLevelledSpeciesIds(playerId))
        {
            var effective = store.EffectiveSpeciesAllocation(playerId, speciesId, AptitudeTuningHub.Tuning);
            species[speciesId] = AptitudeCatalog.All.ToDictionary(
                a => a.Id, a => effective.PointsAt(AllocationScope.DemonType, a.Id), StringComparer.Ordinal);
        }

        return new
        {
            theta,
            budget = check.Budget,
            spent = check.Spent,
            withinBudget = check.WithinBudget,
            shares = AptitudeCatalog.All.ToDictionary(a => a.Id, a => allocation.PointsAt(AllocationScope.Commander, a.Id), StringComparer.Ordinal),
            species
        };
    }

    static object ProjectSpeciesState(RpgStore store, long playerId, string speciesId)
    {
        var demonTypeId = DemonSpeciesCatalog.Get(speciesId).DemonTypeId;
        var level = store.GetRpgActor(playerId, RpgActorKinds.Species, demonTypeId)?.Level ?? 1;
        var allocation = store.EffectiveSpeciesAllocation(playerId, speciesId, AptitudeTuningHub.Tuning);
        var source = PointBudget.DemonTypeSourceFromLevel(level);
        var check = PointBudget.CheckScope(AllocationScope.DemonType, allocation, source, AptitudeTuningHub.Tuning);

        var baseline = store.SpeciesBaselineAllocation(playerId, speciesId, AptitudeTuningHub.Tuning);
        return new
        {
            speciesId,
            level,
            budget = check.Budget,
            spent = check.Spent,
            withinBudget = check.WithinBudget,
            hasOverride = store.HasSpeciesOverride(playerId, speciesId),
            shares = AptitudeCatalog.All.ToDictionary(
                a => a.Id, a => allocation.PointsAt(AllocationScope.DemonType, a.Id), StringComparer.Ordinal),
            baseline = AptitudeCatalog.All.ToDictionary(
                a => a.Id, a => baseline.PointsAt(AllocationScope.DemonType, a.Id), StringComparer.Ordinal)
        };
    }

    public sealed class AllocateAptitudesRequest
    {
        public long? PlayerId { get; set; }
        public Dictionary<string, long>? Shares { get; set; }
    }

    public sealed class AllocateUniqueAptitudesRequest
    {
        public string? InstanceId { get; set; }
        public Dictionary<string, long>? Shares { get; set; }
    }

    public sealed record AptitudesUpdatedDto(
        long PlayerId,
        string Scope,
        string? InstanceId,
        string? SpeciesId);
}
