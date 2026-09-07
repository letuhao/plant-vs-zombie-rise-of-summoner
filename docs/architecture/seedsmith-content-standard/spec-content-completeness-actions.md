# Seedsmith content-completeness — `actions`

**Status:** Built 2026-09-08 (Phase 2, Task 7/8). Module 3 of 7 in
[seedsmith-content-standard-map.md](../seedsmith-content-standard-map.md). Depends on
[spec-content-completeness-core.md](spec-content-completeness-core.md) (built, Phase 0).

Actions is the domain the map calls out as having **nothing today**: no ledger, no `_provenance`,
no missing-field metric — confirmed again here, directly against the code, before writing a line of
this spec (`load.py:31,59-72`'s `_manifest.json`/`_rounds/` dispositions are a corpus-loading
mechanism, not a content-completeness ledger; `data/seed/actions/committed-round-1.json` had zero
`_provenance` occurrences before this task). This is the real test of whether `core` generalizes to
a domain with nothing to migrate from, not just one to adopt.

---

## 1. What "missing" means for an action's own real schema

Read directly, not assumed, per this task's own brief:

- `data/seed/actions/committed-round-1.json`/`committed-round-2.json` (24 real committed
  `action-seed` rows, verified 2026-09-08) carry `name` (real, always-present English text) and
  `descriptionKey` (e.g. `"action.family.cactus.001.desc"` — a **minted identifier**, not content:
  `innate_picker/derive.py:395`'s `row.setdefault("descriptionKey", f"{row['id']}.desc")` mints it
  mechanically from the id, and nothing anywhere resolves it to text). **Actions did not have a
  `flavor`-equivalent field at all** — `kinds.py`'s own `ACTION_SEED_REQUIRED`/`ACTION_SEED_OPTIONAL`
  (measured directly, pre-this-task) name no such field.
