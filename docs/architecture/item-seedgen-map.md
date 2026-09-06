# Capability map: `item-seedgen`

Seedsmith generative tooling for the item-adjacent corpora that have none today. A sibling program to
[item](item-map.md) (mechanics/runtime) and `item-content` (presentation quality) — this one is about
**authoring tooling**: how the content those two programs consume gets produced, so a coding session
never hand-types item content directly again.

## 0. Why this program exists

Audited 2026-09-07 against the real repo state, not assumed: base types (740), affix families (109),
crafting recipes (30), the gem/insert corpus, materials display content, drop-tables (468 entries), and
consumables (60) were all authored once — by an LLM-assisted authoring wave or a coding session directly
— with **zero seedsmith CLI command producing any of them**. `tools/seedsmith/seedsmith/adapters/items/`
has real, `--write`-capable generators for exactly two kinds (`set`, `charm`) and one that exists in
source but is unconditionally refused at `--write` (`combination`). Two of the seven corpora above carry
seedsmith-style `_meta` provenance fields, which look like generation but aren't — no command reproduces
them.

**Explicitly NOT in scope, confirmed by reading this repo's own prior rulings, not assumed:**
- **Uniques (the original 144) and the rare-name word list** stay hand-authored. `item-ideal.md`'s own
  G1 rules uniques hand-authored **by design** ("one unique should cost one authoring session and no
  code change"), and `rare-names.json`'s own `_meta` self-describes as *"Content, not a tunable — a
  balance pass has nothing to change here,"* drawing the Diablo 2 `RarePrefix.txt` analogy directly.
  Owner confirmed 2026-09-07: keep both hand-authored. (New uniques *beyond* the original 144 already
  run through a real, separately-approved seedsmith pipeline — `party-dungeon/spec-unique-pipeline.md` —
  untouched by this map.)
- **`MaterialClass`/`CatalystVerbs`** (the 5-value/3-value fixed C# enums) are a closed mechanical
  taxonomy, not draw-able content — nothing else in this repo generates enum names, and extending them
  is already `ask-first` by the enum's own doc comment. This map covers material *display/flavor*
  content, not the mechanical vocabulary.
- **The drop-table band→row expander** stays explicitly outside seedsmith, per `seedsmith-map.md`'s own
  existing ruling (§5, "Not the band→rows generator... a separate, later thing"). This map covers
  authoring the *symbolic* drop-table corpus (bands/curves/entry kinds), not building the expander.

## 1. The cross-cutting requirement — one harness, not eight private ones

**Owner's standing rule (2026-09-07):** every generator's default run mode is **append + reconcile**,
never overwrite. Resume continues an interrupted run. Reconcile detects gaps against what *should*
exist (a corpus grown by a schema change, a partially-written or invalid entry) and fills only those —
it does not require a bit-for-bit "already done" ledger hit to skip re-checking an entry's validity.
Selective, whole-corpus overwrite is an explicit, separately-invoked mode — never the default.

**This is not a new pattern to invent — `setgen` already has it, proven at scale.**
`adapters/items/setgen/run.py`'s own docstring: *"Resume is not optional at ~1,800 entries. The ledger
is a single JSON file keyed by subject id, written after each subject completes. `plan_run` reads it and
returns only the subjects not already done... reuses [the demon harness's] atomic file lock discipline
[so] a killed process cannot leave a half-written ledger behind."* Module 1 below generalizes this exact
mechanism (ledger shape, atomic write, idempotent resume) into a shared base every generator in this map
uses — plus the one piece `setgen`'s own ledger does not yet do: validate an existing entry's shape
before trusting a ledger hit, so a corpus corrupted by an out-of-band hand edit reconciles instead of
silently staying broken.

## 2. Modules

| # | Module id | Produces | Depends on | Build order |
|---|---|---|---|---|
| 1 | `generator-harness` | The shared resume/append/reconcile/selective-overwrite base (ledger shape, atomic write, entry-validity check) every module below is built on | — | **1 — foundation, nothing else can start first** |
| 2 | `base-types-gen` | The 740-entry base-type corpus (name, slot, level req, `socketMax`, tags) | 1 | 2 |
| 3 | `materials-gen` | Material display/flavor content (name, icon key, flavor text) — NOT the mechanical class/verb taxonomy | 1 | 2 (parallel with 2) |
| 4 | `affix-families-gen` | The 109 affix family definitions (channel, op, tier-band curve, tags) | 1, 2 (references base-type role/frame vocabulary for legality) | 3 |
| 5 | `sockets-gen` | The gem/insert corpus; resolves the allocated-but-never-authored partition found in the audit | 1, 2 (base-type `socketMax` is the real enforced source) | 3 (parallel with 4) |
| 6 | `recipes-gen` | The 30-entry crafting-recipe corpus | 1, 2, 3 (recipes reference base-type/container ids and material ids) | 4 |
| 7 | `drop-tables-gen` | The symbolic drop-table corpus (bands, curves, entry kinds) — expansion to concrete rows stays out of scope, per existing ruling | 1, 2, 4 (drop entries reference base-types and affix-eligible content) | 4 (parallel with 6) |
| 8 | `consumables-gen` | The 60-entry consumable catalog | 1, 4 (consumables reference atom families for their granted effects) | 5 |
| 9 | `set-charm-live-endpoint` | Wires the ALREADY-BUILT `setgen`/`charmgen` brief-and-answer machinery to a real model endpoint — `effects generate`/`demons generate` already have `--endpoint`/`--model`; `items generate` doesn't yet | 1 (adopts the harness's reconcile semantics for a live run, replacing the replay-transport-only path) | 2 — small, independent of 2-8, can run in parallel with the whole chain |
| 10 | `combination-write-unblock` | Unblocks module 21's `--write` refusal for the `combination` kind, completing the legacy socket-word retirement `combogen/migrate.py` already rules for | 1, 5 (replaces sockets-gen's legacy socket-word output) | 5 |

## 3. Dependency graph

```
1 generator-harness
├─► 2 base-types-gen ──┬─► 4 affix-families-gen ──┬─► 6 recipes-gen
│                       │                          ├─► 7 drop-tables-gen
│                       └─► 5 sockets-gen ──────────┴─► 10 combination-write-unblock
│                                                   └─► 8 consumables-gen
├─► 3 materials-gen ────► 6 recipes-gen
└─► 9 set-charm-live-endpoint   (independent side branch — small, parallel to everything)
```

## 4. What each module's spec must answer (per the owner's harness requirement)

Every module spec below must state, explicitly, in its own Testing strategy:
- What "append" means for this corpus (new entries added, existing ones untouched).
- What "reconcile" detects for this corpus specifically (a missing required field post-schema-change,
  an id referenced by a downstream corpus that doesn't resolve, a malformed entry) — named per-module
  because "broken" means something different for a base-type row than for a recipe row.
- The selective-overwrite invocation shape (which ids, or which filter, an explicit overwrite run takes)
  and that it is never the default flag state.

## 5. Build order, summarized

1. `generator-harness` (blocks everything)
2. `base-types-gen`, `materials-gen`, `set-charm-live-endpoint` (parallel)
3. `affix-families-gen`, `sockets-gen` (parallel)
4. `recipes-gen`, `drop-tables-gen` (parallel)
5. `consumables-gen`, `combination-write-unblock` (parallel)

Ten modules. Module specs to follow at `docs/architecture/item-seedgen/spec-<module-id>.md`, in this
order, once this map is approved.
