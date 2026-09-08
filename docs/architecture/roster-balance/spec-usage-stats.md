# Spec: `usage-stats` (FC1)

**Module id:** `usage-stats` · **Program:** [roster-balance](../roster-balance-map.md) · **Build order: 1 of 2**
**Depends on:** — (foundation) · **Model calls: none**

## Objective

**Measure how evenly the action-corpus generation pipelines actually draw from the 98 authored
atom/affix families.** Nothing today does. `coverage_report`'s `atomFamilyNamespace` metric checks
that a picked family *exists* in the 98 — it has never tallied *how often* each one is picked.

Measured directly, 2026-09-06, over every real committed round: **216 accepted candidates, 59 of 98
families ever used, 39 never used at all, top-10 families = 45.1% of every pick.** This module turns
that into a standing report instead of a one-off manual count.

## Design

### It reads committed content, not corpus metadata

Unlike a roster's own intrinsic characteristics, "usage" only exists once generation has happened.
This module reads every `data/seed/actions/_candidates/{general,family,signature}/round-*.json`,
filters to `outcome == "accepted"`, and tallies `draft.atomFamilies`.

**One accepted row counts each of its own families once**, never once per occurrence — a 2-family
bundle contributes 1 to each of its two families' counts, not weighted by bundle size. Counting a
bundle's own repeats would conflate "this bundle is big" with "this family is popular", which are
different facts.

**`round-*-model-experiment.json` files are excluded by name** — they are deliberately off-baseline
comparisons (the refuted model-swap experiment), not real production output, and mixing them in
would double-count the same briefs under a different model.

### Two counts, and why both matter

- **All-time usage** — every round ever committed, the honest cumulative picture.
- **Latest-round usage** — the most recent round only, which is what FC3 actually needs to weight the
  *next* round. All-time usage answers "how are we doing"; latest-round answers "what should change
  next", and conflating them would let one old, no-longer-representative round keep suppressing a
  family the current pipeline already handles fine.

### What it computes, matching the technique that already found this bug

Same discipline as the (superseded) species-roster attempt, correctly reused here because the
underlying statistics are sound even though the corpus was wrong: **normalised Shannon evenness**
(`H / log2(k)`, scale-free, 1.0 uniform / 0.0 degenerate) and **top-N share**, over the 98-family
population — not over only the families that appear (a family with zero picks is a real data point,
not an absence of one).

### The policy — folded in here, not a separate module

One threshold set, small enough that a dedicated policy module would be ceremony:

```jsonc
// data/tuning/action-family-usage.v1.json
{ "version": 1,
  "minEvennessMilli": 850,      // per-mille; 0.85 — see "why 0.85" below
  "maxTop10SharePermille": 300, // no ten families may be 30%+ of all picks
  "minFamiliesUsedShare": 400   // at least 40% of the 98 must appear at all
}
```

**Why 0.85, not the 0.90 the species program chose:** this population is 98-wide with a genuinely
free-form multi-select pick per brief (not a forced single classification), so perfect evenness is
neither achievable nor desirable — some families are mechanically closer to what most briefs need.
0.85 is chosen as a floor that flags the measured 0.908 as passing-but-watched while still catching a
real regression; **it is a starting value, and this file's own header says so**, matching this
repo's tuning-file convention of never presenting a first guess as calibrated.

### It reports the never-used set by name, not just by count

**39 never-used families are 39 different, individually addressable facts**, not one number. The
report lists every one, because FC3 needs to know *which* families to weight up, not merely that
some exist.

## Commands

```powershell
python -m seedsmith actions usage-stats                 # human-readable report
python -m seedsmith actions usage-stats --json          # machine-readable, for FC3
python -m pytest tools/seedsmith/tests/test_usage_stats.py
```

## Project structure

```text
data/tuning/action-family-usage.v1.json                          new — the policy, per §"the policy"
tools/seedsmith/seedsmith/adapters/actions/usage_stats/derive.py new — tally + evenness + verdict
tools/seedsmith/seedsmith/adapters/actions/generate_usage_stats.py new — CLI entry point
docs/research/action-corpus/_usage-<date>.json                   new — the emitted report
tools/seedsmith/tests/test_usage_stats.py                        new
```

## Code style

```python
# One accepted row counts each of its OWN families once, never once per occurrence -- a bundle's own
# size must not be conflated with a family's popularity.
def tally(rows: "Sequence[Mapping]") -> "Counter[str]":
    counts: "Counter[str]" = Counter()
    for row in rows:
        if row.get("outcome") != "accepted":
            continue
        counts.update(set((row.get("draft") or {}).get("atomFamilies") or ()))
    return counts
```

## Testing strategy

| Test | Asserts |
|---|---|
| `a_bundle_counts_each_family_once_never_by_repeat` | a 2-family bundle contributes exactly 1 to each, not weighted |
| `model_experiment_rounds_are_excluded_by_name` | the refuted swap file never enters the tally |
| `unresolved_and_blocked_rows_never_contribute` | only `outcome == "accepted"` counts |
| `evenness_is_computed_over_the_full_98_population` | a family with zero picks affects the number, not merely absent families |
| `never_used_families_are_named_individually` | the emitted list, not just a count |
| `all_time_and_latest_round_are_reported_separately` | and are demonstrably different on a fixture where they diverge |
| `PLANTED_VIOLATION_a_float_threshold_in_the_tuning_file_is_refused` | per-mille integers only |
| `the_real_corpus_reproduces_the_measured_baseline` | 216 accepted / 59 of 98 used / 39 never-used / top-10 share ≈45.1% |
| `the_report_is_byte_identical_across_two_runs` | sorted keys, never dict/filesystem order |
| `an_unreadable_round_file_is_a_named_error_not_a_silent_skip` | corrupt/missing file surfaces, not vanishes |

## Boundaries

**Always:** count real accepted rows only; report the full never-used list by name; keep every
threshold in `data/tuning/`.

**Ask first:** changing which round files count as "real" (currently: everything except
`*-model-experiment.json`).

**Never:** weight a bundle's families by bundle size; conflate all-time and latest-round usage; touch
the affix-family corpus itself (it measures healthy — nothing here to correct); call a model.

## Success criteria

- [ ] Reproduces the measured baseline against the real round files: 216 accepted, 59/98 families
      used, 39 never-used (named), top-10 share ≈45.1%.
- [ ] `minEvennessMilli`/`maxTop10SharePermille`/`minFamiliesUsedShare` all live in
      `data/tuning/action-family-usage.v1.json`.
- [ ] Two runs over unchanged input are byte-identical.
- [ ] All-time and latest-round counts are reported as two distinct numbers.
