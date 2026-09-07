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
existing content (stale or not) happens only through an explicit `--overwrite` parameter a person
invokes by hand, which regenerates everything it's pointed at, not a selectively-diffed stale
subset — matching (rather than overriding) `generate_commander_effects.py`'s own `--stale`
precedent's caution against silently destroying already-good content, (4) the i18n-ready storage
shape (English content + a stable key; explicitly NOT a translation mechanism), (5) a
BIDIRECTIONAL language-contamination check, generalizing the real, proven `language_consistency`
validator (`workflow/validators/language.py:26`, wired into demons + passive-tree) — its current
form only catches CJK-motif-in/mixed-output-out; the real dungeon defect ran the opposite
direction and would survive an unfixed port of it.
**Acceptance:**
- [ ] All five pinned-down items above are answered in the spec, each with a worked example against
      REAL data (passive-tree's own committed corpus, the same one used throughout this program)
- [ ] Item 3's spec text states plainly: automatic path = missing-content only; `--overwrite` =
      manual, unconditional, regenerates everything it's pointed at — and names the staleness key
      as feeding a report-only metric (Task 3's registry), never a regeneration trigger
- [ ] Item 5 states how the directional fix is tested (a real fixture reproducing the ACTUAL
      dungeon defect shape — English motifs, CJK-contaminated output — not just the direction
      `language_consistency` already covers)
**Verification:** spec reviewed and approved before Task 2 starts.
**Dependencies:** None. **Files:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-core.md`.
**Scope:** M (a spec, not code).

### Task 2: Build the shared staleness-key function
**Description:** `tools/seedsmith/seedsmith/pipeline/staleness.py` (new) — one function, computing
the hash Task 1's spec defines, generalizing `adapters/dungeon/provenance.py`'s own
`DungeonProvenance`/`stale_ids` shape into a domain-agnostic form.
**Acceptance:**
- [ ] Given a real passive-tree brief + the real committed `PROMPT_VERSION` + a model id, produces a
      stable hash; changing any one input changes the hash
- [ ] A record stamped with an OLD hash is correctly identified as stale once any input changes
**Verification:** `python -m pytest tools/seedsmith/tests/pipeline/test_staleness.py -q` — new
tests, a real fixture built from passive-tree's own real brief/schema, not synthetic.
**Dependencies:** Task 1. **Files:** `tools/seedsmith/seedsmith/pipeline/staleness.py` (new),
matching test file (new). **Scope:** S.

### Task 3: Build the generalized missing-field metric registry
**Description:** Extend `metrics/quality.py` (or a new `metrics/content_completeness.py`) so a
domain registers its own "what counts as missing" predicate instead of `FlavourMissing`'s own
hardcoded `FLAVOR_EXPECTED_KINDS` frozenset. `FlavourMissing` itself becomes the FIRST registered
predicate (items), proving the generalization is additive, not a rewrite.
**Acceptance:**
- [ ] A new domain registers its own missing-field predicate in one call, no edit to the shared
      module's own internals
- [ ] `FlavourMissing`'s own existing behavior (items-only, `gates=False`) is byte-identical after
      the refactor — same real corpus, same findings
**Verification:** `python -m pytest tools/seedsmith/tests/test_quality*.py -q` (existing suite,
must stay green) plus new registry tests.
**Dependencies:** Task 1. **Files:** `tools/seedsmith/seedsmith/metrics/quality.py` (or new
`content_completeness.py`), `report/cli.py`'s `build_registry()`. **Scope:** M.

### Task 4: Build the generalized backfill loop (missing-only automatic, plus a manual overwrite)
**Description:** Build the domain-agnostic loop: read a ledger, and on a normal resumed run,
regenerate ONLY records with no existing entry at all — generalizing passive-tree's own
`run_language_stage` resumability shape (`adapters/trees/nodegen/run.py:721`), real and
live-proven, safe to make automatic (nothing to lose). Records that already have content are
never touched by this default path, regardless of their staleness key (Task 2 still computes it,
but only for the Task 3 reporting metric). Add a separate `--overwrite` parameter that, when
passed, regenerates every record it's pointed at unconditionally (missing AND existing), for a
person to invoke by hand — mirroring `generate_commander_effects.py`'s own `--stale` precedent's
caution by keeping any touch of existing content an explicit, human-invoked action rather than an
automatic one.
**Acceptance:**
- [ ] Run against a real, mostly-complete passive-tree ledger fixture with no `--overwrite`: only
      the genuinely missing entries regenerate; already-existing entries are a no-op (zero model
      calls), even when their staleness key has changed (simulating a prompt-version bump)
- [ ] The same fixture's stale-by-hash records show up in Task 3's own reporting metric as stale,
      proving detection still works even though it doesn't trigger regeneration
- [ ] Running with `--overwrite` against the same fixture regenerates every targeted record
      (missing and existing alike), proving the manual path is a real, working escape hatch
**Verification:** `python -m pytest tools/seedsmith/tests/pipeline/test_backfill_loop.py -q` — new
tests, against a REAL passive-tree fixture per the plan's own Checkpoint 0 requirement.
**Dependencies:** Tasks 1, 2, 3. **Files:** `tools/seedsmith/seedsmith/pipeline/backfill.py` (new).
**Scope:** M.

### Task 4b: Build the bidirectional language-contamination check
**Description:** Fix and generalize `workflow/validators/language.py:26`'s `language_consistency`
— its current guard clause (`if not motifs or not any(_CJK.search(m) for m in motifs): return []`)
only fires when the SUBJECT'S OWN motifs are CJK, so it cannot catch the real, already-found
dungeon defect (English motifs, CJK-contaminated output). Add the reverse check: flag mixed CJK/
Latin prose in generated output regardless of which language the input motifs used.
**Acceptance:**
- [ ] A real fixture reproducing the actual dungeon defect shape (English motifs, an output field
      containing a CJK fragment) is caught by the fixed validator — the existing CJK-motif direction
      (`commander_effect.py`'s own real historical incident) still passes unchanged
- [ ] The fixed validator is registered in `core`'s own missing-field/quality check family (Task 3's
      registry), not left as a demons/passive-tree-only import
**Verification:** `python -m pytest tools/seedsmith/tests/workflow/validators/test_language.py -q`
(existing suite extended, must stay green) plus the new reverse-direction fixture.
**Dependencies:** Task 1. **Files:** `tools/seedsmith/seedsmith/workflow/validators/language.py`.
**Scope:** S.

### Checkpoint 0
- [ ] Tasks 1-4b all green, tested against REAL passive-tree data, not synthetic fixtures
- [ ] `content-completeness-core`'s own spec and code are stable enough that Phases 1-5 can start
      in parallel without expecting `core` itself to change under them
- [ ] The automatic-backfill contract (missing-only automatic; existing content only via manual
      `--overwrite`) is built and proven by Task 4's own tests, not still open when domain
      adoption starts

---

## Phase 1 — `content-completeness-items`

### Task 5: Write `content-completeness-items`'s own module spec
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-items.md`.
Items already has the most infrastructure (`pipeline/run_ledger.py`, `flavorKey`,
`Quality/FlavourMissing`) — this spec is mostly a MIGRATION plan onto `core`'s shape, not new
design. Must name exactly which existing item-adapter files change.
**Acceptance:**
- [ ] Names every real call site of `pipeline/run_ledger.py` and `FlavourMissing` that migrates
- [ ] States what, if anything, `flavorKey` needs to change to become `core`'s own i18n-ready key
      shape (likely: nothing, since it already mints a stable key — the spec must confirm this
      explicitly, not assume it)
**Verification:** spec reviewed and approved.
**Dependencies:** Checkpoint 0. **Scope:** S.

### Task 6: Migrate items onto the shared engine
**Description:** Wire `content-completeness-core`'s staleness key + registry + backfill loop into
items' own existing generators.
**Acceptance:**
- [ ] `FlavourMissing` is now a registered predicate (Task 3's shape), not a bespoke class
- [ ] A resumed items generation run uses the shared backfill loop; existing behavior unchanged on
      the real committed items corpus (same findings, same no-op-when-healthy contract)
**Verification:** full items-adapter test suite green; a real dry run against the committed items
corpus shows zero unexpected regenerations.
**Dependencies:** Task 5. **Files:** `tools/seedsmith/seedsmith/adapters/items/*` (the real call
sites Task 5 named). **Scope:** M.

---

## Phase 2 — `content-completeness-actions`

### Task 7: Write `content-completeness-actions`'s own module spec
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-actions.md`.
Actions has NOTHING today (`adapters/actions/load.py:31,59-72`'s own `_manifest.json`/`_rounds/`
dispositions are not a ledger; zero `_provenance` in `data/seed/actions/committed-round-1.json`).
This spec designs the ledger/provenance/metric from scratch, on `core`'s shape — the real test of
whether `core` generalizes to a domain that never had its own bespoke version to migrate from.
**Acceptance:**
- [ ] Names what "missing" means for an action's own real schema (which field(s) are the
      description/flavor-equivalent content this program cares about)
- [ ] Names how the existing `_manifest.json`/`_rounds/` mechanism either gets replaced by or
      coexists with the new shared ledger
**Verification:** spec reviewed and approved.
**Dependencies:** Checkpoint 0. **Scope:** M (the design work is real; more than items' own migration).

### Task 8: Build actions' own completeness adoption
**Description:** Implement Task 7's spec — a real ledger, `_provenance` stamping, and a registered
missing-field predicate for actions, using `core`'s shared engine throughout.
**Acceptance:**
- [ ] The real committed action corpus (`data/seed/actions/committed-round-1.json` and siblings)
      gains real `_provenance` on a fresh generation pass
- [ ] The new missing-field metric reports real findings (or a real clean pass) against that corpus
**Verification:** new action-adapter tests green; full seedsmith suite shows no new failures beyond
the already-documented pre-existing cluster.
**Dependencies:** Task 7. **Files:** `tools/seedsmith/seedsmith/adapters/actions/*`. **Scope:** M.

---

## Phase 3 — `content-completeness-demons`

### Task 9: Write `content-completeness-demons`'s own module spec
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-demons.md`.
Demons already write real `_provenance` (`data/seed/demons/species/plant/aerial-flora.json:8-18`)
but have no missing-field metric at all. This spec is narrower than actions' — provenance exists,
only the metric + backfill wiring is new.
**Acceptance:**
- [ ] Names exactly which demon-species field(s) count as "description/flavor" for this program's
      own purpose (distinct from the anchor classification fields `metrics/demon_coverage.py`/
      `demon_roster.py` already check)
**Verification:** spec reviewed and approved.
**Dependencies:** Checkpoint 0. **Scope:** S.

### Task 10: Build demons' own completeness adoption
**Description:** Register the missing-field predicate; wire the shared backfill loop to demons' own
existing `_provenance`-stamped content.
**Acceptance:**
- [ ] The new metric runs against the real 904-species committed corpus, reporting real findings
- [ ] A resumed demon-species generation run backfills only genuinely missing/stale entries
**Verification:** demon-adapter test suite green; a real dry run against the committed corpus.
**Dependencies:** Task 9. **Files:** `tools/seedsmith/seedsmith/adapters/demons/*`. **Scope:** M.

---

## Phase 4 — `content-completeness-dungeon`

### Task 11: Write `content-completeness-dungeon`'s own module spec
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-dungeon.md`.
`adapters/dungeon/provenance.py`'s own `DungeonProvenance`/`stale_ids` (lines 12, 50) are real,
well-shaped CODE, confirmed DEAD (zero production callers, only imported by its own test) — this
spec is "wire up code that was written and never connected," not new design. The
language-contamination check is Task 4b's own fix (`core`'s registry), reused here, never
re-invented dungeon-specific.
**Acceptance:**
- [ ] Names why `data/seed/dungeon/events/*.json` never received a `_provenance` stamp despite the
      code existing (the real wiring gap, confirmed, not assumed)
- [ ] Confirms Task 4b's own fixed, bidirectional `language_consistency` is what gets registered
      for this domain — this task does not re-derive a language check
**Verification:** spec reviewed and approved.
**Dependencies:** Checkpoint 0. **Scope:** S.

### Task 12: Build dungeon's own completeness adoption, and fix the live defect
**Description:** Wire `DungeonProvenance` to actually stamp committed content; register Task 4b's
own fixed missing-field/contamination predicate for this domain.
**Acceptance:**
- [ ] `data/seed/dungeon/events/event.bargain-demon.allpeater-001.json` — the entry with real,
      already-found untranslated Chinese fragments in its `flavor` field — is regenerated clean
      through the new pipeline, confirmed by reading the real file content after the fix, not
      inferred
- [ ] Every other dungeon event carries a real `_provenance` stamp after a fresh generation pass
**Verification:** dungeon-adapter test suite green; the specific fixed file's own content directly
inspected and confirmed free of non-English fragments.
**Dependencies:** Task 11. **Files:** `tools/seedsmith/seedsmith/adapters/dungeon/*`. **Scope:** M.

---

## Phase 5 — `content-completeness-passive-tree`

### Task 13: Write `content-completeness-passive-tree`'s own module spec
**Description:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-passive-tree.md`.
Passive-tree already has the most mature ledger (`run_language_stage`); this module's real work is
the WIRING GAP the ideal doc already found in full: `NodeRecord.cs:37-52` has no name/flavor field;
`PassiveTreeDtos.cs`'s `TreeNodeSummaryDto` has none either; the FE's `TreeNodeSummary`
(`web/fusion-rpg-web/src/lib/bus/types.ts:516-521`) has none, and `PathLattice.tsx:319`/
`TraitDetail.tsx:133` render the raw node id as a fallback.
**Acceptance:**
- [ ] Names the exact field additions to `NodeRecord`, the DTO, and the FE type — a straight-through
      chain, no new generation needed (the English text already exists in the seed corpus)
- [ ] States whether `name`/`flavor` moves through `TreeBinder`'s own catalog assembly
      (`ReportWriter.cs`, rebuilt this session for the mechanical fields) as an ADDITIVE field, or a
      separate side-channel — and why
**Verification:** spec reviewed and approved.
**Dependencies:** Checkpoint 0. **Scope:** S (the design is narrow — the ideal doc already did the
hard discovery work; this is confirming the exact plumbing).

### Task 14: Carry node name/flavor from seed → catalog → server DTO
**Description:** Extend `NodeRecord.cs`, `PassiveTreeCatalogLoader.cs`'s `LoadNode`, `TreeBinder`'s
`ReportWriter.cs`/`PlanReader.cs`, and `PassiveTreeDtos.cs`/`PassiveTreeEndpoints.cs`'s
`ProjectState` to carry `name`/`flavor` all the way from the already-committed seed document to the
`GET /api/passive-tree/{playerId}` response.
**Acceptance:**
- [ ] A real committed node (e.g. from `ferocity.json`, which already has real `name`/`flavor`)
      round-trips through the full chain and appears in a real `GET` response
- [ ] Existing `--check` byte-identity and the real live-boot proof (this session's own H9 evidence)
      both still pass after the field addition
**Verification:** `dotnet test tests/FusionRpg.TreeBinder.Tests`, `dotnet test
tests/FusionRpg.Core.Tests --filter FullyQualifiedName~PassiveTree`, a real `TreeBinder` re-run +
live-boot proof against the isolated dev server (same safe pattern this session established:
`FUSIONRPG_URLS`, never touching the owner's own port).
**Dependencies:** Task 13. **Files:** `src/FusionRpg.Core/PassiveTree/Catalog/NodeRecord.cs`,
`PassiveTreeCatalogLoader.cs`, `tools/TreeBinder/ReportWriter.cs`, `PlanReader.cs`,
`src/FusionRpg.Contracts/PassiveTreeDtos.cs`, `src/FusionRpg.Server/PassiveTreeEndpoints.cs`.
**Scope:** M.

### Task 15: Render the real name/flavor in the web FE
**Description:** Extend `TreeNodeSummary` (`web/fusion-rpg-web/src/lib/bus/types.ts:516-521`) with
the new fields; update `PathLattice.tsx:319` and `TraitDetail.tsx:133` to render `name`/`flavor`
instead of the raw `nodeId` fallback, keeping the id-fallback path for any node that genuinely has
none yet (a not-yet-generated node, matching this program's own "never fabricate content" rule).
**Acceptance:**
- [ ] A real node with committed content renders its real name in the lattice UI, not its id
- [ ] A node with no content yet still renders SOMETHING legible (the existing id fallback), never a
      blank cell
**Verification:** `npx vitest run` (existing FE suite, must stay green); a real Playwright pass
against the same real committed corpus this program's own I1/I4/I6 checks already use.
**Dependencies:** Task 14. **Files:** `web/fusion-rpg-web/src/lib/bus/types.ts`,
`PathLattice.tsx`, `TraitDetail.tsx`, and their contract adapter (`contract/passivesLattice.ts` or
equivalent). **Scope:** S.

### Checkpoint 5b
- [ ] Every domain's own missing-field metric runs clean or reports real, named gaps against its
      real committed corpus
- [ ] A resumed run on each domain is a no-op on already-good content (proven, not assumed)
- [ ] The dungeon Chinese-fragment defect is confirmed fixed by direct file inspection
- [ ] A real node's name/flavor renders in the live web UI, sourced from the real catalog

---

## Phase 6 — `passive-tree-identity-content`

### Task 16: Write `passive-tree-identity-content`'s own module spec
**Description:** `docs/architecture/seedsmith-content-standard/spec-passive-tree-identity-content.md`.
A genuinely NEW generation stage — tree-level name/description for primary/elemental/status
categories, which have NONE today (confirmed: `tree_reading` always equals the tree id verbatim at
every real call site, `report/cli.py:1176`, `species/generate_tree.py:120`). Species' own
`codexSummary` (`generate_codex.py`) is the closest real precedent to mirror, not copy verbatim —
species answers "what does building into this reward," a tree name/description answers something
broader ("what is this tree").
**Acceptance:**
- [ ] Defines the new schema (name + description fields), a real brief, and where the content is
      stored (extending `TreeRecord`/the plan document, per Task 13/14's own new field convention)
- [ ] Defines a review pass for this new content — matches this program's own "generation quality
      is a real, separate concern from completeness" split already established by J2/J3
**Verification:** spec reviewed and approved.
**Dependencies:** Checkpoint 5b. **Scope:** M.

### Task 17: Build the tree-identity generation stage
**Description:** Implement Task 16's spec — a new brief/schema/generation function, mirroring
`generate_codex.py`'s own real, already-proven shape.
**Acceptance:**
- [ ] A real proof-of-concept run against the live local model, for at least 3 real trees across the
      3 currently-empty categories (primary, elemental, status), produces real, coherent name/
      description content — proven the same way every other real generation stage in this program
      was proven this session (a live model call, not a stub)
**Verification:** new tests green; the real PoC run's own output inspected directly.
**Dependencies:** Task 16. **Files:** `tools/seedsmith/seedsmith/adapters/trees/plan/prompts.py` (or
new), schema module, generation function. **Scope:** M.

### Task 18: Wire tree identity content to the server/FE
**Description:** Carry the new tree-level name/description through the same chain Task 14/15 already
built for nodes — `TreeRecord` → DTO → FE.
**Acceptance:**
- [ ] A real generated tree's name/description renders in the web UI (e.g. the passives tab header),
      not the raw `treeId`
**Verification:** `dotnet test`, `npx vitest run`, a real live-boot proof.
**Dependencies:** Task 17. **Files:** same layer as Task 14/15, extended for tree-level (not
node-level) content. **Scope:** S.

### Checkpoint 6 — program complete
- [ ] All 42 generic trees show a real, generated name and description in the live web UI
- [ ] Every generated node across every domain this program touched carries real, current content —
      or a real, named, reported gap, never a silent placeholder
- [ ] A resumed run across every domain backfills only what's genuinely missing, fully
      automatically, with zero human approval step (per the owner's own decision); existing
      content is untouched by that automatic run, and a manual `--overwrite` parameter proves it
      can force a full regeneration when a person actually wants one
- [ ] The storage shape is confirmed i18n-ready by inspection (a stable key exists per record;
      adding a locale subtree later requires no schema change) — no locale has actually been added,
      by design
