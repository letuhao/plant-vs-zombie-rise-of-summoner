# Spec: `cache-decay-void`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this session.
Module id `cache-decay-void`, row 4 of the [deployment-hierarchy map](../deployment-hierarchy-map.md)
(wave 1, depends on `corpse-cache` (module 3, spec written this session, **not yet coded** — zero
`src/` hits for `rpg_corpse_cache`, confirmed this session by `grep -rn "rpg_corpse_cache" src/`) —
[spec-corpse-cache.md](spec-corpse-cache.md)). Ideal:
[deployment-hierarchy-ideal.md](../deployment-hierarchy-ideal.md) §"Corpse-drop, decay, and
retrieval" items 2/7, §"Baggage, revisit loot, and the void" (V2/V5/V6), §"Resolved 2026-09-13 (third
clearing round)" items 2/4/5, and decisions.md row **Deployment hierarchy SSOT (2026-09-13)**, P2.

## Objective

Two things move together, never apart: (1) the two columns `corpse-cache` already declared and left
NULL/0 — `rpg_corpse_cache.decay_started_utc`/`in_void` — get written, exactly once, at the precise
moment V5/V6 name; (2) once running, every surviving row in a cache independently rolls a per-tick
survival check on the world-stage turn, seeded and idempotent, so a cache with no one left to defend
it is never immortal and never double-destroyed on replay.

Success looks like: a lawn cache's clock starts the instant it is created (V5 — no durable row to wait
on); a delve cache's clock stays NULL through every room the party still might walk back to, and starts
the instant `CloseDelve` closes the run (V6); once started, a fixed-seed 32-cache sweep's *median* turn
at which a cache empties falls inside a tunable regression band centered on ≈112 turns, computed from
the shipped `survivalPerTickMilli` — never hardcoded as "112"; replaying the same world turn's commit
twice never re-rolls and never re-destroys a row already gone; a cache with no `decay_started_turn` set
(a live, still-open delve room) decays across zero world turns, no matter how many pass.

## Locked anchors

