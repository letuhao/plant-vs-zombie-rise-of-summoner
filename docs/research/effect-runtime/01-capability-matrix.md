# Effect capability matrix

Effect-oriented CAPTURE / WRITE surface for PVZ Fusion 3.8.1. Reconciles injector code, cecil dump, cheat suite, and local mod decomps. Not an ADR.

See legend in [`00-index.md`](00-index.md).

## Effect-event matrix

| Effect event | Capture today | Write / Intent today | Best API | Status | Notes |
|---|---|---|---|---|---|
| `onSpawn` (plant) | `plant.place` / `plant.spawn` | EntityApply HP/ATK | `CreatePlant.SetPlant`, `Plant.Start` | **WRITE-proven** (stats) | Place API writable; product writes stats not “place for free” |
| `onSpawn` (zombie) | `zombie.place` / `zombie.spawn` | EntityApply + Intent extra | `CreateZombie.SetZombie` | **WRITE-proven** | `pvz.spawn.extra` + cheat SP-* |
| `onSpawn` (bullet) | `bullet.place` / `bullet.init` | Cheat `D-DMG-*` on InitData | `CreateBullet.SetBullet`, `Bullet.InitData` | CAPTURE + **WRITE-claimed** | Bullet.Damage write needs live DPS proof |
| `onHit` | TakeDamage Bullet + AttackPlant → `combat.hit` | — | TakeDamage / AttackPlant (Hit* off) | **READY** LIVE W0-D | Emit when LogDamage/hit-capture or OnDamageDealt grant |
| `onGetHit` | `plant.damage` / `zombie.damage` | DEF scale on TakeDamage | `Plant`/`Zombie.TakeDamage` | **WRITE-proven** (DEF) | Defender + amount; damageFrom stamped when present |
| `onKill` | `zombie.die` / `plant.die` | Cheat kill + arms | `Zombie.Die` / `DestoryZombie` | **READY** LIVE | Enough for onKill design |
| `applyStatus` | `debug.status.applied` | `/api/debug/apply-status` methods | `Buttered` / `SetCold` / … | **READY** LIVE | Prefer methods over floats |
| `spawnEntity` (proc) | ack on Intent | `pvz.spawn.extra` | SetZombie / SetPlant | **WRITE-proven** | No combat→Intent bridge yet |
| `modifyOutgoing` | bullet.init dump | attackDamage Writer; D-DMG cheat | `Plant.attackDamage`, `Bullet.Damage` | **READY** plant ATK→hit | Bullet.Damage ≠ pea-family hit |
| `modifyIncoming` | damage events | TakeDamage Prefix DEF | `ref` damage on TakeDamage | **WRITE-proven** | Alt paths CAPTURE-only (no DEF) |
| `grantEconomy` | `board.economy` | `G-SUN/MONEY/PTS` / `debug.economy` | `Board.theSun` / `theMoney` / `thePoints` | **PROBE** → F24+ | Tag `economy`; ICD/cap in product |
| `modifyMoveSpeed` | entity dump | `Z-SPD-*` / set-mods | `uniqueSpeed` | **PROBE** → F29–F30 | |
| `aura` / tick | — | — | — | **OUT** if only via Update | Prefer timed status timers on entity |
| `ground effect` | — | — | `Bullet.HitLand`, `BoardAction.CreateFreeze` | **DOC** / **MOD-proven** | HitLand Harmony skipped |

## Intent bridge (today vs needed)

| Command | Exists | Feeds Effect |
|---|---|---|
| `pvz.spawn.extra` | Yes | Manual / feature enqueue only — **ready** as spawn action |
| `pvz.status.apply` | No (debug apply-status exists) | Needed later for butter/freeze from Effects |
| `pvz.economy.grant` | No (cheat/`debug.economy` exists) | Optional Intent for grantSun/Money |
| `pvz.bullet.spawn` | No | Optional; SetBullet is noisy |
| Combat → Intent | No | Needs onHit/onKill capture first |

Activity already projects `ZombieKilled` from `zombie.die` and `ExtraSpawnFired` from Intent accept.

## FusionRpg Harmony vs gap

| Already patched | Missing for Effects |
|---|---|
| TakeDamage DEF + log + Bullet `combat.hit` | HitLand coverage (W12); shooter plant ptr on pea hit |
| AttackPlant melee `combat.hit` | Status method Prefix/Postfix (beyond debug API) |
| Bullet.InitData / SetBullet capture | Proc ICD polish (design) |
| Die / DestoryZombie | |
| SetZombie Intent spawn | |
| Z-SLOW-* speed floats | `Buttered` / `SetFreeze` / `SetCold` from Effect sink |

## OUT (must not become product Effect hooks)

- `Update` / `FixedUpdate` / `OnTriggerStay2D` / `Bullet.OnTriggerEnter2D` as primary hooks
- `GameAPP.Start`, EventNodes, particles
- `Board.OnPlantCreate` / `OnPlantDie`
- GodMode / NoLose / writing sun-money as Effect outcomes
- Base `Bullet.HitZombie` / `HitPlant` as FT* primary (W0-D: TakeDamage + AttackPlant)

Prefer **TakeDamage / AttackPlant** over OnTrigger*, and **status methods** over per-frame field spam.
