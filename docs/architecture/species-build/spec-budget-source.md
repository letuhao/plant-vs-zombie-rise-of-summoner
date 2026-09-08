# Spec: `budget-source`

Module 2 in the [species-build capability map](../species-build-map.md). **No dependencies.** Must land
before anything computes a `DemonType` budget, or that thing is built on an inverted ordering.

## Objective

Close audit finding **A1** ([species-build-ideal.md](../species-build-ideal.md) §11): the `DemonType`
point budget's declared source is **almanac XP**, which is an *accumulation*, while the other three
tiers read *indices*. `PointBudget.PointsFor` multiplies `sourceValue × rate` with **no unit
conversion** — its own tuning doc says so explicitly: *"a shipped rate of `3` means exactly 3 points per
source unit"* (`AptitudeTuning.cs:26-32`). The result inverts a locked decision:

| Species level | Cumulative plant XP | DemonType budget (× 4) | Commander budget (Θ=20 × 3) | Ratio |
|---|---|---|---|---|
| 10 | 1,872 | 7,488 | 60 | 125× |
| **12** | **2,640** | **10,560** | **60** | **176×** |
| 20 | 6,992 | 27,968 | 60 | 466× |

`rpg-progression.md`'s own balance note puts a player at **L12–20 after 20 matches**, so these are
ordinary values. The lock being inverted is *"the commander tier is the SMALLEST and the unique tier the
LARGEST"*, and it exists for a stated reason: a commander allocation replicates across the whole roster,
so a dominant one is the worst case.

**Owner decision 14: the source is SPECIES LEVEL.** Budget = `speciesLevel × 4`, which restores the
ordering (`60 < 80 < 120` at L20), makes the tier exactly symmetric with `UniqueDemon` (which already
reads "specimen level"), and matches the original framing better than XP did — *"they will earn bonus
when specie level up"* means points arriving as a visible step, not trickling per placement.

**This module adds no mechanism.** It is a contract correction, a test correction, and three citation
corrections — **plus one arithmetic rule the spec-coverage audit made load-bearing**, below.

### ⛔ The budget is zero at level 1 — `(level − 1) × rate`, not `level × rate`

Added 2026-09-05 by the spec-coverage audit, and it is not a rounding preference.

An unrecorded actor's progression defaults to **`Level = 1`** (`RpgStore.Progression.cs:280`). Under
`demon-type-allocation`'s compose-at-read baseline, `level × rate` would give **every species in every
fixture a non-empty allocation** — including every battle and expedition golden, whose actors would
silently gain a build nobody authored. `battle-allocation` (module 10) is where that would detonate.

`(level − 1) × rate` makes a never-levelled species carry **exactly zero points**, so:

- every existing golden stays byte-identical, and
- the rule matches what the owner actually described — *"they will earn bonus when specie level up"*.
  A species that has never levelled has earned nothing.

**The golden safety is a consequence of stating the rule correctly, not a workaround for a test.** Level
0 and level 1 both yield zero; the budget is `max(0, level − 1) × rate`, and the subtraction happens
before the multiply so nothing can go negative into a `checked` context.

## Design

**Nothing about `PointBudget.PointsFor`'s signature changes.** It takes `sourceValue` and knows only
the rate; that is correct and stays. What changes is the **declared contract** for what a `DemonType`
caller passes, and the test that is supposed to protect it.

### The test is the real deliverable

`PointBudgetTests.Commander_budget_is_smallest_and_unique_largest` cannot see this defect, and its own
comment says why:

```csharp
const long sameSourceValue = 100; // isolates the RATE ordering from any per-scope source difference.
```
— `PointBudgetTests.cs:84`

Holding the source constant proves `3 < 4 ≤ 4 < 6`. That is true, and it **does not imply the budget
ordering the test is named for**. The test is not wrong about rates; it is measuring the wrong thing for
its own claim, and it stays green straight through a 176× inversion.

**Replace it with two tests, not one:**

1. `Rates_are_ordered_commander_smallest_unique_largest` — the existing constant-source check, renamed
   to what it actually proves. Keep it; it is a real property of the shipped file.
