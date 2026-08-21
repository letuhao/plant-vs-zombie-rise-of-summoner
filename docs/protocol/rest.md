# REST API

Base URL: `http://127.0.0.1:5088`

JSON, UTF-8. No auth in v1.

CORS: allow `http://127.0.0.1:5173` and `http://localhost:5173`.

## `GET /health`

```json
{
  "ok": true,
  "injectorConnected": false,
  "lastHeartbeatUtc": null,
  "simEnabled": false,
  "source": "none",
  "currentPlayerId": 1,
  "ingestQueued": 0,
  "lastFlushMs": 0
}
```

`injectorConnected` is true if a heartbeat arrived in the last 5 seconds.  
`source` is `none`, `sim`, or `injector`.  
`simEnabled` is true only when `FUSIONRPG_SIM=1`.  
`currentPlayerId` is the active save (`settings.current_player_id`).  
`ingestQueued` is the in-process writer backlog. `lastFlushMs` is the last SQLite batch commit duration.

## Players

### `GET /api/players`

```json
{
  "items": [ { "id": 1, "name": "Player 1", "createdUtc": "..." } ],
  "currentPlayerId": 1
}
```

### `POST /api/players`

Body `{ "name": "Nene" }`. Creates a row. Returns the row. Does **not** auto-select.

### `PUT /api/players/current`

Body `{ "id": 2 }`. Selects that save. Open matches keep their original `player_id`. Next `board.start` uses this id.

### `GET /api/players/current`

Returns `{ "id": 1, "name": "Player 1", "createdUtc": "..." }` or 404.

## PvzStats

Player-bound modifier foundation (see [architecture/pvz-stats.md](../architecture/pvz-stats.md)).

| Method | Path |
|---|---|
| GET | `/api/pvz-stats/current` |
| GET | `/api/pvz-stats/{playerId}` |
| GET | `/api/pvz-stats/{playerId}/channels/{channel}` |
| GET | `/api/pvz-stats/{playerId}/modifiers` |
| POST | `/api/pvz-stats/{playerId}/modifiers/upsert` |
| POST | `/api/pvz-stats/{playerId}/modifiers/withdraw` |
| POST | `/api/pvz-stats/{playerId}/modifiers/reset` |
| POST | `/api/test/seed-pvz-stats-demo` |

SignalR: `PvzStatsUpdated`. Injector command: `pvz.stats.reload`.

## PvzActivity

Player-bound typed facts + rollups (see [architecture/pvz-activity.md](../architecture/pvz-activity.md)).
Facts APIs return the **hot tail** (≤10 000 facts/player after post-run compact); rollup counters are snapshot-backed and survive trim.

| Method | Path |
|---|---|
| GET | `/api/pvz-activity/current` |
| GET | `/api/pvz-activity/{playerId}` |
| GET | `/api/pvz-activity/{playerId}/facts` |
| POST | `/api/pvz-activity/{playerId}/facts/append` |
| POST | `/api/test/seed-pvz-activity-demo` |

SignalR: `PvzActivityUpdated`.

## PvzIntent

| Method | Path |
|---|---|
| POST | `/api/pvz-intent/spawn-extra` |

Enqueues `pvz.spawn.extra` **only when** the Activity fact is newly inserted (`correlationId` idempotent). Records `ExtraSpawnFired` on successful accept. See [architecture/pvz-intent.md](../architecture/pvz-intent.md).

## UniqueActor

Cold specimens (see [architecture/unique-actor-runtime.md](../architecture/unique-actor-runtime.md)). Equip only in **Roster**; awards apply on next Deploy Bound loadout.

