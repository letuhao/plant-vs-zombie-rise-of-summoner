# Capability map: `item-seedgen`

Seedsmith generative tooling for the item-adjacent corpora that have none today. A sibling program to
[item](item-map.md) (mechanics/runtime) and `item-content` (presentation quality) — this one is about
**authoring tooling**: how the content those two programs consume gets produced, so a coding session
never hand-types item content directly again.

**Audited twice** — once against the initial code/schema read (2026-09-07), then adversarially
re-reviewed on the owner's own request ("audit, debate and strengthen"). The second pass found the
first draft's dependency graph had real errors, corrected below with citations for each.

## 0. Why this program exists

Base types (740), affix families (109), crafting recipes (30), the gem/insert corpus, materials
display content, drop-tables (468 entries), and consumables (60) were all authored once with **zero
seedsmith CLI command producing any of them**. `tools/seedsmith/seedsmith/adapters/items/` has real,
`--write`-capable generators for exactly two kinds (`set`, `charm`) and one that exists in source but is
unconditionally refused at `--write` (`combination`).

**Explicitly NOT in scope, confirmed by reading this repo's own prior rulings:**
- **Uniques (the original 144) and the rare-name word list** stay hand-authored — `item-ideal.md`'s own
  G1, and `rare-names.json`'s own self-description, owner-confirmed 2026-09-07.
- **`MaterialClass`/`CatalystVerbs`** — a closed mechanical taxonomy, not draw-able content.
- **The drop-table band→row expander** — stays a separate, non-seedsmith tool per `seedsmith-map.md`'s
  own existing ruling.

## ✅ Module 11, added 2026-09-07 — the 11th corpus, resolved

`base-types-gen`'s own real content (`data/seed/items/base-types/humanoid-armament-primary-a.json:48`)
hard-references `enhancement-milestones/milestones.json`'s `runtimeFamily` via each base-type's
`enhanceTrack[]` field — a corpus outside the owner's original 10-system list. **Owner decision: fold it
in as module 11, `enhancement-milestones-gen`** (spec at
`item-seedgen/spec-enhancement-milestones-gen.md`) — a self-contained sibling to `affix-families-gen`
(authors its own `atom.enhance-*` family ids, references nothing else in this program), scheduled Phase
2 alongside it. `base-types-gen`'s `enhanceTrack[].family` reference now resolves against it.

## ✅ Family-source question, resolved 2026-09-07 — a third reference kind: `external`

Consumables' `family` and combinations' `grants` fields do not match `affix-families-gen`'s id shape.
Confirmed by reading `docs/architecture/effect-atom/atom-family-library.md:62-128`: `vitality`/
`fortitude`/`bulwark`/`warding`/`mending` are real, shipped, already-populated effect-atom families.
This is a THIRD reference kind the harness's `DependencyValidator` supports (added to its spec): an
**`external` reference** — validated like a hard reference (exact id must exist) but NEVER
auto-backfilled, since this program has no standing to author effect-atom's own content. An unresolved
one is reported as effect-atom's own gap, not guessed at.

## 1. Two cross-cutting requirements

