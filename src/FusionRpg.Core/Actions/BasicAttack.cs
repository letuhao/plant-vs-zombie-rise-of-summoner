using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle;

// This file lives under Core/Actions/ (spec-basic-attack-adoption.md's own Structure section), but
// declares part of BattleEngine itself (namespace FusionRpg.Core.Battle, `partial`) rather than a
// free-standing type: the four adopted steps need ActorState, SelectTarget and IsCcLocked, all
// private to BattleEngine, and duplicating them would be the second private-state copy this whole
// program refuses to create. The authored row lives here; the loop that calls it lives in
// BattleEngine.cs — one seam, two files.

public static partial class BattleEngine
{
    /// <summary>
    /// The authored row for the basic attack (spec-basic-attack-adoption.md §2). Degenerate on
    /// purpose — every timing field at zero proves plumbing and nothing else. `A12`'s real actions
    /// under a real profile are where non-zero timing gets exercised.
    /// </summary>
    public static readonly ActionEnvelope BasicAttackEnvelope = ActionEnvelope.NoOp with
    {
        ActionId = "act.attack",
        Commitment = Commitment.LateBound,
        // S2/S3 (species-skills): the basic attack opts BOTH readers in, so the two channels are live
        // in every battle rather than sitting behind content that does not exist yet. Byte-identical
        // today because both channels register with a default of 0, which is neutral for each: 0%
        // cooldown reduction, and a x1.0 effectiveness multiplier.
        CooldownChannel = DerivedStatChannels.SkillCooldown(DerivedStatChannels.ActionCategoryAttack),
        EffectivenessChannel = DerivedStatChannels.SkillEffectiveness(DerivedStatChannels.ActionCategoryAttack),
    };

    /// <summary>
    /// The authored targeting rule (spec-targeting.md §2a). `Ordering = SourceOrder` is the field
    /// §3's hazard exists to name: <c>TargetResolver</c> sorts by ordinal ptr, but
    /// <see cref="StubIntentSource.TryDeclare"/>'s own <c>NearestEnemy</c> falls back to list order
    /// whenever <see cref="IBattleView.PositionOf"/> is null (no board) — so the basic attack states
    /// which order it means rather than silently disagreeing with the resolver. <c>bloodthirsty</c>
    /// and <c>loyal</c> stay engine-side (spec-action-selection-adoption.md §5, <see cref="BloodthirstyViewFor"/>
    /// below and the bodyguard check in <see cref="RunBasicAttackStep"/>) — trait behaviour a generic
    /// target spec does not (and should not) express.
    /// </summary>
    public static readonly ActionTargetSpec BasicAttackTargeting = new()
    {
        Mode = ActionTargetMode.Single,
        Relation = ActionRelation.Enemy,
        Ordering = ActionTargetOrdering.SourceOrder,
    };

    enum AttackStepOutcome
    {
        /// <summary>No valid target, or the attacker cannot act — the round's attack phase ends for this actor.</summary>
        Continue,
        /// <summary>No valid target for this attacker at all — hazard 3: the ROUND breaks, it does not continue.</summary>
        Break,
        /// <summary>A hit landed; the caller applies it and runs the trait tail.</summary>
        Proceed,
    }

    readonly record struct AttackStep(AttackStepOutcome Outcome, ActorState? Target, long SignedDelta);

