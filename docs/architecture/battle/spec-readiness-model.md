# Spec: readiness-model

Module id `readiness-model` (T3) in the [battle timeline map](../battle-timeline-map.md). Depends on `turn-fsm`. Stat grounding: Chaos `combat-core/01_Cultivation_System_Integration.md`, initiative formula from `08_World_Core_Binding.md`.

## Objective

Decide **when** an actor's next turn lands. This is the one function that unifies ATB, CTB, and today's round-robin — they are the same formula with different parameters. Also owns the turn economy as a pluggable strategy, so the gameplay choice between one-action, Action Points, and Press Turn never becomes an architectural one.

## Design (locked on approval)

### Speed and haste as derived channels

Two new **flat, non-elemental** channels:

```
turn.speed    // higher acts more often
turn.haste    // per-mille action-time multiplier; 1000 = normal, 500 = twice as fast
```

Flat rather than element-split, following the existing precedent for non-elemental channels (`status.power.omni`, `progression.power` are plain consts outside the generated `CombatChannelFamilies` roster). Speed is not elemental, so it must not enter the 12-family × 7-element generation — that would add 14 meaningless channels and change the roster count. They are added to `BattleStatComposer`'s known-channel set so `ChannelMods` can target them.

Names inherited from the Chaos stat families (`speed`, `haste`). The movement family there (`moveSpeed`, `climbSpeed`, `swimSpeed`, `flightSpeed`, `jumpHeight`) is **reserved vocabulary, not build scope** — movement is the next program's problem.

### The readiness function

**Readiness accrues work; it is not a precomputed deadline** (audit I1). This was the single largest correctness fix from the review.

The original formula was evaluated **once, at schedule time**. That is equivalent to CTB/ATB *only while speed and haste stay constant between scheduling and arrival* — which is exactly when they don't. FFX's CTB decrements a counter every tick and ATB fills a gauge continuously, so a Haste landing mid-wait takes effect on **that** turn. Under a precomputed deadline it does not: a mage scheduled for `t+1000` who is hasted at `t+500` still acts at `t+1000`, and the player sees *"I hasted my mage and nothing happened"* — in the mode whose entire identity is speed-driven turn order. The ideal's claim that "FFX CTB is literally this" was wrong in the case that matters, and is corrected there too.

So an actor in `Charging` holds `(accruedWork, rate)`. The scheduled arrival is derived:

```
remainingWork = totalWork - accruedWork
nextReadyTick = now + max(1, RoundDiv(remainingWork × BaseSpeed, rate))
where  totalWork = TimeCostTicks × rank × haste / 1000
       rate      = actor's SpeedChannel value (clamped >= 1)
```

**On any mutation of the actor's speed or haste channels, readiness rebases**: accrue the work done so far, then `Reschedule` with the new rate. T1 already provides `Reschedule` (and now guarantees it preserves `Seq`), so the machinery exists — what was missing was the `remainingWork` concept and the rule that **those channels are the only ones whose mutation triggers a rebase**. Suspension follows the same rule: it stores **work**, not time, so resuming from a stun with haste applied is correctly faster.

`rank` is supplied by the **caller**, not the envelope (audit I2): on the envelope it multiplied into the same product as the time cost, making the two algebraically one number and discarding the FFX distinction between an actor-scoped and an action-scoped factor.

Four decisions inside that arithmetic, each deliberate:

Four decisions inside that line, each deliberate:

- **`max(1, …)`** is a safety invariant, not a nicety. A zero-tick readiness under next-event advance schedules an event at `now`, which pops immediately and reschedules at `now` — an infinite loop that never advances the clock. Every readiness result is at least one tick.
- **`RoundDiv(a, b) = (a + b/2) / b`**, explicitly rounding rather than truncating. FFX's CTB floors here, and the documented consequence is turn-order inconsistencies at high agility with haste. We have the prior art; we should not reproduce its bug. This matches the rounding discipline `ShieldMath` already uses.
- **`rate` is clamped to `>= 1`** before dividing. A stat debuff that reached zero would otherwise divide by zero.
- **`BaseSpeed = 100`**, matching the resolver's scale-100 convention so a speed of 100 means "nominal" everywhere in the codebase.

**Which channel is `rate`?** The envelope's `SpeedChannel` names it. Hardcoding `turn.speed` made "moves fast, attacks slowly" inexpressible even though the Chaos movement family is reserved vocabulary here. One field, and the reserved names become usable without reopening T3.

### Registration — the stat must actually exist

`DerivedStatRegistry.RegisterDefaults` registers channels **by name**, with prefix fallbacks only for `status.*`; anything unknown throws `UnknownDerivedChannelException` from `ValidateChannel`, which `DerivedComposer` calls on every modifier. So without registration, "Speed is a real stat" is false the moment a `turn.*` modifier goes through the compose path — it throws.

Both channels need **non-zero defaults**, unlike every existing combat channel (which default 0): `turn.speed = 100` (0 would hit the divide-by-zero clamp on every actor) and `turn.haste = 1000` (0 would mean instant actions). Battle-only use works through `BattleStatComposer`'s separate known-channel set, but that is not the same as being a real stat.

### Two implementations, and why

`IReadinessFunction` ships with exactly two:

| Implementation | Behavior | Used by |
|---|---|---|
| `ConstantReadiness` | fixed quantum, **ignores speed entirely** | `classic-round` |
| `SpeedScaledReadiness` | the formula above | `galaxy-sync`, `hybrid-atb` |

