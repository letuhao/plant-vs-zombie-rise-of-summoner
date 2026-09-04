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
| **Stock** | `prefix_rolls = 0` **and** `suffix_rolls = 0`, Fixed-only implicits — every copy indistinguishable | `rpg_item_stock`: a **counter** plus one shared canonical instance |
| **Rolled** | anything with a rolled value — unique by construction, cannot stack | one `rpg_item` row each (module 1) |

**The grade is derived, never authored** (§2.2). A row that would be stock and a row that would be
rolled are distinguished by the container's own `prefix_rolls`/`suffix_rolls`, so nothing can
mis-declare itself.

⛔ **Correction — `pool_rolls` does not exist.** This spec defined the stock grade as `pool_rolls = 0`
against a column effect-pipeline T3.2 removed: `ContainerRow` carries `PrefixRolls`/`SuffixRolls`
(`ContainerRow.cs:126,129`), `RarityRow` the same (`:163`), and the content hash split them at **V9**
(`ContentHashRegistry.cs:321-322`; V1's `pool_rolls` row at `:74,104` is the historical version, kept
so old revisions still hash). The code block below was already right — the prose was not. Same defect
as item-ideal §2g #12.

### Capacity is unlimited, and the pressure is elsewhere

§3.2: unlimited, *"with the five pressures in §2.5 and a structural ceiling that is a bug guard, not
a rule."*

⛔ **The structural ceiling must say why it is exempt.** `AGENTS.md` treats a cap on a magnitude as a
progression ceiling unless it is structural; an abuse guard on row count is structural, and the
comment is required, not optional.

⛔ **D26 forbids using capacity as a balance lever.** No inventory ceiling stands in for content
pacing. I12's *"40 items/day → a filter is required"* is a **loot-filter request** — it belongs to
module 20, and it is an interface requirement, not a cap — **and per content event, not per day.**
§2f.2 restates the axis: *"I12's `20–30 items/day` imports a wall-clock axis the game does not have."*
Quoting the tripwire while leaving it phrased per day re-imports the axis the ruling removed. Where
this module needs a volume number — the virtualisation threshold, the inbox count — it is a **row
count**, which has no time axis at all; the inflow threshold is module 11's to instrument per content
event in `item_drop_log`.

### v1 surface: category and list

Owner: *"category and list first."* This module owns the **query surface** — filter, sort, page,
group by category — and module 20 owns the screen. The inventory-management minigame is deferred by
name.

### Comparison is ours — the algorithm, not the pixels

