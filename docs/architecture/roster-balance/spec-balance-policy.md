# Spec: `balance-policy` (RB2)

**Module id:** `balance-policy` · **Program:** [roster-balance](../roster-balance-map.md) · **Build order: 2 of 6**
**Depends on:** RB1 `distribution-stats` · **Model calls: none**

## Objective

**Say what "balanced" means, as data.** RB1 measures; this module holds the thresholds that turn a
measurement into a verdict. It exists so that a rebalance is a **config change with a diff**, not a
code edit and a rebuild — the same reason every other threshold in this repo lives in
`data/tuning/`.

## Design

### Everything here is a tunable, by construction

`docs/architecture/tunables-ssot.md` is the standard, and its test applies to every number in this
module: *would a balance pass ever want to change this?* For all of them the answer is yes, so **not
one threshold may be a `const` in code.** A hard-coded threshold here is the defect this module
exists to prevent.

`data/tuning/roster-balance.v1.json`:

```jsonc
{
  "coverageGrid": ["aptitudePrimary", "elementPrimary", "posture"],
  "densityTarget": { "min": 3000, "ideal": 3600, "max": 5000 },   // per-mille, species per cell
  "axisPolicy": {
    "aptitudePrimary":  { "role": "load-bearing", "minEvennessMilli": 800, "maxTopShareMilli": 250 },
    "elementSecondary": { "role": "cosmetic",     "minEvennessMilli": 0,   "maxTopShareMilli": 1000 }
  },
  "illegalValues": { "posture": ["unresolved"] }
}
```

**Per-mille integers, never floats.** `CLAUDE.md`'s numeric rule is binding here even though these
are ratios rather than magnitudes: a threshold read as `0.8` in one place and `0.80000001` in another
is a reproducibility bug in a gate, and this module's whole output is a gate verdict.

### Three axis roles, because not every axis should be balanced

Forcing every axis toward uniformity would be wrong — some are *meant* to be lopsided.

| Role | Meaning | Enforced |
|---|---|---|
| **load-bearing** | Play reads this axis; skew is a real design problem | evenness and top-share both checked |
| **cosmetic** | Flavour only; skew is acceptable | reported, never failed |
| **derived** | Computed from other axes; balancing it directly is meaningless | excluded from verdicts entirely |

**`elementSecondary` starts `cosmetic`, and that is a deliberate call, not an oversight.** It measures
97.4% `none` (evenness 0.105). Marking it load-bearing would demand rebalancing an axis that is
plausibly *supposed* to be rare — a secondary element is a special case by design. Marking it
cosmetic says "this is not a balance failure" while RB1 keeps reporting the number, so the design
question stays visible without blocking the program.
**What would overturn it:** a `demon-seed` decision that a secondary element should be common.

### ⛔ DECIDED 2026-09-06 (owner) — the five load-bearing axes

Owner's answer to "which axes should the program actually try to FIX": **the coverage grid plus
`rarity` and `threatBand`.** So the shipped policy is:

| Axis | Role | Why |
|---|---|---|
| `aptitudePrimary` | **load-bearing** | grid axis; worst real skew (Onslaught 332 / Ferocity 2) |
| `elementPrimary` | **load-bearing** | grid axis |
| `posture` | **load-bearing** | grid axis; also carries the 12 illegal `unresolved` rows |
| `rarity` | **load-bearing** | 55.8% `fused`, `sunwoven`=4 — feeds progression pacing |
| `threatBand` | **load-bearing** | 73.9% `nuisance`, `scourge`=1 — feeds encounter pacing |
| `attackTempo` | report-only | already the healthiest axis measured (evenness 0.938) |
| `deployMode` | report-only | 721 plant / 120 hypno is plausibly just what PvZ *is*, not a defect |
| `aptitudeSecondary` | report-only | 73.6% `none`; same "rare by design" reading as `elementSecondary` |
| `elementSecondary` | **cosmetic** | 97.4% `none`, evenness 0.105 |

`report-only` is `cosmetic`'s sibling: measured and printed every run, never a verdict failure, and
never a reassignment target. The distinction from `cosmetic` is intent — a `report-only` axis is one
we may promote later, a `cosmetic` one we do not expect to.

### ⛔ DECIDED 2026-09-06 (owner) — the thresholds are targets, not aspirations

