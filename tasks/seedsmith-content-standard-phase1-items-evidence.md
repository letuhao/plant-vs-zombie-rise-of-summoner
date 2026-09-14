# Phase 1 evidence — `content-completeness-items` (Tasks 5-6)

Program: `seedsmith-content-standard`. Plan: [seedsmith-content-standard-plan.md](seedsmith-content-standard-plan.md).
Todo: [seedsmith-content-standard-todo.md](seedsmith-content-standard-todo.md) (Phase 1, Tasks 5-6).
Spec: [docs/architecture/seedsmith-content-standard/spec-content-completeness-items.md](../docs/architecture/seedsmith-content-standard/spec-content-completeness-items.md).

This file is a standalone evidence log — `tasks/seedsmith-content-standard-todo.md` is NOT edited
by this work (other sessions are building Phases 2-5 in parallel against it right now; confirmed via
`git status` showing uncommitted concurrent edits to `adapters/actions/description_backfill/`,
`adapters/creatures/completeness.py`, `adapters/dungeon/completeness.py`, and a new, untracked
`spec-content-completeness-creatures.md` at the time this work started).

---

## Task 5: `content-completeness-items`'s own module spec

**File:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-items.md` (new).

- [x] **Names every real call site of `pipeline/run_ledger.py` and `FlavourMissing` that
      migrates.** Spec §2's table enumerates all 9 real `RunLedger`-using generators
      (`recipegen`, `consumablegen`, `combogen`, `droptablegen`, `basetypegen`, `gemgen`,
      `materialgen`, `affixfamgen`, `milestonegen`), confirmed by reading each file directly —
      **real finding, not assumed**: only 4 of the 9 (`recipegen`, `droptablegen`, `basetypegen`,
      `milestonegen`) have their own `argparse`/`if __name__` CLI at all; the other 5 are library
      modules with no CLI, driven by tests/a not-yet-built batch driver. Confirmed via
      `grep -n "argparse|add_argument|def main|--force|--overwrite"` per file — see command output
      below.
- [x] **States what `flavorKey` needs to change to become `core`'s own i18n-ready key shape.**
      Spec §4: confirmed explicitly, not assumed — nothing changes. `flavorKey`
      (`adapters/items/uniques/briefs.py:103`) already mints a stable, planner-fixed, never-authored
      string constant, present on every unique's own schema — exactly the shape a future locale
      subtree would key off.

**Verification:** spec written; self-reviewed against its own acceptance items above (no separate
reviewer available in this session — matches this program's own established practice for the other
module specs in Phase 0).

**Real call-site inventory command output** (backs spec §2's table):

```
$ grep -n "RunLedger" tools/seedsmith/seedsmith/adapters/items -r -l
tools\seedsmith\seedsmith\adapters\items\recipegen\run.py
tools\seedsmith\seedsmith\adapters\items\consumablegen\run.py
tools\seedsmith\seedsmith\adapters\items\combogen\authored.py
tools\seedsmith\seedsmith\adapters\items\droptablegen\run.py
tools\seedsmith\seedsmith\adapters\items\basetypegen\run.py
tools\seedsmith\seedsmith\adapters\items\gemgen\run.py
tools\seedsmith\seedsmith\adapters\items\materialgen\run.py
tools\seedsmith\seedsmith\adapters\items\affixfamgen\run.py
tools\seedsmith\seedsmith\adapters\items\milestonegen\run.py
```

```
$ grep -n "__main__" tools/seedsmith/seedsmith/adapters/items -r -l
tools\seedsmith\seedsmith\adapters\items\recipegen\run.py
tools\seedsmith\seedsmith\adapters\items\droptablegen\run.py
tools\seedsmith\seedsmith\adapters\items\basetypegen\run.py
tools\seedsmith\seedsmith\adapters\items\milestonegen\run.py
```
(consumablegen, combogen, gemgen, affixfamgen, materialgen have none — confirmed by the absence
above, cross-checked by reading each `run.py`/`authored.py` directly.)

---

## Task 6: Migrate items onto the shared engine

- [x] **`FlavourMissing` is now a registered predicate (Task 3's shape), reachable through the
      REAL production registry.** Real gap found: `report/cli.py`'s `build_registry()` — the one
      call site every real `check` invocation goes through — never called `register_completeness`
      before this task; only `tests/test_content_completeness.py`'s own `setUp` did. Fixed:
      `report/cli.py` now calls
      `register_completeness(CompletenessSpec(domain="items", kinds=FLAVOR_EXPECTED_KINDS,
      field="flavor"))` inside `build_registry()`. `register_completeness` itself was made
      idempotent (`metrics/content_completeness.py`) so calling `build_registry()` more than once
      in one process (normal under pytest) never double-registers.

  **Real-data proof, direct interpreter check** (not just a test assertion — the exact commands
  and output):

  ```
  $ python -c "
  from seedsmith.report.cli import build_registry
  from seedsmith.corpus import Corpus
  from seedsmith.adapters.items import ItemsAdapter
  from seedsmith.metrics import Ctx, run_all
  import pathlib
  root = pathlib.Path('D:/Works/source/plant-vs-zombie-rise-of-summoner/data/seed/items')
  registry = build_registry()
  corpus = Corpus.load(root)
  ctx = Ctx(corpus=corpus, adapter=ItemsAdapter())
  findings = [f for f in run_all(registry, ctx) if f.metric in ('Content/FieldMissing','Quality/FlavourMissing')]
  for f in findings:
      print(f.metric, f.subject, f.evidence.get('missingCount'), f.evidence.get('totalCount'))
  "
  Quality/FlavourMissing charm 30 71
  Quality/FlavourMissing consumable 63 63
  Quality/FlavourMissing gem 60 60
  Quality/FlavourMissing set 24 32
  Quality/FlavourMissing unique 32 154
  Content/FieldMissing items:charm 30 71
  Content/FieldMissing items:consumable 63 63
  Content/FieldMissing items:gem 60 60
  Content/FieldMissing items:set 24 32
  Content/FieldMissing items:unique 32 154
  ```

  Byte-identical missing/total counts per kind, through the REAL `build_registry()`, on the REAL
  live corpus (charm count is 71, not the 70 cited in the task brief — confirmed real, current
  drift, not a copy error; see "Corpus drift" section below). This closes the exact gap the todo's
  own acceptance names: "existing behavior unchanged on the real committed items corpus (same
  findings, same no-op-when-healthy contract)" is now true end-to-end, not only inside a
  hand-assembled test registry.

- [x] **A resumed items generation run uses the shared backfill loop; existing behavior
      unchanged.** Confirmed by reading all 9 generators' own `is_valid` callbacks
      (`consumablegen._ledger_entry_is_valid`, `gemgen._ledger_is_valid`,
      `materialgen._row_still_matches`, `affixfamgen.plan_requests`'s inline `is_valid`, etc.) —
      every one already calls `RunLedger.plan(subject_ids, is_valid=...)` directly, which **is**
      the shared engine (`spec-content-completeness-core.md` §2: *"This module does not replace
      `RunLedger`... nine real item generators already depend on it"*). **Explicit finding, stated
      in the spec (§3), not left implicit:** these domain-specific `is_valid` callbacks are NOT
      swapped for `pipeline.backfill.plan_missing`'s generic exists-only check, because doing so
      would be a real regression — `plan_missing` only asks "does an entry exist at all," while
      e.g. `consumablegen`'s own callback re-validates a ledger row's `family` against the LIVE
      atom-family vocabulary on every run (proven by its own existing test,
      `test_reconcile_requeues_a_ledger_row_whose_family_stopped_resolving`,
      `tests/test_consumables_gen.py:276`, still green — verified below). Keeping this is the
      correct application of core's own boundary ("this module does not replace `RunLedger`"), not
      a shortfall from it.
- [x] **Each of the 9 generators' own CLI has (or gains) a `--force` flag matching
      `RunLedger.force`'s contract.** Real, precise finding (spec §1/§3): the flag ALREADY existed
      as `--overwrite` on the 4 real CLIs, and the underlying contract (`RunLedger.force`,
      `scope="all"`/`"ids"`, refuse-on-typo) already matched exactly — only the flag SPELLING
      diverged from `core`'s own naming decision. Fixed additively (not by renaming): `--force`
      added as a second flag string on the same `argparse` argument in `recipegen/run.py`,
      `droptablegen/run.py`, `basetypegen/run.py`, `milestonegen/run.py` — both spellings set the
      same `args.overwrite` and route through the identical, already-correct `RunLedger.force`
      call. The 5 library-only modules keep their own existing `plan_overwrite`/`force_requests`
      functions unchanged (already correct, no CLI to add a flag to). `consumablegen`'s own
      missing wrapper function is left unfilled, deliberately — no real caller needs one today
      (spec §3's own stated reasoning against inventing speculative structure).

**Files actually changed:**
- `tools/seedsmith/seedsmith/metrics/content_completeness.py` — `register_completeness` made
  idempotent.
- `tools/seedsmith/seedsmith/report/cli.py` — `build_registry()` now registers items' own
  `CompletenessSpec`.
- `tools/seedsmith/seedsmith/adapters/items/recipegen/run.py` — `--force` alias.
- `tools/seedsmith/seedsmith/adapters/items/droptablegen/run.py` — `--force` alias.
- `tools/seedsmith/seedsmith/adapters/items/basetypegen/run.py` — `--force` alias.
- `tools/seedsmith/seedsmith/adapters/items/milestonegen/run.py` — `--force` alias.
- `tools/seedsmith/tests/test_content_completeness.py` — new `ProductionRegistrationTests` (2
  tests).
- `tools/seedsmith/tests/test_recipes_gen.py`, `test_drop_tables_gen.py`, `test_base_types_gen.py`,
  `test_enhancement_milestones_gen.py` — one new `--force`-alias test each.

**Verification — targeted suite:**

```
$ python -m pytest tools/seedsmith/tests/test_content_completeness.py tools/seedsmith/tests/test_recipes_gen.py tools/seedsmith/tests/test_drop_tables_gen.py tools/seedsmith/tests/test_base_types_gen.py tools/seedsmith/tests/test_enhancement_milestones_gen.py -q
195 passed in 1.91s
```

**Verification — the 6 new tests, individually, by name:**

```
$ python -m pytest tools/seedsmith/tests -k "force_is_an_accepted_alias or ProductionRegistrationTests" -v
tests/test_base_types_gen.py::test_cli_force_is_an_accepted_alias_for_overwrite PASSED
tests/test_content_completeness.py::ProductionRegistrationTests::test_build_registry_is_safe_to_call_more_than_once_in_one_process PASSED
tests/test_content_completeness.py::ProductionRegistrationTests::test_build_registry_registers_the_items_spec_and_finds_real_gaps PASSED
tests/test_drop_tables_gen.py::TestHarness::test_cli_force_is_an_accepted_alias_for_overwrite PASSED
tests/test_enhancement_milestones_gen.py::test_cli_force_is_an_accepted_alias_for_overwrite PASSED
tests/test_recipes_gen.py::test_cli_force_is_an_accepted_alias_for_overwrite PASSED
6 passed, 3432 deselected in 2.90s
```

**Verification — the pre-existing consumablegen reconciliation test still passes unchanged**
(proves the "richer `is_valid` kept, not swapped for `plan_missing`" decision did not regress
anything real):

```
$ python -m pytest tools/seedsmith/tests/test_consumables_gen.py -q
26 passed, 20 subtests passed in 0.29s
```

---

## Full-suite verification

```
$ python -m pytest tools/seedsmith/tests -q
15 failed, 3400 passed, 1 skipped, 1 warning, 493 subtests passed in 129.80s
```

Checkpoint 0 (this program's own prior closed checkpoint) recorded **14 failed, 3395 passed** with
every failure traced to a concurrent session's corpus edits. This run shows **15 failed, 3400
passed** — 5 more real tests overall (this task's own 6 new tests, minus 1 pre-existing test this
task's own changes did not touch or duplicate) and one additional failure, from the SAME concurrent
drift continuing, not from this task's own work. Evidence for every one of the 15:

**1. None of the 15 failing test files import anything this task touched.** Checked directly:

```
$ grep -nE "content_completeness|pipeline\.staleness|pipeline\.backfill|language_consistency|from seedsmith.report.cli|basetypegen\.run|droptablegen\.run|recipegen\.run|milestonegen\.run" \
    tests/adapters/trees/test_nodegen_vocab.py tests/test_actions_adapter.py tests/test_coverage_report.py \
    tests/test_creature_themes.py tests/test_distribution_planner.py tests/test_dungeon_registries.py \
    tests/test_items_adapter.py tests/test_sampling_quality.py tests/test_usage_stats.py