| Method | Path | Notes |
|---|---|---|
| GET | `/api/unique/actors` | Query `playerId` optional |
| GET | `/api/unique/actors/{id}` | |
| POST | `/api/unique/actors` | `{ side, typeId, playerId? }` |
| POST | `/api/unique/actors/{id}/deploy` | Intent + loadout merge from `mods_json` when deploy body empty |
| POST | `/api/unique/actors/{id}/fail-deploy` | |
| POST | `/api/unique/actors/{id}/retire` | |
| GET | `/api/unique/actors/{id}/equipment` | Slots + `modsJson` |
| PUT | `/api/unique/actors/{id}/equipment/{slot}` | `{ itemId }` — slot `weapon\|armor\|trinket`; known stubs only; **409** `phase.not_roster`; **400** `unknown_item` / `bad_slot` |
| DELETE | `/api/unique/actors/{id}/equipment/{slot}` | Same Roster gate |
| POST | `/api/unique/actors/{id}/xp` | `{ delta, reason? }` — finite `delta > 0`; **409** `phase.retired`; **400** `bad_delta` |

## Debug pipeline

Controllable effect-test APIs (not EffectBag). Runbook: [runbook/debug-pipeline.md](../runbook/debug-pipeline.md). LIVE checklist: [runbook/debug-live-checklist.md](../runbook/debug-live-checklist.md).

| Method | Path | Notes |
|---|---|---|
| POST | `/api/debug/session/start` | Body optional `{}` / `{ scenarioId }` |
| POST | `/api/debug/session/end` | |
| GET | `/api/debug/session` | Server mirror of session/arms |
| GET | `/api/debug/snapshot` | Queues `debug.snapshot` |
| GET | `/api/debug/events` | `afterId`, `kinds`, `scenarioId`, `limit` |
| GET | `/api/debug/scenarios` | Named scenario ids |
| POST | `/api/debug/scenario/{id}` | One `debug.run-steps` (includes `debug.reset-mods`) |
| POST | `/api/debug/reset-board` | Empty body OK |
| POST | `/api/debug/spawn-plant` / `spawn-zombie` / `spawn-bullet` | |
| POST | `/api/debug/set-mods` / `reset-mods` / `reapply` | |
| POST | `/api/debug/apply-status` / `apply-status-float` / `clear-status` | |
| POST | `/api/debug/kill` / `kill-plant` / `wave-freeze` / `ensure-sun` / `select` | |
| POST | `/api/debug/spawn-extra` / `fire-spawn-extra` | Intent accept path (fact + command) |
| POST | `/api/debug/arm/{kind}` / `disarm` | onkill/onhit arms |
| POST | `/api/debug/effect/enqueue-delta` | Funnel FA10 Writer Add + overlay FX (`amount`, optional `targetPtr`/`tag`; `target` spec may use cell `anchor`) |
| POST | `/api/debug/effect/grant` | Grant overlay (`target` / `delivery` / `burst`) |
| POST | `/api/debug/effect/fire-synthetic` | Inject FT* (`OnDamageDealt` default). Omitting ptrs uses selected. |
| POST | `/api/debug/effect/board-snapshot` | Frozen combat census (`debug.effect.board-snapshot`) |
| POST | `/api/debug/effect/dots` | Active OverTime entries |
| POST | `/api/debug/effect/counters` | Counter meters |
| POST | `/api/debug/fx/probe-shaders` | LIVE `Shader.Find` of Fusion-included particle/unlit shaders (`debug.fx.shader-probe`) |
| POST | `/api/debug/fx/world-flash` | Particle burst at lawn `col`/`row` (defaults spawn cell). No HP write |
| POST | `/api/debug/fx/play` / `list` / `mute` / `unmute` / `state` | Play cue / list / mute / state |
| GET | `/api/debug/effects/contract` | Frozen FT*/FA* including FA10 `ApplyResourceDelta` (`FoundationContractVersion` 2) |
| POST | `/api/debug/combat/pin-element` / `silence-vanilla` / `probe` / `snapshot` | Overlay combat prove |
| POST | `/api/debug/shield/grant` / `clear` / `demo` / `demo-all` / `snapshot` / `bar-status` | RPG shield + world bar audit |
| POST | `/api/debug/board-stats` / `stress-fill` / `stress-clear` / `enter-level` | Census / stress / gated EnterGame |
| POST | `/api/debug/clear-plants` / `clear-zombies` / `spawn-cell` / `economy` / `board-config` / `board-action` | Board helpers |
| POST | `/api/debug/spawn-grid` / `clear-grid` / `set-box` / `grid-query` / `ice-road` | Grid helpers |
| GET/POST | `/api/debug/actor-derived` | Derived combat profile |
| GET | `/api/debug/effects/session-grants` | Session grant snapshot |
| POST | `/api/debug/effects/reload` | Reload effects |

