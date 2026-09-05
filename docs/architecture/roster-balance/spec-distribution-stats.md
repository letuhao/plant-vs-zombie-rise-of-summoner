# Spec: `distribution-stats` (RB1)

**Module id:** `distribution-stats` · **Program:** [roster-balance](../roster-balance-map.md) · **Build order: 1 of 6**
**Depends on:** — (foundation) · **Model calls: none**

## Objective

**Measure the species roster's own characteristic distribution.** Nothing in this repo does, which is
why a lopsided roster propagated into every downstream pipeline unnoticed for the whole program's
life. This module turns *"the roster feels unbalanced"* into a number, and becomes the regression
detector that stops it skewing again silently.

**Read-only and model-free.** It computes and reports; it never edits the corpus and never decides
what balanced means (that is RB2).

## Design

### It derives everything — no hard-coded counts, ever

The owner's binding requirement: *"we don't care how many real species and family."* So the module
reads whatever the corpus holds and derives axes, values, cells and totals from it. **A roster that
grows from 841 to 2000 rows, or gains a fourteenth aptitude, needs no code change.** A hard-coded
species count, family count, axis list or cell count is a defect in this module, not a convenience.

### What it reads

`data/seed/demons/species/**/*.json` (excluding `_index.json`), each file a JSON array of species
rows. The nine characteristic axes present today are discovered, not declared:

`aptitudePrimary` · `aptitudeSecondary` · `posture` · `elementPrimary` · `elementSecondary` ·
`attackTempo` · `deployMode` · `rarity` · `threatBand`

### What it computes

**Per axis:** distinct values, count per value, **evenness**, and top-value share.

Evenness is normalised Shannon entropy — `H / log2(k)` over the observed value counts, where `k` is
the number of distinct values. It is chosen over a raw max/min ratio for one reason that matters
here: it is **scale-free and axis-comparable**, so a 13-value axis and a 2-value axis produce numbers
that mean the same thing. `1.0` is perfectly uniform; `0.0` is everything in one value.

**Across the coverage grid:** occupancy per cell, count of empty cells, density (`rows / cells`),
and the crowded/thin tails. The grid's axes are an input (RB2 owns the choice), not a constant here.

### ⛔ DECIDED — the default coverage grid is `aptitude × element × posture`, and the reason is measured

`docs/research/game-design/` establishes the density band from real shipped rosters: **~3.6 species
per cell is the healthy zone** (Genshin/FGO), **~12.6 is the failure zone** (FEH). Measured against
the real 841-row corpus on 2026-09-05:

| Candidate grid | Cells | Density | Verdict |
|---|---|---|---|
| `aptitude × element` | 78 | **10.78** | near the failure zone — too crowded |
| `aptitude × element × posture` (4 values, incl. `unresolved`) | 312 | **2.70** | below the safe zone — too thin |
| **`aptitude × element × posture` (3 real values)** | **234** | **3.59** | **the healthy band** |

`841 / 3.6 = 234` cells is the ideal target, and `13 × 6 × 3 = 234` hits it almost exactly. That is
the grid. **`unresolved` is excluded because it is not a posture** — it is a vote outcome that leaked
into 12 committed rows (see §"Findings this module reports but does not fix").

**What would overturn it:** the roster changing size enough to move the density out of band, which
this module measures on every run — so the grid choice is re-checkable rather than assumed. RB2 owns
the tunable that names it.

### Findings this module reports but does not fix

It **detects and names**, never edits:

- **Illegitimate axis values.** `posture: "unresolved"` on 12 rows today. `unresolved` is a vote
  outcome, not a characteristic; its producer is `demon-seed`'s classification pipeline, which owns
  the fix. Reported as a distinct defect class, never silently folded into the counts.
- **Degenerate axes.** `elementSecondary` is 97.4% `none` (evenness 0.105) — an axis carrying almost
  no information. Whether that means "under-authored" or "should not be an axis" is a design call for
  `demon-seed`; this module states the number and stops.

## Commands

```powershell
python -m seedsmith roster stats                      # the report, human-readable
python -m seedsmith roster stats --json                # machine-readable, for RB3
python -m pytest tools/seedsmith/tests/test_distribution_stats.py
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/roster/stats/derive.py    new — pure computation
tools/seedsmith/seedsmith/adapters/roster/stats/load.py      new — corpus read, axis discovery
tools/seedsmith/seedsmith/adapters/roster/generate_stats.py  new — CLI entry point
docs/research/roster/_distribution-<date>.json               new — the emitted report
tools/seedsmith/tests/test_distribution_stats.py             new
```

## Code style

```python
# Evenness is normalised Shannon entropy, NOT a max/min ratio: it is scale-free, so a 13-value axis
# and a 2-value axis produce comparable numbers. Deriving `k` from the observed values (never a
# declared vocabulary) is what lets a new aptitude appear without a code change.
def evenness(counts: "Counter[str]") -> float:
    n, k = sum(counts.values()), len(counts)
    if k <= 1 or n == 0:
        return 0.0
    h = -sum((v / n) * math.log2(v / n) for v in counts.values() if v)
    return h / math.log2(k)
```

## Testing strategy

| Test | Asserts |
|---|---|
| `axes_are_discovered_from_the_corpus_never_declared` | a planted row carrying a brand-new axis key appears in the report without a code change |
| `roster_size_is_never_hard_coded` | the module produces a correct report over a 3-row fixture and over the real corpus, with no constant naming either size |
| `evenness_is_1_for_a_uniform_axis_and_0_for_a_single_value` | the two endpoints, exactly |
| `evenness_is_comparable_across_axes_of_different_arity` | a 2-value 50/50 axis and a 10-value uniform axis both score 1.0 |
| `PLANTED_VIOLATION_an_unresolved_axis_value_is_reported_as_a_defect` | `posture: "unresolved"` is surfaced as an illegitimate value, never counted as a real one |
| `a_degenerate_axis_is_flagged_by_its_own_evenness` | a 97%-one-value fixture axis is reported below the degeneracy threshold |
| `grid_density_matches_the_hand_computed_value` | 841 rows over 234 cells reports 3.59, computed independently in the test |
| `empty_cells_are_named_not_just_counted` | the report lists which cells are empty, so RB3 can consume them |
| `the_report_is_byte_identical_across_two_runs` | ordering is stable (sorted keys, never dict/filesystem order) |
| `the_real_corpus_loads_and_reports_without_error` | run against the real tree, not only fixtures |

## Boundaries

**Always:** derive axes, values and totals from the corpus; sort every emitted collection on a stable
key; report illegitimate values as a distinct class.

**Ask first:** adding an axis to the *default* coverage grid — that changes what "balanced" costs and
belongs in RB2's tunable, not here.

**Never:** write to `data/seed/demons/species/**`; hard-code a species, family, axis or cell count;
call a model; silently drop an illegitimate value (it must be reported, and it must not pollute the
real counts).

## Success criteria

- [ ] Runs over the real 841-row corpus and reproduces §2 of the map: the nine axes with their
      evenness, and `aptitude × element × posture(3)` at 234 cells / 3.59 density.
- [ ] Adding a species file, or a new axis value, changes the report with no code edit.
- [ ] `posture: "unresolved"` is reported as an illegitimate value on exactly the rows that carry it.
- [ ] Two consecutive runs over unchanged input are byte-identical.
- [ ] Zero model calls, proven by the same raising-stub convention the sibling pipelines use.
