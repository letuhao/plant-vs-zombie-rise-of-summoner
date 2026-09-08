# Spec: type-weights (A-T1)

**Module id:** `type-weights` · **Program:** [action-corpus](../action-corpus-map.md) §4.1 · **Build order:** 3 of 7 model-free
**Status: proposed 2026-09-03.** Written against the capability map; no build authorized until the map is approved.
**Model calls: none.** A weight is a magnitude, and Law 2 puts magnitudes out of a model's reach entirely.

It owns the per-species generation anchor `spec-action-seeding.md` names and which **does not exist**:
a weight vector over the five shipped action categories, plus a target-shape vector and an element
bias. It turns [A-S0](spec-characteristic-pool.md)'s *ordering* into per-mille integers using
coefficients that live in `data/tuning/`. It invents no vocabulary — *"inventing a third vocabulary is
the exact defect the atom program exists to stop"* (`spec-action-seeding.md:101`).

## The four constraints this module is bound by (map §3, restated inline)

1. **Seeds, not a cartesian.** An atom names a **pool**; element, tier and cell resolve at layer 4, per
   player, at roll time. **A cell is a target, never an identity** — so the element bias emitted here
   weights a *pool choice*, never a pre-multiplied concrete channel.
2. **Small-batch proof before any full run.** The call budget is a **ceiling, not a plan.** This module
   spends none of it; its whole value is that the run's anchor is reviewable before a token is spent.
3. **The roster is 84 species, not 904.** So this file has 84 species rows and 19 family rows, and its
   size is a measurement rather than a projection.
4. **C1's family-access widening is gated** on three things that do not exist. A weight vector may not
   be used to smuggle that widening in through the back door: **weights bias which of an already-legal
   pool is drawn, never which pools are legal.**

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| The five closed categories, and `ActionCategories.All` in declared order — the tie-break order this module uses | `ActionEnums.cs:26-33`, `:119-123` |
| Category names are the **same** `DerivedStatChannels` constants, not a second set of literals | `ActionEnums.cs:96-104` |
| Six target modes; the four area shapes apply **only** under `Area` | `ActionTargetSpec.cs:16-32`, `:41-47` |
| `Area` is board-gated at roll time — an `Area` candidate is not even eligible while no board exists | `ActionSeeder.cs:51-53` |
| Six elements | `ActorElementTypes.cs:3-11` |
| Per-species element and rarity for all 84 | `DemonSpeciesCatalog.Generated.cs:14+` |
| The tuning-file shape this module follows (`schemaVersion`, `version`, `_meta`, rows) | `data/tuning/action-rungs.v1.json:1-12` |
| `WeightedChoice.Pick` — the shipped **runtime generator's** weighted pick, over a caller-supplied `targetShapePool` | `ActionSeeder.cs:37,55` |
| `ActionRow.Targeting` — an **authored** `ActionTargetSpec` on the shipped row, compiled once and cached | `ActionRow.cs:40`; `CompiledAction.cs:27` |

### Real gap

| Thing | Evidence |
|---|---|
| `data/seed/actions/type-weights.json` is named in the Structure block and **does not exist** | `spec-action-seeding.md:173`; `ls data/seed/actions/` shows two unrelated files |
| `src/FusionRpg.Core/Actions/Seeding/TypeWeights.cs` is named and **does not exist** | `spec-action-seeding.md:176` |
| Nothing derives a category bias from a species today | grep over `src/` |

`data/tuning/action-shares.v1.json` (`spec-action-seeding.md:175`) **does** exist and is a different
surface — per-channel `sharePermille`. This module does not touch it.

## 2. Inputs and outputs

**Reads:**

| Path | For |
|---|---|
| `data/seed/actions/_generated/role-lean.json` (A-S0) | `leanOrder`, `separation`, `family`, `element`, `rarity` |
| `data/tuning/action-type-weights.v1.json` | **new** — every coefficient in this module |
| `ActionEnums.cs` / `ActionTargetSpec.cs` / `ActorElementTypes.cs` | the closed member lists and their declared order |

**Writes** `data/seed/actions/type-weights.json`, in the A-C1 envelope:

```jsonc
{
  "schemaVersion": 1,
  "kind": "action-type-weights",
  "_meta": { "partition": "type-weights", "tuningVersion": 1, "leanHash": "..." },
  "entries": [
    {
      "id": "weights.species.cherrybomb",
      "scope": "species",                 // species | family
      "scopeKey": "cherrybomb",
      "categoryMilli":  { "attack": 400, "status": 250, "movement": 150, "support": 120, "defense": 80 },
      "targetModeMilli": { "self": 50, "single": 350, "multi": 150, "rolledTarget": 100, "all": 50, "area": 300 },
      "areaShapeMilli": { "row": 400, "column": 200, "square": 300, "rectangle": 100 },
      "elementBiasMilli": { "fire": 600, "ice": 80, "air": 80, "earth": 80, "light": 80, "dark": 80 },
      "basis": "derived"                  // derived | floor
    }
  ]
}
```

