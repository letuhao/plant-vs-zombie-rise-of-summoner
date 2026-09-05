# Spec: `pipeline-direction` (RB6)

**Module id:** `pipeline-direction` · **Program:** [roster-balance](../roster-balance-map.md) · **Build order: 5 of 6 (parallel with RB4)**
**Depends on:** RB3 `coverage-index` · **Model calls: none**

## Objective

**Make the existing generation pipeline read the index.** Owner, 2026-09-05, diagnosing the real
defect: *"current pipeline missing distribution direction cause diversity bug"* and *"seem like it
current try to load almanac or demon seed first without a distribution plan and it cause current
problem."*

That is exactly what happens. This module is the reconcile: the pipeline keeps generating actions
from the almanac/species seed as it does today, but **directed by coverage instead of sampling
blind**.

## Design

### The defect, stated precisely

`A-S1 distribution_planner` builds 221 briefs and hands each one
`pool.allowedAtomFamilies` = **the entire ~98-family pool** — verified directly in
`data/seed/actions/_briefs/round-1.json`. It already decides anchor, motifs, anti-motifs, category,
target mode, area shape, rung band, structure axes and pairing role. **The one decision it does not
make is the one that determines diversity**, and that decision is handed to a sampled model.

The measured result of that, over a real full round: **52 of 98 families ever used**, top-5 families
**34.8%** of all picks, **46.8%** of accepted bundles distinct, `atom.elpw-overflow` appearing as the
entire bundle **23 times**.

### What changes, and how little

The pipeline shape does not change. Briefs are still briefs; the model still generates from the
almanac/species seed; workers still fan out through the existing `run_many`. **The change is that a
brief's pool is now shaped by the index instead of being the undifferentiated pool.**

```text
today:   corpus -> A-S1 briefs (pool = all 98) -> model picks -> hope coverage emerges  [it did not]
after:   corpus -> RB1/RB3 index -> A-S1 briefs (pool shaped by cell need) -> model picks -> coverage by construction
```

### ⛔ The constraint this must not break, and why it does not

`spec-distribution-planner.md` constraint 4 states *"every tier's `allowedAtomFamilies` is the same
set"*, enforced by a planted-violation test: a tuning file that **narrows `allowedAtomFamilies` per
TIER** while C1's three gates are absent is refused.

**That rule is about tier-based family-access widening — the C1 progression gate — not about
coverage.** It exists so a higher rarity tier cannot unlock a wider family pool than a lower one.
This module narrows per **cell need**, which is orthogonal to tier: two briefs in the same tier may
carry different pools, and two briefs in different tiers with the same cell need carry the same pool.

**This must be proven, not asserted.** A named test asserts that for any two briefs of different
tiers with equal cell need, the emitted pools are identical — so the C1 invariant provably still
holds, and the existing planted-violation test keeps passing untouched.

### Direction is a bias, not a cage

An under-served family is **weighted up**, not made exclusive. A brief whose cell need points at
`atom.terraforming` still offers the model a real choice — enough to refuse or to find a better
partner — because a single-option pool would turn generation into a template and destroy the one
thing the model is actually good at.

**What would overturn it:** measured evidence that weighting alone does not move coverage, at which
point hard restriction becomes a reviewed change with its own numbers.

### Feedback closes the loop

Dedup rejections and unresolved briefs feed back as **cell need that is still unmet**, so the next
cycle retries the gap rather than re-rolling the whole round. Today dedup runs at the end and
rejected **32 of 53** candidates into a graveyard; under this module a reject means "this cell is
still empty", which is what makes another cycle worth running at all.

## Commands

```powershell
python -m seedsmith actions plan --directed         # briefs shaped by the current index
python -m seedsmith roster gap --round <n>          # what the last round left unmet
python -m pytest tools/seedsmith/tests/test_pipeline_direction.py
python -m pytest tools/seedsmith/tests/test_distribution_planner.py   # must stay green, untouched
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/roster/direction/weights.py   new — cell need -> family weights
tools/seedsmith/seedsmith/adapters/roster/direction/feedback.py  new — round outcome -> unmet need
tools/seedsmith/seedsmith/adapters/actions/distribution_planner/  edit — optional index input
tools/seedsmith/tests/test_pipeline_direction.py                  new
```

## Code style

```python
# Direction is a BIAS, never a cage: an under-served family is weighted up, and the pool stays wide
# enough that the model can still refuse or find a better partner. A single-option pool turns
# generation into a template and wastes the one thing a model is better at than a table.
def weight_for(family: str, need: CellNeed) -> int:      # per-mille, integers only
    return BASE_WEIGHT_MILLI + need.deficit_milli_for(family)
```

## Testing strategy

| Test | Asserts |
|---|---|
| `an_index_absent_run_is_byte_identical_to_today` | the parameter is optional and inert when omitted — no silent behaviour change |
| `⛔ two_briefs_of_different_tiers_with_equal_cell_need_get_identical_pools` | **the C1 invariant, proven** — constraint 4 is untouched |
| `the_existing_planted_violation_test_still_passes_unchanged` | per-tier narrowing is still refused |
| `an_under_served_family_is_weighted_up_not_made_exclusive` | the pool still contains alternatives |
| `a_pool_is_never_narrowed_to_a_single_option` | the anti-template guard |
| `a_dedup_reject_becomes_unmet_cell_need` | the feedback path, over a fixture round |
| `an_unresolved_brief_becomes_unmet_cell_need` | not silently dropped |
| `directed_briefs_are_byte_identical_across_two_runs` | determinism preserved for the worker pool |
| `coverage_improves_measurably_on_a_replayed_round` | replay the recorded round-903/904 samples under weighting and assert families-used rises above the measured 52/98 |
| `every_emitted_family_id_is_one_of_the_98` | namespace guard, matching A-S1's own |

## Boundaries

**Always:** leave the pipeline shape alone; keep the index parameter optional and inert by default;
prove the C1 invariant with a named test; keep pools wide enough to refuse.

**Ask first:** hard-restricting a pool rather than weighting it; changing `A-S1`'s brief schema.

**Never:** narrow `allowedAtomFamilies` per tier; reduce a pool to one option; break byte-identical
replay; make the index a required input (an index-less run must still work exactly as today).

## Success criteria

- [ ] With no index supplied, output is byte-identical to today — proven, not argued.
- [ ] A named test proves equal-cell-need briefs in different tiers get identical pools, so C1 holds.
- [ ] `test_distribution_planner.py` passes unchanged, including its planted-violation case.
- [ ] Replaying the recorded round under weighting raises families-used above the measured 52/98.
- [ ] Dedup rejects and unresolved briefs both reappear as unmet cell need.
