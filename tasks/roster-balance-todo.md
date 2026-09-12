# Todo: roster-balance

Plan: [`roster-balance-plan.md`](roster-balance-plan.md). **2 modules, 8 tasks, model-free.**
⛔ **Revised 2026-09-06** — read `roster-balance-map.md` §0 before starting anything; the original
6-module/18-task version targeted the wrong corpus and was never built.

Size: **S** ≤ half a day · **M** ~a day.

---

## Phase 0 — measure

- [x] **0.1 Round-file loader** · **S** · `spec-usage-stats.md`
  - Read every `data/seed/actions/_candidates/{general,family,signature}/round-*.json`, excluding
    `*-model-experiment.json` by name.
  - **Acceptance:** an unreadable/corrupt round file is a named error, never a silent skip.
  - **Verify:** `an_unreadable_round_file_is_a_named_error_not_a_silent_skip`

- [x] **0.2 Usage tally, deduped per bundle** · **S** · Deps: 0.1
  - Count each accepted row's own families once, never once per occurrence.
  - **Acceptance:** a 2-family bundle contributes exactly 1 to each family's count, not weighted by
    bundle size; only `outcome == "accepted"` rows contribute.
  - **Verify:** `a_bundle_counts_each_family_once_never_by_repeat`,
    `unresolved_and_blocked_rows_never_contribute`

- [x] **0.3 Evenness + top-N share over the full 98** · **S** · Deps: 0.2
  - Normalised Shannon evenness computed over all 98 families, not only the ones that appear — a
    zero-pick family is a real data point.
  - **Acceptance:** reproduces the measured baseline: 216 accepted, 59/98 used, evenness computed
    over all 98, top-10 share ≈45.1%.
  - **Verify:** `the_real_corpus_reproduces_the_measured_baseline`

- [x] **0.4 Never-used families named individually** · **S** · Deps: 0.3
  - **Acceptance:** all 39 real never-used families listed by id, not just counted.
  - **Verify:** `never_used_families_are_named_individually`

- [x] **0.5 All-time vs latest-round, reported separately** · **S** · Deps: 0.3
  - **Acceptance:** demonstrably different numbers on a fixture where an old round's usage would
    otherwise mask a current gap.
  - **Verify:** `all_time_and_latest_round_are_reported_separately`

- [x] **0.6 Tuning file + report emission** · **S** · Deps: 0.4, 0.5
  - `data/tuning/action-family-usage.v1.json` (`minEvennessMilli: 850`, `maxTop10SharePermille: 300`,
    `minFamiliesUsedShare: 400`); report to `docs/research/action-corpus/_usage-<date>.json`.
  - **Acceptance:** every threshold is per-mille integer, refused if float; two runs byte-identical.
  - **Verify:** `PLANTED_VIOLATION_a_float_threshold_in_the_tuning_file_is_refused`,
    `the_report_is_byte_identical_across_two_runs`

### ✅ Checkpoint C1 — usage is measurable

---

## Phase 1 — direct

- [x] **1.1 `usage_weights` optional parameter, inert by default** · **M** · `spec-usage-direction.md` · Deps: 0.6
  - Threaded through **all three** propose pipelines (`general_propose`, `family_propose`,
    `signature_propose`) identically to the existing `family_glossary` precedent (`build_context` →
    `sample_draft` → `propose_*_action`). Initially wired into `general_propose` only and marked
    done prematurely — corrected same session once the gap was caught; all three now covered by
    dedicated tests, not inferred from one pipeline's proof.
  - **Acceptance:** omitting the parameter produces byte-identical output to today on **each** of
    the three pipelines — proven by hash, mirroring the `family_glossary` fix's own proof shape.
  - **Verify:** `TestOmittingUsageWeightsIsInert` (general) +
    `TestFamilyProposeWiring`/`TestSignatureProposeWiring::test_context_is_byte_identical_...`
    (family, signature) — all in `test_usage_direction.py`

- [x] **⛔ 1.2 Prove `allowedAtomFamilies` is never touched** · **S** · Deps: 1.1
  - The load-bearing safety property: weighting changes rendering/emphasis only, never the pool
    itself — so `spec-distribution-planner.md` constraint 4 (the C1 tier gate) cannot be violated by
    construction, not merely by discipline.
  - **Acceptance:** a direct equality check on the emitted family set with weighting on vs off, on
    **all three** propose pipelines; `test_distribution_planner.py` passes unchanged, planted-
    violation included.
  - **Verify:** `TestAllowedAtomFamiliesIsNeverTouched` (general) +
    `test_allowed_atom_families_is_never_touched_by_weighting` (family, signature) +
    `test_distribution_planner.py` (must stay green, 92/92 confirmed)

### ✅ Checkpoint C2 — the constraint that matters is provably unbroken

- [x] **1.3 Weight function: zero-cost floor/ceiling, flat-uniform target** · **M** · Deps: 1.2
  - `target_share_milli = 1000 // 98`; linear deficit-based weight, clamped, per-mille integers.
  - **Acceptance:** one of the 39 real never-used families gets the ceiling weight; a family at or
    above target gets the floor, never negative.
  - **Verify:** `a_zero_usage_family_gets_the_ceiling_weight`,
    `a_family_at_or_above_target_share_gets_the_floor_never_negative`

- [x] **1.4 Legible rendering — no numeric leak to the model** · **S** · Deps: 1.3
  - Mirrors `family_glossary`'s own precedent: a human-readable cue, never a raw weight value in the
    prompt text.
  - **Acceptance:** the model-facing rendered text never contains a numeric weight for any family.
  - **Verify:** `weights_render_as_a_legible_cue_never_a_bare_number`

- [x] **⛔ 1.5 Prove the weighting mechanism is correct against real data — corrected 2026-09-06,
  not an outcome proof** · **S** · Deps: 1.4
  - An earlier draft of this task claimed a "replay" of round-903/904 could prove families-used
    rises, copying the vote-aggregation fix's own zero-cost technique without checking it applies.
    **It does not**: that fix replayed already-collected model answers under a new aggregation
    rule; this fix changes the *prompt*, and no replay can predict a model's answer to a prompt it
    was never shown. Corrected to what zero cost can actually prove.
  - Compute real weights from FC1's own real usage report (`docs/research/action-corpus/_usage-*.json`)
    over the real 98-family population.
  - **Acceptance:** every one of the 39 real never-used families gets the ceiling weight; the real
    top-10 concentrated families (`atom.elpw-overflow`, `atom.retribution`, ...) get the floor
    weight. **No model calls** — FC1's committed report is the entire input.
  - **Verify:** `every_real_never_used_family_gets_the_ceiling_weight_against_FC1s_real_report`,
    `the_real_top_10_concentrated_families_get_the_floor_weight`

### ✅ Checkpoint C3 — the weighting mechanism is correct against real data, at zero token cost

---

## Deferred, with a reason

- **A real, full generation re-run under FC3 direction.** This is the ONLY way to actually measure
  whether real model behavior diversifies once shown the weighted prompt — C3 proves only that the
  weighting mechanism itself is correct against real usage data, not that it changes what a model
  does with it. A separate, owner-timed decision with real wall-clock and call cost.
- **A non-flat target-share curve.** Only worth building once evidence (replay or a real run) shows
  the flat-uniform default over- or under-corrects.
- **The creature-species roster's own real findings** (grid occupancy 65/252, non-monotone rarity,
  `posture: "unresolved"` invisible to existing metrics). Handed to `creature-seed`'s own todo per
  `roster-balance-map.md` §0 — genuinely a different program's fix to make.
