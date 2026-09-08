# Todo: `action-corpus` — A-S7 `coverage-assignment`

Plan: [action-corpus-plan.md](action-corpus-plan.md). **1 module, 4 phases, 10 tasks, model-free
except Phase 4's own real proof.**

Sizes: **XS** 1 file · **S** 1-2 · **M** 3-5.

---

## Phase 0 — A-S1's own small, additive dependency

- [x] **T0.1** `pairing.forcedEnabler` emitted by `plan_subject` · **XS** · `distribution_planner/derive.py` — **DONE 2026-09-06**
  - Acceptance: an `enabler`-role brief's `pairing.forcedEnabler` equals
    `PairingAssignment.forced_enabler` exactly (the same family already proven present in that
    brief's `allowedAtomFamilies`); a `payoff`/`none`-role brief's `pairing.forcedEnabler` is `None`.
    Every existing consumer of `pairing.role`/`pairing.pairedPayoffFamily` is unaffected (additive
    key, ignored by anything that doesn't look for it).
  - Verify: `test_forced_enabler_is_serialized_onto_the_brief` — RED confirmed (`KeyError:
    'forcedEnabler'`) before the fix, GREEN after. Real `_briefs/round-1.json` regenerated for
    real. Full `test_distribution_planner.py`: **93/93** (92 baseline + 1 new, 0 regressions).

### ✅ Checkpoint 0 — A-S7 has what it needs to read

---

## Phase 1 — A-S7's own algorithm, pure and model-free

- [x] **T1.1** Population + usage reader · **S** · `coverage_assignment/derive.py` (new module) — **DONE**
  - Acceptance: reads the latest FC1 report (`usage_direction.weights.latest_usage_report_path`)
    and extracts `{familyId: usageCount}` from `allTime.counts` + `allTime.neverUsed` (count 0 for
    every never-used id, not merely absent); returns an empty-usage map (never an error) when no
    report exists yet.
  - Verify: `UsageCountsFromReportTests`, `LoadCurrentUsageTests` — 4/4 passed, including a real
    read against the live committed FC1 report (`atom.chill-punisher` confirmed present at 0).

- [x] **T1.2** Deterministic population ordering · **XS** · Deps: T1.1 — **DONE**
  - Acceptance: `sort_population_by_usage(family_ids, usage_counts)` sorts ascending by
    `(usageCount, familyId)` — two zero-usage families sort by id, not insertion order; the same
    inputs sort identically across two calls.
  - Verify: `SortPopulationByUsageTests` — 4/4 passed.

- [x] **T1.3** `assign_required_families` — the core algorithm · **M** · Deps: T1.2 — **DONE, one real correction from the spec's own literal wording**
  - Acceptance (mirrors spec §2 and §4):
    - a `payoff`-role brief's `requiredFamilies == [pairedPayoffFamily]`;
    - an `enabler`-role brief's `requiredFamilies == [forcedEnabler]` (reads T0.1's new field,
      never re-derives it) — missing it raises `MissingForcedEnablerError`, named;
    - a `none`-role brief gets the next population member in a **global** cursor over the whole
      round (never reset per subject);
    - within `len(population)` `none`-role briefs, every population member is assigned at least
      once (proven by construction, not sampled).
  - **⛔ Correction found during implementation**: the spec said "reuse
    `validate_no_multiplicative_conflict` verbatim" for the conflict-skip. Tracing the real function
    (`distribution_planner/derive.py:340-350`) showed it checks whether a pool's OWN
    `allowed - forbidden` set holds BOTH halves of a pair — a single round-robin CANDIDATE can never
    trigger that by itself, so calling it here would be a category error, not a reuse. The real,
    correct, simpler check: skip a candidate already present in the brief's own
    `forbiddenAtomFamilies` (the exact set `build_pool` already derives from
    `multiplicativePairs` at plan time) — same protection, no parallel implementation, no
    mis-application of an existing one either.
  - Verify: `PairingBriefRequirementTests` (4/4), `RoundRobinTests` (6/6) — 10/10 passed.

- [x] **⛔ T1.4 Real-data proof + planted violations** · **S** · Deps: T1.3 — **DONE**
  - Acceptance: against FC1's real, live, committed report, every real never-used family appears in
    `requiredFamilies` at least once within one full population-length pass of `none`-role briefs —
    zero model calls, real data, the same "prove the mechanism against committed evidence" shape
    roster-balance's own FC3-C3 checkpoint used. Also: a missing `pairing.forcedEnabler` on an
    `enabler`-role brief is refused, naming the brief id and the field; a synthetic all-conflict
    fixture (a 1-member population, forbidden) returns `requiredFamilies: []`, never raises.
  - Verify: `RealDataProofTests::test_every_real_never_used_family_is_covered_within_the_first_pass`
    — passed against the real committed report. Full new suite: **19/19 passed**. Full seedsmith
    sweep: **2320 passed, 2 skipped** — the only 2 failures found (`test_affix_authoring.py`,
    `atom.patron-aura-defense`/`-power`) are the OTHER concurrent session's `patron-absorption`
    (T6.2) work landing, confirmed unrelated (different families, that test's own docstring predicts
    exactly this "expected to go RED" outcome) — not touched, not this program's to fix.

### ✅ Checkpoint 1 — the mechanism is provably correct against real data, zero token cost

---

## Phase 2 — A-S7's CLI

- [x] **T2.1** `generate_coverage_assignment.py` · **S** · Deps: T1.4 — **DONE**
  - Acceptance: `--plan <A-S1 output> [--usage-report-dir PATH] [--plan-out PATH] [--dry-run]`;
    writes an updated `kind: "action-brief"` envelope (A-S2's own copy-through contract stays
    satisfied — every existing field byte-identical, `requiredFamilies` the only addition);
    `--plan-out` defaults to overwriting `--plan`'s own path.
  - Verify: `test_generate_coverage_assignment.py` — 5/5 passed, including a real run against the
    live committed round-1 plan proving every pre-existing field byte-identical.
  - **⛔ Real interaction found and handled, not silently left broken**: A-S1's own `regenerate()`
    fully reconstructs `_briefs/round-<n>.json` from scratch every call — it does not merge with
    on-disk content, so re-running A-S1 alone after A-S7 has run erases `requiredFamilies` (this
    surfaced for real: A-S1's own `test_regenerate_is_byte_identical_across_two_real_runs` broke
    the moment A-S7's output was left sitting on the committed file). Fixed by treating "A-S1 then
    immediately A-S7" as one ordered step, never leaving A-S7's augmented file as `_briefs/
    round-<n>.json`'s persistent resting state between the two — restored the committed file to
    A-S1's own shape; Phase 4 re-runs both in sequence right before generating.

### ✅ Checkpoint 2 — A-S7 is a real, runnable stage in the pipeline

---

## Phase 3 — the splice, identical in shape across all three propose pipelines

- [x] **T3.1** `general_propose.finalize_candidate` splice · **S** · Deps: T2.1 — **DONE**
  - Acceptance: omitting `requiredFamilies` from the brief is byte-identical to today; present +
    `outcome == "accepted"` unions it into the final `atomFamilies`; present + `unresolved` never
    touches the (non-existent) entry.
  - Verify: `RequiredFamiliesSpliceTests` in `test_general_propose.py` — RED confirmed before the
    fix, GREEN after. Full file: 270/270.

- [x] **T3.2** `family_propose.finalize_candidate` splice · **S** · Deps: T2.1 — **DONE**
  - Same acceptance, proven independently — not assumed to transfer (the same discipline FC3's own
    family/signature wiring correction used after the general-only premature-done mistake).
  - Verify: `RequiredFamiliesSpliceTests` in `test_family_propose.py`. Full file green.

- [x] **T3.3** `signature_propose.finalize_candidate` splice · **S** · Deps: T2.1 — **DONE**
  - Same acceptance, proven independently on this pipeline too, plus two extra cases this
    pipeline's own richer vote shape needed: a required family already in the voted set is not
    duplicated; a `blocked` outcome (sample 0 declares no anchor) is never spliced either.
  - Verify: `RequiredFamiliesSpliceTests` in `test_signature_propose.py` — 5/5. Full file: 109/109.
  - **⛔ Real, pre-existing test-isolation defect found while verifying — FIXED 2026-09-06**:
    running the full suite under `pytest-xdist` (parallel workers) intermittently failed
    `test_general_propose.py`'s own `DryRunEntrypointTests` determinism check — traced to
    `test_distribution_planner.py::DeterminismTests::test_regenerate_is_byte_identical_across_two_real_runs`
    writing to the REAL, shared `_briefs/round-1.json` (the default `actions_root`) while another
    worker concurrently read it for an unrelated hash check. Fixed by redirecting only the WRITE
    target to an isolated temp directory per call (`actions_root=tmp/"actions"`, the same pattern
    `DryRunAndOfflineTests` already used one class up) — every real input stays real, since none of
    them derive from `actions_root`. Verified: the exact previously-racy 4-file combination run
    3/3 clean under default (parallel) execution after the fix; full suite unaffected (2340
    passed either way).
  - Full seedsmith sweep (sequential, race-free): **2340 passed, 1 skipped** — the only other
    failures are the same 2 other-session `patron-absorption` ones already noted under T1.4.

### ✅ Checkpoint 3 — every propose pipeline honors a real coverage requirement, provably, at zero extra model-call cost

---

## Phase 4 — real proof, small batch

- [x] **T4.1** One real small batch through the full chain · **S** · Deps: Checkpoint 3 — **DONE, real positive movement**
  - Ran A-S1 (`generate_distribution_planner`) → A-S7 against the ACTUAL file `signature_propose`
    consumes (`_rounds/round-1/p3-briefs.json`, A-S2's assembled output — not `_briefs/round-1.json`
    directly, which lacks `familyActions` and is refused by `signature_propose` for exactly that
    reason) → 20 real signature-propose drafts, round 908.
  - **Real result: 13/20 accepted (65%).**
  - **Real before/after, `generate_usage_stats`:**

    | | Before (after 3 soft-cue batches, flat) | After (1 deterministic-splice batch) |
    |---|---|---|
    | Used families | 59/100 | **72/100** |
    | Never-used | 41 | **28** |
    | Evenness | 794‰ | **817‰** (still below the 850 floor, but real, measured progress) |

    **+13 families used in one 20-brief batch — versus zero movement across three prior batches of
    the soft-cue mechanism (905-907, 60 drafts total, 59/100 the whole time).** This is the real,
    honest answer the task asked for, and it is a clear positive one — not asserted as a target,
    discovered as a fact. `atom.chill-punisher`/`atom.rot-punisher` remain in the never-used list,
    correctly and expectedly: they are pairing-role families, exclusive to family/general scope by
    this program's own design (species never pairs) — this batch was species-scope only.
  - Verify: `python -m seedsmith.adapters.actions.generate_usage_stats --write --gate` — real output
    captured above; written to `docs/research/action-corpus/_usage-2026-09-06.json`.

### ✅ Checkpoint 4 — the program closes with a real, honest answer to "did this actually work" — YES, measurably

---

## Deferred, with a reason

- **Retrofitting `requiredFamilies` onto rounds 901-907.** A real content decision (touches
  already-accepted candidates), explicitly left open in `spec-coverage-assignment.md` §10 — not a
  gate on any task above, since the mechanism's forward-only default is itself the safe,
  non-destructive choice.
- **A non-round-robin rotation order** (e.g. weighted-random). No evidence yet that round-robin's own
  strict ascending order produces a worse content shape than an alternative would — nothing to react
  to until Phase 4's real batch says otherwise.
