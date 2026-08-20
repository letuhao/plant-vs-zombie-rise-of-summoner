# Event envelope

Every event:

```json
{
  "t": "2026-08-14T16:00:00.0000000Z",
  "game": "pvzrh-3.8.1",
  "kind": "plant.spawn",
  "matchKey": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "payload": {}
}
```

`t` is UTC. Server adds `id`, `player_id`, and `run_id` when storing. **Do not send `player_id` on the wire.**

`matchKey` is a guid minted on `board.start` (`Board.Awake`) and attached to every later event until `board.end`. `injector.hello` / `patch.failed` / `catalog.*` may omit it.

Spawn / recapture payloads **are** the dump JSON (same keys as `spawn_stats.stats_json`). Not a 6-int extract.

Do **not** emit events from `Update` / `OnTriggerStay2D` / `GameAPP.Start` / EventNodes.

## Match

| kind | When | payload |
|---|---|---|
| `board.start` | `Board.Awake` | `{ levelName, levelType, boardLevel, modifiers }` |
| `board.modifiers` | same dump on start (also nested on start) | `DumpBoardConfig` |
| `board.snapshot` | GameOver + end | full dump (not 9 keys) |
| `board.economy` | poll sun/money/points/planted/wave-health change | live Board fields |
| `match.result` | `BoardStatistics.GameOver` — **source of truth** | `{ result }` victory/defeat/surrender/timeout |
| `match.win` | `BoardVictory.Win` breadcrumb | `{}` |
| `match.lose` | `GameLose.HandleGameLose` breadcrumb | `{}` |
| `match.invade` | `GameLose.ProcessZombieEnter` | `{ zombiePtr, type, typeName }` |
| `match.restart` | `PauseMenu_Btn.Restart` | `{}` |
| `match.pause` | `UIMgr.EnterPauseMenu` / `InGameUI.PauseGame` via `MatchHost.NotifyPaused(true)` — observe only | `{}` |
| `match.resume` | `UIMgr.BackToGame` (also `BackToMenu` clear) via `MatchHost.NotifyPaused(false)` — observe only | `{}` |
| `board.end` | `Board.Die` | `{ levelName, summary }` |
| `wave.change` | poll `Board.theWave` / `theMaxWave` | `{ wave, maxWave, timeUntilNextWave, zombieCurrentWaveHealth, zombieSpawnHealth, zombieTotalHealth, theTotalNumOfZombie, isHugeWave }` |
| `wave.spawn` | `BoardSpawner.SummonZombies` | `{ wave }` |
| `wave.huge` | `Board.HugeWaveEvent` | `{ wave }` |
| `level.name` | `InGameUI.SetLevelName` + poll | `{ levelName }` |

## Plant / zombie

| kind | When |
|---|---|
| `plant.place` | `CreatePlant.SetPlant` — counts `runs.plants_planted` |
| `plant.spawn` | `Plant.Start` or `SetPlantAttributes` if Start skipped — full dump, `source` |
| `plant.mix` | `CreatePlant.MixEvent` |
| `plant.unique` | `CreatePlant.UniqueEvent` |
| `plant.die` | `Plant.Die` — `{ type, typeName, ptr, reason, reasonName }` |
| `plant.damage` | `TakeDamage` Prefix if `logDamage` (`path: take`) |
| `plant.crash` | `Plant.Crashed` |
| `zombie.place` | `SetZombie` / `SetZombieWithMindControl` |
| `zombie.spawn` | after HP apply — full dump |
| `zombie.die` | `Die` / `DestoryZombie` |
| `zombie.damage` | `TakeDamage` Prefix if `logDamage` |
| `zombie.hypno` | `Zombie.SetMindControl` |
| `zombie.status` | `Buttered` / `UnButtered` / `SetFreeze` / `SetCold` / `SetPoison` / `Warm` / `KillDebuff` |
| `entity.stats` | recapture after `SetHealthInTravel` / `SetZombieHealth` / `Reinforce*` — full dump, **appends** `spawn_stats` |
| `stat.applied` | after EntityApply Resolve→Writer |
| `stat.writer` | proof: EntityStatWriter field write (`hpBefore`/`hpAfter`, `source`) when `SYS-EMIT-PROOF` |
| `stat.limhealth` | proof: `Plant.LimHealth` observe vs Writer registry when `SYS-EMIT-PROOF` |
| `board.plantCreate` | `Board.OnPlantCreate` |
| `board.plantDie` | `Board.OnPlantDie` |

Damage extras use `path`: `real` / `take` / `body` / `apply`. Scale DEF only on `TakeDamage` `ref int`.

## Economy / items

| kind | Hook |
|---|---|
| `sun.spend` | `Board.UseSun` |
| `sun.gain` | `Board.GetSun` |
| `sun.refund` | `Shovel.PayBackSun` |
| `money.spend` | `Board.UseMoney` |
| `money.gain` | `Board.GetMoney` |
| `points.gain` | `Board.GetPoint` |
| `item.drop` | `CreateItem.SetCoin` |
| `item.fertilize` | `Fertilize.Use` |
| `item.hammer` | `Hammer.Use` |
| `item.wheel` | `Wheel.Use` |
| `item.bucket` | `ItemManager.SetBucket` |
| `prize.click` | `PrizeMgr.Click` |
| `prize.spawn` | `Lawnf.SetAward` |