**Every value is an integer per-mille and every vector sums to exactly 1000.** No floats anywhere, in
the file or in the code that writes it.

**⛔ CORRECTED 2026-09-03 (review F10).** The keys were PascalCase enum member names (`"Self"`,
`"Area"`, `"Row"`). Every key is the **wire string** the code of record returns — `"self" "single"
"multi" "rolledTarget" "all" "area"` (`ActionTargetModes.Name`, `ActionTargetSpec.cs:103-112`) and
`"row" "column" "square" "rectangle"` (`ActionAreaShapes.Name`, `:134-141`). This file's own §5
"unknown member" test and AC6 assert against those functions, so the earlier example would have failed
both. The `categoryMilli` keys were already correct: they are the `DerivedStatChannels` constants
`ActionCategories.Name` returns (`ActionEnums.cs:96-104`).

**⛔ Who consumes `targetModeMilli` — decided 2026-09-03 (review F5).** The earlier draft implied
these vectors feed `WeightedChoice.Pick` at roll time (`ActionSeeder.cs:55`) while A-S1 authored
`targetMode` into the brief and A-S3 hashed it as mechanical identity — **both could not hold**.
The decision is **authored**, made in `spec-distribution-planner.md` §3 step 4a, and it gives this
module's two shape vectors a real consumer that is not a second roll:

- **A-S1 consumes them at PLAN time**, by largest remainder, the same way it consumes `categoryMilli`
  — `long`, widened before the multiply, divided by 1000 last, exactly once. `areaShapeMilli` is
  consulted only for briefs allocated `area`.
- **`ActionSeeder.Generate` is untouched.** Its `targetShapePool` is a caller-supplied parameter
  (`ActionSeeder.cs:37`), and its pick is the shipped runtime generator's own roll alongside
  `Instantiator.Draw` (`:47`) — a different production path from a corpus action, which binds
  through the authored `ActionRow.Targeting` (`ActionRow.cs:40`). **Nothing new rolls**, which is the
  law that made this a decision rather than a preference.

**⛔ DECIDED 2026-09-03 — `action-type-weights.v1.json` ships with stated NEUTRAL defaults, tuned
from the smoke batch.** Same precedent as everywhere else in this program:
`spec-innate-picker.md` §3.3's per-mille multipliers *"defaulting to 1000, at which the score
reproduces the lexicographic tuple exactly"* (`spec-innate-picker.md:124-125`) — a neutral value with
its reasoning, so the module is buildable today.

| Key | Used by | Default | Why this is the neutral one |
|---|---|---|---|
| `base` | §3 step 1's `base + (5 - i) * step` | **1000** | With `step: 0` this alone yields a flat 200-per-category vector, so `base` sets the floor and `step` alone controls spread |
| `step` | same | **250** | Ranks 1..5 score 2000/1750/1500/1250/1000 → `400/350/300/250/200` per-mille after normalisation. Monotone, ordered by lean, and **no category is ever zero** — a zeroed category would make a whole slice of the corpus unplannable from a *default* |
| `separationMilli` | §3 step 2, indexed by A-S0's `separation` 0..4 (`null` takes the `0` row) | **`[0, 250, 500, 750, 1000]`** | Linear, spanning the full range: `separation: 0` collapses the spread to flat (the honest "we did not differentiate"), `separation: 4` keeps `base`/`step` intact. It is the identity ramp — the least opinionated total function over the five rows |
| `targetModeMilli` rows | §3 step 4, keyed on lean head plus `reach` | **uniform 1000 per mode within each row**, normalised to `167/167/167/167/166/166` over the six modes | Six modes, no evidence yet which a species should prefer; the shipped example vector in §2 is an **illustration of a tuned row**, not the default |
| `areaShapeMilli` | §3 step 4's conditional sub-vector | **uniform**, `250` each over the four shapes | Same reasoning, four shapes |
| `primaryMilli` / `secondaryMilli` | §3 step 5's element bias | **`400` / `200`**, remainder split evenly | A primary twice its secondary, and both above the even split (`167`), so the bias is real and visible at the default rather than indistinguishable from uniform. This is the one place a flat default would be *wrong*: an element bias vector with no bias is not a neutral value, it is a deleted feature |

