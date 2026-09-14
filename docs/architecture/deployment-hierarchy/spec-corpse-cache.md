# Spec: `corpse-cache`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `corpse-cache`, row 3 of the [deployment-hierarchy map](../deployment-hierarchy-map.md)
(wave 1, depends on `injury-tiers` (module 2, built — [spec-injury-tiers.md](spec-injury-tiers.md))
and decisions.md row **Deployment hierarchy SSOT (2026-09-13)**, P2). Ideal:
[deployment-hierarchy-ideal.md](../deployment-hierarchy-ideal.md) §"Corpse-drop, decay, and
retrieval" (owner-locked 2026-09-13) and §"Resolved 2026-09-13 (third clearing round)" item 1
(V1/V4). Consumes `injury-tiers`' interface verbatim: **the entire real-death trigger is
`rpg_unique_actors.phase == Retired`** (`spec-injury-tiers.md` §Interface).

**Amendment 2026-09-13 (later session, owner-authorized) — `place_kind`/`source_kind` widened for a
new consumer.** `scoped-inventory-hierarchy/spec-cargo-fate.md` §Design 1 filed an ask on this module:
a destroyed legion's cargo needs a cache place this table did not yet support. The owner authorized
resolving it directly this session and added a real requirement of their own: a legion that dies **on
a lane**, not just at a sector, must also get a cache. Resolved as: `place_kind` gains a live
`'world_lane'` value (new) and un-reserves the already-declared `'world_sector'` value (reused, not
duplicated); `source_kind` gains `'legion_death'`. See §1a for the full reasoning and the
re-verification that reusing `'world_sector'` is safe.

## Objective

Two events must move gear out of the assignment tables and into a new place-pinned cache, never copy
it: (1) a unique specimen transitions into `Retired` (commanders are never a `Retired` phase and are
struck from this module's scope — see the Real gap correction below) — hardcore-delve permadeath or an
`injury-tiers`-worsened wound, the two producers of "real death," now the *only* two, per module 2's
interface; (2) **any** delve wipe, permadeath rung or not — the entire party's pack (assigned corpse-
gear, carry-in gear, unbanked haul) empties into the cache, overturning `loot-pack` §7's "carry-in
returns home / haul destroyed" rule (V1/V4, owner-locked, ask filed on party-dungeon). Downed-but-
`Recovering` members (a partial casualty inside a run that is not itself a wipe) drop nothing — gear
stays worn, unchanged from today.

Success looks like: a geared unique dies for real on the lawn — its rolled gear is gone from
`rpg_item_assignment`, present in a new cache row keyed to that match, and the armoury never sees it
again until a retrieval module (5/6) moves it back; a delve wipe below the permadeath rung leaves every
surviving-as-`Recovering` member's pack (worn gear **and** unspent carry-in gear **and** the party's
unbanked haul) in one cache at the room, while the members themselves return home able to fight again
once cured; stripping a specimen's gear mid-deployment cannot dodge the stake, because the gear that
drops is what the specimen had at **deploy time**, never what it happens to be wearing at the instant
of death.

## Locked anchors

- **Move, never copy** (ideal doc, Corrections F2/I1, verified this session): `rpg_item_assignment`'s
  PK is `(specimen_id, role)` with no cache column (`RpgStore.Items.cs:152-159`, confirmed) — a cache
  cannot be "rows pointing at it," because two dead specimens' same-role assignment rows would collide
  under `ON CONFLICT(specimen_id, role) DO UPDATE` (`RpgStore.Items.cs:672-673`, confirmed this
  session — the upsert would silently replace, not merge). The cache is a **new table family**,
  populated by deleting the assignment row and inserting a cache-content row in the same transaction.
- **Only `rpg_item_assignment` (unique), never `rpg_player_item_assignment` (commander pouch).**
  Struck 2026-09-13: an earlier draft of this spec touched both, reasoning from the two tables being
  separate by design ("the two ownership scopes cannot bleed," `RpgStore.Items.cs:162` comment — that
  separation is still real and still why the schema is right). But `rpg_player_item_assignment` is
  Dave's own personal equipment (`CommanderId.Dave` maps to `player:{playerId}`,
  `CommanderId.cs:70` — see [deployment-hierarchy-ideal.md](../deployment-hierarchy-ideal.md)'s
  "commander mortality" correction) and Dave cannot die mid-run in a corpse-drop sense. This module
  never touches that table.
