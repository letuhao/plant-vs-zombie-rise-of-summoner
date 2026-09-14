# PvZ Fusion engine surface — effects, damage, and the hook surface

**Status: research note. Tracking only — no proposals, no design, no authorization to build.**
Captured 2026-09-13 while spec'ing `lawn-combat-wire`, because the same questions kept coming up and
guessing at them cost real time twice. Filed for a **future program that extends the Game Injector**.

Nothing here is a commitment to hook, wrap, or reimplement any of it.

---

## 0. Evidence tier and method

**Tier A — shipped binary.** Read from
`H:\Games\PVZ-Fusion-3.9_MelonLoader\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll` with
Mono.Cecil and `MetadataLoadContext`. **No execution.** Counts marked *(computed)* were tallied from
metadata, not from a wiki.

Sibling prior art, already in-repo and not duplicated here:
[`../genre-mechanics/02-pvz2-chinese-and-fusion.md`](../genre-mechanics/02-pvz2-chinese-and-fusion.md)
covers the *design* reading of the same binary (content counts, rarity, buff drafting). **This file is
the engineering surface** — what an injector can see and hook.

### ⚠ The methodological finding that matters most

**`Il2CppAssemblies` are Il2CppInterop *proxies*. Every method body is a thunk into native
`GameAssembly.dll`:**

```
QingZombie.AttackPlants  →  calls IL2CPP.il2cpp_runtime_invoke
Gargantuar.GargantuarAttackUpdate  →  calls IL2CPP.il2cpp_runtime_invoke
```

So this assembly answers **shape** questions authoritatively — does a field exist, what is its type,
is a method virtual, how many overrides — and answers **behaviour** questions not at all. "Does
`AttackPlants()` loop into `AttackPlant(plant)`?" is not in managed IL and needs the live game.

Know which kind of question you have before reaching for a decompiler. This cost one investigation
already.

Scale: **2,631 types** *(computed)*; **88 direct `Zombie` subclasses** *(computed)*.

---

## 1. The effect system — the host game has its own

This is the "effect list". PvZ Fusion ships a complete status/effect layer of its own, entirely
separate from our RPG effect stack.

**`EffectType` (21)** *(computed)*:
`Cold, Jala, Freeze, Ember, Poison, Portal, Butter, Kelp, Immune, Fragile, IceDoomFreeze, Love,
Launch, Imitate, Fragile_plant, PortalBalloon, Curse, Recover, FireCover, CherryFastShoot,
LotusFastShoot`

**The contract** (`IEffect` / `BaseEffect` / `EffectManager`):

| Type | Members |
|---|---|
| `IEffect` | `OnUpdate`, `OnFixedUpdate`, `Main` |
| `BaseEffect` | `EffectType`, `OnStart`, `OnUpdate`, `OnFixedUpdate`, `OnRemove`, **`OnCover`**, `Value`, `first`, `totalDuration` |
| `EffectManager` | `SetEffect` (two overloads) |

`OnCover` is worth noting — the engine already has a concept of one effect *covering* another, i.e.
its own stacking/replacement rule. Any future bridge must not assume it owns that decision.

**Concrete effect classes** (one per `EffectType`, roughly): `ColdEffect`, `JalaEffect`,
`FreezeEffect`, `EmberEffect`, `PoisonEffect`, `PortalEffect`, `ButterEffect`, `KelpEffect`,
`ImmuneEffect`, `IceDoomFreezeEffect`, `LoveEffect`, `LaunchEffect`, `ImitateEffect`,
`PlantCurseEffect`, `PlantFireEffect`, `PlantFragileEffect`, `PlantRecoverEffect`,
`PlantPortalBalloonEffect`, `FastShootEffect`, plus the containers `PlantEffect`, `ZombieEffect`,
`FlashEffect`.

**Relevance, stated once and not designed here:** our own status vocabulary is host-injected and
closed, and our elemental reactions/status resolver are explicitly deferred
(`lawn-combat-wire-ideal.md` D4). A future injector-extension program has a choice between bridging to
this engine layer and staying parallel to it. **That choice is not made here.**

---

## 2. Damage surface

