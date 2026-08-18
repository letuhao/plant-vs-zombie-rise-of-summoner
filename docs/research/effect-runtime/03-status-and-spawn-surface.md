# Status and spawn surface

Writable / callable status and entity-factory APIs for in-run Effects. Not an ADR.

## Status apply (zombie)

### Methods (cecil + mod usage) — preferred

| Method | Signature | Role | FusionRpg |
|---|---|---|---|
| `SetFreeze` | `(float time, int theFreezeLevel)` | Hard freeze | **CAPTURE** `zombie.status` |
| `SetCold` | `(float time, int coldLevel, bool fromFreeze)` | Chill | **CAPTURE** `zombie.status` |
| `Buttered` | `(float time, bool sprite)` | Butter CC | **CAPTURE** `zombie.status` |
| `UnButtered` / `CheckButtered` | — | Clear / query | **CAPTURE** UnButtered → `zombie.status` on=false; CheckButtered not patched |
| `AddfreezeLevel` / `Unfreezing` | level stack | Freeze intensity | Not patched |
| `SetPoison` / `AddPoisonLevel` / `DamagedByPoison` | poison DoT | **CAPTURE** `SetPoison` → `zombie.status`; Add/Damaged not patched |
| `SetJalaed` / `SetEmbered` / … | fire/jala family | Not patched |
| `SetKelped` / `Garliced` / `SetPortaled` | other CC | Not patched |
| `Warm` / `KillDebuff` | clear debuffs | **CAPTURE** `zombie.status` on=false |
| `SetMindControl` | hypno | **CAPTURE** Postfix | Cheat hypno-all WRITE |

### Fields (speed multipliers) — cheat-only today

| Field | Writer | Status |
|---|---|---|
| `freezeSpeed` / `coldSpeed` / `butterSpeed` | `EntityStatWriter.WriteZombieExtras` via `Z-SLOW-*` | **WRITE-claimed** / **needs-probe** |
| `kelpSpeed` / `garlicSpeed` / `uniqueSpeed` | DOC / cheat speed | Mixed |
| `freezeLevel` / `poisonLevel` | DOC | Prefer methods |

**Guidance:** Effects should call **`Buttered` / `SetCold` / `SetFreeze` / `SetPoison`**, not only poke speed floats. Floats are fallback / visual knobs.

### Board AOE status

| API | Role |
|---|---|
| `BoardAction.CreateFreeze(Vector2 pos, float timer)` | AOE freeze factory (**MOD-proven** patched by other mods) |

### Plant-side status

Less complete in this pass. Plant has `butterP` and TakeDamage paths; freeze-plant helpers exist (`SetFreezedPlant` in dump). Treat plant CC as **DOC / later** unless a specific Effect needs it.

## Spawn factories (in-run)

| Factory | Signature (short) | Capture | Write path |
|---|---|---|---|
| `CreatePlant.SetPlant` | col, row, PlantType, … | `plant.place` | Cheat SP-PLANT; Intent plant side later |
| `CreateZombie.SetZombie` | row, type, x, mindControl? | `zombie.place` | **Intent `pvz.spawn.extra`**, cheat SP-* |
| `CreateZombie.SetZombieWithMindControl` | row, type, x, withEffect | place | Cheat SP-ZOMBIE-MC |
| `CreateBullet.SetBullet` | x, y, row, BulletType, moveWay, fromEnemy | `bullet.place` | No product Intent yet |
| `BoardSpawner.SummonZombies` | wave | `wave.spawn` | Cheat summon |
| `CreateMower.SetMower` | … | mower.place | — |
| `MiniPet.SetPet` / `GridItem.SetGridItem` / `Present.RandomPlant` | misc | capture | cheat |
| `BoardAction.SetDoom` / `CreateCherryExplode` / `CreateFireLine` | combat “spawn” FX | — | DOC / later Effect actions |

## Steal / adapt / avoid

| | Guidance |
|---|---|
| **Steal** | Call vanilla status methods; reuse Intent spawn for proc→spawn |
| **Adapt** | Future `pvz.status.apply` Intent wrapping Buttered/SetFreeze |
| **Avoid** | Per-frame Update re-applying slows; uncapped SetBullet spam from procs |

## Open questions

1. Do `Buttered` / `SetFreeze` respect immunities we must not bypass?
2. Caps for Intent spawn-from-proc (row, ICD, max extras per wave)?
3. Should ground butter (`HitLand`) be a separate Effect event from `onHit`?