## Cards / tools / pets / grid / travel

| kind | Hook |
|---|---|
| `card.pick` | `Mouse.ClickOnCard` |
| `card.use` | `CardUI.UseOnce` (+ `thePlantLevel`) |
| `card.place` | `TryToSetPlantByCard` / `TryToSetZombieByCard` |
| `card.bank` | `InitBoard.CreateCard` |
| `card.drop` | `Lawnf.SetDroppedCard` |
| `almanac.select` | Almanac `SelectCard` |
| `convey.pool` | `ConveyManager.GetCardPool` (one-shot / postfix) |
| `present.open` | `Present.RandomPlant` |
| `plant.shovel` | `Shovel.Use` |
| `plant.unfuse` | `Mouse.DisassemblePlant` |
| `plant.glove` | `TryToSetPlantByGlove` / `Glove.Use` |
| `pet.spawn` | `MiniPet.SetPet` |
| `pet.xp` | `MiniPet.GetExperience` |
| `grid.place` | `GridItem.SetGridItem` |
| `grid.die` | `GridItem.Die` |
| `travel.start` | `TravelMgr.OnBoardStart` |
| `travel.buff` | GetNormalBuff / GetUltiBuff / GetDebuff / GetInvestBuff / UnlockPlant |
| `travel.pick` | `MultipleChoiceMenu.OnSelect` |

## Catalog / mower / bullet / injector

| kind | When |
|---|---|
| `catalog.types` | hello + retry on `Board.Awake`. `{ side, entries: [{ type, typeName, displayName }] }` |
| `catalog.recipes` | `ChildToParents` chunks `{ entries: [{ parentA, parentAName, parentB, parentBName, result, resultName }] }` |
| `catalog.zombies` | `InitZombieList.InitZombie` |
| `mower.place` / `mower.start` / `mower.die` | SetMower / StartMove or poll `started` / Die |
| `bullet.init` | `Bullet.InitData` |
| `bullet.place` | `CreateBullet.SetBullet` |
| `combat.hit` | `Bullet.HitZombie` / `HitPlant` — `{ side, bulletPtr, bulletType, damage, targetPtr, targetType, fromType?, scenarioId? }` |
| `combat.hitland` | `Bullet.HitLand` |
| `injector.hello` | plugin loaded |
| `patch.failed` | SafePatchAll |
| `test.probe` | sim only |

## Debug pipeline

See [runbook/debug-pipeline.md](../runbook/debug-pipeline.md). Kinds (often stamped with `scenarioId`):

| kind | When |
|---|---|
| `debug.session.start` / `end` | Debug session |
| `debug.scenario.start` | Named scenario began (server) |
| `debug.spawn.plant` / `zombie` / `bullet` | Debug spawn with dump fields |
| `debug.status.applied` / `cleared` | Status method or float |
| `debug.onkill.extra` / `onkill.status` | Armed onKill one-shots |
| `debug.onhit.extra` / `onhit.status` | Armed onHit one-shots |
| `debug.mods.set` / `wave.freeze` / `arm` / `disarm` / `snapshot` | Control |
| `debug.fx.shader-probe` | LIVE `Shader.Find` result (`found`, `missing`, `drawShader`) |
| `debug.fx.shown` | VFX cue rendered (`cueId`, anchor, `rgb`, `hybrid`, `primitives`) — vfx-ssot.md §11 |
| `debug.fx.skipped` | VFX cue dropped (`cueId`, enumerated `reason`: `disabled`, `unknown-cue`, `muted`, `rate-limited`, `cap`, `missing`, `no-shader`, `particle-fail`) |
| `debug.fx.list` / `debug.fx.mute` | Catalog cue ids + mute state roundtrip |

## Cheat inject / probe observability

| kind | When | payload |
|---|---|---|
| `cheat.inject` | Every toggle / set-float / action (and pack steps) when `SYS-EMIT-PROOF` | `{ source, op, id?, action?, enabled?, value?, probeId?, packId?, correlationId }` — `source`: `web` \| `f8` \| `pack` |
| `cheat.apply` | Legacy free-text Note (still emitted) | `{ note, probeId?, … }` |
| `cheat.enrich` | First catalog type sighting | `{ side, type, typeName, source }` |
| `probe.start` | `POST /api/cheats/probe` | `{ probeId, packId, label, hint, expectedKinds }` |
| `probe.end` | `POST /api/cheats/probe/end` or F8 End / timeout | `{ probeId, packId?, reason }` |

When a probe is active, high-value outcomes (`stat.applied`, `plant.spawn` / `zombie.spawn`, `board.modifiers`, `board.economy`, `plant.place`) include the same `probeId` / `correlationId` / `packId`.

Live packs: `GET /api/cheats/packs`, `POST /api/cheats/probe`, `POST /api/cheats/probe/end` (always on — not sim-only).

`type` is the enum int. `typeName` is `.ToString()`. `displayName` is `Lawnf.GetName` when it works.

## Noisy kinds (SQLite only, not live-pushed)

`plant.damage`, `zombie.damage`, `bullet.init`, `bullet.place`, `item.drop`, `pet.xp`

## Metrics names (global)

`plants_spawned`, `plants_died`, `zombies_spawned`, `zombies_killed`, `bullets_spawned`, `mowers_used`, `injector_connected`, `runs_started`, `runs_ended`
