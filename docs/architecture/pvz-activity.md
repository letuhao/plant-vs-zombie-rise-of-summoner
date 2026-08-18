# PvzActivity architecture

Player-bound **typed play facts** — substrate for progression/achievements/loot eligibility.

Not RPG quests/trade/building. Those later features **emit** Activity or upsert Stats / enqueue Intent.

See [pvz-middle-layer.md](pvz-middle-layer.md).

## SSOT vs cache

| Table | Role |
|---|---|
| `pvz_activity_facts` | **SSOT** append-only facts (API reads the **hot tail** ≤10 000/player after post-run compact; see [rest.md](../protocol/rest.md), [ledger-snapshot.md](../database/ledger-snapshot.md)) |
| `pvz_activity_rollups` | **Cache** per-player counters JSON (snapshot-backed; survives trim) |
| `pvz_activity_revisions` | Revision bumped when rollups rebuild |

Never treat `events` / `spawn_stats` as progression SSOT — project into facts instead.

## Fact kinds (v1)

| Kind | From |
|---|---|
| `MatchStarted` | `board.start` |
| `MatchEnded` | `match.result` |
| `ZombieKilled` | `zombie.die` |
| `PlantLost` | `plant.die` |
| `PlantPlaced` | `plant.place` |
| `MowerUsed` | `mower.start` |
| `ZombieSpawned` | `zombie.spawn` |
| `ExtraSpawnFired` | Recorded on **successful Intent accept** (`POST /api/pvz-intent/spawn-extra` inserted), not from later `source=extra` capture |

`MowerUsed` and `ZombieSpawned` are **progression-facing** facts (XP awards). Rollup counter rebuild does **not** include them in match/kill/plant tallies — they exist so RpgProgression can apply awards without inventing a second event stream.

Idempotency: `(player_id, run_id, kind, dedupe_key)` unique. `dedupe_key` is always set: capture uses ptr / `col:row` / `run`; append API auto-assigns a GUID when blank. Empty string must not collapse distinct appends.

Allowlist: append only accepts kinds in `PvzActivityKinds.Known` (else 400).

## Write paths

1. **Projection** — server ingest projects capture kinds into facts, then rebuilds rollups; dirty players get SignalR `PvzActivityUpdated`.
2. **Feature API** — `POST .../facts/append` for trusted server-side awards (no game event).
3. **Intent** — spawn-extra inserts `ExtraSpawnFired` once per `correlationId`, then enqueues the command (idempotent gate).

## Read paths

| Route | Role |
|---|---|
| `GET /api/pvz-activity/{playerId}` | Rollup sheet |
| `GET /api/pvz-activity/{playerId}/facts` | Timeline (`kind`, `runId` filters) |
| SignalR `PvzActivityUpdated` | Rollup revision sync |

No injector living-reapply (unlike PvzStats).
