# Audit + handoff: action-corpus distribution and the coverage loop

**Date:** 2026-09-13 · **Program:** `action-distribution-gaps` · **Status:** engineering complete;
two owner-gated real runs outstanding
**Plan/Todo:** [tasks/action-distribution-gaps-plan.md](../../tasks/action-distribution-gaps-plan.md) ·
[tasks/action-distribution-gaps-todo.md](../../tasks/action-distribution-gaps-todo.md)
**Map:** [action-corpus-map.md](../action-corpus-map.md)

This document is the durable record of a multi-session investigation into the Seedsmith action-corpus
generator. It states what was claimed at the start, what was actually measured, every defect found,
what was fixed (with evidence), what remains open, and the exact context a fresh agent needs to
continue. **Read §1 and §6 first if you are taking over.**

---

## 1. Executive summary

The owner opened with three claims about the action generator's distribution. Investigation found the
engine had **four distinct allocation defects** (only one of which the owner had identified), **two
payoff/pairing defects**, **one granularity defect**, **one stale-data defect**, and — most
importantly — **a closed design loop that was never wired**, which made the full-run gate
*unreachable by construction*.

| # | Defect | Status |
|---|---|---|
| A | Trait vocabulary mismatch: pipeline scored open flavor text against a closed 14-trait pool map | **FIXED** |
| B | Per-subject allocation inert at `count == 5` (category) | **FIXED** |
| C | Same defect on `targetMode`: `area` unreachable at 1,131/1,131 subjects | **FIXED** |
| D | Same defect one level down: `areaShape` all-`row` (750/750 species) | **FIXED** |
| E | Frame sequencing: blocked marginals collapsed batch diversity (up to 134 identical briefs) | **FIXED** |
| G1 | Payoff keys leaked into the non-pairing round-robin (74 anchors impossible to satisfy) | **FIXED** |
| G2 | A model-picked payoff key gained no enabler | **FIXED** |
| G3 | The species gate was scope-aggregate only; 828/904 species uncovered and unreported | **FIXED** |
| G4 | Dead contradictory constant `SIGNATURE_ACTIONS_PER_SPECIES = 3` vs tuning `5` | **FIXED** |
| G5 | `species-innate.json` stale (84 vs 904 species) and S6 unrun | **FIXED** |
| H | `compute_verdict` ignored `gates` → **full-run gate deadlock** | **FIXED** |
| I | The design's `S5 → S1` top-up round was never wired | **FIXED** |
| J | Accepted corpus double-counted 11 ids (191 rows for a 179-row corpus) | **FIXED** |

**The engine is now correct and proven on real model batches.** The corpus itself is **2.7 % full**
(180 of 6,655 planned briefs) and the three remaining gate gaps are all *coverage*, not defects. Two
tasks (T3.2, T3.3) are **deferred by owner policy** because they are dozens of model hours.

**Bottom line for a successor:** do not re-investigate the engine — it is done and gated. The
remaining work is either (a) run the corpus to convergence, or (b) the optional cleanups in §6.

---

## 2. The three original claims, verified

The owner's framing on 2026-09-11 was:

> *"we have general action for each action kind … family actions … and also demon specifics signature
> action. seem like the pipeline never read the demon seed from data folder but it try to read defected
> demon species in sqlite (84 stale demon species) so it read wrong location. also the general action
> only have 25 but it should have 100 to 200 action each action kind … maybe need methodology and
> deterministic engine, statistics engine if they never build."*

| Claim | Verdict | Evidence |
|---|---|---|
| Pipeline reads stale 84-species SQLite | **HALF TRUE.** Current code reads the live seed folder (`data/seed/creatures/species/**/*.json`), never SQLite. But the *derived artifacts and docs* were stale: `DemonSpeciesCatalog.Generated.cs` = 84 rows while the live roster = 904; the SQLite `demon_species` table itself held **904**. The "84" came from the old C# projection. | `catalog.py` `load_catalog()`; `LEGACY_CATALOG_PATH` used only by fixtures |
| General tier only 25; should be 100–200 per kind | **TRUE.** `action-corpus-run.v1.json` shipped `generalCount: 25` (5/category) against the sealed ideal's 500 (100/category). Raised to **1000** (200/category, the owner's upper bound). | tuning file v2→v3 |
| Need a deterministic / statistics engine | **ENGINES EXISTED; THE INPUT AND THE METHOD WERE BROKEN.** A-S1 (planner), A-T1 (type weights), A-S5 (10 metrics), A-S7 (coverage assignment) were all built. The *allocation method* was inert and the *trait signal* was near-empty. | measured below |

