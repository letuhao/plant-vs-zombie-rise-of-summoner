# Capability map: `roster-balance`

**Status:** ⛔ **REVISED 2026-09-06 — the original target was wrong, found before any module was
built.** Kept the program name (already approved, already referenced in the plan/todo); the scope
underneath it changed. Read §0 before anything else in this file.

**Program prefix:** `roster-balance`. Module specs → `docs/architecture/roster-balance/spec-<module-id>.md`;
plan → `tasks/roster-balance-plan.md` + `tasks/roster-balance-todo.md`.

---

## 0. ⛔ The correction, in full — read this before the rest of the file

The original version of this map (2026-09-05) targeted the **demon species roster** (841 rows,
axes `aptitudePrimary`/`elementPrimary`/`posture`/`rarity`/`threatBand`), on the theory that its
imbalance was the root cause of the action-corpus diversity bug measured the same day (52/98 atom
families used, `atom.elpw-overflow` duplicated 23×).

**That causal chain was traced and does not hold.** `signature_propose/prompts.py` — the one
action-corpus stage keyed to an individual species — reads the species anchor's `element`/`family`/
`rarity`/`motifs` **only for theming**. `pool.allowedAtomFamilies` is **the full, undifferentiated
98-family pool, identical to general and family scope**; `aptitudePrimary`/`posture` never reach a
brief at all. **The demon-species roster and the atom-family diversity bug are two systems that
share the word "family" and nothing else.**

Rebuilding six modules against the species roster would have shipped a program that could not move
the number it was named for. Found by tracing the actual code path, not by re-deriving from prose —
the same discipline this session's own history keeps re-learning the hard way.

**Where the real evidence pointed once traced:**

1. **The 98-family corpus itself is healthy.** Measured directly against
   `data/seed/items/affix-families/*.json`: `tags` evenness **0.941**, `roles` evenness **0.919**.
   There is nothing to reassign or correct in the family data — a rebalance-and-apply layer (the
   original RB4/RB5) has no defect to act on.
2. **The skew is entirely in generation, not in content.** Aggregate real usage across every
   committed round (216 accepted candidates, general + family + signature, 2026-09-04 through
   2026-09-05): **59 of 98 families ever used, 39 never used at all, top-10 families = 45.1% of
   every pick.** This is a sampling/direction defect, the same shape the vote-aggregation bug was —
   found by measuring, not assumed.
3. **No usage-tracking metric exists.** `coverage_report`'s `atomFamilyNamespace` metric validates
   that an id is one of the 98 — it is a namespace check, not a diversity measurement. Confirmed by
   reading its source (`adapters/actions/coverage_report/derive.py:358-380`): it never tallies counts
   or reports concentration.

**What survives from the original design, and what doesn't:**

| Original module | Verdict |
|---|---|
| RB1 `distribution-stats` | **Redirected.** Same technique (Shannon evenness, top-share), new corpus: real usage counts over the 98 affix families, not demon species axes. |
| RB2 `balance-policy` | **Folded into RB1's own tuning** — one small threshold set, not a separate module; there is no multi-role-axis policy question here, just "is usage even enough." |
| RB3 `coverage-index` | **Dropped.** There is no grid/cell combinatorics in this problem — it is a flat 98-value histogram, not a multi-axis cross product. A grid was the wrong shape for this data from the start. |
| RB4 `rebalance-plan` | **Dropped entirely.** Nothing in the family corpus needs correcting — evenness 0.941/0.919 is healthy. A "plan" module with nothing to plan is not a module. |
| RB5 `plan-apply` | **Dropped entirely**, for the same reason — no corpus write is ever needed. |
| RB6 `pipeline-direction` | **Kept, retargeted.** The actual fix: weight the shared 98-family pool by real usage history instead of demon-species cell need. The design principle ("bias, not a cage"; prove the C1 tier invariant; replay-provable at zero token cost) carries over unchanged — only the input signal changes. |

**Net effect: three modules, not six.** Documented in §2 below.