- **Deploy-time snapshot, not death-time state** (anti-fraud, ideal doc §Corpse-drop item 6). **Real
  gap found this session, not assumed:** `SaveAssignment`/`RemoveAssignment` (`RpgStore.Items.cs:662-693`)
  carry **no phase check at all** — nothing today refuses reassigning a specimen's gear while it is
  `Deploying`/`ActiveBound`/`Recovering`. The expedition program already solved the identical problem
  the other way — `RpgStore.Expeditions.cs:61-62` refuses dispatch on a non-`Roster` actor — so this
  module's own anti-fraud ask (refined, filed alongside the item program's existing cache-contents-
  governance ask in the map) is: **`SaveAssignment`/`RemoveAssignment` gain the same `Phase ==
  Roster`-required gate**, mirroring the expedition precedent exactly. That makes "assignment rows at
  death time" and "assignment rows at deploy time" the same rows *by construction* — no snapshot table,
  no diffing, no second copy of the assignment data to keep in sync. Until the item program accepts
  that ask, this module is **blocked on it** for the anti-fraud property specifically (see §What
  already exists, Real gap).
- **`wound.*` never plays a role here beyond the trigger.** This module does not read or write any
  status — it reacts to the `Retired` phase transition module 2 already produces, and to the wipe
  transaction module 4/`delve-attrition` already runs.

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `rpg_item_assignment` (unique) — `specimen_id, role, ref_kind, ref_id, assigned_utc`, `PRIMARY KEY (specimen_id, role)` | `RpgStore.Items.cs:152-159` |
| `rpg_player_item_assignment` (commander pouch) — identical shape, `PRIMARY KEY (player_id, role)` | `RpgStore.Items.cs:164-171` |
| `rpg_item` — the rolled instance itself: `instance_id PK`, `player_id` (the orphan-sweep's *second* reachability root, independent of any binding — `RpgStore.Items.cs:34-38` comment, confirmed), `disposition` ∈ `{owned, salvaged, transferred, destroyed}` (closed four, `:71-73`) | `RpgStore.Items.cs:85-98` |
| `rpg_item_stock` — fungible (unrolled) per-player counts, the shape haul materials/consumables use | `RpgStore.Items.cs:7` (`RpgItemStockRow`) |
| `RetireUniqueActorUnlocked` — idempotent, reusable on an open connection/transaction (already built for extraction settlement); confirmed this session it **only** flips `phase`/`match_key` — touches no gear, no items, nothing this module needs to avoid double-handling | `RpgStore.UniqueActors.cs:358-385` |
| `injury-tiers`' second caller of the same function (module 2) — a worsened wound retires a specimen through the identical, already-idempotent path hardcore permadeath uses | `spec-injury-tiers.md` §Interface |
| Delve wipe already retires every member **on the permadeath rung only** | `RpgStore.Delve.cs:837-849` (`downedOnce: member.DownedOnce \|\| wiped` → `RetireUniqueActorUnlocked`), gated by `PermadeathGate.Applies` (`PermadeathGate.cs:12-14`) |
| `loot-pack` §7's existing wipe settlement (the rule this module's wipe path overturns) | `party-dungeon/spec-loot-pack.md` §7 — haul `destroyed` + `DeleteInstance`; carry-in gear/stock **return home** — confirmed unmodified in this session's read; the overturn is filed as an ask, not built by this module |
| `CloseDelve` is the one writer for delve settlement, one transaction | `spec-loot-pack.md` §7 (`RpgStore.Delve.CloseDelve`), `spec-delve-attrition.md:423` |
| Expedition's phase-gate precedent this module's own ask mirrors | `RpgStore.Expeditions.cs:61-62` — `if (!string.Equals(actor.Phase, UniqueActorPhases.Roster, ...)) return (false, "specimen.deployed", null);` |

### Wiring gap

