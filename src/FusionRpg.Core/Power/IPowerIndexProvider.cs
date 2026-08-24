using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Power;

/// <summary>
/// Produces Θ — the single integer every other system reads instead of a raw level
/// (spec-power-index.md §2.2). Structurally replaces the old <c>IProgressionPowerProvider</c>
/// (deleted, T1.4 — zero <c>SetLevel</c> callers). Its one real consumer,
/// <see cref="FusionRpg.Core.Stats.Derived.Subsystems.RpgProgressionSubsystem"/>, is not rewired onto
/// this interface yet — that semantic migration (Θ into the ProgressionPower channel) is
/// power-plan.md T3.2, deliberately gated behind Checkpoint 2.
/// </summary>
public interface IPowerIndexProvider
{
    int ActorIndex(StatContext ctx);
    int ContentIndex(ContentContext ctx);
    PowerAxisReport Explain(StatContext ctx);
}

/// <summary>The identity: P(0) = C. No tuning dependency — there is nothing to compose.</summary>
public sealed class StubPowerIndexProvider : IPowerIndexProvider
{
    public int ActorIndex(StatContext ctx) => 0;
    public int ContentIndex(ContentContext ctx) => 0;
    public PowerAxisReport Explain(StatContext ctx) => new(0, Array.Empty<PowerAxisContribution>());
}

/// <summary>
/// Reads an injected snapshot; no I/O (spec-power-index.md §2.2). <see cref="Hydrate"/>/<see cref="Clear"/>
/// are the one mechanism Core owns — caching strategy, refresh cadence, and invalidation are each
/// host's own policy (§2.5), built on top of this, not inside it. Mirrors
/// <c>InjectorProgressionPowerProvider</c>'s existing identity-keyed dictionary shape so a host
/// migrating off the old interface recognises the pattern.
/// </summary>
public sealed class HydratedPowerIndexProvider : IPowerIndexProvider
{
    readonly PowerTuning _tuning;
    readonly Dictionary<string, ActorLadderSnapshot> _actors = new(StringComparer.OrdinalIgnoreCase);

    public HydratedPowerIndexProvider(PowerTuning tuning)
    {
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        PowerIndexComposer.ValidateWeights(_tuning.Weights);
    }

    public void Hydrate(StatContext ctx, ActorLadderSnapshot snapshot) => _actors[Key(ctx)] = snapshot;

    public void Clear() => _actors.Clear();

    public int ActorIndex(StatContext ctx) => Explain(ctx).Total;

    public int ContentIndex(ContentContext ctx) => PowerIndexComposer.ContentExplain(_tuning, ctx).Total;

    public PowerAxisReport Explain(StatContext ctx)
    {
        var snapshot = _actors.TryGetValue(Key(ctx), out var s) ? s : ActorLadderSnapshot.Empty;
        return PowerIndexComposer.ActorExplain(_tuning, snapshot);
    }

    public static string Key(StatContext ctx) => (ctx.PlayerId ?? 0) + ":" + ctx.Side + ":" + ctx.TypeId;
}
