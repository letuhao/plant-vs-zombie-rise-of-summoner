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

**Superseded by the E39 sweep below (2026-09-04) — that earlier note was against the 3.8.1 interop and
explicitly marked UNVERIFIED; this is the real sweep, against 3.9.**

### Plant-side status — E39 assembly sweep (2026-09-04)

`spec-plant-side-status.md` §2c names this module's own first task: which of the 8 `UnityCc` statuses
(`butter`, `freeze`, `cold`, `poison`, `hypno`, `ember`, `jala`, `kelp`) have a real plant-side write
surface was UNVERIFIED — the entry above was recorded against 3.8.1 and flagged as such. This is the
sweep against the shipped 3.9 interop.

**Tool:** `ilspycmd` v11.0.0.9375 (`C:\Users\NeneScarlet\.dotnet\tools\ilspycmd.exe`), `-t Plant`.

**Source:** live game install, generated interop —
`H:\Games\PVZ-Fusion-3.9_MelonLoader\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll`.

**Result — 2 of 8 have any candidate member on `Plant`, 6 have none:**

| Status | Plant-side hit | Shape | Verdict |
|---|---|---|---|
| `butter` | `public unsafe int butterP` | real IL2CPP field, getter/setter pair over a native offset | **CONFIRMED — wired.** No competing name anywhere on the class; the only butter-shaped member `Plant` has. Its exact unit (frame count? stack level? bare flag?) is unverifiable from the interop — IL2CPP compiles the real consumer to native code this assembly never exposes — so `ApplyStatusToPlant` (`DebugActions.cs`) writes the atom's own `level` param, not an invented conversion of `duration` |
| `jala` | `public unsafe virtual void InfluenceByJalapeno()` | real IL2CPP method, callable, no args | **DOWNGRADED to refused**, after this module's own required follow-up read (§2c: a sweep hit is a candidate, not a guarantee). The method sits beside `UpgradeEvent`/`InfluenceByIceShroom`/`UseItem(BucketType, Bucket)` in declaration order — an item-use/upgrade-reaction group, not a CC-apply group — and, like every IL2CPP method, its body is native code the interop wrapper never exposes. There is no static way to confirm it sets the same "on fire" state `Zombie.SetJalaed()` does rather than something unrelated (an upgrade catalyst, a UI hook). Calling it unverified is the exact E17 failure mode this module exists to stop shipping, so it refuses by name (`status-side-unsupported`) instead of wiring an unconfirmed method |
| `freeze` | none | — | **Refuses.** Zero hits, plus a broadened grep (freeze/cold/poison/slow/speed/mindcontrol/charm) found nothing beyond unrelated speed-modifier infrastructure (`moveSpeed`, `AddSpeed`) |
| `cold` | none | — | **Refuses.** Same sweep, zero hits |
| `poison` | none | — | **Refuses.** Same sweep, zero hits |
| `hypno` | none | — | **Refuses.** Same sweep, zero hits |
| `ember` | none | — | **Refuses.** Same sweep, zero hits |
| `kelp` | none | — | **Refuses.** `tanglekelpPlant` exists but is a type-identity bool ("is this plant the tangle-kelp species"), not a kelp-status-applied flag — a false-positive name match, not a real candidate |

**Consequence:** `InjectorEffectActionSink.ExecApplyStatus`/`ExecClearStatus` wire exactly `butter` for a
plant target; the other seven (`freeze`, `cold`, `poison`, `hypno`, `ember`, `jala`, `kelp`) return
`false` with an emit `reason: status-side-unsupported` when aimed at a plant — never a silent no-op, per
`spec-plant-side-status.md` §3's "do not fake a missing plant method with a float write" rule.

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

## `BulletMoveWay` assembly sweep (E37, 2026-09-04)

An earlier `spec-projectile-control.md` draft guessed a 5-member `right|left|up|down|track` set "from
the shape the names suggest, not from the game" — caught and corrected before it shipped (see that
spec's own `⛔ CORRECTED` note). This is the real sweep.

**Tool:** `ilspycmd` v11.0.0.9375 (`C:\Users\NeneScarlet\.dotnet\tools\ilspycmd.exe`), `-t BulletMoveWay <dll>`.

**Sources, all three agree byte-for-byte:**

| Source | Path |
|---|---|
| BepInEx Il2Cpp interop (study reference) | `study\Magnetar-Client\Magnetar Client\References\Bepinex\Il2Cpp\Assembly-CSharp.dll` |
| MelonLoader Il2Cpp interop (study reference) | `study\Magnetar-Client\Magnetar Client\References\Melonloader\Il2Cpp\Assembly-CSharp.dll` |
| Live game install, generated interop | `H:\Games\PVZ-Fusion-3.9_MelonLoader\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll` |

**Full, authoritative, 18-member enum (declaration order):**

```
MoveRight, Puff, MoveRight_threePeater, Track, Fly, Free, Left, Split_left, Throw, Cannon,
PeaNut, Stable, SmoothTrack, Sin, Spin, Jump, SuperGatling, None
```

This supersedes the mod-source grep lead (`Track`, `Stable`, `Throw`, `MoveRight` — all four confirmed
real, but only 4 of 18) and fully replaces the old `right|left|up|down|track` guess: `right`/`up`/`down`
were never real member names — the real horizontal members are `MoveRight`/`Left`, there is no
up/down pair, and the old `track` guess is really `Track`.

**Authored spelling: the exact enum member name, unrenamed** (`"MoveRight"`, `"Puff"`,
`"MoveRight_threePeater"`, `"Track"`, `"Fly"`, `"Free"`, `"Left"`, `"Split_left"`, `"Throw"`,
`"Cannon"`, `"PeaNut"`, `"Stable"`, `"SmoothTrack"`, `"Sin"`, `"Spin"`, `"Jump"`, `"SuperGatling"`,
`"None"`) — the same choice `GridItemTypeValues`/`BoxTypeValues` make for their own reflected IL2CPP
enums in `AtomKindRegistry.cs` (raw ordinals there because those are `Int` params; here a name because
`moveWay` is declared `String`, per §2a). Renaming to a friendlier camelCase would be an invented
spelling nothing sweeps confirm — the sink parses the authored string straight through
`Enum.Parse<BulletMoveWay>(moveWay, ignoreCase: false)`.

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
