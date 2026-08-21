# Spec: turn-fsm

Module id `turn-fsm` (T2) in the [battle timeline map](../battle-timeline-map.md). Depends on `virtual-time-core`. **Amended 2026-08-21** after the [structured review](audit-2026-08-21.md) — this module changed most. Added: `Downed`, the missing transitions, the intent seam, the post-apply trigger phase, the published pending-resolve handle, and a substantially reworked envelope.

## Objective

The per-actor state machine every mode shares, the concurrency width `W`, and the **action envelope** — the seam the combat action program plugs into. Defines *when* an actor may act and *how long that takes*; never what the action does.

## Design (locked on approval)

### States

```
Charging → Ready → Committed → Resolving → Recovering → Charging
```

Suspension (CC) is an orthogonal *predicate*, not a state — see the build correction below. `Downed`, `Dead`, and `Withdrawn` are the exits.

| State | Meaning | Ends when |
|---|---|---|
| `Charging` | Accruing readiness work | readiness event fires (T3) |
| `Ready` | Eligible; awaiting a slot and an intent | slot free **and** intent declared |
| `Committed` | Intent locked, wind-up running, **pending resolve published** | wind-up elapses, or the handle is cancelled |
| `Resolving` | The action applies — atomic w.r.t. the clock | immediately |
| `Recovering` | Post-action lockout | recovery elapses |

**`Downed` is a new state and it is not optional** (audit C3). HP ≤ 0 must not itself be a transition to a terminal state. Today `ReviveImmortals` fires on an actor that is `!Alive` but not yet swept — a limbo the five-state cycle could not name. Drawn as `Resolving → Dead` (terminal), an immortal's revive would attempt `Dead → Charging`, which is not in the table, which **throws** — a crash at the most expensive checkpoint in the program. So: HP ≤ 0 → `Downed` (present, targetable, revivable); `Downed → Dead` only on the scheduled death resolution, which is **veto-capable**; `Downed → Charging` on revive. On-kill and on-death triggers hang off the death resolution, not off an edge.

**Complete exit transitions** (audit I6 — the original diagram omitted most of them, and since illegal transitions throw, a drawing omission becomes a runtime crash):

- `Charging | Ready | Committed | Recovering | Downed → Withdrawn` — retreat is checked after *every* dispatch, mid-round, so an actor in any of these can retreat today.
- `Charging | Ready | Committed | Resolving | Recovering → Downed` — including killed by a counter during its own resolution, or by a DoT while suspended.

**Build correction (2026-08-21):** `Incapacitated` is **not a state in the enum**. Making it one would have re-created exactly the two-sources-of-truth problem the audit rejected — the FSM would hold a suspension marker while `StatusRuntime` holds the authoritative CC instance. It is a *predicate* consulted at the seat check, so a suspended actor simply sits in `Charging`/`Ready` and the transitions above already cover it. A test asserts no such state exists.

**A meta-test asserts the transition table against the documented diagram**, so the two can never drift again.

### Suspension is a derived read, not stored state

`Incapacitated` is evaluated at the seat-check point by querying `StatusRuntime` — it is **never** a cached remainder (audit D2).

CC lifetime is mutated through five paths the FSM cannot observe: Refresh/Replace stacking, family-mutex displacement, `ClearGrant`, `WithdrawEntity` (fired on both retreat and death), and the expiry prune. A cached remainder goes stale on all five. The boundary pairing is also load-bearing: `StatusRuntime` prunes on `ExpiresAt < now` (strict) while the lock test is `ExpiresAt >= now` (inclusive), so an instance expiring exactly at `now` survives *and still locks* — a 1000 ms CC locks exactly one round. A remainder counting to zero resumes one round early, which is a byte-identity break.

A `Committed` actor that becomes incapacitated is **interrupted** per its envelope's `Interruptible` policy.

### Illegal transitions throw

Checked against a table; an illegal transition throws naming both states. No "ignore and continue."

### The intent seam

```
IIntentSource { ActionIntent? TryDeclare(actorView, now); }
```

The kernel gated `Ready → Committed` on "an intent exists" while targeting was explicitly out of scope — a predicate it had no way to evaluate, with **no defined behavior for the false case** (audit C4). That is a deadlock: under next-event advance, an actor holding a slot with no intent schedules nothing, the queue drains, and the clock stops.

