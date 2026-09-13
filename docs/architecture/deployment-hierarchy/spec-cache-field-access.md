# Spec: `cache-field-access`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this session.
Module id `cache-field-access`, row 5 of the [deployment-hierarchy map](../deployment-hierarchy-map.md)
(wave 1, depends on `corpse-cache` (module 3, spec only — [spec-corpse-cache.md](spec-corpse-cache.md))
and `cache-decay-void` (module 4, spec only — [spec-cache-decay-void.md](spec-cache-decay-void.md)); external
`loot-pack` §7 sign-off gates only the wipe-cache half of this spec, not the death-cache half — see
§Locked anchors). Ideal: [deployment-hierarchy-ideal.md](../deployment-hierarchy-ideal.md) §"Baggage,
revisit loot, and the void" (V1/V3/V4/V5/V6) and §"Corpse-drop, decay, and retrieval" item 3 ("Field
retrieval"), §"Resolved 2026-09-13 (third clearing round)" items 1/3/4. Consumes
`party-dungeon/spec-loot-pack.md` in full — its `PackGrid`/`PackArranger`/`PackMoves`/`PackDto`
interface, read this session both as spec **and** as shipped code (see §What already exists — the two
disagree in a way worth reporting).

## Objective

Two ways for a party physically standing in a delve room to recover a corpse-cache's contents, both
reusing `loot-pack`'s existing pack grid rather than inventing a second container: **baggage** (the
same party, live, mid-run, right after a teammate dies in the room they're standing in) and **revisit**
(any party — the same one returning later in the run, or a different `PartyIndex` in the same
multi-party raid arriving later — reaching a room where an earlier death left a cache). Both are the
same mechanism under the hood: claim whatever currently survives in the cache, arrange it into the
claiming party's live grid via the exact algorithm an ordinary loot reveal already uses, spill overflow
to the room floor exactly as an ordinary reveal already does, and record the claim so a second attempt
(a replayed request, or a second party racing for the same room) cannot double-claim a row.

Success looks like: a specimen dies mid-delve, its gear lands in a cache at that room (module 3); the
surviving party walks back into that room before the raid closes and claims it — full inventory if the
pack has room, partial-plus-floor-spill if it does not, nothing lost either way; a second `PartyIndex`
in the same raid reaching the same room later gets whatever the first party left, never a duplicate;
replaying an identical claim request changes nothing the second time; a cache the party never reaches
before the raid closes is untouched by this module — it becomes module 4's problem (decay) and then
module 6's (retrieval), never this module's.

## Locked anchors

- **V3 (closed, ideal doc): baggage is `loot-pack`, not a new container.** "Picking up a corpse's rolled
  gear is a `pack.move`-shaped decision like any other haul pickup" — this module never defines a second
  grid, a second stack cap, or a second footprint table. It calls `loot-pack`'s own placement algorithm.
- **Certain-on-reach (second clearing round, item 3; restated by `cache-decay-void` §Locked anchors,
  binding on this module too): reaching a live cache always recovers whatever decay has not yet taken —
  this module never rolls a second time.** Every row this module reads from `rpg_corpse_cache_item` at
  claim time is unconditionally claimable; there is no success/failure roll here, only a capacity
  question (does it fit in the grid, or does it spill to the floor).
