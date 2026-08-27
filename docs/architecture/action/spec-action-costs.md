# Spec: action-costs (A3)

**Status: REVISED 2026-08-27** against the sealed [action-ideal.md](../action-ideal.md). Module **A3** in
the [action map](../action-map.md). Depends on **A1** and **A12**.

Sequenced **after `A5`** deliberately — putting a behaviour change inside the one module whose entire job
is proving nothing changed would defeat the byte-identity gate. `A4` ships its affordability gate as a seam
until this lands.

**What changed in this revision:**

| | Change | Source |
|---|---|---|
| 1 | **Six resources**, not five — `poise` shipped 2026-08-26 | `decisions.md`, `DerivedStatChannels.cs:510` |
| 2 | The **"exactly 84 channels"** claim is **wrong** — it is 259+ | `derived-stats` shipped 256; `poise` added three |
| 3 | The pools are **already built** — this module no longer creates them | `DerivedStatRegistry.cs:165-171`, `roster.json` |
| 4 | **Cost and cooldown ride the rung** | ideal §6, decisions 11, 12 |
| 5 | **`poise` has a three-part cost** | ideal §2.2, `spec-guard-economy.md` |
| 6 | **"Stamina is free" is a tuning defect, not ours** | ideal §2.1, decision 22 |
| 7 | **An item is not a resource** — open, from another lane | [item/ssot-consumables.md](../item/ssot-consumables.md) §5(b) |

## Objective

Make an action cost something, and make running out matter.

## Design

### 1. The six resources — already registered, not created here

One shared set. The faction difference is a **display label owned by content** — never a channel id, never
a branch.

| id | class | plant label | zombie label | pays for | exhaustion |
|---|---|---|---|---|---|
| `hp` | body | HP | HP | nothing — spent by **being hit** | **none** — depletion is death, owned by the FSM's `Downed` |
| `stamina` | body | Stamina | Stamina | physical: move, basic attack, reposition | ✅ |
| `hunger` | energy | **Sun** | Hunger | nothing — spent by **recovery** | ✅ |
| `spirit` | essence | Spirit | Spirit | nothing — spent by **affliction** | ✅ |
| `qi` | essence | **Yang** | **Yin** | skills and abilities | ✅ |
| **`poise`** | body | Poise | Poise | **guarding** — §4 | ✅ |

`soul` is **not** here — it is the summoner's player-scoped currency, and `A11`'s discard tax spends it.

> ⚠️ **The plant label "Sun" on `hunger` is the ACTOR pool, not the lawn sun bank.** The lawn bank is
> `pvz.*` and match-scoped, owned by `SimEngine`. Read [resource-hub-ssot.md](../resource-hub-ssot.md) §4
> before writing any UI.

**`resource.max.{id}` / `resource.regen.{id}` / `resource.efficiency.{id}` are registered and have no
reader.** `DerivedStatRegistry.cs:165-171` registers all three in a loop over `ResourceIds`. **This module
is that reader.** It does not add channels.

> ⛔ **The old text asserted `AllCombatChannelIds` is "exactly 84". It is 259+.** That was an *acceptance
> criterion*, so left in place it becomes a red test on day one rather than a confusing sentence.
> Resource channels are their own family and correctly do **not** element-expand.

### 2. Current values are runtime state, and they regenerate lazily

```text
value(now) = clamp(stored + rate * (now - lastTick), 0, max)
```

**No scheduled event per pool per actor.** With six pools across 200 actors that is 1,200 timers that do
nothing but arithmetic. Lazy compute-on-read gives an identical answer.

**At battle end a pool resolves to a concrete value and `lastTick` is dropped** — a persisted `lastTick`
would make a saved actor's pool depend on wall-clock between sessions.

### 3. Paying — validate all, consume all, roll back on any failure

An action costs a **list**. `when` is `onCommit` (default) or `perTick`.

**Committing is what costs, not landing.** An interrupted channel has paid; a fizzled action has paid; a
missed attack has paid. One rule, and it is what keeps slot accounting identical on every exit path.

