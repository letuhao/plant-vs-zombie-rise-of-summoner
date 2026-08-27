# Spec: unlock-ladder (A11)

**Status: proposed 2026-08-27.** Module **A11** in the [action map](../action-map.md). New module, from the
sealed [action-ideal.md](../action-ideal.md) §3 — decisions **5, 6, 7, 8, 24**.

Depends on **A12** (the rung table). Blocks **A15** (the grant seam reads the assembled set).

## Objective

**How an actor comes to hold an action, and what it costs to change its mind.**

A demon type levels; each level rolls for an unlock; unlocks arrive stronger as you go; you may hold only
so many, so accepting one more means giving one up. That decision — **keep or discard** — is the module.

## Design

### 1. One counter, and that is the whole anti-exploit

> **`earnCount` counts EARNS — successful acquisitions, and only when a slot was free to take them.**
> Owner, 2026-08-27: *"success only, only count when still have slot — the name earn count means you
> earned, then count."* A failed roll costs nothing. A roll arriving with no free slot is **not an earn**
> and does not advance the ratchet.
>
> **Consequence for the endgame:** discard is a **deliberate act to open capacity**, not a reaction to an
> incoming unlock. You free a slot first, and the next earn lands in it. §5's earlier framing — *"every
> earn after cap forces a discard"* — had the causation backwards.

```text
earnCount        monotonic. Never decrements, never resets.
                 += 1 ONLY on a successful acquisition into a free slot
chance(n)        = max(floor, p1 * delta^(n-1))
rung(n)          = min(earnCount, cap)          -> A12
holding          <= cap; accepting one more requires a discard
```

> **Your chance only ever falls, and your rung only ever rises.** Neither can be gamed, because **discard
> moves neither.**

**Occupancy is not math.** It is only the `< cap` check at the moment of acceptance. An earlier draft keyed
the rung on occupancy; that was retracted, because a slot that remembers its rung freezes an unlucky early
roll permanently — a progression ceiling wearing a different hat, which PS-8 refuses.

Starting values, all four tuning rows in `data/tuning/action-unlock.v1.json`:

| `p1` | `delta` | `floor` | `cap` |
|---|---|---|---|
| 50% | 0.88 | **0.1%** | **10** |

| earn | 1 | 10 | **11** | 20 | 25 | 40 | 50 |
|---|---|---|---|---|---|---|---|
| chance | 50% | 15.8% | **13.9%** | 4.4% | 2.3% | 0.34% | 0.1% ← floor |

> **A single geometric cannot independently pin "still meaningful at 40" and "floored at 50."** Tightening
> one loosens the other. `0.88` is the closest single value; pinning both needs two segments, and that is a
> config change rather than a code one.

### 2. Why the cap is 10, and why the cap is legal

At **cap 25** the first forced discard is earn #26, where chance is ~2% — a decision made twice a year. At
**cap 10** it is earn #11, where chance is still **~14%**: the keep-or-discard tension becomes part of
normal levelling instead of an endgame footnote.

**It also settles the depth question** — at 10 held against 5 equipped the pool is a **bench**, not a
warehouse.

**PS-8 says a cap on a magnitude is a progression ceiling until proven otherwise.** Three arguments, in
order of strength, and the third is the one that actually holds:

1. **It caps a count, not a magnitude.** A limit on how many things one faucet grants is structural, like
   `pool_rolls <= distinct drawable groups`.
2. **The total action pool is uncapped**, because paid sources are (§4). Same shape as the caps register's
   world-size row: *"world size stops; world count does not, and that is the axis."*
3. **Power per fight is bounded by the 5 equipped slots regardless of pool size.** **An uncapped pool grows
   the choice, never the power.**

> **A comment at the declaration must say which of these it is**, per PS-8's exemption rule. It is a
> structural count limit, and the growth axis is elsewhere.

### 3. Discard — a respec of one unlock

Reuses `RespecPolicy`'s shape rather than authoring a second pricing mechanism: ***always available, always
priced, never on a cooldown, never capped.***

**Flat tax, paid in `soul`** (owner, 2026-08-27) — summoner-scoped currency for a summoner-scoped decision.

> ⛔ **"Always available" has exactly one exception, and it is not a new rule.** `A15` freezes the action
> set at run start, so a mid-run discard would leave the frozen set holding an action the actor no longer
> owns. **Discard is available out of a run and refused during one**, with a typed reason — the same shape
> as the shipped equip gate (`phase != Roster` refuses equip). One freeze moment, not two.

**A rung-scaled tax was considered and retracted:** the farm it priced against does not exist, because
chance keys on earn history and never rewinds. A scaled tax would have double-charged the same behaviour.

