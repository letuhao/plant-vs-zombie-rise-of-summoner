# Spec: `legion-cargo`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `legion-cargo`, row 1 of the
[scoped-inventory-hierarchy map](../scoped-inventory-hierarchy-map.md) (wave 1, no dependency — builds
in parallel with `sector-storage`). Ideal: [scoped-inventory-hierarchy-ideal.md](../scoped-inventory-hierarchy-ideal.md)
§Legion scope, Open question #1 (RESOLVED). Decisions: [decisions.md](../decisions.md) "Scoped
inventory hierarchy SSOT (2026-09-13)".

## Objective

A legion (`WorldEntity`) gains its own item cargo — a bounded, visible carry limit distinct from the
empire's uncapped stash — gated by **both** a slot count and a total weight, aggregated from **every**
member regardless of role (owner's explicit choice: not `Bearer`-only, unlike the existing loam-carry
precedent). Because troops are HoMM-style headcount (`WorldEntityMember`, never a `UniqueActor` row),
no per-unit value is ever displayed or individually assigned — only the legion's **totals** are real.
Per-unit `carryWeight`/`backslot` are a **flat tunable default today**, standing in for a real
per-species stat that does not exist yet (no seedsmith unit pipeline — confirmed this session, real gap,
named and left for its own future work). This module also ships same-empire legion-to-legion cargo
transfer: a single-transaction move, never a copy, gated on both legions sharing one `player_id`.

Success looks like: a 5-member legion's cargo grid holds exactly `5 × slotsPerUnit` slots and
`5 × weightPerUnit` total weight, computed fresh from `rpg_world_entity_members`' live count — never
cached, never drifting if a member is added or lost; moving an item from legion A's cargo to legion B's
(same `player_id`) leaves A with one fewer item and B with exactly one more, in one transaction, never
both or neither.

## Locked anchors

- **One ownership root, this module is an overlay, never a second one** (decisions.md, Scoped
  inventory hierarchy SSOT): `rpg_item`/`rpg_item_stock` (`player_id`-keyed) stay the single place an
  item is *owned*. Legion cargo is a **reachability + capacity** layer on top — "this item is currently
  aboard entity E" — never a second `player_id`-shaped column implying dual ownership.
- **Every member counts, not just `Bearer`s (owner-confirmed, correcting this session's own first
  instinct).** `WorldEntityMemberRole.Fighter`/`Bearer` already exists and already gates *loam*-carry
  capacity (`LegionSupply.BearerCount`/`CarryPerBearer`, `LegionSupply.cs:16-21`) — this module was
  initially drafted to mirror that exact shape, but the owner explicitly chose a **separate** rule for
  item cargo: all members contribute, not `Bearer`s only. The two capacities (loam-carry, item-cargo)
  are deliberately independent numbers with independent rules, not one generalized to the other.
