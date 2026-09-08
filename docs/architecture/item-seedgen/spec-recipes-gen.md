# Spec: `recipes-gen`

**Module id:** `recipes-gen` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Phase:** 4 (parallel with `set-charm-live-endpoint`, `combination-write-unblock`)
**Depends on:** `generator-harness`, `base-types-gen`, `materials-gen`

## Reference manifest (per `generator-harness`'s `DependencyValidator`)

Confirmed against the real corpus (`recipes.json`), not assumed — `outputKind` determines the reference
kind, and the three kinds behave differently:

| `outputKind` | Field | Kind | Target | Real example |
|---|---|---|---|---|
| `container` | `outputRef` | **Hard** | `base-types-gen` | `"outputRef": "item.humanoid-torso-a-001"` |
| `material` | `outputRef` | **Hard** | `materials-gen` | `"outputRef": "substrate.humanoid.sound"` |
| `mutation` | — | **None** | — | reroll/enhance/temper/bore/socket operate on an owned instance |
| any | `costLines[].material` | **Hard** | `materials-gen` | `"material": "substrate.humanoid.crude"` |

A `forge`-operation (container-output) recipe generated before its `outputRef` target exists in
`base-types-gen`'s corpus is exactly the failure the owner named — *"you cannot craft something that
not exist"* — and is refused by `items validate --deps`, not caught later at import.

## Objective

A real seedsmith command authoring the 30-entry crafting-recipe corpus
(`data/seed/items/recipes/recipes.json`, read by `MaterialRecipeCatalog.Load`). Confirmed
hand-authored 2026-08-22, then **hand-PATCHED again 2026-09-05** — a human directly rewrote
`"operation": "reroll"` into `"reroll-one"`/`"reroll-all"` string literals inside the committed JSON.
That incident is exactly the failure mode this module exists to end: a schema change should regenerate
or reconcile through a real tool, not get hand-edited string-by-string in a committed file with no
record of why.

## Acceptance criteria

1. `seedsmith items generate --kind recipe` exists, brief-and-answer shape: model picks which
   verb/output pairing a recipe theme calls for; code resolves material quantities and cost numbers
   from `MaterialCatalog`'s tuning tables and `MaterialClass`'s closed vocabulary (never invents a
   material id materials-gen's vocabulary doesn't recognize).
2. The `operation` field is drawn from the CLOSED, current verb vocabulary
   (`CatalystVerbs`/`ItemWorkbench`'s own recognized operation strings) at generation time — so a future
   verb rename is a `reconcile` run against the harness, not a hand-patch commit like 2026-09-05's.
3. A reconcile run against the CURRENT 30-entry corpus, with the current verb vocabulary, either
   confirms all 30 already match it or identifies exactly the ones that don't (proving reconcile would
   have caught the 2026-09-05 drift mechanically, as its own acceptance evidence).
4. Output through `generator-harness`'s ledger.
5. A `container`-output (forge) recipe request naming a target that doesn't yet exist in
   `base-types-gen`'s corpus is refused (not authored with a dangling reference) — or, run with
   `--backfill`, triggers `base-types-gen` to mint that exact target first, per the harness's
   `plan_backfill` contract, before the recipe itself is written.

## Commands

```
python -m seedsmith items generate --kind recipe --brief <theme-file> --write
python -m seedsmith items generate --kind recipe --overwrite <id> --write
python -m seedsmith items generate --kind recipe --write   # default: reconcile the existing 30 against
                                                            # the current operation vocabulary
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/recipegen/   new package
  __init__.py, brief.py, opvocab.py (reads ItemWorkbench's real recognized operations), schema.py,
  emit.py, run.py
```

## Code style

`opvocab.py` reads the real, current operation vocabulary from its C# source of truth (whatever
`ItemWorkbench`/`SocketOperations` actually recognize) rather than hand-typing a Python copy of it —
the exact gap that let the 2026-09-05 drift happen unnoticed until a human caught and hand-fixed it.

## Testing strategy

- Reconcile test: run against the current 30-entry corpus and the current verb vocabulary; assert the
  result matches what the 2026-09-05 hand-patch already fixed (i.e., reconcile finds zero drift today,
  proving it would have caught it before the hand-patch was needed).
- A generated recipe's referenced material ids all resolve against `materials-gen`'s vocabulary.
- A `container`-output recipe naming a nonexistent target is refused by `items validate --deps`, per
  the reference manifest above; with `--backfill`, the target gets minted first and the recipe then
  succeeds.
- A `mutation`-output recipe requires no target-existence check — confirm the validator correctly
  treats `outputKind: mutation` as `None` rather than flagging a false-positive missing reference.
- Harness tests (resume/reconcile/overwrite).

## Boundaries

**Always:** resolve `operation` against the real, current C# vocabulary, never a hand-copied Python
literal list.

**Ask first:** nothing beyond the harness's own boundaries.

**Never:** hand-patch `recipes.json` directly again for a schema/vocabulary change — run reconcile
instead. If reconcile cannot express a needed change, that is this module's own gap to fix, not a
reason to hand-edit the corpus.
