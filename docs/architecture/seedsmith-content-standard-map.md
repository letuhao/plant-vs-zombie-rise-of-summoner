# Capability map: `seedsmith-content-standard`

**Status:** built 2026-09-08 — all 7 modules' specs written, all 18 tasks implemented and
evidenced (see `tasks/seedsmith-content-standard-todo.md`). Idea phase source:
[seedsmith-content-standard-ideal.md](seedsmith-content-standard-ideal.md) (supersedes
[passive-tree-i18n-ideal.md](passive-tree-i18n-ideal.md)).

## Scope, per the owner's own decisions this pass

- **English-content completeness is in scope now, for every domain, built together.** Detect a
  missing field deterministically; regenerate it via the LLM pipeline **automatically on
  resume** — no human approval gate (the owner's own explicit choice, overriding
  `spec-pipeline.md` §6's open-loop review-queue default). **Resolved 2026-09-08**: this automatic
  path is missing-content only — a resumed run never touches content that already exists, even if
  a staleness key shows it's out of date. Regenerating existing content (stale or not) is manual
  only, via an explicit `--force` parameter that regenerates everything it's pointed at.
  Staleness is still detected and reported (a `gates=False` metric), just never auto-acted on.
- **The storage shape must be i18n-ready** (a locale subtree can be added later without a schema
  break) — but **the actual translation pipeline is explicitly deferred**, built later, by a
  *different* pipeline this program does not build. English is the only content this program
  generates.
- **Gates stay report-only** (`gates=False`) for every new metric this program adds, matching
  `Quality/FlavourMissing`'s own current status — promoting any one to CI-enforced is a separate,
  later, per-domain decision.
- **All five domains move together**, not staged one at a time.

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `content-completeness-core` | The shared engine every domain plugs into: one staleness-key shape (brief/prompt/schema/model version hash — the closest real precedent for the KEY's own shape, `DungeonProvenance`, is confirmed dead code, zero production callers), one generalized missing-field metric family (generalizing `Quality/FlavourMissing`'s per-domain narrowing into a registry entry instead of a hardcoded frozenset), one generalized resumable-backfill loop split into two real risk classes — audit-corrected 2026-09-08: of the 5 bespoke `stale_ids()` implementations found, only demons' own two (`anchor/emit.py`, `commander_effect.py`, both with real callers in `run/selectors.py`/`generate_commander_effects.py`) actually drive live regeneration; dungeon/items/structures' own copies are dead code. Demons' own real precedent is deliberately OPT-IN (`--stale` flag), never automatic, because automatic stale-regeneration risks destroying already-good content — **resolved**: this program's own "automatic on resume" covers only genuinely missing content; existing content (stale or not) is regenerated only via an explicit, manually-invoked `--force` parameter, matching the precedent's own caution — plus a bidirectional language-contamination check (fixing `language_consistency`'s own real, proven, but one-directional check), and the i18n-ready storage convention (English content + a stable per-record key, shaped so a `data/seed/<domain>/i18n/<locale>/` subtree can be added later with zero change to this shape) | — |
| `content-completeness-items` | Adopt the shared engine for items — the domain with the MOST existing partial infrastructure (`pipeline/run_ledger.py`, `flavorKey`, `Quality/FlavourMissing` already scoped here) | `content-completeness-core` |
| `content-completeness-actions` | Adopt for actions — the domain with NOTHING today (no ledger, no provenance, no missing-field metric); builds these from scratch on the shared engine | `content-completeness-core` |
| `content-completeness-demons` | Adopt for demon species — `_provenance` already real and written; this module adds the missing-field metric and wires automatic backfill to it | `content-completeness-core` |
| `content-completeness-dungeon` | Adopt for dungeon/events — `DungeonProvenance`/`stale_ids` already exist as CODE but no committed content carries the field; this module wires the writer, and its own first real backfill target is the already-found live defect (a committed event's `flavor` field carrying untranslated Chinese fragments) | `content-completeness-core` |
| `content-completeness-passive-tree` | Adopt for passive-tree — the domain with the MOST mature existing ledger (`run_language_stage`). This module's own real work is the WIRING GAP already found: carry `name`/`flavor` through `NodeRecord` → the server DTO → the web FE (`TreeNodeSummary`/`PathLattice.tsx`, which today renders the raw node id) | `content-completeness-core` |
| `passive-tree-identity-content` | **New content, not a retrofit**: build the tree-level name/description generation stage for the three categories that have none today (primary, elemental, status) — species already has the analog (`codexSummary`). Needs `content-completeness-passive-tree`'s own wiring to exist so this new content has a real path to the player | `content-completeness-passive-tree` |

Build order: `content-completeness-core` → `{items, actions, demons, dungeon, passive-tree}` (parallel) → `passive-tree-identity-content`.

## Why this split, not fewer or more modules

- **`core` is separated from every domain adoption** because it is the one piece that must be
  designed ONCE and then never re-derived — exactly `spec-pipeline.md` §1's own lesson ("effort
  belongs in the brief, the schema and the gate"), generalized: the staleness-key shape, the
  metric-registration shape, and the ledger shape are each a single design decision five domains
  currently answer differently. Building it inside any one domain's own module would re-litigate
  that decision under that domain's own naming, the exact anti-pattern this whole program exists
  to stop.
- **Each domain gets its own module** because each is independently testable (a domain's own
  completeness metric can go green without any other domain existing yet) and each has a
  genuinely different starting point (items: mostly there; actions: nothing; demons: provenance
  but no metric; dungeon: code but no data; passive-tree: the most mature ledger but the biggest
  wiring gap) — one shared "adopt everywhere" module would hide five different real amounts of
  work behind one checkbox.
- **`passive-tree-identity-content` is split from `content-completeness-passive-tree`** because it
  is not a retrofit — it is a brand NEW generation stage (a schema, a brief, a review pass) for
  content that has never existed for three of four tree categories. Folding it into the
  completeness module would conflate "make existing content reach the player" with "invent content
  that doesn't exist yet," which are different kinds of work with different risk profiles.

## What is explicitly OUT of this program's scope

- The translation/localization pipeline itself (deferred, per the owner's own decision — a
  DIFFERENT pipeline, later).
- Promoting any new metric to a hard CI gate (report-only for now, a later, separate decision).
- Any content-quality improvement beyond "the field exists and is current" — e.g. this program
  does not judge whether backfilled English content is as good as a human-reviewed first pass,
  only whether it exists and matches the current pipeline/schema version.
