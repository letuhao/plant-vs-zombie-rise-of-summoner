# 22 — Plan coverage audit: content specs vs `passive-tree-todo.md`

**Status:** audit, 2026-09-05. Read-only. No file outside this one was changed.

**What was audited:** [`tasks/passive-tree-plan.md`](../../../tasks/passive-tree-plan.md) and
[`tasks/passive-tree-todo.md`](../../../tasks/passive-tree-todo.md) — 27 tasks, 6 checkpoints,
phases A–F — against four module specs:

- [`spec-tree-language.md`](../../architecture/passive-tree/spec-tree-language.md)
- [`spec-tree-review.md`](../../architecture/passive-tree/spec-tree-review.md)
- [`spec-species-tree.md`](../../architecture/passive-tree/spec-species-tree.md)
- [`spec-tree-surface.md`](../../architecture/passive-tree/spec-tree-surface.md)

**Method.** For each spec: enumerate its success criteria, its numbered gates and rules, and every
Structure / Testing / Boundaries line that describes work to be *built*; then find the task that
delivers it. A requirement carried only by a checkpoint bullet or by the standing verification block
is **PARTIAL at best** — a checkpoint verifies, it does not build.

**The headline.** The plan's spine is right and its phase ordering is right. What it under-counts is
the *machinery around the runs*: 15 of `tree-language`'s 24 validation gates have no building task,
nothing builds the gate runner, nothing creates
`data/tuning/passive-tree-targets.v1.json` — a file three of the four specs read — and
`tree-review`'s sampling, corpus sheet, escalation ladder and `O(diff)` re-review are absent
entirely. `species-tree` gets two tasks for a module that owns a planner, a uniqueness gate, a
6,720-affix authoring bill and a blocker its own spec calls hard.

---

## 1. `spec-tree-language.md`

Delivering tasks in the plan: **D1** (contract and schema gates), **D2** (property vocabulary and
quota), **D4** (emit and generate the 12 primary trees). **A1** contributes plan-side.

### 1.1 The 24 validation gates (§7)

`spec-tree-review.md:190` states it plainly: *"[`tree-language`] §7 owns the validation gates — **24
of them** — and it is the only document that numbers them."* No task in the todo cites §7.

| # | Gate | Status | Task or gap |
|---:|---|---|---|
| 1 | Schema has no numeric field | **COVERED** | D1 bullet 1 (`MAGNITUDE_DENY_NAMES`, refusal at construction) |
| 2 | Every description carries a negative clause | **MISSING** | `audit_descriptions` has no task |
| 3 | Plan reachability | **PARTIAL** | A1's `--check` is green, but unsatisfiable parents / empty tier / orphan are not named |
| 4 | The target file is complete | **MISSING** | No task creates `data/tuning/passive-tree-targets.v1.json` |
| 5 | Every gate has a threshold (`missing_thresholds`) | **MISSING** | — |
| 6 | Constrained decoding is actually on (`run_preflight`) | **MISSING** | — |
| 7 | Contract (`run_g1`) | **MISSING** | — |
| 8 | Quota conformance, per call | **COVERED** | D1 bullet 2 — the permitted subset is the schema `enum` |
| 9 | Brief conformance (`run_g2`) | **MISSING** | — |
| 10 | Text style (field echo, subject echo, language mixing) | **MISSING** | — |
| 11 | Vote resolution | **MISSING** | The 1-1-1 split is never resolved by any task |
| 12 | Bounded repair (`call_with_self_heal`) | **MISSING** | — |
| 13 | Persist-time re-gate | **MISSING** | — |
| 14 | Idempotence | **PARTIAL** | D4 asserts byte-identity; `should_generate` / `ProvenanceLedger` are not named |
| 15 | `PassiveTree/QuotaDrift` | **PARTIAL** | D2's verification checks the quota; the metric's independent re-derivation is not named |
| 16 | `PassiveTree/MechanismRamp` | **MISSING** | No task checks per-tier count against `archetypes[].mechNodes[t]` |
| 17 | `PassiveTree/CellOccupancy` | **MISSING** | — |
| 18 | `PassiveTree/ExclusionRate` | **PARTIAL** | D2 delivers the forms; the ~2% rate target and its metric are not named |
| 19 | `PassiveTree/ExclusionResolvable` | **PARTIAL** | D2 says property-keyed; the `propertyVocabulary` check and the `NOT_MEASURED` state (§5.1) are not named |
| 20 | `PassiveTree/NearDuplicate` | **PARTIAL** | Only F3's run verification, species only |
| 21 | `PassiveTree/NameCollision` | **MISSING** | The measured 83-of-83 defect has no check |
| 22 | ⭐ `PassiveTree/UnresolvedCount` | **MISSING** | **The one metric promoted to `gates=True`** has no task |
| 23 | Run verdict | **PARTIAL** | D1's *"`NOT_MEASURED` provably denies a pass"* is one property of it |
| 24 | Offline guarantee | **MISSING** | The transport stub that raises has no task |

**2 COVERED · 7 PARTIAL · 15 MISSING.** So: **no, all 24 gates do not have a home.**

**And nothing builds the runner.** `verdict.py`'s `GATING_METRICS` + `missing_thresholds()`,
`tools/seedsmith/seedsmith/metrics/passive_tree.py` (the eight `PassiveTree/*` metrics), and
`python -m seedsmith check --family PassiveTree --gate` (spec §Commands, §Project structure) appear
in no task. D4's *"every validation gate green"* is a run acceptance criterion written against a
harness the plan never builds.

