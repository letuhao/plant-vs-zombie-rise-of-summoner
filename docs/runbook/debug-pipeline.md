# Debug pipeline runbook

Controllable effect-test APIs. You stay in a level; curl or a PowerShell script drives setup and asserts via events.

**Not** EffectBag. Debug one-shots only.

**LIVE prove:** BepInEx — [`debug-live-checklist.md`](debug-live-checklist.md). MelonLoader (Blooms) — [`melon-live-checklist.md`](melon-live-checklist.md). This page is the API reference (host-agnostic).

## Deploy

```powershell
# BepInEx (default)
.\scripts\deploy-play.ps1

# MelonLoader (Blooms 3.8.1 Melon pack — not 3.9)
$env:FUSIONRPG_ML_GAMEDIR = "<Blooms Game Files>"
.\scripts\deploy-play.ps1 -LoaderHost MelonLoader
# After lawn open: .\scripts\smoke-melon-live.ps1
```

Enter any level. Leave the lawn running. Prefer Simulator **off**. Leave `FUSIONRPG_MELON_SKIP_HARMONY` unset for Melon LIVE.

Base URL: `http://127.0.0.1:5088`

## TypeId catalog (defaults — confirm with `/api/types`)

| Role | Constant in code | Typical 3.8.1 |
|---|---|---|
| Peashooter | `DebugScenarios.PeaTypeId = 0` | PlantType 0 |
| Wall-nut | `WallNutTypeId = 3` | confirm live |
| Basic zombie | `BasicZombieTypeId = 0` | ZombieType 0 |

If spawn looks wrong, `GET /api/types?side=plant` and adjust scenario constants or pass explicit `typeId` on spawn calls.

## Quick start

```powershell
# Session
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/session/start -ContentType application/json -Body '{}'

# Freeze waves + clear + pea + tank zombie
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/scenario/p1-baseline -ContentType application/json -Body '{}'

# Watch hits
Invoke-RestMethod 'http://127.0.0.1:5088/api/debug/events?kinds=combat.hit,zombie.damage,debug.spawn.plant&limit=50'
```

## Endpoint catalog

| Method | Path |
|---|---|
| POST | `/api/debug/session/start` |
| POST | `/api/debug/session/end` |
| GET | `/api/debug/session` |
| GET | `/api/debug/snapshot` |
| GET | `/api/debug/events?afterId=&kinds=&scenarioId=&limit=` |
| GET | `/api/debug/scenarios` |
| POST | `/api/debug/scenario/{id}` |
| POST | `/api/debug/reset-board` |
| POST | `/api/debug/clear-plants` |
| POST | `/api/debug/clear-zombies` |
| POST | `/api/debug/spawn-cell` |
| POST | `/api/debug/ensure-sun` |
| POST | `/api/debug/wave-freeze` `{ "enabled": true }` |
| POST | `/api/debug/select` |
| POST | `/api/debug/kill` / `kill-plant` |
| POST | `/api/debug/spawn-plant` |
| POST | `/api/debug/spawn-zombie` |
| POST | `/api/debug/spawn-bullet` |
| POST | `/api/debug/spawn-extra` / `fire-spawn-extra` |
| POST | `/api/debug/set-mods` |
| POST | `/api/debug/reset-mods` |
| POST | `/api/debug/reapply` |
| POST | `/api/debug/board-stats` | Living plants/zombies ATK/HP + effect `sessionMods` (`debug.board-stats` event) |
| POST | `/api/debug/apply-status` |
| POST | `/api/debug/apply-status-float` |
| POST | `/api/debug/clear-status` |
| POST | `/api/debug/arm/{kind}` | `onkill-extra`, `onkill-status`, `onhit-extra`, `onhit-status` |
| POST | `/api/debug/disarm` |
| POST | `/api/debug/effect/grant` | Upsert Foundation grant (ack only; listen for `debug.effect.granted`) |
| POST | `/api/debug/effect/withdraw` | Remove grant |
| POST | `/api/debug/effect/clear` | Withdraw all + clear ICD/dedupe/session effect mods |
| POST | `/api/debug/effect/list` | Emit snapshot summary event (not sync HTTP body) |
| POST | `/api/debug/effect/fire-synthetic` | Inject FT* without lawn capture |
| POST | `/api/debug/effect/enqueue-delta` | Funnel mutation + FA10 Writer Add + overlay floater (`amount`, optional `targetPtr`/`tag`) |
| POST | `/api/debug/effects/reload` | Re-seed catalog (`effects.reload`) |
| GET | `/api/debug/effects/contract` | Server-local FT*/FA* + `FoundationContractVersion` |
| POST | `/api/sim/effect/clear` | Offline host clear (sync `{ ok, revision }`) |
| POST | `/api/sim/effect/grant` | Offline grant → grant DTO + revision |
| POST | `/api/sim/effect/withdraw` | Offline withdraw |
| POST | `/api/sim/effect/fire` | Offline fire (`event` / capture `kind` / `helper=hit\|die\|spawn`) → `IntentPlanDto` |
| POST | `/api/sim/effect/scenario` | Run offline scenario JSON → step results (`matchStart` / `matchEnd` Secondary lifecycle ops) |
| GET | `/api/sim/effect/snapshot` | Sync `EffectCatalogSnapshotDto` (LIVE `list` is event-only) |

