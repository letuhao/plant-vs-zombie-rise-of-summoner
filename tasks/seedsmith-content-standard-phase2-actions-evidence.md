# Phase 2 (`content-completeness-actions`) — Task 7/8 evidence log

Written by the session that built Phase 2, 2026-09-08. Mirrors the todo's own Task 7/8 acceptance
checkboxes with real evidence — exact commands, exact output, file:line citations. Does not edit
`tasks/seedsmith-content-standard-todo.md`/`-plan.md` (other sessions are working Phases 1/3/4 in
parallel on those files concurrently; confirmed via `git status` during this session — see §5).

Spec: [docs/architecture/seedsmith-content-standard/spec-content-completeness-actions.md](../docs/architecture/seedsmith-content-standard/spec-content-completeness-actions.md)

---

## Task 7 — spec

- [x] **Names what "missing" means for an action's own real schema.** Spec §1: `description`
  (new, added to `kinds.py`'s `ACTION_SEED_OPTIONAL`), mirroring items' `flavor`/`flavorKey` split.
  Confirmed by direct read, 2026-09-08, that actions had no such field before this task —
  `descriptionKey` is a minted identifier only (`innate_picker/derive.py:395`), and the real
  `flavor` text A-P1/A-P2/A-P3 already generate is explicitly dropped before commit
  (`candidate_assembly/derive.py:9-23`'s own docstring, which names this exact gap and flags it as
  future work this task closes).
- [x] **Names how `_manifest.json`/`_rounds/` coexists with the new ledger.** Spec §2: orthogonal
  concerns (corpus-graph membership vs. post-commit content completeness), proven not just argued —
  `_rounds/round-1/assembled.json` was read directly and confirmed to share `kind: "action-seed"`
  and the same id grammar as `committed-round-1.json` (e.g. `action.general.0001` appears in both),
  so a bare `Corpus.load(data/seed/actions)` really would raise `CorpusLoadError` on a duplicate id
  — verified by reading the file, not assumed from `load.py`'s own comment.

Spec reviewed against `spec-content-completeness-core.md`'s own six real sections (objective / what
exists / contract / key shape / registry / commands+structure+testing+boundaries) — structure
mirrored, not copied verbatim (actions is a smaller module than `core`).

## Task 8 — build

### (a) The real committed action corpus gains real `_provenance` on a fresh generation pass

Before this task: `data/seed/actions/committed-round-1.json` had 0 `_provenance` occurrences
(confirmed by the task brief and independently re-confirmed here before any change).

Real run, real local model endpoint (`http://localhost:1234/v1/chat/completions`, LM Studio,
confirmed live via `curl -s -m 3 http://localhost:1234/v1/models` before running anything —
`google/gemma-4-26b-a4b-qat` reported available), executed from
`D:\Works\source\plant-vs-zombie-rise-of-summoner\tools\seedsmith`:

```
python -m seedsmith.adapters.actions.generate_action_descriptions --dry-run
```
→ `{"planned": [...24 ids...], "dryRun": true, "generated": []}` — the automatic plan correctly
found all 24 real committed action-seed rows missing a description (no ledger existed yet).

```
python -m seedsmith.adapters.actions.generate_action_descriptions
```
Ran twice in sequence (once for a single-id smoke test via `backfill(only=("action.family.cactus.001",))`
in a `python -c` call, then once for the remaining 23 via the CLI) — both real calls, both wrote
real content:

```
{
  "planned": [ ...23 remaining ids... ],
  "dryRun": false,
  "generated": [ ...same 23 ids... ]
}
```

Real written row (`data/seed/actions/committed-round-1.json`, `action.family.cactus.001`, read back
from disk after the run):

```json
"_provenance": {
  "budgetVersion": 0,
  "finding": "Content/FieldMissing:actions:action-seed",
  "generatedUtc": "2026-09-07T19:05:00.038835+00:00",
  "model": "google/gemma-4-26b-a4b-qat",
  "pipeline": "seedsmith.adapters.actions.description_backfill",
  "promptVersion": "actions/description-backfill/1",
  "stalenessKey": "912dfec8c25df013be2abb349b20a3693bddd0e6502ade43db68ea9d39008881"
},
"description": "A rapid-fire barrage of needles erupts from the cactus, whistling through the air toward the approaching horde."
```

Post-run verification (both real committed files, all 24 rows):

```python
for f in ['data/seed/actions/committed-round-1.json','data/seed/actions/committed-round-2.json']:
    d = json.load(open(f, encoding='utf-8'))
    missing = [e['id'] for e in d['entries'] if not e.get('description')]
    print(f, len(d['entries']), 'missing:', missing)
```
→
```
data/seed/actions/committed-round-1.json 19 missing: []
data/seed/actions/committed-round-2.json 5 missing: []
```

