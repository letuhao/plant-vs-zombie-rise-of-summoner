# Spec: `enhancement-milestones-gen`

**Module id:** `enhancement-milestones-gen` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Phase:** 2 (parallel with `affix-families-gen`, `materials-gen`, `sockets-gen`, `consumables-gen`)
**Depends on:** `generator-harness` only

**Added 2026-09-07** — found on the second audit pass as an undeclared 11th corpus (`base-types-gen`'s
own `enhanceTrack[].family` hard-references it), owner decision: fold in as a full module rather than
rule it out of scope.

## Objective

A real seedsmith command authoring `data/seed/items/enhancement-milestones/milestones.json` (the
enhancement-track content module 15's `enhance` operation grants at milestone thresholds). Confirmed
hand/LLM-authored 2026-08-22 (`_meta.model: "claude-haiku-4-5-20251001"`), no generator behind it.
Structurally self-contained: each entry defines its OWN `runtimeFamily` (`atom.enhance-vigor`,
`atom.enhance-edge`, etc.) with real `kindId`/`params`/`powerBand` — it does not reference OUT to
`affix-families-gen` or any other item-seedgen corpus; it is a sibling generator to
`affix-families-gen`, not a consumer of it.

**Target users:** whoever authors new enhancement-milestone content going forward.

## Acceptance criteria

1. `seedsmith items generate --kind enhancement-milestone` exists, brief-and-answer shape: model picks
   the milestone's theme/name/flavor and which channel it touches; code resolves `powerBand` and the
   op/amount numerics from tuning data, matching the real op vocabulary (`Flat`/`Increased`/etc. — read
   the exact casing/set from `EnhancePolicy.cs`'s own consumer before assuming it matches
   `affix-families-gen`'s vocabulary; confirmed as its own corpus, not assumed to share one).
2. `base-types-gen`'s `enhanceTrack[].family` references resolve against THIS module's real output —
   closing the reference that was `unresolvable: true` before this module existed.
3. Output through `generator-harness`'s ledger.

## Commands

```
python -m seedsmith items generate --kind enhancement-milestone --brief <theme-file> --write
python -m seedsmith items generate --kind enhancement-milestone --overwrite <id> --write
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/milestonegen/   new package
  __init__.py, brief.py, schema.py, emit.py, run.py
```

## Code style

Mirror `affix-families-gen`'s own shape — the closer structural sibling (self-contained family
authoring), not `setgen`'s brief-and-answer-over-existing-vocabulary shape.

## Testing strategy

- A generated milestone's `runtimeFamily` follows the real `atom.<name>` id convention this corpus
  already uses.
- `base-types-gen`'s `enhanceTrack[].family` reference, previously `unresolvable: true`, resolves
  cleanly against a real generated (or the existing hand-authored) milestone entry.
- Harness tests (resume/reconcile/overwrite).

## Boundaries

**Always:** verify the op/amount vocabulary against `EnhancePolicy.cs`'s own real consumer contract
before assuming it matches any other corpus's vocabulary.

**Ask first:** nothing new — this module's scope was itself the thing asked about; the owner already
decided to fold it in.

**Never:** conflate this corpus's `runtimeFamily` ids with `affix-families-gen`'s — same `atom.` prefix
convention, different, non-overlapping id space (`atom.enhance-*` vs. `atom.<family-name>`).
