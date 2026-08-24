using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;

namespace FusionRpg.Injector.Stats;

/// <summary>
/// Injector-side Θ index — replaces <c>InjectorProgressionPowerProvider</c> (T1.4). Wraps
/// <see cref="HydratedPowerIndexProvider"/> rather than duplicating it: hydration mechanics belong to
/// Core (one implementation to keep correct); this class exists so an Injector-specific hydration
/// source (a future SignalR handler, a Harmony hook) has an Injector-namespaced home to attach to
/// without Core ever needing to know about it.
///
/// <para>No such source exists yet — like its predecessor (whose <c>SetLevel</c> had zero callers),
/// this stays un-hydrated in production today. <see cref="ActorIndex"/> therefore returns 0 for
/// every context, matching the old provider's <c>GetLevel</c>-always-0 behaviour exactly.</para>
/// </summary>
public sealed class InjectorPowerIndexProvider : IPowerIndexProvider
{
    readonly HydratedPowerIndexProvider _inner;

    public InjectorPowerIndexProvider(PowerTuning tuning) => _inner = new HydratedPowerIndexProvider(tuning);

    /// <summary>The one hydration mechanism today — a future push source calls this per actor.</summary>
    public void Hydrate(StatContext ctx, ActorLadderSnapshot snapshot) => _inner.Hydrate(ctx, snapshot);

    public void Clear() => _inner.Clear();

    public int ActorIndex(StatContext ctx) => _inner.ActorIndex(ctx);
    public int ContentIndex(ContentContext ctx) => _inner.ContentIndex(ctx);
    public PowerAxisReport Explain(StatContext ctx) => _inner.Explain(ctx);
}