- **Move, never copy** — the same discipline `corpse-cache` already proved (*"A real death moves, never
  copies"*, `spec-corpse-cache.md:240-241`) applies to legion-to-legion transfer identically: delete
  from A's overlay, insert into B's, one transaction.
- **The per-unit stat is a placeholder, not a promise.** The owner's own words: *"every unit have carry
  weight and backslot that cannot view because troop stack is not unique demon. we will add to
  seedsmith unit pipeline later."* This module ships a flat tunable default now; a later, separate
  program adds the real per-species generator. Nothing here blocks on that program, and nothing here
  should be built in a way that requires revisiting when it lands (the per-unit value is read from one
  tunable key today, a per-species table tomorrow — the aggregation formula does not change).

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `WorldEntity` (legion header) — `EntityId`, `Kind`, `OwnerFactionId`, `AtSectorId`, `Members` | `src/FusionRpg.Core/World/WorldState.cs:286-326` |
| `WorldEntityMember` — `InstanceId?`, `SpeciesId`, `Level`, `Hp`, `Wounds`, `Role` (`Fighter`/`Bearer`) | `WorldState.cs:269-284` |
| `rpg_world_entities` (legion header row) — relational table, `PRIMARY KEY (world_id, entity_id)` | `src/FusionRpg.Data/Sqlite/RpgStore.World.cs:92-106` |
| `rpg_world_entity_members` (one row per member) — relational, `PRIMARY KEY (world_id, entity_id, member_index)` | `RpgStore.World.cs:121-131` |
| `CarriedLoam` — an **entity-level** pool, not per-member (*"members carry as a crew, not as individual sacks"*) — the existing precedent for "a legion carries an aggregate resource" | `WorldState.cs:321-325` |
| `LegionSupply.BearerCount`/`CarryPerBearer` — the existing `role-filtered-count × per-unit-tunable` aggregation pattern for loam (**not reused here** — see Locked anchors) | `src/FusionRpg.Core/World/Loam/LegionSupply.cs:16-21`; `LoamPolicy.cs:102` |
| `rpg_item`/`rpg_item_stock` — the ownership root this module overlays, never duplicates | `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs:85-107` |
| `corpse-cache`'s move-never-copy precedent, already spec'd this session | `docs/architecture/deployment-hierarchy/spec-corpse-cache.md:240-241` |

### Real gap

| Gap | What would have to be built |
|---|---|
| No per-unit `carryWeight`/`backslot` field anywhere | Confirmed this session — `WorldEntityMember` has exactly six fields (`InstanceId, SpeciesId, Level, Hp, Wounds, Role`, `WorldState.cs:275-284`), none carry-related. This module reads a **flat tunable default** instead (§Design 1) — the per-species field is a named, deferred future addition, not this module's job. |
| No seedsmith "unit" pipeline | Confirmed this session — `tools/seedsmith/seedsmith/adapters/` has `items`, `demons`, `creatures`, `actions`, `effects`, `_stub`; nothing named `units`/`troops`. The `creatures` adapter's one `CREATURE` `KindSpec` (`tools/seedsmith/seedsmith/adapters/creatures/kinds.py:17-27`) carries PvZ lawn-card flavor/combat fields (`sunCost`, `cooldownSec`, `hp`, `attack`...), not empire troop logistics — confirms there is no existing generator this module could extend even if it wanted per-species values now. Named as its own future real gap, not this module's scope. |
| No legion-scoped item table of any kind | `rpg_item`/`rpg_item_stock` have no entity/legion column (`RpgStore.Items.cs:85-107`, confirmed). This module's own new table (§Design 2) is the first one. |

## Design

### 1. Capacity — computed fresh, never cached

```
memberCount(entityId)   = SELECT COUNT(*) FROM rpg_world_entity_members WHERE world_id=$w AND entity_id=$e
weightCapacity(entity)  = memberCount × tuning.CargoWeightPerUnit     // long, per-mille-free flat value
slotCapacity(entity)    = memberCount × tuning.CargoSlotsPerUnit      // long
```

Both read live off `rpg_world_entity_members`' row count — never stored redundantly on the
`rpg_world_entities` header, so a member joining or dying (already-existing world-turn mechanics this
module does not touch) changes capacity automatically with no second write. This mirrors
`LegionSupply.CarryCapacity`'s own shape (`entity.Members.Count(...) * tuning value`) precisely, just
over **all** members instead of a role-filtered subset (Locked anchors).

### 2. New table — `rpg_world_entity_cargo`

```sql
CREATE TABLE IF NOT EXISTS rpg_world_entity_cargo (
  world_id     TEXT NOT NULL,
  entity_id    TEXT NOT NULL,
  seq          INTEGER NOT NULL,       -- insertion order, stable id within this legion's cargo
  kind         TEXT NOT NULL,          -- 'instance' | 'stack' — same two-kind split as rpg_corpse_cache_item
  instance_id  TEXT,                   -- kind='instance': rpg_item.instance_id (rolled gear, relics)
  container_id TEXT,                   -- kind='stack': fungible container id
  qty          INTEGER,                -- kind='stack' only; long, never negative
  weight_each  INTEGER NOT NULL,       -- long, snapshotted at load time (see §4 — never re-derived per read)
  PRIMARY KEY (world_id, entity_id, seq),
  FOREIGN KEY (world_id, entity_id) REFERENCES rpg_world_entities(world_id, entity_id) ON DELETE CASCADE
);
```

Same `kind ∈ {instance, stack}` split `corpse-cache` already uses (`spec-corpse-cache.md` §Design 1) —
deliberate consistency, not a coincidence: both are "what's aboard a place/entity that isn't the empire
stash" overlays over the same `rpg_item`/`rpg_item_stock` root. `rpg_item.player_id` and
`rpg_item.disposition` are **never touched** by loading cargo — an item is "aboard a legion" purely by
existing in this table with no corresponding armoury-listing row, the identical "reachable by owner,
not by a second column" shape `rpg_item`'s own design already uses (`RpgStore.Items.cs:34-38`).

### 3. Load and unload — the empire-stash boundary

`LoadCargoUnlocked(worldId, entityId, playerId, refKind, refId, qty?)`: the item/stack must currently be
**unassigned and owned by `playerId`** (an armoury-resident `rpg_item` row, or `rpg_item_stock` with
`qty ≥` requested) — refuses `cargo.not-owned` otherwise. Deletes/decrements the stash-side row and
inserts a `rpg_world_entity_cargo` row in the **same transaction**, refusing `cargo.over-weight` /
`cargo.no-slots` if the load would exceed either capacity gate (§1) — checked **before** any write,
never a partial load. `UnloadCargoUnlocked` is the exact reverse: delete the cargo row, restore
ownership to the armoury (an `rpg_item_stock` increment, or clearing whatever "aboard entity E" marker
kept the `rpg_item` row out of `ListAssignments`-style listings).

### 4. Weight snapshot, not a live join

`weight_each` is captured **once, at load time**, from the item's base-type-derived weight (an existing
DERIVED item field, same shape as `item/seed-contract.md`'s "an author may never type a magnitude"
family — this module reads it, never authors it). Capacity checks sum this stored column, never
re-resolving the base-type weight per read — a base-type's weight is not expected to change after
generation, and even if a future balance pass changed it, an already-loaded legion's own manifest
should not silently reflow.

