using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Combat.Shield;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Battle;

// B13 (spec-kernel-adoption.md, battle-timeline-todo.md): BattleRunState is the state object T5
// needs a callback target for. The spec's own Structure section sketches this file at
// Battle/Timeline/BattleRunState.cs — deliberately NOT followed here, and the deviation is recorded
// rather than silent: putting it under Battle/Timeline/ would put every LINQ call and DateTimeOffset
// use in this file under KernelPurityScan's full purity+tick-path rules (that directory has no
// per-file exemption model for "ordinary battle-domain code that happens to be adjacent to the
// kernel"), and it would force ActorState / SelectTarget / IsCcLocked / FindAdjacentWithTrait /
// AnyActive / RunBasicAttackStep / EssenceTraits from `private` to `internal` just to stay
// reachable across namespaces — a visibility change with no zero-behavior-change refactor should
// need. Nesting BattleRunState inside `BattleEngine` instead (same trick BasicAttack.cs already
// uses for `RunBasicAttackStep`, a sibling `partial class BattleEngine` file) keeps every one of
// those private members reachable with NO visibility change at all — the smallest possible diff
// for a step whose entire acceptance bar is "if a golden moves here, stop."
public static partial class BattleEngine
{
    /// <summary>
    /// Everything `Resolve` used to hold as local state and closures, extracted verbatim — no
    /// method body below differs from its pre-extraction original by more than "closure over a
    /// local" becoming "instance method over a field." `Resolve` itself still owns the round
    /// skeleton (the `while` loop, initiative ordering, the per-attacker `Continue`/`Break` check) —
    /// turning that skeleton into scheduled kernel events is B14's job, deliberately not this one's.
    /// </summary>
    sealed class BattleRunState
    {
        public readonly List<ActorState> Actors;
        public readonly Dictionary<string, ActorState> ByKey;
        public readonly BattleEffectHost Host;
        public readonly StatusRuntime Status;
        public readonly ShieldRuntime Shields;
        public readonly ShieldGate ShieldGate;
        public readonly FunnelHpDeltaSink HpSink;
        public readonly BattlePulseSink PulseSink;
        public readonly OverlayCombatCalculator Calculator;
        public readonly SeededRng InitiativeRng;
        public readonly ICombatRng CritRng;
        public readonly SeededRng EssenceRng;
        public readonly BattleStatusRng StatusRng;
        public readonly DateTimeOffset T0;
        public readonly List<BattleEventRec> Events = new();
        public readonly HashSet<string> RecordedDeaths = new(StringComparer.Ordinal);
        public readonly Timeline.BattleTrace? Trace;

        readonly List<ShieldEventRec> _shieldEventScratch = new();