- **V2 (closed):** the void is the *one* home for an unreachable cache. This module writes `in_void`;
  it never invents a second fate, and `source_kind` (`corpse-cache`'s own audit column) is never
  branched on here (module 3's own Boundaries line, honored, not re-litigated).
- **V5 (amended 2026-09-13, later session — `world_sector`/`world_lane` carved out, lawn/siege
  unchanged):** lawn and siege caches still enter the void **immediately** — no durable place-row
  exists to hold a grace window. Delve caches sit at their room until the delve session itself closes.
  **`world_sector`/`world_lane` (newly live, `spec-corpse-cache.md` §1a) do not follow the lawn/siege
  rule**, because the reasoning behind it does not hold for them: a sector or a `WorldLane`
  (`WorldState.cs:246-263`, `:286-309` — re-opened and confirmed this session) is a durable, persistent
  world-map place that never disappears the way a finished lawn match or a closed delve room does, so
  a legion really can walk back to one at any later turn. These two kinds start their decay clock
  immediately (there is no delve-style close event on the world map to wait for — the world turn is
  already advancing) but **never flip `in_void`** under this amendment — see §Design 2. Consequence,
  named rather than hidden: `cache-retrieval-mission` (module 6, `in_void = 1` only) structurally never
  targets a `world_sector`/`world_lane` cache today; `cache-field-access` (module 5) is the only
  recovery path for these two kinds until a future ask adds a void-transition trigger for them.
- **V6 (closed):** the clock does not start at death. Delve: starts at `CloseDelve` (extract or wipe).
  Lawn/siege/world: starts immediately, same moment as void entry. Once running it ticks on the
  **world-stage turn** (`TurnCalendar.cs` — verified below), frozen while nobody advances it.
  Starting-shape full-decay target: **≈112 turns**, one complete 4-season calendar cycle.
- **Certain-on-reach (second clearing round, item 3, closed):** decay is the *only* pre-arrival gamble.
  Reaching a live cache (baggage, revisit, or a successful retrieval mission) always recovers whatever
  decay has not yet taken — this module never rolls a second time at read/retrieval; modules 5/6 read
  surviving rows as-is.
- **No wall clock, ever, for game state** (R6 precedent, cited verbatim by `spec-delve-attrition.md`):
  turn count, never `DateTime`/`DateTimeOffset`/`Environment.TickCount`/`System.Random`. The existing
  discipline this repo already enforces for delve settlement — **"No clock — guard test: no
  `DateTime.UtcNow`, `DateTimeOffset.UtcNow`, `.Now`, `Environment.TickCount`, `ElapsedDays` or
  `System.Random` under `Core/Delve/Attrition/` or in the `CloseDelve` settlement… `rpg_unique_actor_recovery`
  has no `*_utc` column"** (`party-dungeon/spec-delve-attrition.md:387-389`, read this session) — is the
  exact precedent this module extends to its own new file, and the exact reason `corpse-cache`'s column
  name (`decay_started_utc`, a `TEXT` timestamp) cannot be the field the tick arithmetic reads (see
  §Design 1 — Real gap).

## What already exists

### Built

| Finding | Evidence |
|---|---|
| A turn is one day, seven days a week, four weeks a month; season count/length share the same tuning file | `src/FusionRpg.Core/World/Turn/TurnCalendar.cs:13-24` (doc comment + `DaysPerWeek`/`WeeksPerMonth` from `WorldTuningHub.Tuning.Calendar`), `:32-33` (`SeasonCount`/`MonthsPerSeason` from `Tuning.Seasons`) |
| The shipped calendar's actual numbers — verified against the live file, not assumed | `data/tuning/world.v5.json:83-84` — `"daysPerWeek": 7, "weeksPerMonth": 4`; `:105-106` — `"count": 4, "monthsPerSeason": 1`. `DaysPerMonth = 7×4 = 28`; `SeasonOf(turn) = turn / (28×1) % 4` (`TurnCalendar.cs:42`) ⇒ a full season cycle is `28×4 = 112` turns — **the ideal doc's "≈112 turns" is this exact arithmetic, confirmed this session, not a round number picked independently** |
| `SeededRng.DeriveStream(ulong runSeed, string streamName)` — the one mixer, FNV-1a over the stream name XORed into the seed | `src/FusionRpg.Core/Battle/SeededRng.cs:26-27` |
| The named seeded sub-stream precedent the ideal doc cites for a per-tick roll (`tick:{t}`) — a real, shipped production call, not a hypothetical | `src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs:106` — `var rng = SeededRng.DeriveStream(seed, "tick:" + t);` |
| `NextPerMille()` — the integer ‰ roll every other chance in this codebase already compares against a ‰ threshold | `src/FusionRpg.Core/Battle/SeededRng.cs:62` |
| `CommitWorldTurn` — the **one** transaction that steps the engine, diffs the world graph, appends the turn log, and advances the turn counter, all under one `tx` | `src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs:484-577`, specifically the engine step (`:531`), graph diff (`:537`), log insert (`:539-557`), counter advance (`:559-571`), single `tx.Commit()` (`:573`) |
| Turn advance is **exactly +1** per commit — there is no "skip ahead N turns" path anywhere in this engine | `RpgStore.WorldTurns.cs:501` (`var turn = world.CurrentTurn;`) vs `:531`'s `TurnEngine.Step` producing `CurrentTurn = turn + 1` (`TurnEngine.cs:138`) — confirmed by reading both this session |
| A turn only advances when **every** commander has committed (the barrier) — turns are player-paced, not wall-clock | `RpgStore.WorldTurns.cs:520-526` — `new WaitForAllCommitted().ShouldFire(commanders, committed)`, returning `"waiting"` and **no engine step** otherwise |
| A per-(id, turn) idempotency check-before-insert precedent, in the **same file** this module hooks into | `RpgStore.WorldTurns.cs:139-153` (`CommandExistsUnlocked` — `SELECT 1 … WHERE world_id = $w AND turn = $t AND … ; if exists, skip`) — the exact shape this module reuses for `(cache_id, tick)` (§Design 3) |
| `CloseDelve` — the one delve-settlement transaction; `SettleExtractionUnlocked` (called from inside it) is where `RetireUniqueActorUnlocked` already fires for **both** extraction and wipe, and where every *other* recovering specimen this player owns is also aged, regardless of which delve is closing | `src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs:762-790` (`CloseDelve`, one `tx`, `SettleExtractionUnlocked` at `:777`); `:801-856` (`SettleExtractionUnlocked` body — `RetireUniqueActorUnlocked` at `:849` fires for `wiped` and `extracted` alike via `ExtractionSettlement.Decide`'s own `downedOnce`/`extracted` inputs, `:837-844`); the player-wide aging precedent at `:811` (`DecrementAllRecoveringForPlayerUnlocked(db, delve.PlayerId, now)`) |
| `rpg_worlds` schema — `player_id`, `current_turn`, `seed`, `kind` (a delve is its own `rpg_worlds` row, `Kind != "map"`, separate from the player's overworld) | `src/FusionRpg.Data/Sqlite/RpgStore.World.cs:20-35`; delve's own `world_id` column, distinct from a map world, `src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs:98-99` (`player_id INTEGER NOT NULL`, `world_id TEXT NOT NULL UNIQUE`) |
| `TurnEngine.Step` never touches SQL — `CommitWorldTurn`/`TurnEngine` refuse to run on a non-`"map"` world | `RpgStore.WorldTurns.cs:499` (`if (header.Kind != "map") return …`), `:100-103` (same refusal in `SubmitWorldCommands`) — confirmed a delve's own turn advance (`RpgStore.Delve.MoveParty`, not read this session) is a **different** mechanism from the map's `TurnEngine.Step`, so the decay clock this module ticks is unconditionally the **map** world's turn, never a delve's internal room-clock |
| `disposition ∈ {owned, salvaged, transferred, destroyed}` (closed four) and the reuse precedent for destroying a rolled instance | `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs:71-73` (`rpg_item.Disposition` doc comment, four values, `"deleting the underlying instance is always a disposition"`); `DeleteInstance(string instanceId)` at `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:128-134` — cascades `effect_instance` → `effect_instance_atom`/`effect_binding` (FK `ON DELETE CASCADE`, `:93`) and, via `rpg_item`'s own FK to `effect_instance` (`RpgStore.Items.cs:97`), `rpg_item` itself; already the exact mechanism `spec-loot-pack.md:87` uses for a dropped carry-in item (`disposition = 'destroyed'` + `DeleteInstance`) |
| `DeleteInstance` opens its **own** connection/transaction, so it cannot be called inside another module's already-open `tx` — the same constraint `corpse-cache` and `loot-pack` both already worked around by inlining the statement | `RpgStore.AtomInstances.cs:132-133` (`using var db = OpenUnlocked(); using var tx = db.BeginTransaction();`), matching `spec-corpse-cache.md`'s own citation of the identical constraint on `RemoveAssignment` |
| Tunables classification and file convention this module's numbers follow | `docs/architecture/tunables-ssot.md:41-51` (four classes; "would a balance pass ever want to change this number") |

### Wiring gap

| Gap | The inert line |
|---|---|
| `decay_started_utc`/`in_void` exist (once `corpse-cache` ships) but nothing writes them — `corpse-cache`'s own spec says so explicitly | `spec-corpse-cache.md:172` — `"NULL until the clock starts (module 4…)"`, `:281` — `"reading or writing decay_started_utc/in_void (module 4's columns…)"` — this module's entire §Design 1/2 is closing that gap |
| `TurnEngine`'s `Events` phase is, today, a pure report emitter — it computes `TurnCalendar.Roll` and returns `world` **completely unchanged** | `TurnEngine.cs:315-339`, specifically `:338` — `return world;` (no field on `WorldState` is touched by this phase, confirmed by reading the whole method) |
| `rpg_worlds`' own dormant auto-advance fields are declared and never read anywhere — confirmed this session, not assumed | `grep -rn "turn_period_seconds\|catch_up_cap" src/` returns only the two `CREATE TABLE` column declarations (`RpgStore.World.cs:26-27`) — **zero** readers. This is why §Design 4's "frozen while idle" claim holds *today*: nothing auto-commits a turn on a wall clock. Flagged as an ask-first boundary should that mode ever be wired (§Boundaries) |

### Real gap

| Gap | What would have to be built |
|---|---|
| **`decay_started_utc` is architecturally the wrong field for a turn-counted clock** — a `TEXT` wall-clock stamp cannot answer "how many world turns has this cache been decaying," because world turns are player-paced and have no fixed mapping to wall time (the same reason `rpg_unique_actor_recovery` carries **no** `*_utc` column at all, `spec-delve-attrition.md:389`). This module needs a **turn-number** column `corpse-cache` never declared. Resolved in §Design 1: this module adds `decay_started_turn INTEGER` via an additive `EnsureColumn` migration on `rpg_corpse_cache` (matching this codebase's own precedent for a later module needing one more column on an earlier module's table, e.g. `RpgStore.WorldTurns.cs:65`'s `phases_json` addition) — `decay_started_utc` stays, written at the same moment, purely as a human-readable audit stamp (matching `created_utc`'s role elsewhere), **never read by any decay arithmetic**. |
| **`rpg_corpse_cache` has no owner reference** — no `player_id`/`world_id` column at all (confirmed against the schema read in full this session, `spec-corpse-cache.md:97-106`). Without one, nothing can answer "which player's map-world commit should tick this cache." Resolved in §Design 1: this module adds `owner_player_id INTEGER NOT NULL` the same way. Because `corpse-cache` is spec-only (not yet coded), this is a **cheap, pre-code correction** — module 3's own `ResolveOrCreateCacheUnlocked` (not yet written) supplies it at insert time from information it already has locally (the specimen's/delve's own `player_id`), not a later migration backfill. |
| No per-`(cache, tick)` idempotency/roll-history table anywhere in `src/` | confirmed this session — `grep -rn "rpg_corpse_cache_decay" src/` returns nothing. §Design 3 is the new table. |
| No existing precedent of a Data-side write hooking into a world turn's commit transaction for state that is **not** part of `WorldState`'s hash | Confirmed by reading the whole of `RpgStore.WorldTurns.cs` this session: the only writes inside `CommitWorldTurn`'s `tx` are the world graph diff, the turn log, and the turn counter — nothing else piggybacks on it today. **This module is the first.** Stated honestly, not as an existing pattern being followed — see §Design 4 for why this does not require a `RulesetVersion` bump or a `WorldState` field (the cache lives in SQL, outside the hashed/replayed graph, exactly like `rpg_delves`/`rpg_unique_actors` already do). |
| Whether `injury-tiers` (module 2, spec-only) ever fires its own "worsened wound → `Retired`" trigger for a **delve-deployed** specimen **outside** `CloseDelve` (e.g. a per-room check rather than the delve-counted settlement clock every other delve-scoped recovery mechanism uses) | Not resolved here — module 2 has no shipped code to check. §Design 2 states the reasoning this module's design rests on (every delve-attributable `Retired` observed in shipped code routes through `CloseDelve`) and names it as **implementation's first task to reconfirm** once module 2 ships, not asserted as already guaranteed by a cited line. |

## Design

### 1. Schema — two additive columns on `corpse-cache`'s table, one new table

```sql
-- Additive migration on corpse-cache's own rpg_corpse_cache header (EnsureColumn, matching
-- RpgStore.WorldTurns.cs:65's precedent for a later module needing one more column on an earlier
-- module's table). decay_started_utc/in_void stay corpse-cache's declared columns; this module only
-- ever writes them, never redeclares them.
-- decay_started_turn: the WORLD-STAGE TURN NUMBER the clock started at. NULL == not yet running.
--   Never a *_utc/DateTime field for the same reason rpg_unique_actor_recovery carries none
--   (spec-delve-attrition.md:389) -- a turn-counted clock cannot be reconstructed from wall time.
EnsureColumn(db, "rpg_corpse_cache", "decay_started_turn", "INTEGER");
-- owner_player_id: which player's MAP-kind rpg_worlds row owns this cache's decay clock. corpse-cache's
-- own schema never named an owner (real gap, above); this is the cheapest point to add it, pre-code.
EnsureColumn(db, "rpg_corpse_cache", "owner_player_id", "INTEGER");

-- The per-(cache, tick) idempotency guard AND the audit trail, in one row per cache per processed
-- turn -- the same "row exists => already handled" shape CommandExistsUnlocked already uses
-- (RpgStore.WorldTurns.cs:139-153) for (world, turn, commander, command).
CREATE TABLE IF NOT EXISTS rpg_corpse_cache_decay_log (
  cache_id      TEXT NOT NULL,
  tick          INTEGER NOT NULL,   -- the world turn this row processed
  rolled_utc    TEXT NOT NULL,      -- audit only, never read by arithmetic
  outcomes_json TEXT NOT NULL,      -- [{seq, kind, outcome: 'survived'|'destroyed'}, ...] -- audit only
  PRIMARY KEY (cache_id, tick)
);
```

A tick is "already processed" iff a row exists at `(cache_id, tick)` — checked before rolling, inserted
after, exactly the `CommandExistsUnlocked`/`InsertCommandUnlocked` shape. `rpg_corpse_cache` rows are
**never deleted** by this module even once empty (no items left) — an empty header is free to skip on
the next tick's query (`WHERE EXISTS (SELECT 1 FROM rpg_corpse_cache_item WHERE cache_id = …)`) and
stays as a named, addressable row rather than vanishing into limbo, matching every other "dead end gets
a fate, never disappears" rule in this program (M2–M4, ideal doc).

### 2. When the clock starts — one hook, not two, because of what already had to be true

V6 reads as two rules ("`CloseDelve` for delve, immediate for lawn/siege/world"), but this session's
reading of `RpgStore.Delve.cs` (§Built, above) shows they collapse into **one** mechanism: every
delve-attributable `Retired` this session found in shipped code (`RetireUniqueActorUnlocked`'s call at
`:849`, for both the `wiped` and `extracted` branches of `ExtractionSettlement.Decide`) fires from
inside `SettleExtractionUnlocked`, which only ever runs from inside `CloseDelve`'s one transaction — and
`corpse-cache`'s own §Design 2 step 3 creates the cache row **at that same call**. A delve cache is
therefore never created before `CloseDelve` runs, by construction — there is no code path that creates
one earlier and later needs a *second* event to "start" its clock. The `wiped`-party pack-move
(`corpse-cache` §Design 3) is, if anything, an even tighter case: it is `CloseDelve`'s own wipe branch,
literally the same transaction.

So this module needs exactly **one** function, called once, at cache-**creation** time, regardless of
`place_kind`:

```
TryStartDecayClockUnlocked(db, tx, cacheId, placeKind, ownerPlayerId)
    if not a brand-new row (corpse-cache's own "resolve OR create" reused an existing cache): return
    turn = SELECT current_turn FROM rpg_worlds WHERE player_id = ownerPlayerId AND kind = 'map'
    -- Amendment 2026-09-13 (later session, cargo-fate ask): world_sector/world_lane are DURABLE,
    -- revisitable world-map places (a sector/lane never "closes" the way a lawn match or a delve room
    -- does — WorldState.cs confirms both persist for the life of the world), so unlike lawn/siege
    -- (V5's own "no durable place-row" reasoning) they never flip in_void at creation. This IS a
    -- place_kind branch, contradicting this section's own original "no place_kind branch is needed"
    -- claim below — corrected here, not silently kept, because the claim held only for the three
    -- kinds known at the time it was written.
    staysReachable = placeKind IN ('world_sector', 'world_lane')
    UPDATE rpg_corpse_cache SET decay_started_turn = turn, decay_started_utc = now,
        in_void = (staysReachable ? 0 : 1),
        owner_player_id = ownerPlayerId WHERE cache_id = cacheId
```

Called from `corpse-cache`'s own `ResolveOrCreateCacheUnlocked`, one new line, immediately after a
genuine `INSERT` (never on the reuse branch — see the anti-fraud note below). For `place_kind = 'lawn'
| 'siege'` this fires the moment `corpse-cache`'s own death/wipe-move code first resolves the row,
which V5 already requires to be immediate since neither place has a durable holding row to wait on. For
`place_kind = 'delve_room'` this *also* fires at row-creation time — which, by the paragraph above, is
always inside `CloseDelve`. For `place_kind = 'world_sector' | 'world_lane'` (2026-09-13 amendment)
this fires at row-creation time too — `cargo-fate`'s own hook creates the row synchronously inside the
world turn-commit transaction, the moment a legion is found destroyed — but `in_void` stays `0`,
because the sector/lane the cache is pinned to does not stop being a real, reachable place the instant
the cache is created, unlike a lawn match or a completed delve.

**The V6 split for `lawn`/`siege`/`delve_room` is still enforced by *when module 3's own code is ever
invoked* for each kind, not by a conditional in this module** — that part of the original claim holds
unchanged. The one place_kind branch this amendment adds is scoped to the `in_void` write alone, for
exactly the two newly-live kinds; `decay_started_turn`'s own timing rule (immediate for every kind
except `delve_room`, which waits on `CloseDelve`) is unchanged. This remains the one place this spec
asks the implementer to reconfirm against module 2's actual code once it ships (§Real gap, above)
rather than treating it as already settled by a citation.

**No re-stamp on reuse.** A second death's gear joining an already-decaying cache (a second specimen
dying into the same lawn match, or a second wipe touching a room whose cache already exists) does not
reset `decay_started_turn` — the newly-moved items inherit whatever's left of the cache's existing
window. This is the anti-fraud rule's own symmetry (ideal doc §Corpse-drop item 6): gear does not get a
fresher clock by dying into an already-aging cache any more than stripping a doomed specimen dodges the
deploy-time snapshot.

### 3. The per-turn tick — one line in `CommitWorldTurn`, before its own `tx.Commit()`

```
TickCorpseCacheDecayForPlayerUnlocked(db, tx, ownerPlayerId, newTurn, worldSeed)
    for each cache where owner_player_id = ownerPlayerId
                      and decay_started_turn is not null
                      and exists an rpg_corpse_cache_item row for it
                      and no rpg_corpse_cache_decay_log row at (cache_id, newTurn):
        outcomes = []
        for each remaining item row (kind, seq, instance_id | (container_id, qty), rarityOrdinal):
            stream = SeededRng.DeriveStream(worldSeed, $"corpse-decay:{cacheId}:{newTurn}:{seq}")
            effectiveMilli = min(cap, base + rarityOrdinal * rarityStepMilli)   // §Tunables
            survives = stream.NextPerMille() < effectiveMilli
            if not survives:
                if kind == 'instance': inline the DeleteInstance statements (RpgStore.AtomInstances.cs's
                    own cascade, inlined -- it opens its own tx and cannot be called inside this one,
                    the exact constraint corpse-cache and loot-pack both already worked around the
                    same way) against instance_id, then DELETE the rpg_corpse_cache_item row
                if kind == 'stack': DELETE the whole rpg_corpse_cache_item row (the stack, one unit --
                    EVE's per-stack coin flip, never a per-unit-within-a-stack roll; see §Design 3a)
            outcomes.add({seq, kind, outcome: survives ? 'survived' : 'destroyed'})
        INSERT rpg_corpse_cache_decay_log (cache_id, newTurn, now, outcomes_json = outcomes)
```

Called from `RpgStore.WorldTurns.cs:559-571`'s neighborhood — after the turn-counter `UPDATE`, before
`tx.Commit()` (`:573`) — one new line, reusing `header.Seed` (already in scope as `worldSeed`, the same
value `TurnEngine.Step` itself received) and `commanderId`'s resolved `player_id` is not what's wanted
here: **every** player who owns a cache tied to this world's turn gets ticked, not just the commander who
happened to release the barrier, since decay is a property of the *world's* turn advancing, not of who
committed it. `newTurn = result.World.CurrentTurn` (the value just written to `rpg_worlds.current_turn`).

Because `CommitWorldTurn` never advances more than one turn per call (§Built — confirmed this session,
no batch-skip exists anywhere in this engine), this function only ever needs to process the **single**
newly-committed turn for each active cache — never a catch-up loop over a range. A cache whose owner
simply never ends a turn accrues no decay at all, for as long as that is true (§Design 4).

**3a — reconciling EVE (per-item) with Rust (contents-scaled window).** V6 asks for both: per-item
independence (EVE) and "better caches decay slower" (Rust). This module reconciles them without a
second, cache-wide timer (which would break per-item independence and re-introduce a private curve):
`rarityOrdinal` — the same axis `loot-pack`'s own `valuePerCellMilli` already reads
(`spec-loot-pack.md:81`, `LootGrant.RarityOrdinal`) — raises *that item's own* per-tick survival ‰,
capped below 1000. A better item personally resists destruction longer on every tick it survives; the
cache as a whole still empties gradually, item by item, exactly as EVE's wreck does. No new rarity
vocabulary, no cache-wide multiplier, no `f(Θ)`.

### 4. Frozen while idle — verified, not assumed, to need no new machinery

A world turn advances only when `CommitWorldTurn`'s barrier releases (`RpgStore.WorldTurns.cs:520-526`)
— every commander has to have committed. Since this module's tick runs *inside* that same call, on the
newly-committed turn number, decay is frozen for exactly as long as nobody ends the turn — for free, by
construction, not by any idle-detection logic this module adds. The one honestly-flagged risk: `rpg_worlds`
already declares (but nothing reads, confirmed §Wiring gap) `turn_period_seconds`/`catch_up_cap` columns
that *look like* a future wall-clock auto-advance mode. If that mode is ever wired, this module's
"frozen while idle" guarantee needs re-verification against it — named in §Boundaries as ask-first.

### 5. Why this needs no `RulesetVersion` bump and no `WorldState` field

`rpg_corpse_cache*` lives entirely in SQL, exactly like `rpg_delves`/`rpg_unique_actors`/`rpg_expeditions`
already do — none of those are part of `WorldState`'s hashed, replayed graph either, and `TurnEngine.Step`
never touches any of them. This module's tick reads `header.Seed`/the newly-committed turn number (both
already `CommitWorldTurn` inputs/outputs) and writes SQL rows *beside* the hashed commit, in the same
transaction, the same way the turn log itself does — it never changes what `TurnEngine.Step` computes or
what `StateHasher.Hash` sees. The ideal doc's own filed ask on the World program ("Cache modelling in
`WorldState` if turn-settled… decay in `Events` phase = `RulesetVersion` bump + turn-golden re-bless")
describes the *alternative* this module deliberately does **not** take — folding the cache into `WorldState`
itself — precisely because that alternative is the expensive one (a hash-moving change requiring a golden
re-bless) and nothing about V6's requirements needs it: "ticks in the `Events` phase" is satisfied by
*firing at the same turn boundary Events reports on*, not by literally executing inside `TurnEngine.Events`
(a pure, SQL-free Core function that cannot reach `FusionRpg.Data` at all under `guard-dal.ps1`).

