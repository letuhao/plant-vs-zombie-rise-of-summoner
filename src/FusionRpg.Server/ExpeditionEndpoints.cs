using FusionRpg.Contracts;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Expeditions;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// Expedition loop (spec-expeditions.md): dispatch seals squad + seed; collect lazily resolves
/// the timeline, runs each planned battle through WebMatchService (correlation `exp:{id}:{n}`),
/// then applies event Souls / specimen XP / wild-join mints / materials in ONE state-gated
/// transaction — collect is exactly-once even across crashes.
/// </summary>
public sealed class ExpeditionService
{
    readonly RpgStore _store;
    readonly WebMatchService _webMatch;
    readonly IHubContext<RpgHub> _hub;

    public ExpeditionService(RpgStore store, WebMatchService webMatch, IHubContext<RpgHub> hub)
    {
        _store = store;
        _webMatch = webMatch;
        _hub = hub;
    }

    public async Task<(bool Ok, string Reason, ExpeditionRow? Row)> DispatchAsync(
        long playerId, string correlationId, string tierId, IReadOnlyList<string> squad)
    {
        var seed = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
        var (ok, reason, row) = _store.DispatchExpedition(playerId, correlationId, tierId, squad, seed);

        // Replay must match the stored request (house pattern, cf. summon pulls): a reused
        // correlation with a different tier/squad silently returning the old expedition would
        // let the client believe its new squad went out.
        if (ok && reason == "replay"
            && (row!.TierId != tierId || !row.SquadInstanceIds.SequenceEqual(squad, StringComparer.Ordinal)))
            return (false, "correlation.mismatch", null);

        if (ok && reason != "replay")
        {
            try
            {
                await _hub.Clients.Group(RpgConstants.WebGroup)
                    .SendAsync("DemonsUpdated", new { playerId }).ConfigureAwait(false);
            }
            catch
            {
                // best-effort; the dispatch is durable and a replayed request recovers the row
            }
        }

        return (ok, reason, row);
    }

    public sealed record CollectBattleResult(int BattleIndex, bool Boss, string Outcome, long? RunId, string MatchKey);

    public sealed record CollectResult(
        string State, int ElapsedTicks,
        IReadOnlyList<ExpeditionTickOutcome> Ticks,
        IReadOnlyList<CollectBattleResult> Battles,
        long SoulsAwarded,
        IReadOnlyList<MaterialDrop> Materials,
        IReadOnlyList<DemonSpecimenDto> WildJoins,
        IReadOnlyList<(string InstanceId, double Xp)> SpecimenXp);