### 1.2 Success criteria (§Success criteria)

| Requirement | Status | Task or gap |
|---|---|---|
| `audit_schema` passes over the real constant and fails when a numeric field is added | **COVERED** | D1 |
| Marginals match target, by a metric that **re-derives** the quota | **PARTIAL** | D2 verification; re-derivation not named |
| The 166:1 skew is not reproduced, in either direction | **PARTIAL** | D2 |
| Per-tier `mechanism` count `==` `mechNodes[t]`; deepest tier 100% mechanism | **MISSING** | gate 16 |
| Exclusions at target rate, zero node-id predicates, every `nullification` prints both sides | **PARTIAL** | D2 covers forms and printing; rate and node-id predicate check missing |
| Exactly one metric is `gates=True`, and it is `UnresolvedCount` | **MISSING** | — |
| A rerun over unchanged inputs is byte-identical, proven by hash | **COVERED** | D4 bullet 3 |
| The full generic run is ~4,680 calls and the dry-run prints it first | **MISSING** | — |
| `seedsmith check --family PassiveTree --gate` exits 0 | **MISSING** | — |

### 1.3 Structure and boundaries

| Requirement | Status | Task or gap |
|---|---|---|
| The twelve `adapters/trees/nodegen/` modules | **PARTIAL** | Implied by D1/D2/D4; `dedup.py`, `exclusion.py`, `verdict.py` unowned |
| `metrics/passive_tree.py` — the eight `PassiveTree/*` metrics | **MISSING** | — |
| `data/tuning/passive-tree-targets.v1.json` (D32 targets, `legitimateSkew`, gate thresholds) | **MISSING** | **Read by three of the four specs** |
| `tools/seedsmith/tests/adapters/trees/` — offline, stub raises | **MISSING** | gate 24 |
| The 17 named tests in §Testing strategy | **PARTIAL** | D1/D2/D4 name about five of them |

**Spec total: 4 COVERED · 12 PARTIAL · 22 MISSING** (gates plus success criteria plus structure).

---

## 2. `spec-tree-review.md`

Delivering tasks: **D3** (the 20-tree pilot), **D5** (the tree card), **F4** (the full census).
**A2** contributes one hazard fix.

| Requirement | Status | Task or gap |
|---|---|---|
| §2 — the acceptance record says *"every tree was judged"*, never *"the catalog was reviewed"* | **COVERED** | F4 |
| §1.3, §8 open Q1 — a pilot measures the real per-tree rate and gates the full run | **COVERED** | D3 |
| §5.1 — one card per tree, the 2×10 lattice, one screen | **COVERED** | D5 |
| §5.2 rule 3 — render through the **shipped** `formatMagnitude`; one implementation, a Node script in the web package (§5.4) | **COVERED** | D5, `web/fusion-rpg-web/scripts/`. Verified: `web/fusion-rpg-web/src/i18n/magnitude.ts:15` has no bare-number overload |
| §5.2 rule 5 — the sibling panel, three nearest by fingerprint | **COVERED** | D5 |
| §5.2 rule 4 — the species' own `reason` sentence beside the nodes | **COVERED** | D5 (*"beside the species/tree's own sentence"*) |
| §8 hazard 2 — a retired node bricks an actor load; reject once at an import boundary | **COVERED** | A2 bullet 3 and its verification |
| §7 — `PassiveTree/HiddenFileCount`, no `_` skip, `visitedFileCount`, canary fixture | **PARTIAL** | D5 carries one acceptance bullet. The metric, the walk and the canary fixture root have no task, and D5's files are `web/.../scripts/` — the metric lives in `metrics/passive_tree.py` |
| Closed Q4 — the 39 shared trees are their **own** census lot, own sheet, own queue, in category waves | **PARTIAL** | Checkpoint D reviews a sample; F4 is the whole corpus. The shared lot as a lot is not named |
| §3.2 Tier 1 — the four census populations (exclusion nodes, escalated, unresolved votes, review queue) | **MISSING** | — |
| §3.2 Tier 2 — 60 trees via `sampling.stratified_sample`, four stratum axes | **MISSING** | *"do not write a second sampler"* has no first sampler task either |
| §3.2 Tier 3 — ~200 nodes over rare quota cells | **MISSING** | The tier that catches *"every `frostbite` node is the same sentence"* |
| §3.1, §6.3 — the acceptance numbers, in `data/tuning/`, marked as starting values | **MISSING** | — |
| §4.1 — `PassiveTree/TreeEqualValue`, **content-side half** over `tree-binder`'s prices | **MISSING** | The plan-side half is A1's budget column; the content half is unowned |
| §4.2 — `PassiveTree/DeepMechanismValue`, registers `gates = False`, reports | **MISSING** | — |
| §5.2 rule 6 — the verdict control writes `_review/<lot>.json`, which feeds the next brief | **MISSING** | Without it a reject reason never becomes an anti-motif |
| §5.5 — the corpus sheet, seven panels, **read before the cards** | **MISSING** | — |
| §5.5 — the `sheetRead` row, and `--census` **refuses** on a missing or stale `sheetRevision` | **MISSING** | A named success criterion with no task |
| §5.6 — cards regenerated not committed; sheet and queue committed | **MISSING** | — |
| §6.1 — `manualCorrection` stamped `from`/`to`/`by`/`why`, its rate a metric | **MISSING** | — |
| §6.2 — the escalation ladder, rungs 0–5 | **MISSING** | **Checkpoint F says "escalations resolved" against a ladder nothing builds** |
| §6.4 — the nine unshippable conditions, incl. `PassiveTree/ExclusionPresentation` (which gates) | **MISSING** | D40's enforcement has no enforcer |
| **§8 — incremental `O(diff)` re-review: the diff card, `trees review --diff <from> <to>`, the `catalog_revision` lot identity** | **MISSING** | **See §5 below — this is a real gap, not a nicety** |
| §8 hazard 1 — `provenance-supersede` is unbuilt and **pass two cannot run without it** | **MISSING** | Not a task, not even a tracked ask, while F3 budgets *"2–3 passes"* |

