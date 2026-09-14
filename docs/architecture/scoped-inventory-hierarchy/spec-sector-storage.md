# Spec: `sector-storage`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `sector-storage`, row 2 of the
[scoped-inventory-hierarchy map](../scoped-inventory-hierarchy-map.md) (wave 1, no dependency — builds
in parallel with `legion-cargo`). Ideal: [scoped-inventory-hierarchy-ideal.md](../scoped-inventory-hierarchy-ideal.md)
§Sector scope, Open questions #2, #8, #9 (all RESOLVED). Decisions: [decisions.md](../decisions.md)
"Scoped inventory hierarchy SSOT (2026-09-13)".

## Objective

A sector gains its own item storage — a new, **slot-bound** structure that competes with
Well/Extractor/Granary for one of the sector's limited `WorldSlot`s, never a sector-wide freebie.
Capacity **sums additively** across every active item-storage structure the sector has built, mirroring
the shipped loam `CapacityBonus`/`EffectiveCapacity` discipline. This module ships **only** the
structure (a 6th `StructureKind`), its slot occupation, its capacity math, and the schema for what a
sector's storage actually holds — **no deposit/withdraw verb** (that is module 3, `cargo-transfer`).
It also designs — precisely, file:line — the capture-transfer hook a captured sector's storage needs,
so module 4 (`cargo-fate`, which owns building the actual transfer logic per the map) has an
unambiguous place to plug into, rather than a re-derived guess at build time.

Success looks like: a sector with two built item-storage structures (say capacity 20 and 30 slots)
reports exactly 50 slots of capacity, computed **fresh** from `WorldSector.Slots` + `StructureCatalog`
every time — never stored redundantly, never drifting if a structure finishes construction or is razed;
a `StructureKind.ItemStorage` structure occupies a `WorldSlot` exactly like a Granary does, and cannot
coexist with a second structure on the same slot; when `ClaimResolver` reassigns a sector's
`OwnerFactionId`, every row already sitting in that sector's storage table becomes reachable to the new
owner and unreachable to the old one in the same instant, with no separate write and no window where
the two disagree.

## Locked anchors

- **One ownership root, this module is an overlay too** (decisions.md, Scoped inventory hierarchy
  SSOT): `rpg_item`/`rpg_item_stock` stay the only place an item is *owned*
  (`RpgStore.Items.cs:85-107`, confirmed fresh this session). Sector storage never gets its own
  `player_id`-shaped column — see §Design 4 for exactly how reachability is derived instead.
- **Slot-bound, additive capacity (owner Q9, RESOLVED verbatim):** the new structure "competes with
  Well/Extractor/Granary for the sector's limited `WorldSlot`s, same as every other structure
  (`WorldState.cs:119`). Capacity sums additively across multiple storage structures, per this doc's
  own recommendation and precedent (`CapacityBonus`/`EffectiveCapacity`'s existing additive discipline,
  `LoamPhases.cs:66-79`...)." Verified fresh this session: `WorldSlot.StructureId` is one nullable
  `string?` field per slot (`WorldState.cs:119`), the same field a Granary already occupies — an
  item-storage structure needs **no new field on `WorldSlot`**, only a new catalog entry that can be
  assigned to that existing field.
- **A new, distinct `StructureKind` — not a generalization of `CapacityBonus` (owner Q2, RESOLVED,
  corrected in place).** Owner, verbatim: *"A new, distinct structure concept for items."* The ideal
  doc's own prior recommendation (a second additive field, `ItemCapacityBonus`, under the existing
  `Storage` kind) is struck through in that doc as **known wrong** — the owner chose separation
  instead. §Design 1 below makes the evidence-based call this doc's task explicitly required: a **6th
  `StructureKind` member**, not a discriminating field inside `Storage`.
- **Capture transfers stored items to the new owner (owner Q8, RESOLVED)** — the *opposite* of the
  nearest existing precedent, `ClaimResolver.cs`'s Warden-binding capture handling, which **clears
  outright with no transfer** (verified fresh this session at `ClaimResolver.cs:104-107`, quoted in
  §What already exists). This module does not reuse that pattern; §Design 5 designs new logic.
