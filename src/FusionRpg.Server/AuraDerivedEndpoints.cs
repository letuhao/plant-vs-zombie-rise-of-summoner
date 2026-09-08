using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// Actor-scoped derived channels with FULL Hub fan-in + GG-49 contributions.
/// Sheet projection: <c>GET /api/actors/{id}/sheet</c>.
/// Do not bridge to <c>pvz_stat_contributions</c>.
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

            var (hub, ctx) = UniqueActorHubCompose.Build(store, actor);
            var (snapshot, contributions) = hub.ResolveDerivedWithContributions(ctx);
            var registry = hub.Composer.Registry;

            var channels = snapshot.Channels
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv =>
                {
                    registry.TryResolveChannel(kv.Key, out var def);
                    return new
                    {
                        channelId = kv.Key,
                        value = kv.Value,
                        composeKind = def?.Compose.ToString() ?? "",
                        contributions = contributions.ContributionsFor(kv.Key)
                            .Select(c => new
                            {
                                sourceId = c.SourceId,
                                label = ContributionSourceIds.FictionLabel(c.SourceId),
                                op = c.Op.ToString(),
                                value = c.Value
                            })
                            .ToList()
                    };
                })
                .ToList();

            return Results.Ok(new { instanceId = actor.InstanceId, channels });
        });

        g.MapGet("/{instanceId}/sheet", (string instanceId, RpgStore store) =>
        {
            var actor = store.GetUniqueActor(instanceId);
            if (actor == null) return Results.NotFound();
            return Results.Ok(UniqueActorHubCompose.ProjectSheet(store, actor));
        });
    }
}