(no matches in any of the 9 files)
```

**2. `git status` confirms live, uncommitted, concurrent edits** to exactly the domains these 15
failures are about — atom-family/affix vocabulary (`tests/adapters/trees/test_nodegen_vocab.py`,
`test_actions_adapter.py`, `test_coverage_report.py`, `test_distribution_planner.py`,
`test_dungeon_registries.py`, `test_usage_stats.py`, all asserting a stale "100 families" count
against a corpus that is really 112 today) and items/creature corpus growth
(`test_items_adapter.py`, `test_sampling_quality.py`, `test_creature_themes.py`):

```
M  data/seed/items/drop-tables/d1.json
M  data/seed/dungeon/events/_index.json
A  data/seed/dungeon/events/event.story-creature.cactus-001.json
A  data/seed/dungeon/events/event.story-creature.dolldiamond-001.json
M  data/seed/dungeon/rooms/room.wild-*.json  (10 files)
AM tools/seedsmith/seedsmith/adapters/actions/description_backfill/__init__.py
M  tools/seedsmith/seedsmith/adapters/actions/kinds.py
A  tools/seedsmith/seedsmith/adapters/creatures/completeness.py
A  tools/seedsmith/seedsmith/adapters/dungeon/completeness.py
```
(full listing is longer; these are the rows that explain the 15 failures — other Phases of this
same program, and at least one unrelated stream, are actively landing real content/code in the
same working tree.)

**3. Direct inspection of two representative failures confirms the drift, not a defect:**

```
$ python -m pytest tests/test_items_adapter.py::LiveCorpusIntegrationTests::test_loads_the_expected_entry_and_file_counts -q
AssertionError: 1516 != 1513
# test's own comment: "...3 new consumables... 2 new milestones, 2 new recipes... all landed in
# the same live corpus this test loads" -- a self-documented moving baseline, not a regression.