Also verified: no language contamination in any of the 24 generated descriptions (a CJK-regex scan
over every `description` value found zero matches, even though `motifsUsed` on several of these
same rows carries real Chinese source text the model had in its briefing — the model did not leak
it into the English output).

**Ledger proof of resumability** (the whole point of `RunLedger`): re-running `plan()` after the
real generation pass returns `[]` — an automatic resumed run now does nothing, matching the
resolved automatic-backfill contract (existing content is never touched automatically). Also
proven as an executable test:
`RealCommittedCorpusCleanPassTests::test_automatic_backfill_makes_zero_model_calls_and_writes_nothing_when_all_done`
passes an intentionally-unreachable `LlmCallerConfig` — if the automatic path ever tried to call it
with real work pending, the test would fail on the connection; it passes because there is nothing to
plan.

**A real, disclosed limitation:** `budget_version=0` is a structural placeholder (actions has no
numeric budget concept the way items' tier-band budgets do); `Content/FieldStale` is stamped
(`stalenessKey` inside `_provenance`) but not yet wired into a live `seedsmith check --adapter
actions` path — spec §3/§5 name this explicitly as unbuilt, not silently omitted.

### (b) The new missing-field metric reports real findings (or a real clean pass) against real committed data

**The detector actually detects** (not just "the real corpus happens to be clean"), proven against
a synthetic two-row fixture:
`DetectorActuallyDetectsTests::test_one_missing_description_is_reported` — one row with
`description: None`, one with real text; `ContentFieldMissing` reports exactly one finding,
`missingCount=1`, `totalCount=2`.

**The real clean pass**, against the real post-generation corpus (`load_committed`, not a bare
`Corpus.load` — see spec §2 for why):

```python
result = load_committed(ACTIONS_ROOT)
ctx = Ctx(corpus=result.corpus, adapter=ActionsAdapter())
registry = MetricRegistry()
registry.register(ContentFieldMissing()); registry.register(ContentFieldStale())
registry.register(ContentLanguageContamination())
findings = run_all(registry, ctx)
```
→
```
load findings (should be loader-shape findings only, e.g. undeclared prefixes): 0
completeness findings: 0
via build_registry, Content/* findings: 0
```

The `load findings: 0` line also proves the `_manifest.json` edit (`_runs/` declared `exclude`)
actually took — before that edit, `_runs/description-backfill.ledger.json` would have produced an
`undeclared-prefix` finding.

Reachable through the shared production registry, not just a hand-built one:
`register_completeness(ACTIONS_COMPLETENESS_SPEC)` is now a real line in
`report/cli.py`'s `build_registry()` (mirroring items' own `content-completeness-items` call,
landed concurrently by another session this same day — confirmed via `git status` that
`register_completeness` was independently fixed to be idempotent-by-equality while this task was in
progress, closing a real double-registration risk this task's own `build_registry()`-repeated-call
usage would otherwise have hit).

## 3. Test runs (exact commands, exact counts)

```
python -m pytest tests/test_actions_description_completeness.py -q
```
→ `11 passed in 2.99s` (new file: registry shape, detector-fires-on-a-real-gap, real-corpus
clean-pass ×3, provenance-shape, resumed-plan-is-empty, zero-model-calls-when-done,
manual-force-escape-hatch).

```
python -m pytest tests/test_content_completeness.py tests/pipeline/test_backfill_loop.py tests/pipeline/test_staleness.py tests/test_actions_adapter.py tests/test_actions_description_completeness.py -q
```
→ `2 failed, 54 passed, 8 subtests passed` on the first run. One failure was a real, deliberate
consequence of this task's own schema change
(`test_actions_adapter.py::KindSpecTests::test_action_seed_schema_matches_spec_step_4_exactly` —
the test hardcoded the exact `ACTION_SEED_OPTIONAL` set; updated to include `"description"`, with a
comment citing why). **Fixed** — re-run after the fix:

```
python -m pytest tests/test_actions_adapter.py -q
```
→ `1 failed, 23 passed, 8 subtests passed` — the schema test now passes; the one remaining failure
(`FamilyAndPairingVocabularyTests::test_one_hundred_atom_families`, 112 != 100) is pre-existing and
unrelated (see §4).

```
python -m pytest tests -q
```
(full suite, ~127s) →
**`14 failed, 3433 passed, 2 skipped, 1 warning, 493 subtests passed`**

