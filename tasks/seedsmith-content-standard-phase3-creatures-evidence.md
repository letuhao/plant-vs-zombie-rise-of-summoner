# Phase 3 evidence — `content-completeness-creatures` (Tasks 9-10)

Program: [seedsmith-content-standard-plan.md](seedsmith-content-standard-plan.md). Task list:
[seedsmith-content-standard-todo.md](seedsmith-content-standard-todo.md) (Tasks 9-10, Phase 3).
This file is a standalone evidence log — the shared `-todo.md`/`-plan.md` files were deliberately
NOT edited (other agents are working on Phases 1/2/4/5 concurrently, confirmed live via `git
status` throughout this session).

Spec: [docs/architecture/seedsmith-content-standard/spec-content-completeness-creatures.md](../docs/architecture/seedsmith-content-standard/spec-content-completeness-creatures.md).

---

## Task 9 — spec written and self-reviewed

**File:** `docs/architecture/seedsmith-content-standard/spec-content-completeness-creatures.md`.

**Acceptance: "Names exactly which creature-species field(s) count as 'description/flavor' for this
program's own purpose (distinct from the anchor classification fields
`metrics/creature_coverage.py`/`creature_roster.py` already check)."**

Answered with a real, empirical finding, not an assumption:

```powershell
python -c "
import json, glob
sample_keys = set()
for f in glob.glob('data/seed/creatures/species/**/*.json', recursive=True):
    d = json.load(open(f, encoding='utf-8'))
    for row in d:
        sample_keys.update(row.keys())
print(sorted(sample_keys))
"
```
Output (union of every key across all 904 real entries):
`_derived, _provenance, acquisition, aptitudePrimary, aptitudeSecondary, attackTempo, basis,
deployMode, elementPrimary, elementSecondary, family, gameTypeId, posture, pure, rarity, reach,
reason, resourceProfile, side, speciesId, targetPreference, threatBand, traits, variants, verdict`

**Finding: no `name`/`flavor`/`description`/`lore` field exists on any of the 904 real entries.**
The nearest candidate by shape, `reason`, is explicitly disqualified — it is the threat-band
classification model's own audit trail (`tools/seedsmith/seedsmith/adapters/creatures/anchor/prompts.
py:242-246`: *"with a one-sentence reason"*), and its real values cite raw mechanical/stat
vocabulary, e.g. the real, committed `PotatoMine.reason` = *"A single explosion dealing 1800 damage
with a radius of 0.74 blocks..."* — the same class of content this program's own
`generate_commander_effects.py` G1 filter already treats as disqualifying for player-facing text.
This is distinct from `creature_coverage.py`/`creature_roster.py`'s own checked fields (`elementPrimary`,
`aptitudePrimary`, `rarity`, `threatBand`, `posture`, `family`, `deployMode` — all closed-loop
classification axes, confirmed by reading both files in full).

**Decision, stated in the spec §1:** register `field="flavor"` — a new field, matching this
program's universal convention (items' `flavor`, dungeon's event `flavor`, passive-tree's node
`flavor`) — not `reason`. Populating it is a new LLM generation stage this module does not build
(out of scope per the map doc's own description of this module: "adds the missing-field metric and
wires automatic backfill to it," not a generator).

Spec self-review against the six-area format (`spec-content-completeness-core.md`'s own shape,
mirrored): Objective (§1), a second real gap found while grounding it — `Corpus.load()` cannot see
`species` at all (§2), the backfill-wiring proof design (§3), Commands (§4), Project structure
(§5), Code style (§6), Testing strategy (§7), Boundaries (§8). No open questions left — Task 9's
own required naming question is answered with a negative result, which is the finding itself.

---

## Task 10 — built and proven against the real 904-species corpus

### Files changed

- `tools/seedsmith/seedsmith/adapters/creatures/completeness.py` (new) — `load_species_corpus`,
  `ensure_completeness_registered`, `missing_species_ids`, `_AnchorLedgerView`.
- `tools/seedsmith/seedsmith/report/cli.py` (edited) — one additive `elif args.adapter ==
  "creatures":` branch in `cmd_check`, mirroring the existing `dungeon` branch's shape exactly (found
  live, mid-session, as the real precedent for this exact "Corpus.load() can't see this domain's
  own bare-shape content" problem — `adapters/dungeon/completeness.py`, built by a concurrent
  session working Phase 4 in parallel).
- `tools/seedsmith/tests/test_creatures_completeness.py` (new) — 9 tests, all against the real corpus.

**No file outside `tools/seedsmith/`, `docs/architecture/seedsmith-content-standard/`, and this
evidence file was touched.** No creature seed data was regenerated, deleted, or modified — this task
is metric + wiring only, per its own scope.

### Acceptance 1 — "The new metric runs against the real 904-species committed corpus, reporting
real findings"

Real corpus load, real CLI dry run, no mocks:

```powershell
python -c "from seedsmith.report.cli import main; import sys; sys.exit(main(['check','../../data/seed/creatures','--adapter','creatures','--metric','Content/FieldMissing']))"
```
Output:
```
[GAP] Content/FieldMissing — creatures:species: 904 of 904 'species' entries in domain 'creatures' have no 'flavor' (BloverUmbrella, SquashBlover, CactusBlover, +901 more)

