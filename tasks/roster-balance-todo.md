# Todo: roster-balance

Plan: [`roster-balance-plan.md`](roster-balance-plan.md). **6 modules, 18 tasks, all model-free.**

Each task names its spec. **Do not start a task without reading its spec in that session** —
`docs/DESIGN-GATE.md` is binding.

Size: **S** ≤ half a day · **M** ~a day · **L** more than a day.

---

## Phase 0 — measure · *model-free, no dependencies*

- [ ] **0.1 Corpus loader + axis discovery** · **S** · `spec-distribution-stats.md`
  - Read `data/seed/demons/species/**/*.json` (skip `_index.json`), flatten the arrays, discover axes
    from the rows themselves.
  - **Acceptance:** a planted row carrying a brand-new axis key appears in the output with no code
    change; no constant anywhere names a species/family/axis count.
  - **Verify:** `python -m pytest tools/seedsmith/tests/test_distribution_stats.py -k discovery`

- [ ] **0.2 Per-axis statistics** · **S** · Deps: 0.1
  - Distinct values, counts, normalised-Shannon evenness, top-value share.
  - **Acceptance:** evenness is 1.0 for a uniform axis and 0.0 for a single-value axis; a 2-value
    50/50 axis and a 10-value uniform axis both score 1.0 (axis-comparability, the reason entropy was
    chosen over max/min).
  - **Verify:** the two endpoint tests plus the arity-comparability test.

- [ ] **0.3 Illegitimate-value detection** · **S** · Deps: 0.2
  - `posture: "unresolved"` reported as a defect class, and excluded from evenness so it cannot
    flatter the number.
  - **Acceptance:** evenness over a fixture containing `unresolved` equals evenness over the same
    fixture with those rows removed; the 12 real rows are named.
  - **Verify:** `PLANTED_VIOLATION_an_unresolved_axis_value_is_reported_as_a_defect`

- [ ] **0.4 Grid occupancy + density** · **M** · Deps: 0.2
  - Cells, occupancy, empty count, density, crowded/thin tails, for a grid supplied as input.
  - **Acceptance:** reproduces the measured table — `aptitude × element` = 78 cells / 10.78;
    `× posture(3)` = 234 / 3.59; 17 empty on the coarse grid; `(Onslaught, earth)` = 127.
  - **Verify:** `grid_density_matches_the_hand_computed_value`, recomputed independently in the test.

- [ ] **0.5 Report emission + determinism** · **S** · Deps: 0.3, 0.4
  - Human-readable and `--json`, into `docs/research/roster/_distribution-<date>.json`.
  - **Acceptance:** two runs byte-identical (hashed, not eyeballed); every collection sorted on a
    stable key.
  - **Verify:** `the_report_is_byte_identical_across_two_runs`

- [ ] **0.6 Hand the two inherited findings to `demon-seed`** · **S** · Deps: 0.5
  - Record in `demon-seed`'s own todo: the 12 `unresolved` postures (a vote outcome in committed
    data) and `elementSecondary` at 97.4% `none`.
  - **Acceptance:** both written to the owning program's task file with the measured numbers; **this
    program does not fix either.**

### ✅ Checkpoint C1 — the roster is measurable

---

## Phase 1 — decide, then index