⛔ **Nothing owned it.** `spec-item-surfaces.md:227-228` calls the payload *"I13's algorithm (module 2's
surface)"*, `spec-item-card.md:69` consumes it as `CompareModel`, and this spec never mentioned it —
three documents pointing at each other (item-ideal §2h.2, blocker #4). **Claimed here.**

I13 §5.5's three signals, and **no invented scalar** — SC9 forbids depending on E9, and `power_json` is
nullable:

| Signal | Shape | Why it is honest |
|---|---|---|
| **Per-channel delta** | `[{ channel, unit, incumbent, candidate, delta }]`, `unit ∈ game-units \| resolver-points \| per-mille \| ms` | SC4: magnitudes across channel families are not comparable, so label them rather than sum them |
| **Dominance verdict** | a partial order — `strictly-better` \| `strictly-worse` \| `sidegrade` \| `incomparable` | it answers outright exactly where an answer exists; `incomparable` = the two touch disjoint channels |
| **Roll quality** | integer ‰ per atom plus the mean — where the rolled value sits in the atom's authored `[Min, Max]` **after** curve scaling | unit-free, needs nothing E9 has not shipped |

**When module 9 lands, `power_json` becomes a fourth column, never a replacement.** A single number
cannot say *what* got better, which is the whole reason the delta table exists.

⚠ **Best-in-role ranking is a different thing and must not be folded into it.** Dominance is partial;
G-D below needs a total order, so `rank = (channels where this item is the player's maximum for that
role, then rarity ordinal, then mean roll quality ‰)` exists **for protection only**, is deliberately
over-protective, and is replaced wholesale by module 9's power model (I13 §5.8).

**The three-way split, stated so it stays split:** algorithm and payload **here** · `CompareModel`, the
display model, **module 10** (`spec-item-card.md:69,267`) · layout, stacking and the squint-test glyphs
**module 20** (`spec-item-surfaces.md:222`).

### Loadouts — claimed here, applied through module 4

I13 §3.5 and §4.5 designed them; no module claimed them, and no exclusion covered them. **Claimed**,
because the library sits beside the armoury and G-C (loadout membership implies lock) is a salvage
guard, which is ours.

- `rpg_item_loadout(loadout_id PK, player_id, name, frame?, created_utc, revision)` and
  `rpg_item_loadout_entry(loadout_id, role, ref_kind, ref_id)`, PK `(loadout_id, role)`.
- **Entries validate on read, never silently drop.** An entry whose item was salvaged returns with a
  `missing` marker, so the player sees the hole.
- **Two presets wanting one item:** apply **refuses by default** with `LoadoutConflict`, listing exactly
  which cells hold what; `force = true` steals and **reports what it stripped**. Never a silent strip —
  *"why is my other demon naked"* is one silent strip away.

⚠ **Sequencing, not a header dependency:** apply writes `rpg_item_assignment`, which is **module 4's**
table, so the apply path lands with or after module 4. The library, the conflict report and G-C ship
here, and module 2 needs nothing from module 4 to ship its store or its query surface.

### Bulk actions, and the four guards that make them safe by construction

This spec carried `locked` and the soft-delete window and nothing around them. I13 §5.7's guards,
claimed:

| # | Guard | Why it is not a warning dialog |
|---|---|---|
| **G-A** | an **assigned** item is never salvageable | you cannot destroy what you are wearing |
| **G-B** | a **locked** item is never salvageable through **any** path, auto-salvage included | lock is absolute or it is decoration |
| **G-C** | **loadout membership implies lock** | a preset that quietly loses a piece is worse than a refused salvage |
| **G-D** | **best-in-role** items are excluded from bulk selections by default, **and listed as excluded** | players do not lock what they have not looked at, and that is always the item they lose |

**Preview then commit.** `POST /api/items/salvage/preview` returns the exact id list, the yield, and a
guard report — how many matched and how many each of G-A…G-D excluded, **with the excluded items
named**. Commit takes the preview's id list, so a race that adds an item between the two calls cannot
widen the selection. **Unseen items (`seen = 0`) are excluded from manual bulk salvage** unless the
player ticks *include new*.

**Auto-salvage is the same guards moved to the drop boundary**, via `rpg_item_rule` — `action ∈
auto-salvage | hide`, `predicate_json` reusing definitions §3's canonical predicate encoding (depth ≤ 4,
≤ 16 nodes, the same rejection codes). ⛔ **Do not invent a second filter grammar for the same job** —
that is exactly the second-mechanism defect SC1 exists to catch. Every path writes `rpg_item_event`,
which is what makes *"where did my item go"* answerable.

**The yield is module 14's**; the disposition write, the undo window and these four guards are ours.

### What this module does not own

| Not ours | Whose |
|---|---|
| the equip act | module 4 `equip-assign` |
| rendering any of it | module 20 `item-surfaces` |
| what an item *is* | modules 6–8 |
| salvage as an economy | module 14 (this module exposes the disposition write) |

## Data shape

⚠ **This spec created `rpg_item_stock`, `ArmouryQuery.cs` and `ItemEndpoints.cs` with no columns, no
sort keys, no page contract and no endpoint shapes.** Module 20 blocks on all four: its virtualisation
threshold and its gap board are stated against numbers this module has to serve
(`spec-item-surfaces.md:194-195`).

**Module 1 owns `rpg_item`** (`spec-durable-ownership.md:37-39`) **and module 4 owns
`rpg_item_assignment`.** The rest of I13 §4.1 is this module's.

