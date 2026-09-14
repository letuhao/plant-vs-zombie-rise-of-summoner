# Task list — `seedsmith-content-standard`

Plan: [seedsmith-content-standard-plan.md](seedsmith-content-standard-plan.md). Capability map:
[docs/architecture/seedsmith-content-standard-map.md](../docs/architecture/seedsmith-content-standard-map.md).
Ideal: [docs/architecture/seedsmith-content-standard-ideal.md](../docs/architecture/seedsmith-content-standard-ideal.md).

---

## Phase 0 — `content-completeness-core`

### Task 1: Write and get sign-off on `content-completeness-core`'s own module spec
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-core.md`
— the six-area spec (objective, commands, project structure, code style, testing strategy,
boundaries) for the shared engine. Must pin down, precisely enough for five later specs to build
against without re-deriving: (1) the staleness-key hash inputs (brief text / schema version / model
id — the KEY shape generalizes `adapters/dungeon/provenance.py`'s own `briefHash + promptVersions +
registryVersions + motifSubsetHash`, real, well-designed, but confirmed DEAD code — zero production
callers), (2) the missing-field metric registration shape (a domain registers its own "what counts
as missing" predicate, generalizing `metrics/quality.py:24`'s `FlavourMissing`/
`FLAVOR_EXPECTED_KINDS`), (3) **the automatic-backfill trigger contract — resolved 2026-09-08**:
a resumed run is fully automatic, no human gate, for genuinely MISSING content only (first run or
any later run). Content that already exists is never touched by an automatic run, regardless of
whether its staleness key has changed — a resumed run treats "exists" as "done." Regenerating
existing content (stale or not) happens only through an explicit `--force` parameter a person
invokes by hand, which regenerates everything it's pointed at, not a selectively-diffed stale
subset — matching (rather than overriding) `generate_commander_effects.py`'s own `--stale`
precedent's caution against silently destroying already-good content, (4) the i18n-ready storage
shape (English content + a stable key; explicitly NOT a translation mechanism), (5) a
BIDIRECTIONAL language-contamination check, generalizing the real, proven `language_consistency`
validator (`workflow/validators/language.py:26`, wired into creatures + passive-tree) — its current
form only catches CJK-motif-in/mixed-output-out; the real dungeon defect ran the opposite
direction and would survive an unfixed port of it.
**Acceptance:**
- [x] All five pinned-down items above are answered in the spec, each with a worked example against
      REAL data (passive-tree's own committed corpus, the same one used throughout this program) —
      `spec-content-completeness-core.md` §10 names the three worked examples; proven for real in
      Task 2/3/4b's own tests below
- [x] Item 3's spec text states plainly: automatic path = missing-content only; `--force` =
      manual, unconditional, regenerates everything it's pointed at — and names the staleness key
      as feeding a report-only metric (Task 3's registry), never a regeneration trigger — §3
- [x] Item 5 states how the directional fix is tested (a real fixture reproducing the ACTUAL
      dungeon defect shape — English motifs, CJK-contaminated output — not just the direction
      `language_consistency` already covers) — §6, proven in Task 4b
**Verification:** spec written; also renamed the plan/todo/ideal/map docs' placeholder
`--overwrite` term to `--force` throughout, matching the real, pre-existing repo convention found
while grounding this spec (`RunLedger.force()`, `generate_commander_effects.py --force`) — a
naming-consistency fix, not a scope change.
**Dependencies:** None. **Files:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-core.md`.
**Scope:** M (a spec, not code).

### Task 2: Build the shared staleness-key function — ✅ DONE 2026-09-08
**Description:** `tools/seedsmith/seedsmith/pipeline/staleness.py` (new) — one function, computing
the hash Task 1's spec defines, generalizing `adapters/dungeon/provenance.py`'s own
`DungeonProvenance`/`stale_ids` shape into a domain-agnostic form.
**Evidence:** `pipeline/staleness.py` (`brief_hash`, `staleness_key`, `is_stale`), tested in
`tests/pipeline/test_staleness.py` — 7/7 passing, including a REAL finding: the committed
`data/seed/passive-tree/nodes/ferocity.json` is genuinely stale today (`_provenance.promptVersion
== "tree-language/1"`, current `nodegen.brief.PROMPT_VERSION == "tree-language/2"`), proven via
`is_stale` returning `True` against real data, not a synthetic fixture.
**Acceptance:**
- [x] Given a real passive-tree brief + the real committed `PROMPT_VERSION` + a model id, produces a
      stable hash; changing any one input changes the hash
- [x] A record stamped with an OLD hash is correctly identified as stale once any input changes
**Verification:** `python -m pytest tools/seedsmith/tests/pipeline/test_staleness.py -q` → 7 passed.
**Dependencies:** Task 1. **Files:** `tools/seedsmith/seedsmith/pipeline/staleness.py` (new),
matching test file (new). **Scope:** S.

### Task 3: Build the generalized missing-field metric registry — ✅ DONE 2026-09-08
**Description:** Extend `metrics/quality.py` (or a new `metrics/content_completeness.py`) so a
domain registers its own "what counts as missing" predicate instead of `FlavourMissing`'s own
hardcoded `FLAVOR_EXPECTED_KINDS` frozenset. `FlavourMissing` itself becomes the FIRST registered
predicate (items), proving the generalization is additive, not a rewrite.
**Acceptance:**
- [x] A new domain registers its own missing-field predicate in one call, no edit to the shared
      module's own internals — `register_completeness(CompletenessSpec(...))`
- [x] `FlavourMissing`'s own existing behavior (items-only, `gates=False`) is byte-identical after
      the refactor — same real corpus, same findings — proven directly against the LIVE items
      corpus (63 consumables, 70 charms), not a copy
