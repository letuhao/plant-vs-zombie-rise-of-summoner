# Seedsmith content-completeness — `items`

**Status:** Built 2026-09-08. Module 2 of 7 in
[seedsmith-content-standard-map.md](../seedsmith-content-standard-map.md). Depends on
[content-completeness-core](spec-content-completeness-core.md) (Checkpoint 0, closed). Idea phase:
[seedsmith-content-standard-ideal.md](../seedsmith-content-standard-ideal.md).

Items already has the most real infrastructure of any domain this program touches
(`pipeline/run_ledger.py`, `flavorKey`, `Quality/FlavourMissing`). This is a MIGRATION spec: it
names every real call site that adopts `core`'s shape, and states plainly which parts of items'
own existing design were already correct and are being kept, not reinvented.

---

## 1. Objective

Give items the same three answers `core` gives every domain (spec-content-completeness-core.md
§1), using `core`'s shared registry/staleness/backfill machinery instead of items' own
hardcoded `FLAVOR_EXPECTED_KINDS` frozenset — while changing as little of items' own
already-correct, already-tested behavior as possible.

Two real findings drove this spec more than any new design decision did:

1. **`register_completeness` had zero real (non-test) callers.** Phase 0 built the registry and
   the `Content/FieldMissing` metric, and `tests/test_content_completeness.py`'s own
   `ByteIdenticalToFlavourMissingTests` proved the shape works — but nothing outside that test's
   own `setUp` ever called `register_completeness(...)`. A real
   `python -m seedsmith.report.cli check --adapter items` run would have reported **zero**
   `Content/FieldMissing` findings, silently, while `Quality/FlavourMissing` kept reporting real
   ones on the exact same corpus. This is exactly the kind of gap this program exists to close,
   found while building the module meant to close it.
