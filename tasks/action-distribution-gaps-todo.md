# Todo: `action-distribution-gaps` — closing the coverage loop

Plan: [action-distribution-gaps-plan.md](action-distribution-gaps-plan.md). **5 phases, 15 tasks.**

Sizes: **XS** 1 file · **S** 1-2 · **M** 3-5 · **L** multi-run.

---

## Phase 0 — the two A-S7 defects — ✅ DONE 2026-09-12

- [x] **T0.1** Exclude payoff keys from the non-pairing round-robin · **XS** · `coverage_assignment/derive.py`, `generate_coverage_assignment.py`
  - Acceptance: `assign_required_families(..., payoff_families=...)` never returns a payoff key for a
    `role: none` brief; a `role: payoff` brief still returns its own key; the default (empty) payoff
    set is byte-identical to pre-fix behaviour.
  - Verify: real plan regenerated — **0** `role: none` briefs require a payoff key (was 74 anchors);
    both keys still covered via 456 payoff-role briefs; 123 distinct required families (unchanged).
- [x] **T0.2** `splice_payoff_enablers` + wire into A-P1/A-P2/A-P3 · **S** · three `propose/derive.py`
  - Acceptance: a model-picked payoff key gains the first `pairings.json` enabler present in the
    brief's own `allowedAtomFamilies`; an existing enabler is not duplicated; no allowed enabler adds
    nothing; non-payoff input is unchanged; deterministic.
- [x] **T0.3** Tests · **XS** · `test_coverage_assignment.py`
  - Verify: `PayoffExclusionTests` (3), `RealPlanPayoffTests` (2), `SplicePayoffEnablersTests` (5).
    `test_coverage_assignment.py`: **29 passed**. Propose suites: **287 passed, 1 skipped, 1176 subtests**.

## Phase 0b — granularity and staleness defects — ✅ DONE 2026-09-12

- [x] **T0b.1** **G3** — add the per-species coverage gate · **M** · `coverage_report/derive.py`, `metrics/action_coverage.py`, `coverage_report/ctx.py`
  - Root cause: `cellOccupancy`/`thinCell` gate on `cell.species.<category>.<band>`, ONE aggregate over 904 species; quota 976 could be met by ~100 species while 804 held nothing. Measured: 828/904 species had zero accepted actions, unreported.
  - Acceptance: new CLOSED metric `action.corpus.speciesCoverage`, one GAP Finding per uncovered species; required universe == the planner's `subject_category_counts` species keys; contract-based (empty uncovered set), never a population literal.
  - Verify: `SpeciesCoverageMetricTests` (5) — uncovered naming, full-coverage clean, aggregate-blind-by-construction, universe source, determinism.
- [x] **T0b.2** **G4** — delete the contradictory `SIGNATURE_ACTIONS_PER_SPECIES = 3` default · **XS**
  - Root cause: the sealed ideal's B1 number was a module-constant default; the shipped tuning is `perSpeciesCount: 5`. Dead but trusted-on-read.
  - Acceptance: constant gone; `signature_actions_per_species` is a REQUIRED parameter; the metric passes `cov.per_species_count`.
  - Verify: `SignatureCountIsRequiredNotDefaultedTests` (3) — no constant, no default, metric reads tuning.
- [x] **T0b.3** **G5** — run S6 against the current plan · **S**
  - Root cause: `species-innate.json` held 84 entries / `tuningVersion: 1` (legacy catalog), so the one-per-species guarantee described a roster 820 species stale.
  - Verify: now **904 entries, 81 picks, 823 nulls**; 53 round-2000 survivors promoted into `committed-round-2000.json` (126 → 179 committed). `LiveInnateFileTests` (2) assert live-roster coverage and non-legacy hash.
- [x] **T0b.4** Convert the population-pinning real-corpus test · **XS** · `test_coverage_report.py`
  - Root cause: `assertEqual(summary["acceptedCorpusSize"], 138)` pinned a population; the S6 run legitimately moved it to 191.
  - Acceptance: asserts the corpus reconciles to committed + survivors, never a literal (validation-ssot.md).

