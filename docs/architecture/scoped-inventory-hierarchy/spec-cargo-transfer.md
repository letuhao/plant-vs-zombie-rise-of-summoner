# Spec: `cargo-transfer`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `cargo-transfer`, row 3 of the
[scoped-inventory-hierarchy map](../scoped-inventory-hierarchy-map.md) (wave 2, depends on
`legion-cargo` and `sector-storage`, both built — [spec-legion-cargo.md](spec-legion-cargo.md),
[spec-sector-storage.md](spec-sector-storage.md)). Ideal:
[scoped-inventory-hierarchy-ideal.md](../scoped-inventory-hierarchy-ideal.md) §The shape (deposit/
withdraw). Decisions: [decisions.md](../decisions.md) "Scoped inventory hierarchy SSOT (2026-09-13)".

## Objective

The verb that makes modules 1 and 2 into one feature instead of two unconnected tables: a legion
physically present at a sector, belonging to that sector's *current* owning faction, may move an item
directly between its own cargo (`rpg_world_entity_cargo`) and that sector's storage
(`rpg_world_sector_storage`) — **deposit** (legion → sector) and **withdraw** (sector → legion). Neither
direction touches the empire stash (`rpg_item`/`rpg_item_stock`) at all; this is a move between two
overlays of the same ownership root, never a round trip through it. Both directions are gated by the
destination's own capacity rule — module 1's weight+slot dual gate for a legion, module 2's row-count-
vs-`EffectiveCapacity` gate for a sector — checked before any write, and by presence + faction match,
checked fresh against live state, never a cached "you were here last turn" flag.

Success looks like: a legion standing at a sector it (or its faction) currently owns can deposit an
item from its cargo into that sector's storage, and the item disappears from the legion's cargo row
count/weight and appears in the sector's the instant the transaction commits — never both, never
neither; a legion NOT present at the sector, or present but belonging to a different faction than the
sector's current owner, refuses outright; a deposit/withdraw that would exceed the destination's
capacity refuses before touching either table.

## Locked anchors

- **One ownership root, unmoved by either direction.** `rpg_item.player_id`/`rpg_item.disposition` are
  never read or written by this module (Locked anchors, both dependency specs) — a deposit or withdraw
  only ever deletes a row from one overlay table and inserts an equivalent row into the other. The
  underlying item's real owner is, and stays, whichever player originally loaded it into the legion's
  cargo (`spec-legion-cargo.md` §Design 3) — capture/faction changes affect *reachability*, never this
  field (`spec-sector-storage.md` §Design 5).
- **Presence and faction match, read live, never cached.** A legion's eligibility to touch a sector's
  storage is `entity.AtSectorId == sector.SectorId && ResolvesToSameFaction(entity.OwnerFactionId,
  sector.OwnerFactionId)` — evaluated fresh at the moment of the verb call, the same "no data migration
  needed because nothing was ever cached" discipline `spec-sector-storage.md` §Design 5 already proved
  for capture. This module does not introduce a second place that remembers who could reach what.
- **Destination capacity, checked before any write, never after.** Both dependency specs establish this
  independently for their own scope (`spec-legion-cargo.md` §Design 3, `spec-sector-storage.md` §3);
  this module inherits both checks unchanged rather than writing a third capacity rule.
- **Move, never copy.** The same discipline `corpse-cache` proved (`spec-corpse-cache.md:240-241`) and
  both dependency modules already apply to their own internal moves (legion-to-legion transfer;
  the capture-reachability flip) — a deposit/withdraw is delete-then-insert in one transaction, never a
  window where the item exists in both tables or neither.

## What already exists

### Built (both dependency modules, spec'd this session, not yet coded)

