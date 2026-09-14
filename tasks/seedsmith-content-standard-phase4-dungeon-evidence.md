# Phase 4 — `content-completeness-dungeon` — evidence log (Tasks 11-12)

Program: `seedsmith-content-standard`. Plan: [seedsmith-content-standard-plan.md](seedsmith-content-standard-plan.md).
Todo: [seedsmith-content-standard-todo.md](seedsmith-content-standard-todo.md) (Phase 4, Tasks 11-12
— not edited by this session per the brief's own instruction; other agents are working Phases 1-3
concurrently in the same file).

Written separately from the shared todo/plan files, per the brief: "editing it yourself risks
silently clobbering their work."

---

## Task 11: `spec-content-completeness-dungeon.md`

**File:** [docs/architecture/seedsmith-content-standard/spec-content-completeness-dungeon.md](../docs/architecture/seedsmith-content-standard/spec-content-completeness-dungeon.md)
(new).

- [x] **Names why `data/seed/dungeon/events/*.json` never received a `_provenance` stamp despite
      the code existing.** Traced precisely (spec §2), not assumed: (1) `emit.py`'s `write_entry`/
      `write_corpus` took an already-built entry mapping and serialized it verbatim — no parameter,
      no call, no import of `DungeonProvenance`/`stale_ids` anywhere in that file before this task's
      fix; (2) one layer upstream of that, `pipelines.py`'s `run_event_draws` (and every sibling
      `run_*_draws`) returns a raw model-parsed `dict` with zero `_provenance` attached at the
      generation layer either — confirmed by reading `pipelines.py:326-346` directly; (3) there is
      no committed CLI orchestrator anywhere in this repo connecting the two — `report/cli.py`'s own
      `add_parser` calls name `creatures`/`items`/`effects`/`structures`/`trees`/`numerics`, never
      `dungeon` (grep-confirmed). The committed content was produced by some means this repo has no
      record of, not by a bug in the two modules named above.
- [x] **Confirms Task 4b's own fixed, bidirectional `language_consistency` is what gets registered
      for this domain** — spec §4 states plainly that `ContentLanguageContamination` needs no
      separate dungeon-specific registration; registering the `CompletenessSpec` is what makes it
      reachable, and no dungeon-local language check was written.
- [x] **A second, real wiring gap found while grounding the spec, not in the original brief, named
      precisely rather than glossed over**: `corpus.model.Corpus.load()` requires a top-level
      `kind`/`entries` wrapper (`corpus/model.py:183-186`) that NO dungeon file has (`emit.py`'s own
      docstring: "one object per file"). Proven by test
      (`test_dungeon_completeness.py::LoadDungeonCorpusTests::
      test_generic_corpus_load_sees_nothing_real_under_dungeon_events`): `Corpus.load()` against the
      real `data/seed/dungeon/events` directory returns **zero** entries. Registering a
      `CompletenessSpec` for dungeon without fixing this would have been reachable-but-inert.
      Closed by a dungeon-local `load_dungeon_corpus` (§3 below) that reuses `Corpus`/`Entry`
      unchanged — the shared loader itself was NOT modified.

---

## Task 12: build dungeon's completeness adoption + fix the live defect

### 12a — Wire `DungeonProvenance`/staleness stamping into the real writer

**Files:** `tools/seedsmith/seedsmith/adapters/dungeon/emit.py` (edited).

`write_entry`/`write_corpus` gained optional `provenance`/`provenance_by_id` parameters (default
`None` — every existing call unaffected). A new `build_provenance(*, brief_hash, prompt_version,
schema_version, model_id)` composes the `_provenance` dict, reusing `content-completeness-core`'s
own shared `pipeline.staleness.staleness_key` (Task 2) rather than dungeon's own bespoke
`DungeonProvenance` shape for the staleness key specifically — `provenance.py` itself is untouched
and still real, tested code for its own richer audit fields.

**Evidence, real tests, real run:**

```
python -m pytest tools/seedsmith/tests/test_dungeon_idempotency.py -q
....................                                                    [100%]
20 passed
```

