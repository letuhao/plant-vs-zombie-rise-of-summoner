# Spec: `base-types-gen`

**Module id:** `base-types-gen` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Build order:** 2 of 10
**Depends on:** `generator-harness` (1)

## Objective

A real seedsmith command that authors item base-type entries (name, slot, level requirement,
`socketMax`, tags), replacing what is today a one-time, 740-entry hand/LLM-session authoring wave with
no generator behind it (`authoring-fleet-plan.md`'s own words: *"750 identities across 30 role-frames |
W1 · 60 agents"* — a hand-authoring wave, not a command). Confirmed no `base-type` kind exists in
`items generate`'s choices today.

**Target users:** whoever authors new base-type content going forward — a coding session should never
hand-type a base-type entry into JSON again.

## Acceptance criteria

1. `seedsmith items generate --kind base-type` exists, follows the SAME brief-and-answer shape `setgen`
   already proves at scale: the model picks identity/theme fields only (name, flavor, which role-frame
   it belongs to, which slot); every numeric field (level requirement, `socketMax`, any tier gating) is
   resolved by deterministic code reading the tuning tables, never by the model. Match `setgen/brief.py`'s
   own pattern of a schema that mechanically rejects a bare number field reaching a model call.
2. Output writes to the existing corpus path (`data/seed/items/base-types/*.json`), through
   `generator-harness`'s ledger — append+reconcile by default, explicit `--overwrite` for a full redo.
3. `--kind base-type --write` with a live model endpoint (see `set-charm-live-endpoint`'s wiring for the
   endpoint plumbing this module reuses) produces at least one new, valid base-type entry, importable by
   the real `ItemSeedValidator`/`AtomImporter` pipeline with zero new refusals.
4. The 30 role-frame taxonomy (whatever currently partitions the 740 entries) is read from its existing
   source, not re-invented — find and cite it before writing the brief schema.

## Commands

```
python -m seedsmith items generate --kind base-type --brief <theme-file> --write
python -m seedsmith items generate --kind base-type --overwrite <id> --write
python -m seedsmith items check --kind base-type   # existing coverage-metric path, unchanged
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/basetypegen/   new package, mirroring setgen/'s own layout
  __init__.py, brief.py, schema.py, emit.py, run.py, tuning.py
```

## Code style

Mirror `setgen/`'s file-by-file split exactly (brief construction, schema validation, numeric
resolution, emission, run orchestration as separate files) — this is the second generator built against
the same harness, and matching the first one's shape is what makes a third easy later.

## Testing strategy

- Schema test: a bare numeric field in the brief schema fails construction (mirrors `setgen`'s own
  `Pipeline.__post_init__` guard).
- A generated entry imports cleanly through `AtomImporter`/`ItemSeedValidator` with no new refusal.
- Harness tests (resume/reconcile/overwrite) via `generator-harness`'s own shared test shape, applied to
  this corpus specifically.

## Boundaries

**Always:** resolve every numeric field from tuning data, never from the model's own text.

**Ask first:** changing the 30-role-frame taxonomy itself — that's a design decision belonging to
`item-map.md`/module 6's own spec, not this generator's to make unilaterally.

**Never:** let a generated base-type entry skip whatever legality checks `item_role_family`
(module 8's derived matrix) already enforces — generation must fail the same content rules a hand-typed
entry would, not a looser set.
