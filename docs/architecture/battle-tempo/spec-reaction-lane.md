# Spec: `reaction-lane`

Module `reaction-lane` in the [battle-tempo map](../battle-tempo-map.md).
**Depends on `action-timing` and `commitment-binding`.**

**Read before editing:** [battle/audit-2026-08-21.md](../battle/audit-2026-08-21.md) **D7** ·
[action-ideal.md](../action-ideal.md) §2.2 and decision **#3** · [battle-turn-ideal.md](../battle-turn-ideal.md) §3.

---

## 1. Objective

**Let a defender act inside an attacker's action — block, parry, counter.**

The mechanism is **already built**. `ReactionLane` owns a separate `WReact` slot pool composed from
`ActionSlots` (so it inherits the deterministic `(readyTick, seq)` contention ordering), a nesting depth
limit, and a four-value `ReactionOutcome`. `WReact` defaults to **0**, so the lane costs nothing and
does nothing.

This module turns it on and gives it content.

### 1.1 What D7 already decided — do not re-litigate

The 2026-08-21 audit adjudicated this exactly: **"buy the option, don't pay for the feature."** The
pending resolve is published as a **cancellable handle** at commit time rather than living as an
implicit timer, `WReact` defaults to 0, and the whole thing was sequenced *before* T5 precisely because
it changes *when* a resolve is scheduled and retrofitting it later would move goldens.

That purchase has been made. This module spends it.

### 1.2 ⛔ Guard is a STANCE and stays one — sealed decision #3

`action-ideal.md` decision **#3** is sealed: *"Guard is a STANCE, not a reaction. Continuous while
enabled; every other action, movement included, is refused while it holds."*

**So guard is not built here, and must not be.** D7's own adjudication says stance-based defence
*"remains available and needs nothing"*. This module is for **counters and parries** — reactions that
fire in response to a specific triggering resolution — not for the defensive posture the action layer
already owns.

⚠️ **The word "reaction" is overloaded in this repo and the two meanings are unrelated.** Axis **F
`reaction`** in `structureBudget` means a *trigger-based* effect (`OnDeath` + `actorIsKiller`), gated at
rung 9. This module is the *kernel's* reaction lane. Conflating them would spend a complexity axis on a
scheduling feature.

---

## 2. Design

### 2.1 Why it needs `action-timing` first

A reaction needs something to react *into*. With zero wind-up, `Committed → Resolving` is instantaneous
and there is no interval in which a defender could be offered the lane — which is exactly why D7's
finding was that *"`Committed → Resolving` is observable only by the acting actor"*.

Once wind-up exists, the pending-resolve handle spans real ticks and the lane has a window.

### 2.2 Turning it on

