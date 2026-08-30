using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// aura-skill T18b: the derived-channel-with-contributions server endpoint `spec-aura-surface.md` §3
/// names as missing — `ActorChannelDetail.contributions` exists in the web contract with no server
/// producer (`adapt.ts:37` is unconditionally pending). Built directly on T18a's
/// <see cref="ActorHub.ResolveDerivedWithContributions"/>, for a LAWN actor (a `UniqueActor`
/// instance) — battle has its own, separate resolve path and never calls this.
///
/// <para><b>Do not bridge to <c>pvz_stat_contributions</c></b> (`RpgStore.cs:300-313`,
/// `/api/pvz-stats/{playerId}/channels/{channel}`). That table is keyed by `player_id` with no actor
/// column and is a rebuilt-on-every-mutate cache — a different row shape entirely (the spec's own
/// words: "two different things that share a word"). This endpoint resolves LIVE subsystem
/// contributions for one actor instance, the same way T22's `PatronEndpoints.Compute` resolves a
/// live `Θ` rather than reading a cache.</para>
///
/// <para>One `ActorHub` per request — nothing registers it in DI (confirmed by direct read of
/// `Program.cs`'s `AddSingleton` block), matching every other per-request Core object this Server
/// project already constructs on demand (e.g. `PowerLadder` in `WebMatchService.AptitudeChannelMods`).
/// The aptitude allocation delegate mirrors that same method's `store.LoadAllocation(
/// AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId))` exactly — no second allocation
/// read implementation.</para>
/// </summary>
public static class AuraDerivedEndpoints
{
    public static void MapAuraDerived(this WebApplication app)
    {
        var g = app.MapGroup("/api/actors");

        g.MapGet("/{instanceId}/derived", (string instanceId, RpgStore store) =>
        {
            var actor = store.GetUniqueActor(instanceId);
            if (actor == null) return Results.NotFound();

            var level = (int)Math.Max(1, actor.Level);
            var baseline = new EntityBaseline
            {
                Hp = FusionRpg.Core.Battle.BattleRuleset.BaseHp(level),
                MaxHp = FusionRpg.Core.Battle.BattleRuleset.BaseHp(level),
                Atk = FusionRpg.Core.Battle.BattleRuleset.BaseAtk(level)
            };

            var factory = new StatContextFactory();
            var ctx = string.Equals(actor.Side, "zombie", StringComparison.OrdinalIgnoreCase)
                ? factory.ForZombie(actor.InstanceId, baseline, actor.TypeId, playerId: actor.PlayerId)
                : factory.ForPlant(actor.InstanceId, baseline, actor.TypeId, playerId: actor.PlayerId);

            var powerIndex = new Power.ServerPowerIndexProvider(
                store, FusionRpg.Core.Power.PowerTuningHub.Tuning);
            var hub = ActorHubBootstrap.CreateDefault(
                powerIndex: powerIndex,
                aptitudeTuning: AptitudeTuningHub.Tuning,
                aptitudeAllocation: c => c.PlayerId is { } pid
                    ? store.LoadAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(pid))
                    : AptitudeAllocation.Empty);

            var (snapshot, contributions) = hub.ResolveDerivedWithContributions(ctx);

            var channels = snapshot.Channels
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new
                {
                    channelId = kv.Key,
                    value = kv.Value,
                    contributions = contributions.ContributionsFor(kv.Key)
                        .Select(c => new { sourceId = c.SourceId, op = c.Op.ToString(), value = c.Value })
                        .ToList()
                })
                .ToList();

            return Results.Ok(new { instanceId = actor.InstanceId, channels });
        });
    }
}
