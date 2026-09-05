using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Progression;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// species-build-todo.md T4.3 — spec-species-respec.md's own ⛔: "spends are never a generic endpoint,
/// each with its own reason." The single player-facing surface over
/// <see cref="RpgStore.TryRespecSpecies"/> (T4.2) — first override and revert-to-baseline are free,
/// every other change is priced and escalates with that species' own churn count (decision 15). Same
/// budget gate the pre-existing <c>/api/aptitudes/species/allocate</c> route already enforces
/// (`PointBudget.CheckScope`) — a respec still cannot buy more points than the level allows, pricing
/// is an ADDITIONAL friction, never a replacement for the anti-cheat budget cap.
///
/// <para>The sibling bypass this module's own T4.3 evidence once named — <c>AptitudeEndpoints.cs</c>'s
/// <c>POST /api/aptitudes/species/allocate</c> (module 5, `demon-type-allocation`), which wrote a
/// DemonType override directly via <c>store.SaveAllocation</c> with zero pricing awareness — was
/// RETIRED (owner decision, 2026-09-05: "retire it now"), not just documented. This endpoint is now the
/// only write path for a species aptitude override; that route's GET twin still serves reads.</para>
/// </summary>
public static class SpeciesBuildEndpoints
{
    public static void MapSpeciesBuild(this WebApplication app)
    {
        var g = app.MapGroup("/api/species-build");

        g.MapPost("/respec", (RespecSpeciesRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.SpeciesId) || !DemonSpeciesCatalog.IsKnown(body.SpeciesId))
                return Results.BadRequest(new { reason = "species.unknown" });
            if (body.Shares is null) return Results.BadRequest(new { reason = "shares.missing" });
            if (string.IsNullOrWhiteSpace(body.CorrelationId))
                return Results.BadRequest(new { reason = "correlation.missing" });

            AptitudeAllocation newOverride;
            try
            {
                newOverride = body.Shares.Aggregate(AptitudeAllocation.Empty,
                    (acc, kv) => acc + AptitudeAllocation.Single(AllocationScope.DemonType, kv.Key, kv.Value));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { reason = "aptitudes.unknownid", detail = ex.Message });
            }

            var demonTypeId = DemonSpeciesCatalog.Get(body.SpeciesId).DemonTypeId;
            var level = store.GetRpgActor(pid, RpgActorKinds.Species, demonTypeId)?.Level ?? 1;
            var source = PointBudget.DemonTypeSourceFromLevel(level);
            var check = PointBudget.CheckScope(AllocationScope.DemonType, newOverride, source, AptitudeTuningHub.Tuning);
            if (!check.WithinBudget)
                return Results.Conflict(new { reason = "aptitudes.overbudget", spent = check.Spent, budget = check.Budget });

            var outcome = store.TryRespecSpecies(pid, body.SpeciesId, newOverride, body.CorrelationId);
            if (!outcome.Ok)
                return Results.Conflict(new { reason = outcome.Reason, priceAmount = outcome.PriceAmount });

            _ = BroadcastBestEffort(hub, pid);
            return Results.Ok(new
            {
                speciesId = body.SpeciesId,
                level,
                priced = outcome.Priced,
                priceAmount = outcome.PriceAmount,
                respecCount = outcome.RespecCount,
                soulBalance = outcome.Balance.Balance,
                replay = outcome.Reason == "replay",
                shares = AptitudeCatalog.All.ToDictionary(
                    a => a.Id, a => newOverride.PointsAt(AllocationScope.DemonType, a.Id), StringComparer.Ordinal)
            });
        });

        // Pricing preview -- lets a client show the cost BEFORE the player commits to spending it,
        // without mutating anything (GetSpeciesRespecCount is read-only, same decay-on-read the spend
        // path itself uses).
        g.MapGet("/respec-price/{playerId:long}/{speciesId}", (long playerId, string speciesId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            if (!DemonSpeciesCatalog.IsKnown(speciesId))
                return Results.BadRequest(new { reason = "species.unknown" });

            var count = store.GetSpeciesRespecCount(playerId, speciesId);
            var price = RespecPolicy.PriceOf(FusionRpg.Core.Demons.Generation.SpeciesBuildTuningHub.Tuning, count);
            return Results.Ok(new
            {
                speciesId,
                respecCount = count,
                priceResource = price.Resource.ToString(),
                priceAmount = price.Amount,
                // species-build-todo.md T5.1: the persistent "ever touched" marker, distinct from
                // respecCount (which decays back to zero) -- a client predicting free-vs-priced before
                // attempting a save must read THIS, never respecCount == 0.
                everRespecced = store.HasEverRespecced(playerId, speciesId)
            });
        });
    }

    static async Task BroadcastBestEffort(IHubContext<RpgHub> hub, long playerId)
    {
        // Both groups -- same ⛔ this session already found and fixed once for the Commander/species
        // allocation endpoints (AptitudeEndpoints.cs): an injector-only connection never joins
        // WebGroup, so a WebGroup-only send would leave CheatState.SpeciesAllocation stale.
        try { await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("AptitudesUpdated", new { playerId }); }
        catch { /* best-effort; the allocation is durable and the next GET reflects it */ }
        try { await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("AptitudesUpdated", new { playerId }); }
        catch { /* best-effort; the injector re-syncs at its own next session start regardless */ }
    }

    public sealed class RespecSpeciesRequest
    {
        public long? PlayerId { get; set; }
        public string? SpeciesId { get; set; }
        public Dictionary<string, long>? Shares { get; set; }
        public string? CorrelationId { get; set; }
    }
}