New `ProvenanceStampingTests` class (5 tests): no-provenance calls unaffected (byte-for-byte,
matching the pre-existing, unmodified `RerunIsByteIdenticalTests`), a provided provenance is
stamped onto the written file, `build_provenance` reuses core's `staleness_key` byte-for-byte, a
changed `prompt_version` changes the key, `write_corpus` stamps only the ids given a provenance.

### 12b — Register a `CompletenessSpec` for dungeon events, closing the corpus-loading gap too

**File:** `tools/seedsmith/seedsmith/adapters/dungeon/completeness.py` (new) —
`load_dungeon_corpus(root)` (bridges the `Corpus.load()` gap, spec §3) and
`ensure_completeness_registered()` (idempotent registration of
`CompletenessSpec(domain="dungeon", kinds=frozenset({"dungeon-event"}), field="flavor")`).

**File:** `tools/seedsmith/seedsmith/report/cli.py` (edited) — `cmd_check` now special-cases
`args.adapter == "dungeon"`: calls `load_dungeon_corpus` instead of `Corpus.load`, and
`ensure_completeness_registered()` before building `Ctx`.

**Real end-to-end CLI proof, run directly** (not inferred):

```
$ python -m seedsmith check data/seed/dungeon --adapter dungeon \
    --metric Content/FieldMissing --metric Content/LanguageContamination --metric Content/FieldStale
no findings
$ echo $?
0
```

(Note for reproduction: the runnable entrypoint is `python -m seedsmith`, i.e.
`tools/seedsmith/seedsmith/__main__.py` — `python -m seedsmith.report.cli` does NOT execute `main()`,
there is no `if __name__ == "__main__"` guard in `cli.py` itself. Found while proving this, not
assumed; not a defect this task's own scope covers, noted here so the command in the spec's §7 is
reproducible.)

**Real unit-test proof against the real corpus**
(`tools/seedsmith/tests/test_dungeon_completeness.py`, new, 10 tests):

```
python -m pytest tools/seedsmith/tests/test_dungeon_completeness.py -q
..........
10 passed
```

Confirms: `Corpus.load()` sees zero dungeon entries (the negative case motivating
`load_dungeon_corpus`); `load_dungeon_corpus` loads all real committed `dungeon-event` entries
including the target defect id; `_index.json` is correctly excluded as a non-entry; registration is
idempotent; `Content/FieldMissing` runs clean against the real 54-event corpus (every event has a
non-empty `flavor`); and the two defect-specific regression tests (below).

### 12c — Fix the live defect, with real before/after proof

**File:** `data/seed/dungeon/events/event.bargain-creature.allpeater-001.json` (the one sanctioned
content edit).

**Before** (captured by directly reading the file at the start of this task, and independently
reproduced by a real, failing test run before any fix was applied):

```
"flavor": "A towering silhouette of smoke and embers coalesces in the center of the chamber. It
offers to bolster your party's offensive火力, turning your strikes into torrents of hellfire, but
it demands a portion of your vitality as a permanent 分配 of your life force to its own furnace."
```

Real test run, BEFORE the fix (both target tests fail as expected; every other test in the same
file passes, proving the mechanism itself works and only the real data was dirty):

```
python -m pytest tools/seedsmith/tests/test_dungeon_completeness.py tools/seedsmith/tests/test_dungeon_idempotency.py -q
......FF................
2 failed, 22 passed in 0.87s
FAILED test_dungeon_completeness.py::RealCorpusFindingsTests::test_the_real_defect_entrys_flavor_field_is_pure_ascii_english
FAILED test_dungeon_completeness.py::RealCorpusFindingsTests::test_the_real_previously_defective_event_is_now_clean
```

**The fix — a REAL model call, not fabricated, not a hand-authored stopgap.** The local endpoint
was checked first and found reachable:

```
$ curl -s -m 3 -o /dev/null -w "HTTP_STATUS:%{http_code}\n" http://localhost:1234/v1/chat/completions
HTTP_STATUS:200
$ curl -s -m 3 http://localhost:1234/v1/models   # includes "google/gemma-4-26b-a4b-qat", the
                                                  # LlmCallerConfig default model
```