        public BattleRunState(BattleSetup setup, ulong seed, Timeline.BattleTrace? trace, Action<BattleEffectHost>? onEffectHostReady)
        {
            Trace = trace;

            InitiativeRng = SeededRng.DeriveStream(seed, "initiative");
            ICombatRng critRng = new SeededRngCombatAdapter(SeededRng.DeriveStream(seed, "crit"));
            if (trace != null) critRng = trace.WrapCombat("crit", critRng);
            CritRng = critRng;
            EssenceRng = SeededRng.DeriveStream(seed, "essence");
            StatusRng = new BattleStatusRng(seed, trace);
            Calculator = new OverlayCombatCalculator();

            // Stable ordered state — never dictionary-enumerated (determinism discipline).
            Actors = setup.Squad.Select((a, i) => new ActorState(a, i))
                .Concat(setup.Wave.Select((a, i) => new ActorState(a, i)))
                .ToList();
            ByKey = new Dictionary<string, ActorState>(StringComparer.Ordinal);
            foreach (var a in Actors)
                ByKey[a.Setup.Key] = a;

            // Battle-local effect stack: funnel → FA10 sink over engine state; statuses over the
            // composed derived profiles; the clock is the synthetic round clock.
            Host = new BattleEffectHost(key => ByKey.TryGetValue(key, out var a) ? a : null, seed);
            T0 = Host.Clock.UtcNow;
            Status = new StatusRuntime(StatusCatalogBootstrap.CreateDefault(),
                (ptr, attackerLess) => attackerLess || ptr == null || !ByKey.TryGetValue(ptr, out var a)
                    ? ActorDerivedSnapshot.AttackerLess()
                    : a.Derived);

            // Shield stack (battle-adoption): battle-local runtime + gate; every HP delta goes
            // through the shared pipeline so the one-key discipline holds (single FA10 slot per
            // actor per window) and shields absorb before HP — overlay-identical semantics.
            Shields = new ShieldRuntime();
            ShieldGate = new ShieldGate(Shields, (ptr, attackerLess) =>
                attackerLess || ptr == null || !ByKey.TryGetValue(ptr, out var a)
                    ? CombatActorSnapshot.AttackerLess()
                    : new CombatActorSnapshot(a.Derived, a.ElementTypes));
            HpSink = new FunnelHpDeltaSink(Host.Funnel);

            // T14: the grant path. `ExecGrantShield` requires `Bag.ShieldGate`, which neither
            // `BattleEffectHost` nor `SimEffectHost` ever set (AtomKindRegistry.cs's shield.grant D6
            // comment) — wired here, to the SAME gate ordinary attacks already absorb through, so a
            // granted shield and a swing-dealt hit share one shield stack rather than two.
            Host.Bag.ShieldGate = ShieldGate;
            onEffectHostReady?.Invoke(Host);

            PulseSink = new BattlePulseSink((hostPtr, amount, effectId) =>
                ByKey.TryGetValue(hostPtr, out var owner)
                    ? ApplyHp(owner, amount, effectId)
                    : new DamageApplyResult(DamageApplyOutcome.SinkRefused, 0, 0));

            foreach (var a in Actors)
                Events.Add(new BattleEventRec(0, BattleEventKinds.Spawn, a.Setup.Key, a.Setup.TypeId, a.Setup.Side));

            // Innate shields (battle-adoption): direct apply at setup — snapshots composed in the
            // ActorState ctor, so the shield spec's capacity barrier is satisfied by construction.
            // Content durations are ms; battle ticks are rounds.
            foreach (var a in Actors)
            {
                if (a.Setup.InnateShield is not { } innate) continue;
                Shields.Apply(new ShieldGrant
                {
                    OwnerKey = Contracts.EffectOwnerKeys.Entity(a.Setup.Key),
                    SourceId = "innate:" + a.Setup.TypeId,
                    Element = innate.Element,
                    BaseHp = innate.BaseHp,
                    Priority = innate.Priority,
                    DurationTicks = innate.DurationMs is { } ms
                        ? (ms + BattleRuleset.RoundDurationMs - 1) / BattleRuleset.RoundDurationMs
                        : null,
                    RefillOnMerge = false,
                    IsInnate = true
                }, a.Derived, nowTick: 0);
            }

            // Initial statuses land attacker-less at t0 (trait/attack riders reuse this path later).
            // Scripted setup statuses apply deterministically — the L2b evaluator still blocks them
            // on immunity/potency floor (resist channels), but the apply roll is bypassed (0.0 roll).
            var scriptedApplyRng = new FixedStatusRng(0.0);
            foreach (var a in Actors)
            {
                foreach (var spec in a.Setup.InitialStatuses)
                {
                    Status.Apply(new StatusApplyInput(
                        spec.StatusId,
                        HostPtr: a.Setup.Key,
                        AttackerPtr: null,
                        GrantId: "battle:init:" + a.Setup.Key + ":" + spec.StatusId,
                        BaseMagnitude: spec.MagnitudePerPulse,
                        BaseDuration: spec.DurationMs,
                        PeriodMs: spec.PeriodMs,
                        DurationMs: spec.DurationMs,
                        GrantChance: spec.GrantChanceMilli / 1000.0,
                        EffectId: "battle.status." + spec.StatusId,
                        PluginId: "battle",
                        AttackerLess: true), scriptedApplyRng, T0);
                }
            }
        }

