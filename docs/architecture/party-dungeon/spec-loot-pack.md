# Spec: loot-pack

Status: **APPROVED by the owner 2026-09-05 (wave 3) — unbuilt.** Written against shipped code the same day; every `file:line` below was opened this session, and where a map or sibling row drifted from the code the drift is named in place. Every number is a starting shape, never a balance decision.

Module id `loot-pack`, row 11 of the [party-dungeon map](../party-dungeon-map.md) (`:121`; wave 3, `:139`, after `dungeon-loot`, before `supplies-and-objects`). Depends on `dungeon-loot` (`DropResult` rows per reveal — its spec landed mid-session, `spec-dungeon-loot.md:235-238`; the shipped `LootGrant` behind it is `LootPipeline.cs:31-52`), `delve-scope` (`rpg_delves.parties_json` per `PartyIndex`, `decisions_json`, `rpg_delve_rooms.floor_json` — `spec-delve-scope.md:72-73, :86`), `dungeon-registries` (`raid.modes.*.pack.{rows,cols}`, `pack.*` — `spec-dungeon-registries.md:120, :143`), `difficulty-ladder` (`RungTable.Get` — `spec-difficulty-ladder.md:355`), `delve-battle-profile` (log kinds `pack.move`/`pack.drop` — `spec-delve-battle-profile.md:141-144`), `delve-attrition` (rations `useContext: rest`, the wipe — `spec-delve-attrition.md:150, :255-261`). Gates **G3** (`party-dungeon-map.md:159`) and **G4** (`:160`). Ideal: [party-dungeon-ideal.md](../party-dungeon-ideal.md) §11.7 (`:1535-1608`), §11.5 (`:1322-1330`), §11.9 boxes 9/12, §11.10 R3/R11/R12. Review: [audit-2026-09-05.md](audit-2026-09-05.md) S2-6/N5 (`:216`), S2-14 (`:224`), S2-16 (`:226`), §1(e) (`:117`).

## Objective

Give a raid the one thing the armoury deliberately lacks: a bounded, visible carry limit that exists only for the length of a delve. One `PackGrid` per party per delve; footprint **derived** from role and mass class, never authored; a pure first-fit arranger so autopilot and replay agree byte for byte; carry-in before entry from the uncapped home storage into a rung-sized provisioning allowance; carry-out through placement, a floor list for what does not fit, and `pack.move`/`pack.drop` as decisions in the delve log; extraction banking the placed items through the shipped ownership and stock writes in one transaction; a wipe banking nothing.

Success looks like: `PackArranger.Arrange(grid, items)` returns the same grid for 256 shuffles of one set; a base type tagged `heavy` in `core-guard` is `3×2` everywhere with no size field in `base-types/`; a solo clear at `hard` on a domain at the pin fills about four-fifths of a `4×10` grid; 30 cells of rations at a 16-cell allowance are refused; `guard-dal`, `guard-power`, `audit-magic-numbers` green, no new §10 row.

## Locked anchors

- **Owner, verbatim** (`ideal:1535-1537`): *"where your backpack is limit so you cannot bring a whole empire items in the dungeon — imbalance."* Binding reading: *"The pack limits what you **bring in** as well as what you **carry out**."*
- **Box 9** (`:1689-1690`): *"D26 reconciliation accepted verbatim. The pack is this program's structural per-delve limit; the armoury stays uncapped."* The sentence to copy (`:1560-1564`): *"The loot pack is a structural per-delve carry limit on what a raid brings in and walks out with; it is owned by the party-dungeon program, it never touches drop volume, drop tables or the armoury, and the armoury at home stays uncapped exactly as D5 and D26 require — a haul that does not fit is a faucet that did not fire (§6), not a cap on the player."* D26 (`:1549-1551`): *"keep item system purely item generate, drop and apply to actor. we need balance item, not balance the whole game."* Code marker: `tables.v1.json:10` `_meta.noCap`.
- **Per party** (`:1573-1577`): *"2/4 parties = 2/4 packs … a shared pack across two routes is a teleporting bag. At the boss rendezvous packs stay separate and boss grants are dealt round-robin by `PartyIndex` over the manifest's grant index — deterministic, no roll."* R11 (`:1757`) keys pity per party for the same reason. **Extraction is raid-wide** (S2-16, `audit:226`): *"a party may hold at a rest, never bank."*
- **Footprint is derived** (`:1539-1548`): *"Size is DERIVED, never authored as w×h … `footprint(role, massClass)` is a DERIVED column at import … a base-type fact, never an instance roll, never on a rarity axis."* (5) (`:1586-1590`): *"Size, not weight … A scalar weight limit is a magnitude cap the player grinds to the last unit and cannot see, and it collides with the caps rule. A grid is structural, visible."*
- **Arrangement** (`:1578-1585`): first-fit decreasing by area, then grant index, then id ordinal; integer cells, no RNG, *"a pure function of `(pack, grants)`"*; manual moves are trace decisions; charms take no cells. **Box 12** (`:1695`), R5 (`audit:117`): recruits, captures and altar pulls take no cell. **Provisions occupy cells** (`:594`, `:1322-1330`; registries `:55`): `events.provisionSlots` is retired; *"Unused supplies come home and keep their cells"*; `provisionCellsDelta` is the rung's lever (`:1025-1026`).
- **The armoury ruling does not collide.** `item/ssot-inventory.md:670` (*"no capacity at all, no grid"*) and `spec-inventory-and-workshop.md:37-40` (*"no in-run inventory exists at all"*) are armoury-scoped; the pack is run-scoped and empties at extraction (`ideal:1568-1570`). `InventoryCeiling = 20_000` (`RpgStore.Items.cs:258`) is an abuse guard, not a capacity, and this module never reads it.