**7 COVERED · 2 PARTIAL · 15 MISSING.**

**Escalation and incremental re-review, specifically, as asked.** Both are absent.

- **Escalation.** §6.2's five rungs are the mechanism that turns a reviewer's *"no"* into an action —
  node reject (~3 calls), tree reject (120 calls), cell reject in the plan, batch reject → reprompt,
  owner escalation. §6.3's acceptance numbers decide which rung fires. Checkpoint F's bullet
  *"escalations resolved"* assumes all of it. **Rung 4 is not hypothetical — the demon corpus took it
  three times** (`spec-tree-review.md:435`). Without the ladder, a rejected tree in phase F has
  nowhere to go but a hand edit, which §6.1 forbids.
- **Incremental re-review.** §8's opening line is the module's own objective: *"make the second pass
  cost `O(diff)`."* Nothing in phases A–F builds the diff card, the `--diff` verb, or the
  `catalog_revision (from, to)` lot identity. The consequence is concrete and matches the concern
  raised: **after D42's `treeShareMilli` re-measure — which the plan schedules at Checkpoint B and
  again as a "tuning republish" — there is no cheap way to re-certify the corpus.** The spec's own
  success criterion *"a magnitude retune produces an empty human review queue, proven by test"* has
  no task. And the blocker underneath it, `provenance-supersede`, is unbuilt and unlisted.

---

## 3. `spec-species-tree.md`

Delivering tasks: **F2** (pipeline), **F3** (the corpus run). Two tasks for a wave-4 module with its
own planner, its own uniqueness gate, its own authoring bill and a hard blocker. **That is too few.**

| Requirement | Status | Task or gap |
|---|---|---|
| §3.1 step 1 — the favour quota assigns before generation, `largest_remainder_count` | **COVERED** | F2 |
| §5.3, D41 — `speciesUniqueAffixMin = 8`, deepest-mechanism-first marking | **COVERED** | F2 |
| §6 — one `codexSummary` per species | **COVERED** | F2 |
| §7.2 — the 840-tree census under `tree-review`'s protocol | **COVERED** | F4 |
| §5 U1 — no `name`/`flavor` repeats corpus-wide | **PARTIAL** | F3 verification, no metric task |
| §5 U2 — no `(affixIds, quotaCell)` fingerprint in two trees | **PARTIAL** | F3 verification, no reverse index task |
| §7.1 — resumable, `run start/pause/resume/rerun`, no duplicate provenance row | **PARTIAL** | F3 says *"resumable"*; the mid-run-kill test is not named |
| §8 — the four inherited blockers raised **at task start** | **PARTIAL** | Only the 17th atom kind, in the asks table |
| Success criterion 9 — `HiddenFileCount` green over **this module's** seed roots | **PARTIAL** | D5's bullet is about the generic run |
| §2.1 rule 1 — the roster is read from `_index.json` and every file walked **without the `_` skip**; a species on disk but unindexed **halts the run** | **MISSING** | The blind spot §2.1 exists to not inherit |
| §3.1 step 2 — 2–3 alternates per species, drawn from the **same** quota | **MISSING** | This is the shape that makes the 166× defect impossible |
| §3.1 step 1 — a forced cell returns its draw to the pool; an overdrawn quota is **refused, not rebalanced** | **MISSING** | *"The line that gets forgotten"*, and the plan forgot it |
| §3.1 step 4 — `PassiveTree/FavourDrift`, re-derived, symmetric | **MISSING** | — |
| §4 — `mechanicalFavour` is its own field, never the anchor's `elementPrimary`/`aptitudePrimary` | **MISSING** | **The decoupling of thematic vs mechanical favour has no task.** It is the cheapest structural decision in the module and it is invisible in the plan |
| §5.3 rule 4 — the `affix.species.<speciesId>.*` namespace: **6,720 authored affixes**, ids minted once and read back | **MISSING** | **The largest unbudgeted item in the program.** §5.2: the shipped authored corpus is **two** entries |
| §5.1 — `PassiveTree/SpeciesUniqueness`, three findings, reverse index, gates none until calibrated | **MISSING** | — |
| §6 — `codexSummary` passes the schema audit (≤140 chars, no number, no channel id) | **MISSING** | — |
| §7.3 — families are **excluded from the roster** until a closed taxonomy exists | **MISSING** | 698 open tokens; a boundary with no task |
| §8.1 point 2 — the `UniqueDemon` binding lands **before** the census | **MISSING** | *"A reviewer judging 840 cards against a ladder that reads zero is judging the writing, not the tree"* |
| Success criterion 7 — the plan regenerates byte-identically; `PassiveTreeGen --check` | **MISSING** | D4 asserts it for the generic corpus only |

**4 COVERED · 5 PARTIAL · 11 MISSING.**

---

## 4. `spec-tree-surface.md`

Delivering tasks: **E1–E4**.