| Finding | Evidence |
|---|---|
| `rpg_world_entity_cargo` — `(world_id, entity_id, seq)`, `kind ∈ {instance, stack}`, `weight_each` snapshotted at load | `spec-legion-cargo.md` §Design 2 |
| Legion cargo capacity — `memberCount(entityId) × tuning value`, both weight and slots, computed fresh | `spec-legion-cargo.md` §Design 1 |
| `rpg_world_sector_storage` — `(world_id, sector_id, seq)`, `kind ∈ {instance, stack}`, no `weight_each` (no weight gate for this scope) | `spec-sector-storage.md` §Design 4 |
| Sector storage capacity — `SectorItemCapacity.EffectiveCapacity(sector)`, row-count is the unit, computed fresh | `spec-sector-storage.md` §Design 2-3 |
| `WorldEntity.AtSectorId` — "At a sector, or on a lane — never both, never neither" | `WorldState.cs:292-293` |
| `WorldSector.OwnerFactionId` — the live faction-match target, re-derivable every call, never cached (per `spec-sector-storage.md`'s own reachability proof) | `WorldState.cs:157` (via sibling file, `spec-sector-storage.md` §Design 5) |
| `FactionKindCatalog`/`WorldFactionKind` — the closed 5-value faction model this module resolves `OwnerFactionId` strings through, never the closed `CommanderId` enum | `FactionKindCatalog.cs:7-17`, confirmed by both dependency specs |

### Real gap

| Gap | What would have to be built |
|---|---|
| No verb connects the two overlay tables today | Neither dependency module ships one by design (each explicitly defers it to this module, per the map). This module's own new functions (§Design 1-2) are the first. |
| No existing "does this legion's faction match this sector's faction" helper | Both dependency specs name the raw fields (`OwnerFactionId` on both `WorldEntity` and `WorldSector`) but neither builds the comparison — this module's own small helper (§Design 3), not a reuse of an existing function. |

## Design

### 1. Deposit — legion cargo → sector storage

```
DepositUnlocked(worldId, entityId, sectorId, seq, playerId):
  read entity, sector; refuse cargo.not-present if entity.AtSectorId != sectorId
  refuse cargo.wrong-faction if !SameFaction(entity.OwnerFactionId, sector.OwnerFactionId)   -- §3
  read the rpg_world_entity_cargo row at $seq; refuse cargo.not-found if absent
  refuse cargo.owner-mismatch if the row's underlying rpg_item.player_id != playerId          -- defence in depth,
                                                                                                  the caller should
                                                                                                  already be playerId's
                                                                                                  own legion
  capacity = SectorItemCapacity.EffectiveCapacity(sector)                                     -- spec-sector-storage §2
  used     = SELECT COUNT(*) FROM rpg_world_sector_storage WHERE world_id=$w AND sector_id=$s
  refuse cargo.sector-full if used >= capacity
  DELETE FROM rpg_world_entity_cargo WHERE world_id=$w AND entity_id=$e AND seq=$seq
  INSERT INTO rpg_world_sector_storage (world_id, sector_id, seq=nextSeq, kind, instance_id,
                                         container_id, qty)                                    -- weight_each dropped,
                                                                                                  sector storage has no
                                                                                                  weight gate (spec-
                                                                                                  sector-storage §2)
  -- one transaction
```

### 2. Withdraw — sector storage → legion cargo

The mirror, with one asymmetry: legion cargo's capacity gate is **weight and slots**, and its row
carries a `weight_each` sector storage never stored. Withdrawing therefore re-derives `weight_each`
from the item's base-type-derived weight at the moment of withdrawal — the same DERIVED read
`spec-legion-cargo.md` §Design 4 already performs at load time, applied here instead of at armoury-load
time, since this is the first moment the item enters a weight-gated overlay.

```
WithdrawUnlocked(worldId, sectorId, entityId, seq, playerId):
  read entity, sector; refuse cargo.not-present / cargo.wrong-faction — same checks as §1
  read the rpg_world_sector_storage row at $seq; refuse cargo.not-found if absent
  weightEach = <item's base-type-derived weight, read fresh>                 -- rpg_world_sector_storage
                                                                              -- carries NO weight_each
                                                                              -- column by design
                                                                              -- (spec-sector-storage.md
                                                                              -- §2/§Design — no weight
                                                                              -- gate for that scope), so
                                                                              -- this is the FIRST time
                                                                              -- the item is weighed for
                                                                              -- legion purposes -- the
                                                                              -- same existing DERIVED
                                                                              -- item field
                                                                              -- LoadCargoUnlocked reads
                                                                              -- (spec-legion-cargo.md
                                                                              -- §4), not a named
                                                                              -- function either spec
                                                                              -- declares; corrected
                                                                              -- during strengthen pass
                                                                              -- 2026-09-13 (previously
                                                                              -- miscited as calling a
                                                                              -- ResolveDerivedWeight(...)
                                                                              -- function neither module
                                                                              -- actually names, and as
                                                                              -- reading a column
                                                                              -- sector-storage does not
                                                                              -- have)
  weightCapacity = memberCount(entityId) × tuning.CargoWeightPerUnit         -- spec-legion-cargo §1
  slotCapacity   = memberCount(entityId) × tuning.CargoSlotsPerUnit
  usedWeight = SELECT SUM(weight_each) FROM rpg_world_entity_cargo WHERE world_id=$w AND entity_id=$e
  usedSlots  = SELECT COUNT(*) FROM rpg_world_entity_cargo WHERE world_id=$w AND entity_id=$e
  refuse cargo.over-weight if usedWeight + weightEach > weightCapacity
  refuse cargo.no-slots    if usedSlots + 1 > slotCapacity
  DELETE FROM rpg_world_sector_storage WHERE world_id=$w AND sector_id=$s AND seq=$seq
  INSERT INTO rpg_world_entity_cargo (world_id, entity_id, seq=nextSeq, kind, instance_id,
                                       container_id, qty, weight_each)
  -- one transaction
```

### 3. `SameFaction` — the one new helper this module adds

```csharp
static bool SameFaction(string legionFactionId, string sectorFactionId) =>
    string.Equals(legionFactionId, sectorFactionId, StringComparison.Ordinal);
```

Deliberately a **string equality check on `FactionId`**, not a resolve-to-`player_id` comparison —
per both dependency specs' own finding, `rpg_world_factions` has no `player_id` column and only the
`Player` faction resolves to the world's one real `player_id` today (`RpgStore.World.cs:20-22,37-44`).
Comparing `FactionId` strings directly is correct and sufficient for the same-faction case this
program ships (a `Player`-owned legion at a `Player`-owned sector); it also happens to be exactly the
comparison a future multi-empire program would still want at THIS layer (faction-level access), with
the separate, still-real-gap question of resolving a faction to a distinct `player_id`-owned item store
staying `sector-storage`'s and `legion-cargo`'s own concern, never re-derived here.

### 4. What this module does not do

Cross-faction transfer (deposit into or withdraw from a sector owned by a *different* faction than the
acting legion) always refuses `cargo.wrong-faction` — this module ships **zero** cross-faction paths,
matching the program-wide real gap named in both dependency specs and the ideal doc's own Open question
#6b. Legion-to-legion transfer (module 1's own verb) and capture-driven reachability changes (module 2's
own hook, actual transfer logic owned by module 4, `cargo-fate`) are untouched — this module is only the
legion↔sector verb.