- **Move, never copy** — the same discipline `corpse-cache` already proved (*"A real death moves,
  never copies"*, `spec-corpse-cache.md:240-241`) governs every scope-to-scope move this program makes.
  §Design 5 argues this particular transfer needs no move at all, and shows the evidence for why.

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `StructureKind` is closed at exactly **5** values: `LoamSource, Storage, Yield, Refinery, Obstacle` — each one's doc comment literally numbers itself ("a real third thing a structure can do", "the third thing... following Storage's own precedent", "the fourth thing a structure can do") | `StructureCatalog.cs:10-39`, read in full this session |
| `StructureDef`'s complete field list — **no** `StoresItems`/`StorageMedium`/any item-shaped field exists anywhere on it today | `StructureCatalog.cs:42-194`, the whole record read this session |
| `CapacityBonus` — `long`, "Read only by `Storage`-kind structures... how much a granary raises a sector's cap by" | `StructureCatalog.cs:66-67` |
| `LoamPhases.EffectiveCapacity` — the precedent to mirror. **More precise than the ideal doc's own summary:** it is not *only* a `CapacityBonus` sum — it is `checked(LoamPolicy.LoamCapacity + Σ CapacityBonus-over-active-Storage-slots + StructurePolicy.CapacityGrowthFor(sector.DevelopmentLevel))`, gated per-slot on `slot.StructureId is not null`, `slot.ConstructionTurnsRemaining is not > 0`, and `StructureCatalog.IsKnown(id)` | `LoamPhases.cs:52-80`, read in full this session — the growth term (`StructurePolicy.CapacityGrowthFor`) is a base-defense F12 addition the ideal doc's own citation omitted |
| `WorldSlot.StructureId` — one nullable structure reference per slot, the exact field every `StructureKind` shares to occupy ground | `WorldState.cs:119`; DB column `rpg_world_slots.structure_id TEXT`, added via `EnsureColumn` (additive migration, not the original `CREATE TABLE`) | `RpgStore.World.cs:64-76` (CREATE), `:150` (EnsureColumn) |
| `WorldSector.OwnerFactionId` / `SectorPhase` (`Unknown, Explored, Contested, Held, Besieged, Lost`) | `WorldState.cs:157-158`; enum `WorldState.cs:17-25` (via `ClaimResolver.cs` neighbor file — same namespace) |
| `ClaimResolver.cs`'s capture block — the exact site a sibling action would need, and the exact site that reassigns `OwnerFactionId` | `ClaimResolver.cs:95-121` (the whole `next = next with { Sectors = ... }` transform) |
| The Warden-binding precedent this module explicitly does **not** reuse — capture clears the binding with **no transfer**, comment in place: *"the ward exempts FadePolicy only, never combat — capture ends the binding outright, no transfer to the new owner and no refund to the old one."* | `ClaimResolver.cs:104-107` (`WardenBindingId = null`), read fresh this session |
| `rpg_world_sectors`/`rpg_world_slots` schema — `owner_faction_id TEXT`, PKs `(world_id, sector_id)` / `(world_id, sector_id, slot_index)`; no item-shaped column on either | `RpgStore.World.cs:45-76` |
| `WorldFactionKind` is closed at exactly 5: `Player, Zomboss, Clan, Rival, Wild` — only `Player` ("Dave") is a real, item-owning identity | `FactionKindCatalog.cs:7-17` |
| `rpg_world_factions` has **no `player_id` column at all** (PK `(world_id, faction_id)`, fields `kind`/`name`/`policy_id`); `rpg_worlds` carries exactly **one** `player_id` for the entire world | `RpgStore.World.cs:37-44` (factions), `:20-22` (`rpg_worlds`) — re-verified fresh, matching the ideal doc's own F1 correction |
| The turn-commit transaction boundary — `TurnEngine.Step` (which calls `ClaimResolver.Run` internally, `TurnEngine.cs:381`) runs, then `DiffWorldGraphUnlocked(db, tx, world, result.World)` writes every changed row, **all inside the one `tx`** opened at the top of `CommitWorldTurn` | `RpgStore.WorldTurns.cs:509-537` |
| `DiffSectors` — the exact per-sector before/after comparison loop already running inside that same `tx`, already reading and writing `owner_faction_id` per sector | `RpgStore.WorldGraphDiff.cs:133-166`, specifically the `foreach (var (id, s) in after)` loop at `:155-165` |
| `rpg_item`/`rpg_item_stock` — the ownership root this module must never duplicate | `RpgStore.Items.cs:85-107` |
| `corpse-cache`'s `rpg_corpse_cache_item` `kind ∈ {instance, stack}` shape — the pattern to mirror for what a sector's storage holds | `spec-corpse-cache.md:113-120` |
| `legion-cargo`'s `rpg_world_entity_cargo`, spec'd this session, same `kind`/`instance_id`/`container_id`/`qty` split, keyed `(world_id, entity_id, seq)` | `spec-legion-cargo.md` §Design 2 |
| `SlotKind` already has a **Vault** member — thematically the closest existing ground type for item storage, currently legal only for the `Enable` role (`legalRoleSlotPairs`, below), not `Store` | `SlotTypeCatalog.cs:7-22`, `:72` (`vault` row); `SlotValueCatalog.cs:44` |

### Real gap

| Gap | What would have to be built |
|---|---|
| No item-shaped store at sector scope at all | Confirmed by the same `WorldState.cs` read — `WorldSector` carries `LoamStock`/`RubbleStock`/`IronworkStock`/`RecruitStock` (all scalar), nothing item-shaped. This module's own new table (§Design 4) is the first one. |
| No 6th `StructureKind`, no item-capacity field on `StructureDef` | §Design 1/2 name the exact addition. |
| No seedsmith/content pairing for an item-storage role today | `data/seed/structures/_plan.json`'s `legalRoleSlotPairs` (generated planning artifact, not hand-editable per AGENTS.md's "generated seed data" rule) pairs role `Store` only with slot kind `Wildland` — no `Store`↔`Vault` pairing exists, and the ideal doc's own finding that `anchor/schema.py`'s `ROLE_TO_STRUCTURE_KIND` dict is "provably wrong against real shipped rows" means role→kind is not even reliably derivable today regardless. Authoring a real `ItemStorage`-kind catalog row requires either extending the generator's role/slot-kind vocabulary or reusing an existing legal pairing (`Store`↔`Wildland`) with a hand-assigned `Kind`. Named as its own future content task, not solved by this module (mirrors `legion-cargo`'s own per-unit-stat real gap). |
| No mechanism resolves "does faction X have a real item-owning `player_id`" | Nothing today queries this; only `FactionKindCatalog.IdOf(WorldFactionKind.Player) == "player"` combined with the single `rpg_worlds.player_id` answers it, and no helper wraps that check yet. §Design 5 names exactly where it would be added and why it is a no-op until a second real player row exists. |