    /// <summary>
    /// A17 (spec-action-selection-adoption.md §3): active check → CC-lock →
    /// <see cref="StubIntentSource.TryDeclare"/> → loyal-bodyguard redirect → <c>calculator.Compute</c>.
    /// `SelectTarget` is gone as the live targeting path — the intent source decides who, using
    /// <see cref="BloodthirstyViewFor"/> to steer it exactly where the old bloodthirsty branch did,
    /// without teaching it the trait. Everything from the berserker ramp onward is `EngineBehavior`
    /// trait logic and stays in the loop.
    /// </summary>
    static AttackStep RunBasicAttackStep(
        ActorState attacker, BattleRunState state, DateTimeOffset now, long nowTick,
        OverlayCombatCalculator calculator, ICombatRng critRng, BattleTrace? trace, int round,
        IIntentSource? intentSource = null)
    {
        if (!attacker.Active) return new AttackStep(AttackStepOutcome.Continue, null, 0);
        if (IsCcLocked(state.Status, attacker.Setup.Key, now)) return new AttackStep(AttackStepOutcome.Continue, null, 0);

        var view = BloodthirstyViewFor(state, attacker);
        // T6/B20: an injected source is how an interactive battle occupies the `Ready` dwell, and how a
        // replay reads its decision trace instead of re-deciding. `null` keeps the shipped AI policy,
        // which is every battle today — so this is byte-identical until a caller passes one.
        var source = intentSource
            ?? new StubIntentSource(view, state.Cooldowns, NoStanceHeld.Instance, AlwaysAffordable.Instance);
        var intent = source.TryDeclare(attacker.Setup.Key, nowTick);
        if (intent.IsNone) return new AttackStep(AttackStepOutcome.Break, null, 0); // hazard 3: round breaks

        var target = state.ByKey[intent.TargetKey!];
        var bodyguard = FindAdjacentWithTrait(state.Actors, target, "loyal");
        if (bodyguard != null && !IsCcLocked(state.Status, bodyguard.Setup.Key, now))
            target = bodyguard;

        // A18b (spec-on-activate-trigger.md §2): fires once per resolved (non-Break) intent,
        // independent of hit/miss -- a cast succeeds even if the attack roll misses -- at the
        // post-redirect target. A no-op today for every actor without a bound OnActivate grant
        // (A18a's own scope: nothing binds one without a real ContainerId).
        state.Host.Bag.OnEvent(new Contracts.EffectEventDto
        {
            Trigger = AtomTriggers.OnActivate,
            ActorPtr = attacker.Setup.Key,
            TargetPtr = target.Setup.Key,
            Tick = nowTick,
            HitCount = 1,
        });
        state.Host.Flush();

        trace?.Target(round, attacker.Setup.Key, target.Setup.Key);

        var (signedDelta, breakdown) = calculator.Compute(new OverlayCombatRequest
        {
            // A18e (spec-battle-live-stat-modifiers.md §2): the one production read-site this module
            // touches. Byte-identical to Setup.Atk whenever the ledger holds no atk mods for this
            // actor -- every battle today, since nothing binds a stat.modify grant yet.
            BaseOverlayDamage = attacker.LiveAtk(state.Ledger),
            Components = attacker.AttackComponents,
            Attacker = new CombatActorSnapshot(attacker.Derived, attacker.ElementTypes),
            Defender = new CombatActorSnapshot(target.Derived, target.ElementTypes),
            // S3 (species-skills): the attacker's own `skill.effectiveness.{category}` scales the
            // payload, applied INSIDE the resolver (never as a second multiplier afterwards, which
            // would put combat math outside the SSOT and trip the parity tests by design). 0 is
            // neutral and yields exactly 1.0, so an envelope that names no channel is byte-identical.
            EffectivenessMultiplier =
                OverlayCombatRequest.MultiplierFromPerMille(SkillEffectivenessPm(attacker, intent.Envelope)),
            Profile = CombatProfile.BattleSim
        }, critRng);

        if (!breakdown.Hit) return new AttackStep(AttackStepOutcome.Continue, null, 0);

        // A18c (spec-battle-resource-shield-grants.md §2): resource.delta's existing shipped content
        // (fx.poison_on_hit, fx.freeze_on_hit, ...) is OnDamageDealt-triggered, not OnActivate -- a
        // skill's on-hit rider fires when the hit actually lands, mirroring existing content exactly.
        // Only on a landed hit (this line is unreachable on a miss, above); before DispatchHit's own
        // trait tail runs, so a rider fires alongside the calculator-resolved damage, not nested
        // inside EngineBehavior.
        state.Host.Bag.OnEvent(new Contracts.EffectEventDto
        {
            Trigger = AtomTriggers.OnDamageDealt,
            ActorPtr = attacker.Setup.Key,
            TargetPtr = target.Setup.Key,
            Damage = -signedDelta,
            Tick = nowTick,
            HitCount = 1,
        });
        state.Host.Flush();

        // S2 (species-skills): the cooldown is reduced by the attacker's own
        // `skill.cooldown.{category}` — the channel the envelope itself names — resolved HERE, at the
        // arming site, because CooldownLedger stores an absolute tick. An envelope with no
        // CooldownChannel reads nothing and arms at base ticks; that is the neutral path and it stays
        // allocation-free. Inert for Class.None, as before.
        state.Cooldowns.Start(attacker.Setup.Key, intent.Envelope, nowTick,
            SkillCooldownReductionPm(attacker, intent.Envelope));
        return new AttackStep(AttackStepOutcome.Proceed, target, signedDelta);
    }

