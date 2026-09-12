# Implementation plan: `action-distribution-gaps` — closing the coverage loop

**Map:** [action-corpus-map.md](../docs/architecture/action-corpus-map.md).
**Task list:** [action-distribution-gaps-todo.md](action-distribution-gaps-todo.md).

⛔ **Why this program exists.** A 2026-09-12 audit of the action generator's distribution, after the
allocation-engine repairs, found the pipeline mechanically sound but **unable to reach a clean
coverage verdict by construction**. Two gaps remained: `action.corpus.enablerPayoffCoverage` (a real
defect) and `action.corpus.thinCell` (a gate-semantics defect that also exposed a missing design
edge). This plan closes both and restores the loop the design already specifies.

---

## 1. What the audit found

### The planner already does its job

`distribution_planner` (A-S1) plans a fixed count per subject over the live roster and allocates
**category, targetMode, areaShape, pairing role, rung window and pools** deterministically. Measured
on the current 6,655-brief plan, **all 15 `(scope, category)` cells are covered and every cell's
quota equals its planned brief count exactly** (`species.attack` 976 = 976 briefs, etc.). Cell
coverage is not missing.

### But the loop the design specifies was never wired

The design is explicit that A-S1 is **closed-loop over rounds**:

| Source | States |
|---|---|
| `action-corpus-ideal.md` §15 flowchart | `S5 -->|"round n+1 targets"| S1` |
| `action-corpus-ideal.md` §11.4 | *"A statistics pass finds thin distribution and plans the next round"* |
| `action-corpus-ideal.md` §13 | *"Top-up rounds are explicit and numbered. Round n+1's briefs are derived deterministically from round n's coverage report"* |
| `spec-coverage-report.md` §7 | *"**Depended on by: A-S1, which reads the report to build round n+1's briefs.** The cycle is broken by round 1 reading no report at all."* |
| `spec-distribution-planner.md` §7 | *"Depends on … **A-S5** (round n+1 targets — a cycle that is broken by round 1 reading no report)"* |

**The implementation has no such edge.** `generate_distribution_planner.regenerate()` takes no report
parameter and reads nothing from `_reports/` except `is_passing_quality_gate()` — and uses that only
to check the *verdict*, never to consume targets. `next_round_targets()` emits
`target.round-N+1.<scope>.<subject>.<category>` with a `want` shortfall, and **nothing consumes it**.
Only `round-1.json` exists; no round-2 plan can be produced.

### The consequence: `thinCell` can never clear

Each cell's quota is the **desired accepted corpus**. A single round's draw cannot fill it: measured
end-to-end yield is **66%** (validate 88% × dedup 80%), and no run accepts 100% of its briefs. So
`thinCell` (accepted < quota) is a **permanent** reading unless a later round tops up the shortfall —
which is exactly what the unwired `S5 → S1` edge was for. The gate is not measuring a defect; it is
measuring that the top-up round never happened.

### G3 — the species gate was at the WRONG GRANULARITY (fixed 2026-09-12)

`cellOccupancy`/`thinCell` gate on `cell.<scope>.<category>.<band>`. At species scope that is **one
aggregate over all 904 species**: `cell.species.attack.1-10` had quota **976** (= 904 × 5 × 21.6%).
That number can be satisfied by spreading 976 attack rows over ~100 species (9–10 each) while **804
species hold no attack action** — the cell reads perfect. Measured: **828 of 904 species had zero
accepted actions, and no metric reported it.** The `next-target` derivation already names individual
species, so the per-subject view was assumed but never gated.

**Fixed** with a new CLOSED metric **`action.corpus.speciesCoverage`**
(`species_coverage_findings`), one GAP Finding per uncovered species, required universe taken from
the same `subject_category_counts` the quota was recomputed for (no second roster read, no way to
disagree with the planner). It asserts the **contract** — *the set of species with zero rows is
empty* — never a population literal, per `validation-ssot.md`.

### G4 — a dead, contradictory default (fixed 2026-09-12)

`coverage_report/derive.py` carried `SIGNATURE_ACTIONS_PER_SPECIES = 3` as the default for
`roster_reconciliation_findings`. That is the **sealed ideal's** B1 number; the shipped tuning sets
`perSpeciesCount: 5`. The default was dead (the one caller always passes `cov.per_species_count`),
but a wrong default never reached is still a second source of truth the next reader trusts. The
constant is **deleted** and the parameter is now **required**, with tests asserting both.

### G5 — the per-species innate guarantee was stale and unrun (fixed 2026-09-12)

`species-innate.json` held **84 entries with `tuningVersion: 1`** — the legacy catalog projection,
820 species out of date — so the design's one-per-species innate guarantee (ideal §11, S6) could not
be read off the file at all. **Fixed** by running S6 against the current plan: **904 entries, 81
picks, 823 nulls**, with its 53 round-2000 survivors promoted into `committed-round-2000.json`
(126 → 179 committed rows). A guard test now asserts the file covers the live roster exactly and is
not the legacy corpus hash.

**Honest reading:** 823 nulls is correct, not a defect — only 179 accepted rows exist, so 823 species
genuinely have no eligible action yet. The tier becomes populated as later rounds fill the corpus;
what changed is that the file now *tells the truth about 904 species* instead of 84.

### G1 — payoff keys leak into the non-pairing round-robin (fixed 2026-09-12)

A-S7's rotation walked **all** families, including the payoff keys. Because `assign_pairing_roles`
only ever emits pairing briefs on family/general scope, a **species** `role: "none"` brief that
required `atom.rot-punisher` could never have a matching enabler. Measured: **74 anchors** carried a
guaranteed future gap. **Fixed** (`payoff_families` excluded from the round-robin; payoff keys stay
covered via their 456 `role: "payoff"` briefs). Verified: 0 `role: none` briefs require a payoff key;
both keys still covered; 123 distinct required families (unchanged breadth).

