# Spec: `lawn-hit-attribution`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** — (leaf, parallelisable)

---

## Objective

A lawn hit record currently identifies the **bullet** as the attacker, not the creature that fired it.
Everything downstream inherits that error:

- `EventDrainHost.cs:61` records `actorPtr: bullet.Pointer`; `EventDrain.cs:453` passes it through as
  `ActorPtr`; `OverlayCombatMath.cs:50-51` resolves the attacker snapshot from exactly that ptr →
  `InjectorCombatBridge.ResolveActor(bulletPtr)` finds no ActorHub baseline and falls back to the
  literal stub `{Hp=100,MaxHp=100,Atk=10}` with a Neutral element (`InjectorCombatBridge.cs:57-59`).
  **The shooting plant's own `combat.power.*` is never read.**
- `EffectProcAndOwner.MatchesEvent` (`:106-116`) matches `entity:` owner keys against `ev.ActorPtr` /
  `ev.TargetPtr` — the bullet and the victim — so **an `entity:{ptr}` grant can never fire on a
  projectile hit**, which would make `basic-attack-grant` unimplementable for every ranged plant.

This looked like new machinery. It is one field: the host game's own `Bullet` already carries the
firing instance, verified by reading `Assembly-CSharp.dll` metadata directly.

```
Il2Cpp.Plant      from            <- the firing plant INSTANCE
Il2Cpp.Zombie     from_zombie     <- the firing zombie instance
Il2Cpp.PlantType  fromType        <- the type enum; the ONLY one the injector reads today
System.Boolean    shootByZombie   <- which side fired
```

Today the injector reads only `fromType` (`EventDrainHost.cs:57`, `GameHooks.cs:972`, and
`GrantedBulletModifyAtoms.cs:37` which keys `EffectOwnerKeys.PlantType` — **type-scoped, not
instance-scoped**).

Success: a lawn hit record carries the **shooter's** ptr as the attacker and the **bullet's** ptr as
the swing identity, so the attacker resolves through ActorHub and one swing produces exactly one
action trigger.

## Tech stack

