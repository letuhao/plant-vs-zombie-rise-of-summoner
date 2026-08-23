# Power scale — SSOT

**Status:** Proposed 2026-08-23. First document in the power program. Nothing is built.

The single formula that converts *where a thing came from* into *how strong it is*. Every system
that produces a magnitude — items, enemies, rewards — reads this and nothing else.

---

## 1. The idea, in one line

**Content declares relative power. The power scale converts it to absolute.**

A chainmail is "+50 to +100 armour" forever, at every point in the game. What changes is the scale
it is resolved against. The item system never learns what level the player is, what map they are on,
or how many runs they have played — it balances against itself, and this document multiplies.

That separation is the whole point. It means the item corpus can be finished, validated and balanced
**before the progression system exists**, and survives its redesign without a single content edit.

---

## 2. The axes — and one correction to the proposal

The owner proposed three variables: **player level**, **map**, **run count**, modelled on Diablo 2
running two ladders at once.

Two of those already exist. `ssot-generation.md` §4.1 defines `contentLevel`, and its sources
already include `world sector → sectorLevel(danger_band)` (the map axis, owed by the world program)
and `PvZ run → min(mappedRunLevel, playerLevel)` (the run axis).

The third — **player level — is deliberately excluded**, and the existing rationale is worth quoting
because it is correct:

> *"Item level is a property of the content, never of the player. Player level enters the formula
> nowhere. Why content and not player: 'where do I farm this?' must always have an answer. The
> moment item level tracks player level, every piece of content yields the same gear and the map
> flattens."*

**Diablo 2 agrees, and it is the proposal's own reference.** In D2 an item's level comes from the
*area level* of where it dropped. Character level never improves a drop; it gates whether you can
equip one. D2's two ladders are **difficulty → area level** (drives loot) and **character level**
(gates use) — which is exactly the split already written down here.

So this SSOT keeps three axes, with player level in the role D2 gives it:

| Axis | Symbol | Domain | Drives | Bounded? |
|---|---|---|---|---|
| **Map / sector depth** | `M` | 1 … ∞ | power scale | no — the world program extends it |
| **Run count** | `R` | 1 … ∞ | power scale | no — every run raises difficulty |
| **Player level** | `L` | 1 … ∞ | **access only** (`level_req`), never magnitude | no |

### 2.1 Where infinite growth actually comes from

The uncapped-level constraint is satisfied without putting `L` in the formula, and this is the part
worth being explicit about.

Growth comes from **content difficulty being unbounded**, not from player level. `M` and `R` are both
open-ended, so `contentLevel` rises forever and gear rises with it. A level-1000 player farming
map 1 still gets map-1 loot — which is the property that keeps the map meaningful — while a player
pushing deeper or replaying more finds strictly better gear, forever.

Putting `L` in the formula would give unbounded growth *and* destroy the map in the same stroke: it
is the one change that makes every piece of content yield identical rewards.

---

## 3. The formula

```
contentLevel  = mapLevel(M) + runBonus(R)
powerScale(M, R) = scaleAt(contentLevel) / scaleAt(calibrationLevel)
finalValue    = round(baseValue × powerScale × 1000) / 1000      # integer per-mille throughout
```

`baseValue` is whatever the content system rolled inside its own declared range — for items, a value
in `[lo_t, hi_t]`, which `numerics` already produces. **The roll happens in relative space; the
scale is applied after.** So the owner's example resolves exactly as intended:

| Drop | roll in range | scale | result |
|---|---|---|---|
| chainmail @ M1 R1 | +75 armour | ×1.00 | **+75** |
| chainmail @ M5 R40 | +75 armour | ×N | **+75 × N** |

Same item, same range, same relative quality. Only the scale differs.

### 3.1 Three curves, each replaceable

| Curve | Shape | Why |
|---|---|---|
| `mapLevel(M)` | **owed by the world program** | Sector depth → level. Named in ssot-generation §4.1 as `sectorLevel(danger_band)` and still unmapped. |
| `runBonus(R)` | **sub-linear, unbounded** — e.g. `k · log2(1 + R)` or `k · √R` | Run 40 must beat run 1, but run 400 must not trivialise map depth. Linear in `R` would make grinding one easy map strictly better than advancing, which kills the map a second way. |
| `scaleAt(level)` | **the shared power curve** | Must be the *same function* enemies scale by, or the two ladders drift apart. |

None of these numbers is fixed here. This document fixes the **shape and the composition**; the
constants live in a tuning file (§5) exactly as `tier-bands` does for items.

### 3.2 The one property that must hold

**`scaleAt` is shared by items and enemies.** If gear scaled on one curve and opposition on another,
the difficulty of the whole game would be the accidental ratio of two independently-tuned functions,
and nobody could reason about it. One curve, applied to both, means difficulty is set by *encounter
design* — how many enemies, at what depth — which is a designer's decision rather than an emergent
artefact.

This also answers the decay problem that unbounded levels create. Because item magnitudes and enemy
magnitudes both scale by `scaleAt(contentLevel)`, gear keeps a constant *relative* contribution
however deep the game goes. Nothing asymptotes to irrelevance.

---

## 4. What each system owes

| System | Owes | Must never |
|---|---|---|
| **Item system** | relative ranges (`lo_t`, `hi_t`) at the calibration point | know `M`, `R`, or `L` |
| **World / map** | `mapLevel(M)` | scale item values itself |
| **Run / PvZ** | `R`, and the run→difficulty mapping | scale item values itself |
| **Power (this doc)** | `powerScale`, and the shared `scaleAt` | decide *which* items drop |
| **Progression** | `L`, and `level_req` gating | enter the magnitude formula |

**One multiplication, one place.** A magnitude is scaled exactly once, here. A system that applies
its own multiplier on top re-introduces the coupling this document exists to remove, and the result
is the classic bug where two teams each apply a 1.5× and nobody can find the 2.25×.

---

## 5. Tuning and integer safety

Constants live in `data/tuning/power-scale.v{n}.json`, versioned like `tier-bands`. Changing the
game's whole power curve is one file plus a re-resolve; **no content changes**, because no content
stores an absolute number.

**Overflow is a real constraint, not a footnote.** With `M` and `R` both unbounded, an exponential
`scaleAt` reaches int64 (~9.2×10¹⁸) quickly — at 2% per level, level 3,200. Three options, and the
choice belongs to the progression design:

1. **Sub-exponential `scaleAt`** (polynomial): never overflows in practice, but late progression
   feels flat.
2. **Exponential plus big-number presentation** (the idle-game convention): keeps growth exciting,
   needs a mantissa/exponent representation and UI that formats it.
3. **Exponential with a soft cap**: growth slows past a threshold.

This SSOT does **not** pick one; it requires that whichever is picked declares its overflow
behaviour, and that `powerScale` asserts its output is representable rather than silently wrapping.

---

## 6. Open — owed by other programs

1. **`mapLevel(M)`** — the world program. Sector depth to level. Blocks nothing today because the
   item corpus is scale-free.
2. **`runBonus(R)` shape and constants** — needs the run cadence to be known.
3. **`scaleAt` shape** — the overflow decision above; the progression design owns it.
4. **Does `R` reset?** Per save, per season, never? Changes whether `runBonus` needs a soft cap.
5. **Enemy count vs scale.** Depth could add enemies, or stronger enemies, or both. Encounter design,
   but it changes how steep `scaleAt` needs to be.

None of these blocks the item system, which is the point of writing this document before they are
answered.