### G2 — a model-picked payoff key gained no enabler (fixed 2026-09-12)

The model may select any family in
`allowedAtomFamilies`, including a payoff key the plan never required; the accepted row then carried
a payoff with no same-anchor enabler (`caltropnut`, `snowgatling` were the live cases). **Fixed**
with a deterministic, zero-model-call `splice_payoff_enablers` that unions in the first
`pairings.json` enabler that is also in that brief's own allowed pool.

### The gate-semantics defect

All 10 action-corpus metrics are `gates=False` (the framework's rule: *"starts False for every new
metric; promotion is a deliberate, later act"*, and `MetricRegistry.register` enforces the
OPEN-loop case). The general reporter honours it (`{m.id for m in registry.all() if m.gates}`), but
A-S5's `compute_verdict` **ignores `gates` entirely** and treats any GAP as `not-clean`. So
measure-only metrics block the full-run gate.

**Combined effect — a closed loop that cannot open:** `mode: "full"` requires a passing gate → the
gate requires `thinCell` green → `thinCell` requires a filled corpus → a filled corpus requires
`mode: "full"`. The gate is unreachable by construction, which is why the plan had to be generated
through a smoke-mode copy during the earlier repair.

---

## 2. Phases

### Phase 0 — the two A-S7 defects (DONE 2026-09-12)

- **T0.1** Exclude payoff keys from the non-pairing round-robin. **XS** · `coverage_assignment/derive.py`, `generate_coverage_assignment.py`.
- **T0.2** `splice_payoff_enablers` + wire into A-P1/A-P2/A-P3 `finalize_candidate`. **S** · three `propose/derive.py`.
- **T0.3** Tests: `PayoffExclusionTests`, `RealPlanPayoffTests`, `SplicePayoffEnablersTests`. **XS**.

### Phase 1 — gate semantics (needs the owner's decision)

The open question is **what `thinCell` should mean**. Three positions, all defensible:

| Option | Meaning | Cost |
|---|---|---|
| **A. Honour `gates=False`** | Measure-only metrics report and feed targets, but only a promoted (`gates=True`) metric can make a verdict `not-clean`. Matches the framework's documented rule and the general reporter. | S — `compute_verdict` one-line change + tests |
| **B. Redefine `thinCell`'s threshold** | Keep every GAP gating, but judge a cell against a measured *fill ratio* (e.g. below its share of achievable yield) rather than 100% of quota. Needs a tuning number. | S |
| **C. Keep gating, converge over rounds** | Promote `thinCell`/`cellOccupancy`, and define the corpus complete only at ~100% fill via multi-round top-up. Requires Phase 2. | L |

**Recommended: A + C.** A fixes the semantics (a measure-only metric must not gate); C restores the
designed loop and is the thing that actually fills the corpus. They are complementary, not
alternatives — A decides *who may block*, C decides *how the corpus completes*.

- **T1.1** Decide and record the semantics in `spec-coverage-report.md` (owner).
- **T1.2** Implement the chosen `compute_verdict` policy + a test pinning it. **S**.
- **T1.3** Reconcile with the general reporter's `gates` use, so A-S5 and `report/cli.py` cannot
  disagree about what a pass is. **XS**.

### Phase 2 — the `S5 → S1` top-up round (the real fix for `thinCell`)

- **T2.1** `distribution_planner.regenerate` accepts an optional prior coverage report and reads its
  `next-target` rows. **M** · `generate_distribution_planner.py`.
- **T2.2** `plan_round` accepts per-`(scope, scopeKey, category)` top-up counts and merges them into
  the base quota, keeping every allocation exact (`apportion_axis` already guarantees it). **M** ·
  `distribution_planner/derive.py`.
- **T2.3** Round numbering: plan round n+1 from round n's report; `_accepted_neighbours_by_group`
  already reads rounds `< before_round`, so the dedup guard is correct as-is. **S**.
- **T2.4** Convergence criterion: a round is complete when no cell is thin, or a bounded round count
  is reached, whichever first. State it explicitly, never loop unbounded. **S**.
- **T2.5** A model-free test: given a synthetic report with known shortfalls, round n+1's plan
  contains exactly the top-up briefs, and the merged quota is still exact. **M**.

### Phase 3 — verify on real content

- **T3.1** Run one real round against the current plan; confirm `enablerPayoffCoverage` is green (G2)
  and the only remaining gap is `thinCell`. **M**.
- **T3.2** Run the top-up round; confirm `thinCell`'s shortfall shrinks by the measured yield and no
  new `quotaDrift` appears. **M**.
- **T3.3** Only then is `mode: "full"` reachable; run it and record the honest verdict. **L**.

---

## 3. What this program does NOT do

- **Does not make one round fill the corpus.** 100% acceptance is not a target and never was; the
  yield is a real property of the model stage.
- **Does not weaken A-S5.** It corrects *who may gate* and *how completion is reached*; the metrics
  themselves stay exactly as measured.
- **Does not promote any metric to `gates=True`** unless the owner chooses Option C, and then only
  deliberately and with a recorded threshold.

## 4. Success criteria

1. `enablerPayoffCoverage` produces **zero** GAPs on real content (G1 + G2 done; T3.1 confirms).
2. A-S5's verdict semantics match the framework's `gates` rule, pinned by a test.
3. Round n+1's plan is a pure function of round n's report, and the round sequence is replayable.
4. `mode: "full"` is reachable after the designed number of rounds, with an honest recorded verdict.