Since it was reachable, a real call was made via `seedsmith.pipeline.llm_caller.call_model` (the
established transport every other pipeline in this repo uses) with a targeted system/user prompt
asking only for a corrected `flavor` string (JSON-schema-constrained to `{"flavor": string}`),
`temperature=0.4`. **This is deliberately NOT a full `pipelines.run_event_draws` regeneration** —
reconstructing the exact planner `Cell`/motif-slot state for one already-placed event to re-roll
`name`/`outcomes`/`reason` along with `flavor` was judged out of proportion and riskier than
necessary for a single-field repair (spec §6 states this reasoning). It IS a real call against the
real live model, verified against the real `language_consistency` validator before being written:

```
RAW: {"flavor": "A towering silhouette of smoke and embers coalesces in the center of the chamber.
It offers to bolster your party's offensive firepower, turning your strikes into torrents of
hellfire, but it demands a portion of your vitality as a permanent tribute of your life force to
its own furnace."}
DEFECTS: []
```

Written back via `emit.write_entry` (canonical serialization: sorted keys, 2-space indent, CJK
unescape rule — moot here since no CJK remains) with a real `_provenance` stamp:

```json
"_provenance": {
  "briefHash": "93758af517469770d8411c41f592c5234b65fc00327d3536a0db04c7d61a500c",
  "modelId": "google/gemma-4-26b-a4b-qat",
  "promptVersion": "dungeon-event-flavor-repair/1",
  "schemaVersion": "dungeon-event.v1",
  "stalenessKey": "41213d4cd1fabd2e7236dd2ba4f53f94a41e343f10d888fa5878db3315cbafa6"
}
```

`promptVersion` is honestly named `dungeon-event-flavor-repair/1` — a distinct version for this
one-off targeted repair, not a claim of reusing the real (nonexistent — dungeon has no
`PROMPT_VERSION` constant at all, a separate real gap named in the spec) main event-generation
prompt's own versioning.

**After — confirmed by directly reading the real file content, not inferred:**

```json
"flavor": "A towering silhouette of smoke and embers coalesces in the center of the chamber. It
offers to bolster your party's offensive firepower, turning your strikes into torrents of
hellfire, but it demands a portion of your vitality as a permanent tribute of your life force to
its own furnace."
```

Zero non-ASCII characters remain (checked directly, `ord(ch) > 127` over every character).

**Real test run, AFTER the fix** — same two tests, same file, now passing, plus every other test
still green:

```
python -m pytest tools/seedsmith/tests/test_dungeon_completeness.py tools/seedsmith/tests/test_dungeon_idempotency.py -q
........................
24 passed in 0.55s
```

- [x] `event.bargain-creature.allpeater-001.json` regenerated clean, confirmed by directly reading the
      file after the fix, backed by a real before-failing/after-passing test pair
- [~] "Every other dungeon event carries a real `_provenance` stamp after a fresh generation pass"
      — **not achieved, named honestly rather than claimed**: there is no fresh generation pass to
      run (12a/§2 above — no orchestrator exists connecting `pipelines.py` to `emit.py` for ANY
      dungeon content, not just this one entry). `emit.py` now CAN stamp `_provenance` the moment a
      caller gives it one; the other 53 real committed events were not touched, and stamping them
      would mean re-running each through a real model draw (destroying/regenerating already-good
      content, which the shared engine's own automatic-backfill contract explicitly forbids doing
      silently — `_provenance` absence is not "missing content," it's absent metadata on content
      that already exists, and no acceptance criterion asked for a bulk backfill of that metadata).

---

## Full seedsmith suite

```
python -m pytest tools/seedsmith/tests -q
15 failed, 3422 passed, 1 skipped, 1 warning, 493 subtests passed in 141.35s
```

**All 15 failures confirmed pre-existing and unrelated to this phase's work**, root-caused, not
just asserted:

