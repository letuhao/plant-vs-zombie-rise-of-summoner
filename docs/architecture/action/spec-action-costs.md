# Spec: action-costs (A3)

Module **A3** in the [action map](../action-map.md). Depends on **A1**. Sequenced **after `A5`** deliberately — a basic attack is free, so making the cost system a prerequisite would have delayed the only module that can tell us whether the action model is right.

Resource semantics come from [resource-hub-ideal.md](../resource-hub-ideal.md) and the **Resource model** row in [decisions.md](../decisions.md). This module does not redesign them; it spends them.

## Objective

Make an action cost something, and make running out matter.

Two halves:

1. **Paying** — validate, consume, and roll back an action's cost list.
2. **The pools themselves** — the five resources, their channels, their lazy regeneration, and the exhaustion status that fires when one empties.

## Design (locked on approval)

### 1. The five resources

One shared set. The faction difference is a **display label owned by content** — never a channel id, never a branch.

| id | class | plant label | zombie label | exhaustion |
|---|---|---|---|---|
| `hp` | body | HP | HP | **none** — depletion is death, owned by the turn FSM's `Downed` |
| `stamina` | body | Stamina | Stamina | ✅ |
| `hunger` | energy | **Sun** | Hunger | ✅ |
| `spirit` | essence | Spirit | Spirit | ✅ |
| `qi` | essence | **Yang** | **Yin** | ✅ |

`soul` is **not** here. It is the summoner mechanism's player-scoped currency and stays in `rpg_soul_balances`.

### 2. Channels, and what is *not* a channel

Two derived families in the Actor Hub: **`resource.max.{id}`** and **`resource.regen.{id}`**.

> They form **their own family list and must not join `AllCombatChannelIds`**, which a test asserts is exactly **84**. Resources are not element-typed, so element expansion would be wrong for them as well as arithmetically fatal.

**Current values are not channels.** They are per-actor runtime state, and they regenerate **lazily**:

```
value(now) = clamp(stored + rate × (now − lastTick), 0, max)
```

Not scheduled. Four regenerating pools across 200 actors would be **800 recurring events** against a 0.15 ms kernel slice, and the server already runs a compute-on-read law for exactly this reason.

The consequence that is easy to miss: **a lazily-resolved pool can cross a threshold with nothing observing it**, so exhaustion must be re-evaluated **on read**, not only on write.

### 3. Paying

```
validate every cost  →  consume every cost  →  roll back all of them if any fails
```

Atomic, and per pool. An action that consumed `stamina` and then found no `spirit` must leave the actor exactly as it found them — asserted pool by pool, not in aggregate, because an aggregate assertion passes when two errors cancel.

`when` decides the moment:

| `when` | Behaviour |
|---|---|
| `onCommit` (default) | The whole cost at commit |
| `perTick` | One payment per resolve offset. **Failing to pay ends the action** — cancel remaining resolves, release the slot, charge `interrupt_cooldown_milli`. Diablo's channel shape: pay per second, drop the channel when you run dry |

**Committing is what costs, not landing.** Interrupted, fizzled, and missed actions have all paid. One rule with no exceptions, and it is what keeps slot accounting identical on every exit path.

### 4. Exhaustion is a status, not a new mechanism

Every resource except `hp` debuffs derived stats when it empties. `StatusRuntime` already owns instances, stacking, family mutex, resistance, VFX cues, and — the one that matters here — **`icd_ms`**, which exists precisely to stop an apply/clear cycle churning.

Reusing it buys three things that would otherwise need inventing: exhaustion becomes **visible**, **resistable** (a trait can grant resistance to stamina exhaustion), and **dispellable**.

**What the debuff *is* is content, not code.** The registry stores a **container id**; the container is atoms. Hardcoding a channel list here would make this the fifth content system the atom program exists to stop.

Two rules the implementation must hold:

- **Hysteresis.** `exhaustEnter‰` / `exhaustLeave‰` per resource, validated as `leave > enter` at load, defaulting to enter 0‰ / leave 100‰. A pool sitting at zero while regen trickles would otherwise apply and clear the debuff on alternate ticks.
- **No self-regen cycle.** An exhaustion debuff must **never** touch a channel that feeds its own resource's regeneration. That is the only true death spiral, and because the registry knows which channels feed which regen, it is **rejectable by validation** rather than left to judgement.

*(The broader spiral — exhaustion slowing the turn channels — is mostly not real: regen is per tick of simulated time, so a slowed actor waits longer and therefore regenerates more before acting. A proportional floor on the turn channels is still required, but for kernel-stall reasons and it belongs to the readiness model.)*

### 5. Lifetime — persist across a run, refill at rest

> **A rest is a place, not a timer.** A *run* is a sortie away from the summoner's base; a *rest* is the return.

| Structure | Run | Rest |
|---|---|---|
| Expedition | The expedition, across all encounters | Return to base |
| World map | Travel between safe sectors | Arriving at a home or friendly sector |
| One-off skirmish | A run of exactly one encounter | Immediately after — always starts full, persists nothing |

