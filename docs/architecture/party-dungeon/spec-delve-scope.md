# Spec: delve-scope

Status: **APPROVED by the owner 2026-09-05 (wave 1) — not built.** Written against shipped code the same
day the capability map was approved; every line number below was opened this session.

Module id `delve-scope` in the [party-dungeon map](../party-dungeon-map.md) (row 3, wave 1). Depends on
`dungeon-registries` (the room-kind and door-kind registries this module's catalogs read) and on
`decisions.md` row **"World store — delve worlds (2026-09-05)"** (`decisions.md:114`, the map's P2).
**Jointly owned with the world program** — §Structure says which files are whose, and the
`world-map-program.md` row owed is drafted in §Interface.

## Objective

Give a delve somewhere to live **without inventing a second graph type.** The owner decided (ideal §11.9
box #5, `party-dungeon-ideal.md:1665-1670`) that rooms are `WorldSector`s, doors are `WorldLane`s and the
world store persists them — *not* the recommended `DelveGraph`. The review measured the real blast radius
(`audit-2026-09-05.md` §1(a), `:32-58`) and found it **smaller than the box's own remedy list**:
`TurnEngine.Step` has exactly two call sites, both inside a commit for one `world_id`
(`RpgStore.WorldTurns.cs:510`, `:607`), so a delve in its own `rpg_worlds` row is never iterated by the
map's turn and "a mode filter in every world query" is work that does not exist. What breaks is a closed
list — six validation rules, a `LIMIT 1`, a catalog leak, a column-name collision — and this module is it.

Success looks like: a delve row beside a player's map; the map FE still boots onto the map; a rolled graph
validates under the delve profile and is refused under the map profile with the rule named;
`WorldWaveOneAcceptanceTests.The_scenario_hashes_to_its_golden` byte-identical; no path can `Step` a delve.

## Locked anchors

- **Decision 5** (`party-dungeon-ideal.md:1665-1670`): *"Room graph: a scoped `WorldState` … Rooms are
  `WorldSector`s, doors are `WorldLane`s, persisted by the world store."* Its remedy list (`mode='delve'`, a
  filter in every query, party-keyed `Visibility`) was an IDEAL EDIT the review replaced (`audit:57-58`).
- **Review §1(a) ruling** (`audit:49-58`): *"a `parent_world_id` + `kind` column pair (never `mode`), a
  `Validate(world, profile)` overload whose delve profile skips rules 4/5/11/13 and swaps the rule-1/6
  catalogs for a room/door pair not served on `/catalog`, a `kind='map'` filter in `GetActiveWorld`, and
  a hard rule that the delve host never calls `Step` … World goldens do not move (the header row hashes
  `TemplateId, Seed, CurrentTurn` only)."* S1-2 (`audit:196`) files this as the `delve-scope` module.
- **`decisions.md:114`**, quoted: *"**A delve is a `WorldState` row of `kind='delve'`** … `rpg_worlds`
  gains `parent_world_id` and `kind` columns — never `mode`, which is the clock axis
  (`RpgStore.World.cs:25-27`). `WorldValidation.Validate(world, profile)` gains a delve profile that skips
  rules 4/5/11/13 and reads a `RoomTypeCatalog`/`DoorTypeCatalog` pair (same `SectorTypeDef`/`LaneTypeDef`
  shapes) that is **not** served on `/api/world/catalog`. `GetActiveWorld` filters `kind='map'`. **The
  delve host never calls `TurnEngine.Step`** — rooms are moved through by the delve resolver;
  `LegionSupply`, loam, growth and pressure never run on a delve world. World goldens do not move …
  Jointly owned with the world program as module `delve-scope`."*
- **R10–R12** (ideal §11.10): no legion leaves the map — the door issues the Sanctum's delve request with a
  `domainId`; unknown-room pity is **per party**; a once-domain wipe seals the domain but keeps boss loot
  already earned. **Decision 15** (`:1711-1722`): `many` domains are a standing sub-world re-rolled on
  entry, one row per domain; `once` domains are one row per delve, archived and sealed for that player.
  **§4.8** (`:498-513`): parties take separate routes; a player's parties share sight — which
  `Visibility`'s faction keying already gives (`Visibility.cs:92`, `:100`).

## Design

### 1. Schema

All DDL in `FusionRpg.Data`; additive columns through `EnsureColumn` (`RpgStore.cs:3301`; precedent
`RpgStore.World.cs:134-172`; `data-architecture.md:78`). A new `EnsureDelveSchemaUnlocked(db)` is called
from `EnsureWorldSchemaUnlocked` beside `EnsureWorldTurnSchemaUnlocked` (`RpgStore.World.cs:174`).

```sql
-- rpg_worlds: EnsureColumn "kind" TEXT NOT NULL DEFAULT 'map'; EnsureColumn "parent_world_id" TEXT.
-- `mode` (:25) stays the clock axis and is never read for scope.
CREATE INDEX IF NOT EXISTS ix_rpg_worlds_player_kind ON rpg_worlds(player_id, kind, state);

CREATE TABLE IF NOT EXISTS rpg_delves (
  delve_id INTEGER PRIMARY KEY AUTOINCREMENT, player_id INTEGER NOT NULL,
  world_id TEXT NOT NULL UNIQUE,                  -- the kind='delve' rpg_worlds row
  domain_id TEXT NOT NULL, raid_mode TEXT NOT NULL, rung_id TEXT NOT NULL,   -- ids of tuning/ladder rows
  seed TEXT NOT NULL,                             -- ulong as text (rpg_worlds.seed :24; rpg_expeditions.seed RpgStore.cs:561)
  state TEXT NOT NULL,                            -- 'Active'|'Extracted'|'Wiped'|'Archived'
  correlation_id TEXT NOT NULL, entered_utc TEXT NOT NULL, closed_utc TEXT,
  parties_json TEXT NOT NULL DEFAULT '[]',        -- one element per PartyIndex: entity_id, route[], pity{}, haul[]
  decisions_json TEXT NOT NULL DEFAULT '[]',      -- delve-level decision log (route, pack, talk, steer)
  souls_unbanked INTEGER NOT NULL DEFAULT 0, theta_run INTEGER NOT NULL DEFAULT 0,   -- dungeon-loot (wave 3): the at-risk soul ledger (long) and the deepest cleared room's Θ
  quests_json TEXT NOT NULL DEFAULT '[]',                -- delve-quests (wave 4): the offered quest ids with their `need`, verdicts appended at CloseDelve — the stored offer is truth, the rebuild is asserted equal on load
  content_terms_json TEXT,                               -- domain-catalog (wave 4): WorldTier/ZombossLevel/RealmsAdvanced frozen at entry so every room composes against the same terms
  revision INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_rpg_delves_corr   ON rpg_delves(player_id, correlation_id);
CREATE INDEX        IF NOT EXISTS ix_rpg_delves_domain ON rpg_delves(player_id, domain_id, state);

CREATE TABLE IF NOT EXISTS rpg_delve_rooms (
  delve_id INTEGER NOT NULL, sector_id TEXT NOT NULL,   -- = rpg_world_sectors.sector_id in the delve world
  row_index INTEGER NOT NULL, col_index INTEGER NOT NULL,
  kind TEXT NOT NULL, archetype_id TEXT NOT NULL,        -- RoomTypeCatalog id; rooms/<id>.json anchor
  visited INTEGER NOT NULL DEFAULT 0, cleared INTEGER NOT NULL DEFAULT 0,
  key_for_lane_id TEXT,                                  -- the gated lane whose key this room holds (a roll outcome nothing else can rebuild — filed by delve-graph-roll)
  event_id TEXT, resolved_kind TEXT, resolved_archetype_id TEXT,   -- event-deck (wave 3): the drawn event and an unknown room's resolution — draw history the seed alone cannot rebuild; resolved_kind ∈ cache · merchant · fight · event · cage (wild-room §7)
  floor_json TEXT NOT NULL DEFAULT '[]',                 -- unbanked drops on the floor (loot-pack reads)
  revision INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (delve_id, sector_id)
);
```

Three shapes, and why: **per-party state is one JSON array on the header, not a third table** — a raid is
one, two or four parties written in one transaction with the delve (the `squad_json` precedent,
`RpgStore.Expeditions.cs:81`); `loot-pack` splits it out if it ever needs to query a party alone. **Party
position is not stored in `rpg_delve_rooms`** — a party is a `WorldEntity` of kind `Warband`
(`WorldState.cs:64`) owned by the player faction, standing `AtSectorId` (`:260`) in the delve world, which
is what lets `Visibility` see for it (§6) and rule 7 validate it (`WorldValidation.cs:263-303`); a
`party_present` column would be derived state, which `spec-world-model.md:108` forbids. **`parent_world_id`
is nullable** — wave 1 ships the Sanctum picker (map Assumption 2), so a delve may have no parent; the map
door (R10) fills it and nothing reads it yet — it is the seam the world program's later `delving` design
needs.

`RpgStore.cs:749-754`'s reset list gains `DELETE FROM rpg_delve_rooms; DELETE FROM rpg_delves;` ahead of
`rpg_worlds`, for the same orphan reason W21 recorded there (`:744-748`).

### 2. Validation profile

`WorldValidation.Validate(world)` (`WorldValidation.cs:23-43`) runs **sixteen** rules (header `:4`; "fourteen"
is stale). `Validate(world)` becomes `Validate(world, WorldValidationProfile.Map)`, byte-for-byte today's behaviour.

| Rule | Lines | Map profile | Delve profile | Why |
|---|---|---|---|---|
| order | `:46-52` | kept | kept | hashing and replay lean on it |
| 1 catalog ids | `:71-112` | kept | **catalog-swapped**: `SectorTypeCatalog.IsKnown` (`:102`) → `profile.SectorTypeKnown`; `LaneTypeCatalog.IsKnown` (`:110`) → `profile.LaneTypeKnown`; faction, owner and `FactionPolicies` checks (`:77-98`) unchanged | a room kind is not a `SectorTypeCatalog` row and must not become one (§3) |
| 2 lane shape | `:114-135` | kept | kept | a DAG has no self-lanes or duplicate pairs |
| 3 connected | `:137-163` | kept | kept | reachability is the graph roller's own throw (ideal §11.1) |
| 4 homeworld | `:165-182` | kept | **skipped** | a delve has no Home, no Seat, no capital |
| 5 seat counts | `:184-196` | kept | **skipped** | `boss-lair` has `CanHostSeat = true` (`SectorTypeCatalog.cs:98`); room kinds never host a Seat |
| 6 slot shape | `:198-222` | kept | **catalog-swapped**: `SectorTypeCatalog.Get` (`:202`) → `profile.SectorType`; contiguity and intact-guard checks unchanged | rooms carry zero slots in v1, so this is vacuous but must not throw on lookup |
| 7 entity placement | `:263-303` | kept | kept | parties are entities at a room |
| 8 belief refs | `:230-261` | kept | kept | intel rows still name real rooms |
| 9 fracture bound | `:306-312` | kept | kept | defaults pass |
| 10 loam ≥ 0 | `:315-320` | kept | kept | defaults pass |
| 11 home rootbed | `:328-333` | kept | **skipped** | `Sectors.Single(Home)` throws on zero homes |
| 12 handicap bound | `:336-342` | kept | kept | defaults pass |
| 13 template size | `:349-356` | kept | **skipped** | `SizeIdOf` throws for any id but two (`WorldTemplateCatalog.cs:25-30`); a delve's `TemplateId` is the `layoutTemplateId` |
| 14 structure kind | `:362-380` | kept | kept | vacuous with no slots; harmless |
| 15 recruit ≥ 0 | `:383-388` | kept | kept | defaults pass |
| 16 project pairing | `:394-400` | kept | kept | defaults pass |

Rule 1 keeps its faction half in both profiles: a delve world has a player faction and one `Wild` faction
(`FactionKindCatalog.cs:7-15`) with `PolicyId = null`, so no AI ever acts — there is no turn to act in.

### 3. Catalogs

`RoomTypeCatalog` and `DoorTypeCatalog` live in `Core/Delve/` as **`IReadOnlyList<SectorTypeDef>`** and
**`IReadOnlyList<LaneTypeDef>`** (`SectorTypeCatalog.cs:19-34`, `LaneTypeCatalog.cs:7-29`) — the same
records, so `WorldSector.TypeId`/`WorldLane.TypeId` carry them unchanged. **They are projections, not a
second owner:** `dungeon-registries`' `RoomKindCatalog`/`DoorKindCatalog` hold the *rules* (climate
neutrality, secret eligibility, boss row, adjacency bans, joined weights) read from the same registry
files; these two catalogs project that registry into the `SectorTypeDef`/`LaneTypeDef` shape
`WorldValidation` rules 1 and 6 need, and nothing else. One registry, two views, both named in
`decisions.md:114`. Rows come from
`data/seed/dungeon/_registry/room-kinds.json` and `door-kinds.json` (module `dungeon-registries`), one per
kind (`fight · elite · cache · curio · wild · shrine · rest · merchant · trap · unknown · boss`), and pass the
**existing** validators `SectorTypeCatalog.Validate` (`:116-148`) and `LaneTypeCatalog.Validate`
(`:80-105`): rooms have `CanHostSeat = false` and empty `AllowedSlotTypes`, so the Seat pairing (`:139-144`)
passes; `boss` carries `Flags = Boss`; doors are `door`, `one-way-door` (`OneWay`), `gated-door` (`Gated`),
`secret-door`, each `CostMultiplierMilli = 1000` (`> 0` required, `:90-91`; nothing marches, so inert).
`BaseDangerBand` is 0 on every room: depth is the sector's own `DangerBand` (`WorldState.cs:137`), composed
by `difficulty-ladder`, never a catalog constant.

**Not served on `/api/world/catalog`.** `WorldEndpoints.cs:259-300` projects the four map catalogs for the
frozen map FE (`world.ts:372-377`) and does not change; room and door kinds reach the wire only through
`delve-stage`'s projection, and a snapshot test pins the DTO's field set (§Testing).

### 4. `GetActiveWorld` and the FE bootstrap

`GetActiveWorld` is `WHERE player_id = $p AND state = 'active' ORDER BY world_id LIMIT 1`
(`RpgStore.World.cs:384-388`) and the map FE boots from it (`GET /api/world/{playerId}`,
`WorldEndpoints.cs:28-32`; `world.ts:380-386`): a delve id sorting before the map id would silently become
the player's map. Fix: `AND kind = 'map'`. `WorldHeaderRow` (`:690-692`) gains `Kind` and `ParentWorldId`
appended; `ReadHeader` (`:672-675`) and the header SELECTs (`:385`, `:664`; `GetWorldHeader`,
`RpgStore.WorldTurns.cs:639`) read them. `WorldHeaderDto` (`WorldDtos.cs:8-16`) is unchanged — it only ever
describes the map. `CreateWorld` (`:181-216`) writes no `kind` and gets `'map'`; only `CreateDelve` (§7)
writes `'delve'`.

### 5. The never-`Step` rule, and what runs instead

`TurnEngine.Step(world, commands, seed, resolver)` (`TurnEngine.cs:97-98`) is called at
`RpgStore.WorldTurns.cs:510` (commit) and `:607` (replay); neither may ever see a delve world. If one did,
`LegionSupply.Resolve` destroys any out-of-supply entity once a Seat exists (`World/Loam/LegionSupply.cs:124-140`
— under `Loam/`, not `Movement/`) and loam, growth and pressure would run on rooms. Three refusals, one guard:

- `CommitWorldTurn` (`:466`) and `SubmitWorldCommands` (`:81`) refuse `header.Kind != "map"` with
  `world.not-a-map` before any write. Replay (`:600-603`) refuses the same way — today it rebuilds through
  `WorldTemplateCatalog.Build(header.TemplateId)` (`:603`), which throws on a layout template id
  (`WorldTemplateCatalog.cs:44`), so it would fail loudly by accident; make it refuse on purpose.
- `RulesetVersion` stays 7 (`TurnEngine.cs:58`): no `Step` behaviour changes, nothing to bump.
- **Guard test:** no source under `Core/Delve/` or `Data/Sqlite/RpgStore.Delve*.cs` mentions
  `TurnEngine.Step` — the source-scan shape of `WorldCanonicalSeamGuardTests.cs:30`.

What moves parties is the **delve resolver**: a room move is an `UPDATE` of the party's
`WorldEntity.AtSectorId` plus `rpg_delve_rooms.visited`, one transaction, through `RpgStore.Delve.cs`.
Door rules reuse `MarchResolver`'s two checks (`MarchResolver.cs:57-60`: `OneWay` passable only from
`FromSectorId`; `Gated && GateKeyId != null` refuses), lifted into a four-line
`LaneGate.Refusal(LaneTypeDef, WorldLane, string at)` in `World/Movement/` (world program's file) so both
callers read one rule; the delve caller passes the `DoorTypeCatalog` def. A carried key clears
`WorldLane.GateKeyId` (`WorldState.cs:228`) exactly as the map does.

### 6. Sight

`Visibility.SeenBy(world, factionId)` (`Visibility.cs:40-41`) is reused **unchanged**: owned sectors are
`Full` (`:92-93`); every non-`Guard` entity of the faction projects (`ZoneOfControl.cs:29`) `Full` on its
room and `Glimpse` one lane out (`SightLanes = 1`, `:33`). Keyed on faction (`:92`, `:100`), two parties of
one player share sight — exactly §4.8. A **cleared** room is written with `OwnerFactionId = player`, so it
stays `Full` after the party leaves: "visited" memory without running the Intel phase.

The **per-party overlay** is `DelveSight.ForParty(world, partyEntityId, tuning)` in `Core/Delve/` — a
pure read over the delve world: the party's room `Full`, rooms within the **room's own radius** —
`sight.lanes + bands.sightBand.{archetype.sightBand}.extraLanes` (`sight.scoutLanes` plus the same extra
after a scout outcome), the radii `delve-graph-roll` exposes as facts so a `dim` room is not lit —
`Glimpse`, else `None`, intersected with the faction's `SeenBy`. Radii come from
`dungeon.v1.json` per ideal §11.1, not `Visibility.cs:33`'s `const`, which the faction floor keeps. No
change to `world-intel`, `FactionIntel` or `IntelRecorder`.

### 7. Lifecycle

```
CreateDelve  ──▶ Active ──▶ Extracted ─┐
  (seed sealed,     │                   ├─▶ once-domain: Archived (cold archive, graph rows deleted)
   graph validated) └────▶ Wiped ───────┘   many-domain: world row kept, graph re-rolled next entry
```

`CreateDelve(playerId, domainId, raidMode, rungId, correlationId, parentWorldId?, world, rooms)`, one
transaction: validate under the delve profile → insert `rpg_delves` → insert the `rpg_worlds` row
(`kind='delve'`, `parent_world_id`, `template_id = layoutTemplateId`, `seed`, `current_turn = 0`, `mode` at
its `'turn'` default and never read) → `WriteWorldGraphUnlocked` (`RpgStore.World.cs:234`) →
`rpg_delve_rooms`. Correlation-idempotent like expeditions (`spec-expeditions.md:53`). The seed is
server-rolled at commit and never leaves the server (`WorldHeaderDto` carries none, `WorldDtos.cs:8-16`);
`CurrentTurn` is 0 for the delve's whole life.

- **`many` domains** (decision 15): world id `delve-{domainId}-p{playerId}`, one standing row per
  (player, domain); on entry the graph is **replaced** under a fresh sealed seed — `WriteWorldGraphUnlocked`
  is already clear-and-rewrite (`:219-224`) — and a new `rpg_delves` row points at it. The world row's
  `state` stays `'active'`; it is not the map's, so `GetActiveWorld` never sees it.
- **`once` domains**: world id `delve-{domainId}-{delveId}`, one row per delve. On close the graph rows go to
  the cold archive **before** the hot delete (`decisions.md:81`; `IColdArchiveWriter`,
  `data-architecture.md:125`), the world row becomes `'closed'`, and `rpg_delves.state = 'Archived'` is the
  sealed-for-this-player marker `domain-catalog` reads. R12: `Wiped` seals only under
  `onceEntry.sealOnWipe`; boss loot already granted left through `dungeon-loot` at the clear and is not here.

`spec-world-model.md:116` left "what happens to an ended world" open; this answers it for delve worlds only.

### 8. Goldens

`WorldWaveOneAcceptanceTests.The_scenario_hashes_to_its_golden` (`:369-373`, `GoldenFinalHash` `:167`)
plays twenty `Step`s of a `first-light` world and hashes `WorldCanonical.Write`. Nothing here reaches it:
the header row is `Row(sb, "world", w.TemplateId, w.Seed, w.CurrentTurn)` (`WorldCanonical.cs:27`);
`WorldState` has no kind field (`WorldState.cs:299-319`) and gains none; the two `rpg_worlds` columns are
never read into it; the map catalogs' seed rows are untouched; `RulesetVersion` stays 7. What **would** move
one, and is therefore Never: a `Kind` on `WorldState` or in the `"world"` row; room kinds in
`SectorTypeCatalog.Seed` (`:55-102`); a map-profile rule change; any `Step` change. `:47-166` is the re-bless log.

## Numeric types

`delve_id`, `player_id`, `revision` are `long`. `seed` is `ulong` in Core, stored as TEXT
(`RpgStore.World.cs:24`, `:210`; `ReadHeader` `:674` parses it back). `DangerBand`, `row_index`,
`col_index` and pity counters are `int` — indices and bounded counters, not magnitudes. Every amount in
`haul[]` and `floor_json` is `long`; nothing on this surface multiplies, so the minting modules own widening.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests   --filter "FullyQualifiedName~World|FullyQualifiedName~Delve"
dotnet test tests\FusionRpg.Data.Tests   --filter "FullyQualifiedName~World|FullyQualifiedName~Delve"
.\scripts\guard-dal.ps1                  # unchanged: all new SQL in FusionRpg.Data
```

## Structure

```
WORLD PROGRAM'S FILES (edited under joint ownership, reviewed by that program)
src/FusionRpg.Core/World/WorldValidation.cs       → Validate(world, profile); WorldValidationProfile; Map = today
src/FusionRpg.Core/World/Movement/LaneGate.cs     → the two door checks lifted from MarchResolver.cs:57-60
src/FusionRpg.Data/Sqlite/RpgStore.World.cs       → EnsureColumn kind/parent_world_id; GetActiveWorld kind='map'; header SELECTs, ReadHeader, WorldHeaderRow
src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs  → world.not-a-map refusals at :81, :466, :600
src/FusionRpg.Data/Sqlite/RpgStore.cs             → reset list :749-754 gains the two delve tables

THIS MODULE'S FILES
src/FusionRpg.Core/Delve/RoomTypeCatalog.cs, DoorTypeCatalog.cs → registry → SectorTypeDef/LaneTypeDef lists, existing validators
src/FusionRpg.Core/Delve/DelveSight.cs, DelveWorldIds.cs        → per-party Glimpse overlay (pure); the two world-id shapes
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs       → EnsureDelveSchemaUnlocked, CreateDelve, LoadDelve, MoveParty, MarkRoom, AppendDecision, CloseDelve
tests/FusionRpg.Core.Tests/Delve/, tests/FusionRpg.Data.Tests/Delve/

UNTOUCHED: WorldState.cs, WorldCanonical.cs, TurnEngine.cs, SectorTypeCatalog.cs, LaneTypeCatalog.cs,
           Visibility.cs, WorldEndpoints.cs, world.ts
```

## Code style

Catalog discipline as the world catalogs (unknown → throw at startup); the store partial mirrors
`RpgStore.World.cs` (gate-serialized, one transaction, revision bump); integer-only; no SQL outside Data.

```csharp
/// <summary>Which rules apply and which catalogs answer rules 1 and 6. `Map` is today's behaviour exactly.</summary>
public sealed record WorldValidationProfile(
    string Name, Func<string?, bool> SectorTypeKnown, Func<string, SectorTypeDef> SectorType,
    Func<string?, bool> LaneTypeKnown,
    bool RequireHomeworld /* 4, 11 */, bool RequireSeatCounts /* 5 */, bool RequireTemplateSize /* 13 */)
{
    public static readonly WorldValidationProfile Map = new("map",
        SectorTypeCatalog.IsKnown, SectorTypeCatalog.Get, LaneTypeCatalog.IsKnown, true, true, true);
    public static WorldValidationProfile Delve(RoomTypeCatalog rooms, DoorTypeCatalog doors) =>
        new("delve", rooms.IsKnown, rooms.Get, doors.IsKnown, false, false, false);
}
public static WorldState Validate(WorldState world) => Validate(world, WorldValidationProfile.Map);
public static WorldState Validate(WorldState world, WorldValidationProfile profile) { /* §2 table */ }
```

## Testing strategy (gate G1, `party-dungeon-map.md:157`)

- **Coexistence + bootstrap (Data):** a `first-light` map and a delve for one player, the delve id sorting
  first; both round-trip through `WorldCanonical`; `GetActiveWorld` returns the map.
- **Profiles (Core):** a rolled graph validates under `Delve`; under `Map` it throws *"A world needs exactly
  one homeworld sector; found 0"* (`WorldValidation.cs:172`); `first-light` under `Delve` throws rule 1.
- **Goldens:** `WorldWaveOneAcceptanceTests.The_scenario_hashes_to_its_golden` (`:369`),
  `The_same_script_and_seed_replay_to_the_same_twenty_hashes` (`:341`) and
  `The_pure_engine_reproduces_the_stored_hashes_from_the_command_log_alone` (`:347`) unchanged; a
  `WorldCatalogDto` field-set snapshot proves `/catalog` did not grow.
- **Never-Step:** the source-scan guard (§5); commit, commands and replay on a delve id return
  `world.not-a-map` and write no `rpg_world_turn_log` row. **Sight:** two parties in different rooms —
  faction `SeenBy` is the union, each `ForParty` its own cone; a cleared room stays `Full` after leaving.
- **Lifecycle:** `many` re-entry replaces the graph and appends a delve row; `once` close archives before
  delete and leaves `Archived`; a replayed `CreateDelve` with the same correlation returns the recorded row.
- **Guards:** `guard-dal.ps1` green (patterns `:17-26`; every new SQL string is in `RpgStore.Delve.cs`).

## Boundaries

- **Always:** validate before any write; one transaction per delve mutation; `kind`/`parent_world_id` via
  `EnsureColumn`; room and door kinds from the registry through the existing catalog validators; parties
  as `Warband` entities; seed sealed server-side and never on the wire.
- **Ask first:** a third delve table (**taken 2026-09-05 — wave 3 adds two:** `loot-pack` §4's `rpg_delve_pack_lock(delve_id, instance_id UNIQUE)`, the `rpg_expedition_members` shape, so an instance rides one delve at a time and home paths can refuse it; and `event-deck` §8's `rpg_delve_event_seen(player_id, scope, scope_key, event_id, delve_id)` for `per-domain` / `once-per-player` repeat scopes — it outlives the graph rows and the `once` archive; `RpgStore.Delve.cs` is its only writer); `Kind` on `WorldHeaderDto`; a delve-owned `Intel` phase; any change
  to the map profile's rule set; archiving map worlds (the world program's open item).
- **Never:** `mode='delve'` — `mode` is the clock axis (`RpgStore.World.cs:25-27`); a `Mode`/`Kind` field on
  `WorldState` or in `WorldCanonical.Row`; calling `TurnEngine.Step` on a delve world, from anywhere; room
  or door kinds on `/api/world/catalog` or in `SectorTypeCatalog`; SQL outside `FusionRpg.Data`; a stored
  `party_present` or any other derived column.

## Success criteria

1. G1's five clauses green (`party-dungeon-map.md:157`). 2. World goldens byte-identical — proven by running
the Data suite. 3. Map FE boots onto the map with a delve present. 4. `guard-dal` green. 5. The
`world-map-program.md` row below is appended by the world program.

## Interface exposed to dependents

- **`delve-graph-roll`** writes rooms and doors as `WorldSector`/`WorldLane` rows through `CreateDelve` →
  `WriteWorldGraphUnlocked`; its own validator throws first, this profile second. **`difficulty-ladder`**
  writes the composed band into `WorldSector.DangerBand` per room; `rpg_delves.rung_id` records the rung.
- **`loot-pack`, `event-deck`, `wild-room`** read and update per-party `route`/`pity`/`haul` in
  `parties_json` and `rpg_delve_rooms.floor_json` through `RpgStore.Delve.cs` only; **`delve-battle-profile`**
  appends to `decisions_json` beside T10's trace; **`domain-catalog`** reads `rpg_delves(player_id,
  domain_id, state)` for sealing and discovery; **`delve-stage`** reads one Server-assembled projection
  through a single named member — `DelveProjection.For(delveId, playerId)` (added by `spec-delve-stage.md` §18 ask 1,
  2026-09-05), composed of `LoadWorldState` + the two delve tables + `Visibility.SeenBy` + `DelveSight.ForParty` and
  nothing else. One member so the client has one query key and no second source.
- **`world-map-program.md` row owed** (drafted; the world program appends it): *"`delve-scope` — jointly
  owned with the party-dungeon program: `rpg_worlds.kind`/`parent_world_id`, the `Validate(world, profile)`
  overload, the `GetActiveWorld` `kind='map'` filter, the `world.not-a-map` refusals, `LaneGate`. Depends on
  `world-model`, `turn-engine`, `world-movement`. Spec: `party-dungeon/spec-delve-scope.md`.
  `parent_world_id` is reserved for this program's later `delving` design (R10)."*

## Design-gate checklist

```
[x] Subsystems: world store, world validation, world intel, world movement, DAL boundary.
[x] Read this session: party-dungeon-map.md; decisions.md:114 and :81; ideal §4.1-4.2, §4.8, §11.1, §11.9
    box, §11.10; audit §1(a), S1-2, A3 (:417); spec-world-model.md; world-map-program.md;
    data-architecture.md §6; spec-expeditions.md; DESIGN-GATE.md §5.
[x] Every claim cites file:line, re-read today, against CODE (every rule body, both Step sites, the LIMIT 1,
    the /catalog projection, WorldCanonical:27, ReadHeader, WorldHeaderDto). Drift reported: WorldValidation
    has 16 rules, not 14 (:4); LegionSupply.cs is under World/Loam/; Visibility keying is :92 and :100.
[ ] "Goldens do not move" is argued from WorldCanonical's inputs, not yet run — there is no code to run it
    against; the Data suite is the proof and the first build task.
[ ] The map row cites "review §C" for goldens; the audit has no heading C — §1(a):55-56 is cited instead,
    and the map owes a one-word fix.
[x] No §2 invariant contradicted; spec-world-model.md's "ask first" rows (tables beyond the seven, a second
    active world) are answered by decisions.md:114; its "never store derived state" is kept.
[x] Corrections propagated within this spec (Structure, Testing, Boundaries, Interface).
```
