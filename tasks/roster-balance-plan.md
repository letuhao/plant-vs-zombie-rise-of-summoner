# Plan: roster-balance

**Map:** [`docs/architecture/roster-balance-map.md`](../docs/architecture/roster-balance-map.md) (approved 2026-09-05)
**Specs:** `docs/architecture/roster-balance/spec-<module-id>.md` — six modules, RB1-RB6
**Task list:** [`roster-balance-todo.md`](roster-balance-todo.md)

**Path note.** The bare `tasks/plan.md` / `tasks/todo.md` pair belongs to the perf stream and
`AGENTS.md` is explicit that they are *"not defaults and not fallbacks"* — hence this prefixed pair.

---

## 1. What this builds, in one paragraph

**The measurement and correction stage that runs before any content pipeline reads the species
roster.** Nothing today measures the roster's own characteristic distribution, so a lopsided corpus
propagated into every downstream generator unseen — measured directly as 52-of-98 atom-family
coverage and 46.8% distinct bundles in the action corpus. This program measures the distribution,
declares what balanced means as tunable data, emits a coverage index, plans deterministic
corrections, and gives the existing pipeline the distribution direction it never had.
**Six modules, all model-free, zero tokens.**

## 2. The three facts that shape every task below

Measured 2026-09-05/06, not assumed. Each one killed a plausible approach:

1. **The roster is CLOSED.** `data/seed/demons/_dump/almanac/` is 904 rows dumped from the real game;
   every species carries a `gameTypeId` into it. **You cannot invent a species.** New content is
   capped at the **~138 almanac rows not yet classified** — everything else is re-classification.
2. **The imbalance is in CLASSIFICATION, not in the game.** `aptitudePrimary`/`posture`/
   `elementPrimary` are RPG-layer assignments carrying `confidence` and `basis`. `(Onslaught, earth)`
   holding 127 species means the classifier funnelled creatures there, not that PvZ has 127 earth
   brawlers. Re-classifying is therefore designing our own layer, not falsifying game data.
3. **The grid falls out of the data.** Healthy density is ~3.6/cell; `841 / 3.6 = 234`; and
   `aptitude(13) × element(6) × posture(3 real values) = 234` → **density 3.59.** Dropping posture
   gives 78 cells at 10.78 (near the measured failure zone); keeping `unresolved` as a posture gives
   312 at 2.70 (too thin). **Cleaning the data is what produces the correct grid.**

## 3. Two owner decisions this plan is built on

- **Ambition — *"hit the evenness targets whatever it takes"* (2026-09-06).** The floors in RB2 are
  real gates, and RB4 may reach them using `stated`/`observed` rows rather than stopping at the soft
  pool. **The surviving guard:** every such move is stamped `divergesFromAlmanacBasis` and counted in
  the verdict. Diverging is allowed; diverging invisibly is not.
- **Scope — the coverage grid plus `rarity` and `threatBand`** are load-bearing. `attackTempo`,
  `deployMode`, `aptitudeSecondary` are report-only; `elementSecondary` is cosmetic.

## 4. Phases

Ordered by dependency, and **vertically sliced** — every task ends at a real, runnable artefact
against the real corpus, never a half-wired layer.

### Phase 0 — measure (RB1)

The whole program's value starts here and RB1 is useful the day it lands, before anything consumes
it: it converts *"the roster feels lopsided"* into a table, and becomes the regression detector that
stops it drifting again unseen.

**Ends with:** a real report over 841 rows reproducing the map's §2 numbers, and the two findings
handed to `demon-seed` (12 `unresolved` postures; `elementSecondary` at 97.4% `none`).

### Phase 1 — decide, then index (RB2 → RB3)

RB2 turns measurements into verdicts using only tunable data; RB3 turns verdicts into the index.

⚠️ **RB3 is the load-bearing artefact of the whole program.** Everything downstream joins on its
`cellId`, and the parallel worker pool makes its determinism non-negotiable — a non-deterministic
index would silently cost the byte-identical-replay gate this repo currently passes.

### Phase 2 — direct the existing pipeline (RB6)

**Deliberately before RB4/RB5.** RB6 changes no corpus data at all — it only shapes brief pools — so
it delivers measurable diversity improvement at zero risk to committed content, and it can be
validated by replaying the already-recorded round-903/904 samples rather than spending model calls.

