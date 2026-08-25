# Spec — `stat-taxonomy`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** nothing · **Unblocks:** every other module
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Make the counterbalance rule executable instead of remembered.**

The rule — *every combat derived stat is one half of a pair, one side raising the attacker's result and
one lowering it* — has been held in the owner's head. It is correct, it decided six open questions in
one pass, and it caught a real defect in a proposal that had already been written down
([actor-hub-ssot.md §H.0](../actor-hub-ssot.md)). Nothing enforces it. The next person to add
`combat.something` gets no signal at all.

Ten modules depend on classifying stats correctly. This one turns the classification into a type, a
guard, and three written rules that decide the cases arguments would otherwise re-litigate.

**Success looks like:** a contributor adds a contest-class family with no counterpart and CI stops
them, naming the missing half.

**Users:** contributors adding derived channels; the battle, action, shield and atom programs, which
all currently re-derive "does this need a pair?" from scratch.

---

## 2. What it defines

### 2.1 The four classes (normative)

| Class | Definition | Pair | Cap posture |
|---|---|---|---|
| `Contest` | Two actors' values meet in one roll or one delta | **Required** | **Neither half capped** — see 2.2 |
| `Race` | Both actors want the same direction; advantage is being ahead | **Forbidden** | Uncapped; may need a **floor** (2.4) |
| `Pool` | One actor's own capacity or rate | None | Uncapped; depletion is the limit |
| `Feeder` | Modifies a quantity contested downstream | **Inherited** — see 2.3 | Follows the quantity it feeds |

`Race` forbids rather than merely omits a pair: a "make the enemy slower" stat is a **status**, not a
channel. Shipping one as a channel creates a second way to express slow, and the repo has paid for
duplicated expression before.

### 2.2 Pairs are differences, not opposed multipliers

```text
delta = attackerSide − defenderSide        →  sigmoid, as combat.power − combat.defense already does
```

**Both halves stay uncapped.** This is not a stylistic choice. Two *opposed multipliers* force a cap
on at least one side to stay bounded, and a capped defender half against an uncapped attacker half is
a defender-side progression ceiling that loses by construction as `Θ` rises — PS-8's exact failure
shape. A difference has no such pressure and satisfies PS-8 on both halves at once.

### 2.3 The mitigation-order rule — what decides `Feeder` vs `Contest`

Read off the shipped pipeline in [combat-damage-ssot.md](../combat-damage-ssot.md) §6.7:

```text
powerAdjustedDamage = baseDamage + weightedDelta        ← power − defense lands HERE
finalDamage         = max(0, powerAdjustedDamage)
if crit: finalDamage = finalDamage × critMultiplier     ← AFTER mitigation
```

> **A modifier applied *before* mitigation is `Feeder` — `combat.defense` already answers it.
> A modifier applied *after* mitigation is `Contest` and must carry its own counterpart.**

This is *why* `crit.damage` ships with `crit.resist.damage` rather than inheriting: its multiplier
lands after the delta, so `defense` never sees it. The rule is readable off shipped code, not a
convention — which is what makes it settle future cases without a debate.

**Consequence carried into `skill-modifiers`:** `skill.effectiveness.*` is `Feeder` **only while it
stays pre-mitigation**. Moving it after mitigation obliges a `.reduction` half. The placement *is* the
pair, and relocating it later is a breaking change, not a refactor.

### 2.4 The divisor rule

[battle-turn-ideal.md:153](../battle-turn-ideal.md) computes
`nextReadyTick = now + (BaseCost × ActionRank × HasteFactor) / Speed` — a `Race` stat in a denominator.

> **A `Race` stat used as a divisor requires a floor above zero. That floor is a *structural limit* —
> division by zero is a crash, not a balance outcome — so it is PS-8 exempt and must say so in a
> comment.**

The overflow concern inverts for a denominator: the hazard is a very *small* value, not a large one.
Registered in [power/ssot-power-scale.md](../power/ssot-power-scale.md) §11.4 (recursion and
termination guards), not §11.2 (progression ceilings).

### 2.5 `StatClass` and `UnitClass` are two axes, not two schemes

**Corrected 2026-08-24.** An earlier draft of this spec proposed classifying channels as
`magnitude` / `bounded-ratio` / `structural`. **That was a third classification of a thing that already
has two**, and it is exactly the defect this program exists to remove.

