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
| `ZombieKilled` | player | `kill` | +12 × kill power scale (stub **1.0** — `RpgXpAwardMap.NoKillPowerScaleYet`, awaiting `content-scale`) |
| `MatchEnded` defeat | player | `defeat` | −100 |
| `MowerUsed` | player | `mower` | −30 |
| `PlantPlaced` | plant/`type` | `plant_place` | +8 |
| `ZombieSpawned` | zombie/`type` | `zombie_spawn` | +9 |

Every place/spawn awards (not once-per-type-per-run). Kill ledger `payload_json` includes `powerScale` for future audit.

**Power-scaled kill XP is still reserved, not wired:** the multiplier stub still returns 1.0 — only
its *source class* changed (T3.3 deleted `RpgXpPowerScale`, replacing it with a structural constant
in `RpgXpAwardMap` — same value, zero behaviour change). A real value needs `content-scale` (T3.4+),
not yet built. Do not treat flat awards as final combat-coupled design.

> **✅ ADR P1 amended 2026-08-23, built 2026-08-24 (power-todo.md T3.1/T3.2/T3.3 — all three done).**
> The zombie power SSOT now exists: it is `Θ_content` ([power/ssot-power-scale.md](power/ssot-power-scale.md) §5).
> `progression.power` is now linear **`Θ`** from `IPowerIndexProvider`, and the POC curve
> `2^min(level,12)` is retired and **deleted** (`ProgressionPowerCurve.cs` is gone).
> `RpgXpPowerScale` is **deleted** too (T3.3) — its documented future job was exactly what
> `Θ_content` does, and a stub whose replacement now exists was dead code. `RpgXpAwardMap.FromActivity`
> keeps the same 1.0 multiplier as a structural constant (`NoKillPowerScaleYet`) so kill XP itself is
> unaffected until `content-scale` gives it a real value.
> Spec: [power/spec-status-contest.md](power/spec-status-contest.md).

## Combat power (T3.1/T3.2 shipped 2026-08-24; bonus flats still design-only)

RpgProgression **levels** are the future input for combat **`progression.bonus.*`** flats on the
Actor Hub derived catalog — separate from XP awards and now also separate from
**`progression.power`**, which no longer reads level at all (see above).

| Concept | Role today | Future |
|---|---|---|
| **Kill power scale** | Kill XP audit multiplier (stub **1.0**, `RpgXpAwardMap.NoKillPowerScaleYet`) — `RpgXpPowerScale` class **deleted** (T3.3) | May read `Θ_content` for XP only (`content-scale`, T3.4+) — **not** status ApplyScale |
| **`progression.power`** | **In code, shipped** — `RpgProgressionSubsystem` reads `IPowerIndexProvider.ActorIndex(ctx)`, defaulting to Θ=0 (`StubPowerIndexProvider`) when un-hydrated | Real hydration (`InjectorPowerIndexProvider`/`ServerPowerIndexProvider`, both built T1.4) needs a live caller — not this program's scope |
| **`progression.bonus.*`** | Not in code — gated on a bare `Func<StatContext,int>?` delegate `RpgProgressionSubsystem` accepts but nothing production passes (T1.4) | Flat HP/ATK/defense at AppliedCombat merge — separate power ADR, unrelated to `progression.power`'s own T3.2 rewiring |
| **`IProgressionPowerProvider`** | **Deleted** (T1.4) | **Superseded** by `IPowerIndexProvider` — done, not a future |

**Grain for power:** same as progression PK — `(player_id, kind, type_id)`. Plant on lawn uses plant type level; zombie defender uses zombie type level. Player actor level may add summoner-wide omni later.

**Precedence (current):** all Hot entities read `tierPower = progression.power × progression.realm`
through `IPowerIndexProvider` — **0 × 1.0 = 0** until a host actually hydrates a real Θ snapshot
(`HydratedPowerIndexProvider.Hydrate`/`InjectorPowerIndexProvider.Hydrate`), which nothing in
production calls yet (same "built, not wired to a live caller" state as `IPowerIndexProvider` itself
since T1.4). Future: lawn entity reads type level from SQLite; bound unique specimen precedence — see
[unique-actor-runtime.md](unique-actor-runtime.md).

Do **not** conflate the kill power scale (`RpgXpAwardMap.Award.PowerScale`, ledger `powerScale` in kill payload) with combat `progression.power`. See [actor-hub-ssot.md](actor-hub-ssot.md) and [../research/actor-core-chaos-mapping.md](../research/actor-core-chaos-mapping.md) for Chaos level/realm curve reference.

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