`WReact` is a **tuning magnitude** on the profile row (`battle.v{n}.json`'s `timeline.profiles`), like
`W` and `PassQuantum`. Raising it above 0 for `hybrid-atb` is a config change, not a code change —
which is the whole point of the row-not-branch discipline.

✅ **Settled 2026-09-04 (owner): the module is in scope, at `WReact = 1` on `hybrid-atb` only.**
`classic-round` stays at 0, so it keeps its byte-identity and the lane is provably closed there.

⚠️ **One reaction in flight** is enough to prove the mechanism and bound the blast radius. A wider lane
multiplies nested resolutions against a depth limit that has never run under load.

### 2.2a ⛔ D7 — where does a reaction's intent come from?

**Review finding, 2026-09-04.** The first draft specified the lane's *mechanism* completely and never
said **who decides to counter**. A reaction is an action; something must declare it.

**It reuses `IIntentSource`, and must not grow a parallel seam.** That interface is already documented as
serving both masters — *"the AI-policy seam the auto-resolved modes need, and the player-input seam an
interactive mode needs"*. A reaction is declared through it like any other intent, distinguished by the
lane it enters rather than by a separate source.

⚠️ **Auto-resolved battles need a policy, and "always counter if able" is the wrong default.** With
`WReact = 1` and no cost, an unconditional counter is strictly free value, which makes the lane a flat
power increase rather than a decision. The declaring policy must have a **reason to decline** — otherwise
this module is a global buff wearing a mechanic's name.

✅ **Settled 2026-09-04 (owner): design the policy now — and the resource for it already exists.**

⭐ **A reaction costs `poise`.** `resource-hub-ssot.md` defines that pool as paying for *"Guarding — a
flat commit cost to raise a guard, drained further in proportion to what it absorbs"*, and exhaustion
means *"Guard breaks. The actor can still act, but **cannot absorb**."* A counter is defensive effort in
exactly that family, so this needs **no new mechanism and no new meaning**:

- ✅ It satisfies the pool table's own **normative** rule — *"a new cost on a resource whose meaning is
  undecided is an authoring error"*. `poise`'s meaning already covers defensive absorption.
- ✅ The machinery exists: `ActionCostRow(ActionId, ResourceId, AmountSpec, When, AllowLethal)` and
  `CostLedger`'s typed `CannotAfford` refusal.
- ✅ Affordability lands in the right place by the envelope's own rule — *"affordability is a
  selectability question and lives in the intent source"* — so declining is a **selection** outcome, not
  a new branch in the lane.

⭐ **And it produces the decision the lane needed.** `poise` is the same pool guarding spends, so
**countering now competes with guarding later**. Declining stops being an AI heuristic and becomes a
resource judgement: spend to punish this swing, or hold to absorb the next one. That is what stops
`WReact = 1` from being a flat power increase.

⛔ **This still does not build guard** (sealed decision #3 — guard is a stance). Sharing a pool is not
sharing a mechanism; `spec-guard-economy.md` owns guarding, and this module only draws on the same
resource.

⚠️ **What remains genuinely open is the NUMBER, not the shape** — how much `poise` a counter costs, and
the threshold at which a policy holds rather than spends. Both are tunables for a balance pass, not
design, and neither blocks the module.

### 2.2b ⭐ D8 — the counter's PAYOFF half is already built too, and named the riposte

**Verification round 2, 2026-09-04.** Decision 10 said "no new mechanism" about the *cost*. It is truer
than that: **the output side ships as well, tested, with its tunable already authored.**

| Piece | Where | State |
|---|---|---|
| Lane mechanism | `ReactionLane` (`WReact` pool, `DepthLimit`, 4-value `ReactionOutcome`) | ✅ built |
| Pool | `poise`, in `DerivedStatChannels.ResourceIds` — six resources | ✅ registered |
| Cost | `PoiseLedger.TryCommit` / `TryPayAbsorbDrain` over `ActorResourcePools` | ✅ built |
| **Payoff** | **`PoiseRuntime.Riposte(spentPoise, shareMilli)` — spent poise converts to damage** | ✅ built, 12 tests |
| Tunable | `AptitudeGuardEconomy.RiposteShareCapPermille` | ✅ authored, **unmeasured** |

⭐ **`ReactionLane`'s own `DepthLimit` was sized for exactly this module.** Its comment: *"`3` covers the
deepest shape this game has named so far — **a hit, a block, and a riposte to the block**."* The kernel
already anticipated the counter this module turns on.

⛔ **So do NOT invent counter damage.** The counter's output is `Riposte`. Writing a fresh damage path
would be a second curve over a number the class system already ships.

### 2.2c ⛔ D9 — there are TWO poise stacks, and this module must pick one

**The most consequential finding of the review round, and nothing in the repo acknowledges it.** Two
independent poise implementations exist, built ten days apart under different programs. **Both have zero
production callers**, so nothing is broken today — but this is the first module that would call one, and
calling the wrong one entrenches the fork.

| | `Combat/Guard/PoiseRuntime.cs` | `Actions/Defence/PoiseLedger.cs` |
|---|---|---|
| Program | class-system `P7.1–P7.3` | action program `T25/T26` |
| **Pool** | ⛔ its **own** `Dictionary<string, long>` | ✅ `ActorResourcePools` — the six-resource SSOT |
| Commit cost | `Commit(key, flatCost)` | `TryCommit(pools, …)` |
| Absorb drain | `Absorb(key, stopped, milli)` | `AbsorbDrainAmount(stopped, milli)` |
| Riposte | `PoiseRuntime.Riposte` | `Actions/Defence/Riposte.DamageFromSpentPoise` |
| On empty | floors at 0, never refuses | typed `CannotAfford` refusal |

⭐ **The irony names the defect:** `PoiseLedger`'s own doc says it is *"a thin wrapper over T15's
`ActorResourcePools.TrySpend` — **never a second pool mechanism**."* `PoiseRuntime` **is** that second
pool mechanism, written first, under a different program, and neither file mentions the other.

✅ **Settled 2026-09-04 (owner) — decision 13: the fork is RECONCILED, not routed around.** A new root
module, [`poise-unification`](spec-poise-unification.md), collapses the two stacks before this one
builds. `reaction-lane` therefore depends on it, and by the time this module runs there is exactly one
pool and one riposte.

**Cost goes through `PoiseLedger` / `ActorResourcePools` — the side that survives.**

- It is the **resource SSOT** — `resource-hub-ssot.md`'s six pools, the same path `stamina` and `qi` pay
  through. A private dictionary would drift from every regen, cap and telemetry read that hub owns.
- It gives the **typed `CannotAfford` refusal** decision 10 relies on, so declining lands in the intent
  source as a selectability outcome rather than as a silent floor-at-zero.
- `PoiseRuntime`'s own comment explains why it is standalone — *"the action layer
  (spec-action-costs.md) does not exist to trigger yet."* ⚠️ **That is now stale**: `ActorResourcePools`,
  `CostLedger` and `PoiseLedger` all ship.

⚠️ **`PoiseRuntime.Riposte` is still the right function to call for the payoff** — it is a pure static
(`spentPoise × share / 1000`), indifferent to which pool the spend came from. Its twin,
`Riposte.DamageFromSpentPoise`, is byte-identical arithmetic in another namespace. **Pick one and say so
in a comment**; do not leave the next reader to discover both.

⛔ **Reconciling the two stacks is no longer deferred, and it is no longer this module's problem
either** — `poise-unification` owns it as a dependency. ⚠️ That module must also settle the one live
contradiction between the stacks: `PoiseRuntime.Commit` **floors at zero and never refuses**, while
`PoiseLedger.TryCommit` **refuses**. **This module needs the refusal** — decision 10's declining counter
is a typed `CannotAfford` in the intent source, and floor-at-zero produces no refusal to read.

### 2.2d ⚠️ The cost and the payoff are the same number — say which reading applies

Decision 10 says a counter **costs** poise. `Riposte` says spent poise **becomes** damage. Applied
naively, both charge the same pool for one action and the payoff grows with its own cost.

| | Reading A — flat cost | Reading B — the spend IS the attack |
|---|---|---|
| Cost | a fixed `poise` commit | the poise the counter chooses to spend |
| Damage | ordinary action damage | `Riposte(spent, shareCap)` |
| Uses `Riposte`? | ⛔ no — leaves shipped, tested code unused | ✅ yes |
| The decision it creates | spend or hold | **how much** to spend, and hold what is left |

✅ **Settled 2026-09-04 (owner) — decision 12: Reading B.** It uses the shipped mechanism, and it makes the counter a *scaling*
decision rather than a binary one — which is what BASTION's whole economy was built around
(*"a guard that costs nothing when it stops nothing would also produce nothing"*). Reading A would
charge for a counter while leaving `Riposte` inert, which is the worst of both.

⚠️ **The spend range is now a balance number, and it is this module's to size** — how much poise a
counter may commit, and the threshold at which a policy holds instead. Tunables, not design; neither
blocks the build.

### 2.3 The depth limit is a structural cap, and must say so

`ReactionLane.DepthLimit` bounds nested resolution — a counter to a counter to a counter. It is a
**structural limit (recursion), PS-8 exempt, and must carry a comment saying so**. The precedent is
named in the audit itself: this codebase already runs `ProcDepthLimit` and an event-pipeline generation
cap for the same shape.

⛔ **It bounds recursion, not player power.** It must never be reachable by a build — if a player can
routinely hit the depth limit, the cap has become a progression ceiling and `ssot-power-scale.md` §11
applies.

### 2.4 Determinism inside a nested resolve

A reaction resolves **inside** the triggering resolution. `Resolving` stays atomic with respect to the
clock while ceasing to be atomic with respect to the reaction stack.

- The reaction's own damage goes through the **same** `DamagePacket` → `CombatDamageDispatcher` →
  `ShieldGate` path. No second combat path.
- Draw order must be stable: reactions enter the lane in `(readyTick, seq)` order, which `ActionSlots`
  already guarantees and this module inherits rather than reimplements.
- ⛔ **The reactor's own `ActorTurnMachine` never moves.** A reaction is not the reactor's turn — that
  is stated in `ReactionLane`'s own contract and is what keeps the FSM honest.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ReactionLane|FullyQualifiedName~Battle.Timeline"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Golden|FullyQualifiedName~Shield"
.\scripts\guard-funnel-delta.ps1
```

---

## 4. Project structure

```
data/tuning/battle.v{n}.json                           WReact 0 -> 1 for hybrid-atb (published)
src/FusionRpg.Core/Battle/BattleEngine.cs              offer the lane at the pending-resolve window
src/FusionRpg.Core/Battle/Timeline/ReactionLane.cs     mechanism EXISTS — no change expected
tests/FusionRpg.Core.Tests/Battle/Timeline/ReactionLaneTests.cs   extend
```

---

## 5. Testing strategy

1. **All four `ReactionOutcome` values are reachable** — `Entered`, **`NoLane`** (`WReact` 0),
   `DepthExceeded`, `NoSlot`. ⚠️ The value is `NoLane`; this line said `LaneClosed` — a name that does
   not exist — until the coverage audit checked it against `ReactionLane.cs`. `NoLane` is the one that
   must stay true for `classic-round`.
2. **A counter lands inside the attacker's wind-up**, and its damage passes the funnel — asserted
   through the shared dispatcher, not a bespoke path.
3. **The reactor's FSM does not move** — its `TurnState` before and after is unchanged.
4. **Depth limit holds** and is unreachable by ordinary content (a build must not routinely hit it).
5. **`classic-round` is byte-identical** — `WReact` stays 0 there, so the lane is closed and no golden
   moves.
6. **Determinism:** identical seeds reproduce identical nested-resolution order.

---

## 6. Boundaries

- **Always:** keep `WReact` a tuning row; route reaction damage through the existing funnel; comment the
  depth limit as structural.
- **Ask first:** `WReact` above 1; making reactions available to `classic-round`; any reaction that
  moves the reactor's own machine; costing a reaction anything other than `poise`; revisiting Reading B
  (decision 12); building before `poise-unification` lands.
- **Never:** author a fresh counter-damage path when `Riposte` ships (D8); pay a cost through
  `PoiseRuntime`'s private pool instead of `ActorResourcePools` (D9); build guard here (sealed #3);
  conflate this with structure axis F; let the depth limit become reachable by build power.

---

## 7. Success criteria

1. A defender demonstrably counters inside an attacker's wind-up, through the shared damage path.
2. All four outcomes reachable and tested.
3. `classic-round` unchanged and byte-identical.
4. The depth limit is documented as structural and proven unreachable by ordinary play.

---

## 8. Golden movement

`classic-round` keeps `WReact = 0`, so **it moves nothing**. `hybrid-atb` gains reactions and will move
its own outcomes — but per the map that lands **after** `action-timing`'s single re-bless, byte-identical
on top of it. Measure, then predict.