    public async Task<(bool Ok, string Reason, CollectResult? Result)> CollectAsync(
        long expeditionId, long playerId, bool recall)
    {
        var row = _store.TryGetExpedition(expeditionId);
        if (row is null || row.PlayerId != playerId) return (false, "expedition.notfound", null);
        if (row.State != ExpeditionStates.Dispatched)
            return (false, "expedition." + row.State.ToLowerInvariant(), null);

        var tier = ExpeditionTierCatalog.Get(row.TierId);
        var now = DateTimeOffset.UtcNow;
        var due = DateTimeOffset.Parse(row.DueUtc);
        var dispatched = DateTimeOffset.Parse(row.DispatchedUtc);
        if (!recall && now < due) return (false, "expedition.notdue", null);
        var squadIds = row.SquadInstanceIds; // computed property re-parses JSON — read once

        // Recall pro-rates at tick boundaries; a due collect resolves the full timeline. A retry
        // after a crashed attempt must never resolve FEWER ticks than battles already ingested
        // (backwards clock skew would strand committed tail battles outside the closed state —
        // 2026-08-21 review I6), so elapsed is floored at the furthest logged battle's tick.
        var elapsed = now >= due
            ? tier.TickCount
            : Math.Clamp((int)((now - dispatched).TotalMinutes / tier.TickMinutes), 0, tier.TickCount);
        elapsed = Math.Max(elapsed, LoggedBattleTickFloor(row, tier, playerId));

        // The stored squad snapshot rule: BuildSquad reads the LIVE roster, which only works
        // because the soft-lock freezes deployment while dispatched; specimen XP from this very
        // collect lands after resolution. Already-logged battles replay their sealed setups.
        var (squadOk, squadReason, squad, _) = _webMatch.BuildSquad(playerId, squadIds);
        if (!squadOk) return (false, squadReason, null);

        var resolution = ExpeditionResolver.Resolve(tier.TierId, squad!, row.Seed, elapsed);

        // Battles run first — each is correlation-idempotent, so a crashed collect replays them.
        var battleResults = new List<CollectBattleResult>();
        var specimenXp = new Dictionary<string, double>(StringComparer.Ordinal);
        var victories = 0;
        foreach (var plan in resolution.Battles)
        {
            var (ok, reason, outcome) = await _webMatch
                .RunPlannedMatchAsync(playerId, BattleCorrelation(row.Id, plan.BattleIndex),
                    BattleMatchKey(row.Id, plan.BattleIndex), plan.Setup, plan.BattleSeed)
                .ConfigureAwait(false);
            if (!ok) return (false, "battle." + reason, null);

            var report = outcome!.Report;
            battleResults.Add(new CollectBattleResult(
                plan.BattleIndex, plan.Boss,
                report.Outcome.ToString().ToLowerInvariant(), outcome.RunId, outcome.MatchKey));

            // Specimen XP per battle won: survivors earn the tier rate × their genius multiplier.
            if (report.Outcome == BattleOutcome.Victory)
            {
                victories++;
                foreach (var actor in report.Actors)
                {
                    if (actor.Side != "squad" || !actor.Survived) continue;
                    var slot = SlotIndex(actor.Key);
                    if (slot < 0 || slot >= squadIds.Count) continue;
                    var instanceId = squadIds[slot];
                    specimenXp.TryGetValue(instanceId, out var have);
                    specimenXp[instanceId] = have +
                        resolution.Rewards.SpecimenXpPerBattleWon * actor.XpMilli / 1000.0;
                }
            }
        }

        // The resolver's manifest is complete (greedy multiplier and wild-join traits included) —
        // this layer only maps it onto store writes.
        var wildMints = resolution.Rewards.WildJoins.Select(join =>
        {
            var species = DemonSpeciesCatalog.Get(join.SpeciesId);
            return new DemonMintSpec
            {
                SpeciesId = species.SpeciesId,
                Side = species.Side,
                GameTypeId = species.GameTypeId,
                Rarity = species.BaseRarity.ToId(),
                Variant = join.Variant,
                ElementPrimary = species.ElementPrimary.ToElementId(),
                ElementSecondary = species.ElementSecondary?.ToElementId(),
                TraitIds = join.TraitIds.ToList(),
                Origin = "expedition"
            };
        }).ToList();

        var state = recall && elapsed < tier.TickCount ? ExpeditionStates.Recalled : ExpeditionStates.Collected;
        var xpAwards = specimenXp.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => (kv.Key, kv.Value)).ToList();
        var (applied, applyReason, minted) = _store.ApplyExpeditionRewards(
            row.Id, playerId, state,
            new RpgStore.ExpeditionRewardApply(
                resolution.Rewards.EventSouls,
                resolution.Rewards.Materials.Select(m => (m.MaterialId, m.Qty)).ToList(),
                xpAwards,
                wildMints));
        if (!applied) return (false, applyReason, null);

        // Loyalty for the trip as a whole, not per battle: an expedition credits a win when most
        // of its battles were victories. A recall before the first battle is neither. Like the
        // web-match credit, this sits outside the exactly-once rewards envelope on purpose — a
        // retry after the rewards commit finds the expedition closed and returns before reaching
        // here, so the worst case is a lost ±15, never a double credit.
        if (battleResults.Count > 0)
            _store.ApplyContractResults(playerId, squadIds, victories * 2 >= battleResults.Count);

        // Rewards are committed: notifies are best-effort. A hub fault here must not fail the
        // call — the reveal payload is only deliverable once (state gate refuses retries).
        try
        {
            await _hub.Clients.Group(RpgConstants.WebGroup)
                .SendAsync("DemonsUpdated", new { playerId }).ConfigureAwait(false);
            await _hub.Clients.Group(RpgConstants.WebGroup)
                .SendAsync("SoulsUpdated", new { playerId }).ConfigureAwait(false);
        }
        catch
        {
            // best-effort; queries refresh on the next poll regardless
        }

        return (true, "", new CollectResult(
            state, elapsed, resolution.Ticks, battleResults, resolution.Rewards.EventSouls,
            resolution.Rewards.Materials,
            minted.ToList(),
            xpAwards));
    }

    static string BattleCorrelation(long expeditionId, int battleIndex) => $"exp:{expeditionId}:{battleIndex}";
    static string BattleMatchKey(long expeditionId, int battleIndex) => $"exp-{expeditionId}-{battleIndex}";

    /// <summary>Lowest elapsed-tick count consistent with battles already logged by a prior
    /// crashed attempt — at most one log lookup per possible battle (≤5).</summary>
    int LoggedBattleTickFloor(ExpeditionRow row, ExpeditionTierDef tier, long playerId)
    {
        var floor = 0;
        foreach (var (battleIndex, tickIndex, _) in ExpeditionResolver.BattleSchedule(tier))
        {
            if (_store.TryGetWebMatchLog(playerId, BattleCorrelation(row.Id, battleIndex)) != null)
                floor = Math.Max(floor, tickIndex);
        }

        return floor;
    }

    static int SlotIndex(string actorKey)
    {
        var sep = actorKey.LastIndexOf(':');
        return sep >= 0 && int.TryParse(actorKey[(sep + 1)..], out var i) ? i : -1;
    }
}

