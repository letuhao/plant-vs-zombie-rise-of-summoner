# Capability map: scoped inventory hierarchy

**Status: APPROVED 2026-09-13** — module boundaries and build order approved by the owner the same
day, matching the `deployment-hierarchy-map.md`/`party-dungeon-map.md` precedent. A companion
`decisions.md` row ("Scoped inventory hierarchy SSOT") was appended the same day recording the
one-ownership-root/overlay-per-scope pattern this map's modules all follow. Module specs are written
per wave, in dependency order, each verified against code and the ideal before it is placed.

**Ideal it implements:** [scoped-inventory-hierarchy-ideal.md](scoped-inventory-hierarchy-ideal.md) —
idea phase complete, strengthen-pass corrected, all blocking owner questions resolved 2026-09-13 (this
session). **Sub-program of:** [empire-development-map.md](empire-development-map.md) — the one
sub-program `loam-relics-and-wonders-ideal.md` is hard-blocked on (a legion cannot deposit a carried
relic at a sector until this program's sector storage exists).

## What this program is

Two new inventory scopes — **legion cargo** (a slot+weight-limited carry overlay a legion manages,
including same-empire legion-to-legion transfer) and **sector storage** (a new, slot-bound building
that lets a legion deposit/withdraw items at a sector) — plus the two fate rules that make both scopes
safe against the game's own volatility: what happens to carried cargo when a legion is destroyed, and
what happens to stored items when a sector is captured. Empire scope, unique-actor scope, and
delve-party scope (`loot-pack`) already exist and are untouched by this program.

## What it is not

- **Not a rewrite of `rpg_item`/`rpg_item_stock`.** Both remain the single ownership root
  (`RpgStore.Items.cs:85-107`); every scope this program adds is a reachability/capacity **overlay**,
  never a second ownership table — the same discipline `loot-pack` and `corpse-cache` already prove
  out for delve-party and unique-actor scopes respectively.
- **Not cross-faction trade.** Verified this session (`scoped-inventory-hierarchy-ideal.md` Open
  question #3/#6, corrected): no faction other than the world's own single `player_id` owns an item
  store today (`rpg_world_factions` has no `player_id` column, `RpgStore.World.cs:37-44`). The schema
  this program ships keys ownership on the generic `FactionId`/`player_id` model (never the closed
  `CommanderId` enum) so a future multi-empire program isn't blocked, but **actual cross-faction item
  transfer is out of scope** until that program gives a second faction its own real item store.
  Same-empire legion-to-legion transfer (within the single `Player` faction) is in scope.
- **Not the multi-empire program itself**, not sector-to-sector trade routes (explicitly deferred by
  the owner to a future "trading program"), not the per-unit `carryWeight`/`backslot` seedsmith
  extension (real, deferred future work — this program ships with a flat tunable default per unit
  instead, see module 1).
- **Not `loam-relics-and-wonders`.** That program consumes this one's sector-storage interface; it is
  not spec'd or built here.

## Modules

Stable kebab-case ids, chosen once.

| # | Module id | Responsibility | Depends on | Wave |
|---|---|---|---|---|
| 1 | `legion-cargo` | A cargo overlay per `WorldEntity` (legion): slot count + weight limit, both aggregated as Σ(`carryWeight`/`backslot` × member count) across the legion's `WorldEntityMember`s. Per-unit `carryWeight`/`backslot` values are a **flat tunable default today** (`data/tuning/scoped-inventory.v1.json`), not a real per-species stat — the real per-unit generator is the owner-named, explicitly deferred seedsmith unit-pipeline extension (real gap, not this module's to build). Same-empire legion-to-legion transfer: a single-transaction move (never copy) of an item/stack from one legion's cargo overlay to another legion's, gated on both legions sharing the same `player_id`. | — | 1 |
| 2 | `sector-storage` | A new, slot-bound `StructureKind` for item storage — competes with `Well`/`Extractor`/`Granary` for a sector's limited `WorldSlot`s (`WorldState.cs:119`), capacity summing additively across every active storage-kind structure in the sector (mirrors `LoamPhases.cs:66-79`'s existing `CapacityBonus` discipline). No deposit/withdraw verb yet — this module ships the structure, its capacity math, and its seed/catalog entry only. | — | 1 |
| 3 | `cargo-transfer` | The deposit/withdraw verb connecting `legion-cargo` and `sector-storage`: a legion physically present at a sector (`WorldEntity.AtSectorId` matching) may move items between its own cargo overlay and that sector's storage, gated on the legion's faction matching the sector's *current* `OwnerFactionId`. Single-transaction move, same discipline as module 1's legion-to-legion transfer and `corpse-cache`'s own precedent. | `legion-cargo`, `sector-storage` | 2 |
| 4 | `cargo-fate` | The two owner-resolved fate rules: (a) a destroyed legion's cargo becomes a revisit-lootable cache at its last-known place — a sector (reusing `corpse-cache`'s already-reserved `place_kind='world_sector'`) or a lane (new `place_kind='world_lane'`, owner-added requirement 2026-09-13), both tagged `source_kind='legion_death'` — **ask accepted and resolved 2026-09-13**, `deployment-hierarchy`'s `corpse-cache` module amended in place (`spec-corpse-cache.md` §1a); (b) a captured sector's stored items transfer to the new owner in the same transaction as the capture itself, new ownership-transfer logic (does **not** reuse `ClaimResolver.cs`'s Warden-binding capture handling, which clears outright with no transfer — item storage's capture rule is the opposite). | `cargo-transfer`; external `deployment-hierarchy`'s `corpse-cache` module | 2 |