## Tunables

`data/tuning/deployment-hierarchy.v1.json` — the same new domain file the ideal doc's own Tunables table
names for corpse-cache decay (shared with modules 1–3, not a competing file).

| Key | Unit / type | Class | Starting shape |
|---|---|---|---|
| `decay.survivalPerTickMilli.base` | ‰ `long`, bounded 0..1000 | T (PS-8 bounded-ratio exemption, commented) | **994** — solving `(994/1000)^112 ≈ 0.50` (median full-decay turn ≈ 112 for a rarity-0 item; §Numeric types) |
| `decay.survivalPerTickMilli.rarityStepMilli` | ‰ `long` per rarity ordinal step | T | 2 — a rare item's per-tick survival edges up, never guaranteed |
| `decay.survivalPerTickMilli.cap` | ‰ `long`, bounded 0..1000, **< 1000** (no cache is immortal — closed, ideal doc "Alternatives rejected") | T | 999 |
| `decay.medianFullDecayTurnBand.{min,max}` | turns `int` (regression band, computed from the two keys above, never hand-typed as "112") | T | 95, 130 |

Structural, not tunable, each with the exemption comment where declared: the row-major "one tick per
`CommitWorldTurn` call" cadence (no batch-catch-up loop exists to bound); `ShapeLadder`-style closed
outcome vocabulary (`'survived' \| 'destroyed'`, `'instance' \| 'stack'` inherited from `corpse-cache`).

