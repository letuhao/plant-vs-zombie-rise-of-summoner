# Todo: `action-corpus` — A-S7 `coverage-assignment`

Plan: [action-corpus-plan.md](action-corpus-plan.md). **1 module, 4 phases, 10 tasks, model-free
except Phase 4's own real proof.**

Sizes: **XS** 1 file · **S** 1-2 · **M** 3-5.

---

## Phase 0 — A-S1's own small, additive dependency

- [ ] **T0.1** `pairing.forcedEnabler` emitted by `plan_subject` · **XS** · `distribution_planner/derive.py`
  - Acceptance: an `enabler`-role brief's `pairing.forcedEnabler` equals
    `PairingAssignment.forced_enabler` exactly (the same family already proven present in that
    brief's `allowedAtomFamilies`); a `payoff`/`none`-role brief's `pairing.forcedEnabler` is `None`.
    Every existing consumer of `pairing.role`/`pairing.pairedPayoffFamily` is unaffected (additive
    key, ignored by anything that doesn't look for it).
  - Verify: a new case in `PairingRoleTests` — `test_forced_enabler_is_serialized_onto_the_brief`;
    full `test_distribution_planner.py` stays green (92+/92+, no regressions).

### ✅ Checkpoint 0 — A-S7 has what it needs to read

---

## Phase 1 — A-S7's own algorithm, pure and model-free

- [ ] **T1.1** Population + usage reader · **S** · `coverage_assignment/derive.py` (new module)
  - Acceptance: reads the latest FC1 report (`usage_direction.weights.latest_usage_report_path`)
    and extracts `{familyId: usageCount}` from `allTime.counts` + `allTime.neverUsed` (count 0 for
    every never-used id, not merely absent); returns an empty-usage map (never an error) when no
    report exists yet.
  - Verify: `a_family_with_no_recorded_usage_defaults_to_zero_not_absent`,
    `no_usage_report_yet_returns_an_empty_map_not_an_error`

- [ ] **T1.2** Deterministic population ordering · **XS** · Deps: T1.1
  - Acceptance: `sort_population_by_usage(family_ids, usage_counts)` sorts ascending by
    `(usageCount, familyId)` — two zero-usage families sort by id, not insertion order; the same
    inputs sort identically across two calls.
  - Verify: `two_zero_usage_families_break_the_tie_by_id`, `sort_is_stable_across_two_calls`

- [ ] **T1.3** `assign_required_families` — the core algorithm · **M** · Deps: T1.2
  - Acceptance (mirrors spec §2 and §4 exactly):
    - a `payoff`-role brief's `requiredFamilies == [pairedPayoffFamily]`;
    - an `enabler`-role brief's `requiredFamilies == [forcedEnabler]` (reads T0.1's new field,
      never re-derives it);
    - a `none`-role brief gets the next population member in a **global** cursor over the whole
      round (never reset per subject — the same property that fixed the pairing monoculture);
    - assigning a candidate that would recreate a `multiplicativePairs` conflict against the
      brief's own `allowedAtomFamilies`/already-required siblings advances the cursor instead of
      assigning it (reuses `distribution_planner.derive.validate_no_multiplicative_conflict`
      verbatim — no parallel check);
    - within `len(population)` `none`-role briefs, every population member is assigned at least
      once (proven by construction over a real-sized population, not sampled).
  - Verify: `payoff_brief_requires_its_own_payoff_family`,
    `enabler_brief_requires_its_own_forced_enabler`,
    `none_role_briefs_advance_a_global_cursor_never_per_subject`,
    `a_conflicting_candidate_is_skipped_never_assigned`,
    `every_population_member_is_covered_within_one_population_length`

- [ ] **⛔ T1.4 Real-data proof + planted violations** · **S** · Deps: T1.3
  - Acceptance: against FC1's real, live, committed report, every one of the **41 real never-used
    families** (named individually in `docs/research/action-corpus/_usage-2026-09-06.json`) appears
    in `requiredFamilies` at least once within the first 41 `none`-role briefs, in the report's own
    ascending-usage order — zero model calls, real data, the same "prove the mechanism against
    committed evidence" shape roster-balance's own FC3-C3 checkpoint already used. Also: a missing
    `pairing.forcedEnabler` on an `enabler`-role brief (a stale, pre-T0.1 plan) is refused, naming
    the field, never silently treated as `role: none`; a synthetic all-conflict fixture returns
    `requiredFamilies: []` rather than raising.
  - Verify: `every_real_never_used_family_is_covered_within_the_first_41_none_role_briefs`,
    `a_missing_forced_enabler_on_an_enabler_brief_is_refused_by_name`,
    `an_all_conflict_fixture_degrades_to_empty_never_raises`

### ✅ Checkpoint 1 — the mechanism is provably correct against real data, zero token cost

---

## Phase 2 — A-S7's CLI

- [ ] **T2.1** `generate_coverage_assignment.py` · **S** · Deps: T1.4
  - Acceptance: `--plan <A-S1 output> [--usage-report PATH] [--plan-out PATH] [--dry-run]`; writes
    an updated `kind: "action-brief"` envelope (A-S2's own copy-through contract stays satisfied —
    every existing field byte-identical, `requiredFamilies` the only addition); `--plan-out`
    defaults to overwriting `--plan`'s own path, matching A-S1/A-S2's file-handoff convention.
  - Verify: `dry_run_writes_nothing`, `real_run_preserves_every_existing_brief_field_byte_identical`,
    `omitted_usage_report_flag_falls_back_to_the_latest_real_one`

### ✅ Checkpoint 2 — A-S7 is a real, runnable stage in the pipeline

---

## Phase 3 — the splice, identical in shape across all three propose pipelines

- [ ] **T3.1** `general_propose.finalize_candidate` splice · **S** · Deps: T2.1
  - Acceptance: omitting `requiredFamilies` from the brief is byte-identical to today (same
    additive-discipline proof `family_glossary`/`usage_weights` already established); present +
    `outcome == "accepted"` unions it into the final `atomFamilies`; present + `unresolved`/`blocked`
    never touches the (non-existent) entry.
  - Verify: `omitting_required_families_is_byte_identical`,
    `an_accepted_candidate_gets_the_required_family_spliced_in`,
    `an_unresolved_candidate_is_never_spliced`

- [ ] **T3.2** `family_propose.finalize_candidate` splice · **S** · Deps: T2.1
  - Same three acceptance lines and verify names as T3.1, proven independently — not assumed to
    transfer (the same discipline FC3's own family/signature wiring correction used after the
    general-only premature-done mistake).

- [ ] **T3.3** `signature_propose.finalize_candidate` splice · **S** · Deps: T2.1
  - Same three acceptance lines and verify names as T3.1, proven independently on this pipeline too.

### ✅ Checkpoint 3 — every propose pipeline honors a real coverage requirement, provably, at zero extra model-call cost

---

## Phase 4 — real proof, small batch

- [ ] **T4.1** One real small batch through the full chain · **S** · Deps: Checkpoint 3
  - Run A-S1 (existing) → T0.1's `forcedEnabler` → A-S7 (T2.1) → one propose pipeline (signature,
    the one with the most real volume headroom) at a small `--count`, matching the capability map's
    own constraint 2 ("small-batch proof before any full run").
  - Acceptance: re-run `generate_usage_stats --gate` and report the real, honest number — either the
    never-used count shrinks (the fix working) or it doesn't (a real finding to react to, not a
    result to round up). No target number is asserted here; this task's job is producing the real
    evidence, not passing a threshold.
  - Verify: `python -m seedsmith.adapters.actions.generate_usage_stats --gate` (manual read of the
    real output, before vs. after)

### ✅ Checkpoint 4 — the program closes with a real, honest answer to "did this actually work"

---

## Deferred, with a reason

- **Retrofitting `requiredFamilies` onto rounds 901-907.** A real content decision (touches
  already-accepted candidates), explicitly left open in `spec-coverage-assignment.md` §10 — not a
  gate on any task above, since the mechanism's forward-only default is itself the safe,
  non-destructive choice.
- **A non-round-robin rotation order** (e.g. weighted-random). No evidence yet that round-robin's own
  strict ascending order produces a worse content shape than an alternative would — nothing to react
  to until Phase 4's real batch says otherwise.