- **Scope split that this session had to verify, not assume (the brief's own open question, resolved
  below): "revisit by a later party" does NOT mean a later, separate delve run.** `CreateDelve`
  (`RpgStore.Delve.cs:164-204`) inserts a brand-new `AUTOINCREMENT delve_id` and a brand-new `world_id`/
  room graph on every call (`WorldValidation.Validate` against a freshly-supplied `world`/`rooms`
  argument, `:184`); `rpg_delve_rooms` is keyed `PRIMARY KEY (delve_id, sector_id)` (`:114-124`) — rooms
  never carry over from one `delve_id` to another. A "later, separate delve run" therefore cannot
  physically re-enter the room a cache is pinned to; there is no code path that would let it. What DOES
  exist, confirmed this session: `raid_mode` (`solo|pair|quad`, `:100`) supports **multiple parties
  inside the SAME `delve_id`** — `SettleExtractionUnlocked` iterates `foreach (var party in
  delve.Parties)` (`:824`) and `loot-pack`'s own spec independently confirms "2/4 parties = 2/4 packs…
  at the boss rendezvous packs stay separate" (`spec-loot-pack.md:1573-1577`). **"A later party" reads
  correctly as a different `PartyIndex` within the same still-`Active` raid, reaching the room on its own
  route after the death occurred** — not a party in some future, unrelated delve. Once `CloseDelve`
  fires (`DelveStates.Extracted`/`Wiped`/`Archived` — `RpgStore.Delve.cs:77-83`, confirmed the only four
  states, no "paused"/"resumable" state exists), the raid is over for every party in it simultaneously
  (`CloseDelve`'s one `UPDATE … WHERE delve_id = $id`, `:785-788`, is not per-party) — there is no
  mechanism, in this module or any other, for a live party to walk back into a closed delve. **This
  module's entire precondition is `state = Active`; it structurally cannot apply once a raid closes.**
- **Amended 2026-09-13 (later session, cargo-fate ask) — this module is no longer delve-only.** The
  claim this bullet originally made ("this module only ever touches `place_kind = 'delve_room'`
  caches") held because `cache-decay-void`'s V5 set `in_void = 1` immediately for every non-`delve_room`
  kind, `world_sector` included. That premise changed: `spec-cache-decay-void.md`'s own V5 is now
  amended (its §Locked anchors) so `world_sector`/`world_lane` — durable, persistent world-map places,
  unlike a lawn match or a closed delve room — stay `in_void = 0` from creation and never flip. This
  module's own `in_void = 0` read (§1) therefore now also surfaces `world_sector`/`world_lane` rows,
  and this module gains a **second, world-map reachability check** for them (§1a, below), alongside its
  unchanged delve-room check (§1). Lawn/siege remain the only kinds this module never sees (`in_void =
  1` immediately, unchanged) — those two, and only those two, go straight to module 6's domain.
- **The `loot-pack` §7 gate applies to the wipe half only, never the death half.** A death-sourced cache
  (`source_kind = 'death'`, module 3 §Design 2) is created **mid-run**, while the raid is still `Active`
  — nothing about reaching it depends on the §7 overturn. A wipe-sourced cache (`source_kind = 'wipe'`,
  module 3 §Design 3) is created **by `CloseDelve` itself**, at the exact instant the raid stops being
  `Active` — by the point any such cache exists, no live party can ever be "in the room" again, so this
  module's whole mechanism (physical presence in an `Active` raid) cannot apply to it regardless of
  whether the §7 ask lands. **A wipe-sourced cache is never reachable by `cache-field-access`, ask or no
  ask — it is `cache-retrieval-mission`'s (module 6) exclusively, once it reaches the void.** This
  module's own build is therefore **not blocked on the `loot-pack` §7 ask at all** — the map's row 5
  "external `loot-pack` §7 sign-off" dependency is corrected here to: relevant only insofar as module 3's
  wipe-cache rows won't exist until then, which affects what this module has to claim, never whether this
  module's own mechanism works.

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `rpg_delves`/`rpg_delve_rooms`/`DelveStates` schema — one raid per `delve_id`, multiple parties, four terminal states, rooms never shared across delves | `RpgStore.Delve.cs:77-141` (schema), `:77-83` (`DelveStates`) |
| `CreateDelve` always mints a new `delve_id`/`world_id`/room graph — the one-shot-instance proof | `RpgStore.Delve.cs:164-204`, specifically `:184` (`WorldValidation.Validate` on the caller-supplied graph) |
| `CloseDelve` closes every party of a raid in one `UPDATE`, one transaction — never per-party | `RpgStore.Delve.cs:762-793`, `:785-788` |
| A party's live position — `rpg_world_entities.at_sector_id`, written by `MoveParty` | `RpgStore.Delve.cs:321` (`MoveParty(long delveId, string worldId, string partyEntityId, string toSectorId, …)`), write at `:347-350`; schema `RpgStore.World.cs:92-102` (`at_sector_id TEXT`) — this is the exact "current room" pointer `spec-corpse-cache.md`'s own Design-gate checklist left untraced ("not traced to its own file:line this session… implementation's first task"); found this session, fed forward here |
| `AppendDecision(long delveId, object decision)` — a generic decision-log append, already used for concerns beyond `pack.move`/`pack.drop` (quest-reward banking, altar-haul minting) | `RpgStore.Delve.cs:491`; non-pack users cited in `CloseDelve`'s own doc comment, `:744-755` |
| `ReadDelveByCorrelationUnlocked` / `ux_rpg_delves_corr(player_id, correlation_id)` — this codebase's established replay-safety idiom, reused here for the claim log | `RpgStore.Delve.cs:111` (index), `:179-180` (`CreateDelve`'s own use), `:1641` (declaration) |
| `loot-pack`'s Core pack layer is **already built**, not "unbuilt" as its own spec header and Design-gate checklist (`spec-loot-pack.md:3`, its own `[ ] Nothing was run — no code exists`) still claim — **drift found and reported this session, not corrected (out of this module's paths):** `PackGrid`, `PackArranger.Arrange`, `PackMoves` (`ApplyMove`/`ApplyDrop`/`Replay`), `PackAutopilot`, `PackDto`, and `PackSettlement` all exist as real, matching implementations | `src/FusionRpg.Core/Delve/Pack/PackGrid.cs` (113 lines), `PackArranger.cs` (32), `PackMoves.cs` (145), `PackAutopilot.cs` (90), `PackDto.cs` (39), `PackSettlement.cs` (87) — all read in full this session |
| `PackSettlement.Decide`/`ApplyPackSettlementUnlocked` are **wired live into `CloseDelve`** today, implementing TODAY's (pre-overturn) §7 rule: wipe destroys haul, returns carry-in — this is shipped, running code, not a future risk | `RpgStore.Delve.cs:776` (`CloseDelve` calls it unconditionally when `tuning is not null`), `:1209-1251` (`ApplyPackSettlementUnlocked`); wipe branch `PackSettlement.cs:66-74` (`DestroyHaulInstance` for haul, `UnlockCarryInInstance`/`BankStack` for carry-in — "carry-in gear returns home") |
| `PackArranger.Arrange(PackGrid grid, IReadOnlyList<PackItem> items)` places new items into an **already-populated** grid's remaining free cells (it does not reset the grid) — the exact call shape this module needs: claimed cache items placed atop a party's existing, mid-run pack | `PackArranger.cs:13-31`, confirmed by reading `PackGrid.IsFree`/`With`'s own incremental-append shape, `PackGrid.cs:81-97` |
| `PackMoves.ApplyMove`'s `From` parameter is a **closed** two-value set (`"grid"`/`"floor"`) — any other value falls through to `pack.not-here`, never a third branch | `PackMoves.cs:19-20`, `:37-69` (both `if` branches, final `return (false, "pack.not-here", …)` at `:69`) — **this rules out reusing `ApplyMove` for a cache-sourced pickup without an ask to extend `loot-pack`'s own closed vocabulary; this module calls `PackArranger.Arrange` directly instead (below), the same function an ordinary loot reveal would call, never `ApplyMove`** |
| `rpg_item.player_id` is untouched by any assignment move (corpse-cache's own Locked anchor, re-verified this session against the same schema) — a claimed instance is **already owned** by the player before the claim; no `AcquireItem`/new-ownership call is needed, only a pack-lock row | `RpgStore.Items.cs:85-98` (`rpg_item` schema, no `player_id` write path implicated by an assignment change) |
| `rpg_delve_pack_lock(delve_id, instance_id UNIQUE)` — the existing "one delve per instance" lock this module reuses unmodified for a claimed `kind='instance'` row | `RpgStore.Delve.cs:126-130` |
| `DropResult`/`DropResultKind` — the shape an ordinary reveal hands to placement; confirms the shape this module's own claimed-item list should mirror for consistency (`Kind, RefId, InstanceId, Count/Qty, GrantIndex`) even though this module builds `PackItem`s directly rather than going through `DropResult` (below) | `src/FusionRpg.Core/Delve/Loot/DropResult.cs:7-20` |

### Wiring gap

| Gap | The inert line |
|---|---|
| `corpse-cache`'s own `rpg_corpse_cache.place_ref` doc comment for a delve cache reads `(delve_id, room_r, room_c)` packed, but the room's real, only primary key this session found is `(delve_id, sector_id)` | `spec-corpse-cache.md:100` (comment) vs `RpgStore.Delve.cs:114-124` (`PRIMARY KEY (delve_id, sector_id)`) — `rpg_delve_rooms` does carry `row_index`/`col_index` columns alongside `sector_id` on the same row (`:116`), so either key resolves to the same physical room, but this module needs a `sector_id` to join against `rpg_world_entities.at_sector_id` and `RouteFacts`. **Flagged for module 3 to reconcile at build time** (not this module's table to redefine): packing `sector_id` directly, rather than `(room_r, room_c)`, avoids an extra `rpg_delve_rooms` lookup on every claim and matches how every other room-keyed read in this codebase already works (`RouteFacts(party.Route, rooms)` at `RpgStore.Delve.cs:828`, keyed by `SectorId`) |
| `PartyIndex` (`loot-pack`'s own per-party key, e.g. `spec-loot-pack.md:17`) vs `partyEntityId` (`MoveParty`'s own World-graph key, `RpgStore.Delve.cs:321`) — the exact mapping between the two was not traced this session | confirmed both exist as separate identifiers; `DelvePartyState` carries `EntityId` (`RpgStore.Delve.cs:56-57`) alongside its own list index, which is almost certainly the bridge (`EntityId` IS `partyEntityId`) but this module's first implementation task should confirm it against a real caller rather than this spec asserting it unread |

### Real gap

| Gap | What would have to be built |
|---|---|
| No footprint resolution (`Footprint.Derive`/`PackFootprintTable`, `spec-loot-pack.md` §2) exists among the built Pack files (confirmed: only `PackGrid`/`PackArranger`/`PackMoves`/`PackAutopilot`/`PackDto`/`PackSettlement`, no `Footprint.cs`/`PackFootprintTable.cs`) | This module needs `(W, H)` for every `kind='instance'` cache row to build a `PackItem`. That resolution is `loot-pack`'s own responsibility (its §2), not this module's to build — named here as a hard dependency this module inherits, not invented |
| `rpg_corpse_cache_item` (as module 3's spec currently drafts it: `cache_id, seq, kind, instance_id, container_id, qty, origin_owner`) carries **no base-type/`refId` column** for a `kind='instance'` row | To build a `PackItem`, this module needs the rolled instance's base type (to look up its footprint) — `rpg_item` itself has no base-type column either (`RpgStore.Items.cs:85-98`, confirmed), so the resolution path runs through whatever the item program's own `effect_instance`/atom-composition tables hold. Not traced further this session — named as this module's first implementation task, alongside the footprint table above |
| No cache-claim mechanism, log table, or decision kind exists anywhere | Confirmed — `grep -rn "cache.claim\|rpg_corpse_cache_claim" src/` returns nothing (module 3's own tables don't exist yet either). §Design below is entirely new |
| No `ListClaimableCachesUnlocked`-shaped read exists | New — this module's own read path, below |

## Design

### 1. Eligibility read — `ListClaimableCachesUnlocked(delveId, sectorId)`

```sql
SELECT cache_id FROM rpg_corpse_cache
WHERE place_kind = 'delve_room' AND place_ref = $ref AND in_void = 0
  AND EXISTS (SELECT 1 FROM rpg_corpse_cache_item WHERE cache_id = rpg_corpse_cache.cache_id);
```

`$ref` is module 3's own `place_ref` packing for `(delveId, sectorId)` (the `sector_id`-based key, per
the drift note above — this module treats that reconciliation as already landed, since it cannot build
against an unreconciled key). The `place_kind = 'delve_room'` and `in_void = 0` clauses are written
explicitly even though, per §Locked anchors, no other `place_kind` could ever satisfy `in_void = 0` —
defense in depth, and it documents the invariant in the query itself rather than leaving it implicit.
A cache that exists but has had every row already claimed (module 3's header rows are never deleted,
per module 4's own "never delete even when empty" rule) is correctly excluded by the `EXISTS` clause —
no error, just nothing to show.

### 1a. Reachability for `world_sector`/`world_lane` (2026-09-13 amendment) — a live position check, not a raid-membership check

A delve room's reachability (§1) is "is the party's live position, `at_sector_id`, this room, inside an
`Active` raid." A world-map place has no raid to be inside — `WorldEntity` (`WorldState.cs:286-309`,
re-opened and confirmed this session) is the durable, standing unit; reachability is simply whether a
legion's own live position matches the cache's `place_ref`, read fresh off `rpg_world_entities` (the
world-map analogue of the delve's own `MoveParty`-written position, per §Wiring gap above):

```sql
-- world_sector: place_ref = SectorId
SELECT cache_id FROM rpg_corpse_cache
WHERE place_kind = 'world_sector' AND place_ref = $sectorId AND in_void = 0
  AND EXISTS (SELECT 1 FROM rpg_corpse_cache_item WHERE cache_id = rpg_corpse_cache.cache_id);
-- eligible iff the querying legion's own row has AtSectorId = $sectorId (WorldState.cs:292-293)

-- world_lane: place_ref = LaneId
SELECT cache_id FROM rpg_corpse_cache
WHERE place_kind = 'world_lane' AND place_ref = $laneId AND in_void = 0
  AND EXISTS (SELECT 1 FROM rpg_corpse_cache_item WHERE cache_id = rpg_corpse_cache.cache_id);
-- eligible iff the querying legion's own row has OnLaneId = $laneId (WorldState.cs:295), regardless of
-- OnLaneTowardSectorId/LaneProgressMilli -- a legion travelling either direction on the lane is
-- equally "on" it, per WorldLane's own bidirectional-travel comment (WorldState.cs:297-299)
```

No "raid active" gate exists or is needed here — a world-map legion's presence is not scoped to a
session the way a delve party's is; per the V5 amendment (`spec-cache-decay-void.md` §Locked anchors),
the cache simply exists at the place until it decays away or is claimed, with no closing event to wait
for.

**The claim destination differs from delve baggage — named honestly rather than forced into the same
shape.** §Design 2's `ClaimCorpseCacheUnlocked` places claimed items into the claiming **party's**
`loot-pack` `PackGrid` — a delve-scoped container this module already depends on. A world-map legion
has no `PackGrid`; its own carry capacity is `scoped-inventory-hierarchy`'s `legion-cargo` overlay
(`rpg_world_entity_cargo`, slot+weight), a different program's schema this module does not depend on
and should not reach into unilaterally. This module therefore specifies the **reachability rule above**
and confirms the **claim-exclusivity pattern is reusable as-is** (item-level `DELETE … WHERE cache_id =
? AND seq = ?` + row-count check under `RpgStore`'s single-writer lock, §Design 2/3, already generic
enough for any claimant shape) for a world-map claim — but the **write into a legion's cargo overlay is
not built by this module**. It is `scoped-inventory-hierarchy`/`cargo-fate`'s own natural extension:
that module already owns the legion-cargo schema and already extends `corpse-cache`'s place-kind
vocabulary in the opposite direction (cargo-out-to-cache on death). Named as a cross-program follow-on
the same way `corpse-cache`'s own anti-fraud ask is named rather than silently assumed — not a gap to
be discovered later.

### 2. The claim — one transaction, item-level exclusivity, no new lock table

```
ClaimCorpseCacheUnlocked(db, tx, delveId, partyIndex, cacheId, correlationId, now):
    if a row exists at (cache_id, delve_id, party_index, correlation_id) in the claim log:
        return the SAME recorded result — never reprocess (replay safety)

    rows = SELECT seq, kind, instance_id, container_id, qty FROM rpg_corpse_cache_item
           WHERE cache_id = $cacheId  -- whatever currently survives; module 4 already removed
                                       -- anything decay took, and a delve cache never decays
                                       -- before CloseDelve (§Locked anchors) — every row here is,
                                       -- in practice, the full original drop, untouched
    if rows is empty: log an empty claim result, return it (no-op, not an error)

    packItems = for each row: build a PackItem(
        Kind: row.Kind, RefId: <resolved base type — real gap, above>, InstanceId: row.InstanceId,
        Qty: row.Qty ?? 1, W/H: <resolved footprint — real gap, above>, GrantIndex: row.Seq,
        Origin: PackItemOrigin.Haul, RarityOrdinal/ItemLevel: <same resolution path>)
        -- GrantIndex = row.Seq: deterministic, matches PackArranger's own tie-break order, no new
        -- concept introduced

    (newGrid, overflow) = PackArranger.Arrange(currentPartyGrid, packItems)   -- the SAME call an
        -- ordinary loot reveal makes; this module never re-implements placement

    for each claimed row (both placed and overflowed — Arrange always resolves EVERY item to a
                          destination, grid or floor, never a third "failed" state):
        DELETE FROM rpg_corpse_cache_item WHERE cache_id = $cacheId AND seq = $seq
        -- the claim key: whichever transaction's DELETE affects this exact (cache_id, seq) row wins.
        -- Under RpgStore's own single-writer lock (every mutating call wraps `lock (_gate) { using
        -- var db = OpenUnlocked(); using var tx = db.BeginTransaction(); … }`, the pattern every
        -- Delve/World method in this file already follows), a second party's transaction attempting
        -- the same DELETE inside the same call simply finds 0 rows to remove for any seq the first
        -- transaction already took — no separate reservation table, no timing window, needed.
        if row.Kind == 'instance': INSERT INTO rpg_delve_pack_lock(delve_id, instance_id)
            VALUES ($delveId, $instanceId)   -- reused unmodified (§Built); no AcquireItem call — the
            -- item never stopped being owned (corpse-cache's own Locked anchor)

    write newGrid + overflow into parties_json[partyIndex].pack (the SAME column loot-pack's own
        pack.move/pack.drop writers touch) and overflow items into rpg_delve_rooms.floor_json for
        $sectorId (the SAME column an ordinary reveal's floor-overflow already uses)
    AppendDecision(delveId, {seq, kind: "cache.claim", partyIndex, tick: null,
        payload: {cacheId, claimedSeqs: [...], placedGrid: [...], placedFloor: [...]}})
    INSERT INTO rpg_corpse_cache_claim_log(cache_id, delve_id, party_index, correlation_id,
        claimed_utc, result_json) VALUES (...)   -- the replay-safety row checked at the top
    tx.Commit()
```

`cache.claim` is a **new, additive** decision kind on the same `{seq, kind, partyIndex, tick?, payload}`
log `AppendDecision` already accepts generically (§Built) — this is not an edit to `loot-pack`'s own
`pack.move`/`pack.drop` payload shapes and needs no ask against that program, the same way quest-reward
banking and altar-haul minting already added their own concerns to the same log without touching
`pack.*`'s vocabulary.

### 3. Why no separate reservation/lock table for cross-party racing

A field party (this module) and a different `PartyIndex` racing for the same room's cache are resolved
by the **row-delete-under-single-writer-lock** pattern alone (§Design 2) — the same "move never copies"
guarantee `corpse-cache` itself already leans on for its own death-move. No new mutual-exclusion
primitive is needed. What DOES need a new table is **replay safety for one party's own retried
request** (a client double-submit, a dropped response) — `rpg_corpse_cache_claim_log`, keyed
`(cache_id, delve_id, party_index, correlation_id)`, mirroring `CreateDelve`'s own
`ReadDelveByCorrelationUnlocked` precedent (§Built) and `cache-decay-void`'s own `(cache_id, tick)`
"row exists ⇒ already handled" shape (`spec-cache-decay-void.md` §Design 1).

### 4. Module 5 vs module 6 — never actually racing the same cache, stated precisely

The brief for this module asks for a claim-key precise enough that module 6 (`cache-retrieval-mission`)
"can compete for the same cache without double-claiming." Traced against the locked shape, the honest
answer is: **under V2/V5/V6 as closed, modules 5 and 6 never contend for the same cache row at the same
time.** This module only ever reads `in_void = 0` rows (§Locked anchors); module 6 targets only
`in_void = 1` rows (`spec-cache-decay-void.md` §Interface: *"`cache-retrieval-mission` (module 6) targets
only `in_void = 1` rows"*). `in_void` flips `0 → 1` exactly once, non-reversibly, and — for a delve
cache specifically — only at the same `CloseDelve` moment that ends the raid this module's whole
mechanism depends on being `Active`. There is no instant where both a live field party and a runnable
retrieval mission can reach the identical cache. **Re-confirmed 2026-09-13 after §1a's amendment**: a
`world_sector`/`world_lane` cache never flips `in_void` at all under the current ask (`spec-cache-decay-
void.md` §Locked anchors, V5 amendment), so it is permanently outside module 6's `in_void = 1` filter —
this widens which caches module 5 alone serves, it does not create a new contention surface with
module 6. **The claim-key pattern is still specified generically
enough for module 6 to reuse without re-deriving it**: item-level exclusivity is "delete the row you
want, under the store's single-writer lock, and check what you actually got" — no cache-wide lock is
ever taken; request-replay safety is "one log table keyed `(cache_id, <your own claimant key>,
correlation_id)`," where module 6's claimant key is whatever names one retrieval-mission attempt
(e.g. an expedition id) in place of this module's `(delve_id, party_index)`. Module 6 does not need to
share this module's own `rpg_corpse_cache_claim_log` table — table-per-module keeps the two programs
from coupling on a schema neither fully controls, matching this program's own "one ActorHub compose"
discipline applied to claim bookkeeping instead of combat state.

### 5. What a successful claim actually produces — armoury-available, never auto-re-equipped

The ideal doc's phrase "items transfer back to the armoury/assignee on success" (§Corpse-drop item 3)
does **not** mean auto-re-equip, reasoned as follows: (1) the item's original assignee is the specimen
that just died — for a hardcore-permadeath or worsened-wound death that specimen is `Retired`, and a
`Retired` specimen cannot be deployed or re-equipped, so "the assignee" cannot be the literal auto-target;
(2) `rpg_item_assignment`'s primary key is `(specimen_id, role)` (`RpgStore.Items.cs:152-159`) — writing
that row requires an explicit, player-chosen `specimen_id`, never inferred from where the item came from;
(3) a commander's own seat "lapses for re-designation by default" on a real death (ideal doc, owner-locked
list) — there is no standing holder to auto-return a commander's gear to either. **A claimed item becomes
an ordinary, player-owned, unassigned haul item** — exactly like any other mid-delve pickup — carried in
the party's pack (locked via `rpg_delve_pack_lock`, per §Design 2) until the raid extracts, at which point
`loot-pack`'s own unmodified `PackSettlement.Decide`/`ApplyPackSettlementUnlocked` (§Built) makes it
visible at home (`UnlockHaulInstance` — the lock row is deleted, `rpg_item` was never touched, so the
instance simply becomes listable). The player then equips it explicitly, on whichever live specimen they
choose, through the existing assignment flow — this module writes nothing to `rpg_item_assignment`, ever.
**Consequence, stated plainly and inherited, not special-cased:** a claimed item that is still in the pack
if the SAME raid later wipes is subject to whatever the *current* §7 rule is at that moment — destroyed,
under today's shipped `PackSettlement.Decide` wipe branch (`PackSettlement.cs:68-69`), until the V1/V4
overturn lands, after which it would move into a wipe-cache like any other haul. This module does not
special-case a claimed item's own history; it is indistinguishable from ordinary haul the instant it is
placed, by design (`PackItemOrigin.Haul`, the same tag an ordinary drop gets).

### 6. Autopilot — a reasoned default, not an owner lock

Whether an unsteered party (`loot-pack`'s own R9, `PackAutopilot`) should auto-claim a reachable cache is
not addressed anywhere in the owner-locked list. This spec's default, offered for revision rather than
asserted as closed: **yes, on the identical `value-per-cell` rule `PackAutopilot.ResolveFloor` already
applies to any floor item** (`PackAutopilot.cs:37-77`) — a claimed-but-unplaced cache item is, the moment
it is claimed, just another `PackItemOrigin.Haul` floor/grid candidate, so no new autopilot rule is
needed; the *claim* step itself (reading the cache and calling `PackArranger.Arrange`) is what an
autopilot party would still need triggered on its behalf by whatever session loop drives it, which this
spec does not own (FE/session-loop wiring is out of scope, per the ideal doc's own "FE presentation…
belongs to `/idea-ui`").

### 7. What this module does not do

Never rolls a survival/success chance (certain-on-reach is module 4's guarantee, consumed here, not
re-implemented). Never writes `decay_started_turn`/`in_void` (module 4's columns — read-only here).
Never creates a cache row (module 3's job). Never touches `rpg_item_assignment`/`rpg_player_item_assignment`
(§Design 5). Never touches a lawn/siege/world-kind cache (§Locked anchors — structurally excluded, not
filtered as a special case). Never builds a second grid, footprint table, or stack-cap vocabulary — every
placement decision is `PackArranger.Arrange`, unmodified.

## Tunables

This module introduces **no new tunable**. Reasoned, not assumed: the brief for this module asked
specifically whether a claim-window/race-resolution timing number is needed. It is not — §Design 2/3's
claim mechanism resolves at the SQL transaction boundary (delete-and-check-rowcount under the existing
single-writer lock), with no polling, no expiry, and no window to wait out; "certain-on-reach" (module 4)
already removes any probability this module would otherwise need a bounded-ratio tunable for. Baggage
capacity is `loot-pack`'s own `raid.modes.*.pack.{rows,cols}` (`spec-loot-pack.md` Tunables) — reused,
never re-tuned here, per the brief's own instruction.

## Numeric types

`seq` (claimed-item identifier) is `int`, inherited unchanged from `rpg_corpse_cache_item`
(`spec-corpse-cache.md` §Numeric types — "a structural bound, not a magnitude"). `qty` is `long`,
inherited unchanged for the same reason (`CLAUDE.md` "Numeric overflow" — any magnitude is `long`).
`party_index` is `int`, matching every other `PartyIndex`-shaped field in `loot-pack`'s own spec
(`spec-loot-pack.md` §Numeric types: *"`partyIndex` are `int`"*). No new magnitude of any kind is
introduced by this module — it moves existing rows and existing pack cells; it computes nothing that
scales with `Θ` or level. No `float`, no `double`, no `System.Random` anywhere in this module.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CacheFieldAccess"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Pack"   # PackArranger/PackSettlement regression — unmodified by this module, run to prove it
.\scripts\guard-dal.ps1        # every new SQL string lives in FusionRpg.Data
.\scripts\guard-actor-hub.ps1  # unaffected — no combat-derived read added
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.CacheFieldAccess.cs   NEW — schema (rpg_corpse_cache_claim_log),
                                                          ListClaimableCachesUnlocked,
                                                          ClaimCorpseCacheUnlocked (both callable on an
                                                          open tx; the public entry opens its own per
                                                          this file's established lock(_gate) pattern)
tests/FusionRpg.Data.Tests/CacheFieldAccess/             NEW
UNTOUCHED: Core/Delve/Pack/* (PackGrid, PackArranger, PackMoves, PackAutopilot, PackDto, PackSettlement
           — this module CALLS PackArranger.Arrange, never edits any of these files); RpgStore.Delve.cs's
           own ApplyPackSettlementUnlocked/CloseDelve extraction-and-wipe settlement (unmodified — a
           claimed item is ordinary haul from that code's point of view); rpg_corpse_cache/
           rpg_corpse_cache_item schema (module 3's own table — this module only SELECTs and DELETEs
           rows from rpg_corpse_cache_item, never redefines it); rpg_item_assignment/
           rpg_player_item_assignment (never written by this module, §Design 5)
```

## Code style

```csharp
// The claim key at the item level: a DELETE's own row count IS the mutual-exclusion result — no
// reservation table, no timing window. Two parties racing the same seq: the loser's DELETE affects
// zero rows and that seq is simply absent from ITS claimed set, not an error.
int deleted;
using (var cmd = Prepared(db, tx,
    "DELETE FROM rpg_corpse_cache_item WHERE cache_id = $c AND seq = $s;", "$c", "$s"))
    deleted = ExecuteWith(cmd, cacheId, seq);
if (deleted == 0) continue; // another transaction already took this row — not this module's error
```

## Testing strategy

- **Baggage, same party, same room, mid-run:** a death cache created at a still-`Active` raid's room is
  fully claimable by the party currently at that `sector_id`; every surviving row disappears from
  `rpg_corpse_cache_item` and an equal count of pack cells (or floor entries, on overflow) appears.
- **Revisit, different `PartyIndex`, same raid:** party B reaches a room where party A's earlier teammate
  died; party B claims whatever party A did not already take — proven by having party A claim a subset
  first, then asserting party B's claim result excludes those `seq`s.
- **No double-claim across concurrent requests:** two transactions attempting `ClaimCorpseCacheUnlocked`
  for overlapping `seq` sets on the same cache — the sum of both results' claimed `seq`s equals the
  original row set exactly once each, never twice, never short.
- **Replay safety:** the identical `(cacheId, delveId, partyIndex, correlationId)` call twice returns the
  byte-identical result both times, and the second call performs no `DELETE`/no pack write (asserted via
  a query, not just an observed no-op).
- **Overflow never loses an item:** a cache whose combined footprint exceeds the party's remaining grid
  capacity — every claimed row ends up either in a pack cell or in `floor_json`, none silently dropped;
  cross-checked against `PackArranger.Arrange`'s own contract (every input item appears in exactly one of
  `Grid`/`Floor`).
- **Structural exclusion, lawn/siege only (amended 2026-09-13):** a lawn/siege cache row (constructed
  directly in the test, bypassing the normal `in_void` timing) is never returned by either the
  delve-room query (§1) or the world-map query (§1a) regardless of its `in_void` value — the two
  eligibility queries are each keyed to their own `place_kind`, never a shared "not delve_room"
  catch-all. `world_sector`/`world_lane` rows, by contrast, ARE returned by §1a whenever `in_void = 0`
  (which, per module 4's amendment, is always, for these two kinds) and the querying legion's live
  position matches — proving the amendment's own eligibility, not just documenting it in prose.
- **World-map reachability, both kinds (2026-09-13):** a `world_sector` cache is claimable only by a
  legion whose `WorldEntity.AtSectorId` equals the cache's `place_ref`; a `world_lane` cache only by a
  legion whose `OnLaneId` equals it — a legion at a different sector/lane never sees the cache in this
  module's own eligibility read (the destination-capacity check against a legion's own cargo overlay is
  a different module's concern, §1a).
- **Closed raid refuses:** a claim attempt against a `delve_id` whose `state` is not `Active` is refused
  before any row is touched — proving §Locked anchors' "structurally cannot apply once a raid closes"
  claim, not just documenting it.
- **`the_module_never_rolls`:** a source scan over `RpgStore.CacheFieldAccess.cs` for `SeededRng` /
  `System.Random` — zero hits, proving certain-on-reach is honored, not just asserted in prose.
- **No auto-re-equip:** after a successful claim and extraction, the claimed instance appears in a
  listable, unassigned state and `rpg_item_assignment` gains no new row for it, for any specimen.

## Boundaries

- **Always:** claim inside one transaction; delete-then-place, never place-then-delete (so a mid-transaction
  failure cannot duplicate an item); reuse `PackArranger.Arrange` unmodified; log every applied claim
  through the generic `AppendDecision`, never a bespoke second log mechanism; check the replay log before
  doing any work.
- **Ask first:** extending `PackMoves.ApplyMove`'s `From` vocabulary beyond `grid|floor` (this module
  deliberately does not need this ask, having routed around it via `PackArranger.Arrange` directly — but
  a future session should not add a `from: cache` value to that closed set without one); any change to
  `PackSettlement.Decide`'s wipe branch (that is the `loot-pack` §7 ask, owned by `corpse-cache`'s own
  wipe path, not this module); the legion-cargo claim write-path (2026-09-13 amendment, §1a) —
  `scoped-inventory-hierarchy`/`cargo-fate`'s own ask to accept, since it owns the `rpg_world_entity_cargo`
  schema a claimed `world_sector`/`world_lane` item would need to land in; this module specifies only the
  reachability rule and the reusable claim-exclusivity pattern, never the write into that overlay.
- **Never:** a second pack grid, footprint table, or stack cap; a probability roll of any kind; a write to
  `rpg_item_assignment`/`rpg_player_item_assignment`; a claim against a non-`Active` delve (for a
  `delve_room` cache) or against a lawn/siege cache (structurally excluded, unchanged); a shared
  claim-log table between this module and `cache-retrieval-mission`; a write into `rpg_world_entity_cargo`
  (that overlay belongs to `scoped-inventory-hierarchy`, not this module, per §1a).

## Success criteria

1. A death-cache claim by the party in its room moves every surviving row out of
   `rpg_corpse_cache_item` and into that party's pack/floor, provably. 2. A second `PartyIndex` in the
   same raid claiming afterward gets exactly the remainder, never a duplicate. 3. Replaying an identical
   claim request is a no-op the second time. 4. No new `rpg_item_assignment` row is ever written by this
   module. 5. `guard-dal.ps1` green; zero `SeededRng`/`System.Random` hits in this module's own file.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| Claim-key pattern: item-level exclusivity via `DELETE … WHERE cache_id = ? AND seq = ?` + row-count check under `RpgStore`'s existing single-writer `lock(_gate)` (no reservation table); request-replay safety via a `(cache_id, <claimant key>, correlation_id)` log table, this module's own instance being `rpg_corpse_cache_claim_log` keyed `(cache_id, delve_id, party_index, correlation_id)` | `cache-retrieval-mission` (module 6) — mirrors the identical two-part pattern for its own claimant key (an expedition id, not a party key); the two modules never contend for the same cache row simultaneously under the locked V2/V5/V6 shape (module 5 reads only `in_void = 0`, module 6 only `in_void = 1`), so no shared lock or shared table is needed, only a consistent pattern |
| `ListClaimableCachesUnlocked(delveId, sectorId)` | a future delve-stage FE read surface (wave 5, out of this module's own scope — matches `loot-pack`'s own `PackDto` precedent of leaving the client layer to a later wave) |
| The finding that `rpg_world_entities.at_sector_id` (written by `MoveParty`) is the party's live-position pointer | feeds back to `corpse-cache` (module 3), whose own spec left this exact pointer untraced as an open item — this module traced it; module 3's own implementation should reconfirm and cite it directly rather than re-deriving it |
| The `world_sector`/`world_lane` reachability rule (§1a, 2026-09-13) — `WorldEntity.AtSectorId`/`OnLaneId` matched against `place_ref`, plus the reusable claim-exclusivity pattern above | `scoped-inventory-hierarchy`/`cargo-fate` — the natural owner of the actual claim write (into a legion's `rpg_world_entity_cargo` overlay), which this module deliberately does not build (§1a, §Boundaries) |

## Design-gate checklist

```
[x] Subsystems: delve/party state (Data), item ownership (Data, read-only here), the loot-pack pack
    grid (Core, called not edited) — no Status/ActorHub/World-Step subsystem touched.
[x] Read this session, in full: deployment-hierarchy-ideal.md (both halves); deployment-hierarchy-map.md;
    spec-corpse-cache.md; spec-cache-decay-void.md; spec-loot-pack.md.
[x] Code cited by file:line, opened this session: RpgStore.Delve.cs (:77-141, :164-204, :321, :347-350,
    :491, :762-856, :1209-1251, :1619-1641), RpgStore.World.cs (:92-102), RpgStore.Items.cs (:85-98,
    :152-171), Core/Delve/Pack/PackGrid.cs, PackArranger.cs, PackMoves.cs, PackAutopilot.cs, PackDto.cs,
    PackSettlement.cs (all read in full), Core/Delve/Loot/DropResult.cs (:7-20).
[x] Drift reported: loot-pack's own spec header/checklist says "unbuilt"/"no code exists" — its Core pack
    layer (PackGrid/PackArranger/PackMoves/PackAutopilot/PackDto/PackSettlement) is shipped and, via
    ApplyPackSettlementUnlocked, already wired live into CloseDelve, running today's pre-overturn §7 rule.
    corpse-cache's own place_ref packing note (room_r/room_c) does not match rpg_delve_rooms's real PK
    (delve_id, sector_id) — flagged for module 3 to reconcile. spec-corpse-cache.md's own open item (the
    "current room" pointer) is resolved here: rpg_world_entities.at_sector_id via MoveParty.
[ ] The exact PartyIndex ↔ partyEntityId mapping (DelvePartyState.EntityId is the likely bridge) was
    reasoned, not confirmed against a real production caller — named as implementation's first task.
[ ] The instance_id → base-type-id resolution path (needed to look up a footprint) was not traced this
    session — named as a real gap shared with, but not owned by, this module (§What already exists).
[x] No §2 invariant contradicted: SQL only in FusionRpg.Data; no cap on a magnitude (this module
    introduces none); no f(Θ); no second ActorHub composer; no second pack-grid/container; no probability
    roll of any kind (certain-on-reach honored, not re-implemented).
[x] The one external gate this module actually has (module 3's wipe-cache rows not existing until the
    loot-pack §7 ask lands) is stated precisely as bounding WHAT this module can claim, never WHETHER
    this module's own death-cache mechanism works — corrected from the map's blanket "external loot-pack
    §7 sign-off" framing, with the reasoning shown, not asserted.
[x] Amendment 2026-09-13 (later session): this module's own "delve-only in practice" claim (§Locked
    anchors) is corrected — `world_sector`/`world_lane` caches never flip `in_void` under
    `cache-decay-void`'s own V5 amendment, so this module now also serves those two kinds (§1a). The
    claim write-path into a legion's `rpg_world_entity_cargo` overlay is named as
    `scoped-inventory-hierarchy/cargo-fate`'s own follow-on, not built here, since this module has no
    dependency on that program's schema.
```
