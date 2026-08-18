# Unique entity Effects (design reserved)

How FusionRpg treats **one** plant/zombie on the lawn as a unique specimen without registering a new game `typeId` (contrast: Plants+ / CustomizeLib new species).

Parent: [effect-system.md](effect-system.md), [effect-runtime.md](effect-runtime.md).  
Durable lifecycle SSOT: [unique-actor-runtime.md](unique-actor-runtime.md) (UniqueActor FSM).  
Live bind: [match-runtime.md](match-runtime.md) UniqueBindings facet.  
Control loops: [overlay-control-loops.md](overlay-control-loops.md) — equipment procs are **Hot** (Injector EffectBag); equip/loadout is **Cold**.  
FA1 apply scope (shipped): grant `ownerKey` is honored at **compose** (`StatModifier.ApplyOwnerKey` + `StatSystem.Resolve` filter) and **reapply** (`ReapplyLivingForOwner`). LIVE assert: `POST /api/debug/board-stats`.

**Shipped:** FA1 scope grammar (`match` / `plant:N` / `zombie:N` / `entity:{ptr}`); auto withdraw-on-die for `entity:{ptr}` grants; Hot rejects `instance:` (`StatApplyScope.Matches` false + `EffectBag.Grant` throw); UniqueActor tables + deploy; MatchRuntime UniqueBindings; `UniqueOwnerBinder` at Bound; ptr-only absolute loadout.  
**Not built yet:** Full gear shop polish (W12). Roster equip + specimen XP shipped W8.

---

## Product model

| Layer | Owns | Lifetime |
|---|---|---|
| **UniqueActor (Server / Data)** | Specimen: `instanceId`, level, equipment, personal mods, UniqueActor phase | Durable across runs |
| **Catalog / typeId** | Spawn template species (normal game type) | Durable species definition |
| **Run / lawn** | Unity `ptr` + MatchRuntime UniqueBinding | Dies / clears with the match entity |
| **Type RpgProgression** | Almanac XP `(player, kind, type_id)` | Orthogonal grain — not the specimen |

Death on the lawn is **run-local** for the Unity object. The **specimen** recovers to Roster via UniqueActor FSM (gear/level persist). The game does not delete the durable row.

---

## Two power layers

1. **Spawn loadout (absolutes)** — overlay HP/ATK on spawn / `SpawnEntity` → `EntityStatWriter` on **that** ptr once.  
   Caution: global Tab-B-style absolutes would leak to all living; spawn path must write the instance only.
2. **Passive / triggered Effects** — grant with `ownerKey=entity:{ptr}` (FA1 ModifyStat compose; FA2+ procs matched to that ptr).

### Hot — every combat.hit (no Server RTT)

```text
Unity hit → Capture → EffectBag.OnEvent
  → local chance + ICD
  → FA2 freeze / FA1 heal on ptr
  → async events observe only
```

See [overlay-control-loops.md](overlay-control-loops.md) for the Cold equip → Hot proc split. Server UniqueActor must not roll these procs.

---

## Owner key scopes

| `ownerKey` | Meaning | Status |
|---|---|---|
| `match` | Whole lawn / match-wide FA1 | Shipped |
| `plant:{typeId}` / `zombie:{typeId}` | All living of that type | Shipped |
| `entity:{ptr}` | One Unity instance (hex ptr) | Shipped — **lawn apply for uniques** |
| `instance:{guid}` | Durable specimen (Server/Data) | **Reserved** — binder translates to `entity:{ptr}` only while Bound |
| `player:{id}` | Stub → treat as match for apply today | Stub |

Matching (events) and FA1 apply share the shipped grammar. See `EffectOwnerKeys` / `StatApplyScope`.  
Do **not** put `instance:` into hot `StatSystem.Resolve` until a deploy binder exists ([unique-actor-runtime.md](unique-actor-runtime.md)).

---

## Run path (later product)

```text
UniqueActor Roster (Data) → deploy instanceId
  → Intent: typeId + instanceId + absolute loadout + grant templates
  → MatchRuntime: PendingSpawn → *.spawn → Bound (instanceId ↔ ptr)
  → Writer absolutes on ptr
  → Grant(effect templates, ownerKey=entity:{ptr})
  → play
  → die / board end
  → Emit plant.die / zombie.die (OnDeath while entity grants live)
  → ForgetEntity → Withdraw entity:{ptr} grants + baselines
  → UniqueBinding Cleared
  → UniqueActor Recovering → Roster (persist specimen; not only “punish catalog”)
```

**Invariant:** Emit die first (so entity-scoped OnDeath can fire), then withdraw `entity:{ptr}` grants inside `ForgetEntity` **before** IL2CPP reuses the ptr, or the next unit at that address inherits elite FA1 mods.

`instanceId` is required for cross-run progression/gear; `ptr` alone is never the durable key.

---

## Out of scope (this design doc)

- Schema DDL / FE roster UI (see unique-actor-runtime reserved tables)  
- Living-board picker for grant (API can already pass `entity:{ptr}`)  
- Plants+-style new typeIds / prefabs / Almanac  
- HitLand / HitZombie Harmony as FT* primary (on-hit uses TakeDamage + AttackPlant — [effect-runtime.md](effect-runtime.md) W0-D)  
- Implementing UniqueActor or MatchRuntime C#

---

## See also

- [overlay-control-loops.md](overlay-control-loops.md) — Hot vs Cold vs Intent  
- [unique-actor-runtime.md](unique-actor-runtime.md) — UniqueActor FSM + Data SSOT  
- [match-runtime.md](match-runtime.md) — UniqueBindings  
- [rpg-progression.md](rpg-progression.md) — type actors ≠ specimens  
- [effect-runtime.md](effect-runtime.md) — FA1 ModifyStat apply scope  
- [stat-system.md](stat-system.md) — ModifierBag / Writer  
