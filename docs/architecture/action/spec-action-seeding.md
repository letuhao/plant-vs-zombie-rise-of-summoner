# Spec: action-seeding (A13)

**Status: proposed 2026-08-27.** Module **A13** in the [action map](../action-map.md). New module, from the
sealed [action-ideal.md](../action-ideal.md) §7–§8 — decisions **4, 13, 15, 16, 17, 20**.

Depends on **A12** (rung table, structure budget) and **A6** (catalog load and validation).
**Depends on seedsmith for nothing.**

> ### ⛔ This is the RUNTIME generator — the loot model, not a content pipeline
>
> **Owner, 2026-08-27:** *"seedsmith is a tool for developing the game, not the running game. The running
> game has its own generator, but it uses generated seed to add atom effects to a list and solve variants
> for concrete effects — **like a loot system in a Diablo-like game**. Seedsmith build order is later,
> after we complete the action feature."*
>
> An earlier draft of this spec cited seedsmith's principles as if they governed it and put a coverage
> metric on seedsmith's queue as a prerequisite. **Both were wrong.** Seedsmith measures a corpus this
> module produces — it cannot gate the feature that produces it, and it does not run in the game.
>
> | | Owns |
> |---|---|
> | **`A13` (here)** | the **runtime roll**: seed → pool → atoms → variant resolution. Deterministic, replay-safe, in Core |
> | **The authored data** | pools, weights, pairings, `sharePermille` — read by the roll, hand-authored |
> | **seedsmith, later** | measuring corpus coverage and balance as a **dev tool**, after this ships |

## Objective

**Actions are seeded, never handcrafted.** This module decides *what a generated action is made of* — its
atoms, its target shape, its conditional structure — so that a demon type's ten unlocks feel like that type
without anyone authoring them one at a time.

The [concrete roster](concrete-action-roster.md) is the hand-authored floor this is measured against, not
the thing this produces.

## Design

### 1. The generator already exists — this module authors its inputs

[spec-container-schema.md](../effect-atom/spec-container-schema.md), **built**:

> *"A container is a named, ordered bundle of atom references, optionally with a **weighted pool it rolls
> from**."* `pool_rolls` · `min_tier`/`max_tier` · `group` — PoE's mod-family rule, so one action never
> rolls `+10 atk / +12 atk / +14 atk`. **"Rarity selects the `pool_rolls` count and the tier window… No
> third mechanism."**

**An unlocked action is a container roll, and its rung is its rarity.** Nothing to build; everything to
author.

### 2. The split — authored data in, rolled instance out

**No number is ever generated.** Magnitudes come from authored `sharePermille` through `numerics`'
arithmetic; the roll chooses **which** atoms and **which** variant, never **how big**.

