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
| `prefix_rolls` / `suffix_rolls` | INT | **replaces the single `pool_rolls` column, 2026-09-01 (`seed-to-concrete` T0.4/T3.2).** How many atoms to draw from the prefix pool / suffix pool respectively; `0`/`0` = fixed list only. One-per-`group` applies **within each class independently** — a prefix and a suffix may share a `group` value without conflicting |
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
| `group` | optional — **at most one atom per group per instance, per affix class** |
| `affix_class` | **added 2026-09-01 (T0.4).** `prefix` \| `suffix`; selects which roll count (`prefix_rolls`/`suffix_rolls`) draws this row |

A container with no pool rows is a plain fixed list. **"Traits, skills, and species passives use the
core alone" is superseded 2026-09-01 (T0.4, `seed-to-concrete` T5.3 `species-effects`) — species
passives now roll a pool too, 0–N atoms per species by rarity band (owner: *"specie should have 0-to N
effect... lowest rarity maybe have 0-1 effect, highest rarity is 10 have 7-10 effect"*), same as an
item template. `trait.{traitId}` containers (fusion, Q10) and `skill` containers still use the core
alone — this document's original claim held for those, not for `species-passive`.** Item templates and
`species-passive` containers roll the pool.

### The two mechanisms, and why both

**Fixed core** is determinism: a trait always contains what it says. **Weighted pool** is loot: an item template offers candidates and an instance draws from them.

`group` is PoE's mod-family rule — an item takes at most one mod per family, which is what stops a rolled item reading as `+10 atk / +12 atk / +14 atk`. 

**`group` defaults to `(family_id, variant)`, not `family_id`.** A container may therefore roll *fire* power and *ice* power — two variants of one family, normal ARPG itemisation — while never rolling two tiers of the same variant. An explicit value overrides the default, and the `{prefix,suffix}_rolls ≤ distinct **drawable** groups (per class)` check runs **with defaults applied**, never with NULLs — a group whose every row is `weight = 0` is not drawable and does not count.

### Rarity and tier are different axes — restated because it is the easiest thing to get wrong

- **Tier** (on the atom) is *how strong* one affix is.
- **Rarity** (on the container) selects the `prefix_rolls`/`suffix_rolls` counts and the `min_tier`/`max_tier` window.

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
| `prefix_rolls ≤ distinct **drawable** prefix groups` and `suffix_rolls ≤ distinct **drawable** suffix groups` (`HAVING max(weight) > 0`, each `affix_class` counted separately) | otherwise the draw cannot satisfy the one-per-group rule within that class. Counting zero-weight groups passes validation and then silently under-fills: A(10), B(0), C(0) with `prefix_rolls = 3` draws one atom |
| `prefix_rolls > 0` requires at least one `prefix` pool row; `suffix_rolls > 0` requires at least one `suffix` pool row | else reject, per class |
| a **mixed bundle** (`affixClass` on the atom itself — `seed-contract.md` §2.1) consumes one prefix roll **and** one suffix roll simultaneously, never doubling either count | `seed-to-concrete` T0.6 |
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
| Container with no pool rows | legal; `prefix_rolls` and `suffix_rolls` must both be 0 |
| `prefix_rolls`/`suffix_rolls` exceeding that class's distinct groups | rejected |
| `prefix_rolls = 3`, prefix groups A(weight 10), B(0), C(0) | **rejected** `PoolRollsExceedGroups` — three groups exist but only one is drawable |
| Two pool atoms sharing a `group` **and the same `affix_class`** | a draw returns at most one |
| Two pool atoms sharing a `group`, one `prefix` one `suffix` | legal — one-per-group is scoped to the class |
| Negative `weight` | rejected, not clamped |
| `weight = 0` | row kept, never drawn |
| Override naming a param the kind does not declare | rejected |
| Override changing `kind_id` | rejected |
| Override with a malformed value spec | rejected |
| Unknown `atom_id` | container rejected whole |
| Duplicate `seq` | rejected |
| Any edit | `revision` bumps; content hash changes |
| An identical re-write | **nothing happens**. The comparison covers the child rows too: they are replaced wholesale, so a parent-column-only check would miss a changed atom list entirely (E14a) |

## Boundaries

**Always:** reject whole rows; keep SQL in `FusionRpg.Data`; treat the §"contract" block as published once approved — changing it after Checkpoint B is a cross-program break.

**Ask first:** adding a `container_kind`; adding a column; changing the one-per-group rule or the rarity/tier split.

**Never:** author container *content* in this program beyond E11's migration needs; put activation, cooldown, or targeting in these tables; let rarity change an atom's magnitude — rarity picks count and tier, tier carries strength.