| Gap | The inert line |
|---|---|
| No mechanism reacts to a `Retired` transition to move gear anywhere | confirmed this session — `grep -rn "RetireUniqueActorUnlocked\|Retired" src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` returns nothing; the two functions have never been connected |
| `CloseDelve`'s wipe branch retires specimens but never touches `rpg_item_assignment`/haul beyond `loot-pack`'s own (about-to-be-overturned) rule | `RpgStore.Delve.cs:837-849` — no item-table write in this range |
| `SaveAssignment`/`RemoveAssignment` have no phase gate | `RpgStore.Items.cs:662-693`, confirmed this session — the anti-fraud property this module needs does not hold until the item program accepts the phase-gate ask above |

### Real gap

| Gap | What would have to be built |
|---|---|
~~Commander "truly dies" trigger~~ — **struck 2026-09-13, permanently out of scope, not pending.** A follow-on investigation ([warden-mortality-ideal.md](../warden-mortality-ideal.md), withdrawn) traced "commander" to its one real meaning — `CommanderId` closed to `{Dave, Zomboss}` (`CommanderId.cs:20-24`), the two world factions, neither of which can die mid-run in a corpse-drop sense. `rpg_player_item_assignment` ("commander pouch") is Dave's own personal equipment and has no death to trigger a move from. The only real candidate for "a bound specimen with a strategic seat that can die" was the Warden mechanic, which the owner separately withdrew on economy-design grounds. **This module never gains a commander-death path** — `rpg_player_item_assignment` stays wear-only (`item-durability-repair`'s scope), never drop-eligible. |
| Cache-contents governance (salvage/stale guard split) | Ideal doc's ask, still open: which of `SalvageGuards.cs:51` / `RpgStore.Workbench.cs:170-172`'s split enforcement applies to a cache-resident item. This module's own table design (below) sidesteps the question for now by keeping cache-resident items **out of** every existing armoury listing query by construction (a new table, not a new disposition value on `rpg_item` — see §Design 1), but the governance answer is still owed before retrieval (modules 5/6) can safely hand a cache item back into normal circulation. |

## Design

### 1. New tables — a family, matching `rpg_delves`/`rpg_delve_rooms`'s header+contents shape

```sql
CREATE TABLE IF NOT EXISTS rpg_corpse_cache (
  cache_id      TEXT NOT NULL PRIMARY KEY,
  place_kind    TEXT NOT NULL,   -- 'lawn' | 'delve_room' | 'siege' | 'world_sector' | 'world_lane'
                                 -- (siege still unused until that program lands; world_sector/world_lane
                                 -- are LIVE as of the 2026-09-13 amendment — see §1a)
  place_ref     TEXT NOT NULL,   -- match_key (lawn), (delve_id, room_r, room_c) packed (delve), reserved
                                 -- for siege, SectorId (world_sector), LaneId (world_lane)
  source_kind   TEXT NOT NULL,   -- 'death' | 'wipe' | 'legion_death' — which trigger created it (audit
                                 -- trail, never branched on downstream)
  created_utc   TEXT NOT NULL,
  decay_started_utc TEXT,        -- NULL until the clock starts (module 4: immediate for lawn, CloseDelve for delve)
  in_void       INTEGER NOT NULL DEFAULT 0,
  revision      INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS rpg_corpse_cache_item (
  cache_id     TEXT NOT NULL,
  seq          INTEGER NOT NULL,       -- insertion order, stable id within the cache
  kind         TEXT NOT NULL,          -- 'instance' | 'stack'
  instance_id  TEXT,                   -- kind='instance': rpg_item.instance_id (rolled gear)
  container_id TEXT,                   -- kind='stack': fungible container id (haul materials/consumables)
  qty          INTEGER,                -- kind='stack' only; long, never negative
  origin_owner TEXT NOT NULL,          -- specimen_id or player_id the item came from — never used to auto-return, audit only
  PRIMARY KEY (cache_id, seq),
  FOREIGN KEY (cache_id) REFERENCES rpg_corpse_cache(cache_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_rpg_corpse_cache_item_instance ON rpg_corpse_cache_item(instance_id) WHERE instance_id IS NOT NULL;
```

