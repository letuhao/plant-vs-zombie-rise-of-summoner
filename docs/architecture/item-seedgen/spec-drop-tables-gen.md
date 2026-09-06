# Spec: `drop-tables-gen`

**Module id:** `drop-tables-gen` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Build order:** 4 of 10
**Depends on:** `generator-harness` (1), `base-types-gen` (2), `affix-families-gen` (2)

## Objective

A real seedsmith command authoring the SYMBOLIC drop-table corpus
(`data/seed/items/drop-tables/{d1..d4}.json`, 468 entries) — confirmed hand/LLM-authored with seedsmith-
shaped `_meta` but no command producing it. **Explicitly excludes the band→row expander** — confirmed by
this repo's own existing ruling (`seedsmith-map.md` §5: *"Not the band→rows generator... a separate,
later thing"*) that the expansion from symbolic bands/curves into concrete integer-weighted rows
(`data/seed/loot/tables.v1.json`) is deliberately a different, non-seedsmith tool. This module authors
what feeds that expander, not the expander itself.

**Also explicitly not this module's job:** the four currently-unavailable entry kinds (`unique`,
`charm`, `insert`, `consumable`) are already authored content inside the existing corpus, refused at
IMPORT time for runtime-wiring reasons (missing `ContainerKind` values, module 17 wiring, a deliberate
consumable exclusion) — not a generation gap. This module does not need to "fix" those entries; they
already exist and are correctly named as someone else's wiring problem in `item-todo.md`.

## Acceptance criteria

1. `seedsmith items generate --kind drop-table` exists, brief-and-answer shape: model picks which
   band/theme a new table represents; code resolves the actual curve/weight numerics from tuning data,
   matching the existing symbolic (`dropBand`/`qtyCurve`) shape the current 468 entries already use.
2. A generated table references only base-type and affix-family ids that `base-types-gen`/
   `affix-families-gen` actually produced — no dangling references.
3. Output through `generator-harness`'s ledger, into the existing `d1..d4.json` corpus shape (or a
   `d5.json` continuation, matching however the existing 4-file split is organized).

## Commands

```
python -m seedsmith items generate --kind drop-table --brief <theme-file> --write
python -m seedsmith items generate --kind drop-table --overwrite <id> --write
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/droptablegen/   new package
  __init__.py, brief.py, schema.py, emit.py, run.py
```

## Code style

Mirror `setgen/`'s split. Read the existing `d1..d4.json` shape directly before writing the schema —
match the CURRENT symbolic format exactly, since the band→row expander (out of scope here) depends on
that shape staying stable.

## Testing strategy

- A generated table's referenced ids all resolve against the real base-type/affix-family corpora.
- A generated table's symbolic shape is byte-compatible with what the (separate, unbuilt) band→row
  expander expects, per its own documented input contract.
- Harness tests (resume/reconcile/overwrite).

## Boundaries

**Always:** keep output in the symbolic (band/curve) shape — never emit concrete integer weights
directly, that is the expander's job.

**Ask first:** whether the four currently-unavailable entry kinds should be author-able going forward
by this generator, once their respective owners (X7's container kinds, module 17, the consumable
exclusion) resolve — that is a downstream unblock, not this module's own decision.

**Never:** build or duplicate the band→row expander — that boundary is already decided
(`seedsmith-map.md` §5) and this module does not revisit it.