| Requirement | Status | Task or gap |
|---|---|---|
| §2.1 — the Passives tab, not a route; extends the locked placeholder | **COVERED** | E1. Verified: `web/fusion-rpg-web/src/ui/actor/PassivesTab.tsx:12-21` is four `LockedGridSlot`s |
| §2.3 — the lattice is GG-61; **opens scrolled to the player's own depth** | **COVERED** | E2 |
| §4.1 — three currencies named distinctly, never the bare word *points* | **COVERED** | E1 |
| §5.2 — the price of a **plan**, three numbers, order-independence | **COVERED** | E4 |
| §7.2 part 2 — the tier row attributes its requirement | **COVERED** | E4 |
| §7.2 part 3 — **exactly one lender, always singular** (`max`, not a sum) | **COVERED** | E4 |
| §7.2 part 5 — the draft preview reports what a change would **close** | **COVERED** | E4 |
| §9.1 rules 1–2 — the gate-less **condition** presentation, no price, no Unlock verb | **COVERED** | E2 |
| §2.2 Level 0 — invested paths, Focus line, not-working count, unspent, empty state as content | **PARTIAL** | E1 says *"Yours renders"* and nothing more |
| §2.2 Level 1 — the **five-bucket ordering**, search, four category filters, GG-51 query state | **PARTIAL** | E1 says *"All paths render; 39 cards windowed"*. The ordering is the whole design (§7.3: *"ordering is the mitigation"*) |
| §4 — one verb per cell, three states | **PARTIAL** | E3 |
| §4 — the deepen stepper, no slider, no raw-id `NumberInput`, edits the draft | **PARTIAL** | E3's *"without becoming a form"* implies it; the three named rules are not acceptance |
| §5.1 — draft / dirty / **Revert** / preview panel / a Plan that outlives the panel | **PARTIAL** | E4 covers the preview and the price |
| §8 — printed exclusions: both sides, same winner, inert not un-unlocked | **PARTIAL** | E3 covers the render. The Level-0 count, the filter, the toast (GG-16) and *never a modal* are not named |
| §15/§17 Q1 — the **naming** decision applied to player text | **PARTIAL** | Tracked as a non-blocking ask; no task applies the authored names. See §6 below |
| §2.2 / §3 — **Level 0b: the bloodline pin** | **MISSING** | The species tree's *spend* route is not in the plan |
| §3 — the **Demon Codex** read route (and the ask-first `DemonsPage.tsx:367-388` volume fix it hangs off) | **MISSING** | **The species-tree route named in your brief has no task at all** |
| §5.3 — a shared plan carries **no price**; an imported plan is priced on arrival; the URL grammar | **MISSING** | Tests 18–19 |
| §6 — **Focus**: the line, `1/H` prose, moves while editing, no fourteenth unit class | **MISSING** | Success criterion 6 and tests 14–15. E3 cites §6 and delivers none of it |
| §7.2 part 1 — name the rule in the fiction, once, where it first matters | **MISSING** | — |
| §7.2 part 4 — the locked reason is **visible sibling text** naming **both routes**, through one reason table | **MISSING** | Test 9; `ActionCluster.tsx:18-29` already settled the hover argument |
| §9 — a deep tier gets a **distance** (bar + Θ, computed per actor), shows its traits, silhouette only for an undiscovered bloodline | **MISSING** | E2 covers only the *condition* half of §9's table |
| §9.1 rules 3–5 — the collapsed bucket sorts last, is counted in nothing, and `gateState` is **read from the report, never inferred from a zero**; the count is read, never typed | **MISSING** | Tests 30–32, 36. Also needs `TreeResolveReport.gateState`, which A6's acceptance never names |
| §10 — standalone-first: every surface renders with the injector absent | **MISSING** | Test 28 |
| §12 — `PassiveTreeEndpoints.cs` (GET state, POST whole allocation) and `PassiveTreeDtos.cs` | **MISSING** | **E1 has no wire.** A5 builds the store; nothing exposes it |
| §12 — *"Extract the shared allocation hook first"* — `useAllocationDraft` | **MISSING** | The spec says extract **then** build; a tree spend flow would be the third copy |
| §11/§14 — the seven guard suites, the e2e volume fixtures, the 36 named tests | **MISSING** | **The standing verification block names no web command at all** — only `dotnet build`, four guards and two Python audits |

**8 COVERED · 7 PARTIAL · 12 MISSING.**

---

## 5. Ranked missing tasks — in the todo's own format

Ranked by what it costs to discover them late. Paste straight in.

### P1 · D0: `passive-tree-targets.v1.json` — the file three specs read
**Spec:** `spec-tree-language.md` §4.3; `spec-tree-review.md` §6.3; `spec-species-tree.md` §3.2, §5.3.
**Description:** The declared target file, shaped like `data/tuning/demon-roster-targets.v1.json` —
integer per-mille throughout, a `_note` recording provenance, **no axis listing its own members**.
Holds the six quota axes' weights, `legitimateSkew` (empty, every row needs a `_why`), the gate
thresholds, `exclusion.targetShareMilli`, `speciesUniqueAffixMin`, the tier-2/3 sample sizes and the
acceptance numbers. Every value is a **starting value** and says so.
**Acceptance:**
- [ ] Aptitudes read from `data/seed/aptitudes/roster.json`, elements from
      `data/seed/elements/roster.json`, statuses from the status-catalog mirror — a thirteenth
      aptitude changes the grid by construction
- [ ] `_require`/`_validate` load path **refuses to substitute a default**; a missing key is an error
      at load, never a silent zero