- The text *did* already exist, transiently, upstream: `general_propose/prompts.py`'s own
  `GENERAL_ACTION_SCHEMA` has the model author a real `flavor` line for every A-P1/A-P2/A-P3
  candidate (one to two sentences, "evoking what the action feels like," the same shape items'
  `flavor` field uses). But `candidate_assembly/derive.py:9-23`'s own module docstring states,
  as a **deliberate, already-considered choice**: *"It never widens `kinds.py`'s
  `ACTION_SEED_REQUIRED`/`ACTION_SEED_OPTIONAL`"* — `flavor` informs the model's own choices during
  review, then is **dropped** before the row is committed, with an explicit forward pointer: *"A
  future spec that wants player-facing flavour text on the committed corpus (mirroring
  `items/kinds.py`'s own `flavor`/`flavorKey` split) is a real, separate, reviewed schema change to
  `kinds.py` — not something to smuggle in here."* **This spec is that reviewed change.**

**Decision:** add `description` to `kinds.py`'s `ACTION_SEED_OPTIONAL` — the AUTHORED English text,
mirroring items' `flavor`/`flavorKey` pair exactly: `descriptionKey` stays the stable, already-minted
i18n-ready identifier (untouched, still mechanically derived); `description` is the real text that
identifier will eventually resolve to, once a later, separate localization pipeline exists (out of
this whole program's scope, per the map's owner decisions). `CompletenessSpec`:

```python
CompletenessSpec(domain="actions", kinds=frozenset({"action-seed"}), field="description")
```

Default falsy-check, no `is_missing` override: unlike a field such as `scopeKey` (legitimately
`null` for a general-scope action), no accepted `action-seed` row has a legitimate reason to ship
with an intentionally-empty description — every one is meant to reach a player. `is_missing`'s
override exists precisely for the failure mode this domain does *not* have.

## 2. Coexistence with `_manifest.json`/`_rounds/` — a real architectural decision, not deferred

The two mechanisms answer **orthogonal questions** and neither can do the other's job:

| | `_manifest.json`/`_rounds/` (`load.py`) | The new `RunLedger` (`_runs/description-backfill.ledger.json`) |
|---|---|---|
| Question | Does this row belong in the committed corpus graph AT ALL? | Does this *already-committed* row have a description YET? |
| Scope | Pre-commit staging (`_rounds/round-N/survivors.json` → `committed-round-N.json`, A-S6's own promotion move) | Post-commit content completeness, per subject id |
| Keys on | The corpus's own id grammar (`action.*`/`reject.*`/`review.*`) | Committed action ids only — a strict subset the loader has already accepted |
| Failure mode it prevents | A `_rounds/` draft silently double-counted as committed content (the exact duplicate-id collision `_load_committed_corpus`'s own scratch-copy exists to avoid, `load.py:153-186`) | A committed row silently missing player-facing text forever, with no resumable way to detect or fill the gap |

**Verified, not assumed:** `_rounds/round-1/assembled.json` carries the SAME `kind: "action-seed"`
envelope shape and the SAME id grammar as `committed-round-1.json` (both real files read directly,
2026-09-08) — confirming `load.py`'s own reason for excluding `_rounds/` from the generic
`Corpus.load` walk is real, not theoretical: a naive `Corpus.load(data/seed/actions)` over the whole
tree raises `CorpusLoadError` on the first shared id. This is exactly why the backfill generator
(§4) reads through **`load_committed(actions_root)`**, never a bare `Corpus.load` — the same loader
`generate_innate_picker.py` already depends on for the identical reason.

**Decision:** they coexist, unchanged with respect to each other. `_manifest.json` gets one
additive row declaring `_runs/` excluded (matching `_rounds/`'s own disposition shape) so the
ledger has a real, declared home rather than tripping `_classify_prefixes`'s "undeclared-prefix"
finding. Nothing in `load.py`'s own six-step algorithm changes.

## 3. Provenance and staleness shape

`Provenance` (`pipeline/provenance.py`, unchanged) stamped per generated row:

- `pipeline`: `"seedsmith.adapters.actions.description_backfill"`
- `model`: the real model id the local endpoint reports (`google/gemma-4-26b-a4b-qat` for this
  task's own real run)
- `prompt_version`: `PROMPT_VERSION = "actions/description-backfill/1"`
  (`description_backfill/prompts.py`)
- `budget_version`: `0` — a structural placeholder, not a magnitude: actions carries no numeric
  budget concept the way items' tier-band budgets do, but `Provenance` is a shared dataclass with a
  required field every domain must supply something for.
- `finding`: `"Content/FieldMissing:actions:action-seed"` — the real metric+subject pair
  `ContentFieldMissing` would report for this row (its own subject shape is a per-kind aggregate,
  `metrics/content_completeness.py:92`, not per-row — this is the most specific truthful value
  available).
- `generated_utc`: real UTC ISO timestamp, clock injected (`now_fn` parameter,
  `generate_action_descriptions.py`) for testability, matching `pipeline/provenance.py`'s own
  stated discipline.

Staleness key (`pipeline/staleness.py`, unchanged) is stored as an EXTRA field inside `_provenance`
(`"stalenessKey"`, alongside `Provenance`'s own five fields) — the same shape
`tests/pipeline/test_backfill_loop.py`'s own real ferocity fixture test already establishes as the
convention (`_provenance.stalenessKey`, compared against a caller-computed
`_stalenessCurrentKey` at check time). `schema_version` for the key is
`SCHEMA_VERSION = "action-seed/1"` (`kinds.py`'s own schema version this generator was built
against); `brief_hash` hashes `description_backfill/prompts.py`'s own `build_brief(entry.data)`
rendering. **`Content/FieldStale` end-to-end wiring into `seedsmith check --adapter actions` is NOT
built by this task** — the `stalenessKey` is stamped and correct, but no caller yet computes
`_stalenessCurrentKey` for a live actions check the way `_cmd_check_family` does for passive-tree.
Named here as a real, explicit gap rather than silently claimed done: Task 8's own acceptance only
requires `_provenance` + the missing-field metric, not staleness reporting end to end.

## 4. Project structure

```
tools/seedsmith/seedsmith/adapters/actions/kinds.py                    (edited) — `description` added
tools/seedsmith/seedsmith/adapters/actions/description_backfill/
    __init__.py     — ACTIONS_COMPLETENESS_SPEC, registered from report/cli.py's build_registry()
    prompts.py       — PROMPT_VERSION, SCHEMA_VERSION, SYSTEM_PROMPT, DESCRIPTION_SCHEMA, build_brief
    derive.py         — pure: stamp_description, group_ids_by_path, apply_updates_to_doc, canonical_dump
tools/seedsmith/seedsmith/adapters/actions/generate_action_descriptions.py  (new) — the entrypoint;
    owns the one model call and every disk read/write, matching every `generate_*.py` sibling
tools/seedsmith/seedsmith/report/cli.py                                (edited) — one line,
    `register_completeness(ACTIONS_COMPLETENESS_SPEC)`, mirroring items' own call shape
data/seed/actions/_manifest.json                                       (edited) — `_runs/` excluded
data/seed/actions/_runs/description-backfill.ledger.json               (new, real) — the RunLedger
tools/seedsmith/tests/test_actions_description_completeness.py         (new)
```

## 5. Commands

```powershell
# Plan only, zero model calls:
python -m seedsmith.adapters.actions.generate_action_descriptions --dry-run

# Automatic, missing-only (the resumable default):
python -m seedsmith.adapters.actions.generate_action_descriptions

# Manual force, scoped to named ids:
python -m seedsmith.adapters.actions.generate_action_descriptions --force --only action.general.0001

# The registered metric, reachable through the real CLI once an actions corpus check exists:
# (report/cli.py's `cmd_check --adapter actions <root>` today calls a bare `Corpus.load`, which
# cannot load `data/seed/actions/` directly per §2's own duplicate-id finding — reaching
# `Content/FieldMissing` for actions from the CLI today means building a `--family`-style
# actions-specific branch the way `_cmd_check_family` does for PassiveTree; not built by this task,
# named as a real follow-up.)
```

## 6. Testing strategy and boundaries

Proven against the REAL 24-row committed corpus, not a synthetic fixture (this task's own
acceptance criterion): a real generation pass via the real local endpoint
(`http://localhost:1234/v1/chat/completions`, `google/gemma-4-26b-a4b-qat`) stamped `description` +
`_provenance` on all 24 committed rows; `ContentFieldMissing`/`ContentLanguageContamination` report
a clean pass against the post-generation real corpus; a synthetic two-entry fixture proves the
detector actually fires on a real gap (not merely "the real corpus happens to be clean"). Exact
commands and output: `tasks/seedsmith-content-standard-phase2-actions-evidence.md`.

**Always:** keep `Content/FieldMissing`/`Content/FieldStale`/`Content/LanguageContamination` at
`gates=False` (inherited from `core`, unchanged); keep the automatic path missing-only (§3's
`plan_missing`, never touching an already-described row); run the full seedsmith suite before
calling this done.

**Never:** widen `_load_committed_corpus`'s own `_rounds/` exclusion or its duplicate-id behavior;
touch `_meta.corpusHash` on a rewritten committed file (verified 2026-09-08 that it already does not
equal a self-referential hash of that file's own entries even before this task's change — a
pre-existing staleness this task does not attempt to fix, since correctly recomputing it needs
`generate_innate_picker.py`'s own full cross-file `all_accepted` input, which this task's module
does not own).

**Ask first:** wiring `Content/FieldStale` end to end for actions (§3's named gap) or building a
`--family actions`-style CLI branch (§5's named gap) — both are real, scoped follow-ups, not
silently included here.
