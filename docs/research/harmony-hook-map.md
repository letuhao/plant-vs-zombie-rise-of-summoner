# Harmony hook map (3.8.1)

Candidate and in-use Harmony patches for FusionRpg on this pack. Prefix = before original (`return false` skips). Postfix = after.

## Board / level

| Class | Method | Patch | Why |
|---|---|---|---|
| `Board` | `Awake` | Postfix | Cache board, open run (`board.start`) |
| `Board` | `OnDestroy` | Postfix | Drop cached board if pointer matches |
| `Board` | `Die` | Postfix | Close run if needed |
| `BoardStatistics` | `GameOver` | Postfix | Win/lose (`match.result`) |
| `InGameUI` | `SetLevelName` | Postfix | Level title |
| `AlmanacPlantMenu` / `AlmanacZombieMenu` | `SelectCard` | Postfix | Almanac picks |

Level name can also be polled from `InGameUI.Instance` TMP (`LevelName1` / `2` / `3`).

## Plants

| Class | Method | Patch | Why |
|---|---|---|---|
| `CreatePlant` | `SetPlant` / `MixEvent` / … | Postfix | Place / mix / attributes |
| `Plant` | `Start` | Postfix | Spawn dump + scale |
| `Plant` | `Die` | Postfix | Death + reason |
| `Plant` | `TakeDamage` | Prefix | DEF scale + optional damage log |
| `Plant` | `RealTakeDamage` / `Crashed` | Prefix/Postfix | Extra combat capture |

Do **not** patch `Plant.Update` for capture (too hot). Do not ship GodMode / die-block.

## Zombies

| Class | Method | Patch | Why |
|---|---|---|---|
| `CreateZombie` | `SetZombie` / `SetZombieWithMindControl` | Postfix | Factory place |
| `Zombie` | `InitHealth` | Postfix | Early HP dump |
| `Zombie` | `Start` | Postfix | Spawn dump + scale |
| `Zombie` | `Die` / `DestoryZombie` | Prefix | Death (note game spelling **Destory**) |
| `Zombie` | `TakeDamage` / `BodyTakeDamage` / `ApplyDamage` | Prefix | DEF / combat |
| `Zombie` | `SetMindControl` | Postfix | Hypno |
| `TravelMgr` / `Lawnf` | `Reinforce*` / `SetZombieHealth` | Postfix | Recapture after HP rewrite |

## Bullets / mowers

| Class | Method | Patch | Why |
|---|---|---|---|
| `Bullet` | `InitData` | Postfix | Bullet count / optional dump |
| `CreateBullet` | `SetBullet` | Postfix | Place |
| `CreateMower` / `Mower` | `SetMower` / `StartMove` / `Die` | Postfix | Mower lifecycle |

### Effect candidates (not in FusionRpg yet)

Prefer these over banned hot-paths. Full matrix: [effect-runtime/02-hit-pipeline-candidates.md](effect-runtime/02-hit-pipeline-candidates.md).

| Class | Method | Why |
|---|---|---|
| `Bullet` | `HitZombie` / `HitPlant` / `HitLand` | **IN** — emit `combat.hit` / `combat.hitland` (debug session or logDamage) |
| `Zombie` | `Buttered` / `SetCold` / `SetFreeze` / `SetPoison` | **IN** via `/api/debug/apply-status` (not Harmony) |
| `TakeDamage` Prefix | enrich `damageFrom` / `reportType` | Partial — scenarioId stamp; full arg enrich later |

Debug runbook: [runbook/debug-pipeline.md](../runbook/debug-pipeline.md).

## Safety

Almost every patch should:

```text
if (Board == null) return;
if (__instance == null) return;
```

IL2CPP wrappers can be non-null while native is dead. Prefer `Pointer` (`IntPtr`) as identity keys. Null-check `__result` / `gameObject`.

## Must not patch

- `GameAPP.Start`
- `Update` / `OnTriggerStay2D` / EventNodes (spam / crash risk)
- `Bullet.OnTriggerEnter2D` as a product Effect hook (prefer `HitZombie` / `HitPlant`)
- `Board.OnPlantDie` / `OnPlantCreate` (load-time trampoline AV with other mods)

Effect-runtime index: [effect-runtime/00-index.md](effect-runtime/00-index.md).

## TakeDamage signature

3.8.1 `Plant.TakeDamage` is **five args**, not a simple `(int, int)`. See [game-types-381.md](game-types-381.md).