### 5. Legion-to-legion transfer

```
TransferCargoUnlocked(worldId, fromEntityId, toEntityId, seq, playerId):
  read the fromEntityId row at $seq; refuse cargo.not-found if absent
  refuse cargo.cross-empire if either entity's OwnerFactionId does not resolve to playerId's own faction
  refuse cargo.over-weight / cargo.no-slots on the destination (same §1 capacity check)
  delete the row from fromEntityId, insert an equivalent row (new seq) under toEntityId — one transaction
```

Same-`player_id` only (Locked anchors: cross-faction item transfer is a real gap this program does not
build — `scoped-inventory-hierarchy-ideal.md` Open question #3/#6). `OwnerFactionId` is a `string`
(`WorldState.cs:290`); resolving it to a `player_id` for the refusal check reads the existing
`WorldFaction`/`FactionKindCatalog` model (`FactionKindCatalog.cs:7-18`), never the closed `CommanderId`
enum (decisions.md, Scoped inventory hierarchy SSOT).

## Tunables

| Number | Owner | Notes |
|---|---|---|
| `CargoWeightPerUnit` (flat, per member, regardless of role) | `data/tuning/scoped-inventory.v1.json` | Placeholder for a real per-species stat (real gap, deferred); `long` |
| `CargoSlotsPerUnit` (flat, per member) | same file | Same placeholder status; `int` is sufficient (a legion's realistic member count is small, structurally bounded — not a magnitude in the overflow sense) |
| Legion-to-legion transfer cost, if any | same file | Not yet decided whether a same-empire move costs anything beyond being adjacent/reachable — open, a spec-time content call, not blocking this module's build |

## Numeric types

`weight_each`, `weightCapacity`, and any summed weight total are `long` (an item's derived weight is a
magnitude — `CLAUDE.md` "Numeric overflow" rule 1: `long` for any magnitude a `contentScale` touch can
reach). `qty` (stack count) is `long`, matching `corpse-cache`'s own precedent for the identical field.
`seq`/`memberCount`/`slotCapacity` are `int` — bounded by a legion's realistic size, a structural bound
commented as such, never a progression-scaled magnitude.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~LegionCargo"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~World.Legion"
.\scripts\guard-dal.ps1        # every new SQL string lives in FusionRpg.Data
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.LegionCargo.cs   NEW — schema (§Design 2), LoadCargoUnlocked,
                                                     UnloadCargoUnlocked, TransferCargoUnlocked,
                                                     WeightCapacityUnlocked/SlotCapacityUnlocked
tests/FusionRpg.Data.Tests/LegionCargo/              NEW
UNTOUCHED: rpg_item/rpg_item_stock schema, WorldState.cs, WorldEntity/WorldEntityMember records,
           LegionSupply.cs (loam-carry stays its own, separate mechanism)
```

## Code style

```csharp
// Capacity is always computed fresh — never stored, never drifts from the live member count.
public long WeightCapacityUnlocked(SqliteConnection db, string worldId, string entityId)
{
    var count = MemberCountUnlocked(db, worldId, entityId);
    return count * ScopedInventoryTuning.Current.CargoWeightPerUnit;   // widen-before-multiply: count is int, tuning value is long
}
```

## Testing strategy

- **Capacity tracks live member count:** a legion's weight/slot capacity changes the instant a member
  is added or removed from `rpg_world_entity_members` — no second write, no cached total to go stale.
- **Load refuses over capacity:** loading an item whose weight would exceed remaining capacity refuses
  `cargo.over-weight`, nothing written; same for `cargo.no-slots`.
- **Load refuses non-owned items:** an item not owned by the requesting player, or already aboard
  another entity/assigned to a specimen, refuses `cargo.not-owned`.
- **Transfer is atomic:** a legion-to-legion transfer either fully succeeds (source loses the row,
  destination gains it) or fully fails (both unchanged) — no interrupted-transaction state is ever
  observable.
- **Cross-empire transfer refuses:** a transfer where the two legions' `OwnerFactionId`s resolve to
  different `player_id`s refuses `cargo.cross-empire`.
- **Weight snapshot never re-derives:** changing a base type's authored weight after an item is already
  loaded does not change that item's `weight_each` in an already-loaded cargo row.

## Boundaries

- **Always:** capacity check before any write, never after; move (delete+insert) inside one
  transaction for every transfer/unload; snapshot `weight_each` at load time.
- **Ask first:** any change to `rpg_item`/`rpg_item_stock`'s own schema; reusing `LegionSupply`'s
  `Bearer`-only aggregation for this module's capacity (explicitly rejected by the owner, Locked
  anchors) — if this is ever revisited, it needs a fresh decision, not a silent merge.
- **Never:** a second `player_id`-shaped ownership column on cargo rows; a live re-join to a base
  type's weight field on every capacity check; cross-faction transfer (real gap, tracked separately).

## Success criteria

1. A legion's cargo capacity is always `memberCount × tuning value`, provably live. 2. Load/unload/
   transfer are each single-transaction, provably atomic under a forced-failure test. 3. Cross-empire
   transfer refuses. 4. `guard-dal.ps1` green. 5. Zero changes to `LegionSupply.cs`'s existing
   `Bearer`-only loam-carry mechanism.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `rpg_world_entity_cargo` schema, `(world_id, entity_id)` keying | `cargo-transfer` (module 3) — the deposit/withdraw verb reads/writes this exact table when moving items between a legion and a sector |
| `LoadCargoUnlocked`/`UnloadCargoUnlocked` | `cargo-transfer` (module 3) — reused directly for the legion-side half of a sector deposit/withdraw, not reimplemented |
| `(world_id, entity_id, seq)` row shape | `cargo-fate` (module 4) — a destroyed legion's cargo rows are exactly what module 4 moves into a corpse-cache-shaped revisit-lootable place |

## Design-gate checklist

```
[x] Subsystems: world-map legion state (Core), item ownership (Data) — no Status/ActorHub/Combat
    subsystem touched.
[x] Read this session: scoped-inventory-hierarchy-ideal.md §Legion scope; scoped-inventory-hierarchy-map.md
    row 1; decisions.md "Scoped inventory hierarchy SSOT (2026-09-13)".
[x] Code cited by file:line, opened this session: WorldState.cs (:269-326), RpgStore.World.cs
    (:92-131), LegionSupply.cs (:16-21), LoamPolicy.cs (:102), RpgStore.Items.cs (:34-38, :85-107),
    tools/seedsmith/seedsmith/adapters/ (directory listing), creatures/kinds.py (:17-27).
[x] Drift reported: none — the ideal doc's citations for this module's grounding all matched code
    exactly on a fresh open this session. One correction made mid-session: the module's first-instinct
    design (reuse `Bearer`-only aggregation) was explicitly overridden by the owner in favor of
    all-members — recorded in Locked anchors, not silently applied.
[ ] The exact "restore ownership to the armoury" mechanic on unload (§Design 3) was not traced to a
    specific existing function this session — named as implementation's first task, not assumed solved.
[x] No §2 invariant contradicted: SQL only in FusionRpg.Data; no cap on a magnitude (capacity is a
    structural per-legion limit, not a progression ceiling — exempt, commented as such); no `f(Θ)`; no
    second ActorHub composer; no second ownership root.
```
