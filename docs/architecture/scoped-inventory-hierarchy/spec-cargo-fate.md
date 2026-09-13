# Spec: `cargo-fate`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `cargo-fate`, row 4 (final) of the
[scoped-inventory-hierarchy map](../scoped-inventory-hierarchy-map.md) (wave 2, depends on
`cargo-transfer`, built — [spec-cargo-transfer.md](spec-cargo-transfer.md); external dependency on
`deployment-hierarchy`'s `corpse-cache` module, **ask accepted and resolved 2026-09-13** — see §Design 1).
Ideal: [scoped-inventory-hierarchy-ideal.md](../scoped-inventory-hierarchy-ideal.md) Open questions #7
(RESOLVED) and #8 (RESOLVED — see §0 below for why this module's own scope is narrower than the map
assigned). Decisions: [decisions.md](../decisions.md) "Scoped inventory hierarchy SSOT (2026-09-13)".

**Amendment 2026-09-13 (later session, owner-authorized):** the `place_kind` ask this spec originally
filed on `deployment-hierarchy` (§Design 1, below) is **accepted and resolved.** `world_sector` is
reused (not a new value) for a legion that dies at a sector; a new `world_lane` value is added for a
legion that dies on a lane — an owner-added requirement this session, not part of the original ask; and
the reason both cases exist is carried by a new `source_kind = 'legion_death'` value, never by
`place_kind`. The lane-death case, previously left as "destroyed outright... an open question," is now
a first-class branch, symmetric with the sector case. See
`deployment-hierarchy/spec-corpse-cache.md` §1a for the accepting-side amendment.

## 0. Scope correction, made before any design — the map assigned two fates, one is already done

