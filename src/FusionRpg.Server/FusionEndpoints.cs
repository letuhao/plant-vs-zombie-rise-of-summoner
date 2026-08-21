using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// Fusion lab (spec-demon-fusion.md): preview is a pure read; execute is the one-transaction
/// store call with a server-minted seed; the recipe browser projects silhouettes — an
/// undiscovered recipe's id and output species never reach the wire (the output IS the
/// discovery), only its rarity band and, once both ingredient species are in the codex, its
/// input hint.
/// </summary>
public static class FusionEndpoints
{
    public static void MapFusion(this WebApplication app)
    {
        var g = app.MapGroup("/api/fusion");

        g.MapPost("/preview", (FusionHttpRequest body, RpgStore store) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            return Results.Ok(BuildPreview(store, pid, body));
        });

        g.MapPost("/execute", async (FusionHttpRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.CorrelationId))
                return Results.BadRequest(new { reason = "correlation.missing" });
            if (body.CorrelationId.Trim().Length > 64)
                return Results.BadRequest(new { reason = "correlation.toolong" });

            var seed = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
            var (ok, reason, outcome) = store.ExecuteFusion(pid, body.CorrelationId!, ToRequest(body), seed);
            if (!ok)
            {
                return reason is "souls.insufficient" or "materials.insufficient"
                    ? Results.Conflict(new { reason })
                    : Results.BadRequest(new { reason });
            }

            if (!outcome!.Replayed)
            {
                try
                {
                    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("DemonsUpdated", new { playerId = pid });
                    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("SoulsUpdated", new { playerId = pid });
                }
                catch
                {
                    // best-effort; the fusion is durable and replay recovers the outcome
                }
            }

