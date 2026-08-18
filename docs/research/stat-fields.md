# Stat fields (3.8.1 dump)

All field names below exist as properties on the 3.8.1 interop (get/set) unless noted.

FusionRpg v1 scales with **percent + flat** on HP/ATK and DEF via incoming damage Prefix. Combat SSOT is the live dump JSON in `spawn_stats`, not these catalog notes.

## Plant

| Property | Type | Notes |
|---|---|---|
| `thePlantType` | `PlantType` | Enum id |
| `thePlantHealth` | `int` | Current HP |
| `thePlantMaxHealth` | `int` | Max HP |
| `theShieldHealth` | `int` | Extra bar |
| `attackDamage` | `int` | Plant ATK field (bullet path still TBD) |
| `damageAdder` | dictionary | Game additive damage mods |
| `healthAdder` | dictionary | Game additive HP mods |
| `thePlantAttackInterval` | `float` | Lower = faster shots |
| `thePlantAttackCountDown` | `float` | |
| `thePlantProduceInterval` | `float` | Sun / produce |
| `thePlantProduceCountDown` | `float` | |
| `thePlantSpeed` | `float` | Anim / movement related |
| `attackSpeedAdder` | `float` | |
| `moveSpeed` | `float` | |
| `thePlantColumn` / `thePlantRow` | `int` | Grid |
| `ModifyDamage(...)` | method | Game buff API |
| `ModifyHealth(...)` | method | Game buff API |

No dedicated plant “defense” field. Incoming hit size is the `damage` argument on `TakeDamage` / `RealTakeDamage` / `GetDamage`.

## Zombie

| Property | Type | Notes |
|---|---|---|
| `theZombieType` | `ZombieType` | |
| `theHealth` / `theMaxHealth` | `int` | Body HP |
| `theFirstArmorHealth` / `theFirstArmorMaxHealth` | `int` | Armor 1 |
| `theSecondArmorHealth` / `theSecondArmorMaxHealth` | `int` | Armor 2 |
| `CurrentAllHealth` / `TotalAllHealth` | `int` (get) | Body + armor |
| `theAttackDamage` | `int` | Zombie ATK |
| `theArmor` | `float` | |
| `theSpeed` / `theOriginSpeed` | `float` | |
| `uniqueSpeed` | `float` | Often the practical speed scale knob |
| `freezeSpeed` / `coldSpeed` / `butterSpeed` / … | `float` | Status slowdowns |
| `isMindControlled` | `bool` | |
| `InitHealth()` | method | Early HP assignment |
| `UpdateHealthText()` | method | Call after HP writes if UI must match |
| `DamageMultiplier` | `float` (get) | Read-only in dump |

## Board-level numbers (not per-entity)

| Name | Where |
|---|---|
| `board.theSun` | `Board` |
| `board.theMoney` / `board.thePoints` | `Board` |
| `board.showPlantHealth` | `int` |
| `board.showZombieHealth` | `bool` |
| `board.boardStatistics.*` | see [game-types-381.md](game-types-381.md) |
| `board.theWave` / `board.theMaxWave` | Live wave |
| `board.zombieCurrentWaveHealth` / `board.zombieSpawnHealth` / `board.zombieTotalHealth` | Wave HP pools |
| `board.config.*` | Match modifiers (may miss external tools) |

## Bullet damage

| Name | 3.8.1 |
|---|---|
| `Bullet.Damage` | property `int` |
| `Bullet._damage` | backing field |

Plant `attackDamage` vs bullet `Damage` may both matter; unknown which the game uses per plant.

## Percent vs flat

FusionRpg applies `new = max(1, round(base * percent) + flat)` for HP/ATK, and DEF via `newDamage = max(0, round(damage / defensePercent) - defenseFlat)`.

There is no dumped field named `defense`. Closest knobs:

- Shrink `damage` / `theDamage` in a `TakeDamage` Prefix (`ref int`).
- Or raise HP so effective tankiness goes up.
- Zombie `theArmor` / `ModifyArmor`.

## Enums used as ids

Prefer live enum dump + `Lawnf.GetName` on the Unity main thread for display names (not during SignalR connect). Skip `PlantType.Nothing` / `ZombieType.Nothing`.