**Resume/append/reconcile, never overwrite by default** — `generator-harness`'s `RunLedger`,
generalizing `setgen`'s proven ~1,800-entry-scale ledger, with one real gap found and closed on review:
`setgen/run.py`'s own `write_ledger` does not pass `sort_keys=True` to `json.dumps`, so it does NOT
actually guarantee byte-identical re-runs — `generator-harness` does not inherit that gap (see its own
spec, Acceptance #7).

**Dependency-ordered generation, validated deterministically, missing links auto-backfilled** — the
owner's rule, re-verified against the REAL schemas both audit passes:

| Referencing corpus | Field | Reference kind | Real evidence | Target module |
|---|---|---|---|---|
| **Base types** ⚠ new, 2nd pass | `implicit.family` | **Hard** | `humanoid-armament-primary-a.json:33`: `"family": "atom.might"` = `affix-families-gen`'s own shipped id (`g-attack.json:25`) | `affix-families-gen` |
| **Base types** ⚠ new, 2nd pass | `enhanceTrack[].family` | **Hard** ✅ resolved | `humanoid-armament-primary-a.json:48`: `"atom.enhance-edge"` = `enhancement-milestones/milestones.json:28`'s `runtimeFamily` | `enhancement-milestones-gen` (module 11) |
| Recipes (`outputKind: container`) | `outputRef` | **Hard** | `recipes.json:30`: `"outputRef": "item.humanoid-torso-a-001"` | `base-types-gen` |
| Recipes (`outputKind: material`) | `outputRef` | **Hard** | `recipes.json:126`: `"outputRef": "substrate.humanoid.sound"` | `materials-gen` |
| Recipes (any kind) | `costLines[].material` | **Hard** | `recipes.json:35`: `"substrate.humanoid.crude"` | `materials-gen` |
| Sets/charms | `members[].role`+`.frame` → persisted `members[].baseType` | **Categorical, then deterministic binding** | `setgen/schema.py:104-107` accepts the category; `setgen/seedfile.py` resolves a concrete live base-type id by stable lookup before write | `base-types-gen` (coverage), `set-charm-live-endpoint` (binding) |
| Combinations | `ingredients[]` (`supplied_families`) | **Categorical** | `combogen/schema.py:76-87`, enum at line 82 — ⚠ **corrected, 2nd pass: an earlier draft cited lines 92-98, which is actually the `grants` field below, not `ingredients`** | `sockets-gen` |
| Combinations | `hostRole` | **Categorical** | `combogen/schema.py:100-102` | `base-types-gen` |
| Combinations | `grants` (`granted_families`) | **External** ✅ resolved | `combogen/schema.py:88-98`; target confirmed below | `atom-family-library.md` |
| Consumables | `family` | **External** ✅ resolved | `k1.json`: `atom.vitality`/`atom.fortitude`/`atom.mending` — confirmed real, shipped effect-atom families, NOT `affix-families-gen`'s id shape | `atom-family-library.md:62-128` |
| **Drop-tables** ⚠ corrected, 2nd pass | `.ref` (material rows) | **Hard** | `d1.json:58`: `"ref": "essence.earth"` = a `materials-gen` id | `materials-gen` |
| **Drop-tables** ⚠ corrected, 2nd pass | `.ref` (consumable rows) | **Hard** | `d1.json:85`: `"ref": "consumable.k1-001"` | `consumables-gen` |
| **Drop-tables** ⚠ corrected, 2nd pass | `.ref` (gem rows) | **Hard** | `d1.json:437`: `"ref": "gem.g1-002"` | `sockets-gen` |
| **Drop-tables** ⚠ corrected, 2nd pass | `role`+`frame` | **Categorical** | `d1.json:37-39` | `base-types-gen` |

**⚠ The first draft of this table asserted `drop-tables-gen` depended on `affix-families-gen` — a real
audit of every file under `data/seed/items/drop-tables/` finds ZERO `atom.`-prefixed references
anywhere in that directory. That dependency was wrong; the four real ones above replace it.**

**⚠ `affix-families-gen`'s own content has no outward reference of either kind** (its `roles` field is
a shared, closed 15-role enum, not a coverage selector) — confirmed on review. It has no dependency on
anything in this program except the harness, and — per the new finding above — `base-types-gen` now
depends on IT, reversing the first draft's "run in the same parallel phase" grouping.

## 2. Modules

| # | Module id | Produces | Depends on (by module #) | Phase |
|---|---|---|---|---|
| 1 | `generator-harness` | `RunLedger` + `DependencyValidator` (hard/categorical/external resolution, deterministic backfill with a cascade guard and dedup) | — | **1 — foundation** |
| 2 | `affix-families-gen` | The 109 affix family definitions | 1 | 2 |
| 3 | `materials-gen` | Material display/flavor content | 1 | 2 (parallel) |
| 4 | `sockets-gen` | The gem/insert corpus | 1 | 2 (parallel) |
| 5 | `consumables-gen` | The 60-entry consumable catalog | 1; `family` is `external` against `atom-family-library.md` | 2 (parallel) |
| 11 | `enhancement-milestones-gen` | The enhancement-milestone corpus | 1 (self-contained, no other item-seedgen dependency) | 2 (parallel — **added 2026-09-07**, owner-approved) |
| 6 | `base-types-gen` | The 740-entry base-type corpus | 1, **2** (`implicit.family`, hard), **11** (`enhanceTrack[].family`, hard, resolved 2026-09-07) | 3 |
| 7 | `set-charm-live-endpoint` | Live-model wiring for `setgen`/`charmgen` | 1, **6** (categorical `role`+`frame`) | 4 |
| 8 | `recipes-gen` | The 30-entry crafting-recipe corpus | 1, **6** (hard, `container` outputs), **3** (hard, `material` outputs + all `costLines`) | 4 |
| 9 | `combination-write-unblock` | Unblocks module 21's `combination` `--write` refusal | 1, **6** (categorical `hostRole`), **4** (categorical `ingredients`); `grants` is `external` | 4 |
| 10 | `drop-tables-gen` | The symbolic drop-table corpus | 1, **6** (categorical), **3, 4, 5** (all hard) | **5 — last, now depends on the most other modules** |
| 12 | `fill-runner` | Safe full-corpus orchestration, terminal escalation checkpoints, deterministic depth | 1, 2–11 | **post-build operational repair** |

