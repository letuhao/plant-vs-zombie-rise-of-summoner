# Implementation plan — `seedsmith-content-standard`

**Program:** `seedsmith-content-standard`. Capability map:
[docs/architecture/seedsmith-content-standard-map.md](../docs/architecture/seedsmith-content-standard-map.md)
(7 modules). Idea phase:
[docs/architecture/seedsmith-content-standard-ideal.md](../docs/architecture/seedsmith-content-standard-ideal.md).
Task list: [seedsmith-content-standard-todo.md](seedsmith-content-standard-todo.md).

**Status: all 18 tasks across all 7 modules built and evidenced as of 2026-09-08** — see
`seedsmith-content-standard-todo.md` for the full per-task evidence (each phase's own evidence log
where the work was delegated: Phases 1-4; inline in the todo for Phases 0/5/6, built directly in
the orchestrating session). Below is kept as the plan's own historical record of how the work was
sequenced, not a forward-looking TODO. Per `spec-driven-development`'s own gated flow, each
module's own phase opened with writing that module's own spec before any code — this is not
busywork: `content-completeness-core`'s own spec is where the ONE staleness-key shape, the ONE
metric-registration shape, and the automatic-backfill trigger got pinned down precisely enough for
five domains to build against without re-deriving them five times — exactly the failure this whole
program existed to close.

## Owner decisions this plan is built on (2026-09-08)

- **English-content completeness only, for now.** Detect a missing field deterministically,
  regenerate it automatically on resume, no human approval step. **Resolved 2026-09-08**: this
  automatic path is missing-content only — a resumed run never touches content that already
  exists, even if it's detected as stale. Regenerating existing content is manual only, via an
  explicit `--force` parameter that regenerates everything it's pointed at (not a
  selectively-diffed stale subset). Staleness is still detected and reported, never auto-acted on.
- **Translation is explicitly deferred** to a different, later pipeline. This program's own job is
  to make the storage shape i18n-ready (a stable per-record key, room for a locale subtree later),
  never to translate anything.
- **All five domains move together**, not staged one at a time — this plan's own phases 1-5 are
  written to run in PARALLEL once `content-completeness-core` lands, not sequentially.
- **Every new metric stays `gates=False`** (report-only) — matches `Quality/FlavourMissing`'s own
  current status. Promoting any one to a hard CI gate is a separate, later, per-domain decision
  this plan does not make.

## Architecture decisions