The capability map assigns this module two responsibilities: (a) a destroyed legion's cargo becomes a
revisit-lootable cache, and (b) a captured sector's stored items transfer to the new owner. **`spec-
sector-storage.md` §Design 5 already fully resolved (b) as part of module 2's own build**, not left for
this module: `rpg_world_sector_storage` deliberately carries no owner/faction column, so reachability
is derived live from `sector.OwnerFactionId` — the moment `ClaimResolver` reassigns that field inside
the same turn-commit transaction, every existing storage row becomes reachable to the new owner and
unreachable to the old one automatically, with nothing to migrate. That spec names the hook site
(`RpgStore.WorldGraphDiff.cs:155-165`, inside `DiffSectors`) and states the hook's body is a **correct,
documented no-op** today, because no faction besides `Player` resolves to a real `player_id` a literal
ownership reassignment could target. **There is nothing left for this module to build for (b).** This
spec covers **only (a)** — a destroyed legion's cargo fate — and treats (b) as closed, citing
`spec-sector-storage.md` rather than re-deriving or re-implementing it.

## Objective

When a legion (`WorldEntity`) is destroyed — removed from the world between one turn and the next —
its cargo (`rpg_world_entity_cargo`) becomes a **revisit-lootable cache** at the legion's last known
sector, reusing `deployment-hierarchy`'s `corpse-cache` shape (`place_kind`/`place_ref`/
`rpg_corpse_cache_item`) rather than inventing a second cache mechanism. This mirrors the owner's own
framing: a legion's cargo dying with it should feel like the same kind of loss a specimen's gear does on
death, not a silent, un-recoverable vanish and not a free return to the empire stash either.

Success looks like: a legion carrying three items is destroyed; a `rpg_corpse_cache` row appears pinned
to the sector the legion was last at, containing exactly those three items (moved, not copied, from
`rpg_world_entity_cargo` into `rpg_corpse_cache_item`); the legion's own cargo rows are gone (cascade or
explicit delete, never left orphaned); a later party or legion can revisit that sector and loot the
cache through the existing corpse-cache retrieval path — no new retrieval mechanism is built here.

## Locked anchors

- **Destroyed-legion cargo fate — RESOLVED 2026-09-13 (owner, /spec kickoff).** *"Cargo drops as a
  revisit-lootable cache."* Explicitly chosen over "destroyed outright" and "returns to empire stash" —
  see `scoped-inventory-hierarchy-ideal.md` Open question #7 for the full framing this locks in.
- **Reuse `corpse-cache`'s shape, do not fork a second cache mechanism** (map's own external-dependency
  row): `deployment-hierarchy/spec-corpse-cache.md`'s `rpg_corpse_cache`/`rpg_corpse_cache_item` tables,
  `(place_kind, place_ref)` keying, and decay/void machinery (owned by that program's own `cache-decay-
  void` module, not rebuilt here) are the target — this module's only new work is **widening
  `place_kind`** to accept a legion's last sector as a valid place, and the move logic that populates it
  at the right moment.
- **Move, never copy, one transaction — inside the same turn-commit `tx`, before the cascade fires.**
  The turn-commit transaction (`RpgStore.WorldTurns.cs:509-537`) already contains `DiffEntities`
  (`RpgStore.WorldGraphDiff.cs:295-321`), which deletes a legion's `rpg_world_entities` row the instant
  it is missing from the new `WorldState` (`DeleteMissing(...) at :300-301`). **Real risk found this
  session:** `rpg_world_entity_cargo`'s own foreign key is `ON DELETE CASCADE`
  (`spec-legion-cargo.md` §Design 2) — if this module's cache-creation logic does not run **before**
  that delete lands, the cargo rows are silently destroyed with no chance to ever create the cache. This
  is not a hypothetical; it is the literal behavior of the schema module 1 already shipped.

## What already exists

### Built (dependency modules, spec'd this session, not yet coded)

| Finding | Evidence |
|---|---|
| `rpg_world_entity_cargo` — the source of what must move, `(world_id, entity_id, seq)` | `spec-legion-cargo.md` §Design 2 |
| `DiffEntities`'s `DeleteMissing` call — the exact site a legion's row (and, via cascade, its cargo) disappears | `RpgStore.WorldGraphDiff.cs:295-301` |
| `rpg_corpse_cache`/`rpg_corpse_cache_item` — the target shape, `place_kind`/`place_ref` header + contents, `kind ∈ {instance, stack}` | `docs/architecture/deployment-hierarchy/spec-corpse-cache.md` §Design 1 |
| `corpse-cache`'s move-never-copy discipline, already proven for unique-specimen death | `spec-corpse-cache.md:240-241` |
| `WorldEntity.AtSectorId` — the legion's last known place, read from the **previous** `WorldState` (the entity is already gone from `next`) | `WorldState.cs:292-293`; `DiffEntities`'s own `before`/`after` dictionaries, `RpgStore.WorldGraphDiff.cs:297-298` |

### Real gap

| Gap | What would have to be built |
|---|---|
| ~~`corpse-cache`'s `place_kind` enum has no legion-death member~~ — **RESOLVED 2026-09-13.** `spec-corpse-cache.md` §Design 1 originally declared `place_kind ∈ {'lawn', 'delve_room', 'siege', 'world_sector'}`, none of which named "a sector, because a legion that was standing there died." Accepted resolution (`spec-corpse-cache.md` §1a): reuse the already-reserved `'world_sector'` value for the at-sector case, add a new `'world_lane'` value for the on-lane case, and carry the "why" on a new `source_kind = 'legion_death'` value instead of on `place_kind`. See §Design 1 below. |
| Nothing runs cache-creation before `DiffEntities`'s cascade delete | Confirmed this session — `DiffEntities` (`:295-321`) has no call to anything cache-shaped; it is a pure entity-row diff. This module's own hook (§Design 2) is the first. |

## Design

### 1. `place_kind` widened — ACCEPTED, RESOLVED 2026-09-13 (owner-authorized cross-program session)

This module's original ask proposed a brand-new `place_kind = 'legion_death'` value. Re-examined this
session against `deployment-hierarchy/spec-corpse-cache.md`'s own schema comment — which already
reserved `'world_sector'` verbatim (*"'lawn' | 'delve_room' | 'siege' | 'world_sector' (siege/world
unused until those programs land)"*) — the accepted resolution is narrower and reuses more of what
already existed, rather than adding a new value for something the vocabulary already had a slot for:

- **A legion that dies at a sector** (`AtSectorId` set) uses the **already-reserved** `place_kind =
  'world_sector'`, `place_ref = AtSectorId` (not the legion's own `EntityId`; a sector is a durable
  place a party can revisit, an already-deleted legion row is not) — this program is the first real
  consumer of that value (`spec-corpse-cache.md` §1a, amended). `place_kind` names the *shape of the
  place* (a world-map sector); it was never meant to name *why* the cache exists — that is
  `source_kind`'s job.
- **The reason** ("a legion's cargo died here") is carried by a **new `source_kind = 'legion_death'`**
  value instead, alongside the existing `'death'`/`'wipe'` — matching `spec-corpse-cache.md`'s own
  stated role for that column: *"audit trail, never branched on downstream."* Modules 4/5/6 gain no
  `source_kind = 'legion_death'` branch; a legion-cargo cache is handled identically to any other
  `world_sector` cache once created.
- **A legion that dies on a lane** (`OnLaneId` set, `AtSectorId` null — `WorldState.cs:292-295`,
  re-confirmed this session) is a genuinely **new** case: `WorldLane` (`WorldState.cs:246-263`,
  re-confirmed this session — `LaneId`, `FromSectorId`, `ToSectorId`) is its own durable, persistent
  world-map edge, not an ephemeral concept and not addressable by any sector id. Owner-added
  requirement (2026-09-13, this session): *"if it on the world map and reachable we should allow to
  drop cache, so another legion can go and pickup"* — a lane is exactly as reachable and revisitable as
  a sector, so its cargo gets the same treatment, not the destroyed-outright fallback this spec
  originally proposed. `place_kind = 'world_lane'` (a genuinely new value, since a lane and a sector
  are different id-spaces and packing both under one `place_kind` would make `place_ref` ambiguous to
  any downstream reader), `place_ref = OnLaneId`, `source_kind = 'legion_death'` (same reason-label as
  the sector case — the *place* differs, the *why* does not).

Both values are accepted and live as of `spec-corpse-cache.md`'s own 2026-09-13 amendment (§1a there);
this module's own build is no longer gated on that ask — it proceeds under normal spec-to-code
sequencing, matching the same courtesy `deployment-hierarchy` itself extends when asking `party-dungeon`/
`item` for their own closed vocabularies, now closed on this side too.

### 2. The hook — inside `DiffEntities`, before `DeleteMissing`

```
// RpgStore.WorldGraphDiff.cs, DiffEntities, inserted before the existing DeleteMissing call:
var destroyed = before.Keys.Where(k => !after.ContainsKey(k)).ToList();
foreach (var entityId in destroyed)
{
    var lastKnown = before[entityId];   // AtSectorId/OnLaneId read from `previous`, not `next`
    // WorldEntity.AtSectorId/OnLaneId are mutually exclusive and one is always set for a live entity
    // (WorldState.cs:292 doc comment: "At a sector, or on a lane -- never both, never neither") --
    // there is no third branch and no default-to-destroyed path.
    if (lastKnown.AtSectorId is not null)
        MoveLegionCargoToCorpseCacheUnlocked(db, tx, worldId, entityId,
            placeKind: "world_sector", placeRef: lastKnown.AtSectorId, lastKnown.OwnerFactionId);
    else if (lastKnown.OnLaneId is not null)
        MoveLegionCargoToCorpseCacheUnlocked(db, tx, worldId, entityId,
            placeKind: "world_lane", placeRef: lastKnown.OnLaneId, lastKnown.OwnerFactionId);
}
DeleteMissing(db, tx, "rpg_world_entities", "entity_id", next.WorldId, destroyed);   // existing line, now runs after
```

`MoveLegionCargoToCorpseCacheUnlocked` now takes an explicit `placeKind` parameter (RESOLVED
2026-09-13 — was reasoned only for the at-sector case before this amendment) and resolves-or-creates a
`rpg_corpse_cache` row for `(place_kind, place_ref, source_kind='legion_death')` — `corpse-cache`'s own
`ResolveOrCreateCacheUnlocked` already resolves-or-creates per `(place_kind, place_ref)`, so this is a
second legal `place_kind` argument to an existing function, not new resolve logic. It then moves every
`rpg_world_entity_cargo` row for that `entityId` into `rpg_corpse_cache_item` (delete + insert, same
shape module 1's own legion-to-legion transfer already uses) — **before** the pre-existing
`DeleteMissing` call runs, so the cascade delete on `rpg_world_entities` finds an already-empty cargo
table for that entity, never a race against real data. A legion with **no** cargo (never loaded
anything) resolves-or-creates nothing — no empty cache rows for an empty legion.

A legion that died **on a lane** (`AtSectorId is null`, `OnLaneId` set instead, `WorldState.cs:292-295`)
is now a **first-class case, resolved 2026-09-13** (§Design 1), not a destroyed-outright fallback: the
`else if` branch above produces a `place_kind='world_lane'` cache exactly like the sector branch
produces a `place_kind='world_sector'` one, both `source_kind='legion_death'`, both moved through the
identical `MoveLegionCargoToCorpseCacheUnlocked` code path.

### 3. What this module does not do

Decay of the resulting cache, void transition, and retrieval are entirely `deployment-hierarchy`'s
`cache-decay-void`/`cache-field-access`/`cache-retrieval-mission` modules' job — this module creates one
cache row and moves items into it, exactly like `corpse-cache` itself does for a specimen's death, and
stops there. It does not touch `decay_started_turn`/`in_void` or any retrieval path.

## Tunables

None introduced. The cache this module creates decays on `cache-decay-void`'s existing turn-counted
clock, using that module's existing tunable (`≈112 turns`, per `deployment-hierarchy-ideal.md`'s own
locked target) — no second decay rate for a legion-death cache.

## Numeric types

No new magnitude — this module moves existing rows verbatim (`instance_id`/`container_id`/`qty`, all
already typed by `corpse-cache`'s and `legion-cargo`'s own specs).

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CargoFate"
.\scripts\guard-dal.ps1        # every new SQL string lives in FusionRpg.Data
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.WorldGraphDiff.cs   MODIFIED — DiffEntities gains the pre-delete
                                                        cache-move hook (§Design 2)
src/FusionRpg.Data/Sqlite/RpgStore.CargoFate.cs        NEW — MoveLegionCargoToCorpseCacheUnlocked,
                                                        calling into deployment-hierarchy's own
                                                        corpse-cache resolve-or-create function now
                                                        that the place_kind ask (§Design 1) is accepted
tests/FusionRpg.Data.Tests/CargoFate/                   NEW
UNTOUCHED: rpg_world_entity_cargo's own schema (module 1); rpg_corpse_cache's own table/column
           definitions (module 3 owns those; this module only writes rows into the two already-live
           `place_kind` values it was granted, §Design 1)
```

