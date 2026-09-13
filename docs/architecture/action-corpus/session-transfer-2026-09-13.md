# Session transfer prompt — action-corpus distribution

**Copy everything in the fenced block below into a fresh agent session.** It is self-contained: it
names the files to read, the exact commands to run, the verified state, the open items, and the
constraints that must not be violated. The human can also paste just the block; the prose above it is
for the human.

---

````text
You are continuing an investigation into the Seedsmith action-corpus generator in
D:\Works\source\plant-vs-zombie-rise-of-summoner (branch features/derived-stat-extension).

READ FIRST, IN THIS ORDER — do not skip or skim:
1. docs/architecture/action-corpus/audit-2026-09-13-distribution.md   <- the full findings + handoff
2. tasks/action-distribution-gaps-plan.md and -todo.md                 <- the program
3. docs/architecture/action-corpus/spec-coverage-report.md             <- A-S5's contract
4. docs/architecture/action-corpus/spec-distribution-planner.md        <- A-S1's contract
5. docs/architecture/seedsmith/spec-metrics.md (§4 Calibration)        <- why gates=False exists
6. AGENTS.md (session boundary, commit policy) and docs/DESIGN-GATE.md

STATE YOU INHERIT (verified 2026-09-13, all committed, tree clean for program paths)
- Plan: data/seed/actions/_briefs/round-1.json — 6,655 briefs (1000 general / 1135 family /
  4520 species), corpusHash 945e899bbe90aa5f..., tuningVersion 3, ALGORITHM_VERSION 2.
- All four allocation axes are exact: category (species 976/926/860/850/908), targetMode (all 6
  reachable incl. `area`: 750 species + 188 family), areaShape (all 4 reachable:
  188/188/187/187 species, 47/47/47/47 family), quotaDrift clean.
- Accepted corpus: 179 committed rows (committed-round-1/2/909/2000 = 19/5/102/53).
- Coverage report data/seed/actions/_reports/coverage-round-1.json: verdict "pass",
  gatingMetrics [], notMeasuredMetrics [], gapMetrics [enablerPayoffCoverage, speciesCoverage,
  thinCell], acceptedCorpusSize 180.
- Fill: 180 / 6655 = 2.7%. Shortfall: 5,387 next-target rows, 6,490 units, 904 species uncovered.
  15/15 cell groups still thin.
- mode: "full" is REACHABLE: refuse_full_run_if_ungated('full', True, gate) does not raise.
- Endpoint: LM Studio http://localhost:1234/v1, model google/gemma-4-26b-a4b-qat.
- Tests: 276 focused; 3,818 in tools/seedsmith/tests (3,144 subtests). guard-test-substrate green.
- Commits: bdd91b68 (gates decide verdict), d49d2640 (S5->S1 top-up), 860f43aa (one row per id),
  9a05b0ba (defer owner runs), 4cad31c4 (the audit doc).

DO NOT RE-INVESTIGATE. These are fixed, tested and gated; treat them as given:
- The four allocation defects (per-subject largest-remainder was inert at count=5: category flat
  1/1/1/1/1 for all 1,131 subjects; targetMode starved `area` for all 1,131; areaShape all-row at
  750/750 species; blocked-frame sequencing gave 134-long runs and 56% dedup waste). Fixed by the
  scope-level deficit-greedy apportion_axis / apportion_area_shapes, plus stride expansion in
  expand_counts.
- The open-flavor-text vs closed-14-trait-pool mismatch (2,312 tokens, 1 match). Fixed by
  characteristic_pool/curation.py mirroring DemonTraitPoolCuration.
- G1 payoff keys in the non-pairing round-robin (74 impossible anchors). G2 model-picked payoff
  gained no enabler (splice_payoff_enablers). G3 the species gate was scope-aggregate only
  (828/904 uncovered, unreported). G4 dead SIGNATURE_ACTIONS_PER_SPECIES=3 vs tuning 5.
  G5 species-innate.json stale at 84 (now 904 entries, 81 picks).
- compute_verdict ignored `gates` -> full-run deadlock. Now only NOT_MEASURED and gating GAPs block.
- The S5->S1 top-up loop was unwired. Now read_top_up_targets + plan_round(top_up=...) +
  convergence_decision. Proven: 5,387 target rows / 6,490 want -> exactly 6,490 briefs.
- The accepted corpus double-counted 11 ids (191 rows for a 179-row corpus, because
  _rounds/round-1/survivors.json still holds ids promoted into committed-round-2000.json). Fixed in
  generate_coverage_report._build_ctx: one row per id, committed wins.

