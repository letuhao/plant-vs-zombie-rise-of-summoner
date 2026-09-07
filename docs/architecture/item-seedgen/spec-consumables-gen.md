# Spec: `consumables-gen`

**Module id:** `consumables-gen` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Phase:** 2 (parallel with `affix-families-gen`, `materials-gen`, `sockets-gen`, `enhancement-milestones-gen`)
**Depends on:** `generator-harness` only. ⭐ **`drop-tables-gen` depends on THIS module** (hard,
consumable-row `.ref`) — `d1.json:85`: `"ref": "consumable.k1-001"`.

✅ **`family`'s real source, confirmed 2026-09-07** (was ask-first in the first draft). `k1.json`'s
`family` values (`atom.vitality`, `atom.fortitude`, `atom.mending`, `atom.bulwark`, `atom.warding`) are
real, shipped effect-atom families — `docs/architecture/effect-atom/atom-family-library.md:62-128`
documents all five with real mechanical definitions. This is an **`external` reference** (per
`generator-harness`'s own third reference kind): validated like a hard reference (exact id must exist),
but NEVER auto-backfilled — this program has no standing to author effect-atom's own content. An
unresolved `family` reference is reported as a real gap for effect-atom to fix, not guessed at here.

## Objective

A real seedsmith command authoring the 60-entry consumable catalog
(`data/seed/items/consumables/{k1,k2,k3}.json`). Confirmed hand/LLM-authored with no generator. The
audit also found a real, existing content gap this module's reconcile mode should surface as its own
acceptance evidence: `grantsActionId`/`cooldownKey` are real schema fields (`kinds.py:100-101`) but are
authored on **none** of the 60 shipped rows.

**Explicitly not this module's job** (confirmed runtime-wiring gaps, not generation gaps, per the
audit): the missing `ContainerKind.Consumable` C# value, the missing out-of-combat menu executor, and
the seed→concrete generator's own consumable binding. This module authors the content those systems
will eventually consume; it does not build them.

## Acceptance criteria

1. `seedsmith items generate --kind consumable` exists, brief-and-answer shape: model picks the
   consumable's theme/`useContext`/flavor; code resolves `powerBand`/`manifestCost` from tuning data and
   `family` against `atom-family-library.md`'s real, shipped families — never from
   `affix-families-gen`, and never inventing a family id that document doesn't list.
2. A reconcile run against the current 60 entries reports the `grantsActionId`/`cooldownKey` gap
   precisely (which rows are schema-eligible but unauthored) — proving reconcile surfaces a REAL,
   pre-existing gap, not only hypothetical ones.
3. Output through `generator-harness`'s ledger.

## Commands

```
python -m seedsmith items generate --kind consumable --brief <theme-file> --write
python -m seedsmith items generate --kind consumable --overwrite <id> --write
python -m seedsmith items generate --kind consumable --write   # default: reconcile, surfaces the
                                                                # grantsActionId/cooldownKey gap
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/consumablegen/   new package
  __init__.py, brief.py, schema.py, emit.py, run.py
```

## Code style

Mirror `setgen/`'s split, matching the established shape of this program's other generators.

## Testing strategy

- Reconcile test: running against the real 60-entry corpus reports the `grantsActionId`/`cooldownKey`
  gap on exactly the rows that lack them today (a real, measured baseline, not a synthetic fixture).
- A generated consumable's `family` reference resolves against a real atom family.
- Harness tests (resume/reconcile/overwrite).

## Boundaries

**Always:** verify a consumable's `family` reference resolves against `atom-family-library.md`'s real,
shipped families before emitting the entry.

**Ask first:** whether to actually author the missing `grantsActionId`/`cooldownKey` values for the
existing 60 rows once this module can reconcile them — reconcile surfacing the gap is this module's job;
deciding whether/how those 60 specific rows should grant an action is a content decision, not a
mechanical one this generator should make unprompted.

**Never:** invent a `useContext`/`family` value outside the real, closed vocabularies those fields
already have.
