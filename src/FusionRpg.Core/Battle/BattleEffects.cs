using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Battle;

/// <summary>HP surface the battle sink mutates — implemented by engine actor state (and test fakes).</summary>
public interface IBattleHpTarget
{
    long Hp { get; set; }
    long MaxHp { get; }
}

/// <summary>
/// A18e (spec-battle-live-stat-modifiers.md §4): the narrow surface `BattleEffectSink`'s `ModifyStat`
/// branch needs — `ActorState` is private to `BattleEngine` (a different, unrelated top-level class
/// from this file), the same reason `IBattleHpTarget` above exists rather than naming `ActorState`
/// directly. `Derived` is already mutable (`ActorDerivedSnapshot.Set` is `internal`, reachable
/// anywhere in this assembly) — this interface only needs to expose the reference and the baseline
/// this module recomposes against.
/// </summary>
public interface IBattleStatTarget
{
    ActorDerivedSnapshot Derived { get; }
    long BaselineDefense { get; }
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
        // A18d (spec-battle-status-apply.md §1): Clock built BEFORE _sink now (swapped from the
        // original order) so BattleEffectSink can hold a live reference to it -- BattleRunState's own
        // constructor builds Host (and so this Host's own Clock) two lines before Status/StatusRng
        // even exist (BattleRunState.cs:115 vs 117/103), which is why those two are wired via the
        // settable properties below rather than threaded through this constructor at all. Purely
        // internal reordering: neither Clock nor _sink is read before both complete.
        Clock = new FakeEffectClock();
        _sink = new BattleEffectSink(resolveActor, Clock);
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

    /// <summary>A18d: forwards to <see cref="BattleEffectSink"/>'s own settable property, the same
    /// "wire after the dependency exists" shape T14 already used for <c>Bag.ShieldGate</c> and A18c
    /// for <c>Bag.Status</c>/<c>Bag.StatusRng</c> — this constructor's own signature stays
    /// unchanged, so neither existing call site needs to change.</summary>
    public StatusRuntime? Status { set => _sink.Status = value; }

    /// <summary>A18d: see <see cref="Status"/> above — the same shape, one property over.</summary>
    public IStatusRng? StatusRng { set => _sink.StatusRng = value; }

    /// <summary>A18e: the same forwarding shape, one more property — see <see cref="Status"/>'s own
    /// doc comment for why this cannot be a constructor parameter.</summary>
    public BattleStatModifierLedger? Ledger { set => _sink.Ledger = value; }

    /// <summary>A18e: <see cref="IBattleStatTarget"/>'s own resolver — `resolveActor` (the ctor
    /// parameter) is insufficient here, since `owner.Derived`/baseline Defense are not on
    /// <see cref="IBattleHpTarget"/>.</summary>
    public Func<string, IBattleStatTarget?>? ResolveStatTarget { set => _sink.ResolveStatTarget = value; }

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
        readonly FakeEffectClock _clock;

        public BattleEffectSink(Func<string, IBattleHpTarget?> resolve, FakeEffectClock clock)
        {
            _resolve = resolve;
            _clock = clock;
        }

        /// <summary>A18d: wired post-construction via <see cref="BattleEffectHost.Status"/> — see
        /// that property's own doc comment for why this cannot be a constructor parameter.</summary>
        public StatusRuntime? Status { get; set; }
        public IStatusRng? StatusRng { get; set; }

        /// <summary>A18e: wired post-construction via <see cref="BattleEffectHost.Ledger"/>/
        /// <see cref="BattleEffectHost.ResolveStatTarget"/> — same shape.</summary>
        public BattleStatModifierLedger? Ledger { get; set; }
        public Func<string, IBattleStatTarget?>? ResolveStatTarget { get; set; }

        public List<BattleAppliedHpDelta> Applied { get; } = new();

