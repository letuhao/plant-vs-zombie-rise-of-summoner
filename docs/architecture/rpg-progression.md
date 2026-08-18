# RpgProgression architecture

First RPG content feature on top of Pvz* foundation: **per-save actor XP and levels** (player / plant-type / zombie-type). No combat power effects in v1.

See [pvz-middle-layer.md](pvz-middle-layer.md), [pvz-activity.md](pvz-activity.md). For later power/effects vocabulary, see [ARPG effects → FusionRpg mapping](../research/arpg-effects/06-fusionrpg-mapping.md) (inspiration only).

## Player binding

All rows are scoped by `players.id`. Switching current save is a full progression context switch. Awards use the Activity/run `player_id` (stamped at `board.start`).

## Actors

| kind | type_id | Meaning |
|---|---|---|
| `player` | `0` | Save’s player actor |
| `plant` | `PlantType` int | That plant type for this save |
| `zombie` | `ZombieType` int | That zombie type for this save (balance / future power input) |

Identity: `(player_id, kind, type_id)`.

**Orthogonal grain — unique specimens:** individual plant/zombie with gear/level across runs use a durable `instanceId` and the UniqueActor FSM — **not** this type PK. Do not overload `rpg_actor_progression` for specimens. See [unique-actor-runtime.md](unique-actor-runtime.md) and [unique-entity-effects.md](unique-entity-effects.md).

## Tables

| Table | Role |
|---|---|
| `rpg_actor_progression` | Mutable SSOT: level, xp, highest_level, demotion_count, revision (+ XP reason buckets that survive ledger trim) |
| `rpg_xp_ledger` | Append-only awards; UNIQUE idempotency `(player_id, kind, type_id, reason, dedupe_key)`; API reads the **hot tail** ≤5 000/actor after post-run compact (see [rest.md](../protocol/rest.md), [ledger-snapshot.md](../database/ledger-snapshot.md)) |

## Awards (from Activity)

| Activity | Actor | Reason | Delta (POC-locked) |
|---|---|---|---|
| `ZombieKilled` | player | `kill` | +12 × `RpgXpPowerScale.ForKill` (stub **1.0** until zombie power SSOT) |
| `MatchEnded` defeat | player | `defeat` | −100 |
| `MowerUsed` | player | `mower` | −30 |
| `PlantPlaced` | plant/`type` | `plant_place` | +8 |
| `ZombieSpawned` | zombie/`type` | `zombie_spawn` | +9 |

Every place/spawn awards (not once-per-type-per-run). Kill ledger `payload_json` includes `powerScale` for future audit.

**Power-scaled kill XP is reserved:** scaler stub returns 1.0 until zombie power SSOT exists. Do not treat flat awards as final combat-coupled design.

## Curve

Arithmetic per kind:

```text
XpToNext(kind, level) = first(kind) + (level - 1) * step(kind)
```

POC-locked: player `100/45`, plant `80/32`, zombie `70/28`. Unlimited levels; clamp at `long.MaxValue`. Demotion increments `demotion_count` (debt for future power).

## Level-change pipeline

`ILevelChangeHandler` Chain of Responsibility in Core. v1: no power handlers. Future power clears demotion via API.

## API extras

- List actors: `offset` + `limit` → `{ items, total, limit, offset }`
- Ledger: `afterId` cursor → `{ items, limit, nextAfterId? }`
- `GET .../stats` → XP-by-reason, plant/zombie level buckets, recent deltas (charts)

## FE

`#/rpg-progression` Almanac dossier: **Overview** (hero + KPIs + charts + top lists), **Plants** / **Zombies** (`Split` list↔dossier + paging), **Ledger** (filters + cursor pager). Bound to `currentPlayerId`.

Plant/zombie rows and dossiers show **type icons** from `GET /api/icons/{side}/{typeId}.png` (PNG BLOBs in SQLite) when the injector has uploaded them (browse the in-game almanac while the RPG server is running). Missing icons fall back to `#typeId`.

## Balance

Casual (~40 kills/win): first match reaches **≥L2**; ~L12–20 after 20 matches. Loss streak with mowers demotes. Zombie wave spam can outpace plant spam. See `RpgProgressionBalanceTests`.
