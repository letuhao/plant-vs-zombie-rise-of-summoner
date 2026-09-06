# Plan: roster-balance

**Map:** [`docs/architecture/roster-balance-map.md`](../docs/architecture/roster-balance-map.md) —
**revised 2026-09-06, read its §0 first.**
**Specs:** `spec-usage-stats.md` (FC1), `spec-usage-direction.md` (FC3) — the two real modules.
Six earlier spec files (RB1-RB6) are marked superseded in place; kept for the record, not built.
**Task list:** [`roster-balance-todo.md`](roster-balance-todo.md)

---

## 1. ⛔ Why this plan is short, and what it replaced

The original plan (2026-09-05) targeted the demon-species roster on the theory that its imbalance
caused the measured action-corpus diversity bug (52/98 atom families used). **Tracing the real code
found that theory wrong**: `signature_propose` — the one stage keyed to a species — reads the
species anchor only for theming; `allowedAtomFamilies` is the same undifferentiated 98-family pool
on every scope. Demon-species characteristics never reach it.

Once retargeted at the actual corpus (`data/seed/items/affix-families/*.json`, 98 entries), two facts
collapsed the six-module plan into two modules:

1. **The corpus itself is healthy** (`tags` evenness 0.941, `roles` evenness 0.919, measured
   directly) — there is nothing to reassign, so the correction/apply layer (formerly RB4/RB5) has no
   defect to act on and was dropped entirely.
2. **There is no grid.** The original coverage-index (RB3) assumed multi-axis cell combinatorics;
   real usage is a flat 98-value histogram. A grid was the wrong shape for this data.

**What's left is exactly two things:** measure real usage (FC1), and feed it back as a weight (FC3).
Both model-free.

## 2. The real evidence this plan is built on

Measured 2026-09-06, over every real committed round (`_candidates/{general,family,signature}/round-*.json`,
excluding the refuted model-experiment file):

- **216 accepted candidates total.**
- **59 of 98 families ever used; 39 never used at all.**
- **Top-10 families = 45.1% of every pick.** (`atom.elpw-overflow` alone: 29 picks.)
- Corpus-intrinsic health, for contrast: `tags` evenness **0.941**, `roles` evenness **0.919** — the
  skew is entirely in generation, not in the authored content.

## 3. Phases

### Phase 0 — measure (FC1)

Useful the moment it lands: converts a hand-run count into a standing, re-runnable report, and is
the regression detector for this exact bug recurring.

### Phase 1 — direct (FC3)

**Provably zero-risk before spending a token, honestly bounded to what zero cost can prove.** An
earlier draft of this plan claimed the coverage gain itself could be proven by replaying recorded
samples, copying the vote-aggregation fix's own technique without checking it transfers — it does
not: that fix replayed *already-collected* model answers under a new aggregation rule, but this fix
changes the *prompt* the model sees, and no offline replay can predict what a model would answer to
a prompt it was never actually shown. What zero-cost verification correctly proves here is the
*mechanism's* correctness against FC1's real usage report (every real never-used family gets the
ceiling weight); whether the model's real picks actually diversify needs a real run, tracked in §6.

## 4. Checkpoints

| | Passes when |
|---|---|
| **C1 — usage is measurable** | FC1 reproduces §2's numbers over the real round files, byte-identically across two runs |
| **C2 — the constraint that matters is provably unbroken** | a named test shows `allowedAtomFamilies` is never touched by weighting — not merely undiscussed, checked by direct equality — so `spec-distribution-planner.md` constraint 4 (the C1 tier gate) cannot be violated by construction |
| **C3 — the weighting mechanism is correct against real data, at zero token cost** | every real never-used family (39) gets the ceiling weight and the real top-10 concentrated families get the floor, computed from FC1's own real report — a mechanism-correctness proof, not a claim about model behavior |

**No pre-work gate.** Both facts this plan needed (corpus health, real usage numbers) are already
measured; every remaining default is reversible or tunable via `data/tuning/action-family-usage.v1.json`.

## 5. What this plan will not do

- **Touch the demon-species roster or the affix-family corpus.** Neither needs correction; see map §0.
- **Narrow `allowedAtomFamilies`.** Direction is a bias, checked, never a filter.
- **Claim to prove the coverage gain without a real run.** Only the weighting *mechanism's*
  correctness is provable for free; whether real model behavior actually diversifies needs a real
  re-run under direction, tracked as a separate, owner-timed decision (§6).
- **Invent a target-share curve.** Flat-uniform (`1000 // 98`) is the shipped default; anything else
  needs real evidence it under/over-corrects.

## 6. Deferred, with a reason

- **A real, full re-run of the action corpus under FC3 direction.** C3 proves the gain by replay at
  zero cost; spending real wall-clock on a fresh generation run is a separate, owner-timed decision.
- **A non-flat target-share curve.** Only worth building once a replay or real run shows flat-uniform
  over- or under-corrects.