## Phase 1 — gate semantics — ✅ DONE 2026-09-12 (commit bdd91b68)

- [x] **T1.1** Decide and record what `thinCell` means · **S** · `spec-coverage-report.md`
  - **DECIDED: Option A.** Spec-answerable, not an open product call — `spec-metrics.md` §4 (*"New metric → `gates=False`, runs, reports. **Then** a threshold goes into `budget` and `gates` flips"*) and spec §3 step 6 (*"`pass` requires every **gating** CLOSED metric green"*) already fix it; `compute_verdict` had diverged from its own spec.
  - Recorded in spec §3 step 6 (the decision + why it was blocking) and §4 (the converse rule).
- [x] **T1.2** Implement the chosen `compute_verdict` policy · **S** · `coverage_report/derive.py`
  - `compute_verdict(..., gating_metric_ids=)` blocks on `NOT_MEASURED` (any CLOSED metric) and on a `GAP` from a **gating** metric; an unpromoted `GAP` stays in `gapMetrics` and does not block. `Verdict.gating_metrics` added and serialized as `gatingMetrics`; the caller passes `{m.id for m in registry.all() if m.gates}`.
  - Verify: `VerdictHonoursGatesTests` (5). Focused: **50 passed** in `test_coverage_report.py`.
- [x] **T1.3** Reconcile with the general reporter's `gates` use · **XS**
  - `is_passing_quality_gate` no longer requires `gapMetrics == []` — it defers to A-S5's verdict string, which was the definition A-S5 had to abandon. `NOT_MEASURED` stays blocking as belt-and-braces.
  - Verify: `test_gate_reads_the_verdict_flag_not_the_gap_list`; planner suite **114 passed**. Gate subagent confirmed no other call site re-derives the verdict (`gapMetrics` non-test hits are serialization/summary only), and that `report/cli.py` builds the identical `if m.gates` set.
  - Gate: **GATE: PASS** (build-gate subagent) — 164 focused, 3797 full-suite, adversarial checks both directions.

## Phase 2 — the `S5 → S1` top-up round

- [ ] **T2.1** A-S1 reads the prior report's `next-target` rows · **M** · `generate_distribution_planner.py`
  - Acceptance: `regenerate` accepts an optional report path; round 1 reads none (cycle stays broken);
    a missing/malformed report is refused by name, never silently treated as zero shortfall.
- [ ] **T2.2** `plan_round` merges per-`(scope, key, category)` top-up counts into the base quota · **M** · `distribution_planner/derive.py`
  - Acceptance: merged quota is still exact (`apportion_axis` invariant holds); every top-up brief is
    a normal brief with the same contract.
- [ ] **T2.3** Round numbering for the plan · **S**
  - Acceptance: `_briefs/round-<n+1>.json` is planned from `coverage-round-<n>.json`;
    `_accepted_neighbours_by_group(before_round=n+1)` reads the correct earlier rounds.
- [ ] **T2.4** Bounded convergence criterion · **S**
  - Acceptance: a round loop stops when no cell is thin OR a declared round cap is reached, and the
    stop reason is reported. Never unbounded.
- [ ] **T2.5** Model-free top-up test · **M**
  - Acceptance: given a synthetic report with known shortfalls, round n+1's plan contains exactly the
    top-up briefs; quota stays exact; determinism holds.

## Phase 3 — verify on real content

- [ ] **T3.1** One real round; confirm `enablerPayoffCoverage` green · **M**
- [ ] **T3.2** Run the top-up round; confirm `thinCell`'s shortfall shrinks by the measured yield and
      `quotaDrift` stays clean · **M**
- [ ] **T3.3** Run `mode: "full"` once reachable; record the honest verdict · **L**

---

## Open questions

- **Q1.** Is `thinCell` a *gate* or a *work-order signal*? The design calls it "thin cells + next
  targets" and wires it to round n+1 — which reads as a signal. But the metric is named alongside
  the closed metrics. **Owner decision (T1.1).**
- **Q2.** How many top-up rounds is the corpus allowed before a verdict is taken? Needs a declared
  bound (T2.4) — otherwise "converge over rounds" is an unbounded promise.