Twelve modules now (was ten) — module 11 is numbered for when it was found, not its build phase; it
runs in Phase 2 alongside modules 2-5. Module 12 is a post-build orchestration repair; its contract is
[`spec-fill-runner.md`](item-seedgen/spec-fill-runner.md), not a new content dependency.

## 3. Dependency graph (corrected, second pass)

Per-module upstream dependencies (module # → what it needs, per §2's table):
- `6 base-types-gen` needs `2 affix-families-gen` + `11 enhancement-milestones-gen`
- `7 set-charm-live-endpoint`, `8 recipes-gen`, `9 combination-write-unblock` each need `6 base-types-gen`
  (recipes-gen also needs `3 materials-gen`; combination-write-unblock also needs `4 sockets-gen`)
- `10 drop-tables-gen` needs `3 materials-gen` + `4 sockets-gen` + `5 consumables-gen` + `6 base-types-gen`
  (the most of any module — why it is alone in the final phase)
- `1 generator-harness` has no upstream; `2`, `3`, `4`, `5`, `11` each depend only on `1`

## 4. The validator, applied per module

Every module's own spec declares its reference manifest (hard/categorical/none, per field, per target)
and its own Testing strategy includes: a hard-reference resolution test where relevant, a
categorical-coverage test where relevant, and confirmation that a deliberately-broken reference is
caught by `items validate --deps` before import, not after.

## 5. Build order, summarized

1. `generator-harness` (blocks everything)
2. `affix-families-gen`, `materials-gen`, `sockets-gen`, `consumables-gen`, `enhancement-milestones-gen`
   (parallel — none has a confirmed dependency on another item-seedgen module's output)
3. `base-types-gen` (alone — needs phase 2's `affix-families-gen` AND `enhancement-milestones-gen`)
4. `set-charm-live-endpoint`, `recipes-gen`, `combination-write-unblock` (parallel — all need phase 3's
   `base-types-gen`, none needs the others)
5. `drop-tables-gen` (alone, last — the only module needing outputs from phases 2 AND 3)

Eleven modules, five phases.

Ten modules named, one real 11th-corpus question flagged rather than resolved unilaterally. Module
specs at `docs/architecture/item-seedgen/spec-<module-id>.md`.

---

## Filed by the `species-gear-chain` initiative (2026-09-13)

Asks raised by [species-gear-chain-map.md](species-gear-chain-map.md) and its module specs. **Nothing here is built or approved** — each is an ask-first boundary this program owns, filed so it is visible to the owner rather than living only in the requesting map.

| # | Ask | Requesting module | Evidence |
|---|---|---|---|
| 1 | ⛔ **Replace three `27` pins with a reconciliation canary** — `len(ISSUABLE) == len(MaterialCatalog.All)` | ``species-materials`` | `materialgen/vocab.py:120` and `:124` are **module-level `assert`s that hard-crash on import**; `tests/test_recipes_gen.py:193` is a red test. Widening the material vocabulary **breaks the build, not a test** |
| 2 | ⚠ **`materialgen` structurally refuses the ask today** — its own header says it authors *"`name` / `flavor` / `tags` for a material id — **never a new material id**"* | ``species-materials`` | `materialgen/__init__.py:1-10`. Ownership is **this program's** (module 3 `materials-gen`), not seedsmith's |