**The demon-species finding stands, separately, and is handed off, not built here.** Running
`demon_roster.py`'s existing, already-tuned metrics against the real corpus for the first time
(`T2.11`, `tasks/seed-to-concrete-todo.md`, never previously run) produced 19 real GAP findings:
grid occupancy 65/252 cells (257‰, below the 900‰ target), non-monotone rarity, single-element
share 973‰, posture imbalance, aptitude skew. **This is real, valuable evidence for `demon-seed`'s
own program** — recorded there, not fixed here, because it does not touch the bug this program
exists to fix. A second, narrower finding from the same run: `posture: "unresolved"` (12 rows) is
invisible to every existing metric — `UnresolvedCount`'s `VOTED_FIELDS` omits `posture`, and
`PostureBalanceMetric` silently drops rows that match none of its three keys. Also handed to
`demon-seed`.

---

## 1. What this program is, in one paragraph

**It measures how evenly the action-corpus generation pipelines actually draw from the 98 authored
atom/affix families, and directs future generation toward the families real usage has neglected.**
Model-free measurement, model-free direction — the pipelines still call a model to name and bundle
actions; this program only shapes *which families it's shown*, weighted by history.

## 2. Modules

| id | name | what it owns | model calls | deps |
|---|---|---|---|---|
| **FC1** | `usage-stats` | Reads every committed `_candidates/*/round-*.json`, tallies real per-family pick counts (deduped within one accepted row — a family used twice in one bundle counts once), computes evenness/top-share/never-used, emits a report. | none | — |
| **FC2** | `usage-tuning` | One `data/tuning/action-family-usage.v1.json`: the evenness floor and top-share ceiling, per-mille, tunable. Small enough that it is a file, not a module with its own spec — folded into FC1's own spec §"policy". | none | FC1 |
| **FC3** | `usage-direction` | Threads real usage counts into `general_propose`/`family_propose`/`signature_propose` as an optional, additive weighting signal — under-used families rendered/weighted up, the pool never narrowed to fewer than the full 98. | none | FC1 |

**Build order: FC1 → FC3.** FC2 is data shipped alongside FC1, not a separate build step.

## 3. What already exists — read this before claiming a gap

| Thing | State | Evidence |
|---|---|---|
| Parallel worker fan-out, default 4, tunable | **built** | `workflow/runner.py:31` `MAX_WORKERS = 4` |
| The 98-family corpus, with `tags`/`roles`/`powerBand` | **built**, and healthy | `data/seed/items/affix-families/*.json` — measured evenness 0.941/0.919 |
| Per-round candidate files with real accepted rows | **built** | `data/seed/actions/_candidates/{general,family,signature}/round-*.json` |
| Family-namespace validation | **built** | `coverage_report/derive.py:atom_family_namespace_findings` — validates membership, never tallies usage |
| **Any measurement of real per-family USAGE across rounds** | ⛔ **does not exist** | this program, FC1 |
| **Any generation-time weighting by usage history** | ⛔ **does not exist** | this program, FC3 |
| Demon-species roster metrics (a *different* corpus, unrelated to this bug) | **built, already tuned**, first real run done 2026-09-06 | `metrics/demon_roster.py`, `data/tuning/demon-roster-targets.v1.json` — handed to `demon-seed`, not owned here |

## 4. What this program must never do

- **Never narrow `allowedAtomFamilies`.** The pool stays the full 98; direction is a weight applied
  on top, never a filter — the identical guard the original RB6 design already stated, and the
  reason `spec-distribution-planner.md` constraint 4 (per-tier pool identity, the C1 gate) is never
  at risk.
- **Never touch the demon-species roster.** That finding belongs to `demon-seed`; this program reads
  and reports on action-corpus round files only.
- **Never propose editing the affix-family corpus.** It measures healthy (0.941/0.919); there is
  nothing here to reassign.
- **Never break byte-identical replay.** Usage counts and any resulting weights are derived
  deterministically from committed round files, sorted on a stable key.