## Design

### 1. The grid

`PackGrid(rows, cols)` per `(delveId, partyIndex)`, created at `CreateDelve` from `raid.modes.{solo,pair,quad}.pack.{rows,cols}` (`dungeon.v1.json`, owner `dungeon-registries`, `:120`). Starting shape **4 × 10 = 40 cells in every mode** — Diablo 2's inventory (`ideal:1592`); a raid carries more by having more packs, never a bigger one — the literal mechanism of *"a raid can carry more out, and it has more to lose"* (`ideal:85`). Cells are `int` (registries §Numeric types). The grid is a **structural per-run limit, not a progression ceiling; the stash is uncapped** — that sentence is the exemption comment on the tuning `_meta` (registries `:120`) and on `PackGrid`'s constructor, per `ssot-power-scale.md` §11 PS-8 (`:778-780`). The grid is not a magnitude and never reads Θ (§8).

### 2. Footprint derivation

No base type has a size field (grep over `data/seed/items/base-types/`: zero). Every base type carries one value of the exclusive five-value `mass-class` axis (`tags.v1.json:64`, values `:73-79`; e.g. `footing/humanoid/b.json:99-118` — `"role": "footing"`, `"tags": ["medium-heavy", …]`). Roles are the closed fifteen (`ItemRole.cs:11-27`, ids `:37-56`; `standard` declared, never generated, `:29-31`). **Nothing in `src/` reads the tag** (grep `mass-class|MassClass`: zero) and there is no `item_base_type` table (`RpgStore.ItemUniques.cs:17, :39`) — the reader is this module's, the wiring gap the ideal named.

```
i     = clamp( ladderIndex(pack.footprint.role.{role}) + pack.footprint.massStep.{massClass}, 0, 5 )
cells = ShapeLadder[i]                        // ShapeLadder = [1, 2, 3, 4, 6, 8]   (structural)
shape = Orient(cells, BaseTypeSlate.LadderOf(role))
        Weapon ladder → tall  : 1×1  1×2  1×3  1×4  2×3  2×4
        Armour/Jewel/Standard → broad : 1×1  2×1  3×1  2×2  3×2  4×2      (w×h)
```

`pack.footprint.role.*` is a cell count that must be a ladder member (else refuse at load, naming the key); `massStep.*` is a step along the ladder (registries `:143` says "cells int" for both — the second is *ladder steps*, a one-word fix owed). The clamp bounds an index into a six-entry list — structural by nature and commented as such, not a magnitude clamp: a `light` jewel stays `1×1`. Orientation reuses the shipped role → class-ladder table (`BaseTypeSlate.cs:12-29`; `ClassLadder`, `FrameLean.cs:7`). Starting shapes:

| Role (ladder) | base cells | `light` −1 | `medium-light`/`medium` 0 | `medium-heavy`/`heavy` +1 |
|---|---|---|---|---|
| `armament-primary` (Weapon, tall) | 3 | 1×2 | 1×3 | 1×4 |
| `armament-secondary` (Weapon, tall) | 2 | 1×1 | 1×2 | 1×3 |
| `core-guard` (Armour, broad) | 4 | 3×1 | 2×2 | 3×2 |
| `ward-array`, `mantle` | 3 | 2×1 | 3×1 | 2×2 |
| `head-guard`, `girdle`, `footing`, `manipulator` | 2 | 1×1 | 2×1 | 3×1 |
| `sense`, `infusion`; `jewel-major`, `jewel-minor-a/b`, `retinue` | 1 | 1×1 | 1×1 | 2×1 |

`PackFootprintTable.Build(baseTypeEntries, tuning)` computes `footprint(baseTypeId)` once at load from the corpus's `role` and `tags[]`; zero or two mass-class tags, or an unknown role, refuses at load (§9). Uniques derive the same way (the axis applies to `unique`, `tags.v1.json:67-71`). A footprint is never on a grant, never rolled, never rarity-shifted.