[design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3 ships a **`UnitClass`
ledger, each class verified against its consumer in `src/`** and already bound in the web contract
(`ActorChannelDetail.unitClass`):

`GameUnits` · `GameUnitsPerSecond` · `SigmoidPoints` · `SigmoidMultiplierPoints` ·
`StatusPotencyPoints` · `PerMilleRatio` · `Milliseconds` · `Count` · `Flag` · **`LadderIndex`**

It shipped with nine. **`LadderIndex` is the tenth, added 2026-08-24** because this program found that
`Θ` — the most load-bearing derived channel in the game — had no class and could not be expressed in a
contract whose `unit` field is required. Added to **that** spec by its owner's authorisation, not
redefined here.

The two axes are **orthogonal and both required**:

| | Answers | Owned by |
|---|---|---|
| **`StatClass`** — Contest · Race · Pool · Feeder | *does it need a counterpart?* | this spec |
| **`UnitClass`** — the nine above | *what arithmetic is it, and how does it render?* | `spec-magnitude-and-units.md` |

`combat.power.fire` is `Contest` × `GameUnits`. `status.resist.dot` is `Contest` × `StatusPotencyPoints`.
`status.immune.{tag}` is `Pool` × `Flag`. **Every new channel declares both**, and neither is inferred
from the other.

**This subsumes the retracted split.** `GameUnits` and `GameUnitsPerSecond` are the magnitudes; `Flag`
and `PerMilleRatio` are the bounded ones; and the ledger is sharper than "bounded ratio" where it
matters most — **`SigmoidPoints` are *uncapped inputs to a bounded output*.** `parry.rate` scales with
`Θ` forever while the probability saturates, which is the exact shape a `Contest` half needs to satisfy
PS-8 on both sides. That shape had no name in the retracted scheme.

### 2.6 Magnitude materialization — where invariant 13 actually binds

[ssot-power-scale.md](../power/ssot-power-scale.md) **§10.7 decided `double` stands in stat
composition**: `Increased` and `More` are ratios, and composing them in integers would be wrong.

> **The `long` rule binds the value composition *produces*, not the arithmetic that composes it.**

So a `GameUnits` / `GameUnitsPerSecond` channel materializes as `long` at the boundary it leaves
composition — reaching `EntityStatWriter`, a `DamagePacket`, or `BattleRuleset`. §10.7's one exclusion
stands: **a new `double` magnitude outside the composition path is A1, not A7**, and stays a defect.

### 2.7 §8's rejection rule constrains `catalog-extension`

`spec-magnitude-and-units.md` §3 is explicit: *"a channel whose consumer I could not name does not get
a class, it gets a rejection."*

**All 157 new channels have no consumer at registration time** — that is `catalog-extension`'s whole
point. They therefore cannot carry a `UnitClass` until the module that wires their reader assigns one.
This is not a conflict: it is precisely the sheet's **`no-producer`** state, which already renders as
*"nothing grants this yet"*. Stated here so an implementer does not invent a placeholder `UnitClass` to
get past the rejection — **the rejection is the correct behaviour**, and T5 forbids inventing a default.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~StatTaxonomy"
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-stat-pairs.ps1            # NEW - this module ships it
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --summary
```

---

## 4. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Stats/Derived/StatClass.cs` | **new** — `enum StatClass { Contest, Race, Pool, Feeder }` |
| `src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs` | `DerivedStatDef` gains `StatClass Class`, `UnitClass? Unit` and `string? CounterpartOf`. **`double` fields unchanged** (§2.6). `UnitClass` is the ledger's enum, referenced not redefined (§2.5) |
| `scripts/guard-stat-pairs.ps1` | **new** — the guard in §6.2 |
| `data/seed/derived-stats/catalog.json` | each entry gains `class` and `counterpart` |
| `docs/architecture/actor-hub-ssot.md` §H.0 | already drafted; this module makes it normative and links here |
| `docs/architecture/power/ssot-power-scale.md` §11.4, §11.6 | register the divisor floor and every bounded-ratio cap |

**No behaviour changes.** Classification is metadata; no formula reads it in this module.

---

## 5. Code style

Match the existing catalog file. Classification is declared where the channel is registered, never
inferred from the name:

```csharp
// Contest: both halves uncapped - a capped defender half is a progression ceiling (PS-8, taxonomy 2.2).
// Unit: SigmoidPoints - the POINTS are uncapped; the probability they feed saturates. That is the
// shape that satisfies PS-8 on an avoidance stat, and it is the ledger's name for it, not ours.
Register(new(CombatParryRate, FlatSum, 0,
             Class: StatClass.Contest, Unit: UnitClass.SigmoidPoints,
             CounterpartOf: CombatParryBreak));

// Unit unassigned until a reader exists - spec-magnitude-and-units.md SS8 rejects a class for a
// channel with no nameable consumer, and T5 forbids inventing a placeholder to get past it.
Register(new(CombatBlockRate, FlatSum, 0,
             Class: StatClass.Contest, Unit: null, CounterpartOf: CombatBlockBreak));
```

Every `Cap` value carries a comment naming its PS-8 class. A bare `0.95` is indistinguishable from a
ceiling, which is exactly what §11's sweep kept missing.

---

## 6. Testing strategy

### 6.1 Unit

| Test | Asserts |
|---|---|
| `EveryContestFamilyHasACounterpart` | Over `AllRegistered` — every `Contest` names a `CounterpartOf` that itself resolves |
| `CounterpartsAreSymmetric` | If A names B, B names A |
| `RaceFamiliesDeclareNoCounterpart` | `Race` with a counterpart fails — the rule forbids, not merely omits |
| `ContestHalvesAreUncapped` | No `Contest` channel of class `magnitude` carries a `Cap` |
| `EveryCapIsClassified` | Every non-null `Cap` names its PS-8 class in a comment — bounded ratio or structural |
| `UnitClassIsReferencedNotRedefined` | An architecture test: this program declares **no** unit enum of its own. The nine come from the ledger (§2.5) |
| `NoPlaceholderUnitClass` | A channel with no reader carries `Unit: null`, never a guessed value (§2.7) |
| `ShippedFamiliesClassify` | All 99 current channels classify, **including the two unpaired shield pools** — `shield.capacity` and `shield.regen` must land in `Pool`, not fail |

### 6.2 Guard — `guard-stat-pairs.ps1`

Fails closed on: a `Contest` family with no counterpart; an asymmetric pair; a `Race` family with one;
a capped `Contest` magnitude. **Planted-violation test required** — a guard never proven to fail is
not evidence. Follows `guard-power.ps1`'s shape; wires into `deploy-play.ps1` and CI.

### 6.3 Golden

**None should move.** Metadata only. `git status tests/` clean is an acceptance criterion, in the same
role Checkpoint 2 gave it in the power program.

---

## 7. Boundaries

**Always**
- Classify at the registration site; never infer class from a channel name.
- Every `Cap` carries a comment naming its PS-8 class.
- Cite [combat-damage-ssot.md](../combat-damage-ssot.md) §6.7 when placing a modifier — the order is the rule.

**Ask first**
- Any new class beyond the four. Four covered every channel in a 157-channel proposal; a fifth is more likely a misclassification.
- Reclassifying a *shipped* channel — that changes behaviour, unlike classifying a new one.

**Never**
- **Define a third classification of a channel.** `statClass` and `unitClass` are the two, and only one of them is ours (§2.5).
- Invent a `unitClass` for a channel with no nameable consumer (§2.7).
- Widen `DerivedStatDef`'s `double` composition fields to `long`. §10.7 decided this; the `long` rule binds composition's **output** (§2.6).
- Cap one half of a `Contest` pair.
- Ship a `Race` counterpart channel — that is a status.
- Register `turn.speed` / `turn.haste` / `turn.moveSpeed`. Battle stream owns them; they classify as `Race` here without registering.

---

## 8. Success criteria

- [ ] Four classes normative in code, `actor-hub-ssot.md` §H.0 and the seed catalog — one definition, three renderings.
- [ ] **`UnitClass` referenced, not redefined** — no third classification scheme anywhere in the program.
- [ ] Channels with no reader carry `Unit: null`; no placeholder invented.
- [ ] All **99** shipped channels classify; `shield.capacity`/`shield.regen` land in `Pool`.
- [ ] `guard-stat-pairs.ps1` fails on **four** planted violations, one per rule in §6.2, and passes clean on `main`.
- [ ] §2.3, §2.4, §2.5 written where their subsystem looks: §6.7 pipeline order, §11.4 divisor floor, §11.6 ratio caps.
- [ ] `git status tests/` clean — **zero goldens moved**.
- [ ] `audit-overflow.py` and `audit-magic-numbers.py` unchanged from baseline.

---

## 9. Open questions

**None.** §2.3 and §2.4 are derived from shipped code (§6.7's order; `battle-turn-ideal.md:153`'s
formula) rather than chosen, and §2.5 is quoted from a decided section. The one recommendation this
module originally carried — widening `DerivedStatDef` to `long` — was **falsified by reading §10.7**
and is corrected in [actor-hub-ssot.md](../actor-hub-ssot.md) §H.8 R5.