public static class ExpeditionEndpoints
{
    public static void MapExpeditions(this WebApplication app)
    {
        var g = app.MapGroup("/api/expeditions");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(new
            {
                serverUtc = DateTime.UtcNow.ToString("o"),
                tiers = ExpeditionTierCatalog.All,
                items = store.ListExpeditions(playerId).Select(Project)
            });
        });

        g.MapGet("/{playerId:long}/materials", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(new { items = store.ListDemonMaterials(playerId) });
        });

        g.MapPost("/dispatch", async (DispatchRequest body, ExpeditionService svc, RpgStore store) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.CorrelationId))
                return Results.BadRequest(new { reason = "correlation.missing" });
            if (body.CorrelationId.Trim().Length > 64)
                return Results.BadRequest(new { reason = "correlation.toolong" });

            var (ok, reason, row) = await svc.DispatchAsync(
                pid, body.CorrelationId!, body.TierId ?? "", body.Squad ?? new List<string>());
            if (!ok) return Results.BadRequest(new { reason });
            return Results.Ok(new { replayed = reason == "replay", expedition = Project(row!) });
        });

        g.MapPost("/{id:long}/collect", (long id, CollectRequest? body, ExpeditionService svc, RpgStore store) =>
            CollectOrRecallAsync(id, body, svc, store, recall: false));

        g.MapPost("/{id:long}/recall", (long id, CollectRequest? body, ExpeditionService svc, RpgStore store) =>
            CollectOrRecallAsync(id, body, svc, store, recall: true));
    }

    static async Task<IResult> CollectOrRecallAsync(
        long id, CollectRequest? body, ExpeditionService svc, RpgStore store, bool recall)
    {
        var pid = body?.PlayerId ?? store.GetCurrentPlayerId();
        if (!store.PlayerExists(pid)) return Results.NotFound();
        var (ok, reason, result) = await svc.CollectAsync(id, pid, recall);
        if (!ok)
        {
            return reason is "expedition.notfound" ? Results.NotFound()
                : reason is "expedition.notdue" or "expedition.collected" or "expedition.recalled" or "expedition.closed"
                    ? Results.Conflict(new { reason })
                    : Results.BadRequest(new { reason });
        }

        return Results.Ok(new
        {
            state = result!.State,
            elapsedTicks = result.ElapsedTicks,
            ticks = result.Ticks,
            battles = result.Battles,
            soulsAwarded = result.SoulsAwarded,
            materials = result.Materials,
            wildJoins = result.WildJoins,
            specimenXp = result.SpecimenXp.Select(x => new { instanceId = x.InstanceId, xp = x.Xp })
        });
    }

    /// <summary>Wire projection: the sealed seed never leaves the server before collect (the
    /// resolver is pure, so a leaked seed lets a client pre-read every outcome and re-roll via
    /// free recalls — 2026-08-21 review), and correlation/squad_json are server bookkeeping.
    /// Seeds are also ulong — raw serialization would corrupt above 2^53 in JS.</summary>
    static object Project(ExpeditionRow row) => new
    {
        id = row.Id,
        state = row.State,
        tierId = row.TierId,
        squadInstanceIds = row.SquadInstanceIds,
        dispatchedUtc = row.DispatchedUtc,
        dueUtc = row.DueUtc,
        collectedUtc = row.CollectedUtc
    };

    /// <summary>SIM-only timer rewind — mapped inside the /api/test group.</summary>
    public static void MapExpeditionTest(this RouteGroupBuilder test)
    {
        test.MapPost("/expedition-due", (ForceDueRequest body, RpgStore store) =>
        {
            if (body.ExpeditionId <= 0) return Results.BadRequest(new { reason = "id.missing" });
            return store.ForceExpeditionDue(body.ExpeditionId, DateTimeOffset.UtcNow.AddSeconds(-1))
                ? Results.Ok(new { ok = true })
                : Results.NotFound();
        });
    }

    public sealed class DispatchRequest
    {
        public long? PlayerId { get; set; }
        public string? CorrelationId { get; set; }
        public string? TierId { get; set; }
        public List<string>? Squad { get; set; }
    }

    public sealed class CollectRequest
    {
        public long? PlayerId { get; set; }
    }

    public sealed class ForceDueRequest
    {
        public long ExpeditionId { get; set; }
    }
}