Full LIVE operator contract: [runbook/live-test-ssot.md](../runbook/live-test-ssot.md).

## RpgProgression

Per-save actor XP (see [architecture/rpg-progression.md](../architecture/rpg-progression.md)).
Ledger API returns the **hot tail** (≤5 000 rows/actor after post-run compact); `stats` / `xpByReason` use durable buckets and survive trim.

| Method | Path | Notes |
|---|---|---|
| GET | `/api/rpg/progression/{playerId}/summary` | |
| GET | `/api/rpg/progression/{playerId}/stats` | Charts: xpByReason, level buckets, recentDeltas |
| GET | `/api/rpg/progression/{playerId}` | Query: kind, sort, limit, offset. Body: items, total, limit, offset |
| GET | `/api/rpg/progression/{playerId}/{kind}/{typeId}` | 404 if actor missing |
| GET | `/api/rpg/progression/{playerId}/ledger` | Query: kind, typeId, reason, limit, afterId. Body: items, limit, nextAfterId |
| POST | `/api/rpg/progression/{playerId}/{kind}/{typeId}/clear-demotion` | |
| POST | `/api/test/seed-rpg-progression-demo` | |

SignalR: `RpgProgressionUpdated`.

## `GET /api/stats`

Returns current **global** modifiers (not per-player this pass).

```json
{
  "plants": { "hpPercent": 1.0, "hpFlat": 0, "attackPercent": 1.0, "attackFlat": 0, "defensePercent": 1.0, "defenseFlat": 0 },
  "zombies": { "hpPercent": 1.0, "hpFlat": 0, "attackPercent": 1.0, "attackFlat": 0, "defensePercent": 1.0, "defenseFlat": 0 },
  "logDamage": true,
  "applyStats": true
}
```

Default `logDamage` is **true** for this ingest dump (still togglable).

## `PUT /api/stats`

Same body as GET. Persists and broadcasts `StatsUpdated`. Invalid numbers: 400.

## `POST /api/events`

Body: one [EventEnvelope](events.md) or `{ "events": [ ... ] }`.

**Enqueues** into the in-process channel and returns `{ "accepted": N }` immediately. SQLite insert + projections happen on the writer thread (batches of 500–1000, one transaction). `GET /api/events` may lag tens of ms.

`matchKey` on `board.start` opens a run stamped with the **current player**. Child events with that key copy `player_id` from the run.

Noisy kinds (`plant.damage`, `zombie.damage`, `bullet.init`, `bullet.place`, `item.drop`, `pet.xp`) are stored but not live-pushed to the web hub.

## `GET /api/events?limit=100&afterId=0`

Newest last. Default limit 100, max 500. Optional `playerId` query filters to that save.

## `GET /api/types`

Plant/zombie catalog for later RPG tables. Optional `?side=plant` or `?side=zombie`.

```json
{
  "items": [
    {
      "game": "pvzrh-3.8.1",
      "side": "plant",
      "type": 0,
      "typeName": "Peashooter",
      "hpBase": 300,
      "maxHpBase": 300,
      "attackBase": 20,
      "armorBase": null,
      "armorMaxBase": null,
      "seenCount": 12,
      "killedCount": 3,
      "firstSeenUtc": "...",
      "lastSeenUtc": "..."
    }
  ]
}
```