This is the resolution of the owner's two answers — "Speed is a real stat" and "`classic-round` stays byte-identical" — which cannot both hold if `classic-round` reads speed, because varying speed reorders turns and reorders every golden. Making readiness **profile-scoped** means speed is fully real at the stat layer and costs nothing until a battle opts into a profile that consumes it.

### Initiative

Adapted from Chaos (`speed × 1.0 + haste × 0.5 + seeded_tiebreaker`), integer-scaled:

```
initiative = speed × 1000 + hasteContribution × 500 + seededTiebreak
```

Note the source formula already carries a **seeded** tiebreaker — the same determinism discipline we enforce, arrived at independently. Ours draws from the existing `initiative` RNG stream.

`classic-round` keeps today's behavior exactly: a per-round `NextInt(1000)` draw per active actor, minus the swift bonus, stable-sorted. **The draw count and order are part of the byte-identity contract** and are asserted by a stream-parity test, not assumed.

### Turn economy

`ITurnEconomy` decides how many actions a turn buys.

- **`OneActionPerTurn`** — ships now. All that `classic-round` and `galaxy-sync` need.
- **`ActionPoints`** — shaped, not built: `base_ap_per_round`, an AP cost derived from the envelope's duration and cooldown, and `minor_action_threshold_ap` separating minor from full actions (Chaos 08).
- **`PressTurn`** — shaped, not built: a per-*side* budget where hitting weakness or critting refunds, which is the mechanic that gives SMT its identity.

Only the interface plus `OneActionPerTurn` is in this module's build scope.

**Two corrections from the review, and an honest downgrade.** First, **slot acquisition routes through the economy** (`economy.TryAcquire(actor)`) rather than `ActionSlots` deciding alone — otherwise AP needs actor scope and press-turn needs side scope while a single global `W` gates the same edge, and since illegal transitions throw, that mismatch fails loudly.

Second, **the owner has put press-turn in scope**, so the interface is shaped for it now rather than discovering at T6 that it cannot hold its own planned implementations (audit D3):

```
ITurnEconomy {
  Scope Scope;                                   // Actor | Side
  bool TryAcquire(ISchedulable entity);          // slot acquisition routes here
  BudgetDelta OnActionResolved(ActionOutcome o); // weakness refunds, miss penalties
  void OnTurnStart(ISchedulable entity);
}
```

Three implementations ship: `OneActionPerTurn` (actor-scoped, what `classic-round` needs), `ActionPoints` (actor-scoped — `base_ap_per_round`, cost derived from the envelope, `minor_action_threshold_ap`), and `PressTurn` (**side**-scoped — a shared icon budget where hitting weakness spends a half-icon and a miss costs two).

**The purity rule that keeps T4's no-branch test honest:** a side budget is consumed at **slot acquisition** and adjusted at **resolution outcome** — never read inside the readiness function. Readiness stays a pure function of `(work, rate)`. If a budget ever has to be consulted to compute an arrival time, the abstraction has failed and we want to learn that here, not paper over it.

`PressTurn` is now the honest success criterion for this module: it is the implementation that would have broken the original interface, so **if it can be written cleanly, the interface is right.**

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Readiness"
```

## Structure

```
src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs (turn.speed / turn.haste consts — flat, NOT in the element roster)
src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs (RegisterDefaults: turn.speed = 100, turn.haste = 1000)
src/FusionRpg.Core/Battle/Timeline/ReadinessFunction.cs (IReadinessFunction; Constant, SpeedScaled — work-based)
src/FusionRpg.Core/Battle/Timeline/TurnPolicy.cs        (BaseSpeed, RoundDiv, clamps)
src/FusionRpg.Core/Battle/Timeline/TurnEconomy.cs       (ITurnEconomy; OneActionPerTurn)
src/FusionRpg.Core/Battle/BattleStatComposer.cs         (known-channel set gains turn.*)
tests/FusionRpg.Core.Tests/Battle/Timeline/             (readiness math, monotonicity, initiative parity)
```

## Testing strategy

**Computed, not sampled** — the readiness function is pure, so acceptance is exact arithmetic, matching how the U10 rate tests were written. Monotonicity: doubling speed halves the interval (within the rounding rule); haste 500 halves it; haste 2000 doubles it. **Mid-flight rebase (the audit's I1 regression lock):** an actor half-way through a 1000-tick wait that gains haste 500 arrives at `t+750`, **not** `t+1000` — and the same actor suspended and resumed with haste applied resumes faster, proving work is stored rather than time. Boundaries: speed 0 clamps rather than throwing; a zero-cost action still yields `>= 1` tick — asserted directly, because that invariant is the difference between a working clock and a hang. Rounding: values that would truncate differently under floor are asserted against the round-half-up expectation. Determinism: the `initiative` stream's draw count and order under `classic-round` match today's engine exactly (this is a byte-identity precondition for T5, and failing it early is much cheaper than failing it at the golden gate). Roster: adding `turn.*` leaves `AllCombatChannelIds` at 84.

## Boundaries

- **Always:** integer math with explicit rounding; readiness `>= 1` tick; speed clamped before division; `classic-round` ignores speed.
- **Ask first:** which turn economy the interactive mode uses; giving `classic-round` a speed-driven readiness (that is a balance change on the order of the U10 re-tune and needs a win-rate sweep).
- **Never:** element-splitting the speed channels; floating-point in the readiness path; a readiness result that can schedule at `now`.

## Success criteria

1. One formula demonstrably expresses CTB, ATB, and round-robin by parameter choice alone. 2. `ConstantReadiness` reproduces today's turn order draw-for-draw. 3. Speed exists as a real stat and changes nothing until a profile reads it. 4. The economy interface is proven by a second implementation stub, so T6 is a plug-in rather than a refactor.