Refill is **full**. Three consequences:

1. **Pool state needs somewhere to live between encounters** — a per-member row on the run. Nothing stores this today, **and "a run" is not a thing the schema has** (audit I4). Three structures, one of them missing:

   | Context | Run identity | State |
   |---|---|---|
   | Expedition | `rpg_expeditions.id` | Exists |
   | World map | A journey between safe sectors | Needs deriving — the world program owns turns and sectors, not runs |
   | One-off skirmish | **None** | A run of exactly one encounter, so it needs no row: pools start full and persist nothing |

   The skirmish case is the one that resolves the problem rather than complicating it — **absence of a run row means a run of one**, which is both the correct behaviour and the easiest case to test. Only the world-map case needs a decision, and it belongs to that program.
2. **`ExpeditionResolver` must thread pools through its encounters**, or expeditions silently ignore the entire resource system.
3. **`hp` follows the same rule.** A demon ending at 10 HP starts the next fight at 10. That is the intended attrition, and it changes expedition outcomes and every balance number derived from them.

### 6. What `now` means across a boundary

> **Lazy within a battle; concrete between battles.**

Inside a battle, `now` is the simulation tick, persisted with the state so save and load resume from the same tick. **At battle end every lazy pool is resolved to a concrete value and `lastTick` is dropped** — what crosses the boundary is a number, not a number plus a timestamp from a clock about to reset to zero.

**Cooldowns do not survive a battle boundary.** They are ticks in a clock that no longer exists; a cooldown meant to span encounters is a run-scoped effect and belongs to the run. Carrying one across would make it expire instantly, and the bug would look like content being wrong rather than time being wrong.

### 7. Shields are excluded, on purpose

They have capacity, regen, and depletion, which is why they keep looking like resources. But **nothing ever pays a shield to act.** They are consumed by the damage pipeline, element-typed across seven pools, and carry `toughness` and `pen` — semantics no action cost has or wants. They are also shipped and test-locked.

The registry owns the **action economy**; shields belong to the **damage layer**. Recorded so it is not re-argued.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Resource"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionCost"
dotnet test tests\FusionRpg.Data.Tests
```

## Structure

```
src/FusionRpg.Core/Resources/ResourceCatalog.cs     (the five, code-first like StatusCatalog)
src/FusionRpg.Core/Resources/ResourcePool.cs        (value, lastTick, lazy resolve, clamp)
src/FusionRpg.Core/Resources/ExhaustionEvaluator.cs (hysteresis, status apply/clear)
src/FusionRpg.Core/Actions/CostValidator.cs         (validate-all / consume-all / roll-back)
src/FusionRpg.Data/Sqlite/RpgStore.RunPools.cs      (per-run pool state)
tests/FusionRpg.Core.Tests/Resources/
```

## Testing strategy

- **Rollback is atomic, asserted per pool.** An action whose second cost fails leaves every pool exactly as found. An aggregate assertion passes when two errors cancel, so this is checked pool by pool.
- **Lazy regen equals scheduled regen.** Resolve a pool once after 1000 ticks and compare against a thousand one-tick steps. Identical, or the lazy model is a different game.
- **Exhaustion does not flicker.** A pool held at the enter threshold with regen trickling must produce **one** status apply, not one per tick. Count applies, not final state — the final state is identical either way, which is what makes this bug invisible without a counter.
- **A self-regen cycle is rejected at load**, proven against a planted cycle rather than trusted.
- **`resource.*` channels are absent from `AllCombatChannelIds`** — the 84-count assertion still passes, tested directly rather than as a side effect.
- **Exhaustion re-evaluates on read.** Let a pool cross the leave threshold with no write at all, then read it: the status must clear. This is the failure mode lazy resolution introduces, and nothing else catches it.
- **Pools survive an encounter boundary and refill at rest**, with `hp` following the same rule.
- **A `perTick` cost that cannot be paid ends the action** through the interrupt path, releasing the slot and charging `interrupt_cooldown_milli`.

## Boundaries

- **Always:** validate-all before consume-all; keep `resource.*` out of `AllCombatChannelIds`; express exhaustion as a status whose debuff is a container of atoms; resolve pools lazily.
- **Ask first:** adding a sixth resource; changing a display label mapping (that is content); giving shields a registry row.
- **Never:** a scheduled per-tick regen event; an exhaustion debuff touching its own regen channel; `soul` as an actor pool; a hardcoded channel list for an exhaustion debuff; a resource reading a PvZ value — the two games share no state.

## Success criteria

1. An action can cost several resources, atomically, with a per-pool-proven rollback.
2. Four regenerating pools across 200 actors add **zero** scheduled events.
3. Exhaustion is visible, resistable, dispellable, and does not flicker.
4. Pools persist across a run and refill at rest, including through `ExpeditionResolver`.
5. Adding a resource costs a catalog row and two channels — not a system.