**Three compounding brakes, and two already ship:**

| | Brake | State |
|---|---|---|
| 1 | **The chance ratchet** — never rewinds | new; does the real work |
| 2 | The discard tax — flat, `soul` | new; one policy |
| 3 | The levelup cadence — `XpToNext(L) = first + (L−1)·step` | **ships**. Power SSOT §10.1 row 6 keeps it as *"the **cost** ladder, not a power ladder"* |

**None is a wall. Each is a price** — §11.1a's *"a cap is a cliff; the continuous instrument is the real
control"* holding across all three.

**Discard is not a reroll**, in mechanism rather than in wording: it frees a slot and costs a payment, and
the ratchet makes the next attempt strictly more expensive than the last.

### 4. Only the free faucet is capped

Actions from items, passives, variants and future mechanisms are **uncapped, because they were paid for.**
That is [power/ssot-power-scale.md](../power/ssot-power-scale.md) §11.1a verbatim, on removing `MaxSlots`:
*"The hard cap was redundant. Scarcity came from the **escalating price**, not from the ceiling."*

> ⛔ **"Do paid sources share the cap?" is a malformed question**, retracted 2026-08-27: a cap cannot
> contain an uncapped set. The `cap` counts **levelling unlocks**; paid grants are a different faucet with
> a different limiter, and `A15` owns how they assemble.

### 5. What the endgame looks like

Earns 1…cap fill the pool at rungs 1…cap. **At cap, nothing lands until you free a slot** — and when you
do, the next earn arrives at the **top rung**. So the pool converges upward and **the floor rises rather
than the ceiling**, one deliberate discard at a time. At the 0.1% tail
against a rising XP cost that is on the order of a thousand levels per upgrade: endless grind behaving as
the SSOT asks, always advancing and never finished.

**Discarding a low rung early is profitable** — dump a rung-3 at earn 10 and the refill is rung 10. That is
the intended upgrade loop, not an exploit, and it is self-limiting because every attempt burns an earn the
chance never returns.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~UnlockLadder"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~UnlockStore"
python scripts\audit-magic-numbers.py --domain action-unlock
```

## Structure

```
data/tuning/action-unlock.v1.json                       (p1, delta, floor, cap, discardTax)
src/FusionRpg.Core/Actions/Unlock/UnlockLadder.cs       (chance, rung, accept, discard)
src/FusionRpg.Core/Actions/Unlock/UnlockState.cs        (earnCount + held set; NO occupancy math)
src/FusionRpg.Data/Sqlite/RpgStore.ActionUnlocks.cs
tests/FusionRpg.Core.Tests/Actions/UnlockLadderTests.cs
```

## Testing strategy — the ratchet is what needs proving

| Case | Expect |
|---|---|
| **Discard, then re-earn** | chance is **not** restored — asserted directly against the pre-discard value. **This is the anti-farm test; without it the module has no teeth** |
| Rung after a discard | `min(earnCount, cap)` — **not** occupancy. A planted occupancy-keyed implementation fails |
| Chance at earns 1, 11, 40, 50 | matches the table; earn 50 is **at** the floor, not below |
| `floor = 0` | **rejected at load** — a zero floor is a hard progression ceiling, and PS-8 forbids it |
| Accept at `holding == cap` | refused with a typed reason naming the cap, never silently dropped |
| Accept at `holding < cap` | succeeds, no tax |
| Discard | always available, always priced, **never on a cooldown**, never capped — all four asserted |
| Insufficient `soul` | typed refusal naming `soul`, and **no state changes** |
| Earn beyond `cap` | arrives at the **top** rung, forcing a discard |
| Determinism | same seed + same earn sequence → identical unlocks across two runs and **across a shuffled held-set order** |
| Round-trip | `earnCount` and the held set persist; **a test asserts no column stores a resolved rung value** — the rung is derived, never stored |
| Overflow | `earnCount` is `long`; `audit-overflow.py` clean |

## Boundaries

**Always:** derive the rung from `earnCount`; keep the four dials in tuning; price every discard; reject a
zero floor.

**Ask first:** changing `cap`; adding a second counter; making discard conditional on anything.

**Never:** let discard restore chance; key the rung on occupancy; store a resolved rung; cap a paid faucet;
put `p1`/`delta`/`floor`/`cap` in code.

## Success criteria

1. Discard never rewinds the chance, proven by a direct assertion.
2. A planted occupancy-keyed rung implementation **fails**.
3. `floor = 0` is refused at load, with PS-8 named in the message.
4. The held set and `earnCount` persist; no rung is ever stored.
5. Replay is identical across runs and across held-set order.