- [ ] **1.1 `roster-balance.v1.json` + loader** · **M** · `spec-balance-policy.md` · Deps: 0.5
  - Grid, density band (per-mille), per-axis role and floors, `illegalValues`.
  - Ship the decided roles: `aptitudePrimary`/`elementPrimary`/`posture`/`rarity`/`threatBand`
    **load-bearing**; `attackTempo`/`deployMode`/`aptitudeSecondary` report-only;
    `elementSecondary` cosmetic.
  - **⛔ DECIDED 2026-09-06 — ship `minEvennessMilli: 900` on all five load-bearing axes**, priced
    before choosing rather than picked by feel. Cost to reach 0.90, against each axis's own soft
    headroom (`inferred` + vote-split rows): `aptitudePrimary` **128/342** · `rarity` **194/267** ·
    `elementPrimary` **34/235** · `threatBand` **72/170** · `posture` **0/169**. **Every target fits
    inside the soft pool**, so the owner's *"whatever it takes"* costs no `stated`/`observed` moves
    at all and `divergesFromAlmanacBasis` should never fire in practice.
  - ⚠️ Those move counts are an **arithmetic lower bound** from a greedy largest→smallest simulation.
    Real feasibility also needs a soft row to exist *in the crowded cell* with a *plausible*
    destination — which is why RB4 must report unfixable cells rather than assume the arithmetic.
  - **Note:** `posture` measures **0.927** once the 12 illegal `unresolved` rows are excluded (not
    the 0.778 an earlier pass reported *including* them) — removing an illegitimate value fixed the
    axis on its own, and `elementPrimary` at 0.863 already clears 0.85 with zero moves.
  - **Acceptance:** no numeric threshold literal in this module's sources; an axis present in the
    corpus but absent from `axisPolicy` is refused at load, naming it; a float threshold is refused.
  - **Verify:** `python scripts/audit-magic-numbers.py --targets M1` clean for these files.

- [ ] **1.2 Verdict engine** · **S** · Deps: 1.1
  - Measurement × policy → per-axis pass/fail with measured-vs-required numbers.
  - **Acceptance:** cosmetic and report-only axes never fail; derived axes are absent from the checked
    set rather than passed; density fails in **both** directions.
  - **Verify:** the shipped defaults reproduce the expected verdicts — `aptitudePrimary`, `posture`,
    `rarity`, `threatBand`, `deployMode`, `aptitudeSecondary` below their floors; `elementPrimary`
    and `attackTempo` above; `elementSecondary` cosmetic.

### ✅ Checkpoint C2 — balance is defined in data

- [ ] **1.3 Index derivation** · **M** · `spec-coverage-index.md` · Deps: 1.2
  - Every cell incl. empty; targets derived (`rosterSize / cellCount`, clamped to band); four states
    (`empty`/`under`/`at`/`over`); `cellId` built in policy order.
  - **Acceptance:** 234 cells for the real corpus; doubling a fixture roster doubles targets with no
    code change; a hand-written target is refused; no cell created for an illegal axis value.
  - **Verify:** `the_real_corpus_produces_234_cells_at_the_shipped_policy`

- [ ] **1.4 Index emission + `corpusHash`** · **S** · Deps: 1.3
  - **Acceptance:** byte-identical across two runs; `corpusHash` stamped so a consumer can prove
    provenance; `--under` returns only the under-filled set.

### ✅ Checkpoint C3 — the index exists and is stable

---

## Phase 2 — direct the existing pipeline · *no corpus writes, lowest risk*

- [ ] **2.1 Optional index input on A-S1, inert by default** · **S** · `spec-pipeline-direction.md` · Deps: 1.4
  - **Acceptance:** with no index supplied, brief output is **byte-identical to today** — proven by
    hash, not argued.
  - **Verify:** `an_index_absent_run_is_byte_identical_to_today`