**`DamageType` (19)** *(computed)*:
`Normal, NormalAll, Ice, IceAll, Shieldless, IceShieldless, RealDamage, Explode, Squash, Carred,
Hammer, MaxDamage, CherryExplode, JackboxExplode, UltimateTallNutAll, DoomExplode, UltimateBamboo,
Crash, Curse`

**`DamageEffect` (9)**: `Default, SetCold, SetFreeze, SetJalaed, SetButtered, Warm, AddPoisonLevel,
AddFreezeLevel, SetPoison` — the engine's own "this hit also applies a status" channel.

**`DamageMode` (4)**: `Normal, Shieldless, All, Squash`

**Entry points:**

```csharp
Plant.TakeDamage(Int32 damage, IDamageMaker damageFrom, DamageType damageType, PlantType reportType, Boolean fix)
Plant.RealTakeDamage(Int32 damage)
Plant.GetDamage(Int32 damage)
Zombie.TakeDamage(Int32 theDamage, IDamageMaker damageFrom, DamageType theDamageType, PlantType reportType, Boolean fix)
Zombie.BodyTakeDamage(Int32 theDamage)
Zombie.ApplyDamage(DamageType theDamageType, Int32 dmg)
Zombie.GetDamage(Int64 theDamage, DamageType theDamageType, Boolean fix, PlantType fromType)
Gargantuar.BodyTakeDamage(Int32 theDamage)
```

Note `Zombie.GetDamage` takes **`Int64`** while `Plant.GetDamage` takes `Int32` — relevant to our own
`long`-for-magnitudes rule at the boundary.

**Damage modification the engine already owns:**

```csharp
Plant.ModifyDamage(PlantDamageAdder index, Single value, Boolean add, Nullable`1 max)
Plant.ModifyTempDamage(PlantDamageAdder index, Single value, Single during)
Plant.AdjustDamage()
Plant.damageAdder : Dictionary`2
```

**`PlantDamageAdder` (57)** *(computed)* is an indexed additive-modifier channel — the engine's own
equivalent of a stat-mod bag. Partial name list: `TravelNormalBuff, Connect, Level, PointEffect,
AbyssBuff, Abyss_loseHealth, Curse, NewTower, Leader_UltimateGatling, Camp_Land, Jigsaw, Shooting,
DeathChomper, ImitateWheat, XXSPot, UltimateHypnoPumpkin, ScaredyPot, Adv_4_1, AdvantureStartBuff,
UltimateSunMagnet, StarAdvanture_inGame, RandomMix_3, JalaCorn, EventNode, PortalBalloon,
UltimateIceShroom2, Update, Self, BloverPot, ThreePot`. **~22 of the 57 names did not resolve through
Cecil** (they read back as `SerializedConstant`); the count is reliable, the name list is not
exhaustive.

`Board.showBulletDamage` / `Board.damageReporter` exist — the engine has its own damage-number
display, which is worth knowing before we build a second one.

---

## 3. Status enums

**`PlantStatus` (44)** and **`ZombieStatus` (49)** *(computed)* — per-entity animation/behaviour state
(`Default, Dying, Pol_run, Pol_jump, Miner_digging, Flying, …`). These are engine state machines, not
RPG statuses; do not conflate them with our `StatusCatalog`.

---

## 4. Bullet surface

Fields, verified (this is what unblocked `lawn-hit-attribution`):

| Field | Type | Note |
|---|---|---|
| **`from`** | `Plant` | **the firing plant instance** — the injector reads only `fromType` today |
| **`from_zombie`** | `Zombie` | the firing zombie instance |
| `fromType` | `PlantType` | the type enum; the only one currently read |
| `shootByZombie` | `Boolean` | which side fired |
| `targetZombie` / `targetPlant` | `Zombie` / `Plant` | intended target |
| `targetProjectiles` | `List<AirProjectile>` | |
| `torchWood` | `Plant` | the pea-through-torchwood transform source |
| `Damage` / `_damage` | `Int32` | |
| `Team` | `Team` | `Player` or `AI` |
| `hitCount` / `maxHitCount` | `Int32` | **piercing budget** — how many victims one bullet may hit |
| `theBulletRow`, `shootingLevel`, `theStatus`, `hitFilters` | | |