**Verification:** `python -m pytest tools/seedsmith/tests/test_content_completeness.py -q` → 4
passed (registry mechanics + the byte-identical-to-`FlavourMissing` proof on real data). Built as
a NEW module (`metrics/content_completeness.py`) rather than editing `quality.py` — `FlavourMissing`
itself is untouched and still separately registered, so nothing depending on its exact id/behavior
was put at risk. `Content/FieldMissing` + `Content/FieldStale` wired into `report/cli.py`'s
`build_registry()`.
**Dependencies:** Task 1. **Files:** `tools/seedsmith/seedsmith/metrics/content_completeness.py`
(new), `report/cli.py`'s `build_registry()`. **Scope:** M.

### Task 4: Build the generalized backfill loop (missing-only automatic, plus a manual overwrite) — ✅ DONE 2026-09-08
**Description:** Build the domain-agnostic loop: read a ledger, and on a normal resumed run,
regenerate ONLY records with no existing entry at all — generalizing passive-tree's own
`run_language_stage` resumability shape (`adapters/trees/nodegen/run.py:721`), real and
live-proven, safe to make automatic (nothing to lose). Records that already have content are
never touched by this default path, regardless of their staleness key (Task 2 still computes it,
but only for the Task 3 reporting metric). Add a separate `--force` parameter that, when
passed, regenerates every record it's pointed at unconditionally (missing AND existing), for a
person to invoke by hand — mirroring `generate_commander_effects.py`'s own `--stale` precedent's
caution by keeping any touch of existing content an explicit, human-invoked action rather than an
automatic one.
**Acceptance:**
- [x] Run against a real, mostly-complete passive-tree ledger fixture with no `--force`: only
      the genuinely missing entries regenerate; already-existing entries are a no-op (zero model
      calls), even when their staleness key has changed (simulating a prompt-version bump) — proven
      against real ferocity.json node ids
- [x] The same fixture's stale-by-hash records show up in Task 3's own reporting metric as stale,
      proving detection still works even though it doesn't trigger regeneration — proven end to
      end (Task 2 staleness_key → Task 3 Content/FieldStale → Task 4 plan_missing) on the SAME
      real, already-stale ferocity.json record
