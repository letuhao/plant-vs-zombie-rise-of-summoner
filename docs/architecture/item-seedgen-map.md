# Capability map: `item-seedgen`

Seedsmith generative tooling for the item-adjacent corpora that have none today. A sibling program to
[item](item-map.md) (mechanics/runtime) and `item-content` (presentation quality) — this one is about
**authoring tooling**: how the content those two programs consume gets produced, so a coding session
never hand-types item content directly again.

## 0. Why this program exists

Audited 2026-09-07 against the real repo state: base types (740), affix families (109), crafting
recipes (30), the gem/insert corpus, materials display content, drop-tables (468 entries), and
consumables (60) were all authored once with **zero seedsmith CLI command producing any of them**.
`tools/seedsmith/seedsmith/adapters/items/` has real, `--write`-capable generators for exactly two kinds
(`set`, `charm`) and one that exists in source but is unconditionally refused at `--write`
(`combination`).

**Explicitly NOT in scope, confirmed by reading this repo's own prior rulings:**
- **Uniques (the original 144) and the rare-name word list** stay hand-authored — `item-ideal.md`'s own
  G1, and `rare-names.json`'s own self-description, owner-confirmed 2026-09-07.
- **`MaterialClass`/`CatalystVerbs`** — a closed mechanical taxonomy, not draw-able content.
- **The drop-table band→row expander** — stays a separate, non-seedsmith tool per `seedsmith-map.md`'s
  own existing ruling.

## 1. Two cross-cutting requirements

