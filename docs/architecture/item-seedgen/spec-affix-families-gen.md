# Spec: `affix-families-gen`

**Module id:** `affix-families-gen` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Phase:** 2 (parallel with `materials-gen`, `sockets-gen`, `consumables-gen`)
**Depends on:** `generator-harness` only — confirmed on two audit passes: an affix family's own content
(channel/op/tier-curve/tags) carries no outward reference at all; `item_role_family` legality is a
SEPARATE, downstream derived matrix module 8 already owns. ⭐ **`base-types-gen` depends on THIS
module** (its `implicit.family` field hard-references a real affix-family id — see this program's own
capability map §1) — this module must finish before `base-types-gen` starts, the reverse of the first
draft's grouping.

## Objective

A real seedsmith command authoring the 109 affix family definitions (`data/seed/items/affix-families/
*.json` — channel, op, tier-band curve, tags) — confirmed hand/LLM-session-authored, same 2026-08-22
wave as base-types, zero generator behind it. **Not the same thing as `effects generate --kind affix`**
— that existing command belongs to effect-pipeline's own module 9, draws from a different corpus
(`data/seed/atoms/**.json`), and never writes to `data/seed/items/affix-families/`; naming this module
distinctly from that command is deliberate, to avoid the exact confusion a shared name would invite.
Also distinct from `FamilyExpansion.cs`'s (E43) downstream C# expansion of these 109 families into ~490
per-tier atom rows — that transform stays as-is; this module only authors the 109 SOURCE families it
reads.

**Target users:** whoever authors new affix families going forward.

## Acceptance criteria

1. `seedsmith items generate --kind affix-family` exists, brief-and-answer shape: model picks the
   family's theme/name/channel/tag set; code resolves the tier-band curve numerics from tuning data
   (mirroring how `tier-bands.v1.json`'s weights are already authored via `seedsmith numerics rebalance`
   — reuse that numeric-resolution path rather than inventing a second one).
2. A generated family passes the SAME legality check module 8 already enforces
   (`item_role_family`, the ~1,100-cell derived matrix) — generation must fail exactly the content rules
   a hand-typed family would fail, never a looser check.
3. The generated family's `op` field is drawn from the REAL, closed op vocabulary this repo's two
   consumers actually support (`flat`/`increased`/`replace`/`flag` for `stat.derived`;
   `flat`/`increased`/`more` for `stat.modify`) — the generator must know which kind the family targets
   and constrain `op` accordingly, never emit an op neither consumer can parse.
4. Output through `generator-harness`'s ledger.

## Commands

```
python -m seedsmith items generate --kind affix-family --brief <theme-file> --write
python -m seedsmith items generate --kind affix-family --overwrite <id> --write
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/affixfamgen/   new package
  __init__.py, brief.py, schema.py, opvocab.py (the closed op-per-kind table), emit.py, run.py
```

## Code style

Mirror `setgen/`'s split. `opvocab.py` should be the ONE place that knows which ops are legal for
`stat.derived` vs `stat.modify` — both real consumers' own `TryParseOp`/`ToOpcodeShape` already define
this; read them (or a generated mirror of them) rather than hand-duplicating the four/three-item lists a
second time in Python.

## Testing strategy

- A generated family's `op` is always in the correct closed set for its declared atom kind.
- A generated family passes `item_role_family` legality exactly as a hand-typed one would.
- Harness tests (resume/reconcile/overwrite).

## Boundaries

**Always:** constrain `op` to the real, closed vocabulary the actual C# consumer supports for the
family's declared kind.

**Ask first:** whether a NEW family should target `stat.derived` or `stat.modify` when the theme could
plausibly fit either — this is a real design choice with downstream consequences (module 5's own two
delivery paths), not something the generator should silently default.

**Never:** conflate this module's `affix-family` kind with the existing `effects generate --kind affix`
command — different corpus, different owner, different schema.