### The measurement that started it

With the live roster (904 species), the pipeline scored each species' **raw open-vocabulary flavor
traits** (measured: **2,312 distinct tokens**) against a map keyed for the **closed 14-trait gameplay
pool** (`DemonTraitCatalog`). Exactly **one** species (`guardian`, once) matched. Every other species
contributed **zero** trait signal, and the derivation collapsed: **673 of 904** species at
`separation == 0`, only **11 distinct `leanOrder`s** across the roster.

The runtime already solved this: `DemonTraitPoolCuration.PickFor(speciesId, rarity, gameTypeId)`
bridges live flavor text to the closed pool, and `ConcreteSpeciesSeedReader.cs:103` calls it. The
Python pipeline did not. **Fix (A):** `characteristic_pool/curation.py` mirrors that bridge, reading
the frozen `DemonSpeciesLegacyTraitPoolOverlap.Generated.cs` (its own header: *"Never regenerated
from that source again… this file is now itself the frozen source of truth"*) plus the FNV-1a
fallback. Result: distinct `leanOrder`s **11 → 63+**; flat category vectors **673 → 0**.

---

## 3. The allocation defect family (B, C, D, E) — the core discovery

### The arithmetic

`distribution_planner` allocated each axis **per subject** by largest remainder over that subject's
own per-mille vector. At `count == 5`, a member needs weight **≥ 400 per-mille** to win a second slot
(`floor(400·5/1000) == 2`). The shipped `base=1000, step=250` normalises to a **maximum of 267**
per-mille. So no member could ever win a second slot.

| Axis | Members | Measured result before the fix |
|---|---|---|
| `category` | 5 | **All 1,131 subjects** produced the identical `1/1/1/1/1` vector. 1 distinct vector. |
| `targetMode` | **6** | The lowest-weight member (`area`) got **0 slots for all 1,131 subjects** → `targetMode: area` and all four `areaShape` values were **unreachable at family/species scope (0 of 6,655 briefs)**. |
| `areaShape` | 4 (conditional) | At a subject's own area count (0 or 1), all four shapes tied and the declared-order tie-break always picked `row` → **750/750 species, 188/188 family** were `row`. |

The quantization was even **perverse**: `count=5` differentiated *worse* than `count=2`
(1 distinct vector vs 10), because 5 slots over 5 members with a sub-400 top weight is exactly flat.

### The fix: scope-level, deficit-greedy (`apportion_axis`)

Replaced the per-subject split with a **two-level** allocator:

1. **Scope quota** — largest-remainder apportionment of `count × subjects` over the **summed**
   per-mille vector. Exact; this is the aggregate A-S5's `quotaDrift`/`cellOccupancy` gate on.
2. **Per-subject fill** — deficit-greedy: each slot takes the axis member with the most remaining
   scope quota, tie-broken by the subject's own weight, then declared order. Exact by construction
   (`col == quota` asserted), no convergence loop, deterministic.

`apportion_area_shapes` applies the same method to the conditional `areaShape` sub-vector, weighted
by each subject's area-slot count.

**Measured after the fix** (the shipped 6,655-brief plan):

| Axis | Result |
|---|---|
| category | general 200×5; family 244/231/230/217/213; species **976/926/860/850/908** |
| targetMode | all 6 reachable: `area` = 750 species + 188 family briefs |
| areaShape | all 4 reachable: species **188/188/187/187**; family **47/47/47/47** |
| quotaDrift | clean |
| lean agreement | 94 % of species get their own top-`count` categories |

### E — frame sequencing (found by a real batch, not by review)

`expand_counts` emitted each quota **blocked** (all 200 `attack`, then all 200 `defense`, …). Zipping
two blocked vectors made the joint frame constant for long stretches. Measured on the 1,000-brief
general tier: only **15 distinct frames**, runs up to **134 identical briefs**, first 40 briefs = **1
frame**. Since a proposal batch draws a contiguous slice of ordinals, every batch saw near-identical
context — the model produced near-duplicates and A-S3 rejected **32 of 57 (56 %)** at tier 2.

**Fix:** stride/round-robin expansion (key `k`'s j-th unit in round `j`). Marginals unchanged, order
differs. Result: **49 distinct frames**, longest run **2**, first-40 = **35**. Real-batch dedup reject
rate **63 % → 20 %**.

### A hole found while verifying E

The plan's `corpusHash` folded only **inputs** (role-lean hash, type-weights hash, tuning/rungs
versions) — not the planner algorithm. The sequencing fix emitted *different briefs under the same
hash*, so a stale plan and its candidates would have passed the freshness check. **Fix:** added
`ALGORITHM_VERSION` to the hash payload (now `2`).

---

## 4. Pairing defects (G1, G2)

`data/seed/actions/pairings.json` maps 2 payoff families to their enabler families
(`atom.rot-punisher` → `atom.venomous`/`withering`/`bloodletting`/`sporing`; `atom.chill-punisher` →
`atom.freezing`). A payoff key is only coherent with an enabler in the same anchor; A-S5's
`enablerPayoffCoverage` checks this.

**G1 — the round-robin required a payoff without a possible enabler.** A-S7's rotation walked *all*
families. `assign_pairing_roles` only ever emits pairing briefs on **family/general** scope, so a
**species** `role: "none"` brief that required `atom.rot-punisher` could never have a matching
enabler. Measured: **74 anchors** were guaranteed future gaps. **Fix:** `payoff_families` excluded
from the round-robin; the keys stay covered via their 456 `role: "payoff"` briefs. Verified: **0**
`role:none` briefs require a payoff key; 123 distinct required families (breadth unchanged).

**G2 — a model-picked payoff key gained no enabler.** The model may select any family in
`allowedAtomFamilies`, including a payoff key the plan never required. **Fix:** `splice_payoff_enablers`
in `coverage_assignment/derive.py`, wired into all three propose stages via `pairing_table`: for each
payoff key present, add the first `pairings.json` enabler that is also in that brief's allowed pool.
Deterministic, zero model calls.

**Still open (not a defect):** 2 committed rows predate G2 and still show gaps —
`caltropnut` and `snowgatling` in `committed-round-909.json`, each carrying `atom.rot-punisher` with
no enabler. Their anchor's top-up **would** close it (proven: a real top-up row carried
`atom.sporing`, a valid enabler), but that closure was transient (temp scratch, never promoted).

