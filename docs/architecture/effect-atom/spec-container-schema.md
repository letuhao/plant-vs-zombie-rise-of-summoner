# Spec: container-schema (E5)

Module **E5** in the [atom effect map](../effect-atom-map.md). Depends on **E4**. Schema only — nothing in the game changes.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

> **This is Checkpoint B.** Once this spec is reviewed, the [action program](../action-map.md) A1 unblocks — it needs *the contract*, not the implementation. Treat the shapes below as a published interface from the moment they are approved.

## Objective

The tables that let anything own atoms: `effect_container`, `effect_container_atom`, `effect_container_pool`. A container is a named, ordered bundle of atom references, optionally with a weighted pool it rolls from.

**Containers are mechanism, not content.** This program ships the tables and the contract; items, traits, skills, species passives, and world buildings author their own rows when their specs land. We author none of it here beyond what E11's migration needs.

## Design (locked on approval)

### `effect_container`

| Column | Type | Notes |
|---|---|---|
| `container_id` | TEXT PK | |
| `container_kind` | TEXT | `item` \| `trait` \| `skill` \| `species-passive` \| `patron` \| `world-buff` |
| `slot` | TEXT | nullable; item slots |
| `rarity` | TEXT | nullable; FK to a `rarity` table with **explicit append-only ordinals**. The key budgets are looked up by |
| `min_tier` / `max_tier` | INT | nullable; **the tier window the pool may offer** — the mechanism rarity previously only claimed |
| `level_req` | INT | nullable; **enforced at bind** (`LevelTooLow`). A declared field nothing reads is the `GuardsAdjacentAlly` mistake |
| `pool_rolls` | INT | how many atoms to draw from the pool; `0` = fixed list only |
| `tags_json` | TEXT | |
| `enabled`, `revision` | INT | joins the content hash (E8) |

### `effect_container_atom` — the fixed core

| Column | Notes |
|---|---|
| `container_id` | FK |
| `seq` | resolve order, stable |
| `atom_id` | FK |
| `overrides_json` | value-spec overrides |

### `effect_container_pool` — the rolled half

| Column | Notes |
|---|---|
| `container_id` | FK |
| `atom_id` | FK — usually one tier of a family |
| `weight` | spawn weight; `0` excludes without deleting the row |
| `group` | optional — **at most one atom per group per instance** |

A container with no pool rows is a plain fixed list. Traits, skills, and species passives use the core alone; item templates roll the pool.

### The two mechanisms, and why both

**Fixed core** is determinism: a trait always contains what it says. **Weighted pool** is loot: an item template offers candidates and an instance draws from them.

`group` is PoE's mod-family rule — an item takes at most one mod per family, which is what stops a rolled item reading as `+10 atk / +12 atk / +14 atk`. 

**`group` defaults to `(family_id, variant)`, not `family_id`.** A container may therefore roll *fire* power and *ice* power — two variants of one family, normal ARPG itemisation — while never rolling two tiers of the same variant. An explicit value overrides the default, and the `pool_rolls ≤ distinct **drawable** groups` check runs **with defaults applied**, never with NULLs — a group whose every row is `weight = 0` is not drawable and does not count.

### Rarity and tier are different axes — restated because it is the easiest thing to get wrong

- **Tier** (on the atom) is *how strong* one affix is.
- **Rarity** (on the container) selects the `pool_rolls` count and the `min_tier`/`max_tier` window.

Loot rarity and capture rarity both fall out of these plus pool weights. **No third mechanism** — and that is now true rather than asserted, because the window has columns.

### Overrides are value specs, not raw numbers

`overrides_json` replaces a value spec on the referenced atom — so an override obeys the same three roll policies and the same validation as the original (E2). A container may tighten a range or change a chance; it may not introduce a param the kind does not declare, and it may not change the atom's `kind_id`.

This is what makes "the same affix at five tiers" **one family plus five rows** rather than five hand-authored atoms.

### Validation at load

Same law as E4 — a bad row is rejected whole, with its id and reason, and does not enter the catalog.

| Check | Detail |
|---|---|
| every `atom_id` resolves | else reject |
| override keys valid for the referenced atom's kind | E1 schema |
| override value specs well-formed | E2 |
| `weight ≥ 0` | negative is a rejection, not a clamp |
| `pool_rolls ≤ distinct **drawable** groups in the pool` (`HAVING max(weight) > 0`) | otherwise the draw cannot satisfy the one-per-group rule. Counting zero-weight groups passes validation and then silently under-fills: A(10), B(0), C(0) with `pool_rolls = 3` draws one atom |
| `pool_rolls > 0` requires at least one pool row | else reject |
| `seq` unique within a container | stable resolve order |
| every atom's kind supports the container's target runtime | bind-time (E6), not load-time — the same container may be legal on the lawn and rejected in battle |

### The contract action A1 consumes

Published at Checkpoint B, and stable from approval:

```text
Container  = (kind, slot?, rarity?, level_req?) + ordered atom refs + optional weighted pool
AtomRef    = atom_id + optional value-spec overrides
Instance   = a container with its OnInstantiate rolls frozen        (E6)
Binding    = an instance attached to an owner scope                  (E6)
```

What A1 may rely on: containers are ordered **for authoring**, overrides are value specs, `group` guarantees one-per-group (defaulting to family+variant), and rarity governs count and tier-window rather than magnitude. What A1 must **not** assume: that a skill's activation, cooldown, or targeting lives here. Those belong to the turn kernel and the action layer — this schema holds *what a skill contains*, never *when it fires*.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ContainerStore"
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs        (new — DDL, upsert, read; also the `rarity` table)
src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs        (new — loaded DTOs)
src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs  (new — the table above)
tests/FusionRpg.Data.Tests/ContainerStoreTests.cs
tests/FusionRpg.Core.Tests/Atoms/ContainerValidatorTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Fixed-list container round-trip | atoms return in `seq` order, every time — **`seq` is authoring order, not an execution guarantee** (definitions §0); execution order belongs to the actor's effect list |
| Pool atom outside `[min_tier, max_tier]` | `TierOutOfWindow` |
| Every pool row at `weight = 0` | `UnsatisfiablePool` — silently under-filling is the failure this program exists to remove |
| Same atom in both fixed core and pool | `DuplicateAtomInContainer` |
| `rarity` ordinals | explicit and **append-only**; a reordered rarity fails the test, same rule as elements |
| Container with no pool rows | legal; `pool_rolls` must be 0 |
| `pool_rolls` exceeding distinct groups | rejected |
| `pool_rolls = 3`, groups A(weight 10), B(0), C(0) | **rejected** `PoolRollsExceedGroups` — three groups exist but only one is drawable |
| Two pool atoms sharing a `group` | a draw returns at most one |
| Negative `weight` | rejected, not clamped |
| `weight = 0` | row kept, never drawn |
| Override naming a param the kind does not declare | rejected |
| Override changing `kind_id` | rejected |
| Override with a malformed value spec | rejected |
| Unknown `atom_id` | container rejected whole |
| Duplicate `seq` | rejected |
| Any edit | `revision` bumps; content hash changes |

## Boundaries

**Always:** reject whole rows; keep SQL in `FusionRpg.Data`; treat the §"contract" block as published once approved — changing it after Checkpoint B is a cross-program break.

**Ask first:** adding a `container_kind`; adding a column; changing the one-per-group rule or the rarity/tier split.

**Never:** author container *content* in this program beyond E11's migration needs; put activation, cooldown, or targeting in these tables; let rarity change an atom's magnitude — rarity picks count and tier, tier carries strength.