WHAT IS ACTUALLY OPEN (your job)
O1. The corpus is 2.7% full. Not a defect — it needs accepted content. This is the multi-hour run.
O2. Stale candidate scratch blocks the default run path:
    _candidates/{general,family,signature}/round-1.json carry pre-fix hash d73e1db06b24c4ed and
    run_pipeline refuses them by design. A real run needs --fresh, or move that scratch aside
    (gitignored; it is the owner's data, so ask before deleting).
O3. Two legacy payoff gaps: caltropnut and snowgatling in committed-round-909.json carry
    atom.rot-punisher with no enabler. Their round-909 plan no longer exists so they cannot be
    regenerated; splice_payoff_enablers deterministically fixes both (adds atom.venomous), but the
    rows carry _provenance so a hand edit violates the repo's generated-data rule — a repair must be
    a generator/maintenance path.
O4. Producer-side invariant still violated: _rounds/round-1/survivors.json holds 11 full rows whose
    ids are committed. The consumer guard neutralises the damage; a future consumer without the
    guard would break again.
O5. No CLI for a round-scoped top-up: the plumbing exists (regenerate(top_up_report_path=...)) but
    `seedsmith report` has no --top-up-from flag.

ACTION PLAN (from the audit's §10)
Phase A, do first, ~half a session, all safe:
  A1 (S) add `seedsmith actions plan --round N --top-up-from _reports/coverage-round-<N-1>.json`.
  A2 (S) move the stale _candidates/*/round-1.json aside so the default path works without --fresh.
  A3 (XS) document the top-up runbook (one real round, and convergence).
  A4 (S) fix O4: warn/assert on a duplicate, or reduce stale round files to markers.
Phase B, owner-gated, hours — do NOT start without explicit owner agreement:
  B1 (L) one real round over the current plan (~23-28h at ~66% yield).
  B2 (L) top-up rounds until convergence_decision returns converged or a pre-declared cap.
  B3 (M) record the honest final verdict; promote a metric only deliberately.
Phase C, optional hardening: promote a metric with a budget threshold; author more pairings; confirm
whether `relation` being a pure function of `category` is intended.

HOW TO RUN THINGS (PowerShell, repo root)
  $env:PYTHONPATH = "tools/seedsmith"
  python -m pytest tools/seedsmith/tests/test_coverage_report.py -q
  python -m pytest tools/seedsmith/tests -q
  # coverage report for a round (writes data/seed/actions/_reports/coverage-round-<n>.json):
  python -c "import sys;sys.path.insert(0,'tools/seedsmith');from seedsmith.adapters.actions.generate_coverage_report import regenerate;print(regenerate(round_no=1, write=True))"
  # plan a top-up round from a report:
  from seedsmith.adapters.actions import generate_distribution_planner as g
  g.regenerate(actions_root=<target>, top_up_report_path=g.ACTIONS_ROOT/'_reports'/'coverage-round-1.json', full_flag=True, write=True, round_no=2)
  # the gate:
  g.is_passing_quality_gate(g.SMOKE_GATE_EVIDENCE_PATH)  # True today

HARD CONSTRAINTS — violating any of these is a defect
- Generated seed data is never hand-edited. data/seed/actions/** carries generator _provenance; fix
  the generator and regenerate, or add a sanctioned generator repair path.
- Never run raw git commit/git add; it is mechanically blocked. Commit only via the repo-git.commit
  MCP tool, with EXPLICIT paths, never all=true — parallel programs share this tree. Run
  validate_message first if unsure. Push is owner-only.
- A guardrail validates the CONTRACT and closed enums, never a population count or generated text
  (docs/architecture/validation-ssot.md). The roster grows per shipped species, so 904/227/1183 and
  any corpus total are READINGS, not constants. Assert joins, uniqueness, reconciliation,
  determinism, bounds — never a literal that a content shipment would falsify.
- A metric may not gate until it is promoted (gates=True with a calibrated threshold in `budget`).
  spec-metrics.md §4: measure, look, set, gate.
- Read docs/DESIGN-GATE.md §1 before proposing any design change, and read the docs it names.
- Do not weaken the freshness check, the gates semantics, or the plan hash to make a run pass.

SUGGESTED FIRST MOVE
Do Phase A only (A1-A4), each as its own commit, each gated by the build-gate subagent. Then present
the Phase B duration estimate and the declared round cap to the owner and WAIT — Phase B is dozens of
model hours and the owner decides when to run it. If the owner instead wants the corpus filled now,
start B1 only after they confirm the duration, and stop-and-ask on any §4 blocker from the build
skill.
````