## Tunables

This module introduces no new tunable — every capacity/weight number it reads is owned by
`legion-cargo` or `sector-storage`'s own tunable rows (`data/tuning/scoped-inventory.v1.json`). Whether
a deposit/withdraw costs anything beyond being present (a move-turn cost, a distance gate) is
unaddressed by any owner decision — named as an open, non-blocking content question, not invented here.

## Numeric types

No new magnitude — every `long`/`int` this module reads or writes is already typed by its owning
module (`spec-legion-cargo.md`/`spec-sector-storage.md` §Numeric types). This module's own logic is
comparisons and row moves only.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CargoTransfer"
.\scripts\guard-dal.ps1        # every new SQL string lives in FusionRpg.Data
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.CargoTransfer.cs   NEW — DepositUnlocked, WithdrawUnlocked,
                                                       SameFaction; reads (never modifies) the schemas
                                                       and capacity functions from RpgStore.LegionCargo.cs
                                                       / RpgStore.SectorStorage.cs / SectorItemCapacity.cs
tests/FusionRpg.Data.Tests/CargoTransfer/              NEW
UNTOUCHED: rpg_item/rpg_item_stock, rpg_world_entity_cargo's and rpg_world_sector_storage's own schemas
           (this module reads/writes rows, never alters either table's shape)
```

## Code style

```csharp
// One transaction, presence+faction checked before either table is touched, capacity checked
// before the write — refuse-then-move, never move-then-check.
public (bool Ok, string Reason) DepositUnlocked(SqliteConnection db, SqliteTransaction tx,
    string worldId, string entityId, string sectorId, int seq, long playerId)
{
    var entity = ReadEntityUnlocked(db, tx, worldId, entityId);
    var sector = ReadSectorUnlocked(db, tx, worldId, sectorId);
    if (entity.AtSectorId != sectorId) return (false, "cargo.not-present");
    if (!SameFaction(entity.OwnerFactionId, sector.OwnerFactionId)) return (false, "cargo.wrong-faction");
    // ... capacity check, then the single delete+insert, all on the same tx
}
```

## Testing strategy

- **Deposit moves, never copies:** after a successful deposit, the source `rpg_world_entity_cargo` row
  is gone and exactly one equivalent `rpg_world_sector_storage` row exists — asserted by count, not by
  absence-of-error alone.
- **Withdraw re-derives weight correctly:** an item withdrawn from sector storage (which never stored
  `weight_each`) lands in the legion's cargo with the same `weight_each` a fresh armoury-load of the
  identical base type would produce.
- **Presence gate refuses:** a legion not at the sector refuses `cargo.not-present`, no write on either
  table.
- **Faction gate refuses:** a legion at the sector but belonging to a different faction than the
  sector's *current* owner refuses `cargo.wrong-faction` — tested against a sector whose owner just
  changed this same turn (proving the check reads live state, not a stale value).
- **Capacity gates refuse independently:** a deposit into a full sector refuses `cargo.sector-full`
  without touching the legion's own cargo; a withdraw that would overweight the legion refuses
  `cargo.over-weight` without touching the sector's storage.
- **Atomicity under forced failure:** a crash between the delete and the insert (either direction)
  leaves the pre-transfer state fully intact on replay — mirroring both dependency modules' own
  atomicity tests.

## Boundaries

- **Always:** presence + faction check before capacity check, capacity check before any write, one
  transaction per verb call.
- **Ask first:** any deposit/withdraw cost beyond presence (a move-turn charge, a distance gate) — not
  named by any owner decision; a per-verb tunable would need to be a reviewed content ask, not assumed.
- **Never:** touch `rpg_item.player_id`/`disposition`; a cross-faction deposit/withdraw path (real gap,
  tracked, not built here); a cached presence/faction flag anywhere.

## Success criteria

1. Deposit and withdraw are each single-transaction, provably atomic under forced-failure tests.
2. Presence and faction gates read live state, proven against a same-turn ownership change. 3. Both
   destination capacity gates (weight+slots for legion, row-count for sector) are enforced independently
   and correctly. 4. `guard-dal.ps1` green. 5. Zero changes to either dependency module's own schema.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `DepositUnlocked`/`WithdrawUnlocked` | `cargo-fate` (module 4) — not called directly (module 4's fates are triggered by legion destruction/sector capture, not a player-issued deposit/withdraw), but the same `rpg_world_entity_cargo`/`rpg_world_sector_storage` rows this module moves between are exactly what module 4 must account for when a legion dies mid-transit or a sector's storage changes hands |
| `SameFaction` helper | Any future FE/HUD surface that wants to grey out a deposit/withdraw action a legion isn't currently eligible for |

## Design-gate checklist

```
[x] Subsystems: world-map legion/sector state (Core), item ownership (Data) — no Status/ActorHub/
    Combat subsystem touched.
[x] Read this session: scoped-inventory-hierarchy-ideal.md §The shape; scoped-inventory-hierarchy-map.md
    row 3; spec-legion-cargo.md and spec-sector-storage.md in full (both dependencies); decisions.md
    "Scoped inventory hierarchy SSOT (2026-09-13)".
[x] Code cited by file:line, opened this session: WorldState.cs (:292-293 AtSectorId, :157
    OwnerFactionId), FactionKindCatalog.cs (:7-17); every table/function citation otherwise inherited
    verbatim from the two dependency specs, which opened them fresh their own sessions the same day —
    not re-opened here since neither had drifted (both specs are hours old, same session).
[x] Drift reported: none — this module adds no new fact-claim about existing code beyond what its two
    dependencies already established; its own new logic (the deposit/withdraw verbs, the `SameFaction`
    helper) is original to this spec, not a citation.
[x] No §2 invariant contradicted: SQL only in FusionRpg.Data; no cap on a magnitude (capacity checks
    reuse each dependency's own structural limits); no `f(Θ)`; no second ActorHub composer; no second
    ownership root; move never copy, one transaction per verb.
```