2. `Real_budgets_are_ordered_at_representative_sources` — the new one. Each scope is fed a
   **representative value in its own units** (a plausible `Θ_player`, a plausible species level, a
   plausible specimen level, drawn from a small named table in the test) and the ordering is asserted on
   the *budgets*. This is the test that would have caught A1, and the one that protects decision 14.

⛔ **The new test covers THREE scopes, not four, and says why.** `Aspect`'s source is `element_mastery`,
which `PointBudget.cs:15` records **does not exist** — it is owned by the demon program's `aspect-scope`
module, which is reverted and not authorized to build. Feeding it an invented value would decide the very
ordering the test claims to prove — the same defect (a fabricated source) this module exists to fix. So
the test asserts `commander < demonType < uniqueDemon` over real sources, and carries a comment naming
`Aspect` as excluded **because its source does not exist yet**, to be added by whoever builds that tier.

### Citations that still say "almanac XP"

Three, and all three are load-bearing because a future session will read one of them and re-derive the
wrong source:

| Where | Text to correct |
|---|---|
| `docs/architecture/class-system/spec-point-economy.md:37` | the §2 source table row for **demon type** |
| `src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs:12-18` | the type's own doc comment listing the four sources |
| `data/tuning/aptitudes.v5.json` | the `_scopeSourcesWhy` note |

Each correction states **why** the source is an index rather than an accumulation, so the reasoning
survives without needing this spec.

### The power-ladder obligation

A budget derived from a level is exactly the shape a private `f(level)` wears. Species level enters as a
**linear index**, never through a private curve, and the correction notes state that. If a future
module wants the budget to grow non-linearly, that is a reviewed change to the power SSOT's §10
inventory, not a coefficient tweak here.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter PointBudget
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-power.ps1
python scripts\audit-magic-numbers.py --summary
```

## Project structure

```
src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs            doc comment only
src/FusionRpg.Core/Stats/Aptitudes/AptitudeTuning.cs         doc comment only
data/tuning/aptitudes.v5.json                                _scopeSourcesWhy note only
docs/architecture/class-system/spec-point-economy.md         §2 source table row
tests/FusionRpg.Core.Tests/Stats/Aptitudes/PointBudgetTests.cs   one test split into two
```

**No production code path changes**, because no `DemonType` caller exists yet — which is precisely why
this is cheap now and expensive later.

## Code style

- The representative source values in the new test live in a small **named table inside the test**, not
  as bare literals at the assertion — a reader must be able to see that a species level and a `Θ` are
  different units without reverse-engineering it.
- Those values are test fixtures, **not tunables**: they describe plausible play, they do not configure
  the game. Comment says so.
- `long` throughout, `checked` multiply already present in `PointsFor` — unchanged.

## Testing strategy

1. **`Real_budgets_are_ordered_at_representative_sources`** — the new guard. Deliberately fails if the
   `DemonType` source is documented back to an accumulation.
2. **`Rates_are_ordered_...`** — the old test, renamed to its true claim, kept.
3. **A regression test pinning the arithmetic**: `PointsFor(DemonType, level, tuning) == level × 4` at a
   couple of levels, so a silent rate change is visible.
4. **No-cap test unchanged** (`No_cap_on_an_aptitude`) — PS-8 still holds; a bigger source must produce
   a proportionally bigger budget, never a clamp.
5. Full Core suite green, **zero goldens** — nothing here changes a shipped number.

## Boundaries

- **Always:** state, at every corrected citation, *why* the source is an index; keep both tests (rates
  and budgets) — they prove different things.
- **Ask first:** changing any of the four rates (`3/4/4/6`) — they are explicitly UNMEASURED placeholders
  owned by `residual-fit`, and this module has no mandate to tune them; changing `PointsFor`'s signature.
- **Never:** make the budget non-linear in level here (that is a power-SSOT change); delete the
  constant-source test in favour of the new one — the old claim is still worth guarding.

## Success criteria

1. A test exists that fails if `DemonType`'s source is an accumulation, and passes with species level.
2. All three "almanac XP" citations are corrected, each carrying the reason.
3. `guard-power.ps1` green; the linear-index property is stated where a future reader will find it.
4. Full Core suite green; **zero goldens re-blessed**.