**Rule: no legal intent ⇒ the actor takes no slot, emits `action.passed`, and reschedules at `now + PassQuantum`** (a profile field). Invariant test: with every actor unable to declare, the battle terminates by round cap rather than hanging.

This one interface is simultaneously the **AI-policy seam** the auto modes need and the **player-input seam** T6 needs. `classic-round`'s existing `SelectTarget` becomes its first implementation. Affordability (resource costs) is a *selectability* question and belongs here — not in the envelope.

### The published pending resolve, and the reaction lane

At commit, the resolve is scheduled as a **published, cancellable handle** rather than an implicit timer (audit D7). Sequenced **before T5**: it changes *when* a resolve is scheduled, so retrofitting it after the gate would move goldens.

**Owner decision: reactions are in scope**, so the lane is built, not merely enabled:

- `WReact` — a **separate** slot pool from `W`. A defender in `Recovering` must still be able to block, or blocking degrades to "whoever happens to be idle."
- A **bounded nested-resolution stack**: a reaction resolves *inside* the triggering resolution. Restated invariant — **`Resolving` is atomic with respect to the clock, not with respect to the stack.**
- A **depth limit**, following the precedent this codebase already runs for exactly this shape (`ProcDepthLimit`, and the event-pipeline generation cap). Exceeding it drops the reaction and emits telemetry; it never recurses.
- A **reaction budget** on the economy, spent from a different pool than the actor's turn — otherwise every defender blocks every hit.

### Multi-actor coordinated actions (link-strikes)

**Owner decision: in scope.** Two or more actors commit together and produce **one** `Resolving`.

- `SlotReservation` — an N-actor atomic acquire. Either every participant gets its slot or none do; a partial acquire that waits is the deadlock.
- A `WaitingForPartner` dwell inside `Ready`, with a **bounded rendezvous timeout**. On timeout the reservation releases and each participant falls back to a solo intent — the timeout is mandatory, because an unbounded rendezvous at `W = 1` is a guaranteed hang.
- Economy is charged once per participant; recovery applies to each.

Both features are gated by profile knobs (`WReact`, rendezvous enabled) that default off, so `classic-round` is untouched and the T5 gate is unaffected.

### The scheduled entity may be a side

**Owner decision: press-turn is in scope**, which means the scheduled entity is no longer always an actor:

```
ISchedulable = Actor(key) | Side(sideId)
```

A side-scheduled profile schedules readiness for the *side*, which then picks which actor acts (through the same `IIntentSource`). This is what SMT press-turn requires and what a per-actor kernel cannot fake: the budget is per side, and it is mutated by **resolution outcome** — hitting weakness refunds, missing costs double. The economy therefore gains `OnActionResolved(outcome) → budgetDelta` (T3).

**Purity rule that keeps T4's no-branch test honest:** readiness stays a pure function of `(work, rate)`. The side budget is consumed at **slot acquisition**, never inside readiness. If a side budget ever needs to be read to compute an arrival time, the abstraction has failed and we should find out then rather than paper over it.

### The post-apply trigger phase

A slot-free, FSM-neutral point after **every** HP delta where registered listeners run in deterministic order: death veto (`immortal`), on-kill (`soul-eater`), and threshold crossings (`coward`).

This exists because the audit found that **none of the 14 traits in `TraitBattleCatalog` is an action on a timeline** — the kernel's central abstraction is unexercised by the game as it stands. `immortal`, `soul-eater`, and `coward` are all "something happened, respond," which the five-state cycle has no path for. Today the engine gets away with inline calls at fixed round offsets; `hybrid-atb` has no round boundary to hang them on.

### The action envelope

```
ActionEnvelope {
  string ActionId;            // identity — `skill.used` must be able to name what fired
  long TimeCostTicks;         // pre-speed time quantum, fed to T3
  string SpeedChannel;        // which channel readiness divides by (turn.speed, moveSpeed, …)
  long WindupTicks;
  long[] ResolveOffsets;      // multi-hit; default [0] relative to wind-up end
  long RecoveryTicks;
  CooldownClass Class;        // Category | Specific
  string CooldownKey;         // the discriminator Category needs
  long CooldownTicks;
  CooldownStart StartsAt;     // Commit | Resolve | RecoveryEnd
  bool SlotConsuming;         // false for movement and periodic pulses
  int PriorityBand;           // scheduling override; part of the sort key
  Interruptible Interruptible;// Never | OnCC | OnDamage  (+ refund per-mille)
  Commitment Commitment;      // EarlyBound | LateBound | EarlyBoundWithFallback
}
```