---

## 5. Granularity and staleness (G3, G4, G5)

**G3 — the species gate measured the wrong thing.** `cellOccupancy`/`thinCell` gate on
`cell.<scope>.<category>.<band>`, which at species scope is **one aggregate over 904 species**
(`cell.species.attack.1-10` quota = 976 = 904 × 5 × 21.6 %). 976 attack rows spread over ~100 species
would pass while 804 held nothing. Measured: **828 of 904 species had zero accepted actions, and no
metric reported it.** **Fix:** new CLOSED metric `action.corpus.speciesCoverage`, one GAP Finding per
uncovered species, required universe taken from the same `subject_category_counts` the quota was
recomputed for (no second roster read). It asserts the contract (*the uncovered set is empty*), never
a population literal.

**G4 — a dead, contradictory default.** `coverage_report/derive.py` carried
`SIGNATURE_ACTIONS_PER_SPECIES = 3` (the sealed ideal's B1 number) as the default for
`roster_reconciliation_findings`; the shipped tuning sets `perSpeciesCount: 5`. Dead but
trusted-on-read. **Fix:** constant deleted, parameter now **required**.

**G5 — the per-species innate guarantee was stale and unrun.** `species-innate.json` held **84
entries with `tuningVersion: 1`** (legacy catalog, 820 species out of date). **Fix:** ran S6 against
the current plan → **904 entries, 81 picks, 823 nulls**, promoting its 53 round-2000 survivors into
`committed-round-2000.json` (126 → 179 committed). *823 nulls is correct, not a defect* — only 179
accepted rows exist, so 823 species genuinely have no eligible action yet.

---

## 6. The two structural defects (H, I) — and the deadlock

This was the most consequential finding, and it came from asking *"is the gate on species already the
wrong gate?"* → *"should the planner prevent this from the initial run?"*

**H — the verdict ignored `gates`.** `spec-metrics.md` §4 fixes the calibration order: *"New metric →
`gates=False`, runs, reports. **Then** a threshold goes into `budget` and `gates` flips."*
`spec-coverage-report.md` §3 step 6 says *"`pass` requires every **gating** CLOSED metric green."* The
implementation treated **any** GAP as `not-clean`. All 11 action metrics are unpromoted, so **every
verdict was non-clean**. Meanwhile the general reporter (`report/cli.py`) *does* honour `gates` — so
A-S5 and the reporter disagreed about what "passing" means.

**I — the `S5 → S1` loop was never wired.** The design is explicit and repeated:

| Source | States |
|---|---|
| `action-corpus-ideal.md` §15 | `S5 -->|"round n+1 targets"| S1` |
| §11.4 | *"A statistics pass finds thin distribution and plans the next round"* |
| §13 | *"Top-up rounds are explicit and numbered. Round n+1's briefs are derived deterministically from round n's coverage report"* |
| `spec-coverage-report.md` §7 | *"**Depended on by: A-S1, which reads the report to build round n+1's briefs.** The cycle is broken by round 1 reading no report at all."* |

The implementation had no such edge. `next_round_targets()` emitted shortfall rows and **nothing
consumed them**. Only `round-1.json` existed.

**The deadlock.** `mode: "full"` requires a passing gate → the gate requires `thinCell` green →
`thinCell` requires a filled corpus → a filled corpus requires `mode: "full"`. **One round can never
fill its own quota**, so `thinCell` could never clear. The plan had to be generated through a
smoke-mode copy during the repair.

### Fixes

**H:** `compute_verdict(..., gating_metric_ids=)` blocks on `NOT_MEASURED` (any CLOSED metric) and on
a GAP from a **gating** metric; an unpromoted GAP stays in `gapMetrics` and does not block. `Verdict`
carries `gatingMetrics`, read from the live registry — the identical `{m.id for m in registry.all() if
m.gates}` set `report/cli.py` builds. `is_passing_quality_gate` now defers to the verdict string
instead of re-deriving `gapMetrics == []`.

**I:** `read_top_up_targets`/`load_top_up_targets` read round n's `next-target` rows into
`{(scope, scopeKeyOrNone): {category: want}}`; `plan_round(top_up=…)` plans **exactly** the named
shortfall (round n's accepted rows already count against the quota, so a base replan would duplicate
them); `convergence_decision` bounds the loop and reports `converged` vs `round-cap`. Round 1 passes
no report and is byte-identical.

**Verified end-to-end on real data:** 5,387 `next-target` rows / 6,490 total `want` → **exactly 6,490
top-up briefs**. And `mode: "full"` went from **REFUSED → REACHABLE**.

---

## 7. J — the gate caught a real defect in my own work

After refreshing the committed reports, the `build-gate` subagent (a fresh-context independent
verifier) returned **FAIL**: the round-1 report measured **191 rows for a 179-row corpus**.

Root cause: `_rounds/round-1/survivors.json` still held 12 full rows, **11 of whose ids were already
promoted into `committed-round-2000.json`** — G5's S6 run promotes the round *it* runs, so it reduced
*round-2000's* file, not round-1's. A-S5 merged committed rows + non-`promoted` survivors with **no
id-level guard**, double-counting 11 ids (191 rows / 180 distinct) with *different payloads*, and
disagreeing with round-2000's report (179) about the same baseline. Every cell was inflated,
`thinCell` and `speciesCoverage` alike.

**Fix:** `_build_ctx` enforces one row per id, first occurrence winning. Committed is read first and
`load_committed` guarantees committed ids are unique, so the promoted authoritative copy wins and a
stale round copy is dropped. Round-1 now reconciles exactly: **179 committed + 1 genuinely-new
survivor = 180**. The producer-side violation predates this task and is deliberately left in place;
the fix is in the consumer, which is where a violation does damage.

---

## 8. Current state (verified 2026-09-13)

**Plan** — `data/seed/actions/_briefs/round-1.json`

- **6,655 briefs** = 1,000 general + 1,135 family + 4,520 species
- `corpusHash` `945e899bbe90aa5f…`, `tuningVersion` **3**, `ALGORITHM_VERSION` **2**
- `mode: "full"`, `generalCount` 1000, `perFamilyCount` 5, `perSpeciesCount` 5
- All four axes exact (see §3)

**Roster** — 904 species, 227 consolidated families, 1,183 memberships, 0 family-less.

**Accepted corpus** — **179** committed rows

| File | Rows |
|---|---|
| `committed-round-1.json` | 19 |
| `committed-round-2.json` | 5 |
| `committed-round-909.json` | 102 |
| `committed-round-2000.json` | 53 |

**Coverage** — `coverage-round-1.json`

- verdict **`pass`**, `gatingMetrics: []`, `notMeasuredMetrics: []`
- `gapMetrics`: `enablerPayoffCoverage`, `speciesCoverage`, `thinCell` (all **unpromoted**, so none blocks)
- acceptedCorpusSize **180** (179 + `action.family.academic.004`)
- **fill: 180 / 6,655 = 2.7 %**; all **15/15** cell groups thin
- shortfall: **5,387** next-target rows, **6,490** units, **904** species uncovered

**Gate** — `refuse_full_run_if_ungated('full', True, gate)` **does not raise.** `mode: "full"` is
reachable.

**Endpoint** — LM Studio at `http://localhost:1234/v1`, model `google/gemma-4-26b-a4b-qat`
(also present: `google/gemma-4-31b-qat`, `text-embedding-bge-m3`).

**Tests** — 276 focused (`test_coverage_report`, `test_distribution_planner`,
`test_coverage_assignment`, `test_innate_picker`); **3,818** in `tools/seedsmith/tests` with 3,144
subtests. Guards (`guard-test-substrate`) green.

**Tuning files** — `action-corpus-run.v1.json` v3; `action-type-weights.v1.json` v2
(`separationMilli: [200,400,600,800,1000]`, `nullSeparationMilli: 500`).

**Commits** (branch `features/derived-stat-extension`)

| Commit | Subject |
|---|---|
| `bdd91b68` | feat(action-corpus): make gates decide the verdict, not the mere presence of a gap |
| `d49d2640` | feat(action-corpus): wire the S5 -> S1 top-up round |
| `860f43aa` | fix(action-corpus): count one row per id in the accepted corpus |
| `9a05b0ba` | docs(action-corpus): defer the two owner-gated real runs |

Earlier engine work (allocator, curation, G1–G5, docs, `ladders.py`) is in `253d86c7` and later
commits. The tree is **clean** for all program paths.

---

## 9. What remains open

### Open 1 — the corpus is 2.7 % full (the real remaining problem)

Not a defect. `thinCell`, `speciesCoverage`, and the 2 legacy `enablerPayoffCoverage` gaps all close
by drawing accepted content. That is the deferred multi-hour run.

### Open 2 — stale candidate scratch blocks a real-tree run

`_candidates/{general,family,signature}/round-1.json` carry the **pre-fix hash `d73e1db06b24c4ed`**.
`run_pipeline` refuses them by design (the freshness check working). A real run needs `--fresh` or the
scratch cleared. These are gitignored scratch — safe to delete, but it is the owner's data.

### Open 3 — the two legacy payoff gaps

`caltropnut`, `snowgatling` in `committed-round-909.json`. Their round-909 plan no longer exists, so
they cannot be regenerated. Options: (a) let a converged round close them organically, (b) a
deterministic one-off repair using `splice_payoff_enablers` (proven to add `atom.venomous` to both
rows). Note the repo hard rule: generated rows are never hand-edited — a repair must be a generator
path, and these rows carry `_provenance` from `description_backfill`.

### Open 4 — the producer-side `survivors.json` marker violation

`_rounds/round-1/survivors.json` still holds 11 full rows whose ids are committed. The consumer
guard (J) neutralises the damage, but the producer invariant ("one id exists in exactly one place",
`innate_picker`) is still violated on disk. A future consumer that *doesn't* dedup would break again.

### Open 5 — `report/cli.py` has no round-scoped top-up entry

`seedsmith report` runs the full registry but there is no CLI surface for "plan round n+1 from round
n's report"; the plumbing exists (`regenerate(top_up_report_path=…)`) but only tests and ad-hoc
scripts call it. A `--top-up-from <report>` flag would make T3.2 a one-liner.

### Not open (do not re-investigate)

- The allocator, sequencing, curation bridge, G1–G5, H, I, J — all fixed, all tested, all gated.
- "Should the engine have prevented the thin corpus in one round?" — **no**; ~66 % yield is a real
  property of the model stage, and convergence over rounds is the designed mechanism.

---

## 10. Action plan

Ordered by value. Sizes: **XS** 1 file · **S** 1–2 · **M** 3–5 · **L** multi-run.

### Phase A — make the full run safe and ergonomic (do first; ~half a session)

- **A1 (S) — add a round-scoped top-up CLI.** `seedsmith actions plan --round N
  --top-up-from _reports/coverage-round-<N-1>.json`. Without it, T3.2 is an ad-hoc script.
  Acceptance: the command plans `_briefs/round-<N>.json` from the report's `next-target` rows and
  prints `topUpSubjects` + the brief count.
- **A2 (S) — clear or archive the stale candidate scratch.** Move
  `_candidates/*/round-1.json` aside (gitignored, so no commit) so the default `run_pipeline` path
  works without `--fresh`. Acceptance: `generate_action_pipeline --dry-run` (no `--fresh`) passes the
  freshness check.
- **A3 (XS) — document the top-up runbook** in `docs/runbook/local-dev.md` or the program plan: the
  exact commands for one real round and for convergence.
- **A4 (S) — reconcile the producer marker violation (Open 4).** Either have
  `generate_coverage_report` assert-and-warn on a duplicate, or reduce stale round files to markers
  in a maintenance step. Acceptance: no round file holds a full row whose id is committed.

### Phase B — run the corpus to convergence (owner-gated; hours)

- **B1 (L) — one real round against the current plan.** Draw the full 6,655-brief plan (or a bounded
  subset), propose → validate → dedup → assemble → innate → coverage. Expected: ~4,400 accepted rows
  at ~66 % yield, `thinCell` shortfall shrinking by that amount, `quotaDrift` clean.
  **Do not run unattended without agreeing the duration (~23–28 h).**
- **B2 (L) — top-up rounds until `convergence_decision` returns `converged` or the cap.** Each round
  plans only the prior report's shortfall. Declare the cap (the plan's Q2) before starting.
- **B3 (M) — record the honest final verdict.** Expect `pass` with `gates` still `[]`, or promote a
  metric deliberately (`spec-metrics.md` §4: measure, look, set, gate) and then gate it.

### Phase C — optional hardening (only if a future consumer needs it)

- **C1 (S)** — add a `gates=True` promotion path with a `budget` threshold for `thinCell`/
  `speciesCoverage`, so the corpus-completeness question becomes a real gate after calibration.
- **C2 (S)** — `pairings.json` currently exposes only 2 payoff keys out of 125 authored families;
  `pairingReach` reports the low reach honestly. If pairing diversity matters, author more pairs.
- **C3 (XS)** — `relation` is a pure function of `category` (`CATEGORY_RELATION`), so it adds no
  fingerprint diversity. Confirm that is intended or decouple them.

### Do NOT do

- Do not re-derive the plan to "fix" the thin corpus — it is correct; only acceptance can fill it.
- Do not hand-edit generated seed rows (`data/seed/actions/**` carries `_provenance`). Fix the
  generator and regenerate, or add a sanctioned generator repair path.
- Do not weaken the freshness check or the `gates` semantics to make a run pass.