**Rollback is asserted per pool, never in aggregate** — an aggregate assertion passes when two errors
cancel.

A `perTick` cost that cannot be paid **ends the action through the interrupt path**: cancel remaining
resolves, release the slot, charge `interrupt_cooldown_milli`.

### 4. `poise` — three parts, and each obeys a different rule

Guard is a **stance** (`A8`), and its cost is not one number:

| Part | When | Rule it obeys |
|---|---|---|
| **flat commit** | raising the guard | *committing costs, always* |
| **absorb drain** ∝ what the guard stopped | on each absorb | *output is priced* |
| **per-tick hold** | while held | **termination** — §4.1 |

> The first two are [spec-guard-economy.md](../class-system/spec-guard-economy.md)'s decision C: *"two
> different rules governing two different things, which is what each was written for."*

#### 4.1 The per-tick hold is not optional

Two actors both guarding forever deal and take nothing — `netAttrition ≤ 0` on both sides, which is the
**termination invariant**, and `decisions.md` makes it **blocking**: *"no later layer can repair a pool
that refills faster than it drains."*

`when = perTick` already exists and *"failing to pay ends the action through the interrupt path"* is
shipped semantics, so **a mutual guard resolves arithmetically, with no special case.**

**`poise` at zero is a broken guard, not death** — an exhaustion status, like every pool except `hp`.

### 5. Cost and cooldown ride the rung

```text
cost(rung, Theta)  = anchorCost(Theta) * costMulti(rung)     # ValueSpec, so it scales
cooldown(rung)     = baseCd * cdMulti(rung)                  # ticks. NEVER Theta.
```

Both multipliers come from **`A12`**, never from a literal here.

**Cooldown rides the rung alone.** It is time, not a magnitude — PS-3 does not cover it, and a level-1000
actor waiting 1000× longer is nonsense.

**Cost span exceeds power span** (`A12` §3), so a top-rung action is burst you pay for. `resource.efficiency`
is the bounded 0..1 ratio that reduces it, capped at 1.0 and **`Θ`-free**.

### 6. ⛔ The authoring rule this module owns

> **An action cost is authored against the pool's REGEN, never against its MAX.**
> Sized against the pool a cost looks meaningful and is not.

The measurement that produced it ([class-system-ideal.md](../class-system-ideal.md) §8.1b):

```text
strike   cost 1,544 stamina/round   vs   regen 3,784/round   ->  NEVER runs dry
```

**That defect is NOT this module's to fix.** It is `recovery.scaleMilli = 374` in
[`data/tuning/aptitudes.v1.json`](../../../data/tuning/aptitudes.v1.json) (the POC copy under
`tools/CombatSim/tuning/` is **value-identical** — verified 2026-08-27, only two `_meta` strings differ) — one dial multiplying every regen family, solved against
**peer damage** (correct for `hp`) and inherited by `resource.regen.stamina`, which peer damage does not
oppose. It is already scheduled as `residual-fit`'s second fixed step.

**What we owe is the rule, not the number** — and the rule survives whatever that coefficient becomes,
which is exactly why it belongs here and the coefficient does not.

### 7. Exhaustion is a status, never a hardcoded channel list

Reuses `StatusRuntime` — instances, stacking, resistance, VFX, `icd_ms`. The debuff is a **container of
atoms**.

Two properties that are easy to get wrong and are asserted directly:

- **One status apply, not one per tick**, for a pool held at the threshold with regen trickling. The final
  state is identical either way, which is what hides the bug — so it is **counted**.
- **Exhaustion re-evaluates on read**, proven by crossing the leave threshold **with no write at all**.

**A self-regen cycle is rejected at load** — an exhaustion debuff that reduces the regen of the pool whose
emptiness applied it. The `poise` exhaustion **must not touch poise's own regen channel**.

### 8. ⛔ Open — an item is not a resource

[item/ssot-consumables.md](../item/ssot-consumables.md) §5(b), unanswered and owed by this module:

