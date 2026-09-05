# Capability map: `roster-balance`

**Status:** proposed 2026-09-05. **Not approved. No module spec may be written until it is** —
`seedsmith-design`'s own rule: *"capability map — approved before any module spec."*

**Program prefix:** `roster-balance`. Module specs → `docs/architecture/roster-balance/spec-<module-id>.md`;
plan → `tasks/roster-balance-plan.md` + `tasks/roster-balance-todo.md`.

---

## 1. What this program is, in one paragraph

**It measures the species roster's own characteristic distribution, decides what "balanced" means as
data, and deterministically plans corrections — before any generation pipeline consumes the roster.**
Today every content pipeline reads the species corpus and faithfully reproduces whatever skew is
already in it, because **nothing anywhere measures that skew**. This program is the missing first
stage: statistics → policy → coverage index → rebalance plan. It is **entirely model-free** and
spends zero tokens.

## 2. ⛔ The measurement that motivates it — real, 2026-09-05, 841 species rows

Not an impression. Computed directly over `data/seed/demons/species/**`:

| Axis | Distinct | Evenness | Top value's share | Extremes |
|---|---|---|---|---|
| `aptitudePrimary` | 13 | **0.727** | 39.5% | Onslaught **332** / Ferocity **2** |
| `aptitudeSecondary` | 11 | **0.476** | 73.6% | none 619 / Might 4 |
| `posture` | 4 | **0.778** | 46.8% | Force 394 / `unresolved` **12** |
| `elementPrimary` | 6 | 0.863 | 45.1% | earth 379 / air 56 |
| `elementSecondary` | 4 | **0.105** | **97.4%** | none 819 / ice 3 |
| `attackTempo` | 5 | 0.938 | 34.5% | steady 290 / quick 60 |
| `deployMode` | 2 | **0.591** | 85.7% | PlantAvatar 721 / HypnoAlly 120 |
| `rarity` | 10 | **0.654** | 55.8% | fused 469 / sunwoven 4 |
| `threatBand` | 10 | **0.463** | 73.9% | nuisance 136 / scourge **1** |

**Grid density** (the ratio `docs/research/game-design/` says decides roster health):

- `aptitude × element` → 78 cells, **10.78 species per cell**. Measured reference bands: **~3.6 is
  the safe zone** (Genshin/FGO), **~12.6 is the failure zone** (FEH). This sits near the failure end.
- `aptitude × element × posture` → 312 cells, 2.70 per cell.
- **17 of 78 `aptitude × element` cells are completely EMPTY**, while `(Onslaught, earth)` alone holds
  **127 species — 15% of the entire roster in one cell.**

**Two defects fall out of this immediately**, neither previously reported:

1. **`elementSecondary` is a dead axis** — 97.4% `none`, evenness 0.105. It exists in the schema and
   carries almost no information.
2. **`posture: "unresolved"` leaked into 12 committed rows.** `unresolved` is a vote outcome, not a
   real posture value; it should never have reached the corpus.

**This is the root cause of the action-corpus diversity failure measured the same day** (52 of 98
atom families ever used, top-5 families = 34.8% of all picks, 46.8% of accepted bundles distinct).
The generation pipeline was not malfunctioning — it was faithfully sampling a lopsided roster.

## 3. What already exists — read this before claiming a gap

| Thing | State | Evidence |
|---|---|---|
| Parallel worker fan-out, **default 4, tunable** | **built** — do not rebuild | `workflow/runner.py:31` `MAX_WORKERS = 4`; `run_many(..., max_workers=...)` over `ThreadPoolExecutor` |
| Species corpus with the nine characteristic axes | **built**, 841 rows | `data/seed/demons/species/**`, `_index.json` |
| Action-brief planner (a *different* planner — plans briefs, not roster balance) | **built** | `adapters/actions/distribution_planner` (`A-S1`) |
| Species generation / classification pipelines | **built** | `demon-seed` `classify-pipelines`, `DemonSpeciesGen` |
| Seeded permutation + vote SDK | **built** | `adapters/demons/anchor/{permute,vote}` |
| **Any measurement of roster distribution or coverage** | ⛔ **does not exist** | this program |

**Naming, stated once so downstream reads it correctly.** `A-S1 distribution_planner` already exists
and plans **action briefs**. This program plans **roster balance**. They are different things; this
program never takes that name.

