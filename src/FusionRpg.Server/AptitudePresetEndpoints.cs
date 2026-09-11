using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Power;
using FusionRpg.Core.Progression;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// aptitude-sheet AS-3.1 / AS-3.2 — <c>/api/aptitude-presets</c>: named library CRUD, active binding,
/// D13 materialize, favour GET (S1), transactional Activate (S3). Broadcasts via
/// <see cref="AptitudeEndpoints.BroadcastBestEffort"/>.
/// </summary>
public static class AptitudePresetEndpoints
{
    public static void MapAptitudePresets(this WebApplication app)
    {
        var g = app.MapGroup("/api/aptitude-presets");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            var list = store.ListAptitudePresets(playerId).Select(p => ProjectPreset(store, p)).ToList();
            return Results.Ok(new { presets = list });
        });

        g.MapPost("/", (SaveAptitudePresetRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { reason = "presets.name.missing" });

            var kind = string.IsNullOrWhiteSpace(body.Kind) ? RpgStore.AptitudePresetKindPlayer : body.Kind!.Trim();
            var presetId = string.IsNullOrWhiteSpace(body.PresetId)
                ? Guid.NewGuid().ToString("N")
                : body.PresetId!.Trim();
            var entries = ParseEntries(presetId, body.Rows);
            if (entries is null)
                return Results.BadRequest(new { reason = "presets.rows.missing" });

            var row = new RpgAptitudePresetRow(
                presetId, pid, body.Name.Trim(), kind,
                DateTimeOffset.UtcNow.ToString("o"), Revision: 0);
            var reason = store.SaveAptitudePreset(row, entries, isCreate: true);
            if (reason == "presets.softMax")
                return Results.Conflict(new { reason, softMax = AptitudePresetTuningHub.Tuning.SoftMaxPresets });
            if (!string.IsNullOrEmpty(reason))
                return Results.BadRequest(new { reason });

            _ = BroadcastLibrary(hub, pid);
            var saved = store.GetAptitudePreset(presetId)!;
            return Results.Ok(ProjectPreset(store, saved));
        });

        g.MapPut("/{presetId}", (string presetId, SaveAptitudePresetRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var existing = store.GetAptitudePreset(presetId);
            if (existing is null) return Results.NotFound();
            var pid = body.PlayerId ?? existing.PlayerId;
            if (pid != existing.PlayerId) return Results.BadRequest(new { reason = "presets.owner.mismatch" });
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { reason = "presets.name.missing" });

            var kind = string.IsNullOrWhiteSpace(body.Kind) ? existing.Kind : body.Kind!.Trim();
            var entries = ParseEntries(presetId, body.Rows);
            if (entries is null)
                return Results.BadRequest(new { reason = "presets.rows.missing" });

            var row = existing with { Name = body.Name.Trim(), Kind = kind };
            var reason = store.SaveAptitudePreset(row, entries, isCreate: false);
            if (!string.IsNullOrEmpty(reason))
                return Results.BadRequest(new { reason });

            _ = BroadcastLibrary(hub, pid);
            return Results.Ok(ProjectPreset(store, store.GetAptitudePreset(presetId)!));
        });

        g.MapDelete("/{presetId}", (string presetId, long? playerId, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var existing = store.GetAptitudePreset(presetId);
            if (existing is null) return Results.NotFound();
            var pid = playerId ?? existing.PlayerId;
            if (!store.DeleteAptitudePreset(pid, presetId))
                return Results.NotFound();
            _ = BroadcastLibrary(hub, pid);
            return Results.Ok(new { deleted = presetId });
        });

        g.MapGet("/active", (long playerId, string scope, string? scopeKey, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(scope))
                return Results.BadRequest(new { reason = "presets.scope.missing" });
            var active = store.GetAptitudePresetActive(playerId, scope, scopeKey ?? "");
            if (active is null) return Results.Ok(new { playerId, scope, scopeKey = scopeKey ?? "", presetId = (string?)null });
            return Results.Ok(new
            {
                playerId = active.PlayerId,
                scope = active.Scope,
                scopeKey = active.ScopeKey,
                presetId = active.PresetId
            });
        });

        g.MapPut("/active", (SetActiveRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.Scope))
                return Results.BadRequest(new { reason = "presets.scope.missing" });
            if (string.IsNullOrWhiteSpace(body.PresetId))
                return Results.BadRequest(new { reason = "presets.id.missing" });

            var reason = store.SetAptitudePresetActive(pid, body.Scope!, body.ScopeKey ?? "", body.PresetId!);
            if (reason == "presets.notFound") return Results.NotFound();
            if (!string.IsNullOrEmpty(reason)) return Results.BadRequest(new { reason });

            _ = BroadcastScoped(hub, pid, body.Scope!, body.ScopeKey);
            return Results.Ok(new
            {
                playerId = pid,
                scope = body.Scope,
                scopeKey = body.ScopeKey ?? "",
                presetId = body.PresetId
            });
        });

        g.MapPost("/materialize", (MaterializeRequest body, RpgStore store) =>
        {
            if (string.IsNullOrWhiteSpace(body.PresetId))
                return Results.BadRequest(new { reason = "presets.id.missing" });
            if (body.Budget < 0)
                return Results.BadRequest(new { reason = "presets.budget.negative" });

            var preset = store.GetAptitudePreset(body.PresetId!);
            if (preset is null) return Results.NotFound();
            if (body.PlayerId is long pid && pid != preset.PlayerId)
                return Results.BadRequest(new { reason = "presets.owner.mismatch" });

            var result = AptitudePresetMaterialize.Materialize(
                RpgStore.ToRowSpecs(store.GetAptitudePresetEntries(preset.PresetId)), body.Budget);
            if (!result.Ok)
                return Results.Conflict(new { reason = result.Reason });

            return Results.Ok(new
            {
                presetId = preset.PresetId,
                budget = body.Budget,
                shares = result.Shares,
                leftover = result.Leftover
            });
        });

        // S1 — favour is target permille from SpeciesBuildPlanCatalog; empty {} when no plan (S7).
        g.MapGet("/favour/{speciesId}", (string speciesId) =>
        {
            if (string.IsNullOrWhiteSpace(speciesId))
                return Results.BadRequest(new { reason = "species.missing" });
            if (!SpeciesBuildPlanCatalog.IsConfigured)
                return Results.Ok(new { sharesPermille = new Dictionary<string, long>(StringComparer.Ordinal) });

            var shares = SpeciesBuildPlanCatalog.SharesFor(speciesId);
            // Empty catalog entry → {} (not an error). Planned species: values sum 1000.
            return Results.Ok(new { sharesPermille = shares });
        });

        g.MapPost("/activate", (ActivateRequest body, RpgStore store, IPowerIndexProvider powerIndex, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.PresetId))
                return Results.BadRequest(new { reason = "presets.id.missing" });
            if (string.IsNullOrWhiteSpace(body.Scope))
                return Results.BadRequest(new { reason = "presets.scope.missing" });

            var scope = body.Scope!.Trim();
            var scopeKey = body.ScopeKey ?? "";
            var preset = store.GetAptitudePreset(body.PresetId!);
            if (preset is null) return Results.NotFound();
            if (preset.PlayerId != pid) return Results.BadRequest(new { reason = "presets.owner.mismatch" });

            var budgetResolve = ResolveBudget(store, powerIndex, pid, scope, scopeKey);
            if (!budgetResolve.Ok)
                return Results.BadRequest(new { reason = budgetResolve.Reason });

            var mat = AptitudePresetMaterialize.Materialize(
                RpgStore.ToRowSpecs(store.GetAptitudePresetEntries(preset.PresetId)), budgetResolve.Budget);
            if (!mat.Ok)
                return Results.Conflict(new { reason = mat.Reason });

            AptitudeAllocation allocation;
            try
            {
                allocation = ToAllocation(scope, mat.Shares);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { reason = "aptitudes.unknownid", detail = ex.Message });
            }

            var allocScope = ScopeToAllocation(scope);
            if (allocScope is null)
                return Results.BadRequest(new { reason = "presets.scope.unknown" });

            var check = PointBudget.CheckScope(allocScope.Value, allocation, budgetResolve.Source, AptitudeTuningHub.Tuning);
            if (!check.WithinBudget)
                return Results.Conflict(new { reason = "aptitudes.overbudget", spent = check.Spent, budget = check.Budget });

            var outcome = store.TryActivateAptitudePreset(
                pid, preset.PresetId, scope, scopeKey, allocation, mat.Shares, mat.Leftover, body.CorrelationId);
            if (!outcome.Ok)
            {
                if (outcome.Reason is "presets.notFound") return Results.NotFound();
                if (outcome.Reason is "souls.insufficient")
                    return Results.Conflict(new { reason = outcome.Reason, priceAmount = outcome.PriceAmount });
                return Results.BadRequest(new { reason = outcome.Reason });
            }

            _ = BroadcastScoped(hub, pid, scope, scopeKey);
            return Results.Ok(new
            {
                playerId = pid,
                presetId = preset.PresetId,
                scope,
                scopeKey,
                budget = budgetResolve.Budget,
                shares = outcome.Shares,
                leftover = outcome.Leftover,
                priced = outcome.Priced,
                priceAmount = outcome.PriceAmount,
                respecCount = outcome.RespecCount,
                soulBalance = outcome.Balance?.Balance,
                replay = outcome.Reason == "replay"
            });
        });
    }

    static object ProjectPreset(RpgStore store, RpgAptitudePresetRow p)
    {
        var entries = store.GetAptitudePresetEntries(p.PresetId);
        return new
        {
            presetId = p.PresetId,
            playerId = p.PlayerId,
            name = p.Name,
            kind = p.Kind,
            createdUtc = p.CreatedUtc,
            revision = p.Revision,
            rows = entries.Select(e => new
            {
                aptitudeId = e.AptitudeId,
                targetPermille = e.TargetPermille,
                minAbs = e.MinAbs,
                maxAbs = e.MaxAbs,
                minPermille = e.MinPermille,
                maxPermille = e.MaxPermille
            }).ToList()
        };
    }

    static List<RpgAptitudePresetEntryRow>? ParseEntries(string presetId, List<PresetRowDto>? rows)
    {
        if (rows is null || rows.Count == 0) return null;
        return rows.Select(r => new RpgAptitudePresetEntryRow(
            presetId,
            r.AptitudeId ?? "",
            r.TargetPermille,
            r.MinAbs,
            r.MaxAbs,
            r.MinPermille,
            r.MaxPermille)).ToList();
    }

    static AptitudeAllocation ToAllocation(string scope, IReadOnlyDictionary<string, long> shares)
    {
        var allocScope = ScopeToAllocation(scope)
            ?? throw new ArgumentException($"unknown scope '{scope}'");
        return shares.Aggregate(AptitudeAllocation.Empty,
            (acc, kv) => acc + AptitudeAllocation.Single(allocScope, kv.Key, kv.Value));
    }

    static AllocationScope? ScopeToAllocation(string scope) => scope switch
    {
        "commander" => AllocationScope.Commander,
        "unique" => AllocationScope.UniqueDemon,
        "species" => AllocationScope.DemonType,
        _ => null
    };

    static (bool Ok, string Reason, long Budget, long Source) ResolveBudget(
        RpgStore store, IPowerIndexProvider powerIndex, long playerId, string scope, string scopeKey)
    {
        switch (scope)
        {
            case "commander":
            {
                var theta = (long)powerIndex.ActorIndex(new StatContext { PlayerId = playerId });
                var budget = PointBudget.PointsFor(AllocationScope.Commander, theta, AptitudeTuningHub.Tuning);
                return (true, "", budget, theta);
            }
            case "unique":
            {
                if (string.IsNullOrWhiteSpace(scopeKey))
                    return (false, "presets.scopeKey.missing", 0, 0);
                var actor = store.GetUniqueActor(scopeKey);
                if (actor is null) return (false, "unique.notFound", 0, 0);
                if (actor.PlayerId != playerId) return (false, "presets.owner.mismatch", 0, 0);
                var source = PointBudget.UniqueDemonSourceFromLevel(actor.Level);
                var budget = PointBudget.PointsFor(AllocationScope.UniqueDemon, source, AptitudeTuningHub.Tuning);
                return (true, "", budget, source);
            }
            case "species":
            {
                if (string.IsNullOrWhiteSpace(scopeKey) || !DemonSpeciesCatalog.IsKnown(scopeKey))
                    return (false, "species.unknown", 0, 0);
                var demonTypeId = DemonSpeciesCatalog.Get(scopeKey).DemonTypeId;
                var level = store.GetRpgActor(playerId, RpgActorKinds.Species, demonTypeId)?.Level ?? 1;
                var source = PointBudget.DemonTypeSourceFromLevel(level);
                var budget = PointBudget.PointsFor(AllocationScope.DemonType, source, AptitudeTuningHub.Tuning);
                return (true, "", budget, source);
            }
            default:
                return (false, "presets.scope.unknown", 0, 0);
        }
    }

    static Task BroadcastLibrary(IHubContext<RpgHub> hub, long playerId) =>
        AptitudeEndpoints.BroadcastBestEffort(
            hub, new AptitudeEndpoints.AptitudesUpdatedDto(playerId, "commander", null, null));

    static Task BroadcastScoped(IHubContext<RpgHub> hub, long playerId, string scope, string? scopeKey) =>
        AptitudeEndpoints.BroadcastBestEffort(hub, scope switch
        {
            "unique" => new AptitudeEndpoints.AptitudesUpdatedDto(playerId, "unique", scopeKey, null),
            "species" => new AptitudeEndpoints.AptitudesUpdatedDto(playerId, "species", null, scopeKey),
            _ => new AptitudeEndpoints.AptitudesUpdatedDto(playerId, "commander", null, null)
        });

    public sealed class SaveAptitudePresetRequest
    {
        public long? PlayerId { get; set; }
        public string? PresetId { get; set; }
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public List<PresetRowDto>? Rows { get; set; }
    }

    public sealed class PresetRowDto
    {
        public string? AptitudeId { get; set; }
        public long TargetPermille { get; set; }
        public long? MinAbs { get; set; }
        public long? MaxAbs { get; set; }
        public long? MinPermille { get; set; }
        public long? MaxPermille { get; set; }
    }

    public sealed class SetActiveRequest
    {
        public long? PlayerId { get; set; }
        public string? Scope { get; set; }
        public string? ScopeKey { get; set; }
        public string? PresetId { get; set; }
    }

    public sealed class MaterializeRequest
    {
        public long? PlayerId { get; set; }
        public string? PresetId { get; set; }
        public long Budget { get; set; }
    }

    public sealed class ActivateRequest
    {
        public long? PlayerId { get; set; }
        public string? PresetId { get; set; }
        public string? Scope { get; set; }
        public string? ScopeKey { get; set; }
        public string? CorrelationId { get; set; }
    }
}