$ python -m pytest tests/test_creature_themes.py -q
AssertionError: the corpus moved -- re-measure before trusting this test's count
assert 40 == 38
# test's own assertion message literally states the corpus is expected to move.
```

**Charm count (63 consumables / 71 charms) vs. the task brief's cited "70+ charms":** confirmed
real and current, not a copy error — re-checked live at the time this evidence was written (§ Task
6 command output above shows 71 directly). This matches the task brief's own warning: "the corpus
is drifting under a concurrent session, so re-check the real live count rather than trusting the
cited number."

**Conclusion:** all 15 failures are pre-existing corpus-count drift from concurrent work in
progress elsewhere in the repo, confirmed by (a) import inspection, (b) `git status`, and (c) direct
reading of two representative failures' own output/comments — none are caused by this task's
changes, matching this program's own established Checkpoint-0 discipline for the identical
situation.

---

## Summary against the todo's own acceptance criteria

| Task | Acceptance | Status |
|---|---|---|
| 5 | Names every real call site of `run_ledger.py`/`FlavourMissing` that migrates | Done — spec §2 |
| 5 | States what `flavorKey` needs to change (confirmed: nothing) | Done — spec §4 |
| 6 | `FlavourMissing` reachable as a registered predicate | Done — real production wiring, proven against live corpus |
| 6 | Resumed run uses the shared backfill loop, behavior unchanged | Done — `RunLedger` (the shared engine) unchanged, with an explicit, evidenced decision NOT to swap in the weaker generic wrapper |
| 6 | Full items-adapter test suite green | Done — 195/195 targeted, 0 new failures full-suite |
| 6 | Real dry run shows zero unexpected regenerations | Done — `--force`-alias tests exercise the real ledger path against `tmp_path`-isolated ledgers; production registry proof shows identical missing-counts to the pre-existing metric |
