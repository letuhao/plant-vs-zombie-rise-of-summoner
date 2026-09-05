using FusionRpg.Core.Demons;
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

            // species-build `battle-allocation` (module 10, path 4 of its own "four read paths"
            // table): this endpoint used to hard-code AllocationScope.Commander alone, so it could
            // report channel values the lawn does not actually apply -- "a diagnostic that confidently
            // lies." `ctx` already carries the same Side/TypeId the lawn path resolves species from
            // (no new plumbing) -- routed through the SAME SpeciesAllocationSource `allocation-
            // transport` (module 6) uses, not a second, ad-hoc merge.
            var speciesSource = new SpeciesAllocationSource(
                resolveSpeciesId: (side, typeId) => DemonSpeciesCatalog.IsConfigured
                    ? new LawnElementIndex(DemonSpeciesCatalog.All).TryGet(side.ToString().ToLowerInvariant(), typeId, out var def)
                        ? SpeciesLookupResult.Hit(def.SpeciesId)
                        : SpeciesLookupResult.NoSpecies
                    : SpeciesLookupResult.NotConfigured,
                resolveSpeciesAllocation: speciesId =>
                    store.EffectiveSpeciesAllocation(actor.PlayerId, speciesId, AptitudeTuningHub.Tuning),
                resolveCommanderAllocation: pid => pid is { } p
                    ? store.LoadAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(p))
                    : AptitudeAllocation.Empty,
                reportUnconfigured: msg => Console.Error.WriteLine($"[aura-derived] {msg}"));

            var hub = ActorHubBootstrap.CreateDefault(
                powerIndex: powerIndex,
                aptitudeTuning: AptitudeTuningHub.Tuning,
                aptitudeAllocation: speciesSource.Resolve);

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