            return Results.Ok(new
            {
                replayed = outcome!.Replayed,
                mode = outcome.Mode,
                @base = outcome.Base,
                minted = outcome.Minted,
                recipeId = outcome.RecipeId,
                newlyDiscovered = outcome.NewlyDiscovered,
                discoverySouls = outcome.DiscoverySouls,
                balance = outcome.Balance
            });
        });

        g.MapGet("/{playerId:long}/recipes", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            var discovered = new HashSet<string>(store.ListFusionDiscoveries(playerId), StringComparer.Ordinal);
            var codex = store.ListDemonCodex(playerId).Entries
                .Where(e => e.State == DemonCodexStates.Discovered)
                .Select(e => e.SpeciesId)
                .ToHashSet(StringComparer.Ordinal);

            var items = DemonRecipeCatalog.All
                .OrderBy(r => DemonSpeciesCatalog.Get(r.OutputSpeciesId).BaseRarity)
                .ThenBy(r => r.RecipeId, StringComparer.Ordinal)
                .Select((r, slot) =>
                {
                    var output = DemonSpeciesCatalog.Get(r.OutputSpeciesId);
                    var cost = FusionCostTable.Recipe(output.BaseRarity);
                    var isDiscovered = discovered.Contains(r.RecipeId);
                    var hintsUnlocked = codex.Contains(r.InputSpeciesIdA) && codex.Contains(r.InputSpeciesIdB);
                    return isDiscovered
                        ? (object)new
                        {
                            slot,
                            discovered = true,
                            recipeId = r.RecipeId,
                            resultSpeciesId = r.OutputSpeciesId,
                            resultRarity = output.BaseRarity.ToId(),
                            inputs = new[] { r.InputSpeciesIdA, r.InputSpeciesIdB },
                            cost = ProjectCost(cost, output)
                        }
                        : new
                        {
                            slot,
                            discovered = false,
                            resultRarity = output.BaseRarity.ToId(),
                            // The experiment hint: ingredients show once both species are known.
                            inputs = hintsUnlocked ? new[] { r.InputSpeciesIdA, r.InputSpeciesIdB } : null,
                            cost = ProjectCost(cost, output)
                        };
                });
            return Results.Ok(new { items });
        });
    }

    static object BuildPreview(RpgStore store, long playerId, FusionHttpRequest body)
    {
        var roster = store.ListDemonRoster(playerId).Items
            .ToDictionary(s => s.Profile.InstanceId, StringComparer.Ordinal);

        switch (body.Mode)
        {
            case FusionModes.StarMerge:
            case FusionModes.Promotion:
            {
                if (body.BaseInstanceId is null || !roster.TryGetValue(body.BaseInstanceId, out var baseSpec))
                    return new { ok = false, reason = "base.missing" };
                if (!DemonRarityIds.TryParse(baseSpec.Profile.Rarity, out var rarity))
                    return new { ok = false, reason = "base.rarity" };
                if (body.Mode == FusionModes.StarMerge)
                {
                    if (baseSpec.Profile.Star >= StarPolicy.StarCap(rarity))
                        return new { ok = false, reason = "star.maxed" };
                    var target = baseSpec.Profile.Star + 1;
                    return new
                    {
                        ok = true,
                        targetStar = target,
                        sacrificesNeeded = StarPolicy.SacrificesForStar(target),
                        cost = ProjectCost(FusionCostTable.StarMerge(rarity), baseSpec.Profile.ElementPrimary)
                    };
                }

                if (!StarPolicy.CanPromote(rarity, baseSpec.Profile.Star, baseSpec.Profile.Promoted))
                    return new { ok = false, reason = "promotion.not-ready" };
                var newRarity = (DemonRarity)((int)rarity + 1);
                return new
                {
                    ok = true,
                    newRarity = newRarity.ToId(),
                    newSlots = FusionRoller.SlotsFor(newRarity),
                    cost = ProjectCost(FusionCostTable.Promotion(newRarity), baseSpec.Profile.ElementPrimary)
                };
            }

            case FusionModes.Recipe:
            {
                if (body.Sacrifices is not { Count: 2 }
                    || !roster.TryGetValue(body.Sacrifices[0], out var a)
                    || !roster.TryGetValue(body.Sacrifices[1], out var b))
                    return new { ok = false, reason = "sacrifice.invalid" };
                if (string.Equals(body.Sacrifices[0], body.Sacrifices[1], StringComparison.Ordinal))
                    return new { ok = false, reason = "sacrifice.duplicate" }; // execute would refuse too
                var recipe = DemonRecipeCatalog.TryMatch(a.Profile.SpeciesId, b.Profile.SpeciesId);
                if (recipe is null)
                    return new { ok = false, reason = "recipe.unknown" };
                var output = DemonSpeciesCatalog.Get(recipe.OutputSpeciesId);
                var isDiscovered = store.ListFusionDiscoveries(playerId)
                    .Contains(recipe.RecipeId, StringComparer.Ordinal);
                var pickable = a.Profile.TraitIds.Concat(b.Profile.TraitIds)
                    .Distinct(StringComparer.Ordinal).ToList();
                return new
                {
                    ok = true,
                    discovered = isDiscovered,
                    // Undiscovered outputs stay silhouetted even in preview — band + cost only.
                    resultSpeciesId = isDiscovered ? recipe.OutputSpeciesId : null,
                    resultRarity = output.BaseRarity.ToId(),
                    pickableTraits = pickable,
                    cost = ProjectCost(FusionCostTable.Recipe(output.BaseRarity), output)
                };
            }

            default:
                return new { ok = false, reason = "mode.unknown" };
        }
    }

    /// <summary>
    /// DECIDED: the essence line is priced from the (possibly hidden) output's element even for
    /// undiscovered recipes. Yes, element + rarity band can narrow a silhouette — that is the
    /// deliberate breadcrumb: without it a player cannot stock the right essence to afford the
    /// experiment, and the discovery loop would be blind trial-and-refusal.
    /// </summary>
    static object ProjectCost(FusionCost cost, DemonSpeciesDef outputSpecies) =>
        ProjectCost(cost, outputSpecies.ElementPrimary.ToElementId());

    static object ProjectCost(FusionCost cost, string elementId) => new
    {
        souls = cost.Souls,
        shardMaterialId = "shard." + cost.ShardRarity.ToId(),
        shardCount = cost.ShardCount,
        essenceMaterialId = "essence." + elementId,
        essenceCount = cost.EssenceCount
    };

    static FusionRequest ToRequest(FusionHttpRequest body) => new(
        body.Mode ?? "",
        body.BaseInstanceId,
        body.Sacrifices ?? new List<string>(),
        body.PickedTraitId);

    public sealed class FusionHttpRequest
    {
        public long? PlayerId { get; set; }
        public string? Mode { get; set; }
        public string? BaseInstanceId { get; set; }
        public List<string>? Sacrifices { get; set; }
        public string? PickedTraitId { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>SIM-only fixtures — deterministic species mints and material grants for tests.</summary>
    public static void MapFusionTest(this RouteGroupBuilder test)
    {
        test.MapPost("/seed-materials", (RpgStore store, long? playerId, string? materialId, long? qty) =>
        {
            var pid = playerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (!DemonMaterialCatalog.IsKnown(materialId))
                return Results.BadRequest(new { reason = "material.unknown" });
            store.AddDemonMaterials(pid, new[] { (materialId!, Math.Clamp(qty ?? 1, 1, 10_000)) });
            return Results.Ok(new { items = store.ListDemonMaterials(pid) });
        });

        test.MapPost("/mint-demon", (RpgStore store, long? playerId, string? speciesId) =>
        {
            var pid = playerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (!DemonSpeciesCatalog.IsKnown(speciesId))
                return Results.BadRequest(new { reason = "species.unknown" });
            var species = DemonSpeciesCatalog.Get(speciesId!);
            var (specimen, _) = store.MintDemon(pid, new DemonMintSpec
            {
                SpeciesId = species.SpeciesId,
                Side = species.Side,
                GameTypeId = species.GameTypeId,
                Rarity = species.BaseRarity.ToId(),
                Variant = "normal",
                ElementPrimary = species.ElementPrimary.ToElementId(),
                ElementSecondary = species.ElementSecondary?.ToElementId(),
                TraitIds = species.TraitPool.Count > 0
                    ? new List<string> { species.TraitPool[0] }
                    : new List<string>(),
                Origin = "summon"
            });
            return Results.Ok(specimen);
        });
    }
}