⛔ **An earlier draft of this plan called RB6 "the one real technical risk". That was overcautious
and is corrected here.** `spec-distribution-planner.md` constraint 4 requires the eligible **set** to
be identical across tiers; RB6 **weights an unchanged full 98-family pool** rather than narrowing it,
so `allowedAtomFamilies` never changes and the constraint cannot be violated by construction.
Task 2.2 keeps a cheap regression test to pin the property, but it is not a gate and nothing waits
on it.

### Phase 3 — plan corrections (RB4)

Proposal only. Safe to run repeatedly; writes nothing.

### Phase 4 — apply (RB5)

The only module that writes to `data/seed/demons/species/**`. `--dry-run` is the default; `--commit`
is explicit; every apply emits its own reverse plan.

## 5. Checkpoints

| | Passes when |
|---|---|
| **C1 — the roster is measurable** | RB1 reproduces §2's table over the real corpus, byte-identically across two runs |
| **C2 — balance is defined in data** | RB2's every threshold is in `data/tuning/`; `audit-magic-numbers.py` clean for its files; the shipped defaults reproduce the expected per-axis verdicts |
| **C3 — the index exists and is stable** | RB3 emits 234 cells with 17+ empty named, byte-identical across runs, `corpusHash` stamped |
| **C4 — diversity measurably improves** | Replaying the recorded round under RB6 weighting raises families-used above the measured 52/98 — **with no model calls** |
| **C5 — a plan is reviewable** | RB4 produces a plan against the real index; every `reassign` cites its rank reason; shuffling input order changes nothing |
| **C6 — apply is reversible** | RB5 apply → reverse restores an identical corpus hash, proven by test |

**These are review checkpoints, and none of them is a gate.** Nothing here blocks *starting* a phase
on an external decision: the two owner decisions this program needed are already made (§3), the
evenness floors are priced and affordable (§3a), and every remaining default is reversible or
tunable. **There is no pre-work gate in this plan.**

## 3a. The floors are priced, and they are cheap

`minEvennessMilli: 900` on all five load-bearing axes. Moves required to reach it, against each
axis's own soft headroom (`inferred` + vote-split rows):

| axis | evenness now | moves to 0.90 | soft headroom | fits? |
|---|---|---|---|---|
| `aptitudePrimary` | 0.731 | 128 | 342 | yes |
| `rarity` | 0.654 | 194 | 267 | yes |
| `elementPrimary` | 0.863 | 34 | 235 | yes |
| `threatBand` | 0.463 | 72 | 170 | yes |
| `posture` | 0.927 | 0 | 169 | already passes |

**Every target fits inside the soft pool**, so *"whatever it takes"* costs no `stated`/`observed`
moves at all — the `divergesFromAlmanacBasis` guard is cheap insurance that should never fire. Even
0.95 fits (worst case `rarity`, 262 of 267).

⚠️ These are an **arithmetic lower bound** from a greedy largest→smallest simulation. Real
feasibility also needs a soft row to exist *in the crowded cell* with a plausible destination, which
is exactly why RB4 must report cells it cannot fix rather than trusting the arithmetic.

## 6. What this plan will not do

- **Invent a species.** RB5 emits a request; the existing `demon-seed` generator authors identity.
- **Rebuild the worker pool.** `workflow/runner.py` already ships `MAX_WORKERS = 4` over a
  `ThreadPoolExecutor`, which is the default the owner asked for.
- **Fix the two inherited defects it finds.** `posture: "unresolved"` and the degenerate
  `elementSecondary` axis belong to `demon-seed`'s classifier; this program detects and reports them.
- **Spend a single token.** All six modules are model-free.

## 7. Rules that apply to every task here

- **Derive, never hard-code.** No species, family, axis or cell count as a constant — the owner's
  binding *"we don't care how many real species and family."*
- **Per-mille integers, never floats**, for every threshold and ratio — a gate that reads `0.8` two
  different ways is a reproducibility bug.
- **Determinism is a feature, not tidiness.** Sort on stable keys; break ties on a seeded hash of
  `(cellId, speciesId)`, never on input or completion order.
- **Provenance is additive.** A rebalanced row keeps its previous value and gains a record.
- **Git is hands-off.** The owner commits; no task here runs a git write command — which is exactly
  why RB5's reversibility must live in the artefact rather than in version control.