**Dependency direction, no cycles.** `legion-cargo` and `sector-storage` are independent (both wave 1,
buildable in parallel — neither reads the other). `cargo-transfer` needs both to exist. `cargo-fate`
needs `cargo-transfer`'s tables to know what's actually in each scope when a death or capture fires.

## Build order

```
Wave 1  legion-cargo ∥ sector-storage
Wave 2  cargo-transfer  →  cargo-fate
```

**Why this order.** The two new scopes' own schemas (capacity math, structure catalog entry) don't
reference each other — building them in parallel wastes no dependency-waiting time. The verb that
connects them (`cargo-transfer`) obviously needs both to exist first. The fate rules
(`cargo-fate`) need a transfer to have actually happened at least once (there must be cargo/storage
rows to have a fate) — sequencing it last is not just convenience, it is a real data dependency.

## External dependencies

| Dependency | Owner map / module | What this program needs | Gate |
|---|---|---|---|
| `corpse-cache` shape reuse for legion-death cache | `deployment-hierarchy-map.md`, module `corpse-cache` (already spec'd) | Extend `place_kind`/`place_ref` to accept a legion's last-known place as a valid cache place — a small, additive change to an already-spec'd (not yet built) module, not a reopening of it | **RESOLVED 2026-09-13** — accepted as: reuse the already-reserved `world_sector` value for a sector death, add a new `world_lane` value for a lane death (owner-added requirement, `WorldLane` confirmed durable/revisitable — `WorldState.cs:246-263`), and a new `source_kind='legion_death'` value for the "why," never branched on downstream (`spec-corpse-cache.md` §1a). No longer a gate on `cargo-fate`'s own build. |

No asks are filed on any OTHER program beyond this one — `legion-cargo`'s per-unit stat default is
this program's own tunable, not an ask on the (not-yet-existing) seedsmith unit pipeline.

## Tunables

| Number | Owner | Notes |
|---|---|---|
| Per-unit `carryWeight`/`backslot` flat default | `data/tuning/scoped-inventory.v1.json` | A placeholder until the seedsmith unit-pipeline extension (real gap, deferred) provides real per-species values; both `long` |
| Sector storage base slot capacity per structure tier | same file | Mirrors `CapacityBonus`'s existing shape; sums additively |
| Legion-to-legion / cargo-transfer verb costs, if any | same file | Not yet decided whether a move costs anything beyond being in range — a spec-time call for module 1/3 |

## What this program does not touch

`rpg_item`/`rpg_item_stock`'s own schema (module 1-4 only add overlay/reachability rows, never a
second ownership column); `loot-pack`'s `PackGrid` (delve-party scope, untouched); `WardenBindingId`'s
own capture-clear behavior (module 4's sector-capture rule is new, parallel logic — it does not change
how Warden bindings clear); the multi-empire program; sector-to-sector trade routes.

## Open items carried from the ideal

The consent/offer flow for a genuine future cross-faction trade (scoped-inventory Open question #6b) —
explicitly not buildable until a real second empire/`player_id` exists, tracked but not a module here.