Every field earned its place against a named mechanic:

- **`Cost` → `TimeCostTicks`.** Three different costs shared one name — time (readiness), economy (AP/press-turn), and resource (mana). A content author reading `Cost = 30` reads "30 mana." Free to rename now; a golden re-bless once skill content exists.
- **`Rank` relocated** off the envelope to the readiness call. On the envelope it multiplied into the same product as `Cost`, making them algebraically one number — and losing the FFX distinction it was borrowed from, where one factor is actor-scoped and one action-scoped.
- **`SlotConsuming`** — under the original design *everything* mid-action held a `W` slot, so at `W=1` only one actor could ever move, and `regenerator`'s slot-free periodic pulse was inexpressible.
- **`SpeedChannel`** — readiness hardcoded `turn.speed`, making "moves fast, attacks slowly" impossible despite T3 reserving the `moveSpeed` vocabulary.
- **`PriorityBand`** — always-first effects are a scheduling override, not a cost multiplier, and this is **impossible to retrofit** because it changes the sort key from `(dueTick, seq)` to `(band, dueTick, seq)`.
- **`CooldownKey`** — a `Category` class with no discriminator cannot say *which* category.
- **`CooldownClass.Global` dropped** — no mechanic in this game needs a GCD.

**Cooldown state has an owner** (this module), a defined start point (`StartsAt`), and a stated rule for whether it advances while suspended.

**`Commitment` snapshots the whole request, not just the target.** `berserker` reads the attacker's own HP and `bloodthirsty` picks the lowest-HP opponent; once wind-up is non-zero, both need a defined read point. **EarlyBound + death rule:** resolves against a commit-time snapshot; if the target is not active at resolve, the action **fizzles** — emits `action.fizzled`, consumes the economy and full recovery, refunds no readiness. Consuming recovery keeps slot accounting identical on both paths, so `W=1` cannot deadlock on the fizzle branch.

`ActionEnvelope.NoOp` still exists for pure-FSM tests, **but it no longer validates the seam** — see T4.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~TurnFsm"
```

## Structure

```
src/FusionRpg.Core/Battle/Timeline/TurnState.cs        (states, table, meta-test source, illegal throw)
src/FusionRpg.Core/Battle/Timeline/ActorTurnMachine.cs (per-actor machine, derived suspension, pending handle)
src/FusionRpg.Core/Battle/Timeline/ActionSlots.cs      (W + WReact, deterministic contention, release-on-exit)
src/FusionRpg.Core/Battle/Timeline/ActionEnvelope.cs   (the seam)
src/FusionRpg.Core/Battle/Timeline/IntentSource.cs     (IIntentSource, ActionIntent)
src/FusionRpg.Core/Battle/Timeline/TriggerPhase.cs     (post-apply listeners, deterministic order)
tests/FusionRpg.Core.Tests/Battle/Timeline/
```

## Testing strategy

Every legal transition exercised; every illegal one throws naming both states; the **meta-test** asserts the table matches the documented diagram. `Downed`: an immortal actor takes lethal damage, enters `Downed`, revives to `Charging`, and does **not** throw — the exact scenario that would have crashed T5. Slots: `W=1` serializes in `(readyTick, seq)` order; slots release on death, withdrawal, interrupt, **and fizzle** (four tests — a leaked slot deadlocks `W=1`). Intent: with every actor unable to declare, the battle **terminates** rather than hanging. Suspension: CC read derives from `StatusRuntime`, and an instance expiring exactly at `now` still locks. Envelope: a non-zero-wind-up action proves commit→resolve→recover; a `SlotConsuming = false` action runs without taking a slot at `W=1`.

## Boundaries

- **Always:** deterministic contention ordering; slot release on every exit path; illegal transitions throw; CC derived, never cached.
- **Ask first:** adding a state beyond `Downed` — the vocabulary is shared with the observer adapter; whether reactions graduate from a published handle to a real reaction lane.
- **Never:** action semantics (damage, targeting, effects) here; a mode-specific branch inside the machine; `Resolving` doing anything but invoking the existing apply path; a cached CC remainder.

## Success criteria

1. The `Downed` revive path is proven before T5 touches the engine. 2. `W=1` cannot deadlock — proven on the no-intent, fizzle, and interrupt branches. 3. The envelope carries a **real** action (T4), not just `NoOp`. 4. The transition table and the diagram cannot drift.
