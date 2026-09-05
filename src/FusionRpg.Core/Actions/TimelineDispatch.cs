using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Timeline;

namespace FusionRpg.Core.Battle;

// `battle-tempo` `timeline-dispatch` (D14, spec-timeline-dispatch.md §2.5). Same seam convention
// BasicAttack.cs already established: this file lives under Core/Actions/ but declares part of
// BattleEngine itself (namespace FusionRpg.Core.Battle, `partial`), because the action phase needs
// ActorState, BloodthirstyViewFor and DispatchHit, all private to BattleEngine/BattleRunState.
//
// ⛔ Reached ONLY when `activeProfile.UsesTimelineDispatch` is true. No entry in
// `BattleModeProfileCatalog` sets that flag — every shipped profile (classic-round, galaxy-sync,
// hybrid-atb, siege) takes `BattleEngine.cs`'s existing atomic do-while loop, unchanged, byte-for-byte.
// This method exists so a synthetic, never-catalogued test profile can prove W/Commitment/AdvancePolicy
// move for real, closing `battle-tempo-todo.md` Checkpoint B's own unmet line, without landing anything.

public static partial class BattleEngine
{
    /// <summary>
    /// The event-driven replacement for the atomic do-while action phase: actors commit through
    /// `ActionRunner`, honouring `WindupTicks`/`RecoveryTicks` for real instead of resolving
    /// instantly. Runs on a LOCAL `EventQueue`/`SimulationClock` scoped to this one round's action
    /// phase — never the round's own `roundQueue` — which is what keeps this change from needing to
    /// touch `BattleEngine.cs`'s existing `RoundEventKind`/`StatusPulseEventKind` numbering at all
    /// (spec-timeline-dispatch.md's original §2.4 Hazard A only exists on a SHARED queue).
    ///
    /// <para><b>Scope this deliberately does not generalize past.</b> An action's full commit → wind-up
    /// → resolve → recovery lifecycle is assumed to fit inside one round — true for the basic attack at
    /// today's configured 150+50=200 ticks against a 1000ms round (spec §2.4's original Hazard B,
    /// narrowed rather than solved). If that assumption is ever violated (a future action authored with
    /// a longer lifecycle), the structural guard below throws rather than silently corrupting slot
    /// state or leaking a run into the next round.</para>
    ///
    /// <para><b>Re-selection reuses the SAME `IIntentSource` the commit itself used</b>, rather than
    /// routing through `BasicAttackCompiled.Targeting`'s `CompiledTargetSpec` (the wire-format contract
    /// D6/D11 named as the intended long-term seam for a general action). For the basic attack
    /// specifically, `StubIntentSource.TryDeclare` already reads live battle state on every call, so
    /// calling it again IS a correct re-selection — a real second seam for a general action's
    /// authored targeting spec remains open work, out of this method's scope.</para>
    /// </summary>
    static void RunTimelineActionPhase(
        BattleRunState state, Timeline.BattleModeProfile activeProfile, List<ActorState> order,
        Timeline.ITurnEconomy economy, Func<ActorState, string> economyKey, DateTimeOffset now,
        int rounds, Timeline.BattleTrace? trace, Timeline.IIntentSource? intentSource,
        Timeline.ReactionLane reactionLane, Timeline.SimulationClock localClock)
    {
        // `localClock` is a PER-BATTLE clock (constructed once in `BattleEngine.Resolve`, alongside
        // `Cooldowns`/`ResourcePools`/`reactionLane`) that never resets between calls — a real bug,
        // found by LAND1's own staged sweep against real content (not caught by any synthetic probe
        // this session): `Cooldowns` and `ResourcePools` are BOTH per-battle-persistent state that
        // assumes monotonically increasing ticks, but a per-ROUND-fresh clock would hand them a tick
        // sequence that goes backwards every round boundary (`ResourcePoolState.Resolve` threw
        // "nowTick precedes LastTick" the moment a real multi-round battle exercised a reaction).
        // `localQueue`/`slots`/`runner` stay round-scoped below (safe: the drain loop always empties
        // the queue completely before this method returns, every call, so nothing outlives one round
        // regardless of whether the queue itself is fresh or persistent) — only the clock feeding
        // per-battle state needed to stop resetting.
        var localQueue = new Timeline.EventQueue(expectedEvents: Math.Max(4, order.Count * 2));
        var localAdvance = new Timeline.NextEventAdvance();
        var slots = new Timeline.ActionSlots(activeProfile.W, activeProfile.WScope);

        var machines = new Dictionary<string, Timeline.ActorTurnMachine>(order.Count, StringComparer.Ordinal);
        foreach (var a in order) machines[a.Setup.Key] = state.MachineFor(a.Setup.Key);

        string? Reselect(string actorKey, string? deadTargetKey)
        {
            var attacker = state.ByKey[actorKey];
            var view = BloodthirstyViewFor(state, attacker);
            var source = intentSource
                ?? new StubIntentSource(view, state.Cooldowns, NoStanceHeld.Instance, AlwaysAffordable.Instance);
            var intent = source.TryDeclare(actorKey, localClock.Now);
            return intent.IsNone ? null : intent.TargetKey;
        }

        var runner = new Timeline.ActionRunner(
            localQueue, slots, state.Cooldowns,
            key => state.ByKey.TryGetValue(key, out var a) && a.Active,
            activeProfile.DefaultCommitment,
            Reselect);

        var phaseBroken = false;

        bool TryCommitReady(ActorState attacker)
        {
            var machine = machines[attacker.Setup.Key];
            if (!attacker.Active) return false;
            if (machine.State != Timeline.TurnState.Charging) return false;

            // ⛔ SLOT AVAILABILITY IS CHECKED BEFORE THE ECONOMY GATE. This ordering is the fix for a
            // real starvation defect, measured 2026-09-05 while sweeping `classic-round` for `LAND1`:
            //
            // The economy gate below SPENDS the actor's action for the turn, and the no-slot branch
            // further down deliberately does not refund it. At `W = 4` (hybrid-atb) there are usually
            // enough slots that everyone commits on the first pass, so it almost never bit. At
            // `W = 1` (classic-round) it bit every round: actor A takes the only slot, actor B spends
            // its whole turn's economy, fails to commit, returns to Charging -- and when the slot
            // frees, `economy.TryAcquire` now refuses because B already spent this turn. B starves.
            //
            // Symptoms that traced back here, all three fixed by this one line: a battle that should
            // end `Victory` ended `Stalemate` (starved actors deal no damage, so the chip floor never
            // got the chance to prevent a zero-damage stall), an actor never reached
            // `Ready->Committed` and bounced `Ready->Charging`, and the crit RNG stream stopped
            // advancing on every swing (there were fewer swings).
            //
            // The old comment justified spending unconditionally by pointing at the atomic path doing
            // the same. That justification does not hold: the atomic path has NO slot contention --
            // `RunBasicAttackStep` always proceeds once the economy is taken -- so it never reaches
            // the "paid but could not commit" state this path can. Checking first costs nothing and
            // keeps a slot-starved actor's turn intact so it can contend again when a slot frees.
            if (!slots.HasFreeSlot(attacker.Setup.Side)) return false;

            // B38's own discipline, unchanged: the economy gate comes before any resource is taken.
            if (!economy.TryAcquire(economyKey(attacker), 1, localClock.Now)) return false;

            machine.TransitionTo(Timeline.TurnState.Ready);
            trace?.Turn(rounds, attacker.Setup.Key, Timeline.TurnState.Charging, Timeline.TurnState.Ready);

            var (outcome, target, envelope) = DeclareBasicAttack(
                attacker, state, now, localClock.Now, trace, rounds, intentSource);

            if (outcome != AttackStepOutcome.Proceed)
            {
                // No legal target (Break) or CC-locked/inactive (Continue) -- the economy slot this
                // attempt spent is NOT refunded, matching the atomic path's own identical behaviour
                // (its `economy.TryAcquire` runs before `RunBasicAttackStep`, unconditionally too).
                machine.TransitionTo(Timeline.TurnState.Charging);
                trace?.Turn(rounds, attacker.Setup.Key, Timeline.TurnState.Ready, Timeline.TurnState.Charging);
                if (outcome == AttackStepOutcome.Break) phaseBroken = true; // hazard 3, same as the atomic path
                return false;
            }

            var intent = new Timeline.ActionIntent(envelope.ActionId, target!.Setup.Key, envelope);
            if (runner.TryCommit(machine, attacker.Setup.Side, intent, localClock.Now) != Timeline.CommitRefusal.None)
            {
                // No slot / on cooldown -- give the turn back so it can contend again once something
                // frees; DeclareBasicAttack's own side effects (OnActivate, trace.Target) already fired
                // for the pre-existing atomic path in this exact situation too (it declares intent
                // before slots.TryAcquire), so this is not a new double-fire hazard.
                machine.TransitionTo(Timeline.TurnState.Charging);
                trace?.Turn(rounds, attacker.Setup.Key, Timeline.TurnState.Ready, Timeline.TurnState.Charging);
                return false;
            }

            trace?.Turn(rounds, attacker.Setup.Key, Timeline.TurnState.Ready, Timeline.TurnState.Committed);
            return true;
        }

        // Structural cap on ONE round's local action phase -- exempt per tunables-ssot.md
        // ("per-frame/runtime caps"), never a balance number. A genuinely malformed envelope already
        // throws inside ActionRunner.TryCommit's own ValidateOffsets before ever reaching this guard.
        const int MaxLocalIterations = 10_000;
        var localGuard = 0;
        var buffer = new List<Timeline.ScheduledEvent>(Math.Max(4, order.Count));

        while (localGuard++ < MaxLocalIterations)
        {
            if (!phaseBroken)
                foreach (var attacker in order)
                    TryCommitReady(attacker);

            var due = localQueue.PeekDueTick();
            if (due is null) break; // nothing pending, nothing just committed -- the phase is over

            localClock.TryAdvance(localAdvance, localQueue);
            buffer.Clear();
            localQueue.PopDue(localClock.Now, buffer);

            foreach (var ev in buffer)
            {
                var actor = state.ByKey[ev.OwnerKey];
                var machine = machines[ev.OwnerKey];

                if ((Timeline.TimelineEventKind)ev.Kind == Timeline.TimelineEventKind.Resolve)
                {
                    trace?.Turn(rounds, ev.OwnerKey, Timeline.TurnState.Committed, Timeline.TurnState.Resolving);
                    if (runner.OnResolveDue(machine, ev) == Timeline.ActionOutcome.Resolved)
                    {
                        var targetKey = runner.CurrentTarget(ev.OwnerKey)!;
                        var target = state.ByKey[targetKey];

                        // `battle-tempo` `reaction-lane` RL2: the defender reacts to the triggering
                        // RESOLUTION itself — independent of whether the ensuing hit below actually
                        // lands — matching ReactionLane's own doc ("can still react to a triggering
                        // resolution... resolves INSIDE the triggering resolution"). Decision 12: the
                        // counter does not negate the incoming hit, it retaliates alongside it — no
                        // branch here skips ApplyBasicAttack below. `WReact = 0` (every shipped
                        // profile) makes TryEnter always refuse with NoLane, so this is inert wherever
                        // it is inert today.
                        if (target.Active && reactionLane.TryEnter(target.Setup.Key, target.Setup.Side, trace) == Timeline.ReactionOutcome.Entered)
                        {
                            var pools = state.ResourcePools.GetOrCreate(target.Setup.Key, target.Derived, localClock.Now);
                            var (committed, damage) = Timeline.ReactionCounter.TryCounter(
                                pools, Timeline.ReactionLanePolicy.Tuning.PoiseSpend,
                                Timeline.ReactionLanePolicy.Tuning.RiposteShareCapMilli, localClock.Now, target.Derived);
                            if (committed && damage > 0)
                            {
                                state.ApplyHp(actor, -damage, "battle.reaction.counter", attacker: target);
                                trace?.Apply(rounds, actor.Setup.Key, -damage);
                            }
                            reactionLane.Exit(target.Setup.Key);
                        }

                        // A counter above (or, in principle, anything else resolving earlier in this
                        // SAME tick's batch) may have already killed the attacker -- a dead actor must
                        // not still land a hit, matching DeclareBasicAttack's own `!attacker.Active`
                        // early return at commit time. Correctness fix surfaced by building RL2, not
                        // specific to it: the SAME gap exists for any concurrently-resolving kill under
                        // W > 1, reaction lane or not.
                        if (!actor.Active) continue;

                        var step = ApplyBasicAttack(
                            actor, target, state.BasicAttackEnvelopeCompiled, state,
                            now, localClock.Now, state.Calculator, state.CritRng);
                        if (step.Outcome == AttackStepOutcome.Proceed)
                        {
                            state.DispatchHit(actor, step.Target!, step.SignedDelta, rounds);
                            economy.OnActionResolved(economyKey(actor), Timeline.ActionResolutionOutcome.Normal);
                        }
                    }
                }
                else if ((Timeline.TimelineEventKind)ev.Kind == Timeline.TimelineEventKind.Recovery)
                {
                    trace?.Turn(rounds, ev.OwnerKey, Timeline.TurnState.Resolving, Timeline.TurnState.Recovering);
                    runner.OnRecoveryDue(machine, ev);
                    trace?.Turn(rounds, ev.OwnerKey, Timeline.TurnState.Recovering, Timeline.TurnState.Charging);
                }
            }
        }

        if (localGuard >= MaxLocalIterations)
            throw new InvalidOperationException(
                $"timeline-dispatch action phase exceeded {MaxLocalIterations} iterations in round {rounds} " +
                "-- a runaway, not a long action list. An action's lifecycle likely exceeds this method's " +
                "one-round scope assumption (spec-timeline-dispatch.md §2.4/§2.5).");

        // Defensive, not expected to fire given the structural guard above: abandon anything still
        // mid-action rather than silently carry state.Cooldowns/slots state into the next round.
        foreach (var a in order)
            if (runner.IsMidAction(a.Setup.Key)) runner.Abandon(a.Setup.Key);
    }
}
