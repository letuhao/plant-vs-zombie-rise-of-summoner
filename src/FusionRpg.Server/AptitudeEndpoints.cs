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
/// class-system-todo.md P9.1/P9.2's own named gap, closed here: `spec-aptitude-allocation-surface.md`
/// — the first player-reachable way to spend aptitude points. Commander scope only (§1's own scope
/// decision — the other three scopes need a specimen picker or `aspect-scope`, neither of which this
/// endpoint needs). `PointBudget`/`AptitudeAllocation`/`AllocationStore` (`point-economy`, Phase 6) are
/// called as-is, never re-derived — this module is a thin, player-facing surface over already-shipped,
/// already-tested math.
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

            _ = BroadcastBestEffort(hub, pid);
            return Results.Ok(ProjectState(store, powerIndex, pid));
        });

        // `demon-type-allocation` (module 5, spec-demon-type-allocation.md §"Commands") — the
        // player-facing surface over EffectiveSpeciesAllocation. Mirrors the Commander pair above
        // exactly: GET projects the effective (baseline-or-override) state, POST replaces the whole
        // vector, both broadcast AptitudesUpdated to BOTH groups (T2.2's own ⛔ callout — a WebGroup-
        // only send is the exact defect a live probe already found once for Commander).
        g.MapGet("/species/{playerId:long}/{speciesId}", (long playerId, string speciesId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            if (!DemonSpeciesCatalog.IsKnown(speciesId))
                return Results.BadRequest(new { reason = "species.unknown" });
            return Results.Ok(ProjectSpeciesState(store, playerId, speciesId));
        });

        g.MapPost("/species/allocate", (AllocateSpeciesAptitudesRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.SpeciesId) || !DemonSpeciesCatalog.IsKnown(body.SpeciesId))
                return Results.BadRequest(new { reason = "species.unknown" });
            if (body.Shares is null) return Results.BadRequest(new { reason = "shares.missing" });

            AptitudeAllocation allocation;
            try
            {
                allocation = body.Shares.Aggregate(AptitudeAllocation.Empty,
                    (acc, kv) => acc + AptitudeAllocation.Single(AllocationScope.DemonType, kv.Key, kv.Value));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { reason = "aptitudes.unknownid", detail = ex.Message });
            }

            var demonTypeId = DemonSpeciesCatalog.Get(body.SpeciesId).DemonTypeId;
            var level = store.GetRpgActor(pid, RpgActorKinds.Species, demonTypeId)?.Level ?? 1;
            var source = PointBudget.DemonTypeSourceFromLevel(level);
            var check = PointBudget.CheckScope(AllocationScope.DemonType, allocation, source, AptitudeTuningHub.Tuning);
            if (!check.WithinBudget)
                return Results.Conflict(new { reason = "aptitudes.overbudget", spent = check.Spent, budget = check.Budget });

            store.SaveAllocation(AllocationScope.DemonType, SpeciesAllocation.ScopeKey(pid, body.SpeciesId), allocation);

            _ = BroadcastBestEffort(hub, pid);
            return Results.Ok(ProjectSpeciesState(store, pid, body.SpeciesId));
        });
    }

    static async Task BroadcastBestEffort(IHubContext<RpgHub> hub, long playerId)
    {
        // Both groups, matching Program.cs/SimEndpoints.cs's own PvzStatsUpdated pattern: the web
        // client refetches on this event, and the injector's RpgClient.cs:93 handler (enqueues
        // "aptitudes.allocation.reload" -> RefreshCommanderAllocationAsync) needs it too -- an
        // injector connection only ever joins InjectorGroup (RpgHub.cs:27-28), so a WebGroup-only
        // send here left CheatState.CommanderAllocation stale until the next injector reconnect.
        // Found 2026-08-30 verifying aura-skill T5/T6's own "wired end-to-end" claim against a real
        // live game -- confirmed dead via a live probe, not assumed from reading code alone.
        try { await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("AptitudesUpdated", new { playerId }); }
        catch { /* best-effort; the allocation is durable and the next GET reflects it */ }
        try { await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("AptitudesUpdated", new { playerId }); }
        catch { /* best-effort; the injector re-syncs at its own next session start regardless */ }
    }

    /// <summary>The store is agnostic to key shape (`RpgStore.Aptitudes.cs`'s own contract) — matches
    /// `AllocationStoreTests.cs`'s own established `"player:{id}"` convention for the commander scope.</summary>
    public static string ScopeKey(long playerId) => $"player:{playerId}";

    static object ProjectState(RpgStore store, IPowerIndexProvider powerIndex, long playerId)
    {
        var allocation = store.LoadAllocation(AllocationScope.Commander, ScopeKey(playerId));
        var theta = (long)powerIndex.ActorIndex(new StatContext { PlayerId = playerId });
        var check = PointBudget.CheckScope(AllocationScope.Commander, allocation, theta, AptitudeTuningHub.Tuning);

        // species-build T3.1 (module 6, allocation-transport): additive only — `shares` below is
        // byte-unchanged for a player with no species allocations (spec's own ⛔ callout: RpgClient.cs
        // hard-requires the literal key "shares", a rename would silently stop every allocation
        // applying). Only species this player has actually levelled are sent, never the full corpus.
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
        return new
        {
            speciesId,
            level,
            budget = check.Budget,
            spent = check.Spent,
            withinBudget = check.WithinBudget,
            shares = AptitudeCatalog.All.ToDictionary(
                a => a.Id, a => allocation.PointsAt(AllocationScope.DemonType, a.Id), StringComparer.Ordinal)
        };
    }

    public sealed class AllocateAptitudesRequest
    {
        public long? PlayerId { get; set; }
        public Dictionary<string, long>? Shares { get; set; }
    }

    public sealed class AllocateSpeciesAptitudesRequest
    {
        public long? PlayerId { get; set; }
        public string? SpeciesId { get; set; }
        public Dictionary<string, long>? Shares { get; set; }
    }
}
