# Plan: `item-seedgen`

Source: [item-seedgen-map.md](../docs/architecture/item-seedgen-map.md) and its 10 module specs under
`docs/architecture/item-seedgen/`. Owner directive, 2026-09-07: base types, affix families, crafting
recipes, sockets, materials, drop tables, and consumables must become seedsmith-generatable — no
coding-session hand-authoring of any of them going forward. Uniques (the original 144) and the
rare-name word list stay hand-authored, matching this repo's own existing G1 decision and genre
convention (owner-confirmed). Every generator defaults to append + reconcile, never overwrite —
overwrite is an explicit, separately-invoked mode.

## Approach

Ten modules, one shared foundation. `generator-harness` (module 1) generalizes `setgen`'s already-proven
resume-ledger pattern (atomic writes, idempotent resume, per-subject-id tracking) plus one addition —
validating an existing entry's shape before trusting a ledger hit, so reconcile catches real corruption,
not just "was this id attempted." Every other module is a thin adapter over that harness: a brief
schema (model picks identity/theme only), a numeric resolver (code, never the model), and an emit step
writing through the harness.

Two modules are not new generators — they finish existing, stalled work: `set-charm-live-endpoint`
wires the already-correct `setgen`/`charmgen` machinery to a real model call (today only a hand-written
replay-transport stand-in has ever run it), and `combination-write-unblock` resolves the two named
reasons module 21's `--write` is refused outright.

## Build order and parallelism

```
Phase 0  T1-T4   generator-harness                          (blocks everything)
Phase 1  T5-T10  base-types-gen · materials-gen ·
                  set-charm-live-endpoint                    (parallel)
Phase 2  T11-T16 affix-families-gen · sockets-gen            (parallel)
Phase 3  T17-T22 recipes-gen · drop-tables-gen                (parallel)
Phase 4  T23-T27 consumables-gen · combination-write-unblock  (parallel)
```

## Gates vs. checkpoints

Only ONE genuine gate exists in this plan, and it is named exactly where it bites:
`combination-write-unblock` may require bumping `naming.v1.json`'s frozen `registryVersion` — an
architecture-locking change `decisions.md` governs (per this repo's own hard rule). That task is
ask-first by construction; nothing else in this plan blocks on it, and every other module proceeds
independently of whether/when it resolves.

Every other reversible choice (which theme a brief targards, whether a generated entry looks "right")
ships behind the harness's own dry-run/reconcile-report default — reviewable, not blocking.

## Checkpoints

- **Checkpoint A** (after Phase 0): `generator-harness`'s five acceptance criteria all pass with real
  tests — resume, reconcile-detects-corruption, overwrite-by-id, overwrite-all requires the literal
  `all`, dry-run reports without writing.
- **Checkpoint B** (after Phase 1): at least one real base-type, one real material, and one real
  live-endpoint-generated set/charm exist, each importing cleanly through the real production consumer
  (`AtomImporter`/`ItemSeedValidator`).
- **Checkpoint C** (after Phase 2): a real affix family generated end to end passes `item_role_family`
  legality; the allocated-but-unauthored `gems/2` partition has real content.
- **Checkpoint D** (after Phase 3): a reconcile run against the real 30-entry recipe corpus, using
  the module's own real operation vocabulary, confirms zero drift (proving reconcile would have caught
  the 2026-09-05 hand-patch incident before it needed a hand-patch).
- **Checkpoint E** (final, after Phase 4): a reconcile run against the real 60-entry consumable corpus
  correctly reports the known `grantsActionId`/`cooldownKey` gap; `combination-write-unblock` either
  ships real combination content or has a named, dated `decisions.md` entry blocking it — not a silent
  stall.

## What this plan does not cover

- Uniques (144) and rare-names: intentionally excluded, hand-authored by design.
- `MaterialClass`/`CatalystVerbs`: a fixed C# taxonomy, not content.
- The drop-table band→row expander: a separate, non-seedsmith tool by existing ruling.
- Running the full ~904/36/~904 set/charm/strain corpus: a separate, already-held authorization
  decision, unaffected by `set-charm-live-endpoint` merely making a live run possible.
- The item-runtime "Lawn" equip-wiring gap (module 5 of the `item` program, not `item-seedgen`) — tracked
  in `tasks/item-todo.md` instead, since it is existing-program scope.
