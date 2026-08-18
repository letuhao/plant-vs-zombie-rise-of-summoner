# Effect opportunities from game surface

Design inventory: what FusionRpg can **do to the lawn** → candidate Effect triggers/actions.  
Feeds a richer RPG Effect list. **Not** an ADR; **not** EffectBag code.

**Foundation Effect catalog (architecture):** [`../../architecture/effect-system.md`](../../architecture/effect-system.md) — Passive\|Triggered, 4 triggers, 9 actions; Secondary grant/overlay only.

Evidence: LIVE checklist 2026-08-16, expansion F24+ scenarios, [`04-proof-results.md`](04-proof-results.md), [`06-combat-metrics-hit-vs-speed.md`](06-combat-metrics-hit-vs-speed.md), [`cheat-menu-coverage.md`](../cheat-menu-coverage.md).

Legend: **READY** = LIVE or solid WRITE · **PROBE** = wired; needs LIVE row · **DOC** = API exists · **OUT** = banned Harmony / Update spam (not “economy”).

Tag `economy` = sun / money / points — **Effect-capable**; product must ICD/cap.

---

## 1. Triggers (when Effects fire)

| Trigger | Signal today | Status | Design note |
|---|---|---|---|
| `onSpawn` plant | `plant.place` / `plant.spawn` / `stat.applied` / `debug.spawn.plant` | **READY** | Buff on place; HP/ATK/DEF/interval |
| `onSpawn` zombie | `zombie.place` / `zombie.spawn` / InitHealth / `debug.spawn.zombie` | **READY** | Elite HP/speed/ATK |
| `onSpawn` bullet | `bullet.init` / `bullet.place` | **READY** | Tag projectile; Damage for butter-family only |
| `onHit` (dealt) | `combat.hit` + `*.damage` (+ TakeDamage on-hit arms) | **READY** | Pea: TakeDamage Bullet; plant bite: `AttackPlant` (`source=attackPlant`); HitZombie/HitPlant Harmony off |
| `onHit` (forced / spawned bullet) | same + `debug.spawn.bullet` | **READY** LIVE | Spawned bullet with `Damage` hits for that amount (**operator confirm**); Effect **action** + **onHit trigger** when it lands — not plant AnimShoot pea |
| `onGetHit` | same damage events | **READY** | DEF scales here |
| `onKill` | `zombie.die` | **READY** | Extra spawn / status / **grantSun** |
| `onDeath` plant | `plant.die` | **READY** | Revenge / economy reward |
| `onWave` | `wave.spawn` / Board wave | **PROBE** | Wave-start affix |
| `onMindControl` | SetMindControl / MC spawn | **PROBE** | Hypno outcome |
| `onHitLand` | `combat.hitland` | **NOT SHIPPED** | Deferred base HitLand Harmony applies (no AV) but ~134 overrides → no LIVE events |
| Tick / aura | Update | **OUT** | Prefer timed status methods |

LIVE inspect scenarios: `onspawn-inspect`, `ondeath-inspect`.

---

## 2. Actions (what Effects do)

### Survivability / power

| Action | Game knob | Cheat / debug | Status |
|---|---|---|---|
| `modifyHp` | plant/zombie HP + armor | `A-*-HP*`, spawn absolutes | **READY** |
| `modifyAtk` | `attackDamage` / `theAttackDamage` | `A-*-ATK*` | **READY** (plant→hit LIVE; zombie bite **PROBE** F32) |
| `modifyDef` | TakeDamage Prefix | `A-*-DEF*` | **READY** |
| `modifyAttackInterval` | `thePlantAttackInterval` | `P-ATK-INT` | **READY** LIVE |
| `modifyProduceInterval` | `thePlantProduceInterval` | `P-PROD-INT` | **READY** LIVE (faster sun) |
| `modifyBulletDamage` | `Bullet.Damage` | `D-DMG-*` | **READY** write; narrow hit path |
| `modifyZombieSpeed` | `uniqueSpeed` / `theSpeed` / `theOriginSpeed` | `Z-SPD-*` | **READY** LIVE (slow + fast / x10) |
| `modifyTakeDmgMult` | `takeDmgMultiplier` | `Z-TAKEMULT` | **NOT SHIPPED** (LIVE inconclusive) |
| Board pressure | `Board.config` E-* | `E-ZH/ZD/ZS/ZC` + apply | **READY** LIVE F35 (E-ZS=0.4; originSpeed scales) |

### Control / ailments

| Action | Game knob | Status |
|---|---|---|
| `applyButter` / `applyFreeze` / `applyCold` / `applyPoison` | status methods | **READY** LIVE |
| `applyFloatSlow` | speed floats | **READY** weak (no VFX) |
| Extra CC (jala/ember/kelp/garlic) | methods | **NOT SHIPPED** (not wired in debug apply) |
| `clearStatus` | UnButtered / KillDebuff | **READY** |
| AOE freeze | `BoardAction.CreateFreeze` | **READY** LIVE F37 (`env-freeze`) |

### Spawn / board

