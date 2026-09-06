# Spec: `sockets-gen`

**Module id:** `sockets-gen` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Build order:** 2 of 10
**Depends on:** `generator-harness` (1) only — ⚠ **corrected 2026-09-07**: a gem's own content carries
no base-type reference (gems are element/universal, socketable into any socket of the right count) —
`base-types-gen`'s own `socketMax` field is a separate corpus this module never reads or writes.

## Objective

A real seedsmith command authoring the gem/insert corpus (`data/seed/items/gems/*.json`). Confirmed
hand/LLM-session-authored with no generator, and confirmed a real, standing content gap: the audit found
`_registry_snapshot/allocated_partitions.json` allocates `gems/1,2,3` but **`gems/2` was never
authored** — an allocated-but-missing partition, not just a missing generator. This module's first real
run should close that specific gap as its own acceptance evidence.

**Scope note:** `socketMax` (the per-base-type socket count) is authored as part of `base-types-gen`'s
own output (it's a field on the base-type entry, per the audit's finding that production code reads it
from there, not from the separate `sockets.v1.json` tuning file) — this module covers the insertable
GEM corpus only, not socket-count rules.

## Acceptance criteria

1. `seedsmith items generate --kind gem` exists, brief-and-answer shape: model picks the gem's
   name/flavor/element association; code resolves the actual power numbers from tuning data.
2. Running it against the allocated-but-empty `gems/2` partition produces a real, valid,
   importable file — closing the specific gap the audit found, not a synthetic example.
3. Output through `generator-harness`'s ledger. `_registry_snapshot/allocated_partitions.json` stays the
   allocation-tracking source of truth this module reads against (to know which partitions are
   allocated-but-empty) rather than a second, competing tracking mechanism.

## Commands

```
python -m seedsmith items generate --kind gem --brief <theme-file> --write
python -m seedsmith items generate --kind gem --overwrite <id> --write
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/gemgen/   new package
  __init__.py, brief.py, schema.py, emit.py, run.py
```

## Code style

Mirror `setgen/`'s split, matching `base-types-gen`'s own established shape for the second generator
in this program.

## Testing strategy

- The allocated-but-unauthored `gems/2` partition, once generated, imports cleanly with the real
  `GemInsertCorpus.Load` reader — the actual production consumer, not a synthetic parser.
- Harness tests (resume/reconcile/overwrite).

## Boundaries

**Always:** check `allocated_partitions.json` before authoring a new gem id, to avoid claiming a
partition another module (or a hand edit) already owns.

**Ask first:** nothing new beyond the harness's own standing boundaries.

**Never:** touch `sockets.v1.json` or the per-base-type `socketMax` field — those are
`base-types-gen`'s and the sockets tuning spec's own territory, not this module's.