## Numeric types

Turn numbers (`decay_started_turn`, `tick`, `newTurn`) are `int`, matching every existing turn field in
this codebase (`WorldState.CurrentTurn`, `TurnReport`, every `int turn` parameter in
`RpgStore.WorldTurns.cs`/`TurnEngine.cs`, `r.GetInt32` reads throughout) — a structural sequence counter,
never a `contentScale`-touched magnitude (at 1 turn/day, `int.MaxValue` is ≈5.9 million years; no cap is
needed and none is added). `owner_player_id` is `int` matching `rpg_delves.player_id INTEGER` (existing
column, `RpgStore.Delve.cs:98`). Per-mille tunables (`survivalPerTickMilli`, `rarityStepMilli`, `cap`)
are `long`, per `CLAUDE.md` "Numeric overflow" and this codebase's own established convention for every
other ‰ tunable (`spec-loot-pack.md` Numeric types: "Per-mille tunables are `long`") — even though the
legal range is `[0,1000]`, matching house style rather than narrowing to `int` on a case-by-case basis.
`seq` stays `int` (inherited from `corpse-cache`'s own schema, a structural bound on items per cache).
No `float`, no `double`, no `System.Random` anywhere in this module — `SeededRng`/`NextPerMille()` only.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CacheDecay"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~WorldTurns"   # CommitWorldTurn regression
.\scripts\guard-dal.ps1                                  # every new SQL string lives in FusionRpg.Data
.\scripts\guard-actor-hub.ps1                             # unaffected — no combat-derived read added
python scripts\audit-magic-numbers.py --targets M1        # decay.* tunables, not bare literals
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.CacheDecay.cs   NEW
  EnsureCacheDecaySchemaUnlocked  — EnsureColumn(decay_started_turn, owner_player_id) on
                                     rpg_corpse_cache; CREATE TABLE rpg_corpse_cache_decay_log
  TryStartDecayClockUnlocked      — §Design 2, one caller-supplied hook point
  TickCorpseCacheDecayForPlayerUnlocked — §Design 3, one caller-supplied hook point

src/FusionRpg.Data/Sqlite/RpgStore.CorpseCache.cs  (module 3's file, not yet written) gains one line —
                                     ResolveOrCreateCacheUnlocked calls TryStartDecayClockUnlocked
                                     right after a genuine INSERT, same tx — BLOCKED on module 3 landing
                                     its own code first, named here so the hook is not forgotten

src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs   gains one line — CommitWorldTurn calls
                                     TickCorpseCacheDecayForPlayerUnlocked for header.PlayerId, after
                                     the turn-counter UPDATE (:559-571), before tx.Commit() (:573)

tests/FusionRpg.Data.Tests/CacheDecay/  NEW
UNTOUCHED: TurnEngine.cs, TurnCalendar.cs, WorldState.cs, StateHasher.cs (no RulesetVersion bump,
           no hashed field — §Design 5), corpse-cache's own rpg_corpse_cache_item schema/columns
```

## Code style

```csharp
// The one new production line CommitWorldTurn gains, right before its own tx.Commit() (:573).
// header.Seed is already in scope — the same value TurnEngine.Step itself just used.
TickCorpseCacheDecayForPlayerUnlocked(db, tx, header.PlayerId, result.World.CurrentTurn, header.Seed);
```

```csharp
// Per item, per tick: independent, seeded, per-mille, never per-unit-within-a-stack (EVE shape).
var stream = SeededRng.DeriveStream(worldSeed, $"corpse-decay:{cacheId}:{tick}:{item.Seq}");
var effectiveMilli = Math.Min(tuning.DecayCap, tuning.DecayBaseMilli + item.RarityOrdinal * tuning.DecayRarityStepMilli);
var survives = stream.NextPerMille() < effectiveMilli;
```

## Testing strategy

- **Clock-start timing, both branches:** a lawn-cache creation sets `decay_started_turn` to the owner's
  map-world `current_turn` at that exact moment (read, never guessed); a delve-cache creation leaves it
  `NULL` through every room the party still visits, and it becomes non-null only once `CloseDelve` runs,
  equal to the map-world's turn *at that commit*, not the turn the specimen actually died on if the two
  differ. `in_void` flips to `1` at the identical transaction as the first non-null `decay_started_turn`
  write, for `lawn`/`siege`/`delve_room` — no exceptions among those three.
- **`world_sector`/`world_lane` never enter the void (2026-09-13 amendment):** a cache creation at
  either of these two kinds sets `decay_started_turn` to the current map turn immediately (same timing
  as lawn/siege) but leaves `in_void = 0`, never `1` — proving the branch this amendment adds to
  `TryStartDecayClockUnlocked`, in the same test run that proves lawn/siege's own `in_void = 1`
  behavior is unchanged.
- **No re-stamp on reuse:** a second death moving gear into an already-existing, already-decaying cache
  leaves `decay_started_turn` untouched.
- **Idempotent replay — the no-clock guard, extended:** calling `TickCorpseCacheDecayForPlayerUnlocked`
  twice for the identical `(ownerPlayerId, newTurn)` produces byte-identical `rpg_corpse_cache_item`
  state after the second call as after the first (the `rpg_corpse_cache_decay_log` PK guards it). A
  source scan over `RpgStore.CacheDecay.cs` for `DateTime\.(UtcNow|Now)|DateTimeOffset\.(UtcNow|Now)|Environment\.TickCount|System\.Random` —
  zero hits, the same discipline `spec-delve-attrition.md:387-389` already enforces for `Core/Delve/Attrition/`.
- **Frozen while idle:** N calls to `GetWorldTurnReport`'s replay-from-turn-0 path (`RpgStore.WorldTurns.cs:640-649`,
  which calls `TurnEngine.Step` directly, never `CommitWorldTurn`) never write a single
  `rpg_corpse_cache_decay_log` row — decay only ever advances through the one committed-turn path.
- **Per-item independence + the D26-shaped regression:** a 32-seed sweep of caches with mixed rarity
  items computes the observed median turn-to-empty and asserts it lands inside
  `decay.medianFullDecayTurnBand`, printing the number — never asserting "112" as a literal (this
  repo's guardrail-vs-population rule, `AGENTS.md` "A guardrail validates the CONTRACT").
- **Stack vs instance destruction:** a `kind='stack'` row is destroyed or survives as one unit — never
  a per-unit-within-`qty` roll; a `kind='instance'` destruction removes the `rpg_corpse_cache_item` row
  and cascades through the inlined `effect_instance`/`rpg_item` delete, provable by the instance being
  absent from `rpg_item` afterward.
- **`the_module_never_reads_wall_clock`:** the same source-scan shape as the no-clock guard test above,
  run as its own named test rather than folded into another assertion, so a future edit that reintroduces
  `DateTime.UtcNow` fails loudly and specifically.

## Boundaries

- **Always:** one tick per `CommitWorldTurn` call, keyed to the single newly-committed turn; roll every
  remaining item/stack independently; guard every tick with the `(cache_id, tick)` log row before
  touching any item row; derive every roll from `SeededRng.DeriveStream(worldSeed, …)`, never
  `System.Random`; leave an empty cache header in place rather than deleting it.
- **Ask first:** wiring `rpg_worlds.turn_period_seconds`/`catch_up_cap` into a real auto-advance mode —
  if that ever lands, this module's "frozen while idle" guarantee (§Design 4) needs re-verification, not
  a silent assumption that it still holds; folding `rpg_corpse_cache` into `WorldState`'s hashed graph
  (the alternative §Design 5 explicitly did not take) is a different, more expensive design and needs its
  own sign-off, not a retrofit.
- **Never:** a second survival roll at field-access/retrieval read time (certain-on-reach is locked); a
  per-unit roll inside a `kind='stack'` row; branching on `source_kind` (module 3's own boundary,
  inherited); a `*_utc`/`DateTime` field driving tick arithmetic; a catch-up loop processing more than
  one tick per `CommitWorldTurn` call (none is needed — see §Design 3); a `RulesetVersion` bump or a new
  `WorldState` field for this module's own state; a void-transition trigger for `world_sector`/
  `world_lane` caches (2026-09-13 amendment — none exists under this ask, named as future work if a
  later ask needs one, not invented here to fill the gap).

## Success criteria

1. A lawn/siege cache's `decay_started_turn`/`in_void` are set at creation, together; a delve cache's
   stay `NULL` until `CloseDelve`, then are set together; a `world_sector`/`world_lane` cache's
   `decay_started_turn` is set at creation but `in_void` stays `0` (2026-09-13 amendment). 2. Replaying
   one `(ownerPlayerId, newTurn)` tick twice changes nothing the second time. 3. A 32-seed sweep's
   observed median empty-turn lands inside `decay.medianFullDecayTurnBand`. 4. Idle players (no
   `CommitWorldTurn` call) accrue zero decay, proven against the report-replay path specifically.
   5. `guard-dal.ps1`/`guard-actor-hub.ps1` green; no `RulesetVersion` bump; no `WorldState` field added.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `rpg_corpse_cache.in_void` (0/1) | `cache-field-access` (module 5) reads `in_void = 0` — a delve cache still at its room, revisit-loot eligible, **and (2026-09-13 amendment) a `world_sector`/`world_lane` cache, which never leaves `in_void = 0` under this ask**; `cache-retrieval-mission` (module 6) targets only `in_void = 1` rows (V2 — a bounty is a map-less mission against a void-resident cache, never a second fate) — which structurally excludes `world_sector`/`world_lane` today, named in V5 above |
| `rpg_corpse_cache.decay_started_turn` + a plain read of `rpg_worlds.current_turn` for `owner_player_id` | modules 5/6 compute elapsed ticks (`currentTurn - decay_started_turn`) for display/decisioning; neither module derives its own clock or re-implements this arithmetic |
| `rpg_corpse_cache_item` row presence/count (module 3's own table, this module only ever *removes* rows from it) | modules 5/6 read it directly, unchanged from `corpse-cache`'s own established interface — this module never adds a row, never a second content shape |
| `rpg_corpse_cache_decay_log.outcomes_json` | optional/audit only — no module 5/6 requirement reads it; available for a future "why did I lose this" surface, never required for correctness |

## Design-gate checklist

```
[x] Subsystems: world-turn commit (Data), delve settlement (Data), item disposition (Data) — no
    Status/ActorHub/Core-World-Step subsystem touched; TurnEngine.cs itself is UNTOUCHED.
[x] Read this session: deployment-hierarchy-ideal.md §Corpse-drop items 2/7, §Baggage/void (V1-V6),
    §Resolved third clearing round items 2/4/5; deployment-hierarchy-map.md row 4; spec-corpse-cache.md
    in full; spec-deploy-carry.md in full (house style + CommitWorldTurn-adjacent precedent);
    spec-loot-pack.md in full (DeleteInstance/disposition precedent, D26 regression-band pattern);
    tunables-ssot.md §1; decisions.md "World turn phase order" row.
[x] Code cited by file:line, opened this session: TurnCalendar.cs (:13-24, :32, :42), world.v5.json
    (:83-84, :105-106), TurnEngine.cs (whole file, :315-339 Events phase, :138), SeededRng.cs (whole
    file), ExpeditionResolver.cs (:106), RpgStore.WorldTurns.cs (whole file, :139-153, :484-577,
    :520-526, :559-573, :640-649), RpgStore.Delve.cs (:98-99, :762-856), RpgStore.World.cs (:20-35),
    RpgStore.Items.cs (:71-73, :85-98), RpgStore.AtomInstances.cs (:59-134); spec-delve-attrition.md
    (:375-425, specifically :387-389 and :423).
[x] Drift reported: decisions.md's "World turn phase order" row text lists nine phases and omits
    `Assaults` (added 2026-09-05, `TurnEngine.cs:96-126` shows ten) — noted as an existing, pre-existing
    doc/code drift this module did not cause and does not need to fix to proceed (Events is still
    correctly after Pressure and before Snapshot in both). `corpse-cache`'s own `decay_started_utc`
    column name is a real naming defect for a turn-counted clock, not a citation error — corrected here
    by adding a sibling `decay_started_turn` column rather than silently repurposing the existing one.
[ ] Whether `injury-tiers`' own "worsened wound -> Retired" trigger for a delve-deployed specimen could
    ever fire outside `CloseDelve` was reasoned from shipped code's existing shape (every delve-scoped
    recovery mechanism found this session routes through CloseDelve/SettleExtractionUnlocked), not
    confirmed against module 2's own code, which does not exist yet — named as implementation's first
    task to reconfirm, not asserted as already guaranteed.
[x] No §2 invariant contradicted: SQL only in `FusionRpg.Data`; magnitudes (per-mille tunables) are
    `long`; turn counters stay the existing `int` convention with a stated reason, not a magnitude; no
    `f(Θ)`; no second ActorHub composer; no cap without the PS-8 bounded-ratio exemption comment; no
    RulesetVersion bump / no new WorldState field (§Design 5 argues why none is needed).
[x] Two builds explicitly named as blocked on an upstream module landing its own code first
    (`ResolveOrCreateCacheUnlocked`'s one-line hook, `corpse-cache` not yet coded) are stated as BLOCKED
    in §Structure, not silently assumed buildable today.
[x] Amendment 2026-09-13 (later session): V5 corrected for `world_sector`/`world_lane` — re-derived,
    not assumed, that the "no durable place-row" reasoning behind lawn/siege's immediate void does not
    hold for a world-map sector/lane (`WorldState.cs` confirms both are durable, persistent places).
    `TryStartDecayClockUnlocked` gains the one `place_kind` branch this section's original text said
    was unnecessary — corrected here rather than silently kept, since that claim only covered the three
    kinds known when it was written.
```
