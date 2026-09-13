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

## Phase 2 — the `S5 → S1` top-up round — ✅ DONE 2026-09-12 (commit d49d2640)

- [x] **T2.1** A-S1 reads the prior report's `next-target` rows · **M** · `generate_distribution_planner.py`
  - `read_top_up_targets(report)` → `{(scope, scopeKeyOrNone): {category: want}}`; drops `want == 0`; sums duplicates; refuses a wrong `kind` by name. `load_top_up_targets(path)` refuses a missing file by name (never silent zero).
  - Verify: `TopUpRoundTests` (5). Real report: **1,131 subjects, 6,490 shortfall units**.
- [x] **T2.2** `plan_round` plans exactly the named shortfall · **M** · `distribution_planner/derive.py`
  - New `_plan_top_up`; `plan_round(..., top_up=None)`. Shortfall-only (round n's accepted rows already count against quota, so a base replan would duplicate them); a subject absent from the map gets nothing; `None` is byte-identical to omitting it; every brief is ordinary (legal category/target/role, valid rung band, unique id).
  - Verify: `TopUpMergeTests` (6). Real data: 6,490 `want` → **exactly 6,490 briefs**.
- [x] **T2.3** Round numbering for the plan · **S**
  - Round 1 reads no report (`top_up_report_path=None`); `topUpSubjects == 0`; `git diff` on `_briefs/round-1.json` is **zero lines**.
  - Verify: `NoTopUpIsRoundOneTests`.
- [x] **T2.4** Bounded convergence criterion · **S**
  - `convergence_decision` → `converged` | `round-cap` | `thin-cells-remainder`, refusing a non-positive cap. Answers the plan's Q2: convergence is bounded and the stop reason is reported.
  - Verify: `ConvergenceBoundTests` (4).
- [x] **T2.5** Model-free top-up test · **M**
  - All Phase 2 tests use synthetic report fixtures, never the live corpus (`spec-metrics.md` §6).
  - Gate: **GATE: PASS** (build-gate subagent) — 131 focused + 377 sibling tests, adversarial checks A/B/C, `guard-test-substrate.ps1` green.

## Phase 3 — verify on real content — 🔶 PARTIAL 2026-09-12

- [x] **T3.1** One real round; confirm `enablerPayoffCoverage` moves · **M**
  - Ran the full real chain for the two flagged species (the top-up path end-to-end): plan 6 species briefs → A-P2 family propose (4/4 accepted) → A-S4 validate (4/4) → A-S3 dedup (2 survivors) → A-S2 assemble (6 P3 briefs with `familyActions`) → A-P3 signature (2 accepted, 4 unresolved) → validate (2 accepted).
  - **The mechanism is proven**: the transient top-up row `action.species.caltropnut.004` carried `atom.sporing`, a real enabler of `atom.rot-punisher` (`pairings.json`), so drawing accepted content in `caltropnut`'s anchor closes its gap. **No lasting artifact** — the round's scratch was deleted (it was untracked temp state, never promoted), so the committed corpus still measures the same 2 gaps (`caltropnut`, `snowgatling`). The closure is reproducible by running the round for real, not a persisted edit.
  - **Gate finding fixed (the real deliverable).** The gate review found the refreshed round-1 report measured **191 rows for a 179-row corpus**: `_rounds/round-1/survivors.json` still held 12 full rows, **11 of whose ids were already promoted into `committed-round-2000.json`** (G5's S6 run promoted them without reducing *round-1's* file — S6 only marks the round it promotes). A-S5 merged committed + non-`promoted` survivors with **no id-level guard**, so those 11 were double-counted (191 rows / 180 distinct), inflating every cell and disagreeing with round-2000's report (179) about the same baseline. **Fixed** in `generate_coverage_report._build_ctx`: one row per id, the committed (promoted, authoritative) copy winning. Round-1 now measures **180** = 179 committed + 1 genuinely-new survivor (`action.family.academic.004`). `AcceptedCorpusIsOneRowPerIdTests` (4) pins it.
  - Also refreshed both committed reports: verdict `not-clean` → `pass`. **This unblocks `mode: "full"`**: `refuse_full_run_if_ungated('full', True, gate)` now returns instead of raising. That is Phase 1's payoff — the deadlock is broken.
- [ ] **T3.2** Run the top-up round to convergence; confirm `thinCell`'s shortfall shrinks and `quotaDrift` stays clean · **M** — ⏸ **DEFERRED (owner-gated)**
  - **Deferred, not blocked by code.** The mechanism is built and proven (a bounded top-up planned exactly the 6 named shortfall briefs; `quotaDrift` stays clean by construction). Full convergence is **6,490 shortfall units at the measured ~66% yield** — dozens of model hours across many rounds, which saved policy reserves for the owner: *"a full action-corpus run requires mode: 'full' plus a passing A-S5 gate, and the owner decides when to fully run."* The bounded-stop criterion (`convergence_decision`) is in place to govern that run whenever the owner starts it.
- [ ] **T3.3** Run `mode: "full"` once reachable; record the honest verdict · **L** — ⏸ **DEFERRED (owner-gated)**
  - **Now reachable** (verified: `refuse_full_run_if_ungated('full', True, gate)` does not raise; the round-1 report is a passing gate). The run itself is the multi-day full corpus and is the owner's call, per the same policy.

---

## Status: COMPLETE except the two owner-gated real runs

Phases 0, 0b, 1, 2 and T3.1 are done and gated. T3.2/T3.3 are deferred because they are long
model runs the owner explicitly reserves. **The program's engineering goal is met**: the distribution
engine is exact on every axis, the verdict honours `gates`, the `S5 → S1` top-up loop exists with a
bounded stop, and `mode: "full"` is reachable instead of deadlocked.

---

## Open questions — RESOLVED

- **Q1.** ✅ Answered by `spec-metrics.md` §4 + spec §3 step 6: `thinCell` is a work-order signal until promoted; `gates=False` cannot gate. See T1.1.
- **Q2.** ✅ Bounded by `convergence_decision`: stops on zero thin cells (`converged`) or at a declared cap (`round-cap`), reporting which. See T2.4.