        public DamageApplyResult ApplyHp(
            ActorState owner, long amount, string effectId,
            ElementPayloadComponent[]? components = null, ActorState? attacker = null, string? grantId = null)
        {
            var result = DamageApplyPipeline.Apply(
                owner.Setup.Key, amount, hitCount: 1,
                components ?? Array.Empty<ElementPayloadComponent>(),
                attacker?.Derived, owner.Derived, ShieldGate, HpSink,
                pluginId: "battle", effectId: effectId, grantId: grantId);
            owner.ShieldAbsorbed += result.AbsorbedAmount;
            return result;
        }

        /// <summary>
        /// B16: the earliest tick any live status instance's own <c>NextPulse</c> falls due — what
        /// the kernel schedules its next status-pulse event against, so pulses fire at their TRUE
        /// times instead of once per 1000 ms round. A MIN reduction over <c>AllInstances()</c>, so
        /// its dictionary-backed enumeration order (not otherwise guaranteed) cannot matter here —
        /// <see cref="StatusRuntime.Tick"/> itself already host-sorts ordinally before firing any
        /// pulse for exactly the determinism reason this codebase always cites; this method never
        /// touches firing order, only "when is the soonest one due."
        /// </summary>
        public DateTimeOffset? NextStatusPulseAt()
        {
            DateTimeOffset? earliest = null;
            foreach (var inst in Status.AllInstances())
            {
                if (inst.PeriodMs <= 0) continue;
                if (inst.NextPulse > inst.ExpiresAt) continue;
                if (earliest is null || inst.NextPulse < earliest.Value) earliest = inst.NextPulse;
            }
            return earliest;
        }

        public void RunRegeneratorPulses()
        {
            foreach (var a in Actors)
            {
                if (a.Active && a.Has("regenerator"))
                {
                    var def = TraitBattleCatalog.Get("regenerator");
                    ApplyHp(a, Math.Max(1, a.MaxHp * def.RegenPerRoundMilli / 1000), "battle.trait.regenerator");
                }
            }
        }

        public void DrainShieldEvents(int round)
        {
            _shieldEventScratch.Clear();   // caller owns the scratch on EVERY path (DrainEvents appends)
            if (Shields.DrainEvents(_shieldEventScratch) == 0) return;
            foreach (var rec in _shieldEventScratch)
            {
                var key = rec.OwnerKey.StartsWith("entity:", StringComparison.Ordinal)
                    ? rec.OwnerKey.Substring("entity:".Length)
                    : rec.OwnerKey;
                if (!ByKey.TryGetValue(key, out var owner)) continue;
                Events.Add(new BattleEventRec(round, rec.Kind,
                    key, owner.Setup.TypeId, owner.Setup.Side, rec.Amount, rec.Element, rec.ShieldId));
            }

            _shieldEventScratch.Clear();
        }

        public void SweepDeaths(int round)
        {
            foreach (var a in Actors)
            {
                if (!a.Alive && RecordedDeaths.Add(a.Setup.Key))
                {
                    Events.Add(new BattleEventRec(round, BattleEventKinds.Die, a.Setup.Key, a.Setup.TypeId, a.Setup.Side));
                    Shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(a.Setup.Key));
                }
            }
        }

        /// <summary>Immortal death refusal: a queued +1 through the pipeline turns the death into survive-at-1.</summary>
        public void ReviveImmortals()
        {
            var queued = false;
            foreach (var a in Actors)
            {
                if (!a.Alive && !a.Retreated && a.ImmortalCharges > 0 && !RecordedDeaths.Contains(a.Setup.Key))
                {
                    a.ImmortalCharges--;
                    ApplyHp(a, 1, "battle.trait.immortal");
                    queued = true;
                }
            }

            if (queued)
                Host.Flush();
        }

