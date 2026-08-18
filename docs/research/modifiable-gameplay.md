# Modifiable gameplay inventory (pre-RPG)

What **can** change on Plants vs. Zombies Fusion **3.8.1**, before any RPG formulas, loot, or XP design.

Evidence: local interop dump ([stat-fields.md](stat-fields.md), [game-types-381.md](game-types-381.md)), FusionRpg injector ([GameHooks.cs](../../src/FusionRpg.Injector/GameHooks.cs), [GameDumps.cs](../../src/FusionRpg.Injector/GameDumps.cs)), live capture (including external 100× HP), MIT Simple Spawner ([simple-spawner.md](simple-spawner.md)), and **capability labels** from third-party cheat clients studied locally (no source copied; do not name them in product README).

## Legend

| Status | Meaning |
|---|---|
| **WRITE-proven** | FusionRpg or live capture proved writing this affects / reflects gameplay |
| **WRITE-claimed** | Cheat tools expose it; FusionRpg has not verified |
| **CAPTURE-only** | We read and emit; we do not mutate |
| **DOC** | Exists on 3.8.1 dump; unused by injector |
| **OUT** | Banned for FusionRpg product (injector must-not / hot-path / cheat class) |

Combat SSOT remains **`spawn_stats` dump JSON**, not `types.hp_base`. External tools may rewrite HP without touching `Board.config`.

---

## 1. Plant combat / growth

| Knob | Field / API | Status | Notes |
|---|---|---|---|
| HP current / max | `thePlantHealth`, `thePlantMaxHealth` | **WRITE-proven** | FusionRpg `ApplyPlant` percent+flat once per `IntPtr` |
| ATK | `attackDamage` | **WRITE-proven** (write); combat path open | We write it; whether hits use this vs `Bullet.Damage` is unproven |
| DEF | `Plant.TakeDamage` `ref` damage | **WRITE-proven** | No plant defense field; `StatMath.ScaleIncoming` |
| DEF (alt path) | `RealTakeDamage` | **CAPTURE-only** | Logged; **not** DEF-scaled today |
| Shield | `theShieldHealth` | **CAPTURE-only** | Not scaled |
| Attack speed | `thePlantAttackInterval`, `thePlantAttackCountDown`, `attackSpeedAdder` | **CAPTURE-only** / **WRITE-claimed** | Interval in dump; cheat clients claim write |
| Produce speed | `thePlantProduceInterval`, `thePlantProduceCountDown` | **DOC** / **WRITE-claimed** | Not in plant dump today |
| Move / anim | `thePlantSpeed`, `moveSpeed` | **DOC** / **WRITE-claimed** | Rarely needed for RPG plants |
| Level / shoot | `theLevel`, `shootingLevel`, `LimDamage` | **CAPTURE-only** | Dump only |
| Grid | `thePlantColumn`, `thePlantRow` | **CAPTURE-only** | Place events |
| Game buff APIs | `ModifyHealth`, `ModifyDamage` | **DOC** | Untested vs raw field writes |
| Crush | `Plant.Crashed` | **CAPTURE-only** | `plant.crash` |

---

## 2. Zombie combat / movement

| Knob | Field / API | Status | Notes |
|---|---|---|---|
| Body HP | `theHealth`, `theMaxHealth` | **WRITE-proven** | Our scale; external 100× seen as 270 → 27000 via reinforce / set-health |
| Armor 1 / 2 | `theFirstArmor*`, `theSecondArmor*` | **WRITE-proven** | Scaled with HP% when &gt; 0 |
| Total HP (get) | `CurrentAllHealth`, `TotalAllHealth` | **CAPTURE-only** | Computed |
| ATK | `theAttackDamage` | **WRITE-proven** | `Math.Max(1, …)` then scale |
| DEF (incoming) | `Zombie.TakeDamage` `ref theDamage` | **WRITE-proven** | Same DEF formula as plants |
| DEF (alt paths) | `BodyTakeDamage`, `ApplyDamage` | **CAPTURE-only** | No DEF scale today |
| Armor float | `theArmor` | **CAPTURE-only** | Dump; write untested |
| Dmg taken mult | `takeDmgMultiplier` | **CAPTURE-only** | Dump |
| Dmg dealt mult | `DamageMultiplier` | **CAPTURE-only** | Get-only in dump |
| Move speed | `uniqueSpeed`, `theSpeed`, `theOriginSpeed` | **CAPTURE-only** / **WRITE-claimed** | Cheat clients claim `uniqueSpeed` write |
| Status slows | `freezeSpeed`, `coldSpeed`, `butterSpeed`, … | **DOC** | Not RPG base stats |
| Mind control | `isMindControlled`, `SetMindControl` | **CAPTURE-only** | |
| Recapture | `ReinforceZombie`, `Lawnf.SetZombieHealth`, `Board.SetHealthInTravel` | **CAPTURE-only** | Append `entity.stats` / spawn_stats |
| Init order | `InitHealth` then `Start` | **WRITE-proven** timing | Once-per-`IntPtr`; first dump may be pre-buff |