- [ ] `legitimateSkew` starts empty; a row without a `_why` is refused
**Verification:** the loader raises on a stripped key; `missing_thresholds()` lists every gate with no number.
**Depends on:** none. **Scope:** S. **Files:** `data/tuning/passive-tree-targets.v1.json`.
**Phase:** D, **before D1**.

### P2 · D1a: the gate runner and the in-run gates
**Spec:** `spec-tree-language.md` §7 gates 2, 5, 6, 7, 9, 10, 11, 12, 13, 14, 23, 24; §Commands; §Project structure.
**Description:** The harness D4's *"every validation gate green"* is written against. `verdict.py`'s
`GATING_METRICS` + `missing_thresholds()`, the dry-run report, and the shipped in-run gates wired
into the trees adapter: description audit, preflight, contract, brief conformance, text style, vote
resolution, bounded repair, persist-time re-gate, idempotence, run verdict, offline guarantee.
**Acceptance:**
- [ ] `python -m seedsmith check --family PassiveTree --gate` exits 0/1/2/3 on the shipped four codes
- [ ] The dry-run prints `gatingMetrics` and `gatesMissingAThreshold` **before** spending a call, and
      prints the ~4,680-call figure
- [ ] `GATING_METRICS` has **exactly one** entry, and an OPEN-loop metric registered with
      `gates=True` raises
- [ ] `FAIL` beats `NOT_MEASURED`; a held partition alone denies a `PASS`
- [ ] The offline transport stub **raises** on any unexpected call
**Verification:** `python -m pytest tools/seedsmith/tests/adapters/trees`; a run that reaches a model fails the suite.
**Depends on:** D0, D1. **Scope:** M. **Files:** `tools/seedsmith/seedsmith/adapters/trees/nodegen/verdict.py`, `.../run.py`, `tools/seedsmith/tests/adapters/trees/`.
**Phase:** D, before D4.

### P3 · D2a: the eight `PassiveTree/*` corpus metrics
**Spec:** `spec-tree-language.md` §7 gates 15–22; §Project structure (`metrics/passive_tree.py`).
**Description:** `QuotaDrift` (re-derived independently, symmetric), `MechanismRamp` (exact per-tier
count against `archetypes[].mechNodes[t]`, both directions, plus `mechNodes[10] == w[10]`),
`CellOccupancy`, `ExclusionRate`, `ExclusionResolvable`, `NearDuplicate` (local exact Jaccard,
**not** the shared MinHash), `NameCollision`, `UnresolvedCount`.
**Acceptance:**
- [ ] `UnresolvedCount` is the **only** metric at `gates = True`, promoted with `demon_roster.py:357-370`'s reason recorded
- [ ] `QuotaDrift` catches a mutated brief because it re-derives rather than reads
- [ ] `MechanismRamp` is a count, not a threshold — a threshold implementation fails on `broad-and-flat` tiers 4–7
- [ ] `ExclusionResolvable` reports **`NOT_MEASURED`** while the atom-tag registry is unbuilt (§5.1), cited by name and never by ordinal
**Verification:** synthetic corpora with an injected defect per metric — a 166× skew, a missing deep-tier mechanism, a duplicated name across 300 trees.
**Depends on:** D0, D2. **Scope:** M. **Files:** `tools/seedsmith/seedsmith/metrics/passive_tree.py`.
**Phase:** D, before D4.

### P4 · D6: the corpus sheet and the `sheetRead` census gate
**Spec:** `spec-tree-review.md` §5.5, §7; §Success criteria.
**Description:** One page per lot, read **before** the cards: quota heat map, name-token frequency,
exclusion census, nearest-neighbour top 20, rejection rate, machine verdict + `missing_thresholds`,
and the hidden-file census. Plus `PassiveTree/HiddenFileCount` itself — the walk **without** the `_`
skip, reporting `visitedFileCount`, with a canary fixture root.
**Acceptance:**
- [ ] The sheet carries a `sheetRevision`; `trees review --census` **refuses** a lot with no
      `sheetRead` row or a row naming a stale revision
- [ ] The row is `{lot, sheetRevision, by, utc}`, written on dismissal
- [ ] `HiddenFileCount` is green over the real seed roots with a **non-zero** `visitedFileCount`, and
      the same run finds the canary parked entry
- [ ] The sheet and the verdict queue are **committed**; per-tree cards are not
**Verification:** a census against a missing row and against a stale row both refuse, by test; a green with `visitedFileCount == 0` is distinguishable from a green over forty empty files.
**Depends on:** D5. **Scope:** M. **Files:** `web/fusion-rpg-web/scripts/render-tree-cards.mjs`, `tools/seedsmith/seedsmith/metrics/passive_tree.py`, `docs/research/passive-tree/_review/`.
**Phase:** D, **before D3** — the pilot is the first thing that reads a sheet.

### P5 · F2a: the species planner — roster, favour cell, rebalance, drift
**Spec:** `spec-species-tree.md` §2.1, §3.1, §3.2, §4.
**Description:** The deterministic, model-free half of the species pipeline. Roster from
`_index.json` with every file walked **without the `_` skip**; one `mechanicalFavour` cell per
species plus 2–3 alternates from the same quota; the rebalance on a forced override; `FavourDrift`.
**Acceptance:**
- [ ] A species on disk but unindexed, or indexed twice, **halts the run naming both paths** — never *"pick the first one"*
- [ ] `mechanicalFavour` is its **own field**; the anchor's `elementPrimary`/`aptitudePrimary` are inputs to the brief and never the lock — asserted by test
- [ ] A forced cell returns its draw to the pool; an **overdrawn** forced quota is **refused with the rule named**, not rebalanced silently
- [ ] Every alternate offered is inside the quota — so no answer the stage can give breaks the target
- [ ] `FavourDrift` is symmetric: an injected 30% element skew fails it, and so does overshoot
**Verification:** `the_plan_is_reproducible_from_species_id_alone`; a skewed fixture roster.
**Depends on:** D0, D2a. **Scope:** M. **Files:** `tools/seedsmith/seedsmith/adapters/trees/species/plan.py`, `roster.py`.
**Phase:** F, **before F2**.

