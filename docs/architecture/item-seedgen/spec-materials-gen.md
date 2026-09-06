# Spec: `materials-gen`

**Module id:** `materials-gen` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Build order:** 2 of 10
**Depends on:** `generator-harness` (1)

## Objective

A real seedsmith command authoring material DISPLAY/FLAVOR content (name, icon key, flavor text) for
`data/seed/items/materials/materials.json` — confirmed hand/LLM-session-authored today with no
generator. **Explicitly excludes** `MaterialClass`/`CatalystVerbs`/`SubstrateFrames`/`SubstrateGrades` —
the small, closed, fixed C# enums (`MaterialCatalog.cs`) that define the MECHANICAL taxonomy of material
kinds. Those are a schema, not draw-able content, extending them is already `ask-first` by the enum's
own doc comment, and nothing else in this codebase generates enum names — this module never touches
them.

**Target users:** whoever authors new material flavor content going forward.

## Acceptance criteria

1. `seedsmith items generate --kind material` exists — brief-and-answer shape, model supplies name/icon
   key/flavor text for a material id the CLOSED, fixed enum vocabulary already defines (Shard/Essence
   ids sourced from `DemonRarityLadder.All`/`ElementRoster.Concrete`, per the existing audit) — the
   generator never invents a NEW material id, only authors display content for ids the fixed vocabulary
   already lists.
2. Confirms, before generating, that the target id is a real member of the closed vocabulary
   (`MaterialCatalog.cs`'s own enums/arrays) — refuses rather than authors content for an id that
   doesn't exist mechanically.
3. Output through `generator-harness`'s ledger, same append+reconcile default as every other module.

## Commands

```
python -m seedsmith items generate --kind material --brief <theme-file> --write
python -m seedsmith items generate --kind material --overwrite <id> --write
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/materialgen/   new package
  __init__.py, brief.py, vocab.py (reads the closed enum list), emit.py, run.py
```

## Code style

`vocab.py` should read `MaterialCatalog.cs`'s enum/array definitions the same way `AtomVocabCheck`
already mirrors OTHER C# registries into a JSON check-file — don't hand-duplicate the 27-id list into
Python by hand; generate the Python-side reference list the same disciplined way, or read it from a
mirror file if one exists, so the two vocabularies cannot silently drift.

## Testing strategy

- Refusal test: requesting content for an id NOT in the closed vocabulary fails loudly, never silently
  invents a new material kind.
- Harness tests (resume/reconcile/overwrite), applied to this corpus.

## Boundaries

**Always:** verify the target id against the closed C# vocabulary before authoring content for it.

**Ask first:** nothing new here — the vocabulary itself is already ask-first to extend, per existing
doc comment; this module inherits that boundary rather than restating a new one.

**Never:** generate a new material CLASS, verb, frame, or grade — those are the fixed mechanical
taxonomy this module explicitly does not touch.