- **One shared staleness-key SHAPE, generalized from the best existing precedent for the key
  itself** (`adapters/dungeon/provenance.py`'s own `DungeonProvenance`/`stale_ids`, `briefHash +
  promptVersions + registryVersions + motifSubsetHash` — real, well-designed code, though confirmed
  dead: it has zero production callers, only a test importer) — never re-derived per domain.
- **One shared missing-field metric family, generalized from `Quality/FlavourMissing`**
  (`metrics/quality.py:24`) — its own per-domain narrowing (`FLAVOR_EXPECTED_KINDS`, a hardcoded
  frozenset) becomes a registry a domain adopts by adding an entry, not by editing the shared
  module's own internals.
- **Automatic backfill is missing-content only — resolved 2026-09-08.** It reuses passive-tree's
  own `run_language_stage` resumability shape (`adapters/trees/nodegen/run.py:721`) — real and
  live-proven, safe to make fully automatic (nothing to lose). **Existing content is never
  touched by an automatic run, regardless of staleness.** The one real precedent for
  stale-regeneration anywhere in this codebase (`generate_commander_effects.py`'s `--stale` path)
  is deliberately opt-in, for a stated reason (destroying already-good content); this program
  keeps that same caution rather than generalizing it into an automatic path. Regenerating
  existing content (stale or not) happens only through an explicit, human-invoked `--force`
  parameter that regenerates everything it's pointed at — not a selectively-diffed stale subset.
  The staleness-key machinery is still built (Task 2) and still reported as a `gates=False`
  metric ("N records are stale, re-run with `--force` to refresh"), it just never triggers
  regeneration on its own.
- **A general, bidirectional language-contamination check**, generalizing (and fixing) the real,
  already-proven `language_consistency` validator (`workflow/validators/language.py:26`, wired
  into creatures + passive-tree today) — its own current logic only catches CJK-motif-in, mixed-output
  cases; the real dungeon defect ran the other direction and would not be caught by wiring the
  existing validator in unchanged.

## Gates — there are none in this plan

Checked against `planning-and-task-breakdown`'s own gates-vs-checkpoints test: nothing in this
program is irreversible. Generated content is data, always regenerable; ledger/metric/schema
changes are code, provable by tests before anything real runs. Every phase below is followed by a
**checkpoint** (verifies work already done), never a gate (blocks starting on an external
decision).

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| ~~Automatic, no-opt-in regeneration of STALE (not merely missing) content destroys already-good or hand-corrected work~~ — **resolved 2026-09-08**: owner decided automatic backfill is missing-content only; existing content is never touched by an automatic run, regardless of staleness. Regeneration of existing content requires an explicit, manually-invoked `--force` parameter | N/A — closed | Task 1's contract states this plainly: automatic path = missing only; `--force` = manual, regenerates everything it's pointed at, not a diffed stale subset |
| `content-completeness-core`'s own shape is wrong in a way that only shows up once a real domain adopts it | High — 5 domains would inherit the same defect | Prove `core` against passive-tree's own real, already-generated content FIRST inside its own phase (a real fixture, not synthetic), before any domain module starts, even though domain adoption itself is parallel |
| Automatic backfill regenerates a field that was deliberately left blank | Medium — compounds with the row above; named in the ideal doc as a reasoned, unverified failure mode | Each domain's own missing-field metric definition must state, in its own spec, what "missing" means for THAT domain's own schema (e.g. a `null` `flavor` vs. an intentionally-empty string) — not inherited blindly from `FlavourMissing`'s own item-shaped definition |
| Actions has zero existing infrastructure — its own module is the least like the other four | Medium | Its own spec explicitly designs the ledger/provenance from scratch against `core`'s shape, never copies an existing bespoke implementation that doesn't exist for this domain |
| The already-found live defect (untranslated Chinese fragments in a committed dungeon event) gets treated as "in scope, will get fixed automatically" and then silently doesn't, because nothing targets it specifically | Low-medium | `content-completeness-dungeon`'s own acceptance criteria name this exact entry as a required, explicit verification target, not left to an automatic pass to happen to catch |
| **A real, proven, general language-contamination validator already exists (`workflow/validators/language.py`'s `language_consistency`) but only checks ONE direction** (CJK motifs → mixed output) — the real dungeon defect ran the OTHER direction (English motifs, CJK-contaminated output), which this validator's own current logic cannot catch even once wired in | Medium — wiring the validator in as-is would give false confidence without fixing the real defect class | `core`'s own spec must fix the directional asymmetry as part of generalizing this validator, not just wire the existing one in unchanged |

## Open questions

None block STARTING Phase 0's own work — the automatic-vs-manual question (the sharpest one this
audit found) is now resolved, see Architecture decisions and Owner decisions above. What remains
is spec-level detail Task 1 pins down, not an owner decision:

- Exactly which staleness-key inputs are hashed for the FIRST domain to adopt `core` (brief text,
  schema version, model id — likely all three, per the dungeon precedent's own key SHAPE, even
  though that module is dead code) — resolved in `core`'s own spec, not here.
- How `core`'s own missing-field/language-contamination check relates to `language_consistency`
  (real, proven, wired into 2 of 5 domains, but directionally incomplete) — generalize and fix it,
  or build a new, broader check alongside it? Resolved in `core`'s own spec, not here.

## Phases

### Phase 0 — `content-completeness-core`

The shared engine. Nothing else starts until this phase's own checkpoint passes.

Tasks 1-4. **Checkpoint 0: the shared staleness-key function, the generalized missing-field metric
registry, and the generalized resumable-backfill loop all pass their own tests against a REAL
passive-tree fixture (not synthetic) before any domain module begins.**

### Phase 1 — `content-completeness-items`

Tasks 5-6. Depends on Phase 0.

### Phase 2 — `content-completeness-actions`

Tasks 7-8. Depends on Phase 0. Runs in parallel with Phase 1/3/4/5.

### Phase 3 — `content-completeness-creatures`

Tasks 9-10. Depends on Phase 0. Runs in parallel with Phase 1/2/4/5.

### Phase 4 — `content-completeness-dungeon`

Tasks 11-12. Depends on Phase 0. Runs in parallel with Phase 1/2/3/5. Its own task 12 explicitly
targets the already-found live Chinese-fragment defect as a real, named verification case.

### Phase 5 — `content-completeness-passive-tree`

Tasks 13-15. Depends on Phase 0. Runs in parallel with Phase 1/2/3/4. This is the WIRING-GAP
closure the ideal doc already found in full detail: `NodeRecord` has no name/flavor field, the
server DTO has no name/flavor field, the FE renders the raw node id — three real, sequential fixes,
each already scoped with file:line in the ideal doc.

**Checkpoint 5b (after Phases 1-5 all land): every domain's own missing-field metric runs clean (or
reports real, named gaps) against its real committed corpus; a resumed run on each domain is a
no-op on already-good content; the live Chinese-fragment defect is confirmed fixed.**

### Phase 6 — `passive-tree-identity-content`

Tasks 16-18. Depends on Phase 5 (needs the wiring in place so new content has somewhere to go).
Genuinely new content generation (tree-level name/description for primary/elemental/status
categories), not a retrofit — its own spec, brief, schema and review pass, mirroring species'
already-real `codexSummary` shape.

**Checkpoint 6 (program complete) — ✅ CLOSED 2026-09-08, one honest exception:** 41 of 42 generic
trees have a real, generated name/description (`bond`'s own name vote genuinely split twice, left
unresolved rather than forced); every generated node across every domain this program touched
carries real content or a real, named, reported gap. One real, out-of-scope, pre-existing defect
(`AffixComposer`'s missing channel-pool handling) blocks a full live-server rendering proof — the
seed→catalog→DTO→FE chain is proven with real data at every layer short of that final assembly.
Full detail in `seedsmith-content-standard-todo.md`'s own Checkpoint 6.

## Verification standard

Every task: the touched Python module's own test suite green (`python -m pytest
tools/seedsmith/tests/... -q`), full `python -m pytest tools/seedsmith/tests -q` re-run before a
phase's own checkpoint (confirming the same pre-existing, already-documented unrelated failure
count — never treat a NEW failure as pre-existing without checking `git status` first, matching
this program's own established discipline). Any C#-touching task (Phase 5/6) additionally: `dotnet
build` clean on the touched project, the relevant `dotnet test` project green. No task introduces a
numeric magnitude or a bare literal on a balance surface — this program touches text content and
metadata only.
