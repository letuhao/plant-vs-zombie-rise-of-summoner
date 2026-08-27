# Spec: rung-table (A12)

**Status: proposed 2026-08-27.** Module **A12** in the [action map](../action-map.md). New module, from the
sealed [action-ideal.md](../action-ideal.md) §4 — decisions **9, 11, 12, 16**.

Depends on **A1** (the `rung` column). Blocks **A11** and **A3**, both of which read this table.

## Objective

**One authored ladder that every faucet reads.** A rung is *how strong this action is relative to your
last one* — a bounded, level-free quality ladder, distinct from `Θ`, which is how strong everything is.

> **This module exists separately because it has two readers.** `A11` maps an earn count to a rung; `A3`
> reads the cost and cooldown multipliers. Two readers of one table is the whole argument for the table.

## Design

### 1. It needs no new power scale — row 7 is the precedent

[power/ssot-power-scale.md](../power/ssot-power-scale.md) §10 is a closed inventory: *"a power-shaped
number that is not in this table does not have permission to exist."* **Row 7 already covers this shape:**

> Affix tier ladder `m_t = m1 × 1.75^(t−1)`, geometric, 5 rungs. *"Bounded at t5 (9.4× total). A
> **within-item quality ladder in relative space** — it never sees a level. §2's theorem does not apply."*

The unlock ladder is the same shape one level up: a **within-demon-type quality ladder in relative space**.
So it is built from two shipped mechanisms — the `1.75` magnitude ladder and `pool_rolls` breadth — rather
than a third.

**`rung = half an item tier`:** `qPower(r) = 1.75^((r−1)/2)`, so two rungs are one shipped tier. Nothing
new is invented; `1.75` is `bands.v1.json`'s `magnitudeRatioPerMille`.

> ⛔ **The exponent form documents how the authored numbers were DERIVED. It is never evaluated at
> runtime.** `1.75^((r−1)/2)` is irrational at every odd rung, and the power SSOT §9.4 plus the world map's
> byte-identical replay lock both forbid floating point on a magnitude path. **A human evaluates it once and
> stores per-mille integers**; `Math.Pow` must not appear in `Core/Actions/Rungs/`, and a test asserts it.

### 2. The table

`data/tuning/action-rungs.v1.json`. **The cap is the row count**, so changing it is deleting rows and
re-authoring the survivors to span the same range.

| Column | Means |
|---|---|
| `rung` | 1…cap, ordinal, contiguous — a gap is a load rejection |
| `minTier` / `maxTier` | the pool tier window (`effect_container` columns) |
| `poolRolls` | how many atoms a *seeded* action draws. `0` for a concrete action |
| `costMulti` ‰ | multiplies `rpg_action_cost.amount_spec` |
| `cdMulti` ‰ | multiplies `cooldown_ticks` |
| **`structureBudget`** | which complexity axes this rung may use — §4 |

**Authored, not computed**, because the ordering of `(tier, rolls)` pairs is a balance decision. **Machine
checked**, because it must climb — §5.

### 3. ⛔ Cost span exceeds power span — this is the module's balance rule

```text
qPower(r) = 1.75^((r-1)/2)     # 1.32 per rung
qCost(r)  = 1.38^(r-1)          # 1.38 per rung  -- deliberately larger
qCd(r)    = 1.15^(r-1)
```

Across rungs 2→10: **power ×9.38, cost ×13.15 — a 1.40× escalation tax.**

| If | Then |
|---|---|
| cost span **=** power span | a top rung is a bigger rung 1 at identical efficiency — you always equip your five highest, and the loadout is a **sort**, not a decision |
| cost span **<** power span | high rungs strictly dominate |
| **cost span > power span** | high rungs are **burst you pay for**; low rungs stay sustain |

> **This is where FOCUS lives.** `resource.efficiency` and `skill.cooldown.*` are exactly the two
> multipliers this ladder taxes, so a Focus build runs more high rungs at the same pool. It also makes
> three of Focus's largest coefficients measurable for the first time — the class system records them as
> unmeasurable *"because neither engine has cooldowns"*.

**The value is a tunable; the metric is declared now.** Seedsmith P2: *"a metric without a declared target
is an opinion."*

> **Metric: the share of equipped loadouts that mix rungs.** All five at top rung → the tax is too low.
> Nobody equipping top rung → too high. A healthy mix is the target. Starting value **1.5× power span**.

### 4. ⛔ A rung buys STRUCTURE, not only numbers

**The correction that matters most in this module.** With multipliers alone, a rung-10 action **plays
identically** to a rung-2 one and merely hits harder — shallow complexity built into the ladder.

`structureBudget` is a closed set of axis ids ([ideal §8.2](../action-ideal.md)):

**`structureBudget` is a column on each row, not a band.** The shape below is **the shipped 10-row
ladder** — an illustration of one authored table, not a rule. At `cap = 8` or `cap = 15` the rows carry
different budgets and the ranges below do not apply.