Owner: *"hit the evenness targets whatever it takes."* So the shipped floors are real gates, and RB4
is permitted to reach them using rows whose `basis` is `stated`/`observed` (see
`spec-rebalance-plan.md`'s own ranking, step 5) rather than stopping at the soft pool.

**The one guard that survives that instruction:** every move against a `stated`/`observed` row is
stamped `divergesFromAlmanacBasis`, and RB2's verdict reports the running count. **A rebalance that
diverges is allowed; one that diverges invisibly is not.**

### The density target is a band, not a point

`3.6` per cell is the healthy centre from the measured reference rosters; `3.0`-`5.0` is the working
band. A band matters because the roster grows: at 841 rows the grid holds 3.59, but the same grid at
1200 rows holds 5.13 and is out of band — which is the signal to **split an axis into the grid**, not
to delete species. RB1 recomputes this every run so the drift is visible before it is a problem.

### Illegal values are policy, not statistics

RB1 *detects* `posture: "unresolved"`; this module is where it is *declared illegal*, so the rule is
reviewable data rather than a hidden branch. An illegal value never counts toward evenness (it would
flatter the number) and always raises a defect.

## Commands

```powershell
python -m seedsmith roster policy --check           # validate the tuning file against RB1's axes
python -m seedsmith roster verdict                  # RB1 measurement + this policy -> pass/fail per axis
python -m pytest tools/seedsmith/tests/test_balance_policy.py
python scripts/audit-magic-numbers.py --targets M1  # must stay clean for this module's files
```

## Project structure

```text
data/tuning/roster-balance.v1.json                            new — every threshold
tools/seedsmith/seedsmith/adapters/roster/policy/load.py      new — parse + validate
tools/seedsmith/seedsmith/adapters/roster/policy/verdict.py   new — measurement x policy -> verdict
tools/seedsmith/tests/test_balance_policy.py                  new
```

## Code style

```python
# Per-mille integers, never floats (CLAUDE.md's numeric rule): a gate that reads 0.8 in one place and
# 0.80000001 in another is a reproducibility bug, and this module's output IS a gate verdict.
if evenness_milli < policy.min_evenness_milli:
    defects.append(AxisDefect(axis, "evenness", evenness_milli, policy.min_evenness_milli))
```

## Testing strategy

| Test | Asserts |
|---|---|
| `every_threshold_comes_from_the_tuning_file` | a repo-wide scan of this module's sources finds no numeric literal that is a threshold |
| `an_unconfigured_axis_is_refused_at_load_not_defaulted` | a corpus axis absent from `axisPolicy` fails loudly, naming the axis |
| `a_cosmetic_axis_is_reported_but_never_fails_the_verdict` | `elementSecondary` at 0.105 evenness produces a report line and a passing verdict |
| `a_load_bearing_axis_below_its_floor_fails_and_names_the_number` | the defect carries measured and required values, not just "failed" |
| `a_derived_axis_is_excluded_from_the_verdict_entirely` | not merely passed — absent from the checked set |
| `an_illegal_value_never_counts_toward_evenness` | evenness over a fixture with `unresolved` matches evenness over the same fixture without those rows |
| `an_illegal_value_always_raises_a_defect` | and names the rows carrying it |
| `density_out_of_band_is_a_defect_in_BOTH_directions` | too thin fails as loudly as too crowded |
| `PLANTED_VIOLATION_a_float_threshold_in_the_tuning_file_is_refused` | per-mille integers only, enforced at load |
| `the_shipped_tuning_file_validates_against_the_real_corpus` | the committed defaults are legal for today's axes |

## Boundaries

**Always:** keep every threshold in `data/tuning/`; refuse an unknown axis at load rather than
defaulting it; state a reason next to every non-default policy value.

**Ask first:** changing `coverageGrid` — it moves the cell count and therefore what balance costs;
and reclassifying an axis between `load-bearing` and `cosmetic`, which changes what the program will
try to fix.

**Never:** hard-code a threshold; let an illegal value contribute to a statistic; fail a `cosmetic`
axis; use a float where a per-mille integer works.

## Success criteria

- [ ] Every threshold lives in `data/tuning/roster-balance.v1.json`; `audit-magic-numbers.py` is clean
      for this module.
- [ ] The shipped defaults reproduce the map's §2 verdicts against the real corpus: `aptitudePrimary`,
      `posture`, `rarity`, `threatBand`, `deployMode`, `aptitudeSecondary` fail their floors;
      `elementPrimary` and `attackTempo` pass; `elementSecondary` is cosmetic and does not fail.
- [ ] Density is checked as a band, failing both too-thin and too-crowded.
- [ ] `posture: "unresolved"` raises a defect and contributes to no statistic.
- [ ] An axis added to the corpus but not to the policy is refused at load, naming it.