        public bool Execute(EffectExecuteContext ctx, EffectActionPlanItem item)
        {
            // A18d (spec-battle-status-apply.md §1): FA2, distinct from resource.delta's own
            // DoT/contagion piggyback (A18c, a different branch of FireGrant entirely -- this action
            // is a standalone plan item, not a byproduct of ApplyResourceDelta).
            if (string.Equals(item.Action, EffectActions.ApplyStatus, StringComparison.OrdinalIgnoreCase))
                return ExecApplyStatus(ctx, item);

            // A18e (spec-battle-live-stat-modifiers.md §4): FA1, a third standalone plan item action.
            if (string.Equals(item.Action, EffectActions.ModifyStat, StringComparison.OrdinalIgnoreCase))
                return ExecModifyStat(ctx, item);

            if (!string.Equals(item.Action, EffectActions.ApplyResourceDelta, StringComparison.OrdinalIgnoreCase))
                return true; // battle mode consumes ApplyResourceDelta (FA10) / ApplyStatus (FA2) / ModifyStat (FA1) only; every other action is inert here

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

        bool ExecApplyStatus(EffectExecuteContext ctx, EffectActionPlanItem item)
        {
            if (Status is null || StatusRng is null) return true; // not wired (e.g. a bare test harness) -- refuse quietly, not a NullReferenceException

            var statusId = item.Params.TryGetValue("status", out var s) ? s as string : null;
            if (string.IsNullOrWhiteSpace(statusId)) return true; // malformed content, refused upstream at bind

            var durationSec = item.Params.TryGetValue("duration", out var d) ? Convert.ToDouble(d) : 4.0;
            var durationMs = (int)Math.Round(durationSec * 1000);
            var targetPtr = item.Params.TryGetValue("targetPtr", out var p) ? p as string : ctx.Event.TargetPtr;
            if (string.IsNullOrWhiteSpace(targetPtr)) return true;

            // BaseDuration and DurationMs are the SAME unit (ms) -- found empirically: StatusRuntime.Apply
            // uses eval.EffectiveDuration (derived FROM BaseDuration) whenever BaseDuration > 0, so
            // passing durationSec (seconds) here produced a 5ms status for an authored 5-SECOND
            // duration. Verified against the existing scripted-InitialStatuses call
            // (BattleRunState.cs), which already passes the identical ms value to both fields.
            Status.Apply(new StatusApplyInput(
                StatusId: statusId!,
                HostPtr: targetPtr!,
                AttackerPtr: ctx.Event.ActorPtr,
                GrantId: item.GrantId,
                BaseMagnitude: 0, // FA2 never pulses HP -- that payload lives on FA10 resource.delta (A18c)
                BaseDuration: durationMs,
                DurationMs: durationMs,
                GrantChance: 1.0, // the atom's own bind-time chance gate already decided whether this plan item exists
                EffectId: item.EffectId,
                PluginId: "battle",
                AttackerLess: ctx.Event.ActorPtr is null), StatusRng, _clock.UtcNow);
            return true;
        }

        bool ExecModifyStat(EffectExecuteContext ctx, EffectActionPlanItem item)
        {
            if (Ledger is null || ResolveStatTarget is null) return true; // not wired -- refuse quietly, same posture as Status/StatusRng above

            var channel = item.Params.TryGetValue("channel", out var c) ? c as string : null;
            if (string.IsNullOrWhiteSpace(channel)) return true; // malformed content, refused upstream at bind

            // Found against real shipped content (fx.passive_atk_flat), not the atom kind's own
            // authoring-time schema: EffectOverlayMerge's own ModifyStat allowlist ("channel", "flat",
            // "increased", "more", ...) uses THREE SEPARATE, independently-optional keys, never a
            // combined "op"+"amount" pair. A first draft assumed op+amount (matching the ATOM
            // schema's own authoring-time param names, AtomKindRegistry.cs) and silently no-oped on
            // every real grant -- AtomCompiler translates op+amount into whichever of these three keys
            // at compile time; by the time a plan item reaches this sink, it is already in this shape.
            // One action may carry more than one key at once (e.g. a flat AND an increased together).
            var ownerKey = ctx.Grant.OwnerKey.StartsWith("entity:", StringComparison.Ordinal)
                ? ctx.Grant.OwnerKey["entity:".Length..] : ctx.Grant.OwnerKey;
            var owner = ResolveStatTarget(ownerKey);
            if (owner is null) return true; // no live actor under this key (e.g. already dead)

            var pluginId = item.Tags.GetValueOrDefault("plugin", "battle");
            var any = false;
            void AddIfPresent(string key, Stats.ModifierOp op)
            {
                if (!item.Params.TryGetValue(key, out var raw) || raw is null) return;
                Ledger!.Add(ownerKey, channel!, item.GrantId, new Stats.StatModifier
                {
                    Channel = channel!, Op = op, Value = Convert.ToDouble(raw), SourceId = item.GrantId, PluginId = pluginId,
                });
                any = true;
            }
            // "override" is refused at bind (AtomKindRegistry.Validate) -- no case for it here.
            AddIfPresent("flat", Stats.ModifierOp.Flat);
            AddIfPresent("increased", Stats.ModifierOp.Increased);
            AddIfPresent("more", Stats.ModifierOp.More);
            if (!any) return true; // malformed content: channel present, no recognised op key at all

            // "atk" needs no push here -- ActorState.LiveAtk recomposes on every read (A18e §2).
            // Defense already lives in Derived; a targeted in-place Set is how every existing reader
            // (calculator.Compute's CombatActorSnapshot) sees the update on its next read.
            if (string.Equals(channel, "defense", StringComparison.Ordinal))
                owner.Derived.Set(DerivedStatChannels.CombatDefenseOmni,
                    Ledger.Recompose(ownerKey, channel!, owner.BaselineDefense));

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