## Testing strategy

- **Cargo survives the legion, moved not destroyed (sector case):** a legion with cargo, destroyed at a
  sector between two turns, produces a `rpg_corpse_cache` row with `place_kind='world_sector'`,
  `place_ref` equal to its last `AtSectorId`, containing every item it carried — asserted by row count
  and content equality, not just "no exception was thrown."
- **Lane-death creates a `world_lane` cache, symmetric with the sector case (resolved 2026-09-13):** a
  legion destroyed while `AtSectorId is null`/`OnLaneId` is set produces a `rpg_corpse_cache` row with
  `place_kind='world_lane'`, `place_ref` equal to the lane's `LaneId`, containing every item the legion
  carried — asserted by row count and content equality, mirroring the sector-death test exactly, not a
  second, weaker assertion for the lane branch.
- **The cascade-delete race, explicitly proven absent:** a forced-ordering test confirms the cache-move
  runs and commits its rows to the transaction **before** `DeleteMissing` executes — the exact ordering
  bug this spec's own §Locked anchors names as a real, found risk, not a hypothetical to wave away.
  Proven for both branches (sector and lane), not just the sector one.
- **Empty legion creates no cache:** a legion with zero cargo rows, destroyed at either a sector or a
  lane, creates no `rpg_corpse_cache` row at all.