## Design

### 1. `StructureKind` gains a 6th value: `ItemStorage`

**The evidence-based call the task required.** `StructureDef`'s full field list (`StructureCatalog.cs:42-194`,
read in full this session) has no `StoresItems: bool` or `StorageMedium` sub-enum anywhere — there is
nothing to "turn on" inside the existing `Storage` kind. More decisively, the owner's own Q2 answer
explicitly rejected the one design that *would* have fit inside `Storage` (a second additive field
beside `CapacityBonus`, struck through in the ideal doc as "known wrong"). Every existing extension to
`StructureKind` has followed the same pattern — a genuinely new mechanical capability gets its own
member, each one's doc comment literally counting itself ("a real third thing", "the third thing...
following `Storage`'s own precedent", "the fourth thing a structure can do", `StructureCatalog.cs:14,
26-29, 32-37`). Item storage is a fifth, structurally distinct capability (a different resource, a
different consumer, a capacity shape this doc is free to define without inheriting loam's exact rules)
— it belongs in the same list, not folded into an existing member:

```csharp
public enum StructureKind
{
    LoamSource,
    Storage,
    Yield,
    Refinery,
    Obstacle,
    /// <summary>scoped-inventory `sector-storage`: raises a sector's ITEM capacity, never loam's —
    /// a distinct capacity axis, not a second field on <see cref="Storage"/> (owner decision,
    /// scoped-inventory-hierarchy-ideal.md Open question #2).</summary>
    ItemStorage
}
```

New field on `StructureDef`, named to avoid any confusion with the loam field it deliberately does not
share:

```csharp
/// <summary>Read only by ItemStorage-kind structures — how many item-storage slots this structure
/// grants a sector (scoped-inventory `sector-storage`). Never read by loam's EffectiveCapacity, and
/// CapacityBonus is never read by item-storage math — two capacity axes, two fields, per the owner's
/// own rejection of overloading one field to mean both (Open question #2).</summary>
public long ItemStorageCapacityBonus { get; init; }
```

`Validate` (`StructureCatalog.cs:305-346`) gains one more `if (s.ItemStorageCapacityBonus < 0) throw ...`
line, matching every other magnitude field's existing non-negative check in that method.

### 2. Capacity — a single scalar, computed fresh, never cached

**Design call, named explicitly (not owner-mandated for this scope, unlike legion's weight+slot
dual-gate).** The map's own module-2 description frames sector capacity as one additive sum
("capacity summing additively across every active storage-kind structure... mirrors
`LoamPhases.cs:66-79`'s existing `CapacityBonus` discipline") — a single number, not two independent
gates. The owner's Q1 answer explicitly asked for **both** a slot count and a weight limit for legion
cargo; Q2's answer for sector storage addressed *which field/kind* holds the capacity, not whether a
second (weight) axis is needed, and named nothing resembling a weight gate. Absent an explicit ask for
weight here, adding one would be inventing a requirement, not building one — so this module ships a
**single slot-count scalar**, matching loam's own shape exactly. If the owner later wants a weight gate
on sector storage too, it is a one-line extension mirroring `legion-cargo`'s own dual-gate shape, not a
rework.

```csharp
namespace FusionRpg.Core.World;