**Every default keeps §5's invariants true by construction**: each vector sums to exactly 1000 after
the largest-remainder normalisation, every value is an integer per-mille, and no category, mode or
shape is zero. `_meta` in the shipped file states all of the above, that the values are **untuned
placeholders**, and that the first smoke batch's `type-weights.json` plus A-S5's quota-drift and
cell-occupancy findings are the evidence they move on. **Re-tuning is a config change**, never a
rebuild — which is why these are rows and not constants.

## 3. The algorithm

1. **Rank to weight.** For a species whose `leanOrder` is `[c1..c5]`, the raw score of `ci` is
   `base + (5 - i) * step`, with `base` and `step` read from
   `data/tuning/action-type-weights.v1.json`. A **uniform floor** — A-S0's `leanSource: "floor"`,
   a genuine five-way score tie — gives every category the same raw score, so the absence stays
   visible as an even vector rather than being hidden behind an invented preference.
   ⛔ **CORRECTED 2026-09-03 (review F12):** the trigger is `leanSource: "floor"`, **not** "no
   family". A family-less species is now derived like any other (`spec-characteristic-pool.md` §3
   step 3), so reading `family: null` as a flat vector would flatten 31 of 84 species that carry a
   real derivation.
2. **Separation scaling.** The spread between first and last is multiplied by
   `separationMilli[separation]`, a five-row table indexed by A-S0's `separation` (0..4). A species the
   derivation could not differentiate gets a flatter vector, which is the honest representation of
   "we do not know", and it is exactly the thing a balance pass will want to move.
   **`separation: null` takes the same row as `0`** — a family-less species has no floor to be
   distant from, so there is no spread measurement to scale by
   (`spec-characteristic-pool.md` §3 step 5). The two stay distinguishable in `role-lean.json`; only
   the scaling coincides, and that coincidence is a tuning row like every other.
3. **Normalise to per-mille with largest remainder.** `weight_i = (raw_i * 1000) / Σraw`, computed in
   `long`, **widening before the multiply** (`(long)raw_i * 1000`, never `(long)(raw_i * 1000)`), and
   **dividing by 1000 last, exactly once**. The 1000 − Σ⌊⌋ remainder units are handed out one each to
   the largest fractional parts; ties among equal fractions break on the declared category order
   (`ActionEnums.cs:119-123`). This is a total rule, so the vector cannot depend on iteration order.
4. **Target-shape vector** over all **six** modes (`ActionTargetModes.Name`,
   `ActionTargetSpec.cs:103-112`), from tuning rows keyed on the species' lean head plus its `reach`
   when an anchor supplies one. The four area shapes are a **conditional sub-vector** consulted only
   under `area`, and it also sums to 1000. `area` itself keeps a non-zero weight even with no board:
   the board gate stays at `ActionSeeder.cs:51-53` for the runtime path and at `ActionValidator`'s
   `AreaRequiresBoard` for the bind path, and duplicating either here would be a second mechanism for
   one rule.
5. **Element bias** over the six elements: the species' primary takes `primaryMilli`, its secondary
   `secondaryMilli` when present, and the remainder is split evenly across the rest with the same
   largest-remainder rule. A species with `elementSecondary: none` splits over five, not four.
6. **Family rows** are computed identically from the family floor lean, so all 19 families get a row
   even where only some of their members do.
7. **Canonical write** — sorted keys, fixed indent, `\n`, explicit nulls.

## 4. What it must NOT do

- **Never let a model choose, adjust or review a weight.** *"A model has no calibrated sense of scale,
  so a number it picks is a plausible-looking guess that survives review because nothing looks wrong
  with it."* A weight is a probability; that is the deepest part of the deny-list.
- **Never emit a `float` or a `double`.** Every magnitude is `long` and every published value is an
  integer per-mille. A `float` magnitude stops being integer-exact at index 232, inside normal play.
- Never introduce a category, tag, mode, shape or element that is not already in the C# enums. A third
  vocabulary is the defect this whole neighbourhood exists to prevent.
- **Never zero a category to zero out a pool.** A zero weight is a soft absence in a roll; it must
  never be used as a hard gate on what a tier may reach — that is constraint 4's widening, and it is
  gated.
- Never re-implement the board gate on `area`. `ActionSeeder.cs:51-53` owns the roll-time half and
  `ActionValidator`'s `AreaRequiresBoard` owns the bind-time half.
- **Never emit a PascalCase member name.** Every key is a `Name` function's return value; a test
  round-trips every key through the matching `TryParse`.