- **`source_kind` is always `'legion_death'`, never branched on downstream:** both the sector-death and
  lane-death cache rows carry `source_kind='legion_death'`; a regression test confirms no code path in
  this module or its dependents reads that column to change behavior (`spec-corpse-cache.md` §Boundaries
  — audit only).
- **Sector-capture path stays untouched:** a regression test confirms this module adds no logic to
  `DiffSectors` (module 2 already owns that hook) — this module only touches `DiffEntities`.

## Boundaries

- **Always:** run the cache-move before `DeleteMissing`, inside the same transaction; resolve-or-create
  the cache using `deployment-hierarchy`'s own function, never a parallel cache table; route a sector
  death through `place_kind='world_sector'` and a lane death through `place_kind='world_lane'` — never a
  single shared value for both id-spaces.
- **Resolved 2026-09-13:** the `place_kind` widening (§Design 1) — `deployment-hierarchy` accepted the
  ask (reusing `world_sector`, adding `world_lane` and `source_kind='legion_death'`,
  `spec-corpse-cache.md` §1a). This module's build is no longer gated on external sign-off for this ask.
- **Never:** invent a second corpse-cache-shaped table; touch `rpg_world_sector_storage`/`DiffSectors`
  (module 2's own, already-resolved territory); pack a lane's `LaneId` into a `place_kind='world_sector'`
  row (the two id-spaces are never mixed under one `place_kind`, §Design 1); destroy a legion's cargo
  outright for any live `WorldEntity` (per `WorldState.cs:292`'s own invariant, one of `AtSectorId`/
  `OnLaneId` is always set, so a destroyed-outright fallback is never reachable, not merely unlikely).

## Notification hook — named, not built (strengthen pass, 2026-09-13)

A legion's cargo silently becoming a decaying-but-never-voiding, revisit-lootable cache on death is
exactly the kind of delayed-discovery event `docs/architecture/notification-ssot-ideal.md` exists for
— a player who loses a relic-carrying legion has no in-band signal their haul is sitting recoverable
at a specific sector or lane until they happen to look. This module does not build a notification
hook — it fires no new event of its own beyond the existing cache-creation write, which a future
notification consumer can read the same way it would any other `corpse-cache` creation.

## Success criteria

1. A destroyed legion's cargo is provably moved into a `corpse-cache` row (`world_sector` or
   `world_lane`, matching its last-known place) before the entity row itself is deleted. 2. The
   `place_kind` ask is accepted and resolved (2026-09-13), reusing `world_sector` and adding
   `world_lane`/`legion_death` — not silently assumed granted, and not left open. 3. Lane-death is a
   named, tested, first-class case producing a real cache — not a destroyed-outright fallback and not
   silent data loss. 4. `guard-dal.ps1` green. 5. Zero new logic added to `DiffSectors` (confirming §0's
   scope correction held).