**Non-equipment** (`DropEntryKind`, `DropTableModel.cs:16-24`): `Material` and `Insert` are `1×1` stacks; `Consumable` reads `pack.footprint.consumableClass.{restore,draught,ward,board,revive,utility}` (`ConsumableDef.cs:12-36`; `classId` is at `consumables/k1.json:28` — registries `:143` cites `:31`, drift); `Currency` is the ledger's and takes no cell; `Charm` takes no cell (`CharmCorpus.cs:37` `ApCost`; `ideal:1583-1585`); `Unique` derives as equipment; `dungeon-loot`'s `Key` kind (`spec-dungeon-loot.md:203-205`) is a `1×1` stack per `laneId`, cap 1 (structural: one key per gate), consumed by `LaneGate` as `pack.drop{by: door}`. `manifestCost` (`ConsumableDef.cs:311-321`) is the dispatch belt's unit and is not read here (`ideal:1326-1327`: *"two fields for two moments"*).

### 3. Stacks

A cell holds one stack of one fungible `container_id`; caps are `pack.stack.consumableClass.*` and `pack.stack.materialClass.{shard,substrate,essence,catalyst}` (`MaterialClass`, `MaterialCatalog.cs:11-28`; `Souls` is a ledger balance with no key — the registries schema must not require one). Caps are bounded counters, commented as such. `LootGrant.Count` is `long` (`LootPipeline.cs:35`); it splits into `ceil(count / cap)` stacks in `long` arithmetic, each placed as its own `1×1`; a remainder that does not fit goes to the floor, never truncated. Starting shapes: consumables 5/3/3/5/2/5 in class order; materials 20/20/20/10.

### 4. Provisioning — carry-in

Before `CreateDelve` commits, the player fills each party's pack from home: `rpg_item_stock` rows for fungibles (`RpgStore.Items.cs:96-101`) and un-assigned `rpg_item` rows for gear (`:80-94`). The allowance:

```
provisionCells(rung) = pack.provision.baseCells + RungTable.Get(rungId).ProvisionCellsDelta   // 0 ≤ · ≤ rows×cols, checked at load
```