### P6 · F2b: the species-namespace affix corpus (U3's real bill)
**Spec:** `spec-species-tree.md` §5.2, §5.3 rules 3–5; §5.1.
**Description:** 840 × 8 = **6,720** authored affixes under `affix.species.<speciesId>.*`, against a
shipped authored corpus of **two** (`data/seed/effects/affixes/all.json`). Ids minted once and read
back on regeneration. Plus `PassiveTree/SpeciesUniqueness` and its reverse index.
**Acceptance:**
- [ ] The marked nodes are the **deepest mechanism** nodes, ties on branch order then `nodeKey`, chosen in the planner and never at generation time
- [ ] `raising_species_unique_affix_min_never_unmarks_a_marked_node` holds — the mark set at `k=8` strictly contains the set at `k=4`
- [ ] `speciesUniqueAffixMin = 0` is legal: U1 and U2 still gate and the cross-tree-reference clause still fires
- [ ] `U3` reports a finding when any `affix.species.<id>.*` is referenced from another tree
- [ ] The authoring cost is stated in the plan's Risks table before the run is scheduled
**Verification:** the reverse index over a fixture with two trees sharing a namespace affix.
**Depends on:** F2a. **Scope:** L (a run plus a gate). **Files:** `tools/seedsmith/.../species/`, `data/seed/effects/affixes/`.
**Phase:** F, before F3.

### P7 · E0: the wire, and the shared allocation hook
**Spec:** `spec-tree-surface.md` §12 (project structure and the extraction note), §10.
**Description:** `PassiveTreeEndpoints.cs` (GET state, POST one whole allocation, the shape
`AptitudeEndpoints.cs:26-57` already ships), `PassiveTreeDtos.cs`, and the `useAllocationDraft`
extraction the spec requires **first** — `ProgressionTab.tsx:7-14` already admits its allocation
logic is a verbatim copy, and a tree spend flow would be the third.
**Acceptance:**
- [ ] The allocation-changed broadcast reaches **both** `WebGroup` and `InjectorGroup`, per `AptitudeEndpoints.cs:115-117`
- [ ] `AptitudesPage` and `ProgressionTab` both consume the extracted hook; no third copy is created
- [ ] `guard-dal` green — no SQL outside `FusionRpg.Data`
**Verification:** `dotnet test tests/FusionRpg.Guard.Tests`; `npm run build` clean.
**Depends on:** A5. **Scope:** M.
**Phase:** E, **before E1**.

### P8 · E5: Focus, and the distance presentation
**Spec:** `spec-tree-surface.md` §6, §9.
**Description:** The Focus line (`1/H` as prose, the effective number of paths) and §9's *distance*
presentation for deep tiers — a bar, a `Θ` computed from **this** actor, and the traits shown in
full.
**Acceptance:**
- [ ] Focus renders what `tree-resolve` returns and **never re-derives it**, so a dial change moves the line with no FE edit
- [ ] Focus **moves while the draft is edited**, both halves together (M8 / GG-33)
- [ ] The `UnitClass` union is **byte-identical** before and after — the fractional path count is prose, not a `Magnitude`
- [ ] A locked deep tier carries a **distance**, never a condition; the distance is computed per actor, not stated once
- [ ] Only an undiscovered bloodline is ever a silhouette
**Verification:** `npm test -- magnitudeGuard`; tests 10, 14, 15.
**Depends on:** E2, A6. **Scope:** M.
**Phase:** E.

### P9 · E6: Level 0b, the Codex route, and Level 0/1 in full
**Spec:** `spec-tree-surface.md` §2.2, §3, §7.2 parts 1 and 4, §8's finding rules.
**Description:** The bloodline pin (level 0b), the Codex read route, the five-bucket Level 1
ordering with search and four category filters, the Level 0 empty state, the not-working count and
its filter, and the locked reason as visible sibling text naming both routes through one reason
table.
**Acceptance:**
- [ ] A bloodline is pinned to its creature's sheet and **never enters a browse** — 879 is never a collection anywhere
- [ ] Level 1 orders: invested → your own stance's other three → element/status match → everything else → the collapsed gateless bucket
- [ ] Query state survives closing the layer (GG-51)
- [ ] A locked tier's reason is queried **by text, not by `title`**, and names both routes
- [ ] *"2 of your traits are not working"* filters to exactly those
**Depends on:** E1, and — for the Codex route — the ask-first fix to `DemonsPage.tsx:367-388`'s
volume defect (840 DOM subtrees against a 240 threshold), which is another program's file.
**Scope:** M. **Phase:** E.