**Resume/append/reconcile, never overwrite by default** (owner's standing rule) — `generator-harness`'s
`RunLedger`, generalizing `setgen`'s already-proven ~1,800-entry-scale ledger.

**Dependency-ordered generation, validated deterministically, missing links auto-backfilled** (owner's
2026-09-07 rule, after the real content shapes were audited): *"crafting recipe... only generate after
have crafting target"*, *"set bonus... cannot have bonus for item that not exist"*, *"same... strain"*.
Audited against the REAL schemas, not assumed — the reference shape differs per corpus, and the
dependency graph below reflects that difference precisely:

| Referencing corpus | Field | Reference kind | Real evidence | Target module |
|---|---|---|---|---|
| Recipes (`outputKind: container`) | `outputRef` | **Hard** — exact id | `recipes.json:30`: `"outputRef": "item.humanoid-torso-a-001"` | `base-types-gen` |
| Recipes (`outputKind: material`) | `outputRef` | **Hard** — exact id | `recipes.json:126`: `"outputRef": "substrate.humanoid.sound"` | `materials-gen` |
| Recipes (`outputKind: mutation`) | — | **None** — operates on an owned instance | `recipes.json`: reroll/enhance/temper/bore/socket entries carry no `outputRef` | — |
| Recipes (any kind) | `costLines[].material` | **Hard** — exact id | `recipes.json:35`: `"material": "substrate.humanoid.crude"` | `materials-gen` |
| Sets/charms | `members[].role` + `.frame` | **Categorical** — ≥1 real entry must satisfy the combination | `setgen/schema.py:104-107`: `role`/`frame` are enums, never a container id | `base-types-gen` |
| Combinations | `ingredients[]` | **Categorical** — a gem family, ≥1 real gem must satisfy it | `combogen/schema.py:76-98`: `ingredients` enum is a gem-family list, not an id | `sockets-gen` |
| Combinations | `hostRole` | **Categorical** | `combogen/schema.py:100-102`: an enum of roles whose socket ceiling fits | `base-types-gen` |
| Consumables | `family` | **Unresolved — ask first** | `k1.json`: `atom.vitality`/`atom.fortitude`/`atom.mending` — do NOT match this program's own `affix-families-gen` id shape (`atom.ferocity`-style) | Likely effect-atom's own corpus, not this program's — see `generator-harness`'s own Design section |
| Combinations | `granted_families` | **Unresolved — same question as consumables' `family`** | `combogen/schema.py:51,92-98`: parameter supplied by the caller, source not yet confirmed | Same open question |

**Consequence for build order:** `set-charm-live-endpoint` and `combination-write-unblock` are NOT
independent side branches (the original draft of this map placed them that way, before this audit) —
both have real categorical dependencies on `base-types-gen`, and `combination-write-unblock` additionally
depends on `sockets-gen`. Fixed below.

## 2. Modules

| # | Module id | Produces | Depends on | Build order |
|---|---|---|---|---|
| 1 | `generator-harness` | `RunLedger` (resume/append/reconcile/overwrite) + `DependencyValidator` (hard/categorical reference resolution + deterministic backfill planning) | — | **1 — foundation** |
| 2 | `base-types-gen` | The 740-entry base-type corpus (name, slot, level req, `socketMax`, role, frame, tags) | 1 | 2 |
| 3 | `materials-gen` | Material display/flavor content — not the mechanical taxonomy | 1 | 2 (parallel with 2) |
| 4 | `affix-families-gen` | The 109 affix family definitions | 1 | 2 (parallel — no real content dependency on base-types found; `item_role_family` legality is a separate, downstream matrix module 8 already owns, not this generator's own gate) |
| 5 | `sockets-gen` | The gem/insert corpus | 1 | 2 (parallel — gems are element/universal, not base-type-specific) |
| 6 | `set-charm-live-endpoint` | Live-model wiring for the already-built `setgen`/`charmgen` machinery | 1, **2 (moved — sets/charms' `(role, frame)` members are a categorical reference into base-types-gen's output; running before base-types coverage exists means every generated set risks an unfillable member)** | 3 |
| 7 | `recipes-gen` | The 30-entry crafting-recipe corpus | 1, 2, 3 (hard references, both output kinds) | 3 (parallel with 6) |
| 8 | `drop-tables-gen` | The symbolic drop-table corpus | 1, 2, 4 | 4 |
| 9 | `consumables-gen` | The 60-entry consumable catalog | 1, **ask-first on the real `family` source (table above) — do not build against `affix-families-gen` on the assumption they're the same vocabulary** | 4 (parallel with 8, pending the ask-first resolution) |
| 10 | `combination-write-unblock` | Unblocks module 21's `combination` `--write` refusal | 1, **2, 5 (moved — `hostRole`/`ingredients` are categorical references into base-types-gen and sockets-gen)**, and the same `granted_families` open question as consumables | 4 |

## 3. Dependency graph (corrected)

```
1 generator-harness
├─► 2 base-types-gen ──┬─► 6 set-charm-live-endpoint
│                       ├─► 7 recipes-gen ◄── 3 materials-gen
│                       ├─► 8 drop-tables-gen ◄── 4 affix-families-gen
│                       └─► 10 combination-write-unblock ◄── 5 sockets-gen
├─► 3 materials-gen
├─► 4 affix-families-gen
├─► 5 sockets-gen
└─► 9 consumables-gen   (ask-first on its real family-source dependency before this edge is final)
```

## 4. The validator, applied per module

Every module's own spec must declare its reference manifest (per `generator-harness`'s Design section)
and its own module's Testing strategy must include: a hard-reference resolution test where relevant, a
categorical-coverage test where relevant, and confirmation that a deliberately-broken reference is
caught by `items validate --deps` — not just by that module's own local schema check.

## 5. Build order, summarized

1. `generator-harness` (blocks everything)
2. `base-types-gen`, `materials-gen`, `affix-families-gen`, `sockets-gen` (parallel — none references
   another item-seedgen corpus for its OWN content, per the table in §1)
3. `set-charm-live-endpoint`, `recipes-gen` (parallel — both need phase 2's outputs to resolve real
   references, neither needs the other)
4. `drop-tables-gen`, `consumables-gen`, `combination-write-unblock` (parallel)

Ten modules, four phases (was five — `set-charm-live-endpoint` and `recipes-gen` collapse into one
phase now that their real dependencies are both satisfied by the end of phase 2). Module specs at
`docs/architecture/item-seedgen/spec-<module-id>.md`.