- [ ] **2.2 Confirm the C1 invariant holds (cheap regression test, not a risk)** · **S** · Deps: 2.1
  - ⛔ **Downgraded 2026-09-06 — an earlier draft called this "the one genuine technical risk" and
    hung a checkpoint on it. That was overcautious.** `spec-distribution-planner.md` constraint 4
    requires the eligible **set** to be identical across tiers. RB6 **weights** an unchanged full
    98-family pool rather than narrowing it (`spec-pipeline-direction.md`: *"direction is a bias, not
    a cage"*), so `allowedAtomFamilies` never changes at all and the constraint **cannot** be
    violated by construction. The test stays because it is nearly free and pins the property.
  - **Acceptance:** a named test shows two briefs of *different tiers* with *equal cell need* receive
    identical pools; `test_distribution_planner.py` passes **unchanged**, planted-violation included.

- [ ] **2.3 Cell-need → family weighting** · **M** · Deps: 2.2
  - Under-served families weighted **up**, never made exclusive; per-mille integers.
  - **Acceptance:** a pool is never reduced to a single option; every emitted family id is one of the
    98; directed briefs byte-identical across two runs.

- [ ] **2.4 Feedback: rejects and unresolved become unmet need** · **M** · Deps: 2.3
  - **Acceptance:** a dedup reject and an unresolved brief both reappear as unmet cell need rather
    than being dropped — the thing that makes a second cycle worth running.

- [ ] **2.5 Replay-prove the diversity gain, zero tokens** · **M** · Deps: 2.4
  - Replay the **already-recorded** round-903/904 `samplePicks` under weighting.
  - **Acceptance:** families-used rises above the measured **52/98** baseline. **No model calls** —
    the recorded samples are the input.

### ✅ Checkpoint C5 — diversity measurably improves

---

## Phase 3 — plan corrections · *proposal only, writes nothing*

- [ ] **3.1 `classify` moves for the ~138 unclassified almanac rows** · **M** · `spec-rebalance-plan.md` · Deps: 1.4
  - Diff the 904-row almanac against the corpus's `gameTypeId` set; emit `classify` requests.
  - **Acceptance:** the count matches the real gap; pure gain — no existing row referenced.

- [ ] **3.2 Reassign ranking on the REAL confidence vocabulary** · **M** · Deps: 3.1
  - Rank `unresolved` → `deterministic-fallback` → `split` (prefer the recorded
    `minorityValues` destination) → `inferred` → `high`+`stated`/`observed` **last**.
  - ⚠️ **`confidence == "low"` does not exist in this corpus** — an earlier spec draft ranked on it.
  - **Acceptance:** a planted evidence-free reassign is refused; ranking order proven over a mixed
    fixture; a `split` move lands on the recorded minority value.

- [ ] **3.3 `divergesFromAlmanacBasis` stamping** · **S** · Deps: 3.2
  - Any move against a `stated`/`observed` row is stamped and **counted in the verdict**.
  - **Acceptance:** the count is reported per plan. **Diverging is allowed; diverging invisibly is
    not** — the surviving guard on the owner's "whatever it takes".

- [ ] **3.4 Cost, limits and determinism** · **M** · Deps: 3.3
  - Projected post-plan evenness; cells it **cannot** fix, named; seeded-hash tie-break.
  - **Acceptance:** shuffling input rows produces a byte-identical plan; a plan that still misses the
    thresholds **says so** rather than emitting moves that merely look like progress.

### ✅ Checkpoint C6 — a plan is reviewable

---

## Phase 4 — apply · *the only writer*

- [ ] **4.1 Dry-run diff + `corpusHash` guard** · **M** · `spec-plan-apply.md` · Deps: 3.4
  - **Acceptance:** `--dry-run` is the default and writes nothing (filesystem assertion); a stale
    `corpusHash` is refused, naming the mismatch — the likeliest real failure in a repo with
    concurrent work.

- [ ] **4.2 Apply with additive provenance** · **M** · Deps: 4.1
  - One axis per move; previous value kept; `_rebalance` record appended; confidence never silently
    raised.
  - **Acceptance:** a two-axis move is refused as two moves; an illegal value can never be created.

- [ ] **4.3 Reverse plan + idempotence + resume** · **M** · Deps: 4.2
  - **Acceptance:** apply → reverse restores an **identical corpus hash**; applying twice is a no-op;
    an interrupted apply resumes without double-applying.
  - **Why it matters:** this session may never run a git write command, so reversibility lives in the
    artefact, not in version control.

- [ ] **4.4 Single-writer guard** · **S** · Deps: 4.3
  - **Acceptance:** a repo-wide check proves RB5 is the only writer of
    `data/seed/demons/species/**`, mirroring `guard-single-writer.ps1`'s convention.

### ✅ Checkpoint C7 — apply is reversible

---

## Deferred, with a reason

- **Re-running the full action corpus under RB6 direction.** Task 2.5 proves the gain by replay at
  zero token cost; an actual re-run is a separate, owner-timed decision with real wall-clock cost.
- **Promoting a `report-only` axis to load-bearing.** `attackTempo`/`deployMode`/`aptitudeSecondary`
  are measured every run; promoting one is a reviewed policy edit, not a code change.
