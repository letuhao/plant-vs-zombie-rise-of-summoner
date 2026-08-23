# RpgProgression architecture

First RPG content feature on top of Pvz* foundation: **per-save actor XP and levels** (player / plant-type / zombie-type). No combat power effects in v1.

See [pvz-middle-layer.md](pvz-middle-layer.md), [pvz-activity.md](pvz-activity.md). For derived status power and ApplyScale, see [actor-hub-ssot.md](actor-hub-ssot.md). For later power/effects vocabulary, see [ARPG effects → FusionRpg mapping](../research/arpg-effects/06-fusionrpg-mapping.md) (inspiration only).

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

> **⚠ ADR P1 amended 2026-08-23.** The zombie power SSOT now exists: it is `Θ_content`
> ([power/ssot-power-scale.md](power/ssot-power-scale.md) §5). `RpgXpPowerScale` is **retired** — its
> documented future job is exactly what `Θ_content` does, and a stub whose replacement exists is dead
> code. The POC curve `2^min(level,12)` is retired with it; `progression.power` becomes linear `Θ`.
> Spec: [power/spec-status-contest.md](power/spec-status-contest.md). **Specced, not built** — the
> stub descriptions above remain accurate until wave 3.

## Combat power (design — not shipped)

RpgProgression **levels** are the future input for combat **`progression.power`** on the Actor Hub derived catalog — separate from XP awards and separate from **`progression.bonus.*`** combat flats.

| Concept | Role today | Future |
|---|---|---|
| **`RpgXpPowerScale.ForKill`** | Kill XP audit multiplier (stub **1.0**) | May read zombie tier for XP only — **not** status ApplyScale |
| **`progression.power`** | Not in code | Derived channel from type level × realm; v1 Actor Hub stub **1.0** hardcoded |
| **`progression.bonus.*`** | Not in code | Flat HP/ATK/defense at AppliedCombat merge — separate power ADR |
| **`IProgressionPowerProvider.UpdatePower`** | **In code** since ADR P1 — `IProgressionPowerProvider.cs:15`. This row said *"Not in code"* and was wrong | **Superseded** by `IPowerIndexProvider` (ADR P1 amendment, 2026-08-23) |

**Grain for power:** same as progression PK — `(player_id, kind, type_id)`. Plant on lawn uses plant type level; zombie defender uses zombie type level. Player actor level may add summoner-wide omni later.

**Precedence (v1 stub):** all Hot entities use hardcoded **`tierPower = 1.0`** until `IProgressionPowerProvider.UpdatePower` ships. Future: lawn entity reads type level from SQLite; bound unique specimen precedence — see [unique-actor-runtime.md](unique-actor-runtime.md).

Do **not** conflate `RpgXpPowerScale` (ledger `powerScale` in kill payload) with combat `progression.power`. See [actor-hub-ssot.md](actor-hub-ssot.md) and [../research/actor-core-chaos-mapping.md](../research/actor-core-chaos-mapping.md) for Chaos level/realm curve reference.

## Curve

Arithmetic per kind:

```text
XpToNext(kind, level) = first(kind) + (level - 1) * step(kind)
```

POC-locked: player `100/45`, plant `80/32`, zombie `70/28`. Unlimited levels; clamp at `long.MaxValue`. Demotion increments `demotion_count` (debt for future power).

## Level-change pipeline

`ILevelChangeHandler` Chain of Responsibility in Core. v1: no power handlers. Future: `UpdatePower` on level change clears demotion via API and feeds Actor Hub `progression.power`.

## API extras

- List actors: `offset` + `limit` → `{ items, total, limit, offset }`
- Ledger: `afterId` cursor → `{ items, limit, nextAfterId? }`
- `GET .../stats` → XP-by-reason, plant/zombie level buckets, recent deltas (charts)

## FE

`#/rpg-progression` Almanac dossier: **Overview** (hero + KPIs + charts + top lists), **Plants** / **Zombies** (`Split` list↔dossier + paging), **Ledger** (filters + cursor pager). Bound to `currentPlayerId`.

Plant/zombie rows and dossiers show **type icons** from `GET /api/icons/{side}/{typeId}.png` (PNG BLOBs in SQLite) when the injector has uploaded them (browse the in-game almanac while the RPG server is running). Missing icons fall back to `#typeId`.

## Balance

Casual (~40 kills/win): first match reaches **≥L2**; ~L12–20 after 20 matches. Loss streak with mowers demotes. Zombie wave spam can outpace plant spam. See `RpgProgressionBalanceTests`.