**`BulletType` (244)**, **`ParticleType` (163)**, **`BulletMoveWay` (18)**, **`BulletHitFilter` (7)**
(`SameRowOnly, LifeOnly, TargetOnly, GroundFilter, FlyingFilter, UnderWater, UnderLand`),
**`BulletStatus` (11)** *(all computed)*.

`hitCount`/`maxHitCount` is the engine's own multi-victim model and is the natural cross-check for our
swing-id dedupe.

---

## 5. Attack / hook surface

What the injector hooks today, and what it misses:

```
Zombie.AttackPlant(Plant)        virtual=True  newslot=True    <- HOOKED
QingZombie.AttackPlant(Plant)    virtual=True  newslot=False   <- override, NOT hooked
QingZombie.AttackPlants()        virtual=False                 <- multi-target, NOT hooked
EternalZombie_a.AttackPlants()   virtual=False                 <- multi-target, NOT hooked
Shulkflower.AttackEffect(List<…>)                              <- plant-side AoE, NOT hooked
WaterShulk.AttackEffect(List<…>) 
Gargantuar.GargantuarAttackUpdate()                            <- own attack path
Plant.AttackLandZombie(Zombie) / Plant.OnEat(Zombie)
Zombie.AttackUpdate() / MiniPet, BlackHole, Lunar, LunarLine, CherryBlover, CherryMagnet,
  Shulkflower, ThronsShulk, UltimateDolphin, ZombieJackson, ZombieJackson.AttackUpdate()
```

Harmony patches a *specific* method and an override carries its own body, so patching the base
captures none of the rest. Tracked as a live gap in
[`../../architecture/lawn-combat-wire/spec-lawn-hit-attribution.md`](../../architecture/lawn-combat-wire/spec-lawn-hit-attribution.md).

---

## 6. Buff families (counts only)

*(computed)*, matching the counts already recorded in `genre-mechanics/02`:

| Enum | Count |
|---|---|
| `AdvBuff` | 177 |
| `UltiBuff` | 56 |
| `PlayerWeaponBuff` | 45 |
| `InvestBuff` | 42 |
| `EveBuff` | 32 |
| `PlayerBuff` | 29 |
| `FruitBuffType` | 25 |
| `ZombieBuff` / `EveZombieBuff` | 10 / 10 |
| `TowerBuff` | 9 |
| `BuffType` | 5 (`UnlockPlant, AdvancedBuff, UltimateBuff, Debuff, InvestmentBuff`) |
| `PetBuff` | 1 |

Most of these read back as `SerializedConstant` through Cecil — **counts are reliable, member names
are not** without a different reader.

---

## 7. What I could not find

Per this repo's research convention, the absences are part of the result.

1. **Any method body.** See §0 — the proxies thunk to native. Every behavioural question here is
   unanswered: whether `AttackPlants()` loops, what `OnCover` actually does, how `damageAdder` entries
   combine.
2. **Reliable member names for ~22 of 57 `PlantDamageAdder` entries** and for most of the large buff
   enums. Counts resolved; names did not. A different metadata reader, or the live game, would close
   this.
3. **Whether `EffectManager.SetEffect` is reachable/safe from a Harmony context** — signature only, no
   call-site analysis.
4. **Any interface list.** `t.IsInterface` returned nothing on this assembly even though `IEffect` and
   `IDamageMaker` clearly exist — the IL2CPP proxy shape defeats that query. Interfaces were found by
   name, so this list is **not exhaustive**.
5. **Version drift.** Everything here is 3.9 on this machine. The 3.8.1 BepInEx install may differ and
   was not re-read.

---

## 8. What this is for

A future **injector-extension program**. Open questions it would inherit, recorded so they are not
rediscovered:

- Bridge to the engine's `EffectType`/`EffectManager` layer, or stay parallel to it?
- Adopt `PlantDamageAdder` as a sanctioned modifier channel, or keep all RPG modification in our own
  stack and write only final values?
- Hook the uncaptured attack methods (§5), or accept those creatures as RPG-inert?
- Use `hitCount`/`maxHitCount` as the swing/pierce model instead of our own dedupe?

**None of these is decided. This file exists so the next program starts from evidence.**