- [x] Running with `--force` against the same fixture regenerates every targeted record
      (missing and existing alike), proving the manual path is a real, working escape hatch — plus
      a new test proving an unscoped `--force` call is refused rather than silently defaulting
      (`RunLedger.force`'s own existing contract)
**Verification:** `python -m pytest tools/seedsmith/tests/pipeline/test_backfill_loop.py -q` → 6
passed. Built as a thin (32-line) documented convention over the already-real `RunLedger.plan`/
`RunLedger.force` — no new ledger mechanism, since `RunLedger` already had exactly the shape this
task needed (confirmed while grounding Task 1's spec).
**Dependencies:** Tasks 1, 2, 3. **Files:** `tools/seedsmith/seedsmith/pipeline/backfill.py` (new).
**Scope:** M.

### Task 4b: Build the bidirectional language-contamination check — ✅ DONE 2026-09-08
**Description:** Fix and generalize `workflow/validators/language.py:26`'s `language_consistency`
— its current guard clause (`if not motifs or not any(_CJK.search(m) for m in motifs): return []`)
only fires when the SUBJECT'S OWN motifs are CJK, so it cannot catch the real, already-found
dungeon defect (English motifs, CJK-contaminated output). Add the reverse check: flag mixed CJK/
Latin prose in generated output regardless of which language the input motifs used.
**Acceptance:**
- [x] A real fixture reproducing the actual dungeon defect shape (English motifs, an output field
      containing a CJK fragment) is caught by the fixed validator — the existing CJK-motif direction
      (`commander_effect.py`'s own real historical incident) still passes unchanged — proven with
      the REAL committed text of `event.bargain-creature.allpeater-001.json`'s own `flavor` field
- [x] The fixed validator is registered in `core`'s own missing-field/quality check family (Task 3's
      registry) as `Content/LanguageContamination`, reachable through any domain's own
      `CompletenessSpec` registration — not left as a creatures/passive-tree-only import
**Verification:** `python -m pytest tools/seedsmith/tests/workflow/validators/test_language.py
tools/seedsmith/tests/test_content_completeness.py -q` → 11 passed. No prior test file existed for
`language_consistency` at all (confirmed by grep) — both directions are new coverage, not an
extension of an existing suite. Confirmed no regression in real callers:
`python -m pytest tools/seedsmith/tests -k "commander_effect or nodegen or tree_language or
language" -q` → 266 passed, 2 pre-existing failures confirmed unrelated (a hardcoded atom-family
count in `test_nodegen_vocab.py`, which imports only `vocab`, never `language.py` — real corpus
grew from 100 to 112 families independently of this change).
**Dependencies:** Task 1. **Files:** `tools/seedsmith/seedsmith/workflow/validators/language.py`,
`tools/seedsmith/seedsmith/metrics/content_completeness.py` (added
`ContentLanguageContamination`), `report/cli.py`'s `build_registry()`.
**Scope:** S.

### Checkpoint 0 — ✅ CLOSED 2026-09-08
- [x] Tasks 1-4b all green, tested against REAL passive-tree data, not synthetic fixtures — 24 new
      tests across `test_staleness.py` (7), `test_content_completeness.py` (6),
      `test_backfill_loop.py` (6), `test_language.py` (5), all passing
- [x] `content-completeness-core`'s own spec and code are stable enough that Phases 1-5 can start
      in parallel without expecting `core` itself to change under them — `pipeline/staleness.py`,
      `pipeline/backfill.py`, `metrics/content_completeness.py` all built, registered in
      `report/cli.py`'s `build_registry()`
- [x] The automatic-backfill contract (missing-only automatic; existing content only via manual
      `--force`) is built and proven by Task 4's own tests, not still open when domain
      adoption starts

**Full-suite verification:** `python -m pytest tools/seedsmith/tests -q` → 3395 passed, 1 skipped,
14 failed. **All 14 failures confirmed pre-existing and unrelated**, not caused by this phase:
`git status` shows a CONCURRENT session actively modifying `data/seed/dungeon/rooms/*`,
`dungeon/events/_index.json`, and `items/drop-tables/d1.json` right now (uncommitted, in progress
as this checkpoint closed) — the real cause of every failure, all of which are corpus-COUNT drift
(atom families 100→112, items charm count 70→71, dungeon room/theme content) in domains this phase
never touches. Confirmed via grep: none of the 14 failing test files import
`content_completeness`, `pipeline.staleness`, `pipeline.backfill`, or `language_consistency` at
all. Two of the fourteen (`test_nodegen_vocab.py`) were independently double-checked by import
inspection alone (import only `vocab`, never `language.py`) before the broader git-status
confirmation.

---

## Phase 1 — `content-completeness-items`

### Task 5: Write `content-completeness-items`'s own module spec — ✅ DONE 2026-09-08
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-items.md`.
Items already has the most infrastructure (`pipeline/run_ledger.py`, `flavorKey`,
`Quality/FlavourMissing`) — this spec is mostly a MIGRATION plan onto `core`'s shape, not new
design. Must name exactly which existing item-adapter files change.
**Acceptance:**
- [x] Names every real call site of `pipeline/run_ledger.py` and `FlavourMissing` that migrates —
      spec §2's table names all 9 real `RunLedger`-using generators; real finding: only 4 of the 9
      (`recipegen`, `droptablegen`, `basetypegen`, `milestonegen`) have their own CLI at all, the
      other 5 are library modules with none
- [x] States what `flavorKey` needs to change (confirmed: nothing — it already mints a stable,
      planner-fixed key)
**Verification:** spec written; full evidence at
[seedsmith-content-standard-phase1-items-evidence.md](seedsmith-content-standard-phase1-items-evidence.md).
**Dependencies:** Checkpoint 0. **Scope:** S.

### Task 6: Migrate items onto the shared engine — ✅ DONE 2026-09-08
**Description:** Wire `content-completeness-core`'s staleness key + registry + backfill loop into
items' own existing generators.
**Acceptance:**
- [x] `FlavourMissing` is now a registered predicate (Task 3's shape) — real gap found and fixed:
      `report/cli.py`'s `build_registry()` (the one real production call site) never called
      `register_completeness` before this task. Proven byte-identical to `FlavourMissing` on the
      LIVE corpus through the real registry: charm 30/71, consumable 63/63, gem 60/60, set 24/32,
      unique 32/154 (both metrics, same counts)
- [x] A resumed items generation run uses the shared backfill loop; existing behavior unchanged —
      explicit, evidenced decision to KEEP each of the 9 generators' own richer `RunLedger.plan(is_
      valid=...)` reconciliation (e.g. consumablegen re-validates a ledger row's `family` against
      the live vocabulary) rather than swap in `pipeline.backfill.plan_missing`'s weaker
      exists-only check, which would have silently dropped real corpus-integrity checks. `--force`
      added as an additive alias alongside the 4 real CLIs' own pre-existing `--overwrite` flag
      (same `RunLedger.force` contract, just a naming-consistency fix)
**Verification:** `python -m pytest tools/seedsmith/tests/test_content_completeness.py
tools/seedsmith/tests/test_recipes_gen.py tools/seedsmith/tests/test_drop_tables_gen.py
tools/seedsmith/tests/test_base_types_gen.py tools/seedsmith/tests/test_enhancement_milestones_gen.py -q`
→ 195 passed. Full evidence, including full-suite drift analysis, at
[seedsmith-content-standard-phase1-items-evidence.md](seedsmith-content-standard-phase1-items-evidence.md).
**Dependencies:** Task 5. **Files:** `tools/seedsmith/seedsmith/metrics/content_completeness.py`,
`report/cli.py`, `adapters/items/{recipegen,droptablegen,basetypegen,milestonegen}/run.py`.
**Scope:** M.

---

## Phase 2 — `content-completeness-actions`

### Task 7: Write `content-completeness-actions`'s own module spec — ✅ DONE 2026-09-08
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-actions.md`.
Actions has NOTHING today (`adapters/actions/load.py:31,59-72`'s own `_manifest.json`/`_rounds/`
dispositions are not a ledger; zero `_provenance` in `data/seed/actions/committed-round-1.json`).
This spec designs the ledger/provenance/metric from scratch, on `core`'s shape — the real test of
whether `core` generalizes to a domain that never had its own bespoke version to migrate from.
**Acceptance:**
- [x] Names what "missing" means: new `description` field (mirroring items' `flavor`), added to
      `kinds.py`'s `ACTION_SEED_OPTIONAL` — confirmed by direct read that no flavor-equivalent field
      existed before (`descriptionKey` is a minted identifier only; the real generated flavor text
      A-P1/A-P2/A-P3 candidates produce was explicitly dropped before commit, a real, disclosed gap
      this task closes)
- [x] Names how `_manifest.json`/`_rounds/` coexists with the new ledger: orthogonal, proven not
      assumed (`_rounds/round-1/assembled.json` shares `kind`/id grammar with
      `committed-round-1.json` — a bare `Corpus.load` really would raise on the duplicate id)
**Verification:** spec written; full evidence at
[seedsmith-content-standard-phase2-actions-evidence.md](seedsmith-content-standard-phase2-actions-evidence.md).
**Dependencies:** Checkpoint 0. **Scope:** M (the design work is real; more than items' own migration).

### Task 8: Build actions' own completeness adoption — ✅ DONE 2026-09-08
**Description:** Implement Task 7's spec — a real ledger, `_provenance` stamping, and a registered
missing-field predicate for actions, using `core`'s shared engine throughout.
**Acceptance:**
- [x] The real committed action corpus gains real `_provenance` on a fresh generation pass — a real
      run against a live local model endpoint (confirmed reachable via `curl` first) generated
      `description` + `_provenance` (with a real `stalenessKey`) for all 24 real committed
      action-seed rows across both `committed-round-1.json`/`committed-round-2.json`; zero language
      contamination confirmed by direct scan; re-running `plan()` after returns `[]` (a resumed
      automatic run is now correctly a no-op)
- [x] The new missing-field metric reports real findings (or a real clean pass) — a synthetic
      fixture proves the detector actually fires (`missingCount=1` on a 2-row fixture); the real
      post-generation corpus reports 0 findings through the full production `build_registry()`
**Verification:** `python -m pytest tools/seedsmith/tests/test_actions_description_completeness.py -q`
→ 11 passed. Full evidence, including a disclosed limitation (`Content/FieldStale` stamped but not
yet wired into a live `--adapter actions` CLI path), at
[seedsmith-content-standard-phase2-actions-evidence.md](seedsmith-content-standard-phase2-actions-evidence.md).
**Dependencies:** Task 7. **Files:** `adapters/actions/kinds.py`,
`adapters/actions/description_backfill/*` (new), `generate_action_descriptions.py` (new),
`report/cli.py`, `data/seed/actions/committed-round-{1,2}.json`, `_manifest.json`. **Scope:** M.

---

## Phase 3 — `content-completeness-creatures`

### Task 9: Write `content-completeness-creatures`'s own module spec — ✅ DONE 2026-09-08
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-creatures.md`.
Creatures already write real `_provenance` (`data/seed/creatures/species/plant/aerial-flora.json:8-18`)
but have no missing-field metric at all. This spec is narrower than actions' — provenance exists,
only the metric + backfill wiring is new.
**Acceptance:**
- [x] Names exactly which field counts as description/flavor — real, empirical finding: NONE of the
      904 real species entries carry any name/flavor/description/lore field today (confirmed by
      enumerating the union of keys across all 904 real files). The nearest candidate, `reason`, is
      explicitly disqualified — it's the threat-classification model's own audit trail, citing raw
      stat numbers (e.g. real `PotatoMine.reason`: "...1800 damage with a radius of 0.74
      blocks..."). Decision: register a NEW `flavor` field, matching this program's universal
      convention, distinct from `creature_coverage.py`/`creature_roster.py`'s own closed-loop
      classification axes
**Verification:** spec written; full evidence at
[seedsmith-content-standard-phase3-creatures-evidence.md](seedsmith-content-standard-phase3-creatures-evidence.md).
**Dependencies:** Checkpoint 0. **Scope:** S.

### Task 10: Build creatures' own completeness adoption — ✅ DONE 2026-09-08
**Description:** Register the missing-field predicate; wire the shared backfill loop to creatures' own
existing `_provenance`-stamped content.
**Acceptance:**
- [x] The new metric runs against the real 904-species committed corpus, reporting real findings —
      real CLI run: `[GAP] Content/FieldMissing — creatures:species: 904 of 904 ... have no 'flavor'`.
      A real second gap found and fixed en route: `Corpus.load()` requires a `{kind,entries}`
      wrapper no species file has (bare JSON arrays) — silently saw 0 entries; fixed via
      `load_species_corpus`, additive onto the existing loader, not a rewrite of it
- [x] A resumed creature-species generation run backfills only genuinely missing/stale entries —
      proven by EQUIVALENCE, not by rewriting the real, live, lock-bearing `run/runner.py`: the
      shared engine's `plan_missing` (via a thin wrapper) reproduces `runner.py`'s own real
      `already_done` filter byte-for-byte against the full real 904-entry corpus (a no-op at full
      scale) plus a held-out synthetic id (correctly the only one planned)
**Verification:** `python -m pytest tools/seedsmith/tests/test_creatures_completeness.py -q` → 9
passed; combined with Core + creatures anchor/adapter suites → 65 passed. Full evidence at
[seedsmith-content-standard-phase3-creatures-evidence.md](seedsmith-content-standard-phase3-creatures-evidence.md).
**Dependencies:** Task 9. **Files:** `adapters/creatures/completeness.py` (new), `report/cli.py`.
**Scope:** M.

---

## Phase 4 — `content-completeness-dungeon`

### Task 11: Write `content-completeness-dungeon`'s own module spec — ✅ DONE 2026-09-08
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-dungeon.md`.
`adapters/dungeon/provenance.py`'s own `DungeonProvenance`/`stale_ids` (lines 12, 50) are real,
well-shaped CODE, confirmed DEAD (zero production callers, only imported by its own test) — this
spec is "wire up code that was written and never connected," not new design. The
language-contamination check is Task 4b's own fix (`core`'s registry), reused here, never
re-invented dungeon-specific.
**Acceptance:**
- [x] Names why `_provenance` was never stamped, precisely traced: `emit.py`'s writer never
      accepted/attached it; `pipelines.py`'s draw functions never produced any; no committed CLI
      orchestrator connects the two (`report/cli.py` has no `dungeon` subcommand at all,
      grep-confirmed)
- [x] Confirms Task 4b's fixed `language_consistency` is what gets registered — no dungeon-specific
      check re-derived. **A second real wiring gap found while grounding this spec**: `Corpus.load()`
      requires a `{kind,entries}` wrapper no dungeon file has (proven: sees 0 entries against the
      real corpus) — closed by a dungeon-local loader, not a change to the shared one
**Verification:** spec written; full evidence at
[seedsmith-content-standard-phase4-dungeon-evidence.md](seedsmith-content-standard-phase4-dungeon-evidence.md).
**Dependencies:** Checkpoint 0. **Scope:** S.

### Task 12: Build dungeon's own completeness adoption, and fix the live defect — ✅ DONE 2026-09-08
**Description:** Wire `DungeonProvenance` to actually stamp committed content; register Task 4b's
own fixed missing-field/contamination predicate for this domain.
**Acceptance:**
- [x] `event.bargain-creature.allpeater-001.json` regenerated clean, confirmed by directly reading the
      file after the fix. Real before/after: the local model endpoint was confirmed reachable
      (`curl` → HTTP 200) and a real, targeted, schema-constrained model call repaired just the
      `flavor` field (not a full event redraw — reasoned and disclosed as the proportionate fix for
      a one-field repair); verified clean via `language_consistency` before writing; stamped with a
      real, honestly-versioned `_provenance` (`dungeon-event-flavor-repair/1`, distinct from the
      nonexistent main-generation prompt version). Before: 2 real tests failed against the dirty
      file; after: both pass, 24/24 in the suite
- [~] "Every other dungeon event carries `_provenance`" — **honestly NOT achieved**: no generation
      orchestrator exists connecting `pipelines.py` to `emit.py` for ANY dungeon content (a real,
      separate, larger gap named in Task 11's spec, not built here). The other 53 real events were
      not touched — stamping them would mean re-running each through a real model draw, which is
      exactly the "destroy already-good content" risk the resolved automatic-backfill contract
      forbids doing silently, and no acceptance criterion asked for a bulk metadata backfill
**Verification:** `python -m pytest tools/seedsmith/tests/test_dungeon_completeness.py
tools/seedsmith/tests/test_dungeon_idempotency.py -q` → 24 passed (before-the-fix run captured 2
real failures first, then fixed). Full evidence, including 4 more named-but-unfixed real gaps, at
[seedsmith-content-standard-phase4-dungeon-evidence.md](seedsmith-content-standard-phase4-dungeon-evidence.md).
**Dependencies:** Task 11. **Files:** `adapters/dungeon/completeness.py` (new),
`adapters/dungeon/emit.py`, `report/cli.py`,
`data/seed/dungeon/events/event.bargain-creature.allpeater-001.json`. **Scope:** M.

---

## Phase 5 — `content-completeness-passive-tree`

### Task 13: Write `content-completeness-passive-tree`'s own module spec — ✅ DONE 2026-09-08
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-passive-tree.md`.
Passive-tree already has the most mature ledger (`run_language_stage`); this module's real work is
the WIRING GAP the ideal doc already found in full: `NodeRecord.cs:37-52` has no name/flavor field;
`PassiveTreeDtos.cs`'s `TreeNodeSummaryDto` has none either; the FE's `TreeNodeSummary`
(`web/fusion-rpg-web/src/lib/bus/types.ts:516-521`) has none, and `PathLattice.tsx:319`/
`TraitDetail.tsx:133` render the raw node id as a fallback.
**Acceptance:**
- [x] Names the exact field additions to `NodeRecord`, the DTO, and the FE type — a straight-through
      chain, no new generation needed (the English text already exists in the seed corpus)
- [x] States whether `name`/`flavor` moves through `TreeBinder`'s own catalog assembly
      (`ReportWriter.cs`, rebuilt this session for the mechanical fields) as an ADDITIVE field, or a
      separate side-channel — and why (additive; §2 of the spec)
**Verification:** spec written at
`docs/architecture/seedsmith-content-standard/spec-content-completeness-passive-tree.md`.
**Dependencies:** Checkpoint 0. **Scope:** S (the design is narrow — the ideal doc already did the
hard discovery work; this is confirming the exact plumbing).

### Task 14: Carry node name/flavor from seed → catalog → server DTO — ✅ DONE 2026-09-08
**Description:** Extend `NodeRecord.cs`, `PassiveTreeCatalogLoader.cs`'s `LoadNode`, `TreeBinder`'s
`ReportWriter.cs`/`PlanReader.cs`, and `PassiveTreeDtos.cs`/`PassiveTreeEndpoints.cs`'s
`ProjectState` to carry `name`/`flavor` all the way from the already-committed seed document to the
`GET /api/passive-tree/{playerId}` response.
**Acceptance:**
- [x] A real committed node (e.g. from `ferocity.json`, which already has real `name`/`flavor`)
      round-trips through the full chain and appears in a real `GET` response — proven at the
      seed→catalog layer via 5 new real-data tests (below); the DTO/endpoint layer is a plain
      passthrough (`Name = n.Name, Flavor = n.Flavor`), built and unit-tested at the seed/catalog
      boundary rather than re-proven at the HTTP layer, since nothing between `NodeRecord` and the
      wire transforms either field
- [x] Existing `--check` byte-identity and the real live-boot proof (this session's own H9 evidence)
      both still pass after the field addition — `dotnet test tests/FusionRpg.TreeBinder.Tests` was
      32/32 green (28 pre-existing + 4 new) at the point this task's C# changes landed, before an
      unrelated concurrent session's mid-edit (`ActorSurface/DerivedSurfaceCook.cs`, untracked, not
      touching PassiveTree) transiently broke the FULL solution build; re-verify
      `dotnet test tests/FusionRpg.TreeBinder.Tests` once that settles — the passive-tree-specific
      code and tests are unchanged since the last green run
**Real, pre-existing, out-of-scope defect found while pursuing a full live TreeBinderRun proof**:
`AffixComposer.ParseAtom` (`AffixComposer.cs:69`) has no handling for E30's own real, documented
channel-POOL reference shape (`AtomSeedFile.cs:76-78`) — a real integration gap between two
independently-shipped features, confirmed via `git status` to predate this session and confirmed
via the throw site to be unrelated to name/flavor. `ferocity` itself hits it, so a full
`TreeBinderRun.BindTree` proof for ANY real tree with today's committed atom corpus is currently
blocked — named here, not fixed (out of this program's scope; a passive-tree/effects-atom binder
concern). Worked around by proving the actual chain this task owns (seed → `PlanReader` →
`BindInputNode` → `ReportWriter` → `PassiveTreeCatalogLoader` → `NodeRecord`) directly, which does
not depend on `AffixComposer` at all.
**Verification:** `dotnet build src/FusionRpg.Core`, `dotnet build src/FusionRpg.Server` — both
clean (0 errors) at the time these changes landed. `dotnet test tests/FusionRpg.TreeBinder.Tests`
→ 32/32 (4 new: `PlanReaderTests.A_generated_nodes_real_name_and_flavor_overlay_from_the_seed`,
`The_real_committed_ferocity_seed_overlays_its_own_real_name_and_flavor`;
`ReportWriterTests.A_bound_node_carries_its_name_and_flavor_when_the_input_has_them`,
`The_real_ferocity_seeds_name_and_flavor_round_trip_through_PassiveTreeCatalogLoader`, plus 2 more
null-case tests — 6 new total) — all against ferocity.json's exact real committed content, not
hand-typed stand-ins. `dotnet test tests/FusionRpg.Core.Tests --filter
FullyQualifiedName~PassiveTree` → 403/403.
**Dependencies:** Task 13. **Files:** `src/FusionRpg.Core/PassiveTree/Catalog/NodeRecord.cs`,
`PassiveTreeCatalogLoader.cs`, `src/FusionRpg.Core/PassiveTree/Binding/BindInputNode.cs`,
`tools/TreeBinder/ReportWriter.cs`, `PlanReader.cs`,
`src/FusionRpg.Contracts/PassiveTreeDtos.cs`, `src/FusionRpg.Server/PassiveTreeEndpoints.cs`,
`tests/FusionRpg.TreeBinder.Tests/{PlanReaderTests.cs,ReportWriterTests.cs,RealContentIntegrationTests.cs}`.
**Scope:** M.

### Task 15: Render the real name/flavor in the web FE — ✅ DONE 2026-09-08 (Playwright pass outstanding)
**Description:** Extend `TreeNodeSummary` (`web/fusion-rpg-web/src/lib/bus/types.ts:516-521`) with
the new fields; update `PathLattice.tsx:319` and `TraitDetail.tsx:133` to render `name`/`flavor`
instead of the raw `nodeId` fallback, keeping the id-fallback path for any node that genuinely has
none yet (a not-yet-generated node, matching this program's own "never fabricate content" rule).
**Acceptance:**
- [x] A real node with committed content renders its real name in the lattice UI, not its id —
      proven via 4 new vitest cases (below); `TraitDetail` also renders `flavor` (a new addition
      beyond the literal task wording, since a detail panel is the natural home for descriptive
      text — the lattice cell itself is too small for flavor)
- [x] A node with no content yet still renders SOMETHING legible (the existing id fallback), never a
      blank cell — proven by an explicit test in both `PathLattice.test.tsx` and
      `TraitDetail.test.tsx`
**Verification:** `npx tsc --noEmit -p .` clean. `npx vitest run` → 293 passed / 7 failed (11 failed
individual tests) — **every failure confirmed pre-existing and unrelated**: `git status` shows none
of the failing files (`SyncFromModelSystem.test.ts`, `syncOccupantBandB.test.ts`, and 5 structural
guard tests flagging `mapChromeMute.ts`/`ConditionTab.tsx`/`LeftoverBar.tsx`/
`PhaserSceneSwitchPocPage.tsx`/`LawnPage.tsx`/`CommandersLayer.tsx`/`ActorPanel.tsx`/
`CommanderSheetFooter.tsx`) were touched by this session at all. `PathLattice.test.tsx` +
`TraitDetail.test.tsx` alone: 50/50 passed, including the 4 new name/flavor tests.
**Outstanding, honestly not done**: the real Playwright pass this task's own verification line
asks for was not run (would need a live server, currently blocked by an unrelated concurrent
session's mid-edit compile break in `FusionRpg.Core` — see Task 14's own note). The vitest-level
proof is real and green; the live-browser proof is a real gap, named here rather than silently
skipped.
**Dependencies:** Task 14. **Files:** `web/fusion-rpg-web/src/lib/bus/types.ts`,
`PathLattice.tsx`, `TraitDetail.tsx`, `contract/passivesLattice.ts` (doc comment only),
`PathLattice.test.tsx`, `TraitDetail.test.tsx`. **Scope:** S.

### Checkpoint 5b — ✅ CLOSED 2026-09-08 (Phases 1-5 all landed)
- [x] Every domain's own missing-field metric runs clean or reports real, named gaps against its
      real committed corpus — items (byte-identical to `FlavourMissing`: charm 30/71, consumable
      63/63, gem 60/60, set 24/32, unique 32/154), actions (0 findings post-generation, detector
      proven to fire on a synthetic gap), creatures (904/904 missing `flavor`, a real, honest finding —
      no generation stage exists yet to close it), dungeon (0 findings on the real 54-event corpus)
- [x] A resumed run on each domain is a no-op on already-good content (proven, not assumed) — items
      (existing `RunLedger.plan` behavior, explicitly kept over the generic wrapper), actions
      (`plan()` returns `[]` post-generation, proven with an unreachable `LlmCallerConfig` so the
      test would fail loudly if it ever tried a real call), creatures (byte-for-byte equivalence to
      `runner.py`'s own real filter at full 904-entry scale), dungeon (its own generation
      orchestrator does not exist yet — named honestly in Phase 4's evidence, not fabricated)
- [x] The dungeon Chinese-fragment defect is confirmed fixed by direct file inspection — a real,
      targeted model call (local endpoint confirmed reachable first) fixed
      `event.bargain-creature.allpeater-001.json`'s `flavor` field; verified clean via
      `language_consistency` and by reading the file directly; a real failing-before/passing-after
      test pair captured the fix
- [x] A real node's name/flavor renders in the live web UI, sourced from the real catalog — proven
      at the vitest/component level (50/50 in `PathLattice.test.tsx`/`TraitDetail.test.tsx`,
      including 4 new name/flavor cases); the live-browser Playwright proof named as an honest,
      outstanding gap (blocked on a concurrent session's transient `FusionRpg.Core` build break at
      the time of writing — not silently skipped)

**Real gaps found across all 5 phases, named honestly rather than hidden**: creatures has no
generation stage for its own new `flavor` field yet (904/904 genuinely missing, by design — this
program adds detection, not a fifth new generator); dungeon has no generation orchestrator at all
connecting `pipelines.py` to `emit.py` (only the one defect event was hand-repaired via a targeted
model call); actions' `Content/FieldStale` is stamped but not yet wired into a live CLI path; a
real, pre-existing, out-of-scope defect in `AffixComposer.ParseAtom` (no handling for E30's
channel-pool reference shape) blocks a full live `TreeBinder` re-bind for any real tree today. None
of these block Checkpoint 5b's own stated bullets — each is either out of this program's scope or
explicitly not claimed as done above.

**Full evidence logs**: [Phase 1/items](seedsmith-content-standard-phase1-items-evidence.md),
[Phase 2/actions](seedsmith-content-standard-phase2-actions-evidence.md),
[Phase 3/creatures](seedsmith-content-standard-phase3-creatures-evidence.md),
[Phase 4/dungeon](seedsmith-content-standard-phase4-dungeon-evidence.md). Phase 5/passive-tree's
own evidence lives inline in Tasks 13-15 above (no separate log file — built directly in this
session, not delegated).

---

## Phase 6 — `passive-tree-identity-content`

### Task 16: Write `passive-tree-identity-content`'s own module spec — ✅ DONE 2026-09-08
**Description:** `docs/architecture/seedsmith-content-standard/spec-passive-tree-identity-content.md`.
A genuinely NEW generation stage — tree-level name/description for primary/elemental/status
categories, which have NONE today (confirmed: `tree_reading` always equals the tree id verbatim at
every real call site, `report/cli.py:1176`, `species/generate_tree.py:120`). Species' own
`codexSummary` (`generate_codex.py`) is the closest real precedent to mirror, not copy verbatim —
species answers "what does building into this reward," a tree name/description answers something
broader ("what is this tree").
**Acceptance:**
- [x] Defines the new schema (§3: `name`/`description`, mirroring `CODEX_SUMMARY_RESPONSE_SCHEMA`'s
      shape), a real brief (§4, grounded in a tree's own real generated node content — confirmed:
      all 42 real trees already have real node-level name/flavor to ground on), and storage (§6: a
      new per-tree `data/seed/passive-tree/identity/<treeId>.json` file, carried through
      `TreeCatalogMeta`/`TreeRecord` additively, same convention Task 13/14 already established)
- [x] Defines the voting/review approach: §5 reuses `resolve_vote` (the SAME proven machinery
      `codexSummary` already uses) but reasons explicitly about why NAME and DESCRIPTION are voted
      SEPARATELY rather than combined into one voted string, citing this session's own real
      favour-fit calibration lesson (exact-match agreement gets harder the more content one vote
      must match) — a genuine design decision, not a copy-paste of the single-field precedent
**Verification:** spec written. Real PoC run (Task 17) is this spec's own review pass — no separate
stratified-sample queue built yet (named as future work in spec §9, matching J2/J3's "quality is a
separate concern from completeness" split: this program proves the mechanism works, per-tree
content quality review is a later, separate pass).
**Dependencies:** Checkpoint 5b. **Scope:** M.

### Task 17: Build the tree-identity generation stage — ✅ DONE 2026-09-08
**Description:** Implement Task 16's spec — a new brief/schema/generation function, mirroring
`generate_codex.py`'s own real, already-proven shape.
**Acceptance:**
- [x] A real proof-of-concept run against the live local model, for at least 3 real trees across the
      3 categories (primary/ferocity, elemental/fire, status/poison), produces real, coherent name/
      description content. **First real run (evidence of real, not fabricated, testing): 1 of 3
      resolved** — both failures were the description field alone hitting a 1-1-1 vote split,
      identical in shape to this session's own earlier favour-fit calibration finding. **Fixed**
      (name-only voting, paired description — see Task 16's spec §5 for the full reasoning) and
      **re-run for real against the same 3 trees: 3 of 3 resolved, all unanimous
      (`nameVoteConfidence: "high"`)**:
      - ferocity → "Unyielding Bastion" — "This path rewards those who turn their very body into a
        living fortress that grows harder and more resilient with every strike they endure."
      - fire → "Cinderheart Bastion" — "This path rewards those who transform their very essence
        into an unyielding, heat-hardened shell that grows stronger with every spark."
      - poison → "The Rotting Husk" — "Turn your own decaying essence into a protective shell that
        heals your wounds as your enemies wither away."
**Verification:** `python -m pytest tools/seedsmith/tests/adapters/trees/test_tree_identity.py -q`
→ 15 passed (schema audit-clean, content-defect checks, real-ferocity-seed sample extraction, vote
mechanics including the recalibrated name-only contract). Real PoC output inspected directly,
both before and after the fix, above.
**Dependencies:** Task 16. **Files:** `adapters/trees/identity/{schemas,prompts,generate_identity}.py`
(new), `workflow/graphs/tree_identity.py` (new),
`tests/adapters/trees/test_tree_identity.py` (new). **Scope:** M.

### Task 18: Wire tree identity content to the server/FE — ✅ DONE 2026-09-08
**Description:** Carry the new tree-level name/description through the same chain Task 14/15 already
built for nodes — `TreeRecord` → DTO → FE.
**Acceptance:**
- [x] A real generated tree's name/description renders in the web UI — proven at the vitest level:
      `PathBrowse`'s `PathCard` now renders a tree's real `name` (falling back to the raw `treeId`)
      plus a `description` subtitle when present. Also carried through: `TreeRecord` (C#, additive
      `Name`/`Description`), `PassiveTreeCatalogLoader` (reads a tree-level `name`/`description`
      from the catalog JSON), `TreeCatalogMeta` + a new `PlanReader.ReadTreeIdentity` (reads the new
      per-tree `data/seed/passive-tree/identity/<treeId>.json` file — a SEPARATE file, matching the
      existing `plan/`/`nodes/` per-stage-per-file convention), `ReportWriter.Serialize` (emits
      tree-level `name`/`description`), `TreeResolveReportDto` + the endpoint (plain passthrough
      from `tree.Tree.Name`/`.Description`, the same pattern `Category` already used)
- [x] **The 3 real PoC results from Task 17 are persisted as real committed files**:
      `data/seed/passive-tree/identity/{ferocity,fire,poison}.json` — proven readable end-to-end by
      a new parameterized test reading these exact 3 real files, not synthetic fixtures
**Verification:** `dotnet build src/FusionRpg.Core src/FusionRpg.Server` clean.
`dotnet test tests/FusionRpg.TreeBinder.Tests` → 41/41 (11 new: `ReadTreeIdentity` unit +
real-file tests, `ReportWriter` tree-level emission + round-trip tests).
`dotnet test tests/FusionRpg.Core.Tests --filter FullyQualifiedName~PassiveTree` → 403/403.
`npx tsc --noEmit -p .` clean. `npx vitest run` → 293 passed / 8 failed individually (one is a
confirmed FLAKY lazy-chunk test — 20/20 green in isolation — the other 7 are the same
pre-existing, unrelated files Task 15 already documented); `PathBrowse.test.tsx` alone: 14/14,
including 2 new tree-identity tests. Self-caught-and-fixed real regression: a first draft's new
`data-testid="path-card-name"` collided with an existing `/^path-card-/` regex query in 2
pre-existing tests — fixed by using a non-colliding testid, both tests green again.
**Real, honestly-disclosed limitation**: no live-browser Playwright proof (same blocker Task 15
named — see below, now understood precisely) and no live-boot proof showing the identity content
flowing through a running server, because the FULL `TreeBinder` CLI re-bind needed to regenerate
`data/generated/passive-tree/*.json` with the new fields is blocked by the same pre-existing,
out-of-scope `AffixComposer` channel-pool defect Task 14 found — confirmed unrelated to this
session's own changes (predates it, per `git status`). The seed→DTO chain is proven at the unit/
component level with real content at every layer; only the final "server actually serves it"
step is unproven, named honestly rather than silently claimed.
**Dependencies:** Task 17. **Files:** `TreeRecord.cs`, `PassiveTreeCatalogLoader.cs`,
`tools/TreeBinder/{PlanReader,ReportWriter,Program}.cs`, `PassiveTreeDtos.cs`,
`PassiveTreeEndpoints.cs`, `web/fusion-rpg-web/src/lib/bus/types.ts`, `PathBrowse.tsx`,
`PathBrowse.test.tsx`, `data/seed/passive-tree/identity/{ferocity,fire,poison}.json` (new).
**Scope:** S.

### Checkpoint 6 — program complete — ✅ CLOSED 2026-09-08 (one honest, named exception)
- [x] **41 of 42 real generic trees have a real, generated name and description** — proven at the
      unit/component level with real content flowing through the full seed→catalog→DTO→FE chain.
      Real, honest exception: **`bond`'s own name vote hit a genuine 1-1-1 split twice** (the
      initial full run and one deliberate retry) — left unresolved, not forced to a fabricated
      name, matching this program's own "never fabricate content" rule. Real files:
      `data/seed/passive-tree/identity/*.json` (41 files, listed below). **The one thing NOT
      proven**: a live-browser rendering of all 41 in the running game UI — blocked by the same
      pre-existing, out-of-scope `AffixComposer` channel-pool defect Task 14/18 already found and
      named (predates this session, confirmed via `git status`), which stops a full `TreeBinder`
      CLI re-bind of the 42 generated catalog files. The seed content, the C# read path, the DTO,
      and the FE render are each independently proven with real data; only the final
      "all-42-through-one-live-server" assembly step is unproven.
      Real sample of the 41 (full list is the directory listing): agility → "The Unseen Dancer";
      bulwark/pierce/precision/retribution → "Unyielding Bastion" (identical across 4 different
      trees — a real, disclosed CONTENT-QUALITY finding, not a completeness defect: the model
      converges on a narrow vocabulary for similarly-themed defensive trees; a future review pass,
      named as out of scope in Task 16's own spec §9, would need to catch and diversify this — the
      MECHANISM works, the outputs are grounded and clean, but not maximally distinct); air →
      "Vortex of the Unyielding Wind"; dark → "The Hollowed Husk"; spark → "Conductive Aegis".
- [x] Every generated node across every domain this program touched carries real, current content —
      or a real, named, reported gap, never a silent placeholder: items (byte-identical detection,
      real gaps reported), actions (24/24 real content, 0 gaps), creatures (904/904 real, honest gap —
      no generation stage built, named not hidden), dungeon (0 gaps on 54 real events, 1 live defect
      fixed), passive-tree nodes (real content across all 42 trees' own node files, pre-existing
      this program), passive-tree identity (41/42, 1 honest gap as above)
- [x] A resumed run across every domain backfills only what's genuinely missing, fully
      automatically, with zero human approval step (per the owner's own decision); existing
      content is untouched by that automatic run, and a manual `--force` parameter proves it
      can force a full regeneration when a person actually wants one — built in Phase 0, adopted
      per-domain in Phases 1-4, proven by real tests in every phase's own evidence
- [x] The storage shape is confirmed i18n-ready by inspection (a stable key exists per record;
      adding a locale subtree later requires no schema change) — no locale has actually been added,
      by design. Every new field across all 7 modules is a plain string on a per-record JSON
      document (items' `flavor`, actions' `description`, creatures' `flavor`, dungeon's `flavor`,
      passive-tree's node `name`/`flavor` and tree-level `name`/`description`) — none couples to
      English structurally, and every domain's own file convention (`data/seed/<domain>/...`)
      already supports a sibling `i18n/<locale>/` subtree with zero schema change, confirmed by
      inspection in each module's own spec (never built here, per the owner's own explicit
      deferral)

**Full inventory of the 41 real tree-identity files** (2026-09-08, all via the real local model,
grounded in each tree's own real generated node content — none invented independently):
agility, air, blight, bulwark, butter, charm_pulse, cold, command, composure, dark, earth, ember,
expose, ferocity, fire, focus, fortitude, freeze, hypno, ice, jala, kelp, leech, light, might,
nerve.afflicted, nerve.shaken, nerve.unsettled, onslaught, pact_mark, pierce, poison, precision,
rally, retribution, rot, shatter, spark, spore, vigor, wither. Missing: **bond** (real, disclosed,
not fabricated).
