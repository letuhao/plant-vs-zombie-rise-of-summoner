using FusionRpg.Core.Actions;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Combat.Shield;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Battle;

// B13 (spec-kernel-adoption.md, battle-timeline-todo.md): BattleRunState is the state object T5
// needs a callback target for. The spec's own Structure section sketches this file at
// Battle/Timeline/BattleRunState.cs — deliberately NOT followed here, and the deviation is recorded
// rather than silent: putting it under Battle/Timeline/ would put every LINQ call and DateTimeOffset
// use in this file under KernelPurityScan's full purity+tick-path rules (that directory has no
// per-file exemption model for "ordinary battle-domain code that happens to be adjacent to the
// kernel"), and it would force ActorState / IsCcLocked / FindAdjacentWithTrait /
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
    sealed class BattleRunState : IBattleView
    {
        /// <summary>
        /// A17 (spec-action-selection-adoption.md §2): the fallback held action for any actor whose
        /// `EquippedActionIds` is null or empty — "no loadout" must still produce a legal, single-
        /// action AI decision, never `ActionIntent.None` by construction. Hand-built rather than run
        /// through `ActionCompiler.Compile`: the basic attack has no rung, no container, no atoms —
        /// forcing it through the real-content compiler would mean inventing fake rung/container rows
        /// for something that fundamentally has neither, exactly the trap `A5`'s own degenerate
        /// envelope was designed to sidestep. `TargetSpecCompiler.Compile` and `PredicateCompiler.Always`
        /// are still the REAL compiler pieces, reused rather than re-guessed, for the two fields that
        /// have one.
        /// </summary>
        static readonly CompiledAction BasicAttackCompiled = new(
            ActionId: BasicAttackEnvelope.ActionId,
            Kind: ActionKind.Basic,
            Rung: 0,
            Tags: new[] { ActionTag.Offensive },
            Enabled: true,
            Revision: 0,
            Grantable: false,
            DefaultAttackEligible: true,
            ContainerId: "",
            Envelope: BasicAttackEnvelope,
            Targeting: TargetSpecCompiler.Compile(BasicAttackTargeting),
            MinRange: 0,
            MaxRange: int.MaxValue,
            RangeChannel: null,
            RequiresLineOfSight: false,
            Condition: PredicateCompiler.Always,
            Costs: Array.Empty<CompiledActionCost>(),
            Scopes: Array.Empty<ActionScopeRow>());

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
        public readonly SeededRng RidersRng;

        /// <summary>Wave E1: riders decide their own chance on <see cref="RidersRng"/>, so the
        /// evaluator gets a scripted 0.0 rather than a second roll. Same object and same reasoning as
        /// the scripted setup-status path.</summary>
        static readonly FixedStatusRng RiderApplyRng = new(0.0);
        public readonly BattleStatusRng StatusRng;
        public readonly DateTimeOffset T0;
        public readonly List<BattleEventRec> Events = new();
        public readonly HashSet<string> RecordedDeaths = new(StringComparer.Ordinal);
        public readonly Timeline.BattleTrace? Trace;

        /// <summary>A17: real ledger, per spec-action-selection-adoption.md §6 — inert for the
        /// all-zero basic-attack envelope (`Class.None`), so A19's real cooldowns need no further
        /// wiring here when they arrive.</summary>
        public readonly Timeline.CooldownLedger Cooldowns = new();

        /// <summary>
        /// B38 — one <see cref="Timeline.ActorTurnMachine"/> per actor, for the whole battle.
        ///
        /// <para>Before this the per-actor FSM existed and was fully tested but was never driven by a
        /// real battle: `ActorTurnMachine` appeared nowhere in the engine. An interactive dwell needs a
        /// `Ready` state to occupy, so B20/B21/B22 had nothing to attach to. These machines are what
        /// give them one.</para>
        ///
        /// <para><b>Pure bookkeeping under `classic-round`</b>: with zero wind-up and zero recovery the
        /// cycle collapses to Charging → Ready → Committed → Resolving → Recovering → Charging around
        /// the same attack, in the same order, drawing the same RNG. Byte-identical by construction.</para>
        /// </summary>
        public readonly Dictionary<string, Timeline.ActorTurnMachine> TurnMachines = new(StringComparer.Ordinal);

        public Timeline.ActorTurnMachine MachineFor(string actorKey) =>
            TurnMachines.TryGetValue(actorKey, out var m)
                ? m
                : TurnMachines[actorKey] = new Timeline.ActorTurnMachine(actorKey);

        /// <summary>A18e (spec-battle-live-stat-modifiers.md §1): one instance per battle, same
        /// lifetime as Cooldowns/Shields above.</summary>
        public readonly BattleStatModifierLedger Ledger = new();

        /// <summary>aura-skill T4: the recompose seam for `Derived` (`combat.*`) channels — one
        /// instance per battle, same lifetime as <see cref="Ledger"/>. Nothing adds to this yet (no
        /// aura wiring lands before T9); it exists so a later, real toggle event has a call to make
        /// rather than a mechanism to invent under time pressure.</summary>
        public readonly BattleDerivedModifierLedger DerivedLedger = new();

        /// <summary>aura-skill T4: the explicit recompose entry point — deliberately not called
        /// anywhere in `Resolve`'s own loop. "Explicit, never implicit per-tick" (the task's own
        /// acceptance bar) means a real trigger (an aura toggling on/off, T13) calls this at the
        /// moment it happens; nothing calls it on a schedule.</summary>
        public void RecomposeDerived(string actorKey)
        {
            var actor = ByKey[actorKey];
            DerivedLedger.Recompose(actorKey, actor.BaseDerived, actor.Derived);
        }

        readonly List<ShieldEventRec> _shieldEventScratch = new();
        readonly Dictionary<string, IReadOnlyList<CompiledAction>> _heldActions = new(StringComparer.Ordinal);

        /// <summary>aura-skill T3 (audit D3): equipped-action ids that could not be resolved against
        /// the supplied <see cref="ActionCatalog"/> — the actor degrades to the basic-attack fallback
        /// instead of failing the whole battle. Empty on every setup a golden has ever blessed.</summary>
        public readonly List<string> Warnings = new();

        public BattleRunState(BattleSetup setup, ulong seed, Timeline.BattleTrace? trace,
            Action<BattleEffectHost>? onEffectHostReady, ActionCatalog? actionCatalog = null,
            IContainerEffectResolver? containerResolver = null)
        {
            Trace = trace;

            InitiativeRng = SeededRng.DeriveStream(seed, "initiative");
            ICombatRng critRng = new SeededRngCombatAdapter(SeededRng.DeriveStream(seed, "crit"));
            if (trace != null) critRng = trace.WrapCombat("crit", critRng);
            CritRng = critRng;
            EssenceRng = SeededRng.DeriveStream(seed, "essence");
            // Wave E1: riders draw from their OWN stream, never from "status". The status stream is
            // already the contagion-spread stream, and sharing it would make every rider content
            // change a full-battle butterfly -- the audit fix this wave's spec names explicitly, and
            // the same one-system-one-stream rule `essence` above already follows.
            RidersRng = SeededRng.DeriveStream(seed, "riders");
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

            // A18c (spec-battle-resource-shield-grants.md §1): the SAME shape as ShieldGate above,
            // one line down. `EffectBag.cs:439`'s DoT/contagion piggyback (StatusEffectBridge.TryApplyFromGrant,
            // called from the ApplyResourceDelta branch of FireGrant) is gated on Bag.Status/Bag.StatusRng
            // being set -- neither BattleEffectHost nor SimEffectHost ever did, the exact same gap
            // ShieldGate had. Wired to the SAME StatusRuntime/stream the round loop's own Status.Tick
            // already uses -- one status system, one "status" RNG stream, every application path,
            // never a second instance a grant-applied DoT would roll against differently than a
            // scripted or pulse-delivered one.
            Host.Bag.Status = Status;
            Host.Bag.StatusRng = StatusRng;

            // A18d (spec-battle-status-apply.md §1): the SAME shape, one level over -- BattleEffectSink
            // (not Bag) needs its own Status/StatusRng reference to call StatusRuntime.Apply directly
            // for a standalone status.apply (FA2) plan item, forwarded through Host's own settable
            // properties since BattleEffectSink is private to BattleEffectHost.
            Host.Status = Status;
            Host.StatusRng = StatusRng;

            // A18e (spec-battle-live-stat-modifiers.md §3): the same forwarding shape, one more
            // property. ActorState already implements IBattleStatTarget (its own Derived/BaselineDefense
            // are already public), so the SAME lambda shape resolveActor (below) already uses works
            // here too -- just returning the wider interface.
            Host.Ledger = Ledger;
            Host.ResolveStatTarget = key => ByKey.TryGetValue(key, out var a) ? a : null;
            onEffectHostReady?.Invoke(Host);

            PulseSink = new BattlePulseSink((hostPtr, amount, effectId, components) =>
                ByKey.TryGetValue(hostPtr, out var owner)
                    ? ApplyHp(owner, amount, effectId, components)
                    : new DamageApplyResult(DamageApplyOutcome.SinkRefused, 0, 0));

            foreach (var a in Actors)
                Events.Add(new BattleEventRec(0, BattleEventKinds.Spawn, a.Setup.Key, a.Setup.TypeId, a.Setup.Side));

            // Innate shields (battle-adoption): direct apply at setup — snapshots composed in the
            // ActorState ctor, so the shield spec's capacity barrier is satisfied by construction.
            // B17 (battle-timeline-todo.md): `DurationTicks` is now TRUE ms, passed straight
            // through with no round-ceiling — "battle ticks are rounds" stopped being true the
            // moment `ShieldRuntime.Tick` started being called with `roundClock.Now` (below)
            // instead of a round counter. Round-ceiling meant a 100 ms innate shield silently lived
            // a full 1000 ms round; the true value now expires exactly when authored.
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
                    DurationTicks = innate.DurationMs,
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

            // aura-skill T12 (Gate B): "an aura is on" becomes "a channel has a value," via the T4
            // recompose seam. Delivered once, at construction — a live mid-match toggle is T13's own
            // job, not this one's. Friendly = same Setup.Side as the aura's own CommanderSide; battle's
            // squad/wave partition already IS the own-side/enemy-side split (no oracle needed here —
            // T21a's MechanicalOwnSideOracle answers a different, live-lawn question).
            foreach (var aura in setup.ActiveAuras)
            {
                foreach (var a in Actors)
                {
                    if (a.Setup.Side != aura.CommanderSide) continue;
                    DerivedLedger.Add(a.Setup.Key, aura.TargetChannel, aura.SourceId, aura.Value);
                    RecomposeDerived(a.Setup.Key);
                }
            }

            // A17 (spec-action-selection-adoption.md §2): compile each actor's loadout ONCE, here —
            // never per decision, matching HeldActionsOf's own documented contract that the AI relies
            // on for its "Reads scales with targets, not actions" acceptance bar. Null or empty
            // EquippedActionIds (BattleModels.cs's own doc: "null when the caller has no
            // action/loadout system to consult") falls back to the single hand-built basic attack —
            // "no loadout" must still be a legal, single-action decision, never ActionIntent.None by
            // construction.
            //
            // aura-skill T3 (audit D3): a non-empty list that CANNOT resolve — no ActionCatalog
            // supplied, or an id the catalog doesn't have — used to throw and fail the whole battle,
            // which meant the first authored Skill grant broke every web battle AND poisoned any
            // already-stored BattleSetup log row (it re-threw on every replay, forever). There is no
            // production action-authoring path yet (aura-equip-path, unspecced): degrading to "no
            // equipped actions" + a named warning is the honest behavior for content that cannot
            // exist in production today, not a masked bug — T19 wires the real ActionCatalog and this
            // degrade path stops firing for any actor whose loadout it can actually resolve.
            foreach (var a in Actors)
            {
                var ids = a.Setup.EquippedActionIds;
                IReadOnlyList<CompiledAction> held;
                if (ids is null || ids.Count == 0)
                {
                    held = new[] { BasicAttackCompiled };
                }
                else if (actionCatalog is null)
                {
                    Warnings.Add(
                        $"Actor '{a.Setup.Key}' has {ids.Count} equipped action id(s) but no ActionCatalog " +
                        "was supplied to resolve them; falling back to the basic attack.");
                    held = new[] { BasicAttackCompiled };
                }
                else
                {
                    var list = new List<CompiledAction>(ids.Count);
                    var unresolved = new List<string>();
                    foreach (var id in ids)
                    {
                        var compiled = actionCatalog.Get(id);
                        if (compiled is null) unresolved.Add(id);
                        else list.Add(compiled);
                    }

                    if (unresolved.Count > 0)
                    {
                        Warnings.Add(
                            $"Actor '{a.Setup.Key}' has equipped action id(s) [{string.Join(", ", unresolved)}] " +
                            "not in the supplied ActionCatalog; falling back to the basic attack.");
                        held = new[] { BasicAttackCompiled };
                    }
                    else
                    {
                        list.Sort(ActionTagPreference.Compare);
                        held = list;
                    }
                }

                _heldActions[a.Setup.Key] = held;
                BindContainers(a, held, containerResolver);
            }
        }

        /// <summary>
        /// A18a (spec-action-container-binding.md §2): bind each held action's real atom container,
        /// once, alongside the loadout compile above — never per decision, matching the same
        /// "compile once" discipline A17's own loadout loop already established. A `ContainerId` a
        /// non-empty loadout resolves to (basic attack's own `""` always skips) MUST resolve against a
        /// real, supplied resolver — loud failure on a missing container or an empty result, never a
        /// silent skip, matching this codebase's standing "loud validation over silent corruption"
        /// stance (the same shape the `ActionCatalog` check just above already uses).
        /// </summary>
        void BindContainers(ActorState a, IReadOnlyList<CompiledAction> held, IContainerEffectResolver? containerResolver)
        {
            foreach (var action in held)
            {
                if (string.IsNullOrEmpty(action.ContainerId)) continue;

                if (containerResolver is null)
                    throw new ArgumentException(
                        $"Actor '{a.Setup.Key}' holds action '{action.ActionId}' with container '{action.ContainerId}' but no IContainerEffectResolver was supplied to resolve it.",
                        nameof(containerResolver));

                var effectIds = containerResolver.EffectIdsFor(action.ContainerId);
                if (effectIds.Count == 0)
                    throw new ArgumentException(
                        $"Actor '{a.Setup.Key}' holds action '{action.ActionId}' with container '{action.ContainerId}', which the supplied IContainerEffectResolver could not resolve.",
                        nameof(containerResolver));

                foreach (var effectId in effectIds)
                {
                    Host.Bag.Grant(new Contracts.EffectGrantDto
                    {
                        GrantId = $"battle:{a.Setup.Key}:{action.ActionId}:{effectId}",
                        EffectId = effectId,
                        OwnerKind = "entity",
                        OwnerKey = Contracts.EffectOwnerKeys.Entity(a.Setup.Key),
                        PluginId = "battle",
                        Priority = 0,
                    });
                }
            }
        }

        // ---- IBattleView (A17): the read seam StubIntentSource is confined to — never a direct
        // read of Actors/ByKey from outside this class. PositionOf is always null (no board exists
        // yet), which is what makes NearestEnemy's own SourceOrder fallback the live behavior today.
        public IReadOnlyList<string> LiveActorKeys
        {
            get
            {
                var live = new List<string>(Actors.Count);
                foreach (var a in Actors) if (a.Active) live.Add(a.Setup.Key);
                return live;
            }
        }

        public int SideOf(string actorKey) => ByKey[actorKey].Setup.Side == "squad" ? 0 : 1;

        public GridPos? PositionOf(string actorKey) => null;

        public EntityFacts FactsOf(string actorKey)
        {
            var a = ByKey[actorKey];
            var hpMilli = a.MaxHp > 0 ? (int)Math.Clamp(a.Hp * 1000 / a.MaxHp, 0, 1000) : 0;
            var elementId = a.Setup.ElementPrimary is { } e ? (int)e : 0;
            return new EntityFacts(
                Side: SideOf(actorKey), TypeId: a.Setup.TypeId, HpMilli: hpMilli, ElementId: elementId,
                Row: 0, Col: 0, IsMindControlled: false, IsKiller: false, StatusMask: 0);
        }

        public IReadOnlyList<CompiledAction> HeldActionsOf(string actorKey) =>
            _heldActions.TryGetValue(actorKey, out var held) ? held : Array.Empty<CompiledAction>();

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
                // MUST mirror StatusRuntime.Tick's own eligibility gate exactly — a status whose
                // Kind isn't OverTime/Contagion never advances its NextPulse there (e.g. `butter`,
                // a pure crowd-control status, is StatusKind.UnityCc with PeriodMs > 0 purely as an
                // artifact of the shared authoring shape). Filtering only on PeriodMs > 0 without
                // this check schedules a pulse Tick() will never actually fire — NextPulse never
                // moves, this method keeps returning the same stuck tick, and the round loop spins
                // forever rescheduling it. A real incident, not a hypothetical: this is the exact
                // bug BasicAttackHazardTests.Hazard2 (a `butter`-CC'd actor) caught during B16.
                if (inst.Kind != StatusKind.OverTime && inst.Kind != StatusKind.Contagion) continue;
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
        /// <summary>
        /// Wave E1 — the attacker's on-hit riders, applied to the actor it just LANDED a hit on.
        ///
        /// <para><b>Byte-identical when nobody has riders</b>, and structurally rather than luckily:
        /// the method returns before touching any RNG for an empty list, so the `riders` stream is
        /// never drawn from and no other stream is perturbed. That is the wave's zero-rider invariant.</para>
        ///
        /// <para>Riders carry the ATTACKER, unlike the t0 initial statuses which land attacker-less —
        /// so resist and potency evaluate against real attacker context, which is the point of applying
        /// a status on a hit rather than at setup. The chance roll is the rider's own
        /// `GrantChanceMilli`, drawn from the dedicated stream; the L2b evaluator still independently
        /// blocks on immunity and the potency floor, exactly as it does for scripted statuses.</para>
        /// </summary>
        void ApplyOnHitRiders(ActorState attacker, ActorState target)
        {
            foreach (var traitId in attacker.Setup.TraitIds)
            foreach (var spec in TraitBattleCatalog.Get(traitId).OnHitRiders)
            {
                var roll = RidersRng.NextPerMille();
                Trace?.Draw("riders", roll);
                if (roll >= spec.GrantChanceMilli) continue;

                Status.Apply(new StatusApplyInput(
                    spec.StatusId,
                    HostPtr: target.Setup.Key,
                    AttackerPtr: attacker.Setup.Key,
                    GrantId: "battle:rider:" + attacker.Setup.Key + ":" + spec.StatusId,
                    BaseMagnitude: spec.MagnitudePerPulse,
                    BaseDuration: spec.DurationMs,
                    PeriodMs: spec.PeriodMs,
                    DurationMs: spec.DurationMs,
                    // Already rolled on the riders stream above; the evaluator must not roll a SECOND
                    // time on the status stream, which would both double-gate the rider and consume a
                    // draw that belongs to contagion.
                    GrantChance: 1.0,
                    EffectId: "battle.rider." + spec.StatusId,
                    PluginId: "battle",
                    // Attacker-ful, unlike the t0 initial statuses: the whole point of a rider is that
                    // the attacker's potency meets the defender's resist.
                    AttackerLess: false),
                    // The chance was already decided above on the riders stream, so the evaluator is
                    // handed a scripted 0.0 -- the same FixedStatusRng the scripted setup path uses,
                    // and for the same reason: one roll per decision, on the stream that owns it.
                    RiderApplyRng, Host.Clock.UtcNow);
            }
        }

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

            ApplyOnHitRiders(attacker, target);

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