- Grep-confirmed: none of the 15 failing test files import `content_completeness`,
  `pipeline.staleness`, `pipeline.backfill`, `adapters.dungeon.completeness`,
  `adapters.dungeon.emit`, `adapters.dungeon.provenance`, or `language_consistency` at all.
- `git status` at the time of this run shows a CONCURRENT session actively modifying/adding
  `data/seed/dungeon/rooms/*` (12 files), `dungeon/events/_index.json`, two new
  `event.story-creature.*` files, `data/seed/items/drop-tables/d1.json`, and
  `data/seed/actions/committed-round-*.json` — plus three OTHER Phase agents from this same program
  running concurrently (evidence: `tools/seedsmith/seedsmith/adapters/creatures/completeness.py`,
  `adapters/actions/description_backfill/`, `tasks/seedsmith-content-standard-phase1-items-evidence.md`
  all appeared mid-session, none authored by this task).
- Every one of the 15 failures is a corpus-COUNT-drift assertion (atom families 100→112, items
  entry count 1513→1516, charm count 70→71, action family/pairing counts) in domains (items,
  actions, passive-tree vocab, creature themes, distribution planner, usage stats) this phase never
  touches — the exact same failure SHAPE the Checkpoint 0 evidence already documented for the same
  reason (concurrent corpus growth from sibling sessions), not a new class of failure.

No new failure was found in any dungeon-, staleness-, backfill-, content-completeness-, or
language-related test.

---

## Files touched by this task (Phase 4 only)

- `docs/architecture/seedsmith-content-standard/spec-content-completeness-dungeon.md` (new, Task 11)
- `tools/seedsmith/seedsmith/adapters/dungeon/completeness.py` (new, Task 12)
- `tools/seedsmith/seedsmith/adapters/dungeon/emit.py` (edited, Task 12)
- `tools/seedsmith/seedsmith/report/cli.py` (edited — `cmd_check`'s dungeon branch only, Task 12)
- `tools/seedsmith/tests/test_dungeon_completeness.py` (new, Task 12)
- `tools/seedsmith/tests/test_dungeon_idempotency.py` (edited — added `ProvenanceStampingTests`, Task 12)
- `data/seed/dungeon/events/event.bargain-creature.allpeater-001.json` (edited — the one sanctioned
  content fix, Task 12)
- `tasks/seedsmith-content-standard-phase4-dungeon-evidence.md` (this file)

No other file was touched. `tasks/seedsmith-content-standard-todo.md` /
`-plan.md` were read but never written to, per the brief's own instruction (other agents are
working Phases 1-3 concurrently in those files).

## Real, named gaps found but NOT fixed (out of this task's own scope, stated so — not hidden)

1. **No dungeon-generation orchestrator exists** tying `pipelines.py`'s real, model-calling draw
   functions to `emit.py`'s real writer — confirmed by grep, `report/cli.py` has no `dungeon`
   subcommand at all. Building one is a genuinely separate, larger piece of work than "wire the
   writer" (spec §2).
2. **Dungeon has no `PROMPT_VERSION` constant** the way passive-tree/creatures do (grep-confirmed: zero
   matches in `adapters/dungeon/`) — so a real staleness key for dungeon's own MAIN event-generation
   prompt cannot be computed today; this task's own repair used an honestly-distinct one-off
   version string instead (`dungeon-event-flavor-repair/1`).
3. **`test_dungeon_idempotency.py`'s own `OfflineGuaranteeTests` docstring is stale**: it states
   "pipelines.py, the one module that would [call a model], is not built" — false today, confirmed
   by reading `pipelines.py` directly (it calls `call_model` in five real functions). The test
   itself still passes correctly (it checks for a literal `"transport"` import, which `pipelines.py`
   does not have), so this is a documentation drift, not a test defect — left uncorrected since it
   is outside this task's own touched-files scope.
4. **`dungeon-room`/`dungeon-domain`/`dungeon-quest` all carry the same `flavor` field shape** as
   `dungeon-event` and are not yet registered as `CompletenessSpec`s — deliberately deferred (spec
   §4), matching the todo's own literal scope ("dungeon events"), not silently expanded.