| Action | Game knob | Status |
|---|---|---|
| `spawnExtraZombie` | `pvz.spawn.extra` | **READY** |
| `spawnPlant` | `CreatePlant.SetPlant` | **READY** |
| `spawnMindControlled` | `SetZombieWithMindControl` | **READY** LIVE (hypno) |
| `spawnBullet` | `SetBullet` + `Damage` | **READY** LIVE | Forced projectile: Damage→hit (**operator confirm**). Use as Effect **action** (lob extra shot) and as **onHit trigger source** when that bullet lands. Does **not** replace plant-fired pea ATK. |
| Summon wave | `BoardSpawner.SummonZombies` | **PROBE** |
| Cherry / doom / fireline | BoardAction | **READY** LIVE F38–F40 |
| `spawnGrave` / `clearGrave` | `SetGridItem` / `Die` | **READY** LIVE F42–F43; onKill arms F48–F49 |
| `spawnIceBlock` | `GridItemType.IceBlock` | **READY** LIVE F44 |
| `setBoxType` (water/grass/lava/dirt) | `BoardGrid.boxType` | **READY** LIVE F45–F47; Dirt/nuclear F50 |
| Ice trail (`CreateIceRoad`) | `DriverZombie` | **NOT SHIPPED** — F51 FAIL (spawns Sledge; no trail Effect) |
| Environment / weather (snow, fog, fallout scene) | SceneType / FogMgr | **NOT SHIPPED** — LEVEL-BOUND. See [`08-environment-field-surface.md`](08-environment-field-surface.md) |

### Economy (`economy` tag)

| Action | Game knob | Cheat | Status |
|---|---|---|---|
| `grantSun` / `setSun` | `Board.theSun` | `G-SUN-SET` / `G-SUN-ADD` | **READY** LIVE F24–F25 |
| `grantMoney` / `setMoney` | `Board.theMoney` | `G-MONEY-*` | **READY** LIVE F26–F27 |
| `grantPoints` / `setPoints` | `Board.thePoints` | `G-PTS-*` | **READY** LIVE F28 |

Assert: UI + `board.economy` event. Product Effects should **cap/ICD** economy grants; not banned at API level.

### Explicit OUT (hooks / abuse paths)

- Primary hooks on `Update` / `OnTrigger*` / `GameAPP.Start` / EventNodes  
- `Board.OnPlantDie` / `OnPlantCreate` (trampoline AV)  
- Uncapped Update spam of slows  
- GodMode / NoLose as silent Effect outcomes (proof cheats only)

---

## 3. Richer RPG Effect list (v1 sketch)

### On spawn
1. +% HP / ATK / DEF on plant or zombie  
2. −attack interval (plant fire rate)  
3. Elite: +HP, −`uniqueSpeed`, +ATK  
4. Poison-on-spawn for first N zombies  
5. Faster sunflower produce (`modifyProduceInterval`)

### On hit / get hit
6. Lucky Hit → method butter/cold (ICD)  
7. Temporary DEF More on get-hit  
8. Pea family plant fire: plant ATK only (not Bullet.Damage)  
8b. Spawned/proc bullet: `Damage` drives hit; landing can trigger onHit Effects  

### On kill / on death
9. OnKill → `spawnExtraZombie` (capped)  
10. OnKill → status AOE / butter nearest  
11. OnKill → `grantSun` / `grantMoney` (small, capped)  
12. OnPlantDeath → revenge spawn / tiny sun

### Movement / pressure / MC
13. Slow elites via `uniqueSpeed`  
14. Match affix `zombieSpeedMultiplier` (**READY** F35)  
15. Mind-control chance / `spawnMindControlled`
16. Field AOE: freeze / doom / fireline / cherry (**READY** F37–F40)
17. Spawn/clear grave, ice block, paint water/grass/lava/dirt (**READY** F42–F47, F50)
18. OnKill → spawn grave / clear random grave (**READY** F48–F49)
19. Ice trail / Sledge road — **skip** until non-spawn path exists (F51 FAIL)

### Status depth (v1.5)
20. Poison stacks, freeze duration mods  
21. Extra ailment palette (jala/kelp…)

---

## 4. Expansion LIVE map (F24+)

| Scenario id | Checklist | Assert |
|---|---|---|
| `econ-sun-set` / `econ-sun-add` | F24–F25 | `board.economy` + UI sun |
| `econ-money-set` / `econ-money-add` | F26–F27 | money |
| `econ-points-set` | F28 | points |
| `zombie-speed-slow` / `zombie-speed-fast` | F29–F30 | **visual** walk |
| `onspawn-inspect` | F31 | spawn + `stat.applied` fields |
| `ondeath-inspect` | F32 | `plant.die` / `zombie.die` |
| `zombie-atk-bite` | F33 | `plant.damage` ≫ baseline |
| `plant-produce` | F34 | sunflower + produce interval |
| `board-config-speed` | F35 | E-ZS + spawn feel / config dump |
| `spawn-mc` | F36 | hypno visual + place |
| `env-freeze` / `env-doom` / `env-fireline` / `env-cherry` / `env-grave` | F37–F41 | `debug.board.action` / `grid.place` |
| `tile-grave` / `tile-grave-clear` / `tile-iceblock` | F42–F44 | grid spawn/clear |
| `tile-box-water` / `grass` / `lava` / `dirt` | F45–F47, F50 | `debug.box.set` |
| `onkill-grave` / `onkill-clear-grave` | F48–F49 | onKill arms |
| `tile-ice-road` | F51 **FAIL** | spawns Sledge — **NOT SHIPPED** |

See [`../../runbook/debug-live-checklist.md`](../../runbook/debug-live-checklist.md) §6 + §8 + §10.

---

## 5. Design implications

1. More Effects ≠ more hooks — reuse TakeDamage, die, spawn, status methods, Intent, economy writers.  
2. Separate axes: hit · interval · move speed · status · spawn · **economy**.  
3. Status = methods for VFX; floats = fallback.  
4. Economy Effects need hard caps (per kill / per match).  
5. Prefer this file + checklist over stale matrix rows.

## See also

- [`05-effect-system-prerequisites.md`](05-effect-system-prerequisites.md)  
- [`../arpg-effects/06-fusionrpg-mapping.md`](../arpg-effects/06-fusionrpg-mapping.md)  
- [`../../runbook/debug-live-checklist.md`](../../runbook/debug-live-checklist.md)  
- [`../cheat-menu-coverage.md`](../cheat-menu-coverage.md)
