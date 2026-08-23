using FusionRpg.Contracts;
using FusionRpg.Core.Effects;

namespace FusionRpg.Core.Battle;

/// <summary>HP surface the battle sink mutates — implemented by engine actor state (and test fakes).</summary>
public interface IBattleHpTarget
{
    long Hp { get; set; }
    long MaxHp { get; }
}

/// <summary>One FA10 window result: the clamped delta actually applied to an actor.</summary>
public sealed record BattleAppliedHpDelta(string ActorKey, long Amount, int MergedCount);

/// <summary>
/// Battle-local effect stack (spec-match-source-core.md): a private EffectBag + EffectFunnel whose
/// FA10 sink mutates engine-owned battle state instead of Unity. Merge semantics, |amount| caps and
/// mailbox caps are the shipped Funnel's — bypassing it would fork combat numbers between modes.
/// Battle state is memory; the Unity writer paths stay PvZ-only.
/// </summary>
public sealed class BattleEffectHost
{
    readonly EffectFunnel _funnel;
    readonly BattleEffectSink _sink;

    public BattleEffectHost(Func<string, IBattleHpTarget?> resolveActor, ulong rngSeed)
    {
        _sink = new BattleEffectSink(resolveActor);
        Clock = new FakeEffectClock();
        var catalog = new InMemoryEffectCatalog();
        catalog.ReplaceAll(EffectAtomCatalog.CreateAll());
        Bag = new EffectBag(catalog, new InMemoryEffectGrantStore(),
            new EffectProcPolicy(Clock, new BattleEffectRandom(rngSeed)), _sink);
        Bag.UtcNow = () => Clock.UtcNow;
        _funnel = new EffectFunnel(Bag);
    }

    public EffectBag Bag { get; }
    public FakeEffectClock Clock { get; }
    public EffectFunnel Funnel => _funnel;

    /// <summary>Deltas actually applied in the last flush window (clamped to [0, MaxHp]).</summary>
    public IReadOnlyList<BattleAppliedHpDelta> LastApplied => _sink.Applied;

    public bool QueueHpDelta(string actorKey, long amount, string? effectId = null, string? grantId = null) =>
        _funnel.EnqueueMutation(actorKey, amount,
            pluginId: "battle",
            effectId: string.IsNullOrWhiteSpace(effectId) ? "battle.hp_delta" : effectId,
            grantId: grantId);

    public void Flush()
    {
        _sink.Applied.Clear();
        _funnel.Flush();
        _funnel.AcknowledgeWindow();
    }

    sealed class BattleEffectSink : IEffectActionSink
    {
        readonly Func<string, IBattleHpTarget?> _resolve;

        public BattleEffectSink(Func<string, IBattleHpTarget?> resolve) => _resolve = resolve;

        public List<BattleAppliedHpDelta> Applied { get; } = new();

        public bool Execute(EffectExecuteContext ctx, EffectActionPlanItem item)
        {
            if (!string.Equals(item.Action, EffectActions.ApplyResourceDelta, StringComparison.OrdinalIgnoreCase))
                return true; // battle mode consumes FA10 only; other actions are inert here

            var ptr = item.Params.TryGetValue("targetPtr", out var p) ? p as string : null;
            if (string.IsNullOrWhiteSpace(ptr))
                return true;
            var target = _resolve(ptr!);
            if (target == null)
                return true;

            var amount = item.Params.TryGetValue("amount", out var a) ? Convert.ToInt64(a) : 0L;
            var mergedCount = item.Params.TryGetValue("mergedCount", out var m) ? Convert.ToInt32(m) : 1;

            var before = target.Hp;
            var after = (int)Math.Min(target.MaxHp, Math.Max(0L, before + amount));
            target.Hp = after;
            Applied.Add(new BattleAppliedHpDelta(ptr!, after - before, mergedCount));
            return true;
        }
    }

    /// <summary>Owned-PRNG adapter for EffectProcPolicy — never System.Random on a replayable path.</summary>
    sealed class BattleEffectRandom : IEffectRandom
    {
        readonly SeededRng _rng;
        public BattleEffectRandom(ulong seed) => _rng = SeededRng.DeriveStream(seed, "proc");
        public double NextDouble() => (_rng.NextULong() >> 11) * (1.0 / (1UL << 53));
    }
}
