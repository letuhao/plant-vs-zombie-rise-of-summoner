# Hit pipeline candidates

Dump + local mod evidence for on-hit hooks. Not an ADR.

> **W0-D supersedes “HitZombie primary” below.** Product FT* on-hit SSOT = enriched **TakeDamage** (Bullet cast) + **`Zombie.AttackPlant`** melee. Base `Bullet.Hit*` Harmony stays off. The recommended-order table and “FusionRpg ignores damageFrom” notes are **historical** — `damageFrom` is stamped; adapter maps `source=takeDamage` / `attackPlant`. See [effect-runtime.md](../../architecture/effect-runtime.md) § FT* on-hit SSOT.

Evidence roots:

- Cecil: [`../../_cecil_dump/out.txt`](../../_cecil_dump/out.txt) (Bullet ~L1125–1168, TakeDamage signatures)
- Injector: [`GameHooks.cs`](../../../src/FusionRpg.Injector/GameHooks.cs), [`GameCaptureHooks.cs`](../../../src/FusionRpg.Injector/GameCaptureHooks.cs)
- Local decomp mods under workspace `_hp_decomp/` (HitZombie Prefixes)

## Recommended product hook order

1. **`Bullet.HitZombie(Zombie)`** / **`Bullet.HitPlant(Plant)`** — primary on-hit (virtual; many subtypes override)
2. **`Bullet.HitLand()`** — ground butter / splash
3. Enrich **`TakeDamage` Prefix** to also log `damageFrom`, `reportType` (already on signature; FusionRpg ignores them today)
4. **Do not** patch `OnTriggerEnter2D` / `OnTriggerStay2D` / `Update` for Effects

## Candidate methods

| Method | Signature | Role | FusionRpg | Product? |
|---|---|---|---|---|
| `Bullet.HitZombie` | `void HitZombie(Zombie zombie)` | Projectile → zombie | Not patched | **Yes — primary** |
| `Bullet.HitPlant` | `void HitPlant(Plant plant)` | Projectile → plant | Not patched | **Yes** |
| `Bullet.HitLand` | `void HitLand()` | Ground impact | Not patched | Later |
| `Bullet.CheckZombie` / `KeepHiting` | filters / multi-hit | Pre-hit | Not patched | Optional |
| `Bullet.FireZombie` | fire convert | Torch path | Not patched | Later |
| `Bullet.OnTriggerEnter2D` | collision entry | Calls into Hit* | Not patched | **OUT** |
| `Bullet.InitData` | init | Sets bullet state | Postfix + cheat write | Capture / outgoing probe |
| `CreateBullet.SetBullet` | factory | Spawn bullet | Capture | Capture |
| `Zombie.TakeDamage` | `(int, IDamageMaker, DamageType, PlantType, bool)` | Damage sink | Prefix DEF + log | Keep; enrich args |
| `Plant.TakeDamage` | same shape | Plant sink | Prefix DEF + log | Keep; enrich args |
| `Zombie.BodyTakeDamage` / `ApplyDamage` | alt sinks | After armor / typed | Capture + god only | No product DEF today |
| `Plant.RealTakeDamage` | alt | Bypass-ish | Capture + god | No product DEF |
| `Zombie.AttackPlant` / `EatEffect` | melee | Bite path | Deferred Prefix → `combat.hit` | **LIVE** plant-side hit |
| `BoardAction.CreateFreeze` / `SetDoom` / `CreateCherryExplode` | AOE factories | Non-bullet hits | Not patched | Later |

## What HitZombie gives Effects

From `__instance` (Bullet) + `zombie` argument, a Prefix can read without banned hot-paths:

| Field / arg | Use |
|---|---|
| `Bullet.Damage` | Outgoing damage number at hit |
| `Bullet.theBulletType` | Projectile identity |
| `Bullet.fromType` | `PlantType` shooter (cecil + mod usage) |
| `Bullet.theBulletRow` | Lane |
| `Zombie` arg | Target ptr / type |
| Optional: call into status | e.g. vanilla butter calls `Buttered` after TakeDamage |

**MOD-proven pattern** (butter-style hit body):

```text
zombie.TakeDamage(…, bullet.Damage, bullet.fromType);
zombie.Buttered(duration, sprite);
```

So on-hit Effects should prefer **HitZombie** (before or after original) over inventing collision math.

## TakeDamage identity gap

3.8.1 signature (cecil):

```text
TakeDamage(int theDamage, IDamageMaker damageFrom, DamageType theDamageType, PlantType reportType, bool fix)
```

FusionRpg Prefix today only binds `ref int theDamage` / `damage` and emits defender ptr + amount. **Code gap, not dump gap:** attacker / plant type are already parameters — enriching the Prefix is lower risk than new hooks for *partial* onHit identity. Full projectile identity still wants HitZombie (`theBulletType`, bullet ptr).

## Steal / adapt / avoid

| | Guidance |
|---|---|
| **Steal** | Base `Bullet.HitZombie` Prefix like local mods; use `fromType` + `Damage` |
| **Adapt** | Emit a single `combat.hit` event → Activity / future EffectBag |
| **Avoid** | Per-subtype-only patches as the only strategy; OnTrigger* product hooks |

## Open questions (updated 2026-08-16)

1. Does base `Bullet.HitZombie` run for all subtypes? **No** — ~**167** types define `HitZombie` (cecil); pea/cabbage/butter override and do not call base. Base-only Harmony is incomplete.
2. Does `IDamageMaker damageFrom` resolve to Bullet? **Often yes** for plant projectiles — FusionRpg now emits `combat.hit` from TakeDamage via `TryCast<Bullet>` (`source=takeDamage`). **LIVE F4 PASS.**
3. HitLand: deferred Harmony after `Board.Awake` (not chainloader) — applies without AV, but ~**134** `HitLand` overrides miss the base patch → **F23 FAIL** / `combat.hitland` NOT SHIPPED. HitZombie/HitPlant Harmony stay **off** by default (`Combat.EnableUnsafeHitPatches`).
4. Melee `AttackPlant` — required for zombie→plant hit identity? **Yes** — LIVE: plant TakeDamage `damageFrom` is the **plant itself** (`damageFromClass=WallNut`); deferred `Zombie.AttackPlant` emits `combat.hit` (`source=attackPlant`, `attackerKind=zombie`). Scenario `hit-capture-plant` / [`_checklist-hit-plant-live.json`](_checklist-hit-plant-live.json).

See checklist F4 / F23 and [`04-proof-results.md`](04-proof-results.md).