1 gap
```
Exit code 1 (a real GAP finding, `gates=False` so this does not fail CI — consistent with every
other metric this program registers).

Also proven in `tests/test_creatures_completeness.py::ContentFieldMissingAgainstRealCorpusTests::
test_reports_904_of_904_missing_flavor`: asserts `subject == "creatures:species"`,
`missingCount == 904`, `totalCount == 904`, `field == "flavor"` — against `load_species_corpus`
reading the real, on-disk `data/seed/creatures/species` tree, not a fixture.

A real gap discovered and fixed while proving this: `Corpus.load()` (the generic seed-file loader)
requires a top-level `{kind, entries}` wrapper (`corpus/model.py:183-186`); every real species file
is a bare JSON array (`anchor/emit.py`'s own `render_family_file`), so the generic loader silently
sees zero `species` entries — registering the `CompletenessSpec` alone would have been reachable but
inert (a false-clean pass, not a real 904/904 gap). Fixed by `load_species_corpus`, which walks
`species/**/*.json` directly and ADDS entries onto the corpus the generic loader already built
correctly for the sibling `creature`/`commander-effect` kinds (confirmed: `creature` kind count is
unaffected by adding species — `test_species_entries_add_onto_an_existing_corpus_without_
disturbing_it`).

### Acceptance 2 — "A resumed creature-species generation run backfills only genuinely
missing/stale entries"

Creatures' own real, live, already-shipped resumability (`adapters/creatures/run/runner.py`'s `start()`)
already implements the resolved missing-only-automatic contract natively:

```python
existing_anchors = _load_existing_anchors(paths.anchors_dir)
already_done = {a["speciesId"] for a in existing_anchors} if not force_selector_ignores_existing else set()
ids = [i for i in ids if i not in already_done]
```

This task's own scope (per the module map: "wires automatic backfill," not "rewrite `runner.py`")
is to prove the SHARED engine (`pipeline.backfill.plan_missing`) generalizes to this real,
production behavior rather than being a sixth independent reimplementation of the same shape.
`missing_species_ids` (a thin wrapper duck-typing `RunLedger.read_done()` over the real anchor
list) is proven, against the real, full 904-entry corpus, to produce output identical to
`runner.py`'s own bespoke filter:

- `test_every_real_species_id_is_a_no_op_not_missing` — feeding all 904 real ids back in: the
  shared engine's plan is `[]`, a true no-op at full corpus scale (zero unnecessary regeneration).
- `test_a_genuinely_new_id_not_yet_anchored_is_correctly_planned` — a held-out synthetic id mixed
  into the real 904 is the only one planned.
- `test_matches_runner_pys_own_bespoke_already_done_filter_exactly` — `runner.py`'s own
  `already_done`/`ids = [i for i in ids if i not in already_done]` logic is reproduced verbatim
  against a slice of the real corpus plus two new ids, and asserted byte-for-byte equal (after
  sorting) to `missing_species_ids`'s own output: both return exactly
  `["FirstNewSpecies", "SecondNewSpecies"]`.

`runner.py` itself was intentionally NOT modified — it is a complex, live, production module (a
real mutual-exclusion lock, threaded classification, per-species crash-safe checkpointing,
documented incidents from 2026-09-02/09-04). Rewriting its internals is outside this narrow
module's stated scope and risk budget; the equivalence proof above is the generalization evidence
Checkpoint 5b asks for ("a resumed run on each domain is a no-op on already-good content, proven,
not assumed") without touching that module's own spec (`spec-run-control.md`).

`Content/FieldStale` correctly reports nothing for creatures today: no `flavor` field exists yet to
carry a staleness key, matching the metric's own documented silence-is-correct convention (a domain
that never stamps `_stalenessCurrentKey` produces no finding, not a false claim of freshness).
Species anchors keep their own richer, already-real staleness detector (`anchor/emit.py`'s
`stale_ids`, `dumpHash` + per-pipeline `promptVersions`) for the classification fields it already
governs — untouched by this task, per its own boundary against inventing a domain-specific
staleness shape inside `core`, applied here in the reverse direction (an existing, finer-grained
domain detector is not replaced by the generic one either).

### Idempotent registration

`ensure_completeness_registered()` guards on `(domain, field)` before appending, matching
`adapters/dungeon/completeness.py`'s own established idiom (found live, same session, same
problem: `register_completeness` itself has no dedup, and `report/cli.py`'s `build_registry()` —
confirmed 7 real call sites — would otherwise register the same spec repeatedly within one process,
e.g. a test suite run). Proven by `test_idempotent_across_repeated_calls`: three calls, one
registered spec.

### Test commands run and exact results

```powershell
python -m pytest tools/seedsmith/tests/test_creatures_completeness.py -q
```
```
9 passed in 2.70s
```

```powershell
python -m pytest tools/seedsmith/tests/pipeline/test_staleness.py tools/seedsmith/tests/test_content_completeness.py \
  tools/seedsmith/tests/pipeline/test_backfill_loop.py tools/seedsmith/tests/workflow/validators/test_language.py \
  tools/seedsmith/tests/test_creatures_completeness.py tools/seedsmith/tests/test_anchor_emit.py \
  tools/seedsmith/tests/test_adapter_creatures.py -q