`FusionRpg.Injector` (record sites, Unity/IL2CPP) + `FusionRpg.Core` (`GameEventRec` / `EventDrain`
DTO shape). The Core half stays Unity-free.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EventDrain"
.\scripts\guard-secondary-no-unity.ps1
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/Effects/EventDrainHost.cs` | Read `bullet.from` / `from_zombie`; record shooter ptr + swing id |
| `src/FusionRpg.Core/Events/EventDrain.cs` | Carry the new swing id through to the DTO |
| `src/FusionRpg.Core/Events/GameEventRec.cs` | The record gains a swing-id field |

## The record shape — two identities, not one

**Owner decision, 2026-09-13: one attack is one action trigger; triggering it multiple times is a
defect.** A piercing pea hitting five zombies is *one swing*, five damage applications. That needs
both identities on the record:

| Field | Was | Becomes | Why |
|---|---|---|---|
| attacker | `bullet.Pointer` | `bullet.from.Pointer`, or `from_zombie.Pointer` when `shootByZombie` | ActorHub resolve + `entity:{ptr}` grant matching |
| **swing id** (projectile) | — | `bullet.Pointer` | Dedupes the action trigger — `lawn-hit-entry` fires once per swing |
| **swing id** (melee) | — | `(attackerPtr, frame)` | Same, for multi-target bites |
| target | victim ptr | unchanged | Each victim keeps its own defence/element matchup |

**Melee attribution is already correct** — `TryRecordMeleeDealt` records the true `attackerPtr`. Do not
change it.

**But melee DOES need a swing id — corrected 2026-09-13.** An earlier draft of this spec claimed "one
bite = one victim". That is false: the host game has multi-target melee, verified by reading
`Assembly-CSharp.dll` metadata —

| Method | Shape |
|---|---|
| `Zombie.AttackPlant(Plant)` | single target — the only thing the injector hooks (`GameHooks.cs:1026`) |
| `QingZombie.AttackPlants()` | **plural**, no args |
| `EternalZombie_a.AttackPlants()` | **plural**, no args |
| `Shulkflower.AttackEffect(List<…>)` | plant-side, takes a collection |
| `WaterShulk.AttackEffect(List<…>)` | plant-side, takes a collection |

`grep` for any of these four types in `src/FusionRpg.Injector/` returns **zero hits** — the injector
does not know they exist. And `AttackPlants()` is a *separate method*, not an override of
`AttackPlant(Plant)`, so the existing Harmony patch catches it **only if** it loops into
`AttackPlant(plant)` per victim. That is unresolved from metadata alone (no IL bodies) and is a
**build-time verification item**, not an assumption.

**Melee swing id = `(attackerPtr, frame)`.** One attacker's bites within one frame are one swing.
This is correct whichever way the loop question resolves, and it reuses machinery that already exists:
`_meleePairsByTarget` / `_meleePairsFrame` (`EventDrainHost.cs:26-27,85-90`) already scopes melee
bookkeeping per frame and clears on frame advance. Do not invent a second frame-scoped structure.

## Code style

`bullet.from` may be null (a bullet whose shooter already died, or an engine-spawned projectile with
no owner). **A null shooter is ordinary, not an error**: skip the RPG contribution and let the vanilla
hit stand — never throw, never substitute the bullet ptr as a fallback, because that silently
reintroduces the stub-resolve bug this module exists to remove.

Read the field once per record and pass it down; do not re-read IL2CPP fields per consumer.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | The DTO carries attacker and swing id independently; a record with a swing id round-trips |
| Core unit | Two records sharing one swing id are recognisable as one swing (the dedupe key `lawn-hit-entry` will use) |
| Injector (where buildable) | `bullet.from` is read for a plant shot; `from_zombie` when `shootByZombie` |
| Injector | Null shooter → no RPG contribution, no throw |
| Live | Covered by `lawn-combat-live-proof`: assert the recorded attacker is the firing plant's ptr, **not** the bullet's — the single most direct falsifier for this module |

## Boundaries

- **Always:** prefer the real shooter; treat a null shooter as "no RPG contribution".
- **Always:** keep melee's existing attacker — it was never wrong.
- **Ask first:** using `bullet.fromType` for anything new. Type-scoped attribution is what
  `GrantedBulletModifyAtoms` already does and is a *different* capability (per-type effects); this
  module exists precisely because per-instance identity was missing.
- **Never:** fall back to `bullet.Pointer` as the attacker; re-read IL2CPP fields per consumer; change
  what `fromType` means for existing consumers.

## Success criteria

- [ ] A projectile hit records the firing creature's ptr as attacker, proven live.
- [ ] The same hit records the bullet ptr as swing id, and N victims of one bullet share it.
- [ ] `InjectorCombatBridge.ResolveActor` returns a real ActorHub-composed snapshot for that attacker
      — **not** the `{Hp=100,MaxHp=100,Atk=10}` stub.
- [ ] An `entity:{ptr}` grant bound to the firing plant matches on a projectile hit (the precondition
      `basic-attack-grant` depends on).
- [ ] Melee **attribution** unchanged; melee **gains** a `(attackerPtr, frame)` swing id.
- [ ] A null shooter produces no RPG contribution and no exception.
- [ ] **Multi-target melee resolved, not assumed.** Spawn a `QingZombie` / `EternalZombie_a` against
      several plants and record which of these is true: (a) the existing `Zombie.AttackPlant` patch
      fires once per victim — the swing id handles it; or (b) it does not fire at all — those attacks
      are currently invisible to the RPG layer and that is a named capture gap to track, since
      `Plant.TakeDamage` is deliberately not used for melee `combat.hit` to avoid double-counting
      (`GameHooks.cs:982`). Record the answer in this spec rather than leaving it open.