- **Never describe these vectors as a roll input.** They are consumed by A-S1 at plan time
  (§2's F5 note); saying otherwise re-opens the second-roll question the law closes.
- Never carry a number a balance pass would move in code. Every coefficient in §3 is a row in
  `data/tuning/action-type-weights.v1.json`.

## 5. Testing strategy

| Case | Expect |
|---|---|
| **Determinism** | two runs over an unchanged `role-lean.json` produce a byte-identical `type-weights.json`, asserted by hash |
| **Sum invariant** | every `categoryMilli`, `targetModeMilli`, `areaShapeMilli` and `elementBiasMilli` sums to **exactly 1000**, over all 84 species and 19 family rows |
| **Planted violation — a float** | a coefficient authored as `0.4` in the tuning file is **refused at load**, naming the row. The audit tests all four smuggling shapes, including a string `"400"` and an enum of numeric strings |
| **Planted violation — unknown member** | a tuning row keyed on `"economy"`, on a seventh target mode, or on a PascalCase `"Area"`/`"Row"` is refused, naming the key |
| **Planted violation — hard gate** | a species row with a category at 0 is legal, and a test asserts the generator still treats that category as *reachable*, so a zero weight never becomes a family-access gate |
| **Largest remainder** | a hand-built vector whose exact division leaves 3 remainder units distributes them to the three largest fractions, and shuffling the input order changes nothing |
| **Overflow** | the normalisation widens before multiplying; a synthetic vector at the top of the range does not overflow, and a forced overflow **throws** |
| **Roster size** | exactly 84 species rows and 19 family rows — the count is asserted, so a silent drift toward the 904 almanac count fails |
| **Offline guarantee** | the suite passes with the transport stubbed to raise |

## 6. Acceptance criteria

1. `data/seed/actions/type-weights.json` exists, loads through A-C1's envelope, and carries 84 species
   rows plus 19 family rows.
2. Every vector in the file sums to exactly 1000, and every value is a non-negative integer.
3. No `float`, `double`, or decimal literal appears anywhere in the file or in the module's source.
4. Every coefficient the algorithm uses is a row in `data/tuning/action-type-weights.v1.json`; a magic
   number audit over the module reports zero targets.
4b. That file **exists and ships with the stated neutral defaults** (the table above §3):
   `base: 1000`, `step: 250`, `separationMilli: [0, 250, 500, 750, 1000]`, uniform `targetModeMilli`
   and `areaShapeMilli` rows, and `primaryMilli: 400` / `secondaryMilli: 200`. A test asserts every
   default vector sums to exactly 1000 after normalisation and that no category, mode or shape is
   zero at the default; `_meta` says in those words that the values are untuned placeholders and that
   the first smoke batch, read through A-S5's quota-drift and cell-occupancy findings, is the
   evidence they move on. **Re-tuning is a config change.** ⛔ **DECIDED 2026-09-03** — §2's example
   vectors are an illustration of a *tuned* row and were never a default.
5. A species with A-S0 `separation == 0` produces a measurably flatter category vector than one with
   `separation == 4`, and the flattening factor is a tuning row. A species with `separation: null`
   takes the same row as `0`, and a test asserts a **family-less** species still gets a vector shaped
   by its own `leanOrder` rather than a flat 200/200/200/200/200 (review F12).
6. Category, mode, shape and element keys are exactly the strings the C# `Name` functions return —
   asserted member by member and round-tripped through `TryParse`, including **six** target modes
   (`"self" "single" "multi" "rolledTarget" "all" "area"`) and four area shapes (`"row" "column"
   "square" "rectangle"`). ⛔ **CORRECTED 2026-09-03 (review F10):** "the C# enum members" is what
   let PascalCase into §2's example; the member *name* and the wire *string* are not the same thing.
6b. `targetModeMilli` and `areaShapeMilli` have a named consumer — A-S1's plan-time largest-remainder
   allocation (`spec-distribution-planner.md` §3 step 4a) — and no code path in this program feeds
   them to `WeightedChoice.Pick`.
7. A rerun over unchanged inputs is byte-identical by hash, with provenance recording the lean hash and
   the tuning version.
8. Zero model calls, proven by a stub that raises.

## 7. Dependencies

**Depends on:** **A-S0** (map §4 — `A-T1` depends on `characteristic-pool`); A-C1's envelope for the
output file.
**Depended on by:** **A-S1**, which uses these weights to set category quotas — moving the category
decision out of the model is the single largest cost saving in the program.
**Cross-program (map §7):** none blocking. `spec-action-seeding.md:176` also names a `TypeWeights.cs`
runtime reader; **that is the action program's file, not this module's** — this module authors the data
and stops at the file boundary.