---

## 3. Bullets / projectiles

| Knob | Field / API | Status | Notes |
|---|---|---|---|
| Damage | `Bullet.Damage` | **CAPTURE-only** | `bullet.init`; writing unproven |
| Place | `CreateBullet.SetBullet` | **CAPTURE-only** | Noisy |
| Homing / type swap / keep shooting | (cheat modules) | **WRITE-claimed** / **OUT** | Not FusionRpg product for v1 RPG base stats |

Open probe: does changing `Plant.attackDamage` change bullet hits, or only writing `Bullet.Damage`?

---

## 4. Spawn / factories / waves

| Knob | Field / API | Status | Notes |
|---|---|---|---|
| Place plant | `CreatePlant.SetPlant` | **CAPTURE-only** (API is writable) | `plant.place`; counts `plants_planted` |
| Mix / unique / attributes | `MixEvent`, `UniqueEvent`, `SetPlantAttributes` | **CAPTURE-only** (+ scale on attributes if first apply) | |
| Place zombie | `CreateZombie.SetZombie`, `SetZombieWithMindControl` | **CAPTURE-only** (API writable) | Simple Spawner uses these |
| Wave summon | `BoardSpawner.SummonZombies` | **CAPTURE-only** | `wave.spawn` |
| Huge wave | `Board.HugeWaveEvent` | **CAPTURE-only** | |
| Level zombie list | `InitZombieList.InitZombie` | **CAPTURE-only** | `catalog.zombies` |
| Almanac pick | `Almanac*Menu.SelectCard` | **CAPTURE-only** | Spawner pattern |
| Board multipliers | `Board.config` — see below | **CAPTURE-only** | Writing config untested |
| Wave timer / spawn-rate hacks | cheat wave modules | **WRITE-claimed** / **OUT** for casual write | Prefer careful `Board` fields later if needed |

### `Board.config` (`GameLevel.BoardConfig`) — captured as `board.modifiers`

| Key | Status | Likely RPG role |
|---|---|---|
| `zombieHealthMultiplier` | **CAPTURE-only** | Match-wide zombie HP pressure |
| `zombieDamageMultiplier` | **CAPTURE-only** | Match-wide zombie ATK |
| `zombieSpeedMultiplier` | **CAPTURE-only** | Match-wide move |
| `zombieCountMultiplier` | **CAPTURE-only** | Spawn pressure |
| `zombieStartAmmor` | **CAPTURE-only** | Game typo; starting armor |
| `plantModifyMin` / `plantModifyMax` | **CAPTURE-only** | Game random plant modify band |
| `zombieModifyMin` / `zombieModifyMax` | **CAPTURE-only** | Game random zombie modify band |
| `waveInterval` | **CAPTURE-only** | Time between waves |
| `conveyInterval` | **CAPTURE-only** | Conveyor timing |

External HP tools often leave these at `1` while `spawn_stats` shows ×100.

---

## 5. Economy / board live

| Knob | Field / API | Status | Notes |
|---|---|---|---|
| Sun | `Board.theSun`, `UseSun` / `GetSun` | **CAPTURE-only** / write **OUT** | Product must not write sun |
| Money | `Board.theMoney`, `UseMoney` / `GetMoney` | **CAPTURE-only** / write **OUT** | |
| Points | `Board.thePoints`, `GetPoint` | **CAPTURE-only** | |
| Wave | `theWave`, `theMaxWave`, `timeUntilNextWave` | **CAPTURE-only** | Prefer live Board over stale `boardStatistics.currentWave` mid-match |
| Wave HP pools | `zombieCurrentWaveHealth`, `zombieSpawnHealth`, `zombieTotalHealth` | **CAPTURE-only** | |
| Caps | `maxSun`, `maxMoney` | **CAPTURE-only** | |
| Counts | `plantedCount`, `theCurrentPlantCount`, `theTotalNumOfZombie` | **CAPTURE-only** | |
| End stats | `BoardStatistics.*` | **CAPTURE-only** | Snapshot / GameOver |
| Mowers | `CreateMower` / `StartMove` / `Die` | **CAPTURE-only** | |
| Unlimited sun/money/points | cheat economy modules | **WRITE-claimed** / **OUT** | |

---