### `rpg_item_stock` — I13 §4.3

| Column | Type | Notes |
|---|---|---|
| `player_id` | INT | PK part |
| `container_id` | TEXT | PK part — FK → `effect_container.container_id`, must be `stock_eligible` |
| `qty` | INT NOT NULL DEFAULT 0 | `≥ 0`, enforced |
| `updated_utc` | TEXT NOT NULL | |

PK `(player_id, container_id)`. **`catalog_revision` is deliberately not in the key** — stock is fungible
*because* it is standard issue. What changes across a revision is the **canonical stock instance**:
exactly one `effect_instance` per `(container_id, catalog_revision)`, `origin = 'stock'`, that every
stock assignment binds through, refreshed inside the importer's own transaction (I13 §5.6). Many
bindings may point at one instance; nothing in E6 forbids it, and it is what keeps 720 stock cells from
becoming 720 instance rows.

`stock_eligible` is a **derived** column on `effect_container` — `prefix_rolls = 0 AND suffix_rolls = 0`,
the same predicate as `GradeOf` below, never a second authored flag that could disagree with it.

`rpg_item_rule` and `rpg_item_event` take I13 §4.6 / §4.7's shapes unchanged;
`rpg_item_event.kind ∈ acquired | assigned | released | locked | unlocked | salvaged | undone | purged |
promoted | stale-flagged | stock-converted`.

### Sort keys, filters and the gap board — I13 §5.9

**Sort:** `acquired` (default, newest first) · `rarity-ordinal` · `role` · `roll-quality-milli` ·
`assigned-to` · `locked` · `unseen`. Rarity always sorts on the **ordinal**, never the label.

**Filters:** role · frame · rarity range · assigned/unassigned · locked · unseen · stale · *fits
specimen X* · *improves any specimen* · affix family present · socket count (I4) · set (I5).

**Gap board:** for each `(specimen, role)` cell one of `locked` \| `empty` \| `stock` \| `rolled`, plus
whether an unassigned **strict improvement** exists in the armoury. 48 × 15 = 720 cells, **computed,
never stored** — memoised per `(player_id, armoury revision, catalog_revision)` and invalidated on any
assignment or acquisition. Defaults to showing only cells with an available strict improvement, which is
a short list.

### Page contract

Keyset, matching the house shape (`Program.cs:369`, `RpgStore.Progression.cs:400`, which returns
`{ items, nextAfterId }` with `null` at the end): `limit` clamped server-side, and **`afterKey`, an
opaque `"<sortValue>|<instance_id>"` composite** — `rpg_item`'s PK is a hex `instance_id`, not a
monotonic integer, so `afterId` alone cannot page an `acquired_utc` sort stably. Response
`{ items, nextAfterKey }`. **Offset paging is refused**: rows arrive continuously, and an offset page
silently repeats and skips items under insertion.

### Endpoints — `ItemEndpoints.cs`

| Method | Route | Serves |
|---|---|---|
| GET | `/api/items` | the page above — `sort`, the §5.9 filters, `limit`, `afterKey` |
| GET | `/api/items/stock` | the counters |
| GET | `/api/items/gaps` | the gap board |
| POST | `/api/items/compare` | `{ incumbent, candidate }` → the three-signal payload |
| POST | `/api/items/salvage/preview` · `/commit` · `/undo` | the guard report, then the previewed id list |
| GET · PUT · DELETE | `/api/items/loadouts…` | the library; **apply** lands with module 4 |

⚠ **Module 20 declares `ItemSurfaceEndpoints.cs` — *"armoury page, gap board, compare"*
(`spec-item-surfaces.md:272`) — over three of the same reads.** Two files serving one contract is how a
surface and its data drift apart. **The data endpoints are this module's**; module 20 composes over them
and restates none of them. Flagged for the plan rather than resolved here, because it is a sequencing
call between two specs.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Armoury"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ArmouryCompare"
.\scripts\guard-dal.ps1
```

## Project structure

```text
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs        EDIT — rpg_item_stock, rpg_item_rule,
                                                     rpg_item_event, the two loadout tables