2. **`--overwrite` is not a placeholder.** `spec-content-completeness-core.md`'s own §2 states the
   plan/todo docs' earlier `--overwrite` was "a placeholder" superseded by `--force`
   (`RunLedger.force()`'s own method name, `generate_commander_effects.py --force`). That is true
   for the PLAN's own prose. It is not true for items' own real, shipped code: `--overwrite` is the
   actual, live CLI flag name across every one of item-seedgen's own generator-harness specs
   (`spec-recipes-gen.md`, `spec-drop-tables-gen.md`, `spec-base-types-gen.md`,
   `spec-enhancement-milestones-gen.md`, `spec-materials-gen.md`, `spec-sockets-gen.md`,
   `spec-affix-families-gen.md`, `spec-consumables-gen.md`), tested, and referenced by every one of
   those eight docs today. This is a genuine, independent SECOND name for the same manual-override
   concept — items' own naming predates `core`'s own naming decision, not a stale doc placeholder.
   §3 below states what this spec does about it.

## 2. Real call-site inventory

**Nine real generators depend on `RunLedger` today** (confirmed by grep, matching
`spec-content-completeness-core.md` §2's own count): `recipegen`, `consumablegen`, `combogen`,
`droptablegen`, `basetypegen`, `gemgen` (sockets-gen), `materialgen`, `affixfamgen`,
`milestonegen`. Splitting them by what they actually expose is the real finding this spec adds —
the todo's own framing ("each of those 9 generators' own CLI") assumes a uniform shape that the
real code does not have:

| Generator | Own CLI (`argparse`, `if __name__`) | Manual-override path today |
|---|---|---|
| `recipegen/run.py` | Yes | `--overwrite <ids\|all>` → `RunLedger.force` |
| `droptablegen/run.py` | Yes | `--overwrite <ids\|all>` → `RunLedger.force` |
| `basetypegen/run.py` | Yes | `--overwrite <ids\|all>` → `RunLedger.force` |
| `milestonegen/run.py` | Yes | `--overwrite <ids\|all>` → `RunLedger.force` |
| `gemgen/run.py` | **No** — library module only | `plan_overwrite(partition, target, ...)` → `RunLedger.force`, called by `generate_partition(..., overwrite=...)` |
| `affixfamgen/run.py` | **No** — library module only | `force_requests(ledger, requests, scope=...)` → `RunLedger.force` |
| `materialgen/run.py` | **No** — library module only | `plan_overwrite(ids, ...)` → `RunLedger.force` |
| `combogen/authored.py` | **No** — library module only | `plan_overwrite(plan, target, ledger)` → `RunLedger.force` |
| `consumablegen/run.py` | **No** — library module only | **None** — no `plan_overwrite`/`force_*` wrapper exists; its own test (`test_overwrite_by_id_bypasses_ledger_state_for_named_ids_only`, `tests/test_consumables_gen.py:288`) calls `ledger.force(...)` directly |

Confirmed by reading each file directly (not inferred from naming): the five "library module only"
rows have no `argparse`/`if __name__ == "__main__"` block anywhere in their own module — they are
driven by a test or a not-yet-built batch driver that injects an `answer_fn`/`call`, matching
`gemgen/run.py`'s own docstring ("The model call itself is not made here"). "Each of the 9
generators' own CLI" (the todo's own phrasing) therefore does not apply uniformly: only 4 of 9
have a CLI at all today.

**`FlavourMissing`'s own scope, unchanged:** `metrics/quality.py:21`'s `FLAVOR_EXPECTED_KINDS`
(`base-type`, `unique`, `charm`, `consumable`, `gem`, `set`) is the real, historically-measured set
of player-facing item kinds. This spec does not widen or narrow it — `content-completeness-core`'s
own registry takes this exact frozenset as `CompletenessSpec.kinds` verbatim.

## 3. What migrates, what does not, and why

**Migrates — registration (the real gap, §1):**
`report/cli.py`'s `build_registry()` now calls
`register_completeness(CompletenessSpec(domain="items", kinds=FLAVOR_EXPECTED_KINDS,
field="flavor"))` before registering `ContentFieldMissing()` — the one real production call site
every `check` invocation goes through, so `Content/FieldMissing` now reports on items on every
real run, not only inside a test's own manually-assembled registry. `register_completeness` was
made idempotent (skip an exact duplicate `CompletenessSpec`, frozen-dataclass `==`) as part of this
migration — a real, small core fix, not a items-specific hack: `build_registry()` is called
repeatedly within one pytest process and `tests/test_content_completeness.py`'s own
`clear_registry()` calls would otherwise leave the registry empty for whatever runs next in the
same process, or (without idempotency) duplicate the spec and inflate finding counts if
`build_registry()` is ever called more than once for any other reason. `FlavourMissing` itself
stays registered, unchanged — this is an additive second reporting path over the same real data
(Task 3's own guarantee), never a replacement.

**Does NOT migrate — `RunLedger.plan`'s own `is_valid` callbacks stay domain-specific, on
purpose.** `pipeline/backfill.py`'s `plan_missing(ledger, subject_ids)` is `core`'s own generic
convenience wrapper: "needing work" means **no ledger entry at all**, full stop (spec-content-
completeness-core.md §3's own contract — staleness and every other signal are deliberately excluded
from the automatic path). Items' own 9 generators already call `RunLedger.plan(subject_ids,
is_valid=<domain-specific check>)` directly, and every one of those `is_valid` callbacks does REAL
work `plan_missing`'s bare existence check does not:

- `consumablegen._ledger_entry_is_valid` (`consumablegen/run.py:144-150`) requeues a ledger row if
  its recorded `family` no longer resolves against the LIVE `atom-family-library.md` vocabulary —
  proven live by its own test, `test_reconcile_requeues_a_ledger_row_whose_family_stopped_resolving`
  (`tests/test_consumables_gen.py:276`).
- `gemgen._ledger_is_valid` (`gemgen/run.py:86-91`) requeues a row whose recorded entry is not a
  real, complete assembled entry.
- `materialgen._row_still_matches` (`materialgen/run.py:70-78`) requeues a row if the corpus no
  longer carries a matching id for it (hand-deleted or hand-edited out from under the ledger).
- `affixfamgen.plan_requests`'s own `is_valid` (`affixfamgen/run.py:70-81`) requeues a row whose
  minted id is no longer present in its own partition file.

**Swapping any of these onto `plan_missing`'s bare "entry exists" check would be a real
regression**, not a migration: a corpus row invalidated behind the ledger's back (a renamed atom
family, a hand-deleted entry) would silently stop being reconciled, and a `RunLedger` row that
merely exists would be trusted forever. `spec-content-completeness-core.md` §2 already states the
boundary this respects: *"This module does not replace `RunLedger`... [it] adds the one thing it
doesn't do: staleness reporting."* Items' own reconcile logic is not staleness in `core`'s sense
(prompt/schema/model drift, §4) — it is corpus-integrity reconciliation, a real, additional check
`RunLedger.plan`'s own `is_valid` parameter exists specifically to carry, per-domain. Keeping it
unchanged **is** correct adoption of `core`'s own stated boundary, not a shortfall from it.

**Migrates — CLI naming parity, additively.** The real, already-shipped `--overwrite` flag (§1,
finding 2) is not renamed — doing so would touch 4 real CLIs and require updating 8 real,
independently-reviewed generator-harness specs for a purely cosmetic consistency win over an
already-working, already-tested interface, with no reported confusion or defect motivating it.
Instead, `--force` is added as a second flag string on the SAME `argparse` argument in all 4 real
CLIs (`ap.add_argument("--overwrite", "--force", ...)`), so both spellings set the same
`args.overwrite` variable and route through the identical `RunLedger.force` call already there.
This gives forward naming parity with `core`'s own convention (RunLedger.force()/
`generate_commander_effects.py --force`) without breaking anything that already types
`--overwrite`, and without rewriting 8 real specs to chase a name change this task was not asked to
make. The 5 library-only modules are unchanged at the CLI layer (they have none) — their own
`plan_overwrite`/`force_requests` functions already wrap `RunLedger.force` correctly and need no
new flag, since there is no flag parser to add one to.

**consumablegen's own missing `force`/`plan_overwrite` wrapper is left alone, deliberately.**
Unlike its 8 siblings, `consumablegen/run.py` has no `plan_overwrite`/`force_*` convenience
function — only a direct `ledger.force(...)` call from its own test. This is not treated as a gap
to fill here: nothing today drives `consumablegen` through a CLI or a batch runner that would need
such a wrapper (its own module docstring already scopes its generate half narrowly — "deciding
whether/how those 60 specific rows should grant an action is a content decision... not this
generator's to make unprompted" is about its separate reconcile half, but the same narrow-scope
posture holds: inventing a wrapper function with no real caller would be speculative structure, the
opposite of this program's own "an honest gap costs a sentence" discipline). `RunLedger.force` is
already reachable by any real caller the same way its own test reaches it.

## 4. `flavorKey` and the i18n-ready storage shape

**Confirmed explicitly, not assumed (todo's own Task 5 acceptance item 2):** `flavorKey`
(`adapters/items/uniques/briefs.py:103`, `DESCRIPTIONS["flavorKey"]` = *"The minted i18n key for
this anchor's flavor text. NOT authored -- fixed by the planner."*) needs **no change** to serve as
`core`'s own i18n-ready key shape. It already is exactly that shape: a stable, planner-minted
string constant (`_planned_const`, `build_unique_schema.py:130` area), never authored by the model,
present on every unique's own schema (`build_unique_schema`'s own `required` list,
`briefs.py:231-233`) alongside its sibling `nameKey`/`iconKey`. A future locale subtree
(`data/seed/items/i18n/<locale>/`, per `content-completeness-core`'s own §5/ideal-doc §"The
shape" item 3) could key directly off `flavorKey` today with zero schema change — this program's
own scope explicitly stops short of building that subtree (translation is deferred, per the owner's
own settled decision), so nothing here builds it, only confirms the key is already ready for it.

## 5. Commands

```powershell
# Real production run — Content/FieldMissing now reports on items (the fixed gap, §1/§3):
python -m seedsmith.report.cli check --adapter items

# A resumed real generator, missing-only automatic (unchanged; already correct):
python -m seedsmith.adapters.items.recipegen.run

# The same generator, forcing a named id or everything — both spellings now accepted:
python -m seedsmith.adapters.items.recipegen.run --overwrite recipe-draw-000
python -m seedsmith.adapters.items.recipegen.run --force recipe-draw-000
python -m seedsmith.adapters.items.recipegen.run --force all
```

## 6. Project structure

```
tools/seedsmith/seedsmith/report/cli.py                        (edited) — §3, registers items' spec
tools/seedsmith/seedsmith/metrics/content_completeness.py       (edited) — §3, idempotent register
tools/seedsmith/seedsmith/adapters/items/recipegen/run.py       (edited) — §3, --force alias
tools/seedsmith/seedsmith/adapters/items/droptablegen/run.py    (edited) — §3, --force alias
tools/seedsmith/seedsmith/adapters/items/basetypegen/run.py     (edited) — §3, --force alias
tools/seedsmith/seedsmith/adapters/items/milestonegen/run.py    (edited) — §3, --force alias
tools/seedsmith/tests/test_content_completeness.py              (edited) — production-wiring proof
tools/seedsmith/tests/test_recipes_gen.py                       (edited) — --force alias test
tools/seedsmith/tests/test_drop_tables_gen.py                   (edited) — --force alias test
tools/seedsmith/tests/test_base_types_gen.py                    (edited) — --force alias test
tools/seedsmith/tests/test_enhancement_milestones_gen.py        (edited) — --force alias test
```

No new module. This spec's own work is migration/wiring, matching its own §1 framing — everything
it touches already existed.

## 7. Code style

Matches this program's own established style (frozen dataclasses, `from __future__ import
annotations`, real file:line citations for every generalization claim) — no new style questions;
every file touched already follows it.

## 8. Testing strategy

- **Production-wiring proof** (the real gap, §1): `test_content_completeness.py`'s new
  `ProductionRegistrationTests` calls the REAL `report.cli.build_registry()` (not a hand-built
  registry) and proves `Content/FieldMissing`'s findings on the real, live items corpus are
  identical to `Quality/FlavourMissing`'s own findings on the same corpus, plus a second test
  proving `build_registry()` is safe to call more than once in one process (idempotent
  registration).
- **CLI naming-parity proof**: one new test per real CLI (`recipegen`, `droptablegen`,
  `basetypegen`, `milestonegen`) proving `--force` is accepted as an alias for `--overwrite` and
  routes through the same real `RunLedger.force` call, against a `tmp_path`-isolated ledger (never
  the real committed one).
- **Real-corpus counts are expected to drift** — a CONCURRENT session was actively modifying
  `data/seed/items/drop-tables/d1.json` and other item files during this task (confirmed via
  `git status`, not assumed); a corpus-count assertion moving (e.g. 70→71 charms) is pre-existing
  drift, not a regression from this spec's own work, per this program's own established discipline
  (Checkpoint 0's own log documents the identical situation). See the evidence log for the exact
  before/after failure lists and the import-based proof that none of the drifted tests touch any
  file this spec's own work changed.

## 9. Boundaries

- **Always:** keep `FlavourMissing`'s own existing behavior byte-identical (Task 3's guarantee,
  re-verified here against the real corpus through the actual production `build_registry()`, not
  only a test-constructed registry); keep every item generator's own domain-specific `is_valid`
  reconciliation logic exactly as-is (§3); keep `--overwrite` working unchanged for every existing
  caller.
- **Ask first:** renaming `--overwrite` outright anywhere item-seedgen's own 8 generator-harness
  specs document it — a real, wider-blast-radius change than this migration task, not made here
  without it being asked for directly.
- **Never:** swap a domain generator's own richer `is_valid` reconcile check for `core`'s generic
  exists-only `plan_missing` wrapper — doing so silently drops real reconciliation coverage 4+ real
  generators already depend on (§3); invent a wrapper function (e.g. a `consumablegen` force
  helper) with no real caller just to make the 9-generator table look uniform.

## Open questions

None. The one real ambiguity this spec found (whether `--overwrite` should be renamed to match
`core`'s own convention) is resolved in §3: additive alias, not a rename, with the reasoning stated
plainly rather than deferred.