    /// <summary>
    /// The acting actor's cooldown reduction for this action, per-mille — 0 when the envelope names
    /// no channel, which is every action that does not opt in. Reads the actor's already-composed
    /// derived snapshot, so this adds no resolve.
    /// </summary>
    static long SkillCooldownReductionPm(ActorState actor, ActionEnvelope envelope) =>
        envelope.CooldownChannel is { } channel ? (long)actor.Derived.Get(channel) : 0;

    /// <summary>The acting actor's effectiveness bonus for this action, per-mille — 0 when the
    /// envelope names no channel. Returns a <c>long</c> on purpose: the per-mille to multiplier
    /// conversion is double arithmetic and belongs in <c>Combat/</c>, because this directory bans
    /// floating point.</summary>
    static long SkillEffectivenessPm(ActorState actor, ActionEnvelope envelope) =>
        envelope.EffectivenessChannel is { } channel ? (long)actor.Derived.Get(channel) : 0;

    /// <summary>
    /// spec-action-selection-adoption.md §5: `bloodthirsty` stays engine-side, reimplemented as a
    /// pre-filter over the attacker's own enemy view rather than as a branch inside
    /// <see cref="StubIntentSource"/>, which must not gain trait vocabulary it does not own. Moving
    /// the lowest-HP live enemy to the front of <see cref="IBattleView.LiveActorKeys"/> is enough:
    /// <c>NearestEnemy</c>'s own no-board fallback returns the first enemy it finds.
    /// </summary>
    static IBattleView BloodthirstyViewFor(BattleRunState state, ActorState attacker)
    {
        if (!attacker.Has("bloodthirsty")) return state;

        ActorState? lowest = null;
        foreach (var a in state.Actors)
            if (a.Active && a.Setup.Side != attacker.Setup.Side && (lowest == null || a.Hp < lowest.Hp))
                lowest = a;

        return lowest is null ? state : new BloodthirstyView(state, lowest.Setup.Key);
    }

    /// <summary>The same live-actor set as <c>inner</c>, with one key moved to the front — the
    /// mechanism <see cref="BloodthirstyViewFor"/> uses.</summary>
    sealed class BloodthirstyView : IBattleView
    {
        readonly IBattleView _inner;
        readonly IReadOnlyList<string> _order;

        public BloodthirstyView(IBattleView inner, string priorityKey)
        {
            _inner = inner;
            var live = inner.LiveActorKeys;
            var ordered = new List<string>(live.Count) { priorityKey };
            foreach (var key in live)
                if (!string.Equals(key, priorityKey, StringComparison.Ordinal))
                    ordered.Add(key);
            _order = ordered;
        }

        public IReadOnlyList<string> LiveActorKeys => _order;
        public int SideOf(string actorKey) => _inner.SideOf(actorKey);
        public GridPos? PositionOf(string actorKey) => _inner.PositionOf(actorKey);
        public EntityFacts FactsOf(string actorKey) => _inner.FactsOf(actorKey);
        public IReadOnlyList<CompiledAction> HeldActionsOf(string actorKey) => _inner.HeldActionsOf(actorKey);
    }
}