```
```
65 passed in 2.98s
```
(Confirms Phase 0's own Core suite plus this phase's own new tests plus the pre-existing creatures
anchor/adapter suites are all still green together — no regression introduced by the `cli.py` edit
or the new module.)

**Full suite:**
```powershell
python -m pytest tools/seedsmith/tests -q
```
```
15 failed, 3422 passed, 1 skipped, 1 warning, 493 subtests passed in 141.30s
```

**All 15 failures confirmed pre-existing and unrelated to this phase**, via two independent checks:

1. `git status` at the time of this run shows active, uncommitted, concurrent modification of
   `data/seed/items/drop-tables/d1.json`, `data/seed/dungeon/events/*` (+2 new files),
   `data/seed/dungeon/rooms/*` (10 files), `data/seed/actions/committed-round-*.json`,
   `data/seed/passive-tree/nodes/BambooDragon.json` — other agents actively working Phases 1/2/4/5
   of this same program, in progress as this evidence was captured. Every one of the 15 failures is
   a corpus-COUNT mismatch (items entries 1513→1516, charm count 70→71, atom families 100→112,
   creature-theme/action-pairing counts) in exactly these domains — the same class of drift Checkpoint
   0's own evidence already documented and root-caused for a different failure set on 2026-09-08.
2. Grep-confirmed: none of the 15 failing test files import `content_completeness`,
   `pipeline.staleness`, `pipeline.backfill`, `adapters.creatures.completeness`, or
   `language_consistency` — and this task's only code edit (`report/cli.py`'s `cmd_check`) is
   gated behind `args.adapter == "creatures"`, an additive branch that cannot affect the
   items/actions/trees/dungeon code paths any of the 15 failing tests exercise.

Failing files (all pre-existing, all corpus-count drift in domains this task does not touch):
`test_nodegen_vocab.py` (×2), `test_actions_adapter.py` (×2), `test_coverage_report.py`,
`test_creature_themes.py` (×2), `test_distribution_planner.py` (×3), `test_dungeon_registries.py`,
`test_items_adapter.py`, `test_sampling_quality.py`, `test_usage_stats.py` (×2).

---

## Summary against Task 9/10's own checkboxes

- [x] Task 9: names exactly which creature-species field counts as description/flavor — answer:
      none exist yet; this program registers a new `flavor` field, `reason` is explicitly
      disqualified with real evidence.
- [x] Task 10: the new metric runs against the real 904-species corpus, reporting a real finding
      (904/904 missing `flavor`) — proven via both a direct CLI invocation and a unit test.
- [x] Task 10: a resumed creature-species generation run backfills only genuinely missing entries —
      proven via equivalence between the shared engine's `plan_missing` (through
      `missing_species_ids`) and `run/runner.py`'s own real, live, already-shipped bespoke filter,
      at full 904-entry corpus scale.
- [x] Creature-adapter test suite green (65/65 across Core + creatures-specific suites); full suite run
      with all 15 failures confirmed pre-existing and unrelated by two independent checks.
