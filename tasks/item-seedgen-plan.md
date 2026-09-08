# Plan: `item-seedgen`

Source: [item-seedgen-map.md](../docs/architecture/item-seedgen-map.md) and its 10 module specs under
`docs/architecture/item-seedgen/`. **Revised 2026-09-07 (second pass)** after the owner's own
"audit, debate and strengthen" request surfaced real dependency-graph errors in the first draft — see
the map's own §0/§1 for exactly what changed and why, cited against real schema evidence both times.

Owner directive: base types, affix families, crafting recipes, sockets, materials, drop tables, and
consumables must become seedsmith-generatable — no coding-session hand-authoring of any of them going
forward. Uniques (144) and rare-names stay hand-authored (owner-confirmed). Every generator defaults to
append + reconcile, never overwrite. **Generation order must respect real content dependencies** — a
recipe cannot target an item that doesn't exist, a set cannot bonus a piece slot nothing fills, a
combination cannot bind a gem family or host role nothing satisfies. The resume/reconcile validator
must be a deterministic engine that finds missing dependencies and can trigger the owning generator to
backfill them — never guessing, never silently skipping.

## ✅ All 5 checkpoints (A-E) CLOSED, 2026-09-07 — all 11 modules built and tested

Checkpoint A (`generator-harness`), B (affix-families/materials/sockets/consumables/enhancement-
milestones), C (`base-types-gen`), D (`set-charm-live-endpoint`/`recipes-gen`/`combination-write-
unblock`) and E (`drop-tables-gen`, final) all pass. Full detail, evidence, and every real finding along
the way: `tasks/item-seedgen-todo.md`. The 11th-corpus decision below (once genuinely open) is resolved:
folded in as module 11 (`enhancement-milestones-gen`), owner-decided 2026-09-07.

~~## ⛔ One decision needed before Phase 3 can finish: the 11th corpus~~ — **RESOLVED 2026-09-07**,
kept below for history.

`base-types-gen`'s real content hard-references `enhancement-milestones/milestones.json` — a corpus
outside this program's original 10 modules, found on the second audit pass. Named, not resolved:
fold it in as an 11th module, or rule it out of scope like uniques. `base-types-gen`'s own spec treats
this reference as `unresolvable: true` until decided — it does not guess.

## Approach

Ten modules (plus the flagged 11th question), one shared foundation. `generator-harness` — a `RunLedger`
(resume/append/reconcile/overwrite, generalizing `setgen`'s proven pattern, WITH a real
`sort_keys=True`-style canonical serialization `setgen`'s own code doesn't actually have) plus a
`DependencyValidator` (hard-reference resolution, categorical-coverage resolution, deterministic
backfill planning with a cascade guard and request deduplication — both added after adversarial review
found the first draft's backfill design could loop or double-request).

Two modules finish existing, stalled work rather than building from scratch: `set-charm-live-endpoint`
wires the already-correct `setgen`/`charmgen` machinery to a real model call; `combination-write-unblock`
resolves the two named reasons module 21's `--write` is refused outright.

## Build order and parallelism (corrected)

```
Phase 1  generator-harness                                    (blocks everything)
Phase 2  affix-families-gen · materials-gen · sockets-gen ·
         consumables-gen                                      (parallel — none depends on
                                                                 another item-seedgen module)
Phase 3  base-types-gen                                        (ALONE — needs affix-families-gen
                                                                 specifically, found on 2nd pass)
Phase 4  set-charm-live-endpoint · recipes-gen ·
         combination-write-unblock                             (parallel — all need base-types-gen,
                                                                 none needs the others)
Phase 5  drop-tables-gen                                        (ALONE, last — needs base-types-gen,
                                                                 materials-gen, sockets-gen AND
                                                                 consumables-gen at once)
```

**What changed from the first draft, and why it matters:** `consumables-gen` moved from phase 4 to
phase 2 (nothing in this program depends on it existing later — everything is the reverse: things
depend on IT). `base-types-gen` moved from phase 2 to its own phase 3, alone, because it needs
`affix-families-gen`'s output first. `drop-tables-gen` moved from phase 4 to a new, final phase 5,
alone, because its real dependencies (found on audit, not assumed) are materials/sockets/consumables/
base-types together — the single module needing the most upstream work finished first.

## Gates vs. checkpoints

Two genuine gates, both named exactly where they bite, neither blocking anything else in this plan:
1. The 11th-corpus decision above — blocks only `base-types-gen`'s OWN `enhanceTrack` field, not its
   `implicit.family` field or anything else in the plan.
2. `combination-write-unblock` may require bumping `naming.v1.json`'s frozen `registryVersion` — an
   architecture-locking change `decisions.md` governs. Ask-first by construction.

Every other reversible choice ships behind the harness's own dry-run/reconcile-report default.

## Checkpoints

- **Checkpoint A** (after Phase 1): `generator-harness`'s full acceptance criteria pass, INCLUDING the
  two added after review — a cascade-guard test (a backfilled entry's own unresolved reference is
  caught, not silently accepted) and a dedup test (two entries naming the same gap produce one
  backfill request) — and a determinism test that checks the actual SERIALIZED report file is
  byte-identical across two runs, not just the in-memory result.
- **Checkpoint B** (after Phase 2): real affix families, materials, gems, and consumables all exist and
  reconcile cleanly; the consumables `family`-source ask-first (generator-harness's own Design section)
  is resolved before this checkpoint closes, not deferred past it.
- **Checkpoint C** (after Phase 3): a real generated base-type's `implicit.family` resolves against
  Phase 2's real affix-family corpus; its `enhanceTrack[].family` is either resolved (if the 11th-corpus
  decision landed) or explicitly, visibly `unresolvable: true` — never silently dropped.
- **Checkpoint D** (after Phase 4): a live-generated set/charm's every member role+frame resolves
  against Phase 3's real base-types; a forge-recipe's `outputRef` resolves against the same; a
  combination's `hostRole`/`ingredients` resolve against Phase 3's base-types and Phase 2's gems.
- **Checkpoint E** (final, after Phase 5): a generated drop-table entry's four reference kinds (material,
  consumable, gem, base-type role+frame) all resolve against their real Phase 2/3 corpora. All five
  checkpoints pass together with no silently-skipped task.

## What this plan does not cover

- Uniques (144) and rare-names: hand-authored by design.
- `MaterialClass`/`CatalystVerbs`: a fixed C# taxonomy, not content.
- The drop-table band→row expander: a separate, non-seedsmith tool by existing ruling.
- The item program's own "Lawn"/"Battle" equip-wiring gap (module 5, `item` program) — tracked in
  `tasks/item-todo.md`, but see the sequencing note below: it happens BEFORE the full run.

## ✅ The full ~904/36/~904 run — approved 2026-09-07, sequenced, not immediate

Owner's own sequencing: **complete building everything first** (this program's 11 modules through
Phase 5's Checkpoint E, AND the item program's own queued Lawn/Battle equip-wiring tasks), **then** run
`classes.v1.json` v4's registry regeneration, **then** run the full set/charm/strain generative content
pass. This is the reverse of "generate content, then worry about tooling" — the owner wants the
generators and the runtime wiring both proven first, so the eventual full run draws from real,
validated, dependency-correct tooling rather than repeating the ~904-piece cost against machinery still
finding its own bugs. Do not treat this approval as authorization to run any part of it now.