        /// <summary>Coward retreat: below the threshold the actor leaves the battle alive (no die event).</summary>
        public void CheckRetreats()
        {
            foreach (var a in Actors)
            {
                if (!a.Active || !a.Has("coward")) continue;
                var def = TraitBattleCatalog.Get("coward");
                if ((long)a.Hp * 1000 < (long)a.MaxHp * def.RetreatBelowMilli)
                {
                    a.Retreated = true;
                    Status.WithdrawEntity(a.Setup.Key);
                    Shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(a.Setup.Key));
                }
            }
        }

        public void PostFlush(int round)
        {
            ReviveImmortals();
            SweepDeaths(round);
            CheckRetreats();
        }

        public bool AnyActive(string side) => BattleEngine.AnyActive(Actors, side);

        /// <summary>
        /// The per-attacker tail (spec-basic-attack-adoption.md's boundary: everything from the
        /// berserker ramp onward is EngineBehavior trait logic, not the declared basic-attack action
        /// itself) — berserker ramp, essence riders, guardian split, apply, flush, tallies, revive,
        /// kill/death, soul-eater, retreat check. The caller (`Resolve`'s per-attacker loop) still
        /// owns calling `RunBasicAttackStep` and checking its `Continue`/`Break` outcome — only a
        /// `Proceed` step reaches this method.
        /// </summary>
        public void DispatchHit(ActorState attacker, ActorState target, long signedDelta, int round)
        {
            var damage = (int)(-signedDelta);

            // Berserker ramp: battle mechanic on resolver OUTPUT, never inside the formula.
            if (attacker.Has("berserker"))
                damage = damage * TraitBattleMath.BerserkerRampMilli(
                    TraitBattleCatalog.Get("berserker"), attacker.Hp, attacker.MaxHp) / 1000;

            // Essence riders (void-touched / chaos-marked): per-landed-hit proc on its own stream.
            var rider = 0;
            foreach (var essenceId in EssenceTraits)
            {
                if (!attacker.Has(essenceId)) continue;
                var def = TraitBattleCatalog.Get(essenceId);
                var essenceRoll = EssenceRng.NextPerMille();
                Trace?.Draw("essence", essenceRoll);
                if (essenceRoll < def.EssenceProcMilli)
                    rider += Math.Max(1, damage * def.EssenceRiderMilli / 1000);
            }

            // Guardian: an adjacent active guardian pulls a share of the hit onto itself.
            // Each slice passes the gate separately — both actors' shields absorb their own
            // portion (spec: guardian two-slice semantics).
            var guardian = FindAdjacentWithTrait(Actors, target, "guardian");
            var share = guardian != null
                ? damage * TraitBattleCatalog.Get("guardian").GuardShareMilli / 1000
                : 0;

            var mainDelta = -(damage - share + rider);
            ApplyHp(target, mainDelta, "battle.attack", attacker.AttackComponents, attacker);
            Trace?.Apply(round, target.Setup.Key, mainDelta);
            if (share > 0)
            {
                ApplyHp(guardian!, -share, "battle.trait.guardian", attacker.AttackComponents, attacker);
                Trace?.Apply(round, guardian!.Setup.Key, -share);
            }

            Host.Flush();
            attacker.DamageDealt += damage + rider;   // resolver output, pre-absorb (spec)

            ReviveImmortals();
            var killsThisHit = 0;
            foreach (var victim in guardian == null ? new[] { target } : new[] { target, guardian })
            {
                if (!victim.Alive && RecordedDeaths.Add(victim.Setup.Key))
                {
                    attacker.Kills++;
                    killsThisHit++;
                    Events.Add(new BattleEventRec(round, BattleEventKinds.Die, victim.Setup.Key, victim.Setup.TypeId, victim.Setup.Side));
                    Shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(victim.Setup.Key));
                }
            }

            // Soul-eater: on-kill heal through the pipeline.
            if (killsThisHit > 0 && attacker.Has("soul-eater"))
            {
                var def = TraitBattleCatalog.Get("soul-eater");
                ApplyHp(attacker,
                    (long)killsThisHit * Math.Max(1, attacker.MaxHp * def.OnKillHealMilli / 1000),
                    "battle.trait.soul-eater");
                Host.Flush();
            }

            CheckRetreats();
        }
    }
}