## 4. The 14 full-suite failures — root-caused, all pre-existing and unrelated

None of the 14 touch any file this task changed. Confirmed by `git status --porcelain` during this
session: `data/seed/items/drop-tables/d1.json`, `data/seed/dungeon/events/*`,
`data/seed/dungeon/rooms/*` (11 files), and `tools/seedsmith/seedsmith/adapters/items/*/run.py`
(4 files), plus new `adapters/demons/completeness.py` / `adapters/dungeon/completeness.py` and their
own new test files, all show as modified/added by concurrent sessions actively building Phases 1/3/4
of this same program while this session ran — matching the task brief's own warning. One real
root cause explains 8 of the 14 directly, and a second explains the rest:

- **Root cause 1 — the authored affix-family count grew from 100 to 112** (items domain, a
  concurrent session). `adapters/actions/vocab.py:74-86`'s `load_family_ids()` reads
  `data/seed/items/affix-families/*.json` fresh every call — this is genuinely shared, cross-domain
  vocabulary, not actions' own data. Failures: `test_nodegen_vocab.py::RealCorpusCountsTests`,
  `::PermittedForBranchTests`, `test_actions_adapter.py::FamilyAndPairingVocabularyTests`,
  `test_coverage_report.py::UnpairedPayoffTests`, `test_distribution_planner.py::PoolTests` (×2),
  `test_usage_stats.py::TestRealCorpusTests` (×2) — every one of these asserts an exact historical
  count (`100`, `98`) against a live count that grew to 112 while this session ran, each with its
  own "⛔ CORRECTED <date>" comment showing this is a recurring, expected-to-need-re-measuring
  pattern in this codebase, not a defect this task introduced.
- **Root cause 2 — a concurrent items/demons/dungeon content wave** landed real new rows mid-session
  (`git status`: new demon theme sets, new dungeon events/rooms, a new drop-table row): 4 more
  failures each assert a specific historical corpus count/set that moved by a small amount
  (`test_demon_themes.py` ×2: 38→40 themed entries, one new `themeKey` value; `test_items_adapter.py`:
  1513→1516 entries; `test_sampling_quality.py`: charm total 70→71; `test_distribution_planner.py
  ::DeterminismTests`: the committed `_briefs/round-1.json` is stale relative to a vocabulary that
  changed underneath it — its own test docstring already anticipates this exact failure mode).
- **`test_dungeon_registries.py::AtomFamilyTests`** — the grantable-atom-families set widened from 9
  to 25 members; same root cause 1 (shared atom/affix-family vocabulary growth), different call site.

None of these are actions-domain regressions; none touch `description_backfill/`,
`generate_action_descriptions.py`, `kinds.py`'s `description` addition, `_manifest.json`'s `_runs/`
row, or the committed action files this task wrote to. Not fixed here — they are other streams' own
corpus-count assertions needing their own re-measure, exactly as this repo's `AGENTS.md` /
`MEMORY.md` precedent (`concurrent-session-atoms-patron-drift`, `pre-existing-uncommitted-drift-*`)
already documents happening repeatedly.

## 5. Files touched (all inside the three permitted trees)

```
docs/architecture/seedsmith-content-standard/spec-content-completeness-actions.md   (new)
tasks/seedsmith-content-standard-phase2-actions-evidence.md                         (new, this file)
tools/seedsmith/seedsmith/adapters/actions/kinds.py                                 (edited)
tools/seedsmith/seedsmith/adapters/actions/description_backfill/__init__.py         (new)
tools/seedsmith/seedsmith/adapters/actions/description_backfill/prompts.py          (new)
tools/seedsmith/seedsmith/adapters/actions/description_backfill/derive.py           (new)
tools/seedsmith/seedsmith/adapters/actions/generate_action_descriptions.py          (new)
tools/seedsmith/seedsmith/report/cli.py                                             (edited, +2 lines)
tools/seedsmith/tests/test_actions_adapter.py                                       (edited, 1 test fixed)
tools/seedsmith/tests/test_actions_description_completeness.py                      (new)
data/seed/actions/_manifest.json                                                    (edited, +1 row)
data/seed/actions/committed-round-1.json                                            (edited — 19 rows gained description+_provenance)
data/seed/actions/committed-round-2.json                                            (edited — 5 rows gained description+_provenance)
data/seed/actions/_runs/description-backfill.ledger.json                            (new)
```

`tasks/seedsmith-content-standard-todo.md`/`-plan.md` were **not** edited, per this task's own
instruction (other sessions are actively editing them for Phases 1/3/4).