Starting shape `baseCells 16` (Darkest Dungeon's sixteen shared slots, `ideal:1324`); deltas `+4 +2 0 0 0 −2 −2 −4 −4 −6` from `very-easy` to `impossible` (`hard` = 0, the identity row, `spec-difficulty-ladder.md:97`). **Drift:** registries `:145` declares `provisionCellsDelta` in `difficulty.rungs[]`; the ladder's table (`:99-110`), tunables (`:228-241`) and `RungDef` do not carry it — `RungDef` gains the column, a penalty column under R8 (never the only difference between neighbours).

This is the *"cannot bring a whole empire"* rule: carried-in footprints must sum to ≤ `provisionCells`, else `pack.over-provisioned` refuses the request naming the excess. Carry-in items are placed by §5's first-fit; the player may rearrange before entry (`pack.move`, `tick = null`). Fungibles are debited from `rpg_item_stock` at commit **after** a `qty ≤ stock` check (`pack.stock-short`), because `AdjustStock`'s `MAX(0, …)` (`RpgStore.Items.cs:302-305`) would floor a short stock silently. Gear is soft-locked by `rpg_delve_pack_lock(delve_id, instance_id UNIQUE)` (the `rpg_expedition_members` shape, `spec-expeditions.md:47`): one delve per instance; home salvage/assign paths must refuse a locked instance (row owed, §Interface). **Equipped gear does not occupy the pack** — what a demon wears is `rpg_item_assignment` (`:147-155`), projected at deploy; the pack holds un-assigned instances and stock only. The soul price of provisioning is `dungeon-loot`'s / `supplies-and-objects`' (`spec-difficulty-ladder.md:216`); this module counts cells.

### 5. Placement and auto-arrange

At each reveal `dungeon-loot` emits `DropResult(kind, refId, instanceId?, count, row, col, grantIndex)` rows (`spec-dungeon-loot.md:235`) for the clearing party — in the boss room dealt **round-robin by `PartyIndex` over `grantIndex`** (`grant i → party i mod parties`; `loot.bossGrantDistribution = round-robin`, the rule id `dungeon-registries` already carries under `loot.*`). Merchant purchases and curio drops arrive the same way (`spec-supplies-and-objects.md:185, :360`). `PackArranger.Arrange(grid, items)`: (1) sort by footprint **area descending**, then `grantIndex` ascending, then `refId` ordinal; (2) scan anchors **row-major** — `r = 0..rows−h` outer, `c = 0..cols−w` inner — and take the first where the `w×h` rectangle is free; **no rotation** (Diablo 2 has none; it doubles the search and adds a decision axis); (3) no anchor → the room's **floor list** (`rpg_delve_rooms.floor_json`, `spec-delve-scope.md:86`), in sorted order. Pure, integer-only, no RNG, no store; auto-arrange **places, never drops** (`ideal:1602`).

A placed haul instance is acquired at placement — `AcquireItem` (`RpgStore.Items.cs:265-278`), `origin_kind = "delve"`, `origin_ref = loot:delve:{id}:{r}:{c}` — and lock-rowed. **Reconciliation with `spec-dungeon-loot.md:237-238`** (*"unowned until extraction"*): the orphan sweep collects an instance with *"neither a binding nor an owner"* (`RpgStore.Items.cs:33-37`), so an unowned haul is at the sweep's mercy for the length of a delve; ownership at placement plus the lock row uses only shipped writes and keeps the item off home surfaces. A floor-left item stays unowned and is collected exactly as a drop that never fired — the reconciliation sentence, literally. Fungible haul lives only in the pack JSON until extraction.

### 6. Moves, drops and the decision log

Two decisions, appended to `rpg_delves.decisions_json` via `AppendDecision` (`spec-delve-scope.md:73, :273`) in the `{seq, kind, partyIndex, tick?, payload}` shape (`spec-delve-battle-profile.md:141-144`): `pack.move {from: grid|floor, itemKey, toRow, toCol}` — the target rectangle must be free of every cell not the item's own; from `floor` only while the party stands in that room; and `pack.drop {itemKey, qty?, by: player|autopilot|use|door, rule?}` — frees the cells; a dropped haul item joins the room's floor; a dropped carry-in item is `destroyed` at settlement; `by: use` is a supply consumed at a rest or by a verb (`spec-supplies-and-objects.md:122, :138`) and frees the cell at 0 (`ideal:1572`). `PackMoves.Apply(grid, decision)` is pure; an illegal move refuses (`pack.cell-occupied` / `pack.out-of-grid` / `pack.not-here`) and only applied decisions are logged. A move is one `UPDATE rpg_delves SET parties_json, decisions_json, revision = revision + 1` — one row, one transaction, which is why the pack lives in `parties_json` (§Structure).

**Autopilot never moves.** An un-steered party (R9) resolves its floor by the rule id `pack.autopilot.floorRule = value-per-cell` (`audit:216`): per floor item in sorted order — if a fit exists, place; else find the placed haul item with the lowest `valuePerCellMilli` whose removal alone frees a fit; if the floor item exceeds it by ≥ `pack.autopilot.swapMarginMilli`, log `pack.drop{by: autopilot, rule: value-per-cell}` and place; else leave. `valuePerCellMilli = rarityOrdinal × 1000 / cells`, `itemLevel` tiebreak — an ordering key over two ordinals (`LootGrant.RarityOrdinal`, `.ItemLevel`, `LootPipeline.cs:42-43`), never a price, never `P(Θ)`. Autopilot never drops carry-in. Rule ids are a closed list (`value-per-cell`, `leave`); an unknown id refuses at load.

### 7. Extraction and wipe

Extraction is **raid-wide** (S2-16): a party holding at a `rest` keeps its own grid and floor claims until the raid extracts; nothing banks early. `PackSettlement.Decide(packs, outcome)` is pure and returns a write list; `RpgStore.Delve.CloseDelve` (the only writer, `spec-delve-attrition.md:423`) applies it **in the same transaction** as attrition's settlement:

- **Extracted:** lock rows deleted (haul instances, already owned, become visible at home); every fungible stack upserted into `rpg_item_stock` with the statement of `RpgStore.Items.cs:300-305` inlined through `ExecIn(db, tx, …)` — `AdjustStock` opens its own transaction (`:297-309`) so it cannot be called inside `CloseDelve`'s (the `PersistLoot` precedent, `RpgStore.Loot.cs:365`, keeps every write on one `tx`); unconsumed carry-in stock returns the same way; dropped carry-in gear → `disposition = 'destroyed'` + `DeleteInstance` (`RpgStore.AtomInstances.cs:128`).
- **Wiped** (`spec-delve-attrition.md:255-261`; R3 `ideal:1750`): **the haul banks nothing** — haul rows `destroyed`, instances deleted (as `spec-dungeon-loot.md:238` states), haul stacks discarded, locks deleted. Carry-in gear and unconsumed carry-in stock **return home**: the ideal's stated risk is the haul and the eaten provisions (`:1322-1330`, `:1574`); making home gear a wipe sink would turn the pack into a second armoury sink the item program did not agree to (D26) — an ask-first row if wanted. R12's kept boss grant left through `dungeon-loot` at the clear and is not in this ledger.

### 8. D26 reconciliation — the metric, and count-pin over Θ-scaled cells

Drop **volume** is linear in `Θ_actor`: `scale‰ = 1000 + 25·(Θ − 20)`, floored at 100 (`DropVolume.cs:35-42`; `item-drop-volume.v1.json:6-9`; §10 row 28, `ssot-power-scale.md:641`) — ×1.0 at the pin, ×2.0 at Θ 60, ×3.0 at Θ 100. The map (`:174`) asks this module to choose between a Θ-scaled cell count and an explicit count-pin (N5, `audit:216`).

**Ruling: count-pin. The grid does not read Θ.** (1) A grid growing with Θ is an `f(Θ)` in a subsystem — a new §10 row in a closed inventory — and it re-admits *"the whole empire"* exactly where hauls become valuable. (2) Row 28 chose linear volume *because* the management minigame was deferred; the pack **is** that minigame, so volume outrunning the grid at high Θ is the intended pressure. (3) Past the pin the choice becomes value-per-cell (Dark and Darker, `ideal:1598`) — the game the owner asked for. The reconciliation sentence holds: tables fire in full, the floor is *"a faucet that did not fire"*, the armoury never sees a cap.

**The metric** (`PackFill.Estimate`, pure): `fillMilli = 1000 × Σ_rooms E[grants_room(Θ)] × meanCells / (rows×cols − provisionCells)`, with `E[grants_room] = Σ_groups rolls × scale‰(Θ) / 1000` over the room's `DropTableRow` and `meanCells` the footprint table's role-budget-weighted mean (≈ 2.4 with §2's shapes). Illustrative arithmetic with room-table `rolls` **assumed** (they are `dungeon-loot`'s): a solo path of five `fight` (1), one `elite` (2), one `cache` (3), one `boss` (4) at Θ 20 → 14 grants ≈ 34 cells against 40 − 16 = 24 haul cells → ~1400‰ before provisions are eaten, **~850‰ once the sixteen provisioning cells are consumed** — a full clear at the identity rung fills about four-fifths of the grid; at Θ 60 the same path yields ~68 cells and two-fifths of the drops face the value-per-cell choice. The count pins at ~Θ 20–25 for this shape. The regression test computes `fillMilli` from the **shipped** tables and asserts it inside `pack.fillBand.identity.{min,max}Milli` (700–1000); a balance pass moves it through `rolls` on the loot tables, never through `rows × cols`.

### 9. Refusals

At load or at the request boundary, none silent: a `pack.footprint.role.*` key not naming one of the fifteen roles, or a role missing a key; a `massStep` key outside the five tag values; a base type with zero or two mass-class tags; a base cell count not on the ladder; a footprint larger than the grid in either dimension (`pack.footprint-exceeds-grid`, naming the base type — never truncated); `provisionCells` outside `[0, rows×cols]` for any rung; a stack cap < 1; an unknown autopilot rule id; a grant whose role fails `ItemRoles.TryParse` (`ItemRole.cs:58-64`) — `pack.unknown-role`, left on the floor **with the refusal in the floor entry**, so a corpus defect is visible in play and in the log rather than a missing item.

### 10. Determinism

`Arrange`, `Apply`, `Decide`, `Estimate` and the autopilot rule are pure functions over integers — no `System.Random`, no clock, no store; the sort is total (area, grant index, `refId` under `StringComparison.Ordinal`), so two hosts agree on every tie. `(delve seed, decisions_json, every room's battle trace)` is the whole run (`spec-delve-battle-profile.md:144`): replaying the `pack.*` entries over the reveals re-derives every party's grid and floor byte for byte — the property G3 and G4 assert.

## Tunables

All in `data/tuning/dungeon.v1.json`; schema and T5 loader are `dungeon-registries`' (`:165-175`), so new keys enter through that spec. A missing key is a load rejection. Every value is a starting shape.

| Key | Unit / type | Class | Owner | Starting shape |
|---|---|---|---|---|
| `raid.modes.{solo,pair,quad}.pack.{rows,cols}` | cells int | **S** — `_meta.structural[]`: *structural per-run limit, not a progression ceiling; the stash is uncapped* | registries `:120` (read) | 4 × 10, all three |
| `pack.footprint.role.{fifteen roles}` | cells int ∈ {1,2,3,4,6,8} | T | registries `:143` (read) | §2 table |
| `pack.footprint.massStep.{five mass classes}` | ladder steps int (unit fix owed on `:143`) | T | registries (read) | −1 0 0 +1 +1 |
| `pack.footprint.consumableClass.{six}` · `pack.stack.consumableClass.{six}` | cells int · count int | T | registries (read) | 1 each · 5 3 3 5 2 5 |
| `pack.stack.materialClass.{shard,substrate,essence,catalyst}` | count int (no `souls` key) | T | registries (read) | 20 20 20 10 |
| `difficulty.rungs[].provisionCellsDelta` | cells int; `hard` = 0 | T (penalty column) | registries `:145` (read); `RungDef` column owed by the ladder | +4 +2 0 0 0 −2 −2 −4 −4 −6 |
| **new** `pack.provision.baseCells` | cells int ≤ rows×cols | T | this module via registries | 16 |
| **new** `pack.autopilot.floorRule` · `.swapMarginMilli` | rule id ∈ {`value-per-cell`,`leave`} · ‰ long | T | this module | `value-per-cell` · 250 |
| `loot.bossGrantDistribution` | rule id ∈ {`round-robin`} | T | registries `:144` (read — not a new key; the brief's `pack.` spelling is retired) | `round-robin` |
| **new** `pack.fillBand.identity.{min,max}Milli` | ‰ long (regression band) | T | this module | 700 · 1000 |

Structural constants in code, each with the exemption comment: `ShapeLadder = [1,2,3,4,6,8]` and its two orientations (the closed list of legal rectangles); the row-major scan order; the key stack cap of 1.

## Numeric types

`rows`, `cols`, `r`, `c`, `w`, `h`, cell counts, stack caps, `partyIndex` are `int` — bounded by the grid or the ladder (registries §Numeric types: *"`int` for bands, counts, rows, cells, rungs, Θ deltas"*). Stack quantities are `long` (`LootGrant.Count`, `LootPipeline.cs:35`); the split is `checked` long arithmetic and only the resulting stack **count** narrows to cells, after the fit check. Per-mille tunables are `long`. **This module holds no magnitude of its own**: rarity ordinal and item level pass through as the pipeline's `int`s; a price is `dungeon-loot`'s `long` and is not read here. No `float`, no `double`, no `System.Random`.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Pack"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Delve"
.\scripts\guard-dal.ps1                              # every new SQL string is in RpgStore.Delve.cs
.\scripts\guard-power.ps1                            # no f(Θ) under Delve/Pack
python scripts\audit-magic-numbers.py --targets M1   # Pack files are Rules-shaped: no bare literals
```

## Structure

```
src/FusionRpg.Core/Delve/Pack/
  PackGrid.cs · Footprint.cs (ShapeLadder, Orient, Derive) · PackFootprintTable.cs (Build at load; refusals)
  PackArranger.cs (Arrange — pure) · PackMoves.cs (Apply, Replay) · PackAutopilot.cs (FloorRule.ValuePerCell)
  PackProvisioning.cs (Validate carry-in) · PackSettlement.cs (Decide → write list) · PackFill.cs (D26 metric)
  PackDto.cs (rows, cols, cells[], floor[], provisionCellsLeft — the wave-5 layer's read model)
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs   (delve-scope's file, :273) gains parties_json[p].pack read/write,
                                              EnsureDelvePackLockUnlocked (rpg_delve_pack_lock), Lock/Unlock,
                                              and the pack half of CloseDelve on the same tx
tests/FusionRpg.Core.Tests/Delve/Pack/ · tests/FusionRpg.Data.Tests/Delve/
UNTOUCHED: LootPipeline.cs, DropVolume.cs, RpgStore.Items.cs, RpgStore.Loot.cs, ItemRole.cs, the base-type corpus
```

**Persistence: `parties_json`, plus one lock table.** `delve-scope` put per-party state on the header as one JSON array (`spec-delve-scope.md:92-94`: *"`loot-pack` splits it out if it ever needs to query a party alone"*). It does not: every read is by delve, and every write is a move or a reveal that also appends a decision to the **same row**, so one `UPDATE` carries grid and log together and a half-applied move cannot exist. The element is `{rows, cols, cells: [{r, c, w, h, kind, refId, qty, origin: carryIn|haul}]}` per `PartyIndex`. The lock table exists because the *item* side needs a `UNIQUE(instance_id)` net JSON cannot give — the reason `rpg_expedition_members` is a table (`spec-expeditions.md:47`). `RpgStore.cs:747`'s reset list gains it ahead of `rpg_delves`.

## Code style

Pure functions over records; catalog discipline (unknown → throw at load); integer-only; the store partial mirrors `RpgStore.World.cs` (gate-serialized, one transaction, revision bump); no SQL outside Data.

```csharp
public static class Footprint
{
    // STRUCTURAL: the closed list of legal rectangles as cell counts — not a balance number; a seventh
    // shape is a code change with a comment, never a tuning edit. massStep steps along this index.
    static readonly int[] ShapeLadder = { 1, 2, 3, 4, 6, 8 };

    /// <summary>footprint(role, massClass) — a base-type fact derived at load, never authored, never rolled.</summary>
    public static (int W, int H) Derive(ItemRole role, string massClass, PackTuning t)
    {
        var i = Array.IndexOf(ShapeLadder, t.FootprintRoleCells(role)) + t.MassStep(massClass); // both refuse at load
        i = Math.Clamp(i, 0, ShapeLadder.Length - 1);   // index into a six-entry list: structural bound, not a magnitude clamp
        var tall = BaseTypeSlate.LadderOf(ItemRoles.Id(role)) == ClassLadder.Weapon;
        var cells = ShapeLadder[i];
        return cells == 1 ? (1, 1) : tall ? Tall[cells] : Broad[cells];   // Tall/Broad: the two orientation tables of §2
    }
}

public static class PackArranger
{
    /// <summary>First-fit decreasing. Pure: same grid + same items ⇒ same result on every runtime.</summary>
    public static (PackGrid Grid, IReadOnlyList<PackItem> Floor) Arrange(PackGrid grid, IReadOnlyList<PackItem> items)
    {
        var floor = new List<PackItem>();
        foreach (var item in items.OrderByDescending(x => x.W * x.H).ThenBy(x => x.GrantIndex).ThenBy(x => x.RefId, StringComparer.Ordinal))
        {
            if (item.W > grid.Cols || item.H > grid.Rows)
                throw new PackRejection($"pack.footprint-exceeds-grid: '{item.RefId}' is {item.W}x{item.H} on {grid.Cols}x{grid.Rows}");
            var placed = false;
            for (var r = 0; r <= grid.Rows - item.H && !placed; r++)
                for (var c = 0; c <= grid.Cols - item.W && !placed; c++)
                    if (grid.IsFree(r, c, item.W, item.H)) { grid = grid.With(item, r, c); placed = true; }
            if (!placed) floor.Add(item);
        }
        return (grid, floor);
    }
}
```

## Testing strategy (gates G3/G4, `party-dungeon-map.md:159-160`)

- **Property — order-free arrangement:** 256 shuffles of one grant set arrange to the identical grid and floor; replaying the `pack.*` log over the same reveals reproduces the grid.
- **Footprint table equals the derivation:** every corpus base type's entry equals `Footprint.Derive`; every (role, mass class) pair lands on the ladder; the §2 table is asserted row by row; zero or two mass-class tags refuse naming the id; `standard` in a grant refuses `pack.unknown-role`.
- **Provisioning refuses over-capacity:** 17 cells at a 16-cell allowance → `pack.over-provisioned`; 16 → accepted; a rung whose `provisionCells` exceeds the grid refuses at load; `qty` above stock → `pack.stock-short`, nothing written.
- **Extraction banks exactly the placed items:** haul stacks reach `rpg_item_stock` by exact `qty`, every haul row is unlocked, floor items are never stocked, unconsumed carry-in returns, dropped carry-in is `destroyed`; one transaction (a failure mid-list leaves the pre-close state). **Wipe banks nothing:** haul rows `destroyed`, instances gone, carry-in returned. **Raid-wide:** two parties, one holding at a rest — nothing stocked until the raid closes; boss grants round-robin by `PartyIndex`.
- **Autopilot:** never emits `pack.move`; emits `pack.drop{by: autopilot}` only past the margin; never drops carry-in; an unknown rule id refuses at load.
- **`the_pack_never_reads_armoury_capacity`:** a source scan over `Core/Delve/Pack/` and the pack half of `RpgStore.Delve.cs` for `InventoryCeiling` / `CountArmouryRows` (the never-Step guard's shape, `spec-delve-scope.md:180-181`).
- **D26 regression:** `PackFill.Estimate` over the shipped tables at the identity rung and Θ 20 lands inside `pack.fillBand.identity`, printing the number. **Guards** green; §10 unchanged.

## Boundaries

- **Always:** derive footprint at load from role and mass class; one pure arranger; log every applied move and drop; grids per party; settle at `CloseDelve` on one transaction; refuse at load naming the key; carry-in only through the lock table and the stock statement.
- **Ask first:** carry-in gear as a wipe loss; a second autopilot rule; rotation; per-mode grid sizes that differ; a pack table beyond the lock rows; a `disposition` value beyond the item program's four (`RpgStore.Items.cs:61-63`); changing which `DropEntryKind`s take cells.
- **Never:** a footprint authored on an item, base type or grant; a grid that grows with progression — soft caps reconcile *magnitudes* to endless grind, a grid is structural, and scaling it on Θ is a new power-shaped scale that dissolves the owner's limit exactly where it matters; equipped gear in the pack; a shared raid pack; autopilot moving items; `System.Random`, a clock or a store read inside `Core/Delve/Pack/`; reading `InventoryCeiling`; a weight scalar.

## Success criteria

1. G3's *"loot into the pack"* clause on a solo autopilot run and G4's *"per-party packs"* clause on a 4-party raid, byte-identical on replay. 2. §Testing green, `the_pack_never_reads_armoury_capacity` included. 3. `ssot-power-scale.md` §10 unchanged; `guard-power` green. 4. Zero size fields in `data/seed/items/`. 5. The owed fixes propagated (registries `:143` unit and `k1.json:28`; `RungDef.ProvisionCellsDelta`; the item program's lock check).

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `Pack.Place(partyIndex, DropResult[])` → `Arrange` over the live grid; floor list back | **`dungeon-loot`** (reveals, boss round-robin), **`supplies-and-objects`** (merchant sales, curio drops, `VerbResolver` `Place` — `:185, :360`) |
| `PackFootprintTable.Build(...)`; `.ForConsumable(classId)` | **`supplies-and-objects`** — `sizeBand`/`stackBand` derived from `pack.footprint/stack.consumableClass.*` (`:85-86`), never authored |
| `Pack.Consume(partyIndex, containerId, qty)` → `pack.drop{by: use}`; a `Key` stack readable by `LaneGate` | **`delve-attrition`** `RestResolver` (rations `useContext: rest`); **`delve-scope`** doors |
| `PackDto` (no `PartyIndex` in a label — the stage names parties) and the two decision payloads | **`delve-stage`** (wave 5): the band-2 pack layer (`decisions.md:113`; `design/information-architecture.md:113-118`) owns the grid UI, this module the model. Today the web renders no armoury: `StoragePage.tsx:3-12` is run/archive storage, `/api/items/armoury` (`ItemSurfaceEndpoints.cs:72-106`) has no web caller and its `ArmouryRowDto` hard-codes `Assigned: false` (`:83`) |
| `PackSettlement.Decide` write list; `rpg_delve_pack_lock` | `RpgStore.Delve.CloseDelve` (the only writer) |
| **Row owed to the item program** (`item-map.md`): *"salvage, transfer, assign and bulk paths refuse an instance in `rpg_delve_pack_lock` (`pack.carried`); the armoury listing hides or badges it. Spec: `party-dungeon/spec-loot-pack.md` §4."* | — |
| **Fixes owed to siblings:** registries `:143` massStep unit → *ladder steps*, `k1.json:31` → `:28`; ladder `RungDef` gains `ProvisionCellsDelta`; `spec-dungeon-loot.md:237` *"unowned until extraction"* → owned at placement (§5) | `dungeon-registries`, `difficulty-ladder`, `dungeon-loot` |

## Design-gate checklist

```
[x] Subsystems: item ownership/stock (DAL), loot pipeline output, delve store, decision log, caps register,
    tunables, power ladder (§10 closed), Game GUI bands.
[x] Read this session: party-dungeon-map.md (row 11, gates, external deps, :174); registries, scope, ladder,
    battle-profile §4a and attrition in full (seed-contract, graph-roll, encounter by reference); ideal §1,
    §4.5-4.6, §6 row, §8, §11.5, §11.7-11.11; audit §1(e), S1-1, S2-6/14/15/16, §5; decisions.md:113-116;
    DESIGN-GATE §1/§5; ssot-power-scale.md §10 row 28, §11; ssot-inventory.md:670;
    spec-inventory-and-workshop.md §1; design/information-architecture.md §2.4a; spec-expeditions.md.
[x] Code cited by file:line, opened today: RpgStore.Items.cs (:33-37, :61-63, :80-101, :147-155, :258,
    :265-278, :297-309), RpgStore.AtomInstances.cs (:62-71, :128), RpgStore.Loot.cs (:376-443),
    RpgStore.ItemUniques.cs (:17, :39), PredicateNode.cs (:11-18), LootPipeline.cs (:31-52, :134, :192),
    DropTableModel.cs (:16-24), DropVolume.cs (:35-42), item-drop-volume.v1.json (:6-9), ItemRole.cs,
    BaseTypeSlate.cs (:12-29), FrameLean.cs (:7), ConsumableDef.cs (:12-56, :311-321), MaterialCatalog.cs
    (:11-28), CharmCorpus.cs (:37), tags.v1.json (:64, :67-79), k1.json (:28), footing/humanoid/b.json
    (:99-118), ItemSurfaceEndpoints.cs (:31-36, :72-106), StoragePage.tsx (:1-12).
[x] Drift reported: k1.json classId :28 not :31; massStep unit; RungDef lacks provisionCellsDelta; mass-class
    has zero readers in src/; no item_base_type table; AdjustStock floors at MAX(0); the web has no armoury
    view; information-architecture.md lives under docs/design/; dungeon-loot's "unowned until extraction".
    Sibling specs (scope, registries) moved on disk during this session; the line numbers above are the
    post-move ones.
[ ] spec-dungeon-loot.md and spec-supplies-and-objects.md landed mid-session; their pack-facing lines
    (:203-205, :235-238; :85-86, :122, :138, :185, :360) were read and reconciled above, the rest was not.
[ ] The §8 arithmetic assumes room-table `rolls` (dungeon-loot's); the test reads shipped tables.
[ ] No Diablo 2 inventory research file exists under docs/research/genre-mechanics/ (01-09 checked); the
    prior-art numbers are the ideal's own (:1592-1602), not re-searched.
[ ] Nothing was run — no code exists; "one transaction" and "no hashed row moves" are argued from the store's
    shape, and the first build task proves them.
[x] No §2 invariant contradicted: no cap on a magnitude, no f(Θ), SQL only in Data, no engine words in
    player-facing DTO fields, Foundation untouched.
[x] Corrections propagated within this spec (Design, Tunables, Structure, Testing, Boundaries, Interface).
```