### Spawn plant body (example)

```json
{
  "typeId": 0,
  "col": 2,
  "row": 2,
  "atk": 99,
  "hp": 300,
  "maxHp": 300,
  "attackPercent": 5.0
}
```

### set-mods body (example)

```json
{
  "probePlant": true,
  "probeBullet": false,
  "logDamage": true,
  "plant": { "attackPercent": 5.0, "defensePercent": 1.0 },
  "zombie": { "defensePercent": 1.0, "uniqueSpeed": 0.2 },
  "bullet": { "damageSet": -1, "damagePercent": 1.0 }
}
```

## Named scenarios

| id | Purpose |
|---|---|
| `p1-baseline` / `p1-plant` / `p1-bullet` | ATK path proof |
| `hit-capture` | `combat.hit` fields |
| `status-butter` / `freeze` / `cold` / `poison` | method status |
| `status-float-butter` | float-only CC |
| `status-clear` | clear CC |
| `def-plant` / `def-zombie` / `def-alt-paths` | DEF + P6 observe |
| `onkilled-extra` / `onhit-extra` / `onhit-status` / `onkill-status` | proc loops |
| `kill-signal` / `kill-plant` | die events |
| `spawn-matrix` | absolute mods |
| `spawn-bullet-hit` | forced bullet |
| `wave-freeze-check` | stable board |
| `hitland-butter` | HitLand (may need butter bullet type) |
| `econ-sun-set` / `econ-sun-add` | Set/add sun → `board.economy` |
| `econ-money-set` / `econ-money-add` | Set/add money |
| `econ-points-set` | Set points |
| `zombie-speed-slow` / `zombie-speed-fast` | `uniqueSpeed` 0.3 / 2.0 (visual) |
| `onspawn-inspect` | Absolute HP/ATK on spawn dumps |
| `ondeath-inspect` | `plant.die` + `zombie.die` payloads |
| `zombie-atk-bite` | Zombie ATK×5 vs wall-nut |
| `plant-produce` | Sunflower + produce interval |
| `board-config-speed` | `E-ZS` board.config |
| `spawn-mc` | Mind-controlled zombie |
| `combat-area-row` / `combat-random` | Overlay area / random FA10 fan-out |
| `combat-counter-target` / `combat-counter-actor` | Counter burst (5 synthetics) |
| `combat-dot` | OverTime scheduler (−20 × 5) |
| `combat-heal` | Positive FA10 heal |

`GET /api/debug/scenarios` lists ids.

## LIVE checklist

Full ordered rows (F1–F23 completed; **F24–F36 expansion**), P1 verdict, and sign-off: **[`debug-live-checklist.md`](debug-live-checklist.md)**.

`POST /api/debug/scenario/{id}` enqueues a single `debug.run-steps` (leading `debug.reset-mods`, then steps sequential on the injector main thread).