Names can exist with `seenCount: 0` after `catalog.types`. `hpBase` on types is a **first-seen sample**, not combat SSOT. Use `spawn_stats` / the run’s dump for real HP.

`displayName` comes from `Lawnf.GetName` when present.

## Type icons (almanac art)

Captured live from `AlmanacCardUI` when the player selects a card in the in-game pedia. Stored as PNG BLOBs in SQLite (`type_icon_layers` / composed `type_icons`); served from the API (not loose files under `data/icons`).

| Method | Path | Notes |
|---|---|---|
| `PUT` | `/api/icons/{side}/{typeId}` | Raw `image/png` body; write-if-absent; `{ created, url }` |
| `GET` | `/api/icons/{side}/{typeId}` | Metadata `{ url }` or 404 |
| `GET` | `/icons/{side}/{typeId}.png` | Static file |

`side` is `plant` or `zombie`. On first create, hub broadcasts `TypeIconUpdated` to the web group.

## `GET /api/recipes`

Fusion recipes from `catalog.recipes`. `{ "items": [ { "parentA", "parentAName", "parentB", "parentBName", "result", "resultName" } ] }`

## `GET /api/runs/{id}/spawns`

Append-only `spawn_stats` for that run: `{ "items": [ { "id", "ptr", "side", "type", "source", "capturedUtc", "stats" } ] }`. `stats` is the full dump JSON.

## `GET /api/runs`

Filtered to **current player** unless `?playerId=` is passed.

```json
{
  "items": [
    {
      "id": 1,
      "playerId": 1,
      "matchKey": "...",
      "startedUtc": "...",
      "endedUtc": "...",
      "levelName": "Unknown",
      "result": "victory",
      "mowersUsed": 2,
      "plantsPlanted": 12,
      "plantsDied": 1,
      "zombiesKilled": 40,
      "summary": {},
      "archiveUri": null
    }
  ]
}
```

`archiveUri` is set when the run’s capture was cold-moved under `archive/`; otherwise null/absent (still hot on the DB).

## Storage (user-driven clear)

No background archive GC. Web `/storage` multi-selects targets; open runs are refused. Nuclear `POST /api/test/reset` stays sim-only.

| Method | Path | Body / notes |
|---|---|---|
| `GET` | `/api/storage/summary` | `{ archiveCount, closedRunsStillHot, openRuns, activityOverTail, xpOverTail }` |
| `GET` | `/api/storage/archives` | `{ items: [ { uri, kind, runId, createdUtc } ] }` from cold catalog |
| `POST` | `/api/storage/archives/delete` | `{ uris: string[] }` → `{ deleted, refused }` (path must stay under `archive/`) |
| `POST` | `/api/storage/runs/purge-capture` | `{ runIds: long[] }` → `{ deleted, refused }` — closed only; clears hot events/entities/mowers/spawn_stats; keeps run row |
| `POST` | `/api/storage/runs/delete` | `{ runIds: long[] }` → `{ deleted, refused }` — closed only; purge capture + delete run row (+ catalog links) |
| `POST` | `/api/storage/trim-tails` | `{}` → `{ ok: true }` — user-triggered Activity/XP compact to sealed tails |

## `POST /api/heartbeat`

JSON `{}` or `{ "source": "injector" }` (default `injector`). `{ "source": "sim" }` is used by the in-process simulator heartbeat.

## `POST /api/metrics` / `GET /api/metrics`

Global rollups. Same as before. Extra name: `mowers_used`.

## `POST /api/commands/reload-stats`

Empty body. SignalR `StatsUpdated` to group `injector`.

## Simulator and probes (`FUSIONRPG_SIM=1` only)

If the env var is unset, all of these are **404**. If `source` is a live `injector`, sim POSTs return **409**.