## 6. Travel / mix / meta (observe first)

| Knob | Status | Notes |
|---|---|---|
| Fusion recipes `PlantMixTreeManager.ChildToParents` | **CAPTURE-only** | Catalog; not combat math |
| Travel buffs / picks / reinforce plant | **CAPTURE-only** | Later “loot/buff events,” not base entity stats |
| Cards, shovel, glove, fertilize, hammer, wheel | **CAPTURE-only** | Player actions |
| Pets / grid / present / prizes | **CAPTURE-only** | Meta / side systems |
| Card CD / plant-anywhere / column plant | **WRITE-claimed** / **OUT** | Cheat QoL, not RPG base stats |

---

## 7. Explicit OUT (FusionRpg must not)

From [injector/spec.md](../injector/spec.md) and [game-types-381.md](game-types-381.md) tier D:

- GodMode / die-block / invuln / NoLose
- Writing sun / money (cheat economy)
- `Time.timeScale` freezes
- Harmony on `GameAPP.Start`, `Update`, `OnTriggerStay2D`, EventNodes, particles
- `Board.OnPlantDie` / `OnPlantCreate` (load-time trampoline AV with other plant mods)
- `CreatePlant.CheckMix` / `MixData.TryGetMix` (aim spam)
- Copying third-party plugin source into this AGPL tree
- SQLite or player ids inside the injector

---

## 8. FusionRpg writers today (summary)

| Side | Writes | How |
|---|---|---|
| Plant | HP max/current, `attackDamage` | `ApplyPlant` once per ptr |
| Plant | Incoming damage | `TakeDamage` Prefix DEF |
| Zombie | Body HP, armor1/2, `theAttackDamage` | `ApplyZombie` once per ptr |
| Zombie | Incoming damage | `TakeDamage` Prefix DEF |
| Everything else in dumps | — | Capture / recapture only |

Config: global plant mod + global zombie mod (`hp` / `attack` / `defense` percent+flat). Not per-type yet.

---

## 9. Candidate RPG stats → game knobs

No formulas here — only mapping and readiness.

| Candidate RPG stat | Primary game knob(s) | Readiness |
|---|---|---|
| Plant HP | `thePlantHealth` / `thePlantMaxHealth` | **Ready to drive** |
| Plant ATK | `attackDamage` (+ maybe `Bullet.Damage`) | **Needs in-game proof** (which path hits) |
| Plant DEF | `TakeDamage` Prefix (optional: more HP) | **Ready to drive** (Prefix path) |
| Plant attack speed | `thePlantAttackInterval` (+ adder) | **Needs write proof** |
| Plant produce rate | `thePlantProduceInterval` | **Needs write proof**; economy-adjacent |
| Zombie HP | Body + armor fields | **Ready to drive** |
| Zombie ATK | `theAttackDamage` | **Ready to drive** (assume melee uses it; still watch dumps) |
| Zombie DEF | `TakeDamage` Prefix; maybe `theArmor` / `takeDmgMultiplier` | **Ready** Prefix; armor fields need proof |
| Zombie move speed | `uniqueSpeed` (fallback `theSpeed`) | **Needs write proof** |
| Match spawn pressure | `zombieCountMultiplier`, factories, `waveInterval` | **Capture ready**; write config / timers **needs proof** |
| Match zombie pressure (HP/ATK/speed) | `Board.config` multipliers | **Capture ready**; writing **needs proof** |
| Sun / money as RPG currency | Board economy fields | **Out of product scope** to write; capture OK for quests later |
| Fusion / travel buffs | Recipes + travel events | **Observe first** — not base combat stats |
| God mode / time scale / plant-anywhere | Cheat modules | **Out of product scope** |

### Follow-up probes (not this doc’s job)

1. Live match: change only `attackDamage` vs only `Bullet.Damage` — which changes DPS?
2. Write `thePlantAttackInterval` / zombie `uniqueSpeed` — does gameplay change and stick past other mods?
3. Write `Board.config` multipliers mid-match or at `Board.Awake` — do spawns respect them?

---

## Related docs

- [stat-fields.md](stat-fields.md) — field list
- [game-types-381.md](game-types-381.md) — dump keys + A/B/C/D + signatures
- [events-lifecycle.md](events-lifecycle.md) — spawn/die order
- [harmony-hook-map.md](harmony-hook-map.md) — patches in use
- [open-questions.md](open-questions.md) — runtime risks
- [simple-spawner.md](simple-spawner.md) — factory spawn pattern
- [effect-runtime/00-index.md](effect-runtime/00-index.md) — Effect-oriented CAPTURE/WRITE matrix (HitZombie, status, Intent spawn)
