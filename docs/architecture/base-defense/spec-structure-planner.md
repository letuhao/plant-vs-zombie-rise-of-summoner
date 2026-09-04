# Spec: `structure-planner`

**Module 26 of 29 · level c3 · depends on `structure-corpus` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. **Added by owner decision 33**, folded in by decision 45.

---

## Objective

**Decide what to generate before anything is generated — deterministically, with no model.**

Owner decision 33, verbatim:

> *"pipeline generator need a **deterministic planner (not LLM)** to prepare what it should generate
> first."*

This promotes a seedsmith guideline — *"order the build so the model-free modules come first"* — into a
**required stage**. The reason is decision 32: a structure's HP is `P(Θ) × an authored material tier`,
so the **tier ladder must be ordered**, and an ordering that emerges from whatever a model happened to
name is not an ordering.

**Success looks like:** a committed, diffable plan that says exactly which rows will exist and what
each one's fixed properties are — before a single token is spent.

---

## Why a planner and not a prompt

Three failures it closes, each named in the research:

| Failure | Without a planner | With one |
|---|---|---|
| **The tier ladder is unordered** | The model names "stone", "iron", "granite", "reinforced" — is granite above iron? Decision 32's mechanics rest on an answer nobody declared | The ladder is **declared first**; the model picks a rung that already exists |
| **Distribution skew** (D2's Hammerdin) — *"every individual number defensible, the offering degenerate"* | Per-role counts emerge from whatever the model produced | Counts are **targets**, and the plan is checked against them before generation |
| **Budget is discovered late** | *"Compute the call budget before choosing a decomposition — **it decides the architecture, not the schedule**"* | The plan **is** the budget: rows × votes × stages, known before the run |

---

## The contract

### 1. The plan is a committed artifact

```
data/seed/structures/_plan.json
```

Not a runtime object. **Committed and diffable**, because *"a generated row nobody can diff is a row
nobody can review"* — and a plan is the highest-leverage thing to review, since every row descends
from it.

### 2. What the planner fixes — five things, all model-free

| # | Fixed | Source |
|---|---|---|
| 1 | **The tier ladder**, ordered | Decision 32/33. `rubble < timber < stone < iron < …`, declared once |
| 2 | **Which roles exist and the target count per role** | §4's ten roles; `budget` in tuning; target ~36 total |
| 3 | **Which (role × slot kind) combinations are legal** | `SlotKind`'s shipped 14 values × role |
| 4 | **How many variants per row** | §6: a tier chain is a `variants` list, **not four rows** |
| 5 | **Which `acquisitionPaths` each kind may declare** | Decision 35; `none` illegal |

The model then writes **identity into slots the planner already opened** — name, family, flavour,
`reason`. It never chooses how many rows exist, which tier is stronger, or what may sit where.

### 3. Deterministic, and that is testable

```csharp
/// <summary>
/// The plan is a pure function of (research corpus, tuning, seed). No clock, no RNG that is not
/// seeded, no model. The same inputs produce the same plan byte-for-byte, forever — which is what
/// makes it reviewable as a diff rather than as a snapshot.
/// </summary>
public static StructurePlan Build(StructureCorpus handAuthored, StructureSeedTuning tuning, ulong seed);
```

**No `DateTime`, no `System.Random`.** Where an ordering is needed, it is ordinal by id — the same
discipline `LegionSupply` and `ReachMap` already apply.

### 4. The plan is checked before it is used

The plan is where the skew guard bites, and it bites **cheaply** — before generation rather than after:

- Per-role actual vs declared target.
- **Grid density recomputed**: `roles × rows` must land in §4's 2.4–4.0 band.
- Every tier on the ladder has at least one row, or the tier is cut.
- No (role × slot kind) combination is declared and empty.

**A plan that fails these does not proceed to generation.** Catching skew here costs nothing; catching
it after a run costs the run.

### 5. It also plans the budget

> *"Compute the call budget before choosing a decomposition — it decides the architecture, not the
> schedule."*

The plan states: rows × pipeline stages × vote count, and **which fields are vote-worthy**. *"Majority-vote
only the load-bearing fields. Voting everything triples the run."* And *"a vote set chosen by vibes
rather than by cost-of-being-wrong"* is a listed red flag.

---

## Tunables

`data/tuning/structure-seed.v{n}.json`:

| Block | Rows |
|---|---|
| `budget` | per-role target counts |
| `bands.tierLadder` | the ordered tier list — **the ladder decision 32 depends on** |
| `plan.voteFields` | which fields get majority vote |
| `plan.voteCount` | votes per voted field |

## Numeric types

Counts and indices are `int`. **The planner produces no magnitudes** — it produces ordinals and counts;
`structure-catalog-import` is the single place an ordinal becomes a number.

## Boundaries

**Always:** commit the plan · deterministic, ordinal-ordered · check skew and density before generation
· state the call budget.

**Ask first:** a tier ladder longer than the `bands` table can price · exceeding the density band.

**Never:** call a model in this module · let the model choose row counts, tier order, or slot legality ·
a wall clock or unseeded RNG · proceed to generation on a failing plan.

---

## Testing

Tests **never call a model** — stub the transport so it *raises*.

| Test | Asserts |
|---|---|
| `Plan_is_byte_identical_across_10000_runs` | determinism |
| `Plan_is_a_pure_function_of_its_inputs` | no clock, no unseeded RNG — source scan too |
| `Tier_ladder_is_totally_ordered` | **decision 32's precondition** |
| `Every_tier_has_at_least_one_row_or_is_cut` | no dead rungs |
| `Per_role_counts_match_declared_targets` | the skew guard |
| `Grid_density_lands_in_the_2_4_to_4_0_band` | §4, recomputed on the plan |
| `A_failing_plan_blocks_generation` | the gate is real |
| `No_declared_and_empty_role_slot_combination` | |
| `Call_budget_is_stated_before_any_run` | rows × stages × votes |
| `Vote_fields_are_declared_not_defaulted` | *"chosen by cost-of-being-wrong, not by vibes"* |
| `Transport_stub_raises_if_a_test_calls_a_model` | |

## Success criteria

1. A committed, diffable plan.
2. Byte-identical over 10,000 runs; provably model-free and clock-free.
3. The tier ladder is totally ordered — **decision 32 is unsound without this.**
4. Skew and density are checked before generation, and a failing plan blocks it.
5. The call budget is stated up front.

## Open questions

None. Decision 33 fixes the requirement; §4 fixes the numbers.