Copy results into [`../research/effect-runtime/04-proof-results.md`](../research/effect-runtime/04-proof-results.md) and [`07-effect-opportunities.md`](../research/effect-runtime/07-effect-opportunities.md).

## Assert tips

```powershell
$after = 0
$r = Invoke-RestMethod "http://127.0.0.1:5088/api/debug/events?afterId=$after&kinds=combat.hit,zombie.damage&limit=100"
$r.items | Select-Object id, kind, payload
```

Wait ~0.5–1s after each scenario for Unity main-thread drain (longer for bite/speed visuals).

## FA10 enqueue-delta (HP + floater)

BepInEx 3.8.1 and Melon 3.9. In-match, `SYS-DAMAGE-FX` default on. `debug.effect.fire-synthetic` is FT* only — use this for Writer Add + overlay.

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/spawn-zombie -ContentType application/json -Body '{"typeId":0,"row":2}'
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/select -ContentType application/json -Body '{}'
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/effect/enqueue-delta -ContentType application/json -Body '{"amount":-100}'
Invoke-RestMethod 'http://127.0.0.1:5088/api/debug/events?kinds=debug.effect.fired,stat.writer,debug.fx.shown,debug.fx.skipped,debug.effect.enqueue-delta&limit=50'
```

Expect `ApplyResourceDelta` fired, `stat.writer` `hpBefore`/`hpAfter` delta −100, `debug.fx.shown` (Neutral white floater). Optional `{ "amount": 0, "tag": "Dodge" }` is MISS only (no HP write).

Area / multi-target (after combat SSOT I3): freeze lawn census, then pass `target` on enqueue-delta (or grant `fx.overlay_damage` and fire a hit). Example row nuke:

```powershell
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/scenario/combat-area-row
# then either wait for a pea hit, or:
$ptr = "<zombie ptr from debug.board-stats>"
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/effect/enqueue-delta -ContentType application/json -Body (@{
  amount = -50
  targetPtr = $ptr
  target = @{ mode = "Area"; shape = "Row"; anchor = "EventTarget"; filters = @{ side = "zombie" }; maxTargets = 8 }
} | ConvertTo-Json -Depth 6 -Compress)
```

Expect several `stat.writer` rows (one per zombie in the lane) and `debug.combat.packet` with `fa10` count. Single-target `{ "amount": -100, "targetPtr": "..." }` is unchanged. Empty `POST /api/debug/select` body `{}` selects the first living zombie.

Named combat scenarios (grant + select + fire-synthetic): `combat-area-row`, `combat-counter-target`, `combat-counter-actor`, `combat-dot`, `combat-heal`, `combat-random`.

Counter / DoT: after `combat-dot`, the synthetic hit arms OverTime; injector ticks ~100ms and applies −20 HP five times over 5s. Inspect with `POST /api/debug/effect/dots` and `POST /api/debug/effect/board-snapshot`. Do not `fire-synthetic` `OnTimer` to tick DoT.

`debug.combat.packet` fields: `source` (`capture` / `synthetic` / `dot` / `enqueue-delta`), `fa10`, `ptrs`, `trigger`, `skipped`.

## Injector commands

`debug.run-steps`, `debug.reset-mods`, `debug.session`, `debug.spawn-plant`, `debug.spawn-zombie`, `debug.spawn-bullet`, `debug.set-mods`, `debug.reapply`, `debug.apply-status`, `debug.apply-status-float`, `debug.clear-status`, `debug.arm`, `debug.disarm`, `debug.kill`, `debug.kill-plant`, `debug.wave-freeze`, `debug.ensure-sun`, `debug.economy`, `debug.board-config`, `debug.select`, `debug.spawn-cell`, `debug.reset-board`, `debug.clear-plants`, `debug.clear-zombies`, `debug.snapshot`, `debug.effect.enqueue-delta`, `debug.effect.fire-synthetic`, `debug.effect.board-snapshot`, `debug.effect.dots`, `debug.effect.counters`, plus `pvz.spawn.extra`.

REST helpers: `POST /api/debug/economy`, `POST /api/debug/board-config`.