Two tables, not one, mirroring the `rpg_delves`/`rpg_delve_rooms` and `rpg_expeditions`/
`rpg_expedition_members` header+contents precedent already in this codebase — a cache's *place* and
*decay state* (module 4's job) live on the header row; *what's in it* is a separate, growable list.
`rpg_item.player_id` is **left untouched** by a move — moving an instance into a cache changes no
column on `rpg_item` itself (disposition stays `'owned'`, the closed four is never extended). An item
is "in a cache" purely by existing in `rpg_corpse_cache_item` with no corresponding
`rpg_item_assignment` row — the same "reachable by owner, not by binding" shape `rpg_item` already
uses (Locked anchors), extended with one more kind of "not currently equipped." Armoury listing
queries (`ListAssignments`, `RpgStore.Items.cs:695-703`) are untouched and, by construction, never
return a cached item — it has no assignment row to list.

### 1a. Amendment 2026-09-13 (later session) — `world_sector`/`world_lane` go live, `source_kind` widens

Resolves the ask filed by `scoped-inventory-hierarchy/spec-cargo-fate.md` §Design 1 (a destroyed
legion's cargo needs a cache place this table did not yet support) plus a real requirement the owner
added directly in this session: a legion that dies **on a lane** (`WorldEntity.OnLaneId` set,
`AtSectorId` null — `WorldState.cs:292-295`, re-opened and confirmed this session) must also get a
cache, not lose its cargo outright, because `WorldLane` (`WorldState.cs:246-263`, re-opened and
confirmed this session — `LaneId`, `FromSectorId`, `ToSectorId`) is exactly as durable and revisitable
a place as a sector: a real, persistent world-map edge, never an ephemeral match/room instance.

**Re-verified this session, not assumed, that reusing `'world_sector'` is safe.** Grepped and read in
full: the three sibling modules that read `place_kind` (`cache-decay-void`, `cache-field-access`,
`cache-retrieval-mission`) treat `'world_sector'` as nothing more than "a value nothing has used yet" —
this schema's own comment ("siege/world unused until those programs land") is the *only* place it was
ever mentioned before this amendment, and no query, test name, or interface line in any of the three
sibling specs branches on it or assumes a different meaning for it. Reusing it for a legion's
cargo-drop is sound because `place_kind` names the **shape of the place** (a world-map sector,
addressed by its own id) — it was never a name for *why* the cache exists. That is `source_kind`'s job,
which is exactly why a future world-actor-combat specimen death at the same sector (real gap, still
unbuilt — `deployment-hierarchy-map.md` "What this program does not touch") would correctly land in
the **same** `world_sector` cache as an earlier legion's dropped cargo, distinguished only by its own
`source_kind='death'` row sitting alongside `source_kind='legion_death'` rows already there — the
"modules 4/5/6 treat every cache identically regardless of `source_kind`" invariant this module already
locks (§Boundaries), never a new exception carved out for this case.

`'world_lane'` is a genuinely **new** value, not a reuse — a lane and a sector are different id-spaces
(`WorldLane.LaneId` vs a sector id), and packing both under one `place_kind` would make `place_ref`
ambiguous to every downstream reader (module 5/6 could not tell, from `place_ref` alone, whether to
resolve a sector or a lane). A dedicated value keeps `place_ref` unambiguous, matching this table's own
existing per-`place_kind` `place_ref` discipline — a different packing scheme per kind is already true
for `lawn` vs `delve_room`, so this is one more case of an existing pattern, not a new one.

`source_kind` gains `'legion_death'` — a legion's cargo dying with it is a **third** reason a cache
exists, distinct from a unique specimen's own `'death'`/`'wipe'`, but it stays purely an audit label:
modules 4/5/6 gain no `source_kind='legion_death'` branch anywhere, per this module's own unchanged
Boundaries line (§Boundaries, updated below to name all three values).

**First real consumer of each newly-live value**, named per this session's own attribution
instruction: `place_kind='world_sector'`, `place_kind='world_lane'`, and `source_kind='legion_death'`
are all first lit up by `scoped-inventory-hierarchy/spec-cargo-fate.md` §Design 1/2 (a destroyed
legion's cargo move-to-cache hook inside `DiffEntities`, before its `DeleteMissing` cascade). No other
module in either program writes these values yet; `cache-decay-void` §Locked anchors carries a
corresponding amendment for how `world_sector`/`world_lane` caches start their decay clock, since a
sector/lane — unlike a lawn match or a closed delve room — never stops being a reachable place.

### 2. Move on `Retired` (death path)

At the point `RetireUniqueActorUnlocked` commits (both existing callers: hardcore permadeath, and
`injury-tiers`' new second caller), in the **same transaction**:

1. Read every row from `rpg_item_assignment WHERE specimen_id = $id`. **`rpg_player_item_assignment`
   is never read here** — the commander-death path that would have justified it is struck (Real gap
   correction, above); the table stays wear-only under `item-durability-repair`.
2. For each, delete the assignment row (`RemoveAssignment`'s existing DELETE statement, inlined on
   the shared transaction — `AdjustStock`-style, since `RemoveAssignment` opens its own connection/
   transaction today and cannot be called inside this one, the same reason `loot-pack` §7 inlines
   `AdjustStock`'s statement rather than calling it).
3. Resolve or create the destination `rpg_corpse_cache` row for the specimen's **place at death**
   (lawn: `match_key`; delve: the specimen's current room, read off `DelveMemberState`/the party's
   room pointer — this module does not invent a new "current room" concept, it reads the one
   `delve-attrition` already tracks).
4. Insert one `rpg_corpse_cache_item(kind='instance', instance_id=...)` row per moved assignment.

Stock-grade kit never drops (Locked anchor, unchanged from the ideal doc) — only rows with a real
`rpg_item.instance_id` behind them move; a `ref_kind` pointing at a stock-backed container is left in
place, since a stock-backed assignment has no instance row to move in the first place.

### 3. Move on any delve wipe (V1/V4 — the `loot-pack` §7 overturn)

Inside `CloseDelve`'s existing wipe branch (`RpgStore.Delve.cs:837-849`), **in addition to** the
existing per-member `RetireUniqueActorUnlocked` call on the permadeath rung:

1. One `rpg_corpse_cache` row per delve room the wipe leaves gear in (`source_kind='wipe'`).
2. For **every** party member (both the ones this wipe just retired, and the ones going to
   `Recovering` instead — V4's "nothing returns home, regardless of rung"): move their
   `rpg_item_assignment` rows exactly as §2, plus move whatever unconsumed carry-in gear/stock and
   unbanked haul `loot-pack`'s own `PackSettlement.Decide` would otherwise have destroyed or returned
   (haul) or returned home (carry-in) — every one of those items becomes a `rpg_corpse_cache_item`
   row instead. **This step does not exist until the party-dungeon program accepts the filed ask**
   (map §External dependencies) amending `spec-loot-pack.md` §7; until then this module's wipe path is
   spec'd but not safe to build, because it would silently diverge from an approved sibling spec.

### 4. What this module does not do

Decay, void transition, and retrieval are modules 4/5/6 — this module only ever **creates** a cache
and **populates** it once. It never reads `decay_started_utc`/`in_void`, never deletes a cache row,
and never moves an item back out. `ActorHub` is untouched — moving an assignment row changes what
`ApplyEquippedGrants`'s full-rebuild projection (`RpgStore.Items.cs:840-874`) sees on the specimen's
*next* materialize, exactly as an ordinary unequip already does; no new combat-derived read is added.

## Tunables

This module introduces no rate, chance, or magnitude — it is pure data movement. `place_kind`/
`source_kind` are closed vocabularies (structural, not tunable): the constructor/insert path refuses
an unrecognized value rather than accepting a free-text string, exempt from the tunables rule as a
closed enum, not a balance number.

## Numeric types

`qty` (stack count) is `long` — a haul stack can, in principle, exceed `int` range under a large drop
volume multiplier (per `CLAUDE.md` "Numeric overflow," any magnitude is `long`, never assumed small).
`seq` is `int` (bounded by a single cache's realistic item count, well under the `int` ceiling — a
structural bound, not a magnitude). No `float`/`double` anywhere in this module.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CorpseCache"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Delve.CloseDelve"   # wipe path regression
.\scripts\guard-dal.ps1        # every new SQL string lives in FusionRpg.Data
.\scripts\guard-actor-hub.ps1  # unaffected — no combat-derived read added
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.CorpseCache.cs   NEW — schema (§Design 1), MoveOnRetireUnlocked,
                                                     MoveOnWipeUnlocked (both callable on an open tx)
src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs  RetireUniqueActorUnlocked gains one caller-supplied
                                                     hook point (or its two existing call sites each gain
                                                     one line calling MoveOnRetireUnlocked on the same tx)
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs         CloseDelve's wipe branch gains the §Design 3 call —
                                                     BLOCKED on the loot-pack §7 ask, do not build early
src/FusionRpg.Data/Sqlite/RpgStore.Items.cs         SaveAssignment/RemoveAssignment gain the Roster-phase
                                                     gate — BLOCKED on the item-program ask, §Locked anchors
tests/FusionRpg.Data.Tests/CorpseCache/             NEW
UNTOUCHED: rpg_item's schema/disposition enum, ListAssignments, ApplyEquippedGrants
```

## Code style

```csharp
// Inside RetireUniqueActorUnlocked's two existing callers, same transaction — never a second commit.
void MoveAssignedGearToCorpseCacheUnlocked(SqliteConnection db, SqliteTransaction tx, string specimenId, string placeKind, string placeRef)
{
    var cacheId = ResolveOrCreateCacheUnlocked(db, tx, placeKind, placeRef, sourceKind: "death");
    foreach (var row in ReadAssignmentsUnlocked(db, tx, specimenId))
    {
        if (row.RefKind != "instance") continue;   // stock-backed assignments never drop
        DeleteAssignmentUnlocked(db, tx, specimenId, row.Role);
        InsertCacheItemUnlocked(db, tx, cacheId, kind: "instance", instanceId: row.RefId, originOwner: specimenId);
    }
}
```

## Testing strategy

- **A real death moves, never copies:** after `RetireUniqueActorUnlocked` commits, the specimen's
  `rpg_item_assignment` rows are gone and an equal number of `rpg_corpse_cache_item` rows exist,
  referencing the same `instance_id`s; `rpg_item.disposition` is unchanged (`'owned'`).
  `ApplyEquippedGrants`'s next full-rebuild for that specimen (if it were ever deployed again — it
  cannot be, it is `Retired`) omits every moved item, proven by reading the projection query directly.
- **Two specimens, same role, one cache — no collision:** two unique actors both wearing (say)
  `core-guard` die into the same lawn-match cache; both survive as two separate `rpg_corpse_cache_item`
  rows (`seq` differs) — the exact PK collision the strengthen-pass correction ruled out for a
  "repoint the assignment row" design is asserted absent here.
- **Stock-backed assignment never drops:** a specimen with a `ref_kind='stock'` role dies; no
  `rpg_corpse_cache_item` row is created for that role.
- **Commander pouch never drops (struck 2026-09-13):** a regression test asserts
  `rpg_player_item_assignment` is never read or written by this module's move path, under any trigger
  — the negative of what an earlier draft of this spec assumed.
- **Anti-fraud, blocked-and-named:** a test asserting `SaveAssignment` refuses on a non-`Roster`
  specimen is written now and marked pending on the item-program ask — proving the property is
  *specified*, even though it cannot yet be *proven* against shipped code.

## Boundaries

- **Always:** move (delete + insert) inside the same transaction as the triggering event; never touch
  `rpg_item`'s own columns on a move; never invent a fifth `disposition` value.
- **Ask first:** the `SaveAssignment`/`RemoveAssignment` phase-gate (item program); the `loot-pack` §7
  wipe-rule amendment (party-dungeon program) — §Design 3 does not build until that lands.
- **Never:** a second reachability root for `rpg_item` beyond `player_id`/binding; branching downstream
  behavior on `source_kind` (`'death'` vs `'wipe'` vs `'legion_death'`) beyond the audit column —
  modules 4/5/6 treat every cache identically regardless of how it was created; reading or writing
  `decay_started_utc`/`in_void`
  (module 4's columns, this module only ever writes them to their initial `NULL`/`0` defaults); reading
  or writing `rpg_player_item_assignment` (struck 2026-09-13 — commander-pouch gear never drops).

## Success criteria

1. A lawn/delve real death moves assigned rolled gear into a cache row, provably absent from
   `rpg_item_assignment` afterward. 2. A delve wipe (once the loot-pack ask lands) empties the whole
   party's pack the same way, regardless of permadeath rung. 3. Two same-role deaths into one cache
   never collide. 4. `guard-dal.ps1` green. 5. Zero new `rpg_item.disposition` values.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `rpg_corpse_cache` / `rpg_corpse_cache_item` schema | `cache-decay-void` (module 4) — owns `decay_started_utc`/`in_void`, reads/writes rows this module only creates |
| `ResolveOrCreateCacheUnlocked` place-keying (`place_kind`/`place_ref`) | modules 5/6 — a field-access or retrieval-mission lookup keys off the identical `(place_kind, place_ref)` pair, never a second place-naming scheme |
| `rpg_corpse_cache_item.kind ∈ {instance, stack}` | modules 5/6 — an `instance` row transfers via the existing `rpg_item`/assignment machinery; a `stack` row via `AdjustStock`-shaped writes, same split as `loot-pack`'s own extraction settlement |
| `place_kind ∈ {lawn, delve_room, siege, world_sector, world_lane}`, `source_kind ∈ {death, wipe, legion_death}` (amended 2026-09-13, §1a) | `scoped-inventory-hierarchy/cargo-fate` — the first writer of `world_sector`/`world_lane`/`legion_death` rows, via this module's own `ResolveOrCreateCacheUnlocked`, never a parallel cache table |

## Design-gate checklist

```
[x] Subsystems: item assignment/ownership (Data), delve settlement (Data), unique-actor FSM (Data) —
    no Status/ActorHub/World subsystem touched.
[x] Read this session: deployment-hierarchy-ideal.md §Corpse-drop; deployment-hierarchy-map.md row 3;
    spec-injury-tiers.md §Interface; spec-loot-pack.md §7 (Wiped) and §Structure.
[x] Code cited by file:line, opened this session: RpgStore.Items.cs (:34-38, :71-73, :85-98, :152-171,
    :662-703), RpgStore.UniqueActors.cs (:358-385), RpgStore.Delve.cs (:837-849), RpgStore.Expeditions.cs
    (:61-62), PermadeathGate.cs (:12-14).
[x] Drift reported: `SaveAssignment`/`RemoveAssignment` carry no phase gate — the ideal doc's
    "deploy-time snapshot" language does not hold against shipped code today; this spec proposes the
    expedition-precedent phase-gate as the fix rather than assuming the snapshot property already
    exists.
[ ] The exact "current room" pointer this module reads at a delve death (§Design 2 step 3) was not
    traced to its own file:line this session — named as `delve-attrition`'s existing tracked state,
    not re-derived here; implementation's first task is confirming the exact field.
[x] No §2 invariant contradicted: SQL only in `FusionRpg.Data`; no cap on a magnitude; no `f(Θ)`; no
    second ActorHub composer; no new `disposition` value invented unilaterally (flagged as owed to the
    item program instead).
[x] Two builds explicitly gated on external sign-off (§Design 3, the phase-gate) are named as BLOCKED
    in §Structure, not silently assumed buildable.
[x] Amendment 2026-09-13 (later session): `place_kind`/`source_kind` widened to accept
    `scoped-inventory-hierarchy/cargo-fate`'s ask (§1a) — re-verified this session, not assumed, that
    reusing the already-reserved `world_sector` value is safe (grepped and read all three sibling
    modules in full; none branches on it beyond "not yet used"). Sibling specs `spec-cache-decay-void.md`
    and `spec-cache-field-access.md` were amended in the same session so the two newly-live values are
    carried end to end, not left as a schema-only widening with no reader.
```
