using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>Demon reads + specimen edits (spec-demon-core.md). Summoning arrives with its own module.</summary>
public static class DemonEndpoints
{
    public static void MapDemons(this WebApplication app)
    {
        var g = app.MapGroup("/api/demons");

        // Species + trait catalogs (code-authored/generated — same payload for every player).
        g.MapGet("/catalog", () => Results.Ok(new
        {
            species = DemonSpeciesCatalog.All.Select(s => new
            {
                speciesId = s.SpeciesId,
                name = s.Name,
                side = s.Side,
                gameTypeId = s.GameTypeId,
                demonTypeId = s.DemonTypeId,
                elementPrimary = s.ElementPrimary.ToElementId(),
                elementSecondary = s.ElementSecondary?.ToElementId(),
                rarity = s.BaseRarity.ToId(),
                deployMode = s.DeployMode == DemonDeployMode.HypnoAlly ? "hypno-ally" : "plant-avatar",
                summonable = s.Acquisition.HasFlag(DemonAcquisition.Summonable),
                captureOnly = s.Acquisition == DemonAcquisition.CaptureOnly,
                variants = s.Variants,
                traitPool = s.TraitPool
            }),
            traits = DemonTraitCatalog.All.Select(t => new
            {
                traitId = t.TraitId,
                name = t.Name,
                blurb = t.Blurb
            })
        }));

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(store.ListDemonRoster(playerId));
        });

        g.MapGet("/{playerId:long}/codex", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(store.ListDemonCodex(playerId));
        });

        // Summon state: banners, costs, today's focus, and the player's visible pity counters.
        g.MapGet("/{playerId:long}/summon-state", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            var pity = store.GetSummonPity(playerId);
            var focus = SummonBannerCatalog.FocusFor(DateOnly.FromDateTime(DateTime.UtcNow));
            return Results.Ok(new
            {
                banners = SummonBannerCatalog.All.Select(b => new
                {
                    bannerId = b.BannerId,
                    costPerPull = b.CostPerPull,
                    costPerTen = b.CostPerTen,
                    focusElement = b.HasElementFocus ? focus.ToElementId() : null
                }),
                pity = new
                {
                    pullsSinceHeirloom = pity.PullsSinceHeirloom,
                    pullsSinceSunwoven = pity.PullsSinceSunwoven,
                    heirloomGuaranteeAt = SummonRoller.HeirloomHardPity,
                    sunwovenGuaranteeAt = SummonRoller.SunwovenHardPity
                },
                balance = store.GetSoulBalance(playerId)
            });
        });

        g.MapPost("/summon", async (SummonRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.CorrelationId))
                return Results.BadRequest(new { reason = "correlation.missing" });
            if (body.CorrelationId.Trim().Length > 64) // a UUID is 36; dedupe rows are permanent
                return Results.BadRequest(new { reason = "correlation.toolong" });

            // Server-authoritative: focus resolved from today's rotation, seed minted here and recorded.
            var focus = SummonBannerCatalog.FocusFor(DateOnly.FromDateTime(DateTime.UtcNow)).ToElementId();
            var seed = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
            var (ok, reason, outcome) = store.ExecuteSummon(
                pid, body.BannerId ?? SummonBannerCatalog.StandardRift, body.Count, body.CorrelationId!, seed, focus);
            if (!ok)
            {
                return reason is "souls.insufficient"
                    ? Results.Conflict(new { reason, balance = outcome?.Balance })
                    : Results.BadRequest(new { reason });
            }

            await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("SoulsUpdated", new { playerId = pid });
            await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("DemonsUpdated", new { playerId = pid });
            return Results.Ok(new
            {
                replayed = outcome!.Replayed,
                specimens = outcome.Specimens,
                pity = new { pullsSinceHeirloom = outcome.Pity.PullsSinceHeirloom, pullsSinceSunwoven = outcome.Pity.PullsSinceSunwoven },
                balance = outcome.Balance,
                discoverySouls = outcome.DiscoverySouls
            });
        });

        g.MapPost("/specimen/{instanceId}/nickname", async (
            string instanceId, NicknameRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var nickname = body.Nickname?.Trim();
            if (nickname is { Length: > 32 })
                return Results.BadRequest(new { reason = "nickname.toolong" });
            var (ok, profile) = store.SetDemonNickname(instanceId, nickname);
            if (!ok) return Results.NotFound();
            await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("DemonsUpdated", new { instanceId });
            return Results.Ok(profile);
        });

        g.MapPost("/specimen/{instanceId}/lock", async (
            string instanceId, LockRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
        {
            var (ok, profile) = store.SetDemonLocked(instanceId, body.Locked);
            if (!ok) return Results.NotFound();
            await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("DemonsUpdated", new { instanceId });
            return Results.Ok(profile);
        });
    }

    public sealed class SummonRequest
    {
        public long? PlayerId { get; set; }
        public string? BannerId { get; set; }
        public int Count { get; set; } = 1;
        public string? CorrelationId { get; set; }
    }

    public sealed class NicknameRequest
    {
        public string? Nickname { get; set; }
    }

    public sealed class LockRequest
    {
        public bool Locked { get; set; }
    }
}
