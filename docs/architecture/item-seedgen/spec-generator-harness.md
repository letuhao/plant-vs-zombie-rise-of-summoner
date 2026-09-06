# Spec: `generator-harness`

**Module id:** `generator-harness` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Build order:** 1 of 10 — foundation
**Depends on:** nothing (every other module depends on this)

## Objective

Two pieces every generator in this program builds on, so eight modules don't each invent their own
resume logic and dependency checking:

1. **`RunLedger`** — resume/append/reconcile/selective-overwrite for one corpus, generalizing `setgen`'s
   already-proven pattern (`adapters/items/setgen/run.py`: *"Resume is not optional... The ledger is a
   single JSON file keyed by subject id... reuses [the demon harness's] atomic file lock discipline"*).
2. **`DependencyValidator`** — a deterministic, cross-corpus reference-resolution engine. **Amended
   2026-09-07, before this module was ever built, after auditing the real content shapes**: a recipe's
   `outputRef` names an exact container id that must exist before the recipe is meaningful
   (`recipes.json:30`: `"outputKind": "container", "outputRef": "item.humanoid-torso-a-001"`); a set's
   member references a `(role, frame)` CATEGORY, not a specific id (`setgen/schema.py:104-107`); a
   combination's ingredients reference a gem FAMILY category (`combogen/schema.py:92-98`). One
   corpus-local "is this entry's own shape valid" check (what the original draft of this spec described)
   cannot catch any of these — a set can be perfectly well-formed and still name a `(role, frame)`
   combination zero real base-types satisfy. This module closes that gap.

**Target users:** every other `item-seedgen` module's own generator command.

## Design — the two reference kinds, and why they need different resolution

**Hard reference** — an entry names an EXACT id in another corpus. `recipes.json`'s `outputKind:
"container"`/`"material"` entries. Resolution: does `outputRef` exist verbatim in the target corpus?
Binary, no ambiguity.

**Categorical reference** — an entry names a SELECTOR (role+frame, a family enum) that some entry in
another corpus must satisfy, but not a specific id — this is the seed-to-concrete model working exactly
as intended (module 4's own principle: a set names a slot shape, the runtime binds a specific instance
per player, per [[seed-to-concrete-generator-principle]]). Resolution: does AT LEAST ONE entry in the
target corpus satisfy the selector? A set requiring `(role: weapon, frame: plant)` with zero base-types
matching that combination is a real, silent coverage gap no per-entry validity check would ever surface.

**No reference** — `recipes.json`'s `outputKind: "mutation"` entries (reroll/enhance/salvage/bore/socket)
operate on a player's already-owned instance; there is no NEW target content to resolve. (Their
`costLines[].material` entries are still hard references, into `materials-gen`'s corpus.)

**One open question this module does not resolve unilaterally**: consumables' `family` field
(`k1.json`: `atom.vitality`, `atom.fortitude`, `atom.mending`...) does not match ANY id shape this
program's own `affix-families-gen` produces (`atom.ferocity`-style, from `g-attack`/`g-life`/
`g-armour`) — these look like a DIFFERENT, likely effect-atom-owned vocabulary this program does not
generate at all. **Ask-first, named explicitly in `consumables-gen`'s own spec**: confirm the real source
of these ids (an existing, already-populated effect-atom corpus, most likely) before assuming this
program owns generating it. Never guess a corpus into existence to make a reference resolve.

## Acceptance criteria

**RunLedger (as originally specced):**
1. Given subject ids and an `is_valid(id, entry) -> bool` check, returns exactly the ids needing work —
   never-attempted ones, and ones whose on-disk entry now fails `is_valid`.
2. Atomic temp-file-then-replace writes; a killed process leaves the ledger in its last-good state.
3. Standard CLI shape: `--write` (default, append+reconcile), `--overwrite <id[,id...]>`,
   `--overwrite all` (the literal `all` required), `--dry-run`.

**DependencyValidator (new):**
4. Each module declares a **reference manifest**: for each field that references another corpus, its
   kind (`hard` | `categorical`) and target module. A manifest entry is data (a small declared table),
   never inferred by scanning field names.
5. `validate(corpus, manifest, targets) -> ValidationReport` — for every entry, every declared
   reference, reports `resolved: bool` and, for categorical refs, the resolved count (so "resolves, but
   only barely — 1 match" is visible, not just pass/fail).
6. `plan_backfill(report) -> BackfillPlan` — for each UNRESOLVED hard reference whose target id matches
   the OWNING module's own naming convention (e.g. `item.<frame>-<slot>-<letter>-<seq>` for base-types),
   emits a targeted generation request naming that EXACT id to the owning module's own generator — never
   a generic "make something." For an unresolved categorical reference, emits a targeted request naming
   the missing `(selector)` combination specifically (e.g. "generate at least one base-type with
   `role=weapon, frame=plant`"), not an arbitrary new entry.
7. **Determinism, proven not asserted**: `validate` and `plan_backfill` take no model/LLM call anywhere
   in their own logic — running either twice against the same on-disk state produces byte-identical
   output. Backfill's ACTUAL content generation (the targeted request handed to the owning module) still
   goes through that module's own brief-and-answer authoring — the DECISION of what's missing and that
   it must be generated is deterministic; the prose/identity of the generated fix is not, and was never
   claimed to be.
8. A missing reference that does not match any known owning module's naming/selector convention is
   reported, never guessed at — `plan_backfill` names it as `unresolvable: true` with the raw reference
   value, and the run refuses to proceed past it without an explicit `--ignore-unresolved <id>` override
   (never a silent skip).

## Commands / interfaces touched

```
python -m seedsmith items validate --deps            # runs DependencyValidator across every declared
                                                       # manifest, reports resolved/unresolved/coverage
python -m seedsmith items validate --deps --backfill  # also executes plan_backfill's targeted requests
                                                       # against each gap's real owning generator
```

## Project structure

```text
tools/seedsmith/seedsmith/pipeline/run_ledger.py         new — RunLedger, as originally specced
tools/seedsmith/seedsmith/pipeline/dependency_validator.py   new — reference manifest, validate(),
                                                              plan_backfill()
tools/seedsmith/tests/test_run_ledger.py                 new
tools/seedsmith/tests/test_dependency_validator.py       new
```

## Code style

`RunLedger`: match `setgen/run.py`'s own shape, reusing the demon harness's atomic-lock primitive.
`DependencyValidator`: the reference manifest is a plain data structure (a list of
`(field_path, kind, target_module)` tuples) that each module's own `run.py` supplies — this module never
introspects another module's schema to guess references; guessing is exactly the ambiguity a declared
manifest exists to remove.

## Testing strategy

- RunLedger: resume, reconcile-detects-corruption, overwrite-by-id, overwrite-all-requires-literal,
  kill-mid-run — as originally specced.
- Hard-reference test: a recipe's `outputRef` pointing at a real base-type resolves; pointing at a
  fabricated one does not, and is reported with the raw unresolved id.
- Categorical-reference test: a set requiring `(role, frame)` with zero satisfying base-types is
  reported unresolved with count `0`; with one satisfying base-type, resolved with count `1` (visible,
  not just "pass").
- Determinism test: run `validate` twice against identical on-disk state, assert byte-identical reports.
- Backfill test: an unresolved hard reference matching a real naming convention produces a targeted
  generation request naming that exact id — not a generic "generate one more entry" request.
- Unresolvable test: a reference matching no known convention is reported `unresolvable: true` and the
  run refuses without an explicit override — never silently skipped.

## Boundaries

**Always:** declare every cross-corpus reference in a module's manifest before that module ships;
resolve categorical references by count, not by boolean presence, so "barely covered" stays visible.

**Ask first:** consumables' real `family` vocabulary source, per the Design section above — do not
assume this program owns generating it.

**Never:** let `plan_backfill` invent an id or a selector value that doesn't already appear as a real,
declared reference somewhere in the corpus being validated. Never let an unresolved reference pass
silently — report or refuse, never both-are-fine-by-default.