| rung band *(shipped 10-row shape)* | axes unlocked |
|---|---|
| 1–2 | one atom, no condition — the plain verb |
| 3–4 | `scopeSplit` **or** a rider status |
| 5–6 | **`condition`** — the first rung that references foreign state |
| 7–8 | **`sequence`** (multi-offset) **or** `consumption` |
| 9–10 | **`reaction`** **or** `restriction` |

**A rung-10 action is a different kind of thing from a rung-2, not a bigger one.** That is what Last
Epoch's deep nodes do — *"can even transform them entirely"* — and it is what gives `A11`'s keep-or-discard
decision something to weigh beyond a number.

`A13` reads `structureBudget` when it generates; `A6` rejects an authored action whose structure exceeds
its rung's budget.

### 5. The monotonicity assertion — what makes an authored table safe

A test prices **every rung** through E9's `PowerVector` and **fails if rung *u+1* is not worth more than
rung *u***.

> A designer picks the sequence; **arithmetic proves it climbs.** Without this, an authored ladder is a
> list of numbers nobody checked, and a non-monotonic rung would make `A11`'s whole progression a lie.

**This assertion depends on E9 pricing predicates**, and the build order guarantees it. Until
`power_predicate_frequency` lands, a conditional atom prices as though its condition always holds — so a
rung spending its `structureBudget` on a condition would price **above** its true worth and the test would
pass **for the wrong reason**.

> **Phase 0 dissolves that** (map §4.3, owner 2026-08-27: *"extend dependencies first, before build
> action"*). Predicate pricing lands before this module builds, so the assertion means what it says from
> day one. **This paragraph stays as the record of why the order matters** — a future session that reorders
> it reintroduces a test that cannot fail for the reason it claims.

### 6. One table, many faucets

Every source of actions needs a rung, and that must not become three private curves.

| Source | Rung from | Capped? |
|---|---|---|
| Demon-type levelling (`A11`) | `min(earnCount, cap)` | **cap**, tunable |
| Item grant | the item's rarity / tier ladder | no |
| Passive skill, variant | that system's own tier | no |
| Future mechanisms | **declare a mapping at registration** | no |

**A new mechanism that grants actions declares its mapping; it does not invent a rung scale.**

### 7. What the rung must never do

> **`Θ` makes everything bigger; the rung makes *this* action better than your last one. They multiply
> once and never again.**

```text
value(rung, Theta) = anchor(Theta) x q(rung)
anchor(Theta)      = sharePermille * P(Theta) / 1000
```

⛔ **PS-4 applies directly: the rung ladder must NEVER be multiplied by `contentScale`.** The anchor
already did it. This is the mistake that rule exists to catch, and a rung ladder is a bigger blast radius
than an affix one.

**Cooldown rides the rung alone, never `Θ`.** A cooldown is ticks — neither contest nor magnitude, so PS-3
does not cover it, and a level-1000 actor waiting 1000× longer is nonsense.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~RungTable"
python scripts\audit-magic-numbers.py --domain action-rungs
python scripts\audit-overflow.py
```

## Structure

```
data/tuning/action-rungs.v1.json                      (the authored ladder)
src/FusionRpg.Core/Actions/Rungs/RungTable.cs         (parse, index, reject)
src/FusionRpg.Core/Actions/Rungs/RungMultipliers.cs   (readonly struct; no dictionary at resolve)
tests/FusionRpg.Core.Tests/Actions/RungTableTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| **Monotonic E9 price across every rung** | pass — and a **planted inverted row fails**, which is what makes the test worth having |
| Cost span vs power span | `qCost(cap)/qCost(2) > qPower(cap)/qPower(2)`, asserted directly — a regression to a flat tax is red |
| A gap in the `rung` sequence | load rejection naming the missing index |
| `structureBudget` naming an unknown axis | rejection, never ignored |
| An action whose structure exceeds its rung's budget | rejected by `A6` at load |
| Multiplying a rung value by `contentScale` | **an architecture test forbids it** — grep-style, because the failure is silent and PS-4 names it |
| Cooldown vs `Θ` | a test resolves one action at `Θ=20` and `Θ=5000`: magnitude moves, **cooldown identical** |
| Changing the cap | deleting rows re-spans; a test asserts the top rung's multiplier after a 10→8 edit |
| Zero rows | rejection — an empty ladder is not a valid ladder |
| Resolve-path allocation | **zero bytes**; `RungMultipliers` is a readonly struct, no dictionary lookup |

## Boundaries

**Always:** keep the table data; keep `rung` an ordinal index; assert monotonicity in CI; keep every
multiplier per-mille integer.

**Ask first:** changing `1.75`; changing the cost or cooldown ratios; adding a `structureBudget` axis.

**Never:** multiply a rung by `contentScale`; let cooldown read `Θ`; put a multiplier in code; let a rung
be a magnitude rather than an index.

## Success criteria

1. The ladder is one authored file, and CI proves it climbs.
2. A planted inverted rung **fails** the monotonicity test.
3. Cost span exceeds power span, asserted as a number rather than described.
4. A rung-10 action differs from a rung-2 in **structure**, not only magnitude.
5. Two readers (`A11`, `A3`) resolve identical multipliers for the same rung.