See [testing/probes.md](../testing/probes.md) and [runbook/simulator.md](../runbook/simulator.md).

| Method | Body (typical) |
|---|---|
| `POST /api/sim/hello` | `{}` |
| `POST /api/sim/board/start` | `{ "levelName": "Sim" }` — returns `{ "matchKey": "..." }` |
| `POST /api/sim/board/end` | `{ "summary": {} }` |
| `POST /api/sim/board/snapshot` | `{ "sun", "wave", "maxWave", ... }` |
| `POST /api/sim/match/result` | `{ "result": "victory" }` |
| `POST /api/sim/match/win` | `{}` |
| `POST /api/sim/match/lose` | `{}` |
| `POST /api/sim/wave` | `{ "wave": 2, "maxWave": 10 }` |
| `POST /api/sim/plant/place` | `{ type, col, row, ptr }` |
| `POST /api/sim/zombie/place` | `{ type, row, ptr }` |
| `POST /api/sim/plant/mix` | `{ usedType, plantPtr, row }` |
| `POST /api/sim/economy` | `{ sun, money, points, plantedCount }` |
| `POST /api/sim/sun/spend` | `{ count }` |
| `POST /api/sim/level/name` | `{ levelName }` |
| `POST /api/sim/wave/spawn` | `{ wave }` |
| `POST /api/sim/card/use` | `{ plantType, cost }` |
| `POST /api/sim/pet` | `{ petType }` |
| `POST /api/sim/grid` | `{ type, col, row }` |
| `POST /api/sim/hypno` | `{ ptr }` |
| `POST /api/sim/crash` | `{ ptr }` |
| `POST /api/sim/recipes` | `{}` emits `catalog.recipes` |
| `POST /api/sim/zombies-catalog` | `{ types: [...] }` |
| `POST /api/sim/entity/stats` | recapture dump `{ ptr, side, hp, source }` |
| `POST /api/sim/plant/spawn` | optional `{ type, hp, maxHp, attack, col, row, ptr }` |
| `POST /api/sim/plant/damage` | `{ "ptr": "P1", "damage": 50 }` |
| `POST /api/sim/plant/die` | `{ "ptr": "P1" }` |
| `POST /api/sim/zombie/spawn` | optional `{ type, hp, maxHp, attack, armor, armorMax, ptr }` |
| `POST /api/sim/zombie/damage` | `{ "ptr": "Z1", "damage": 50 }` |
| `POST /api/sim/zombie/die` | `{ "ptr": "Z1" }` |
| `POST /api/sim/mower/place` | optional `{ ptr, type, typeName, row }` |
| `POST /api/sim/mower/start` | `{ "ptr": "M1" }` |
| `POST /api/sim/mower/die` | `{ "ptr": "M1" }` |
| `POST /api/sim/bullet` | `{}` |
| `GET /api/sim/state` | entities, ptr counters, last stats, `matchKey` |
| `POST /api/sim/effect/clear` | `{}` — bag-only clear |
| `POST /api/sim/effect/grant` | `EffectGrantDto` |
| `POST /api/sim/effect/withdraw` | `{ grantId }` |
| `POST /api/sim/effect/fire` | `event` / capture `kind` / `helper=hit\|die\|spawn` → `IntentPlanDto` |
| `POST /api/sim/effect/scenario` | scenario JSON **or** `{ path }` → `EffectScenarioRunResult` (supports `matchStart` / `matchEnd` Secondary lifecycle ops) |
| `GET /api/sim/effect/snapshot` | `EffectCatalogSnapshotDto` |
| `POST /api/test/reset` | `{}` |
| `GET /api/test/snapshot` | health + stats + events + runs + players + metrics + sim + `eventCount` / `eventCounts`. **Flushes** the writer first |
| `POST /api/test/probe` | `{ "name", "scenario", "data" }` |

Spawn defaults: plant 300 HP / 20 ATK; zombie 270 HP / 50 ATK / armor 0.