/// <summary>scoped-inventory `sector-storage`: mirrors LoamPhases.EffectiveCapacity's exact shape
/// (same active-slot gate, same additive sum) for the ITEM capacity axis instead of loam's. Does
/// NOT include LoamPolicy.LoamCapacity's base term or StructurePolicy.CapacityGrowthFor's
/// development-level growth — those are loam-specific; item storage has no base allowance (zero
/// structures, zero slots, by design) and no named growth term today (real gap, not built here).</summary>
public static class SectorItemCapacity
{
    public static long EffectiveCapacity(WorldSector sector)
    {
        long bonus = 0;
        foreach (var slot in sector.Slots)
        {
            if (slot.StructureId is not { } id) continue;
            if (slot.ConstructionTurnsRemaining is > 0) continue;
            if (!StructureCatalog.IsKnown(id)) continue;

            var structure = StructureCatalog.Get(id);
            if (structure.Kind == StructureKind.ItemStorage) bonus += structure.ItemStorageCapacityBonus;
        }
        return checked(bonus);
    }
}
```

`checked` per `CLAUDE.md`'s overflow rule 5 ("overflow throws, never wraps") even though a sum of a few
`long` catalog values realistically never approaches the ceiling — the same discipline
`LoamPhases.EffectiveCapacity` already applies to its own sum (`LoamPhases.cs:79`).

### 3. Row-count is the capacity unit — one row, one slot, regardless of stack size

Following `legion-cargo`'s own implicit convention (its `slotCapacity` gates *row count*, not summed
`qty`): a `kind='stack'` row holding 500 of a fungible material occupies exactly one storage slot, the
same as a `kind='instance'` row holding one rolled item. `EffectiveCapacity` (§2) is compared against
`SELECT COUNT(*) FROM rpg_world_sector_storage WHERE world_id=$w AND sector_id=$s` — never a summed
`qty` — by module 3 (`cargo-transfer`) at deposit time; this module ships the read, not the refusal
(§Interface).

### 4. New table — `rpg_world_sector_storage`

```sql
CREATE TABLE IF NOT EXISTS rpg_world_sector_storage (
  world_id     TEXT NOT NULL,
  sector_id    TEXT NOT NULL,
  seq          INTEGER NOT NULL,       -- insertion order, stable id within this sector's storage
  kind         TEXT NOT NULL,          -- 'instance' | 'stack' — same split as rpg_corpse_cache_item / rpg_world_entity_cargo
  instance_id  TEXT,                   -- kind='instance': rpg_item.instance_id (rolled gear, relics)
  container_id TEXT,                   -- kind='stack': fungible container id
  qty          INTEGER,                -- kind='stack' only; long, never negative
  PRIMARY KEY (world_id, sector_id, seq),
  FOREIGN KEY (world_id, sector_id) REFERENCES rpg_world_sectors(world_id, sector_id) ON DELETE CASCADE
);
```

Deliberately **no `weight_each` column** (§2's design call — no weight gate for this scope) and
**deliberately no owner/faction column of any kind** — see §5 for exactly why the absence of an owner
column is not an oversight but the whole mechanism that satisfies Q8. Same `kind ∈ {instance, stack}`
split `corpse-cache` and `legion-cargo` both already use — three overlays over the same
`rpg_item`/`rpg_item_stock` root, one shape, not a coincidence. `rpg_item.player_id` and
`rpg_item.disposition` are **never touched** by depositing into this table (module 3's job to design in
full — this module only states the invariant it must preserve): an item is "in a sector's storage"
purely by existing in this table with no corresponding armoury-listing row, identical to
`legion-cargo`'s own "reachable by owner, not by a second column" shape (`spec-legion-cargo.md` §Design
2).

### 5. The capture-transfer hook — exact site, and why it needs no data migration today

**Exact site, as the task requires.** The turn-commit transaction is `CommitWorldTurn`
(`RpgStore.WorldTurns.cs:484-537`): it runs `TurnEngine.Step` (which calls `ClaimResolver.Run`
internally at `TurnEngine.cs:381`, reassigning `sector.OwnerFactionId` at `ClaimResolver.cs:101`), then
calls `DiffWorldGraphUnlocked(db, tx, world, result.World)` at `RpgStore.WorldTurns.cs:537` — **all
inside the single `tx`** opened at `RpgStore.WorldTurns.cs:510`. Inside that call, `DiffSectors`
(`RpgStore.WorldGraphDiff.cs:133-166`) already loops every sector's before/after pair at `:155-165`
(`foreach (var (id, s) in after) { if (before.TryGetValue(id, out var was) ...`) and already has both
`was.OwnerFactionId` and `s.OwnerFactionId` in hand — the exact place a capture is already detectable,
with zero new plumbing to find it.

**Argument for one transaction, not two — matching `corpse-cache`'s discipline.** Every other table
`DiffWorldGraphUnlocked` touches commits or rolls back together with the sector's own `OwnerFactionId`
row (the equivalence guard at `RpgStore.WorldGraphDiff.cs:56-68` even re-reads and re-hashes the whole
graph on the *same* connection before commit, specifically to catch a write that should have happened
and didn't). A capture-transfer step run as a **second**, later transaction would open exactly the
failure window `corpse-cache`'s own "move never copy" precedent exists to close: a crash between the
two commits would leave a sector already flipped to its new owner while its storage still behaved as
if the old owner controlled it — an observable, provable inconsistency. The sibling action belongs
**inside** `DiffSectors`'s existing per-sector loop, in the same `tx`, not a phase run after
`CommitWorldTurn` returns.

**Why the hook's body is a no-op today, and that is not a dodge of Q8.** §Design 4 puts no owner/faction
column on `rpg_world_sector_storage` at all — reachability is derived **live**, at read time, by
whichever code checks "does this sector's *current* `OwnerFactionId` match the acting legion's faction"
(module 3's job, per the map). Because nothing on the storage row itself ever recorded who could reach
it, the moment `ClaimResolver` reassigns `sector.OwnerFactionId` and that write lands in the same `tx`
(§ above), **every row already in that sector's storage becomes reachable to the new owner and
unreachable to the old one, automatically, with nothing left to migrate.** This is the strongest
possible reading of "move never copy": there is no move to make, because there was never a second copy
of ownership to keep in sync. It also matches the underlying fact `legion-cargo`'s own design already
established: depositing an item into an overlay table never touches `rpg_item.player_id` (`spec-legion-cargo.md`
§Design 4's own weight-snapshot note doesn't touch ownership either) — the item's real owner is, and
stays, whichever real player deposited it, for as long as it sits in *any* overlay table, sector storage
included.

**The part that is a genuine, named real gap — not solved here.** The ideal doc's own Q8 text anticipated
a literal `rpg_item.player_id` (or stock-equivalent) reassignment "across a faction boundary." Per the
already-verified Q3/Q6 finding (`rpg_world_factions` has no `player_id` column; only `Player`
resolves to the world's one real `player_id`, `RpgStore.World.cs:20-22,37-44`), there is **no second
real player-owned item store today for a non-`Player` faction to receive anything into** — the identical
finding that downgraded cross-faction *trade* from "resolved" to "real gap" applies here without
change. So: an AI-faction capture of a `Player`-held storage sector, or the reverse, is **fully correct
under the reachability-only design above** (the player simply loses reach until the sector is
recaptured — nothing is destroyed, nothing is duplicated, nothing silently vanishes). A **literal**
`rpg_item.player_id`-rooted transfer only becomes a meaningful, buildable operation once a genuine
second empire/player row exists (the same prerequisite Q6b already names). The hook point for that
future write is named precisely so nobody re-derives it: **the same `DiffSectors` `foreach` loop,
`RpgStore.WorldGraphDiff.cs:155-165`**, guarded on `was.OwnerFactionId != s.OwnerFactionId` and on the
new owner resolving to a *different* real `player_id` than the old one — a condition that cannot fire
today (there is only one real `player_id` in any world), so the guard itself is the whole reason this
is correctly a real gap and not a missed wiring opportunity.

## Tunables

| Number | Owner | Notes |
|---|---|---|
| `ItemStorageCapacityBonus` per structure catalog row | **Correction to the map's own Tunables table** — the map lists "sector storage base slot capacity per structure tier" under `data/tuning/scoped-inventory.v1.json`, but the *precedent it cites* (`CapacityBonus`) is not a `data/tuning/*.json` value at all — it is a `StructureCorpus`/seed-content field (`StructureCatalog.cs:256-283`'s `ToStructureDef`, sourced from `data/seed/structures/**`, a generated tree per AGENTS.md). `ItemStorageCapacityBonus` follows the identical precedent: **content-authored per structure row** (seedsmith `structures` pipeline or hand-authored catalog content, once the real-gap content pairing above is resolved), never a bare tunable file entry. `long`. |
| A structural row-count abuse guard on `rpg_world_sector_storage` per sector (optional, mirrors the armoury's `InventoryCeiling = 20_000` guard, `RpgStore.Items.cs:268-276`) | `data/tuning/scoped-inventory.v1.json`, if the owner wants one | Not required by any owner decision — named as a spec-time option, never a progression ceiling if added (a bug guard only, matching the armoury's own doc comment) |
| Content pairing for the new `StructureKind` (`RequiredSlotKind`, tier costs) | `data/seed/structures/**` (generated) | Real gap, named in §What already exists — not this module's own build item |

## Numeric types

`ItemStorageCapacityBonus` and `SectorItemCapacity.EffectiveCapacity`'s return value are `long`, matching
`CapacityBonus`'s own type and `CLAUDE.md`'s overflow rule 1 (`long` for any magnitude). `qty` (stack
count) is `long`/`INTEGER`, matching `corpse-cache`'s and `legion-cargo`'s own precedent for the
identical field. `seq` is `int` — a structural row-ordering id bounded by a sector's own realistic
storage size, not a progression-scaled magnitude, the same reasoning `legion-cargo`'s own `seq`/`memberCount`
typing already documents.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SectorItemCapacity"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~SectorStorage"
.\scripts\guard-dal.ps1        # every new SQL string lives in FusionRpg.Data
```

## Structure

```
src/FusionRpg.Core/World/StructureCatalog.cs        MODIFIED — StructureKind gains ItemStorage (§1);
                                                     StructureDef gains ItemStorageCapacityBonus (§1);
                                                     Validate gains one non-negative check
src/FusionRpg.Core/World/SectorItemCapacity.cs      NEW — EffectiveCapacity(WorldSector) (§2)
src/FusionRpg.Data/Sqlite/RpgStore.SectorStorage.cs NEW — schema (§4), a row-count read for module 3's
                                                     capacity check (§3); Load/Unload verbs are module
                                                     3's own deliverable, not built here
src/FusionRpg.Data/Sqlite/RpgStore.WorldGraphDiff.cs MODIFIED — DiffSectors gains the capture hook site
                                                     (§5), body is a documented no-op until a second
                                                     real player_id exists
tests/FusionRpg.Core.Tests/World/SectorItemCapacityTests.cs   NEW
tests/FusionRpg.Data.Tests/SectorStorage/                     NEW
UNTOUCHED: LoamPhases.cs (loam's own EffectiveCapacity, unchanged); WorldSlot/WorldSector record shape
           (no new field — StructureId is reused as-is, §Locked anchors); ClaimResolver.cs's own
           WardenBindingId-clear line (unreused, per §Locked anchors); rpg_item/rpg_item_stock schema.
```

## Code style

```csharp
// Mirrors LoamPhases.EffectiveCapacity's exact shape — same active-slot gate, same additive sum,
// a different capacity axis and a different catalog field, never the same one.
public static long EffectiveCapacity(WorldSector sector)
{
    long bonus = 0;
    foreach (var slot in sector.Slots)
    {
        if (slot.StructureId is not { } id) continue;
        if (slot.ConstructionTurnsRemaining is > 0) continue;
        if (!StructureCatalog.IsKnown(id)) continue;

        var structure = StructureCatalog.Get(id);
        if (structure.Kind == StructureKind.ItemStorage) bonus += structure.ItemStorageCapacityBonus;
    }
    return checked(bonus);
}
```

## Testing strategy

- **Capacity is additive across multiple structures:** two active `ItemStorage`-kind structures in one
  sector (capacities 20 and 30) report exactly 50 — never 20, never 30, never a max of the two.
- **Capacity ignores a structure still under construction:** a slot with
  `ConstructionTurnsRemaining > 0` contributes zero, matching `EffectiveCapacity`'s own gate exactly.
- **Capacity ignores loam-`Storage`-kind and every other kind:** a Granary (`Kind = Storage`) on the
  same sector contributes nothing to `SectorItemCapacity.EffectiveCapacity`, proving the two axes never
  bleed into each other (the owner's own reason for rejecting a shared field, Open question #2).
- **Row count, not summed qty, is the capacity unit:** one `kind='stack'` row with `qty=500` occupies
  exactly one slot for capacity purposes (§3).
- **Reachability is live, never cached:** a test writes a storage row for sector S while
  `OwnerFactionId = "player"`, then flips `OwnerFactionId` to `"zomboss"` via the exact `DiffSectors`
  path (`RpgStore.WorldGraphDiff.cs:155-165`) and asserts the row is unchanged in the table but the
  reachability check (module 3's, exercised here only through the read this module exposes) now
  resolves against the new owner — proving §5's "no data migration needed" claim, not just asserting it
  in prose.
- **The capture hook fires inside the turn-commit transaction, not after it:** a forced-failure test
  (mirroring `legion-cargo`'s own atomicity test) proves a crash immediately after `DiffSectors`'s
  sector-owner write but before `tx.Commit()` leaves the sector's `OwnerFactionId` and its storage
  rows' observable reachability in the **same** pre-capture state — never one flipped and the other not.
- **Validate refuses a negative `ItemStorageCapacityBonus`:** matching every other magnitude field's
  existing non-negative check in `StructureCatalog.Validate`.

## Boundaries

- **Always:** capacity computed fresh from `WorldSector.Slots` + `StructureCatalog`, never cached on
  `WorldSector`/`WorldSlot`; the capture hook lives inside `DiffSectors`'s existing per-sector loop, in
  the same `tx` as every other turn-commit write; row count (never summed `qty`) is the capacity unit.
- **Ask first:** reusing `StructureKind.Storage`/`CapacityBonus` for item capacity (explicitly rejected
  by the owner, §Locked anchors) — if this is ever revisited, it needs a fresh decision, not a silent
  merge; adding a weight gate to sector storage (§2's design call) without a named owner ask.
- **Never:** an owner/faction/player_id column on `rpg_world_sector_storage` (§4 — the absence is the
  mechanism, not an omission); a second `EffectiveCapacity`-shaped function that also reads
  `CapacityBonus` (the two axes must stay provably independent, per the testing strategy above); a
  literal `rpg_item.player_id` reassignment to a faction with no real item-owning `player_id` (real gap,
  §Design 5, not this module's or `cargo-fate`'s to force).

## Notification hook — named, not built (strengthen pass, 2026-09-13)

A sector's storage becoming reachable/unreachable to a different owner the instant `ClaimResolver`
reassigns `sector.OwnerFactionId` (§5's own "no migration needed at all" mechanism) is a real,
player-relevant event this module creates with zero signal to the player who lost or gained it —
their stored items silently changed hands. `docs/architecture/notification-ssot-ideal.md` is the
owner's own sibling idea doc for exactly this class of event (it exists because chaos-decay
sector-fade needed a player-facing signal, and this is the same shape: a state change the player
would otherwise only discover by revisiting the sector). This module does not build a notification
hook — it only names the event as a real future consumer of that program, once it ships.

## Success criteria

1. `StructureKind` has exactly 6 members, `ItemStorage` the newest, each doc-commented like its
   siblings. 2. `SectorItemCapacity.EffectiveCapacity` sums correctly across N active structures,
   provably independent of loam's own `EffectiveCapacity`. 3. `rpg_world_sector_storage` exists with no
   owner column, FK-cascades on sector delete. 4. The capture hook's exact site is provable by the
   forced-failure atomicity test, not just documented. 5. `guard-dal.ps1` green. 6. Zero changes to
   `LoamPhases.cs`'s own loam capacity math.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `rpg_world_sector_storage` schema, `(world_id, sector_id, seq)` keying, row-count-is-capacity-unit rule | `cargo-transfer` (module 3) — the deposit/withdraw verb reads/writes this exact table and must check `COUNT(*) < SectorItemCapacity.EffectiveCapacity(sector)` before every deposit |
| `SectorItemCapacity.EffectiveCapacity(WorldSector)` | `cargo-transfer` (module 3) for the deposit-time capacity check; any future FE/HUD surface that displays a sector's storage capacity |
| The reachability rule itself — no owner column on the storage row; whoever calls in must compare the acting legion's `OwnerFactionId` against the sector's own **current** `OwnerFactionId`, read live, never cached | `cargo-transfer` (module 3) — this is the exact gate module 3's own deposit/withdraw refusal (`cargo.wrong-faction` or equivalent) must implement; this module does not implement the refusal itself |
| The `DiffSectors` hook site (`RpgStore.WorldGraphDiff.cs:155-165`), and the finding that its body is
  correctly a no-op until a second real `player_id` exists | `cargo-fate` (module 4) — the map assigns the actual capture-transfer *logic* to module 4; this spec exists so module 4 does not have to re-derive the exact site or re-litigate whether a data migration is needed (it is not, for every scenario reachable today) |
| `StructureKind.ItemStorage` / `StructureDef.ItemStorageCapacityBonus` | Any future seedsmith `structures` content pipeline extension (real gap, named above) that authors real catalog rows for this kind |

## Design-gate checklist

```
[x] Subsystems: world-map sector/structure state (Core), item ownership (Data) — no Status/ActorHub/
    Combat subsystem touched.
[x] Read this session: scoped-inventory-hierarchy-ideal.md §Sector scope + Open questions #2/#8/#9;
    scoped-inventory-hierarchy-map.md row 2; decisions.md "Scoped inventory hierarchy SSOT
    (2026-09-13)"; spec-legion-cargo.md (house style + §Design 2/4 precedent, sibling module, not a
    dependency); deployment-hierarchy/spec-corpse-cache.md (§Design 1, move-never-copy precedent).
[x] Code cited by file:line, opened this session: StructureCatalog.cs (:10-39 StructureKind, :42-194
    StructureDef, :66-67 CapacityBonus, :256-283 ToStructureDef, :305-346 Validate); LoamPhases.cs
    (:52-80 EffectiveCapacity, including the growth-term nuance the ideal doc's own citation omitted);
    WorldState.cs (:17-25 SectorPhase, :96-146 WorldSlot, :148-244 WorldSector); ClaimResolver.cs
    (:95-121 capture block, :104-107 WardenBindingId clear + its own "no transfer" comment);
    RpgStore.World.cs (:20-22 rpg_worlds, :37-44 rpg_world_factions, :45-76 sectors/slots CREATE,
    :150 structure_id EnsureColumn); RpgStore.WorldTurns.cs (:484-537 CommitWorldTurn, the transaction
    boundary); RpgStore.WorldGraphDiff.cs (:45-69 DiffWorldGraphUnlocked + equivalence guard,
    :133-166 DiffSectors); FactionKindCatalog.cs (:7-17); RpgStore.Items.cs (:85-107);
    SlotTypeCatalog.cs (:7-22, :72); data/seed/structures/_plan.json (legalRoleSlotPairs, generated
    content, read not edited).
[x] Drift reported: one correction to the ideal doc's own citation (`LoamPhases.EffectiveCapacity`
    also includes `LoamPolicy.LoamCapacity` and `StructurePolicy.CapacityGrowthFor`, not just the
    `CapacityBonus` sum — named in §What already exists, not silently carried forward); one correction
    to the map's own Tunables table (`CapacityBonus`'s real precedent is seed-corpus content, not a
    `data/tuning/*.json` entry — named in §Tunables).
[x] No §2 invariant contradicted: SQL only in FusionRpg.Data; no cap on a magnitude presented as a
    progression ceiling (item-storage capacity is a structural per-sector limit, exempt and commented
    as such, mirroring loam's own exemption); no `f(Θ)`; no second ActorHub composer; no second
    ownership root (§Design 4's explicit no-owner-column design is the strongest form of this rule,
    not an exception to it).
[ ] The literal `rpg_item.player_id`-rooted transfer for a genuine multi-empire capture (§Design 5's
    named real gap) was not designed further this session — correctly deferred behind the same
    prerequisite Q6b already named (a second real player/empire row), not assumed solved.
```