## Interface exposed to dependents

None — this is the final module in the map; nothing in this program depends on `cargo-fate`.
`deployment-hierarchy`'s `cache-decay-void`/retrieval modules consume the resulting cache rows exactly
as they would any other corpse-cache, with no new interface needed on their side.

## Design-gate checklist

```
[x] Subsystems: world-map legion state (Core/Data), corpse-cache (external, deployment-hierarchy) — no
    Status/ActorHub/Combat subsystem touched.
[x] Read this session: scoped-inventory-hierarchy-ideal.md Open questions #7/#8; scoped-inventory-
    hierarchy-map.md row 4; spec-cargo-transfer.md, spec-legion-cargo.md, spec-sector-storage.md (all
    three siblings, for the scope-correction in §0); deployment-hierarchy/spec-corpse-cache.md in full;
    decisions.md "Scoped inventory hierarchy SSOT (2026-09-13)".
[x] Code cited by file:line, opened this session: RpgStore.WorldGraphDiff.cs (:295-321 DiffEntities,
    :300-301 DeleteMissing — the real cascade-race finding); WorldState.cs (:292-295 AtSectorId/
    OnLaneId).
[x] Drift reported: the map's own module-4 description ("legion-death cache + sector-capture transfer")
    is corrected in §0 — sector-capture transfer was already fully resolved by module 2, not a gap this
    module needed to close. This is reported, not silently absorbed as "already done, nothing to say."
[x] No §2 invariant contradicted: SQL only in FusionRpg.Data; no cap on a magnitude; no `f(Θ)`; no
    second ActorHub composer; no second corpse-cache-shaped table (reuses the existing one via a
    reviewed ask); move never copy, one transaction, and — the new finding this module adds — correctly
    sequenced *before* an existing cascade delete that would otherwise destroy the data first.
[x] Owner-authorized resolution (2026-09-13, later session, re-opened `spec-corpse-cache.md`,
    `spec-cache-decay-void.md`, `spec-cache-field-access.md`, `spec-cache-retrieval-mission.md`,
    `WorldState.cs:246-263`/`:286-309` fresh rather than trusting this file's own earlier citations):
    the `place_kind` ask is accepted — `world_sector` reused, `world_lane` added, `source_kind` gains
    `legion_death` (`spec-corpse-cache.md` §1a). The lane-death case, previously an open question with
    a destroyed-outright fallback, is resolved as a first-class branch (§Design 1/2) per the owner's own
    added requirement that a reachable world-map place should drop a pickable cache. Sibling specs
    `spec-cache-decay-void.md` (V5 amendment) and `spec-cache-field-access.md` (§1a reachability rule)
    were updated in the same session to carry the two new place kinds end to end, not left as a
    schema-only widening with no reader.
```
