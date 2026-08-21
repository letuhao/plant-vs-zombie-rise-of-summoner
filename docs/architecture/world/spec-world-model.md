# Spec: world-model (wave 1)

**Status:** Draft — pending owner review. Module id `world-model` in the [world map program](../world-map-program.md). No dependencies.

## Objective

The map's nouns and their storage: a world, its sectors, the slots inside them, the lanes between them, the factions that own things, and the entities standing on them. Nothing moves and nothing is built in this module — it exists so that everything later has somewhere to live.

Success looks like: a starter world can be created from a code-authored template, read back identically after a restart, and validated so that a malformed map is rejected loudly at creation rather than discovered as a null three modules later.

## Design

### What a world is

A world is per-player and per-save (`players.id`), created from a **template id** plus a **world seed**, and stamped with the mode fields that the turn engine reads. One active world per player in v1; ended worlds are retained for history.

### Catalogs (code, not DB — the `StatusCatalogBootstrap` precedent)

| Catalog | Contents | Validation |
|---|---|---|
| `SectorTypeCatalog` | id, display name, base danger band, allowed slot mix, whether it may host a Seat, flags (`no-base`, `boss`, `nexus`, `fortress`, `home`) | unknown id rejects; a type that forbids Seats may never be marked base-capable |
| `SlotTypeCatalog` | id, kind (`wildland` `essence-deposit` `shard-vein` `material-seam` `lair` `tear` `vault` `shrine` `market` `spire` `anomaly` `hazard` `seat`), whether it is buildable, whether it yields | unknown id rejects |
| `LaneTypeCatalog` | id (`rift` `corridor` `ley` `deep` `one-way` `gated`), base cost multiplier, whether it carries supply, whether it carries pressure | unknown id rejects |
| `FactionKindCatalog` | `player` · `zomboss` · `clan` · `rival` · `wild` | fixed set |
| `WorldTemplateCatalog` | **v1: one authored template** (`first-light`) — a fixed 6-sector layout with named sectors, slots, lanes, factions, and the homeworld | template output is deterministic from `(templateId, seed)` |

Climates reuse `ElementTypeId` (fire · ice · air · earth · light · dark) — no new element vocabulary.

Catalog discipline is the same rule the species and status catalogs already follow: unknown ids reject at the write gate, and every catalog validates itself at bootstrap (stable ids, no duplicates, referential integrity between them).

### Data (all DDL inside `FusionRpg.Data`, `EnsureColumn` migration style)

| Table | Key | Columns (essence) |
|---|---|---|
| `rpg_worlds` | `id` | `player_id`, `template_id`, `seed`, `mode` (`turn` v1), `turn_period_seconds` (null in turn mode), `catch_up_cap`, `current_turn`, `last_advanced_utc`, `engine_version`, `ruleset_version`, `state` (`active`\|`ended`), `created_utc`, `revision` |
| `rpg_world_factions` | `(world_id, faction_id)` | `kind`, `name`, `policy_id` (AI policy, null for the human), `disposition`, `state` |
| `rpg_world_sectors` | `(world_id, sector_id)` | `type_id`, `climate`, `danger_band`, `phase` (`unknown`\|`explored`\|`contested`\|`held`\|`developed`\|`besieged`\|`lost`), `owner_faction_id` (nullable), `stability`, `pressure`, `depletion`, `development_level`, `intel_state`, `last_seen_turn`, `layout_x`, `layout_y`, `revision` |
| `rpg_world_slots` | `(world_id, sector_id, slot_index)` | `slot_type_id`, `element` (nullable override), `state` (`intact`\|`claimed`\|`depleted`\|`ruined`), `owner_faction_id` (nullable), **`guard_wave_id`** (nullable), **`guard_state`** (`intact`\|`cleared`), `revision` |
| `rpg_world_lanes` | `(world_id, lane_id)` | `from_sector_id`, `to_sector_id`, `type_id`, `length`, `width`, `hazard`, `ward_level`, `gate_key_id` (nullable), `state` (`open`\|`severed`), `revision` |
| `rpg_world_entities` | `(world_id, entity_id)` | `kind` (`legion`\|`warband`\|`guard`\|`caravan`\|`warlord`), `owner_faction_id`, `at_sector_id` (nullable), `on_lane_id` (nullable), `lane_progress` (integer per-mille), `stance`, `movement_remaining`, `state`, `revision` |
| `rpg_world_entity_members` | `(world_id, entity_id, member_index)` | `instance_id` (FK → `rpg_unique_actors`, nullable for non-player forces), `species_id`, `level`, `hp`, `wounds` |

Three deliberate shapes:

- **Guards are per-slot and named, not scored.** A slot carries a `guard_wave_id` — a reference to an encounter the combat stream owns — rather than a strength number this module would have to invent and balance. Guards defend the *thing* (the vein, the lair, the vault), never the ground: a legion may walk through a guarded sector freely, because only hostile *entities* project zone of control. Clearing a rich sector therefore takes several turns and several fights, one slot at a time, and every balance question about how hard a guard is belongs to the module that owns balance.

- **`layout_x` / `layout_y` are stored, not derived.** A graph the player cannot form a mental picture of is unusable, and stable positions across sessions are part of that picture. The generator (wave 4) will emit them; the authored template hard-codes them.
- **Entities live in one table with a `kind`**, not one table per creature class. Every mobile on the map answers the same questions — where is it, who owns it, what is it made of — and forking storage per kind is exactly the pattern the audit flagged.

An entity is either **at a sector** or **on a lane** (with `lane_progress`), never both and never neither — enforced by a check at the store gate.

### Creation and validation

`CreateWorld(playerId, templateId, seed)` builds the whole graph in **one transaction** and validates before committing:

1. Every referenced catalog id exists.
2. Every lane joins two existing sectors; no self-lanes; no duplicate undirected pairs.
3. The graph is connected (no unreachable sector).
4. Exactly one sector is flagged `home`, owned by the player faction, and it holds a Seat slot.
5. Every base-capable sector has exactly one Seat slot; every `no-base` sector has none.
6. Slot indexes are contiguous from 0 and within the sector type's allowed mix.
7. Every entity is at a sector or on a lane, and its lane/sector exists.

Failure rejects the whole creation. A malformed world must never reach the turn engine.

### Reads

`LoadWorldState(worldId)` returns the full graph as a Core model in **stable order** (sectors by id, slots by index, lanes by id, entities by id) — determinism starts here, before the engine is even involved. `revision` on every row supports cheap change detection; the world row's `revision` bumps on any child mutation.

### Server

`GET /api/world/{playerId}` (active world summary) · `GET /api/world/{worldId}/state` (full graph view for the FE) · `POST /api/test/world/create` (SIM only: create from template + seed). SignalR `WorldUpdated` on revision bump. No mutation endpoints in this module — the turn engine owns writes.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests    # catalog validation, model shape
dotnet test tests\FusionRpg.Data.Tests    # creation atomicity, round-trip, validation rejects
.\scripts\guard-dal.ps1                   # all new SQL inside FusionRpg.Data
```

## Structure

```
src/FusionRpg.Core/World/            → WorldState.cs, SectorTypeCatalog.cs, SlotTypeCatalog.cs,
                                       LaneTypeCatalog.cs, WorldTemplateCatalog.cs, WorldValidation.cs
src/FusionRpg.Contracts/             → WorldDtos.cs
src/FusionRpg.Data/Sqlite/           → RpgStore.World.cs (schema, CreateWorld, LoadWorldState)
src/FusionRpg.Server/                → WorldEndpoints.cs (reads only)
tests/FusionRpg.Core.Tests/World/    → catalog validation, template determinism
tests/FusionRpg.Data.Tests/          → creation atomicity, round-trip fidelity, each validation rule
```

## Code style

Catalog bootstrap mirrors `StatusCatalogBootstrap`; the store partial mirrors `RpgStore.UniqueActors.cs` (gate-serialized, revision bumps); DTOs in Contracts; integer-only state (per-mille where a fraction is needed); no Unity, no float, no SQL outside Data.

## Testing strategy

- **Core:** every catalog validates (stable kebab-case ids, no duplicates, cross-catalog references resolve); the `first-light` template produces an identical world from the same seed across two builds.
- **Data:** create → load → assert deep equality; forced mid-creation failure leaves zero rows; each of the seven validation rules has a rejecting test; load order is stable across repeated reads.
- **Guard:** `guard-dal` green.

## Boundaries

- **Always:** one transaction for world creation; catalog discipline (unknown → reject); revision bump on every write; stable ordering on every read.
- **Ask first:** adding tables beyond the seven; making catalogs DB-authored; more than one active world per player.
- **Never:** SQL outside `FusionRpg.Data`; injector involvement; storing derived state that `step` can recompute; float in any stored game value.

## Success criteria

1. `first-light` creates a valid 6-sector world, deterministically, from `(template, seed)`. 2. Round-trip after restart is byte-identical. 3. All seven validation rules reject as specified, atomically. 4. Reads are stably ordered. 5. `guard-dal` and all existing suites green.

## Open questions

Whether ended worlds are archived like the ledgers, retained in place, or reduced to a summary row when a campaign closes — defer to the first world that actually ends. Whether `guard_wave_id` values validate against a real wave catalog in wave 1 or stay opaque strings until `combat-handoff` lands: opaque is cheaper now, validated is safer later, and the answer follows whatever the combat stream exposes first.