src/FusionRpg.Core/Items/ArmouryQuery.cs           new — filter/sort/keyset page, no SQL
src/FusionRpg.Core/Items/ArmouryCompare.cs         new — delta / dominance / roll quality, no scalar
src/FusionRpg.Core/Items/SalvageGuards.cs          new — G-A…G-D, preview then commit, best-in-role
src/FusionRpg.Server/ItemEndpoints.cs              new — the six routes above
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
| `no_capacity_cap_exists_outside_the_named_abuse_guard` | ⭐ D26 — a **grep-shaped guard** over `src/FusionRpg.Core/Items/` and `RpgStore.Items.cs`, allowlisting exactly one ceiling (`InventoryCeiling`, 20 000 rows). Copied from `spec-drop-volume.md:345`'s shape, which solved the identical problem |
| `disposition_is_a_soft_delete_with_an_undo_window` | §4.2's tombstone, not a hard delete |
| `the_comparison_payload_carries_delta_dominance_and_roll_quality` | I13 §5.5's three signals, all present |
| `comparison_invents_no_scalar` | SC9 — no weighted sum of magnitudes anywhere in `ArmouryCompare` |
| `dominance_is_a_partial_order` | disjoint channel sets ⇒ `incomparable`, never a guess |
| `best_in_role_is_a_protection_heuristic_not_a_score` | §5.8 — it is total where dominance is partial, and it is over-protective on purpose |
| `a_loadout_entry_whose_item_was_salvaged_returns_missing` | never a silently shorter loadout |
| `applying_a_loadout_whose_item_is_held_elsewhere_refuses_with_LoadoutConflict` | two presets, one item; `force` reports what it stripped |
| `an_assigned_or_locked_item_is_never_salvageable_through_any_path` | G-A + G-B, auto-salvage included |
| `loadout_membership_implies_lock` | G-C |
| `best_in_role_items_are_excluded_from_bulk_and_named` | G-D — the guard that prevents the actual disaster |
| `commit_salvages_exactly_the_previewed_ids` | a race between preview and commit cannot widen the selection |
| `auto_salvage_reuses_the_canonical_predicate_encoding` | SC1 — no second filter grammar |
| `the_page_contract_is_keyset_and_never_offset` | no repeat and no skip under concurrent insertion |
| `the_gap_board_is_computed_and_memoised_never_stored` | §5.9 — a query, not a store |

## Boundaries

**Always:** one armoury per player; derive the storage grade; keep the query surface free of SQL
(`guard-dal`); page by keyset; report every guard exclusion by name.

**Ask first:** any capacity limit that is not an abuse guard; a fifteenth reason code beyond I13 §6's
fourteen.

**Never:** add a per-specimen bag — it forces a move operation between containers, and move
operations are where inventory games grow tetris (§2.3). Never use inventory pressure to regulate
drop volume (**D26**). Never synthesize a comparison scalar — a naive sum of magnitudes across channel
families would be wrong *and* look authoritative, which is worse than no number (SC4, I13 §3.4).
Never silently strip a loadout's item from another cell.

## Success criteria

- [ ] One player-scoped armoury, no per-specimen storage anywhere in the schema.
- [ ] Stock items are counters; rolled items are rows; the grade is derived and tested.
- [ ] The category+list query surface answers filter/sort/page without SQL leaving `FusionRpg.Data`.
- [ ] The structural row ceiling carries its exemption comment.
- [ ] **No cap of any kind exists beyond that one — proven by the grep-shaped guard, not by review.**
- [ ] The comparison payload ships all three signals, no invented scalar, and module 20 renders it.
- [ ] The loadout library ships with validate-on-read entries and a `LoadoutConflict` that names cells.
- [ ] Bulk salvage is preview-then-commit, and G-A…G-D each refuse with the excluded items named.
- [ ] `rpg_item_stock`'s columns, the sort keys, the keyset page contract and the six routes are
      written down here, and module 20 builds against them without inventing a shape.