### P10 · F5: the three-tier sampling design and the acceptance numbers
**Spec:** `spec-tree-review.md` §3.1, §3.2, §6.3.
**Description:** Tier 1's four census populations, tier 2's 60-tree stratified cluster sample through
the **shipped** `sampling.stratified_sample`, tier 3's ~200 nodes over rare quota cells, and the
acceptance table.
**Acceptance:**
- [ ] Draws go through `sampling.stratified_sample` — **no second sampler is written**
- [ ] Every non-empty stratum gets at least one sample; a rare quota cell appears in the tier-3 draw
- [ ] The same draw twice is identical, seeded from `metric id + corpus revision`
- [ ] Sixty clean trees report the **4.87%** bound, computed not tabled; three rejects in sixty is a batch reject
- [ ] Every acceptance number resolves from `data/tuning/`, mechanically — none in code
**Depends on:** D0, D3. **Scope:** M. **Phase:** F, before F4.

### P11 · F6: escalation, the verdict queue, and the unshippable list
**Spec:** `spec-tree-review.md` §6.1, §6.2, §6.4.
**Description:** Rungs 0–5, the `manualCorrection` stamp and its rate metric, the verdict queue as a
committed machine-readable artifact whose reject reasons become the next run's anti-motifs, and the
nine conditions that make a lot unshippable — including `PassiveTree/ExclusionPresentation`, which
**gates**.
**Acceptance:**
- [ ] A rejection **names the rule and regenerates**; nothing mutates a draft into legality
- [ ] An exclusion printed on one side only, naming two different winners, or whose loser is marked un-unlocked rather than **inert**, denies the lot a pass (D40)
- [ ] A well-presented `nullification` **ships** — stated as a test so the withdrawn rule cannot creep back
- [ ] A verdict writes a machine-readable row; a review producing no artifact did not happen
**Depends on:** P10, D5. **Scope:** M. **Phase:** F, before F4.

### P12 · F7: incremental re-review — the diff card, and its blocker
**Spec:** `spec-tree-review.md` §8; `spec-species-tree.md` §8.
**Description:** The `O(diff)` second pass. The diff card as a second mode of the same card, the
`trees review --diff <fromRev> <toRev>` verb, and the `catalog_revision (from, to)` lot identity.
**Raise `provenance-supersede` as a hard blocker at task start** — `ProvenanceLedger.record` raises
on a re-recorded row, and **pass two cannot run without it**, while F3 budgets 2–3 passes.
**Acceptance:**
- [ ] A magnitude retune produces an **empty** human review queue, proven by test
- [ ] A renamed node id produces a **full tree diff** — the id-stability dependency proven, not assumed
- [ ] A changed node is judged **inside its tree**, never as an isolated line
- [ ] `provenance-supersede` is either built or recorded in the plan's Risks table as blocking pass 2
**Depends on:** F4, A2. **Scope:** M. **Phase:** F.

### P13 · C5: the `UniqueDemon` binding, before the species census
**Spec:** `spec-species-tree.md` §8.1 point 2.
**Description:** Nothing in `src/` passes `AllocationScope.UniqueDemon` to `PointBudget.PointsFor`
or `CheckScope`. Its twin already ships — `SpeciesAllocation.cs:35,62` does exactly this for
`DemonType`, including the index transform `PointBudget.DemonTypeSourceFromLevel`.
**Acceptance:**
- [ ] Specimen level reaches an aptitude budget at `UniqueDemon` scope, mirroring the `DemonType` transform
- [ ] A species tree's tier ladder reads non-zero on an actor with a levelled specimen
**Verification:** a reviewer opening a species card sees a live ladder, not zeros.
**Depends on:** C3. **Scope:** S. **Phase:** C or F, **before F4's census**.

### P14 · E7: the web verification suite (amend the standing block)
**Spec:** `spec-tree-surface.md` §11, §14.
**Description:** The todo's standing verification names `dotnet build`, four guards and two Python
audits — **and no web command**. Every E-phase task therefore has no verification bar.
**Acceptance:**
- [ ] The standing block adds: `npm test -- volumeMatrix diffStateMatrix fourStatesMatrix vocabularyGuard magnitudeGuard bandGuard xyflowGuard`, `npm run build`, `npm run check:bundle`, `npm run test:e2e`
- [ ] E2E volume fixtures at 10 / 100 / 1000 for the browse, plus the 40-cell lattice at the 1280×720 floor
- [ ] `Every_surface_renders_with_the_injector_absent` (GG-39, standalone-first) is a named test
**Depends on:** none. **Scope:** S. **Phase:** E, before E1.

---

## 6. What the plan claims that its spec does not support