## 4. Vocabulary — fixed here so the modules agree

- **Axis** — one characteristic (`aptitudePrimary`, `elementPrimary`, …).
- **Cell** — one combination of the axes chosen as the coverage grid (e.g. `aptitude × element`).
- **Index** — the enumerated set of cells with their target and actual occupancy. **Derived from the
  corpus, never hard-coded to a species or family count** — the roster may grow or shrink freely.
- **Stage** — one phase of this program (measure → policy → index → plan → apply).
- **Job** — one unit of downstream work directed by the index. Jobs are what the existing
  `run_many` worker pool parallelises; this program produces their direction, not the pool.

## 5. Modules

| id | name | what it owns | model calls | deps |
|---|---|---|---|---|
| **RB1** | `distribution-stats` | The statistics function. Reads the species corpus and computes, per axis: distinct values, counts, evenness, top-share; plus grid occupancy, density, empty cells. Pure, read-only, emits a report. **Derives everything from the data — no hard-coded roster size.** | none | — |
| **RB2** | `balance-policy` | What "balanced" *means*, as tunable data: per-axis minimum evenness and maximum top-share, the target density band, which axes are load-bearing vs cosmetic, and which values are legal at all. Lives in `data/tuning/`. **No magic numbers in code.** | none | RB1 |
| **RB3** | `coverage-index` | Applies RB2 to RB1 and emits **the index**: every cell with target vs actual occupancy, the under-filled set, the over-crowded set, and the thin cells named explicitly. This is the artefact that gives the existing pipeline its missing distribution direction. | none | RB1, RB2 |
| **RB4** | `rebalance-plan` | The deterministic engine. Turns the index into an ordered, reproducible correction plan: which cells need new species, which existing rows carry a re-assignable characteristic, and the cost of each move. **Proposes; never mutates.** | none | RB3 |
| **RB5** | `plan-apply` | Applies an approved plan — emits new species requests to the *existing* demon-seed generator, and/or rewrites characteristics with full provenance and a reversible diff. Never invents species identity itself. | none | RB4 |
| **RB6** | `pipeline-direction` | The reconcile the current pipeline is missing: makes the action/brief pipelines **read the index** so generation is directed by coverage instead of sampling blind. | none | RB3 |

**Every module is model-free.** The whole program spends zero tokens — matching this repo's own
"order the build so the model-free modules come first" rule, and it produces reviewable value before
any expensive stage runs.

## 6. Dependency direction and build order

```text
RB1 distribution-stats        (measure — stands alone, immediately useful)
      |
RB2 balance-policy            (decide what balanced means — tunable data)
      |
RB3 coverage-index            (the INDEX — the artefact everything else consumes)
      |         \
RB4 rebalance-plan             RB6 pipeline-direction   (can proceed in parallel with RB4)
      |
RB5 plan-apply
```

**Build order: RB1 → RB2 → RB3 → (RB4 ∥ RB6) → RB5.**

RB1 alone already pays for itself: it turns "the roster feels lopsided" into the table in §2, and it
is the regression detector that stops the corpus silently skewing again.

## 7. Two findings this map hands to other programs, not itself

- **`posture: "unresolved"` in 12 committed rows** — a vote outcome that leaked into real data.
  Belongs to `demon-seed`'s classification pipeline, not here. RB1 will *detect* it; fixing the
  producer is that program's.
- **`elementSecondary` is 97.4% `none`** — either the axis is under-authored or it should not be an
  axis. That is a design decision for the demon-seed program; RB1 reports it, RB2 can mark the axis
  cosmetic, but this program does not decide it.

## 8. What this program must never do

- **Never invent species identity.** RB5 emits a *request* for a species with given characteristics;
  authoring its name and flavour stays with the existing demon-seed generator.
- **Never hard-code roster, family or species counts.** Every target derives from the corpus and
  `data/tuning/`, so the roster can grow without touching code (the owner's own binding requirement:
  *"we don't care how many real species and family"*).
- **Never rebuild the worker pool.** `run_many`/`MAX_WORKERS` already ship at the requested default.
- **Never mutate the corpus outside RB5**, and never without a reversible, provenance-stamped diff.
- **Never let a rebalance move break byte-identical replay** — every ordering resolves on a stable
  key, never on completion order.
