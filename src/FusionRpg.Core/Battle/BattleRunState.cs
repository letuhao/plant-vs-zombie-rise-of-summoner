using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Cost;
using FusionRpg.Core.Actions.Unlock;
using FusionRpg.Core.Battle.Board;
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
        ///
        /// <para><b>battle-tempo `action-timing` (2026-09-05):</b> this field moved from `static
        /// readonly` to an ordinary instance field, computed once per `BattleRunState` (i.e. once per
        /// battle) rather than once per process — the ONLY change that made room for
        /// <see cref="ActionTimingDerivation.DeriveBasicAttack"/> to read
        /// <see cref="ActionTimingPolicy.Tuning"/> here: a `static readonly` initializer runs at first
        /// type touch, which could race host startup's `ActionTimingPolicy.Configure` call and throw
        /// for any caller that reaches `BattleRunState` first. An instance initializer runs during
        /// `new BattleRunState(...)`, by which point every real caller (`BattleEngine.Resolve`) has
        /// already configured tuning — the same timing every other `Policy`/`Tuning` read in this
        /// class already assumes.</para>
        /// </summary>
        readonly CompiledAction BasicAttackCompiled = new(
            ActionId: BasicAttackEnvelope.ActionId,
            Kind: ActionKind.Basic,
            Rung: 0,
            Tags: new[] { ActionTag.Offensive },
            Enabled: true,
            Revision: 0,
            Grantable: false,
            DefaultAttackEligible: true,
            ContainerId: "",
            Envelope: ActionTimingDerivation.DeriveBasicAttack(BasicAttackEnvelope, ActionTimingPolicy.Tuning),
            Targeting: TargetSpecCompiler.Compile(BasicAttackTargeting),
            MinRange: 0,
            MaxRange: int.MaxValue,
            RangeChannel: null,
            RequiresLineOfSight: false,
            Condition: PredicateCompiler.Always,
            Costs: Array.Empty<CompiledActionCost>(),
            Scopes: Array.Empty<ActionScopeRow>());

        /// <summary>`battle-tempo` `timeline-dispatch` (D14): the one field of
        /// <see cref="BasicAttackCompiled"/> the timeline-dispatch action phase needs (the derived
        /// wind-up/recovery envelope) — exposed narrowly rather than widening the whole field's
        /// visibility, since nothing else outside this class needs the rest of it (targeting, costs,
        /// scopes) today.</summary>
        public Timeline.ActionEnvelope BasicAttackEnvelopeCompiled => BasicAttackCompiled.Envelope;

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

        /// <summary>D4.6 (spec-wild-room.md §5): `act.capture`'s own stream, "the battle's own seed,
        /// never a second RNG" — one line beside `EssenceRng`/`RidersRng`, same reasoning: capture's
        /// content (seal tiers, status bonuses) must not butterfly any other system's rolls.</summary>
        public readonly SeededRng CaptureRng;

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

        /// <summary>D4.6 (spec-wild-room.md §5): "each failed attempt on the same target shifts [the
        /// delta band] toward `far-above`… kept in a per-battle `CaptureAttempts` ledger beside
        /// `CooldownLedger`." Keyed on the target alone (`attempts(target)` in the spec's own
        /// formula takes one argument) — `CooldownLedger`'s own compound actor×slot key is a
        /// different shape for a different question, not a template here.</summary>
        public readonly Dictionary<string, int> CaptureAttempts = new(StringComparer.Ordinal);

        /// <summary>`battle-tempo` `reaction-lane` RL2: one registry per battle, mirroring
        /// `Cooldowns`' own "fresh per battle, not per actor construction elsewhere" shape. Reuses
        /// `LawnActorResourcePools` verbatim rather than a near-duplicate type — its own mechanism
        /// (a `Dictionary&lt;ptr, ActorResourcePools&gt;`, lazily filled, full at first access) carries
        /// no lawn-specific logic despite the name; a battle actor is exactly the second consumer its
        /// own doc comment already anticipates ("Unity-free by construction"). No faction branch: a
        /// `wave` actor gets a pool exactly like a `squad` actor does — resource-hub-ssot.md's own
        /// rule ("one shared set... faction difference is a display label, never a branch").</summary>
        public readonly LawnActorResourcePools ResourcePools = new();

        /// <summary>A19 (spec-action-costs-cooldowns-adoption.md T56.1): whichever tick the caller
        /// currently considers "now" — `BasicAttack.cs`'s functions and `TimelineDispatch`'s
        /// `RunTimelineActionPhase` each compute a tick locally with no single shared read `CostLedger`
        /// (a real, previously-unbuilt dependency) can point its own `Func&lt;long&gt; nowTick` at, so
        /// every call site that already threads a tick locally also assigns it here first. Mutable by
        /// design — this is a live pointer into "what tick is it right now for this battle", not a
        /// snapshot; unlike `T0` (fixed at construction), it moves every time a caller advances.</summary>
        public long NowTick;

        /// <summary>A19 (T56.1): the real, first production `CostLedger` in this repo — grep-confirmed
        /// zero prior construction sites anywhere in `src/`. Built once here, not per-call, mirroring
        /// `Cooldowns`/`ResourcePools`' own "one instance for the whole battle" shape. `costsByActionId`
        /// adapts `CompiledAction.Costs` (`CompiledActionCost`) into `ActionCostRow` — structurally
        /// compatible fields, different types, because the compiled and authored shapes serve different
        /// callers (`ActionCostRow` also carries `AllowLethal`, which a compiled cost's own consumer
        /// never needed until now). Empty when `actionCatalog` is null — vacuously affordable, the same
        /// "an action with no cost table is unaffected" additive discipline this program uses everywhere.</summary>
        public readonly CostLedger CostLedger;

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

        /// <summary>passive-tree G2 (spec-mechanism-wiring.md §4.2): the per-round half of the recompose
        /// seam. `RecomposeDerived` above stays explicit/per-actor for a live toggle event (aura-skill
        /// T13's own job); this is the one new call site `BattleEngine.Resolve`'s round loop makes, once
        /// per actor, at the start of every `RoundEventKind` — so a mechanism that changed
        /// `DerivedLedger` after construction (a status applied mid-battle, a gate quantity crossing a
        /// threshold) is composed into `Derived` before the round's regen/initiative/attacks read it,
        /// rather than only ever being visible in the NEXT battle. Provably safe to call every round
        /// even when nothing changed: `BattleDerivedModifierLedger.Recompose` always rebuilds from
        /// `BaseDerived`, never from `Derived`'s own prior value, so repeated calls with an unchanged
        /// ledger are byte-identical no-ops (`BattleDerivedModifierLedgerTests.An_empty_ledger_recomposes_nothing`).</summary>
        public void RecomposeDerivedForAllActors()
        {
            foreach (var a in Actors)
                RecomposeDerived(a.Setup.Key);
        }

        readonly List<ShieldEventRec> _shieldEventScratch = new();
        readonly Dictionary<string, IReadOnlyList<CompiledAction>> _heldActions = new(StringComparer.Ordinal);

        /// <summary>aura-skill T3 (audit D3): equipped-action ids that could not be resolved against
        /// the supplied <see cref="ActionCatalog"/> — the actor degrades to the basic-attack fallback
        /// instead of failing the whole battle. Empty on every setup a golden has ever blessed.</summary>
        public readonly List<string> Warnings = new();

        /// <summary>
        /// base-defense siege-board (spec-siege-board.md §4): null for every caller that does not
        /// supply one, which is every caller until siege-resolver. This is what keeps the module
        /// golden-free — a field nothing sets changes no serialized bytes and no code path.
        /// </summary>
        readonly BoardState? _board;

        /// <summary>base-defense `siege-positions` §3: null for every caller without a board (every
        /// caller until this module wires siege battles through one) — the value `BattleEngine.Resolve`'s
        /// round loop passes to `Status.Tick`'s own optional trailing `board` parameter.</summary>
        public Combat.BoardSnapshot? CombatBoardSnapshot { get; }

        /// <summary>A22 (spec-action-resolution-by-category.md §1): the constructor already received
        /// this — captured only inside the `rungOf` closure below (`:493`), never kept as a field.
        /// `ApplyBasicAttack` needs it too, to resolve `envelope.ActionId`'s own `Category` and branch
        /// resolution shape — the reason this module exists at all.</summary>
        public ActionCatalog? ActionCatalog { get; }

        public BattleRunState(BattleSetup setup, ulong seed, Timeline.BattleTrace? trace,
            Action<BattleEffectHost>? onEffectHostReady, ActionCatalog? actionCatalog = null,
            IContainerEffectResolver? containerResolver = null, BoardState? board = null,
            Func<string, UnlockState>? unlockStateFor = null, UnlockTuning? unlockTuning = null,
            IReadOnlyList<RunnerBinding>? runnerBindings = null,
            IReadOnlySet<string>? containersWithRunnerCoverage = null)
        {
            Trace = trace;
            _board = board;
            ActionCatalog = actionCatalog;

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
            CaptureRng = SeededRng.DeriveStream(seed, "capture");
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

            // A25 (battle-runner-path-integration): built here, inside the constructor, rather than
            // via `onEffectHostReady` -- that callback only receives `Host`, and the Secondary
            // runner's own `nowMs` reader must close over THIS instance's own `NowTick` field (a
            // frozen clock silently breaks every ICD gate, see UseRunner's own doc comment). `this` is
            // valid throughout a constructor body, so the closure below is correct despite `NowTick`
            // not yet having a meaningful value at construction time -- it is read lazily, per event,
            // never at wiring time.
            if (runnerBindings is { Count: > 0 })
                Host.UseRunner(runnerBindings, seed, () => NowTick);
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

            // base-defense `siege-positions` §2-3: assigned once, only for a battle that HAS a board
            // (whoever constructs the BoardState is responsible for having placed actors onto it
            // before calling BattleEngine.Resolve — this module does not place them itself, see
            // Board/Placement.cs's own doc comment for why). `Host.Bag.BoardSnapshot` is left at its
            // default `BoardSnapshot.Empty` otherwise, which is every existing caller's exact current
            // behaviour (this line does not even run for them). `CombatBoardSnapshot` is the SAME
            // value but genuinely nullable (Empty is not null) — §3's `Status.Tick` needs true `null`
            // for "no board", not an empty-but-non-null snapshot, to stay byte-identical for every
            // existing battle.
            if (_board is not null)
            {
                CombatBoardSnapshot = Board.BoardSnapshotAdapter.ToCombatSnapshot(this);
                Host.Bag.BoardSnapshot = CombatBoardSnapshot;
            }

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

            // G2 (spec-mechanism-wiring.md §4.2): forwards straight to DerivedLedger.Add, the one
            // write this ledger has — a live mid-battle trigger (T13's still-unbuilt aura toggle, or a
            // test simulating one) calls this through `onEffectHostReady` the same way every other
            // Battle-adoption trigger reaches this host's own collaborators.
            Host.AddDerivedContribution = DerivedLedger.Add;
            onEffectHostReady?.Invoke(Host);

            // passive-tree G1 (spec-gate-counters.md §7 P1, R9): the pulse site — every status
            // DoT/HoT delta Status.Tick delivers reaches HP through exactly this sink, so this is the
            // one call P1's defaulted `origin` parameter existed for. Deferred while BattleEngine.cs/
            // BattleRunState.cs were under another session's concurrent edit; both are clean now.
            PulseSink = new BattlePulseSink((hostPtr, amount, effectId, components) =>
                ByKey.TryGetValue(hostPtr, out var owner)
                    ? ApplyHp(owner, amount, effectId, components, origin: DamageOrigin.StatusPulse)
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
            var costsByActionId = new Dictionary<string, IReadOnlyList<ActionCostRow>>(StringComparer.Ordinal);
            foreach (var a in Actors)
            {
                var ids = a.Setup.EquippedActionIds;
                IReadOnlyList<CompiledAction> held;
                if (a.Setup.Kind == CombatantKind.Structure && (ids is null || ids.Count == 0))
                {
                    // base-defense `combatant-kind` §5: a structure with no actions has nothing to do
                    // — it does not fall back to a basic attack. The fallback below exists so an
                    // ANIMATE actor is never inert; a wall being inert is the point. This also keeps
                    // "garrisoning a wall grants nothing" true in HeldActionsOf's union below, since
                    // the empty list is what gets lent.
                    held = Array.Empty<CompiledAction>();
                }
                else if (ids is null || ids.Count == 0)
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
                BindContainers(a, held, containerResolver, containersWithRunnerCoverage);

                // A19 (T56.1): collect real cost rows for every action actually reachable in THIS
                // battle -- ActionCatalog exposes no "all actions" enumerator (only Get(id)/Count),
                // so building from each actor's own resolved `held` list is both sufficient (nothing
                // outside a held loadout can ever be committed) and simpler than inventing one.
                foreach (var action in held)
                {
                    if (action.Costs.Count == 0 || costsByActionId.ContainsKey(action.ActionId)) continue;
                    var rows = new List<ActionCostRow>(action.Costs.Count);
                    foreach (var cost in action.Costs)
                        rows.Add(new ActionCostRow(action.ActionId, cost.ResourceId, cost.ScaledAmount, cost.When));
                    costsByActionId[action.ActionId] = rows;
                }
            }

            CostLedger = new CostLedger(
                costsByActionId,
                poolsFor: key => ResourcePools.GetOrCreate(key, ByKey[key].Derived, NowTick),
                derivedFor: key => ByKey[key].Derived,
                rungOf: (actorKey, actionId) =>
                    EffectiveRungOf(actorKey, actionId, actionCatalog, unlockStateFor, unlockTuning),
                nowTick: () => NowTick);
        }

        /// <summary>
        /// A23 (spec-cost-scaling-holder-rung.md §2): `CostLedger` must scale by the HOLDER's
        /// `effectiveRung` (progression-derived), never the content's authored `Rung`
        /// (spec-rung-semantics.md §3.1 — `StructureBudgetGuard` is the reader that wants the authored
        /// value; this ledger is not). A match in the actor's own <see cref="UnlockState.Held"/> list
        /// resolves through <see cref="UnlockLadder.EffectiveRung"/>; no match (every intrinsic/basic
        /// action, and every caller that supplies no <paramref name="unlockStateFor"/> at all — the
        /// exact byte-identical-to-today default) falls back to the authored `Rung`, unchanged.
        /// </summary>
        static int EffectiveRungOf(string actorKey, string actionId, ActionCatalog? actionCatalog,
            Func<string, UnlockState>? unlockStateFor, UnlockTuning? unlockTuning)
        {
            var state = unlockStateFor?.Invoke(actorKey);
            if (state is not null && unlockTuning is not null)
            {
                foreach (var held in state.Held)
                {
                    if (held.UnlockId == actionId)
                        return UnlockLadder.EffectiveRung(held.EarnCountAtAcceptance, unlockTuning).Value;
                }
            }

            return actionCatalog?.Get(actionId)?.Rung ?? 0;
        }

        /// <summary>
        /// A18a (spec-action-container-binding.md §2): bind each held action's real atom container,
        /// once, alongside the loadout compile above — never per decision, matching the same
        /// "compile once" discipline A17's own loadout loop already established. A `ContainerId` a
        /// non-empty loadout resolves to (basic attack's own `""` always skips) MUST resolve against a
        /// real, supplied resolver — loud failure on a missing container or an empty result, never a
        /// silent skip, matching this codebase's standing "loud validation over silent corruption"
        /// stance (the same shape the `ActionCatalog` check just above already uses).
        ///
        /// <para>A25 (battle-runner-path-integration): a container whose atoms are ENTIRELY
        /// Runner-path has, correctly, zero Compiled-path effect ids — `IContainerEffectResolver`'s own
        /// contract never covered the Runner path (A18a scoped it to Defs only). Found empirically, not
        /// designed for up front: a real end-to-end test with a purely-Runner-path action threw here
        /// even though `Host.Runner` was correctly wired, because this check could not tell "genuinely
        /// unresolvable" apart from "resolved elsewhere, via the runner." <paramref
        /// name="containersWithRunnerCoverage"/> (built the same pass as `runnerBindings`, from the
        /// SAME per-container loop, never string-parsed back out of a binding id) is what makes that
        /// distinction — a container in this set is real, known content, just not Compiled-path at
        /// all, so the throw below no longer fires for it.</para>
        /// </summary>
        void BindContainers(ActorState a, IReadOnlyList<CompiledAction> held, IContainerEffectResolver? containerResolver,
            IReadOnlySet<string>? containersWithRunnerCoverage)
        {
            foreach (var action in held)
            {
                if (string.IsNullOrEmpty(action.ContainerId)) continue;

                if (containerResolver is null && containersWithRunnerCoverage?.Contains(action.ContainerId) != true)
                    throw new ArgumentException(
                        $"Actor '{a.Setup.Key}' holds action '{action.ActionId}' with container '{action.ContainerId}' but no IContainerEffectResolver was supplied to resolve it.",
                        nameof(containerResolver));

                var effectIds = containerResolver?.EffectIdsFor(action.ContainerId) ?? Array.Empty<string>();
                if (effectIds.Count == 0 && containersWithRunnerCoverage?.Contains(action.ContainerId) != true)
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
        // read of Actors/ByKey from outside this class. PositionOf is null with no board (every
        // caller until siege-resolver), which is what makes NearestEnemy's own SourceOrder fallback
        // the live behavior today; a real board makes it return real positions (siege-board §4).
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

        public GridPos? PositionOf(string actorKey) =>
            _board is null ? null : _board.Positions.TryGetValue(actorKey, out var p) ? p : null;

        public EntityFacts FactsOf(string actorKey)
        {
            var a = ByKey[actorKey];
            var hpMilli = a.MaxHp > 0 ? (int)Math.Clamp(a.Hp * 1000 / a.MaxHp, 0, 1000) : 0;
            var elementId = a.Setup.ElementPrimary is { } e ? (int)e : 0;
            return new EntityFacts(
                Side: SideOf(actorKey), TypeId: a.Setup.TypeId, HpMilli: hpMilli, ElementId: elementId,
                Row: 0, Col: 0, IsMindControlled: false, IsKiller: false, StatusMask: 0);
        }

        /// <summary>
        /// base-defense `combatant-kind` §4: a garrisoned structure lends its actions to its occupant —
        /// the union of the occupant's own held actions and the structure it currently occupies, found
        /// by a linear scan of <see cref="Actors"/> for the one whose <c>GarrisonedBy</c> names this
        /// key (small counts, the same discipline <c>FindAdjacentWithTrait</c> already uses; there is
        /// no reverse index and none is needed at this scale). Byte-identical for every existing
        /// battle: <c>GarrisonedBy</c> is null on every actor there, so the scan never matches and this
        /// always returns exactly <c>own</c>.
        /// </summary>
        public IReadOnlyList<CompiledAction> HeldActionsOf(string actorKey)
        {
            var own = _heldActions.TryGetValue(actorKey, out var held) ? held : Array.Empty<CompiledAction>();
            var garrisonedStructureKey = FindGarrisonedStructureKey(actorKey);
            if (garrisonedStructureKey is null) return own;

            var lent = _heldActions.TryGetValue(garrisonedStructureKey, out var structureHeld)
                ? structureHeld : Array.Empty<CompiledAction>();
            if (lent.Count == 0) return own;
            var union = new List<CompiledAction>(own.Count + lent.Count);
            union.AddRange(own);
            union.AddRange(lent);
            return union;
        }

        /// <summary>
        /// base-defense `siege-ai` 17.9 (spec-siege-ai.md §5.20 rule 5): the SAME "which structure names
        /// this actor as its garrison" scan <see cref="HeldActionsOf"/> already performs, shared rather
        /// than duplicated — <see cref="IBattleView.GarrisonedStructureKeyOf"/>'s own real implementation.
        /// Returns the garrisoned structure's key, or `null` when `actorKey` garrisons nothing (every
        /// existing battle: `GarrisonedBy` is null on every actor, so this always returns `null`).
        /// </summary>
        string? FindGarrisonedStructureKey(string actorKey)
        {
            foreach (var a in Actors)
            {
                if (a.Setup.Kind != CombatantKind.Structure || a.Setup.GarrisonedBy != actorKey) continue;
                return a.Setup.Key;
            }
            return null;
        }

        public string? GarrisonedStructureKeyOf(string actorKey) => FindGarrisonedStructureKey(actorKey);

        /// <summary>base-defense `siege-ai` (module 17): the SAME `Derived` snapshot `CostLedger`'s own
        /// `poolsFor`/`derivedFor` callbacks already read from `ByKey[key].Derived` (`:500-501` above) —
        /// exposed here, never recomputed, so a live scoring AI's hit-chance estimate reads the exact
        /// stats every other system in this class already does.</summary>
        public FusionRpg.Core.Stats.Derived.ActorDerivedSnapshot? DerivedOf(string actorKey) =>
            ByKey.TryGetValue(actorKey, out var a) ? a.Derived : null;

        public DamageApplyResult ApplyHp(
            ActorState owner, long amount, string effectId,
            ElementPayloadComponent[]? components = null, ActorState? attacker = null, string? grantId = null,
            DamageOrigin origin = DamageOrigin.DirectHit)
        {
            var result = DamageApplyPipeline.Apply(
                owner.Setup.Key, amount, hitCount: 1,
                components ?? Array.Empty<ElementPayloadComponent>(),
                attacker?.Derived, owner.Derived, ShieldGate, HpSink,
                pluginId: "battle", effectId: effectId, grantId: grantId, origin: origin);
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

        /// <summary>
        /// party-dungeon D2.10 — lifted out of <see cref="CheckRetreats"/> with **no behaviour
        /// change** (the coward-retreat call site below is byte-identical to what it inlined
        /// before), so it can gain producers beyond the coward trait: a capture (`wild-room`) and a
        /// player-issued retreat (`delve-attrition`) both leave a battle the same way a coward
        /// does — alive, no die event, shields released.
        /// </summary>
        public void Withdraw(ActorState actor)
        {
            actor.Retreated = true;
            Status.WithdrawEntity(actor.Setup.Key);
            Shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(actor.Setup.Key));
        }

        /// <summary>Coward retreat: below the threshold the actor leaves the battle alive (no die event).</summary>
        public void CheckRetreats()
        {
            foreach (var a in Actors)
            {
                if (!a.Active || !a.Has("coward")) continue;
                var def = TraitBattleCatalog.Get("coward");
                if ((long)a.Hp * 1000 < (long)a.MaxHp * def.RetreatBelowMilli)
                    Withdraw(a);
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
        /// base-defense `siege-waves` §3: roster growth — a reinforcement joining mid-battle.
        ///
        /// <para><b>Runs the SAME key validation `Resolve` applies at setup</b> (extracted to
        /// <see cref="ValidateActorKey"/> for exactly this reuse) — a mid-battle actor that bypassed
        /// those checks would be silently unhittable at the shield gate.</para>
        ///
        /// <para><b>Appends, never inserts or reorders</b> — <see cref="Actors"/> is a plain
        /// <see cref="List{T}"/>; an index shift mid-battle would invalidate every in-flight effect
        /// that captured one (a shield grant, a status instance, anything keyed by list position rather
        /// than actor key). <see cref="ActorState.SideIndex"/> for the newcomer is the count of actors
        /// already on its own side — the same 0-based-per-side numbering the constructor's own
        /// <c>Squad.Select((a,i) => ...)</c>/<c>Wave.Select((a,i) => ...)</c> already establish.</para>
        ///
        /// <para><b>Placed on the board only when both a board exists AND a position is supplied</b> —
        /// resolving a district edge into a real candidate cell is `siege-resolver`'s job (a later
        /// module), the same scoping <see cref="Board.Placement"/> already states.</para>
        ///
        /// <para><b>Scoped out, stated rather than silently skipped</b>: unlike the constructor's own
        /// per-actor setup, this method does not apply <see cref="BattleActorSetup.InnateShield"/>,
        /// <see cref="BattleActorSetup.InitialStatuses"/>, active-aura membership, or loadout/container
        /// compilation for the newcomer — none of those are in this task's own stated contract (append,
        /// validate, place, never reorder), and building them against no real caller yet would be
        /// exactly the unrequested surface this program's standing rule warns against. A reinforcement
        /// still fights (it is `Active`/`Alive`/targetable/damageable the moment it is added) — it just
        /// arrives without whatever a fresh setup-time actor would have gotten from those four systems,
        /// until a real caller (`siege-resolver`) needs one of them.</para>
        /// </summary>
        public void AddActor(BattleActorSetup setup, Actions.GridPos? position, int round)
        {
            var seenKeys = new HashSet<string>(ByKey.Keys, StringComparer.Ordinal);
            BattleEngine.ValidateActorKey(setup, seenKeys);

            var sideIndex = Actors.Count(a => a.Setup.Side == setup.Side);
            var actor = new ActorState(setup, sideIndex);
            Actors.Add(actor);
            ByKey[setup.Key] = actor;
            // Round is the actual arrival round, unlike the constructor's own initial-roster loop
            // (which spawns everyone at round 0, correctly, since that IS when they arrive).
            Events.Add(new BattleEventRec(round, BattleEventKinds.Spawn, setup.Key, setup.TypeId, setup.Side));

            if (_board is not null && position is { } p)
                _board.Place(setup.Key, p);
        }

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

    /// <summary>
    /// Test-only seam (matching <c>RpgStore.DiffCommitForTest</c>'s established precedent): constructs
    /// a real <see cref="BattleRunState"/> and returns <c>HeldActionsOf(actorKey)</c>'s action ids
    /// directly. base-defense `combatant-kind` §4's garrison union has no production reader yet —
    /// exactly like the pre-existing loadout-compile mechanism it sits beside
    /// (<c>EquippedActionIdsReportingTests</c>'s own comment: "nothing reads <c>HeldActionsOf</c> for
    /// real behavior") — and <see cref="BattleRunState"/> itself is private/nested per B13's own
    /// deviation note, so this is the only way to prove the mechanism without waiting for
    /// `siege-resolver` to wire a real caller.
    /// </summary>
    internal static IReadOnlyList<string> HeldActionIdsForTest(
        BattleSetup setup, ulong seed, string actorKey, ActionCatalog? actionCatalog = null)
    {
        var state = new BattleRunState(setup, seed, trace: null, onEffectHostReady: null, actionCatalog: actionCatalog);
        return state.HeldActionsOf(actorKey).Select(a => a.ActionId).ToList();
    }

    /// <summary>
    /// Test-only seam, same shape and same reason as <see cref="HeldActionIdsForTest"/>: base-defense
    /// `siege-positions`'s <c>PositionOf</c>/<c>CombatBoardSnapshot</c> live on the private/nested
    /// <see cref="BattleRunState"/>, so this is the only way to prove them without a production caller
    /// (that is `siege-resolver`'s job, a later module) yet threading a board all the way through
    /// <see cref="Resolve"/>.
    /// </summary>
    internal static (GridPos? Position, Combat.BoardSnapshot? Snapshot) PositionAndSnapshotForTest(
        BattleSetup setup, ulong seed, string actorKey, Board.BoardState? board)
    {
        var state = new BattleRunState(setup, seed, trace: null, onEffectHostReady: null, board: board);
        return (state.PositionOf(actorKey), state.CombatBoardSnapshot);
    }
}