1. **D4: *"Every validation gate green or explicitly `NOT_MEASURED`."*** For
   `PassiveTree/UnresolvedCount` — the one metric at `gates=True` — `NOT_MEASURED` **denies a pass**
   (`spec-tree-language.md` §7 gate 23; `spec-tree-review.md` §6.4 rule 1: *"A gating metric failed
   or did not run. An absent check is never a pass"*). As written, D4 would let the primary corpus
   ship with the gating metric unmeasured. Tighten to *"every gate green; the gating metric measured;
   any `NOT_MEASURED` named and cited"*.

2. **F3 budgets *"2–3 passes"* against a blocker the spec calls hard.**
   `spec-species-tree.md` §8 marks `provenance-supersede` ⛔ **Hard**: *"a prompt-version bump cannot
   regenerate… pass 2 cannot start without this."* It appears nowhere in the plan — not as a task,
   not in Risks, not in the non-blocking asks.

3. **The naming ask is classified as non-blocking; the spec files it under *Ask first*.**
   `spec-tree-surface.md` §15: *"A name is content and the owner's call, and one is needed **before
   any player text is written**."* E1's own acceptance — *"three currencies named distinctly"* — is
   player text. The plan's default (*"spec vocabulary until authored"*) is a workable choice, but it
   is only honest if a later task **applies** the authored names. There is no such task. Either
   make it blocking on E1, or add a one-line E-phase task that swaps the vocabulary.

4. **D2 assigns a `tree-plan` deliverable to a `tree-language` task.** D2's first acceptance bullet
   is *"the plan emits the closed property set before any node text exists"* — that is
   `spec-tree-plan.md` §4's `propertyVocabulary`, and **A1's acceptance never lists it**. The
   property vocabulary belongs in A1's emitted output.

5. **D5's `HiddenFileCount` bullet is mis-homed.** `HiddenFileCount` is `spec-tree-review.md` §7's
   metric in `metrics/passive_tree.py`; D5's stated files are `web/fusion-rpg-web/scripts/`. The card
   task cannot deliver a Python metric. Moved into P4 above.

6. **E2 and E3 cite the wrong sections.** E2 cites `spec-tree-surface.md` §5 — which is *Plan before
   spend*. The lattice is §2.3 and the gate-less condition is §9.1. E3 cites §6–§8 and delivers
   nothing from §6 (Focus) at all.

7. **The todo renumbers the spec's own levels.** The spec has Level 0 / 0b / 1 / 2 / 3
   (§2.2). The todo has *"levels 1–2"*, *"Level 3 — the lattice"*, *"Level 4 — the trait"* — off by
   one throughout, which makes every cross-reference between the two documents wrong by a level.

8. **E2's gate-less condition needs a field A6 never promises.** `spec-tree-surface.md` §9.1 rule 5
   requires `TreeResolveReport.gateState` (`wired | unproduced`), *"read from the catalog and never
   inferred from a zero."* A6's acceptance names `req(t)`, `F` and `H`, and no report shape. Add the
   field to A6 or E2 cannot be built as specified.

---

## 7. Fully covered, verified

These need no new task and no amendment. Named so the next pass does not re-derive them.

- **The tree card** (`spec-tree-review.md` §5.1–§5.4) — D5 carries the 2×10 lattice, the species'
  own sentence, the sibling panel, and the rule that matters most: it renders through the **shipped**
  `formatMagnitude` in the web package rather than a second implementation. Verified in code —
  `web/fusion-rpg-web/src/i18n/magnitude.ts:15` takes a `Magnitude` and has no bare-number overload,
  which is the GG-46 guard the card depends on. **So yes, the `formatMagnitude` contract is covered**,
  for the card. What is not covered is the E-phase surface's own use of it — see P14.
- **The review pilot** (`spec-tree-review.md` §8 open Q1, success criterion 1) — D3 measures the real
  per-tree rate and recomputes the census size from it, before phase F is scheduled.
- **The census claim wording** (`spec-tree-review.md` §2) — F4's *"every tree was judged, never the
  catalog was reviewed"* is exact.
- **The retired-node hazard** (`spec-tree-review.md` §8 hazard 2) — A2's *"a legacy fixture with a
  retired node renders red rather than throwing"*, rejected once at import, closes it.
- **`speciesUniqueAffixMin = 8`, deepest-mechanism-first** (`spec-species-tree.md` §5.3, D41) — F2
  names both halves, including the selection rule that keeps a later change `O(diff)`.
- **`codexSummary` emission** (`spec-species-tree.md` §6) — F2 books it into the same run, which is
  the whole point of D30's §10.3 finding.
- **The gate-less *condition* presentation** (`spec-tree-surface.md` §9.1 rules 1–2) — E2 gets it
  exactly right, including *no price and no Unlock verb*.
- **Three currencies, named** (`spec-tree-surface.md` §4.1, R1) — E1's acceptance is the spec's own
  load-bearing sentence.
- **One lender, always singular** (`spec-tree-surface.md` §7.2 part 3) — E4 names it and names why
  (`max`, not a sum).
- **The price of a plan, not of a node** (`spec-tree-surface.md` §5.2) — E4.
- **Content waits for its gate** — F1's *"only after their gate quantities are live (Checkpoint C)"*
  is `tree-plan`'s `R-G1` and the map's sequencing rule, correctly enforced by phase order.

---

## 8. Design-gate checklist

```
[x] I identified the subsystem: the passive-tree program's plan and task list.
[x] I read DESIGN-GATE.md this session, plus passive-tree-map.md and all four
    specs under audit, in full.
[x] Every factual claim cites file:line or spec section.
[x] I verified against CODE where a spec claim was load-bearing for a coverage
    verdict: magnitude.ts:15 (no bare-number overload), PassivesTab.tsx:12-21
    (four LockedGridSlots and the now-false comment), web/.../scripts/ holds
    exactly check-bundle.mjs and gen-tokens.mjs.
[x] I read the surrounding section of every rule quoted - in particular
    spec-tree-review.md 6.4's rule 2, whose D40 history is easy to misread as a
    ban on nullification when it is a presentation contract.
[x] I counted the 24 gates by counting the rows of spec-tree-language.md 7,
    not by trusting the "24" written in spec-tree-review.md:190. They agree.
[ ] I tested (not assumed) any constraint I report. NOT DONE and stated: this is
    a document audit. No suite was run and no code was changed.
[x] Nothing contradicts a 2 invariant. Every proposed task is a plan-side
    addition; none proposes a cap, a private level curve or a new unit class.
[ ] Corrections propagated. NOT DONE by design - this audit writes only itself.
    The corrections belong in tasks/passive-tree-todo.md and are the owner's to
    apply, or a follow-on task's.
```