> *"`rpg_action_cost` is `(action_id, resource_id, amount_spec, when)`, priced against the locked actor
> resources. **A consumable's cost is an item, which is not a resource.** `A3` must either widen
> `resource_id` to admit an item stock row, or state that consuming the item is a **precondition** (`A4`)
> rather than a **cost** (`A3`). Either is fine; leaving it unstated means the first consumable action has
> nowhere to declare what it spends."*

**Recommendation: a precondition, not a cost.** Three reasons, and the third is decisive:

1. `resource_id` is a **closed set of six** and widening it to a polymorphic id is the `effect_binding`
   mistake in a different table (`A1` §5).
2. Rollback semantics differ — a resource rolls back arithmetically; an item stock row is a transaction.
3. **Costs scale with `Θ` and rungs; an item does not.** *One potion* is one potion at every level, so it
   fails the pure-number property the whole cost economy rests on.

That makes the inventory read `A4`'s leaf, which that spec now owes.

### 9. Run lifetime

Pools persist across an encounter and **refill at rest** — a rest is *returning to base*, a run is *a
sortie away from it*. `hp` included.

**No run row means a run of one** — the skirmish case, which is both correct and the easiest to test.
**Cooldowns do not cross a battle boundary**; pools do.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionCost"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Resource"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~RunPool"
```

## Structure

```
src/FusionRpg.Core/Actions/Cost/CostLedger.cs        (validate all -> consume all -> roll back)
src/FusionRpg.Core/Actions/Cost/ResourcePool.cs      (lazy regen; struct, zero alloc on read)
src/FusionRpg.Core/Actions/Cost/ExhaustionPolicy.cs  (threshold + hysteresis, via StatusRuntime)
src/FusionRpg.Data/Sqlite/RpgStore.RunPools.cs
tests/FusionRpg.Core.Tests/Actions/ActionCostTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| **Six ids**, asserted directly | a five-resource regression is red |
| **Lazy regen == scheduled regen** | one resolve after 1000 ticks equals a thousand one-tick steps |
| **Zero scheduled events** for five regenerating pools across 200 actors | counted, not asserted in prose |
| Rollback | asserted **per pool** — an aggregate assertion passes when two errors cancel |
| A `perTick` cost that cannot be paid | ends via the interrupt path, releases the slot, charges `interrupt_cooldown_milli` |
| Missed / fizzled / interrupted | **all paid**, all cool down |
| Two mutual guards | **terminate** — the per-tick hold drains both; a planted zero-hold version **hangs**, which is what makes the test worth having |
| `poise` at zero | exhaustion status, **not** death |
| `poise` exhaustion touching `resource.regen.poise` | **rejected at load** — the self-regen cycle |
| Exhaustion apply count | **one**, for a pool held at the threshold with regen trickling |
| Exhaustion leave | re-evaluated **on read**, with no write |
| Cost at `Θ`=20 vs `Θ`=5,000 | scales; **cooldown identical** |
| `resource.efficiency` | bounded 0..1, capped at 1.0, `Θ`-free — asserted at both ends |
| Pools across an encounter boundary | survive; refill at rest, `hp` included |
| Cooldowns across a battle boundary | do **not** survive |
| At battle end | pools resolve to a concrete value and **`lastTick` is dropped** |

## Boundaries

**Always:** validate all before consuming any; roll back per pool; author costs against regen; take
multipliers from `A12`; keep `resource.efficiency` `Θ`-free.

**Ask first:** widening `resource_id`; a seventh resource; changing exhaustion's threshold shape.

**Never:** a scheduled regen event per pool; a persisted `lastTick`; a hardcoded channel list in an
exhaustion debuff; an exhaustion that reduces its own pool's regen; `soul` as an action cost; a cost
multiplier written as a literal in this module.

## Success criteria

1. Six resources, asserted — not five.
2. Lazy regen is provably identical to scheduled regen, with zero timers.
3. Two mutual guards terminate, and a planted zero-hold version hangs.
4. Rollback holds per pool.
5. The item-as-cost question is **answered in writing**, so the first consumable action has somewhere to
   declare what it spends.
