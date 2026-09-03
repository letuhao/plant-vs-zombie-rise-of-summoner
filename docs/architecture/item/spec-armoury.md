# Spec: `armoury`

**Module id:** `armoury` · **Program:** [item](../item-map.md) · **Build order:** 2 of 21
**Depends on:** `durable-ownership` (1)
**Rulings:** D5, D1, D26 · lane [ssot-inventory.md](ssot-inventory.md)

## Objective

One **player-scoped armoury** holding everything a player owns, with two storage grades so that
"everybody is geared" and "nobody manages 720 rows" are both true.

**Users:** every module that reads what a player has; module 20 renders it.

> **This is the feature D5 named.** The owner declined a schema patch for R1 and asked for the
> inventory instead — *"make it category and list first, reserve and share for all for now, we will
> add inventory management mini game in future."* That is [ssot-inventory.md](ssot-inventory.md)'s
> existing design, unchanged.

## Design

### The armoury, not bags

**One player-scoped store. No per-specimen bag, no bank, no stash tab, no warehouse** (§2.3). A
specimen does not *hold* items; an assignment (module 4) points from a `(specimen, role)` cell at an
item in the armoury.

The consequence is why it is the right shape: *"swap this helm onto that demon"* is a single row
update, and *"which of my 48 demons could use this?"* is one query rather than 48.

### Two storage grades — the thing that makes D1 affordable

D1 makes gear uncapped: the commander and every unique demon may wear the full set. §3.1's option D
is what keeps that from meaning 720 hand-placed rows.

| Grade | What | Storage |
|---|---|---|
| **Stock** | `pool_rolls = 0`, Fixed-only implicits — every copy indistinguishable | `rpg_item_stock`: a **counter** plus one shared canonical instance |
| **Rolled** | anything with a rolled value — unique by construction, cannot stack | one `rpg_item` row each (module 1) |

**The grade is derived, never authored** (§2.2). A row that would be stock and a row that would be
rolled are distinguished by the container's own `prefix_rolls`/`suffix_rolls`, so nothing can
mis-declare itself.

### Capacity is unlimited, and the pressure is elsewhere

§3.2: unlimited, *"with the five pressures in §2.5 and a structural ceiling that is a bug guard, not
a rule."*

⛔ **The structural ceiling must say why it is exempt.** `AGENTS.md` treats a cap on a magnitude as a
progression ceiling unless it is structural; an abuse guard on row count is structural, and the
comment is required, not optional.

⛔ **D26 forbids using capacity as a balance lever.** No inventory ceiling stands in for content
pacing. I12's *"40 items/day → a filter is required"* is a **loot-filter request** — it belongs to
module 20, and it is an interface requirement, not a cap.

### v1 surface: category and list

Owner: *"category and list first."* This module owns the **query surface** — filter, sort, page,
group by category — and module 20 owns the screen. The inventory-management minigame is deferred by
name.

### What this module does not own

| Not ours | Whose |
|---|---|
| the equip act | module 4 `equip-assign` |
| rendering any of it | module 20 `item-surfaces` |
| what an item *is* | modules 6–8 |
| salvage as an economy | module 14 (this module exposes the disposition write) |

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Armoury"
.\scripts\guard-dal.ps1
```

## Project structure

```text
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs        EDIT — rpg_item_stock, the query surface
src/FusionRpg.Core/Items/ArmouryQuery.cs           new — filter/sort/page, no SQL
src/FusionRpg.Server/ItemEndpoints.cs              new — read surface for module 20
tests/FusionRpg.Data.Tests/Items/ArmouryTests.cs   new
```

## Code style

```csharp
// Storage grade is DERIVED, never authored (ssot-inventory.md §2.2). An authored flag could disagree
// with the container it describes; a derived one cannot. Stock items are a counter because every copy
// is provably identical - that is what makes 48 specimens x 15 slots 720 cells and never 720 decisions.
static StorageGrade GradeOf(ContainerRow c) =>
    c.PrefixRolls == 0 && c.SuffixRolls == 0 ? StorageGrade.Stock : StorageGrade.Rolled;
```

## Testing strategy

| Test | Asserts |
|---|---|
| `there_is_exactly_one_armoury_per_player` | §2.3 — no per-specimen bag exists to create |
| `storage_grade_is_derived_from_the_container` | never authored, cannot disagree |
| `stock_items_are_a_counter_not_a_row_per_copy` | the property that makes D1 affordable |
| `a_rolled_item_never_stacks` | the dividing line item-ideal §7 draws |
| `swapping_an_item_between_specimens_is_one_row_update` | §2.3's stated payoff |
| `the_structural_row_ceiling_is_an_abuse_guard_and_says_so` | AGENTS.md's exemption comment, asserted |
| `capacity_is_not_used_as_a_balance_lever` | D26 — no cap stands in for pacing |
| `disposition_is_a_soft_delete_with_an_undo_window` | §4.2's tombstone, not a hard delete |

## Boundaries

**Always:** one armoury per player; derive the storage grade; keep the query surface free of SQL
(`guard-dal`).

**Ask first:** any capacity limit that is not an abuse guard.

**Never:** add a per-specimen bag — it forces a move operation between containers, and move
operations are where inventory games grow tetris (§2.3). Never use inventory pressure to regulate
drop volume (**D26**).

## Success criteria

- [ ] One player-scoped armoury, no per-specimen storage anywhere in the schema.
- [ ] Stock items are counters; rolled items are rows; the grade is derived and tested.
- [ ] The category+list query surface answers filter/sort/page without SQL leaving `FusionRpg.Data`.
- [ ] The structural row ceiling carries its exemption comment.
- [ ] No capacity or volume limit exists that a content-pacing system should own instead.
