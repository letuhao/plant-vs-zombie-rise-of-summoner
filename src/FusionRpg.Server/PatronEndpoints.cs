using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Patron;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// Patron designation (spec-patron-demon.md): server-authoritative aura computed from the
/// specimen's rarity/star/level; changes broadcast to the web AND pushed to the injector as a
/// `patron.aura` command (applied from the NEXT match — the plugin freezes the running one).
/// </summary>
public static class PatronEndpoints
{
    public static void MapPatron(this WebApplication app)
    {
        var g = app.MapGroup("/api/patron");

        g.MapGet("/{playerId:long}", (long playerId, RpgStore store) =>
        {
            if (!store.PlayerExists(playerId)) return Results.NotFound();
            return Results.Ok(ProjectState(store, playerId));
        });

        g.MapPost("/set", async (SetPatronRequest body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.CorrelationId))
                return Results.BadRequest(new { reason = "correlation.missing" });
            if (body.CorrelationId.Trim().Length > 64)
                return Results.BadRequest(new { reason = "correlation.toolong" });

            var (ok, reason, _) = store.SetPatron(pid, body.InstanceId ?? "", body.CorrelationId!);
            if (!ok)
            {
                return reason is "souls.insufficient"
                    ? Results.Conflict(new { reason })
                    : Results.BadRequest(new { reason });
            }

            RefreshRuntimeState(store);
            try
            {
                await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("PatronUpdated", new { playerId = pid });
                await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("SoulsUpdated", new { playerId = pid });
                var cmd = TryBuildPatronCommand(store);
                if (cmd != null)
                {
                    inbox.Enqueue(cmd); // poll fallback — the push may race a reconnect
                    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("Command", cmd);
                }
            }
            catch
            {
                // best-effort; the designation is durable and the injector inbox poll recovers
            }

            return Results.Ok(ProjectState(store, pid));
        });
    }

    /// <summary>The injector-facing aura command for the CURRENT player's patron — also pushed
    /// on injector Hello so a fresh inject/reconnect always has the latest designation.</summary>
    public static CommandDto? TryBuildPatronCommand(RpgStore store)
    {
        var playerId = store.GetCurrentPlayerId();
        var computed = Compute(store, playerId);
        if (computed == null) return null;
        var aura = computed.Value.Aura;
        return new CommandDto
        {
            Name = "patron.aura",
            Payload = new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["elementPrimary"] = aura.ElementPrimary,
                ["elementSecondary"] = aura.ElementSecondary,
                ["powerMilli"] = aura.PowerMilli,
                ["defenseMilli"] = aura.DefenseMilli,
                ["secondaryPowerMilli"] = aura.SecondaryPowerMilli,
                ["secondaryDefenseMilli"] = aura.SecondaryDefenseMilli
            }
        };
    }

    /// <summary>The session-grant form of the aura marker — upserted into the effect-grant
    /// session at each pvzrh board.start so reconnect rehydrate carries it (same grant id the
    /// injector plugin uses; upserts converge).</summary>
    public static EffectGrantDto? TryBuildPatronSessionGrant(RpgStore store)
    {
        var playerId = store.GetCurrentPlayerId();
        var computed = Compute(store, playerId);
        if (computed == null) return null;
        return new EffectGrantDto
        {
            GrantId = "patron:aura",
            EffectId = "fx.patron_aura",
            OwnerKey = "match",
            PluginId = "sec.patron.aura"
        };
    }

    /// <summary>Keeps the in-process runtime state (SIM plugins share it) aligned with the store.</summary>
    public static void RefreshRuntimeState(RpgStore store)
    {
        var playerId = store.GetCurrentPlayerId();
        var computed = Compute(store, playerId);
        PatronRuntimeState.Set(playerId, computed?.Aura);
    }

    static (PatronRow Row, PatronAura Aura)? Compute(RpgStore store, long playerId)
    {
        var row = store.GetPatron(playerId);
        if (row == null) return null;
        var profile = store.GetDemonProfile(row.InstanceId);
        var actor = profile == null ? null : store.ListDemonRoster(playerId).Items
            .FirstOrDefault(s => s.Profile.InstanceId == row.InstanceId)?.Actor;
        if (profile == null) return null;
        if (!DemonRarityIds.TryParse(profile.Rarity, out var rarity)) return null;

        // aura-skill T22 (owner sign-off 2026-08-30): the player's own Θ, read the SAME way
        // AptitudeEndpoints.cs's own ProjectState does — no DI thread needed through this class's 4
        // external callers (Program.cs, RpgHub.cs, EventIngest.cs, SimEndpoints.cs), since
        // ServerPowerIndexProvider wraps only `store` + the already-globally-configured
        // PowerTuningHub.Tuning, both already in scope here.
        var powerIndex = new FusionRpg.Server.Power.ServerPowerIndexProvider(
            store, FusionRpg.Core.Power.PowerTuningHub.Tuning);
        var theta = powerIndex.ActorIndex(new FusionRpg.Core.Stats.StatContext { PlayerId = playerId });

        var aura = PatronPolicy.Aura(
            rarity, profile.Star, actor?.Level ?? 1, theta, FusionRpg.Core.Power.PowerTuningHub.Tuning,
            profile.ElementPrimary, profile.ElementSecondary);
        return (row, aura);
    }

    static object ProjectState(RpgStore store, long playerId)
    {
        var computed = Compute(store, playerId);
        return new
        {
            patron = computed == null
                ? null
                : new
                {
                    instanceId = computed.Value.Row.InstanceId,
                    setUtc = computed.Value.Row.SetUtc,
                    revision = computed.Value.Row.Revision,
                    aura = computed.Value.Aura,
                    switchCostSouls = PatronPolicy.SwitchCostSouls
                },
            switchCostSouls = PatronPolicy.SwitchCostSouls
        };
    }

    public sealed class SetPatronRequest
    {
        public long? PlayerId { get; set; }
        public string? InstanceId { get; set; }
        public string? CorrelationId { get; set; }
    }
}