That is the same discipline seedsmith states as its P1 (*"a model has no calibrated sense of scale, so a
number it picks is a plausible-looking guess that survives review because nothing looks wrong with it"*) —
**cited as a principle this module shares, not as a dependency it has.**

| Authored by hand — read by the roll | Rolled at runtime — deterministic, seeded |
|---|---|
| `sharePermille` per channel — *the entire tunable surface*, and a missing one **rejects rather than defaults** | which atoms, via pool + weights + `group` |
| the rung table (`A12`) | which **variant** of each family |
| per-type weight vectors (§3) | the **target shape** (§4) |
| enabler/payoff pairings (§5) | every magnitude, through `numerics`' arithmetic |
| **name templates** — §2.1 | the composed **name** |

#### 2.1 ⚠️ Identity at runtime is template composition, NOT a model

An earlier draft of this table had *"identity: name, flavour — **LLM**"* in the generated column. **That is
wrong for a runtime roll**: nothing calls a model mid-battle, and a generated action must be named
deterministically or two players with the same seed see different text.

**The loot model already answers this.** *Sharp Sword of the Bear* is affix templates composed by rule, not
prose written per drop. So a rolled action's name composes from its atoms' family templates the same way.

> **The LLM half belongs to the authored corpus, and that corpus is seedsmith's — later.** This module
> composes names from templates it reads; it never generates prose and never calls anything non-deterministic.

**A predicate is neither identity nor magnitude** — it is structure, so it generates deterministically from
a weighted template pool, exactly like atoms.

### 3. A demon type is a weight vector — not a third vocabulary

The action taxonomy is already closed, **twice**:

| Vocabulary | Members | Consumed by |
|---|---|---|
| `action-category` | `attack · defense · support · movement · status` | `skill.cooldown.{category}`, `skill.effectiveness.{category}` |
| `tags_json` | `offensive · defensive · heal · buff · debuff · movement · summon · utility` | `A7`'s selection — *"AI reads tags, never internals"* |

> **A demon type is a weight vector over the five shipped action-categories, plus its element/aspect bias.**
> One small authored row per type. A fire type weights `attack`; a warden type weights `defense`.

**Inventing a third vocabulary is the exact defect the atom program exists to stop.**

### 4. The target spec is rolled too — decision 17

Targeting lives on the **action row** (`target_spec_json`, `min_range`, `max_range`, `anchor_source`), so it
was never a vocabulary gap — it was simply **left out of the generation surface**.

**The same atom list at single-target, at `eachTarget`, and at a `Square` area is three genuinely different
actions.** So the type weight vector weights **target shapes** alongside categories.

⚠️ **`Mode = Area` is rejected at bind time while no board exists** — an area action needs cells to
enumerate. So the shape pool is **board-gated**, and the gate is loud rather than silent.

### 5. ⛔ Enabler/payoff pairing — the constraint pricing cannot substitute for

**Owner, 2026-08-27:** *"rot is one of 21 statuses… a defence demon can be rotted, an attack demon carries a
rot action, and that attack demon can attack that defence demon. So to apply x2 damage on a rotted demon is
not easy."*

E9's four-factor chain prices that difficulty (ideal §8.6). **It does not make the combination exist.**

> **`rot` is 1 of 21 statuses.** Weight a pool's statuses independently and a rot-conditional payoff will
> almost never share a ten-action pool with a rot applier, let alone a five-slot loadout. The discount is
> then **paid for a combination the generator never assembles** — a real price cut for an unreal
> capability, which is worse than not discounting at all.

**So the weight vector carries a second thing: enabler/payoff pairing.** A type whose pool can roll *"double
damage against Chilled"* must also weight *"applies Chill"*. **The pair is the unit, not the action.**

This is what makes a five-slot loadout a **combo** rather than five independent picks, and it is the
generated counterpart of the complexity definition: *an action that references foreign state is only
interesting if something in reach can create that state.*

**The metric, in `budget`'s own terms** — closed-loop, machine-checkable, which is P3's bar:

> **Every conditional payoff in a pool has at least one enabler in the same pool.**

### 6. Complexity is generated, and the rung says how much

**Complexity is predicate usage, not atom count** (ideal §8.1). `A12`'s `structureBudget` says which axes a
rung may spend on, and this module spends it:

| rung band | what the generator may add |
|---|---|
| 1–2 | one atom, no condition |
| 3–4 | a rider status **or** a scope split |
| 5–6 | a **condition** — the first rung referencing foreign state |
| 7–8 | a **sequence** (`resolve_offsets_json`) **or** a **consumption** (`hasStatus` + `status.clear`) |
| 9–10 | a **reaction** (a trigger left behind) **or** a **restriction** (a self-debuff, `scope: caster`) |

**Seven of the nine axes need no new vocabulary.** Only linkage does, and it is a cross-program ask.

### 7. What a seeded action must never be

| Never | Why |
|---|---|
| A number chosen by a model | P1. It is the whole reason this module has an authored half |
| An atom kind, trigger, tag, category or predicate leaf outside its closed set | every list is closed and rejects unknowns |
| A container rolling two halves of a known multiplicative pair | E9 is **knowingly ~12.5% wrong** on crit-rate × crit-damage and the element ring; random generation *will* hit it. Use `group` to exclude |
| A duration authored in seconds for a control family | `A14` owns the conversion; the almanac's seconds are seed text, not authored values |
| A pool offering a conditional payoff with no enabler | §5 |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionSeeding"
python -m seedsmith report --corpus actions
```

## Structure

```
data/seed/actions/type-weights.json            (per-type category, element, shape weights)
data/seed/actions/pairings.json                (enabler <-> payoff)
data/tuning/action-shares.v1.json              (sharePermille per channel - the balance surface)
src/FusionRpg.Core/Actions/Seeding/TypeWeights.cs
src/FusionRpg.Core/Actions/Seeding/StructureBudget.cs
tests/FusionRpg.Core.Tests/Actions/ActionSeedingTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| **Enabler coverage** | every conditional payoff in a generated pool has an enabler in the same pool — **asserted here, in Core, with a planted unpaired pool failing.** Not deferred to a dev tool that does not exist yet |
| A channel with no authored `sharePermille` | **rejected at import, never defaulted** — `numerics` §2's own rule |
| Structure exceeding the rung's budget | rejected, naming the rung and the axis |
| Two halves of a multiplicative pair in one container | rejected by `group` — asserted against a planted crit-rate + crit-damage pool |
| `Mode = Area` with no board | **rejected at bind time**, loudly |
| Same seed, two generations | byte-identical pools |
| A type's ten unlocks | category mix within its authored weights, asserted as a distribution rather than per-roll |
| An unknown atom kind / trigger / tag / leaf | rejected, never skipped |
| A control duration authored in seconds | rejected — `A14` owns the unit |
| Generated pool priced through E9 | every rung's total within its budget, and **monotonic across rungs** |

## Boundaries

**Always:** author shares by hand; generate structure deterministically; pair enablers with payoffs; reject
an unauthored share.

**Ask first:** adding a weight axis; changing the structure bands; letting a model touch a number.

**Never:** a third action vocabulary; a magnitude from a model; an unpaired conditional payoff; a
multiplicative pair inside one container; a default for a missing share.

## Success criteria

1. A type's pool reads as that type, with no per-action authoring.
2. Every conditional payoff has a reachable enabler, asserted — and a planted unpaired pool fails.
3. No generated number came from a model.
4. Rung structure matches the budget, and the generated corpus prices monotonically.
5. Generation is byte-identical for a seed.
