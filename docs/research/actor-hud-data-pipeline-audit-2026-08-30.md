# Actor HUD data pipeline audit — SSOT and Hot read path

**Date:** 2026-08-30  
**Scope:** Where actor HUD fields come from, which FSM owns each fact, single observe pipeline, duplicate
retirement  
**Status:** Research reference. **Audit only — no build authorized.** Verified against code 2026-08-30.

**Governed by:** [actor-hud-map.md](../architecture/actor-hud-map.md) ·
[overlay-control-loops.md](../architecture/overlay-control-loops.md) ·
[software-architecture.md](../architecture/software-architecture.md) §8 ·
[match-runtime.md](../architecture/match-runtime.md) ·
[unique-actor-runtime.md](../architecture/unique-actor-runtime.md) ·
[actor-hub-ssot.md](../architecture/actor-hub-ssot.md)

**Related audits:** [actor-hud-audit-2026-08-30.md](actor-hud-audit-2026-08-30.md) (user perspective)

**Why this file exists.** The actor-hud specs named inputs (`ShieldRuntime`, bindings) but did not lock
**where data lives**, **which FSM is authoritative**, or **the one pipeline** from SSOT → `Occupant.hud`.
Without that, implementation drifts into ad-hoc reads (SQLite mid-match, `theLevel`, parallel shield paths).

---

## Executive summary

**Verdict:** A HUD slot **may ship only when** its SSOT row is `wired` or `shipped` and `ActorHudBuilder`
reads exclusively through the **Read API** column below. Wiring gaps may leave a slot empty; they **must
not** add fallback reads from banned sources.

Today most rows are **partial** with **duplicate observe paths** — the program collapses them into one
Hot pipeline. Cold SQLite (`rpg_unique_actors`) affects HUD **only after** deploy push + Hot resolve at
bind/apply — never per-frame SQL during `InMatch`.

**Owner decision (2026-08-30):** `identity.levelBand` uses **`ActorDerivedSnapshot` pinned at
`EntityApply`** after `ActorHub.Resolve` — not live SQL, not `theLevel`, not a per-frame private re-resolve
except as documented fallback when pin missing.

---

## 1. Three FSMs — what HUD may read during a match

From [software-architecture.md](../architecture/software-architecture.md) §8 and
[overlay-control-loops.md](../architecture/overlay-control-loops.md) §3.

| FSM | Store | HUD read at display time? | HUD fields |
|-----|-------|---------------------------|------------|
| **MatchPhase** | `MatchRuntime` / `MatchState` (Hot RAM) | **Yes** — only `InMatch` / `Paused` | Gates per-unit HUD; hosts `UniqueBindings` facet |
| **UniqueBindings** | `MatchUniqueBindingsFacet` (Hot seam) | **Yes** — `TryGetByPtr(ptr)` | `identity.role`, bound `instanceId` on observe |
| **UniqueActor** | `rpg_unique_actors` (Cold SQLite) | **No mid-match poll** | Level/gear reach HUD only via grants at bind + EntityApply resolve |

**Hard ban:** No `FusionRpg.Data`, no REST `/api/unique/*`, no Server FSM between combat and HUD build during
a live match.

---

## 2. Three IDs — never collapse

| ID | Role in HUD |
|----|-------------|
| **`ptr`** | Owner key for Hot runtimes, HUD slot pool, builder cache |
| **`instanceId`** | Durable specimen when `UniqueBindings` phase = `Bound` |
| **`typeId`** | Almanac species — **not** power band or tier |

---

## 3. Master SSOT table (per HUD field)

| HUD field | Authoritative SSOT | FSM / store | Read API (`ActorHudBuilder`) | InMatch? | v1 status | Duplicates to retire |
|-----------|-------------------|-------------|------------------------------|----------|-----------|---------------------|
| `identity.role` | `MatchUniqueBindingsFacet` | Hot RAM | `TryGetByPtr(normalizedPtr)` | yes | wired API exists | Inspector guessing binding |
| `identity.levelBand` | `ActorDerivedSnapshot` pin | Hot pin | `InjectorDerivedOverride.TryGet` → `progression.power` → `PowerBandDisplay` | yes | **wired** at EntityApply (slice 1, 2026-08-30) | `theLevel`, raw Θ on lawn |
| `identity.tier` unique | BoardProjection / spawn observe | Hot events | `flags.unique` on occupant fold path | yes | partial | — |
| `identity.tier` elite | Demon/rarity derived | Hot | TBD — **omit v1** if no signal | — | inert | — |
| `identity.tier` boss | Expedition spawn | Cold+Hot | **omit v1** | — | inert | — |
| `identity.flags` | Binding + unique + demon profile | Hot + observe | Composed in builder from above | yes | partial | — |
| `resources.shield` | `ShieldRuntime` | Hot RAM | `EffectRuntime.Bag.ShieldGate.Runtime` totals/stacks | yes | **wired** (slice 2) | `rpgShieldHp`/`Max`, `ShieldBarPool` |
| `statuses[]` | `StatusRuntime` | Hot RAM | Instance list by owner ptr (combat owner key) | yes | **wired** (slice 2) | `statusChips` text fold |
| `overflow.statusCount` | `ActorHudLayout` | Core pure | Full status list − visible cap | yes | **wired** (slice 2) | — |
| Vanilla HP | Unity engine | PvZ fields | **Not HUD v1** — Phaser `setHpDisplay` only | yes | shipped | — |

Code verification:

- `MatchUniqueBindingsFacet.TryGetByPtr` — [UniqueBindings.cs](../../src/FusionRpg.Core/Match/UniqueBindings.cs)
- `GameDumps.AddRpgShield` — [GameDumps.cs](../../src/FusionRpg.Injector/GameDumps.cs) (parallel today)
- `EntityApply` → `ActorHub.Resolve` → `InjectorDerivedOverride.Pin` — [EntityApply.cs](../../src/FusionRpg.Injector/Stats/EntityApply.cs) (production pin, slice 1)
- `InjectorDerivedOverride.TryGet` — [InjectorDerivedOverride.cs](../../src/FusionRpg.Injector/Stats/InjectorDerivedOverride.cs)

---

## 4. Cold SQLite tables — deploy context only

From [data-architecture.md](../architecture/data-architecture.md). **Not HUD read path mid-match.**

| Table | Role for HUD |
|-------|----------------|
| `rpg_unique_actors` | Specimen SSOT (level, gear, phase FSM); affects HUD after loadout push + Hot resolve |
| `rpg_player_commander` | Band A commander-surface — **not** per-unit HUD |

Cold path:

```text
Deploy Intent → UniqueActor FSM (Server) → loadout/grants push → EffectBag at bind
  → EntityApply + ActorHub.Resolve → InjectorDerivedOverride.Pin(ptr)
  → ActorHudBuilder reads pin → levelBand on lawn
```

---

## 5. Single observe pipeline

```text
Hot stores (MatchUniqueBindingsFacet, ShieldRuntime, StatusRuntime, derived pin)
    → ActorHudBuilder.Build(ptr)     [single injector entry]
    → actorHud on entity.stats / debug.board-stats
    → lawnProjectorFold → Occupant.hud
    → ActorHudPool (Unity) + SyncFromModelSystem (Phaser) + Inspector
```

| Stage | Component | Trigger |
|-------|-----------|---------|
| 0 Pin derived | `EntityApply.RunPlant` / `RunZombie` | After `ActorHub.Resolve` → `InjectorDerivedOverride.Pin(ptr, resolved.Derived)` |
| 1 Build | `ActorHudBuilder.Build(ptr)` | `GameDumps.Plant`/`Zombie`, `DebugRuntime.BoardEntityStats` |
| 2 Attach | `actorHud` nested dict | `entity.stats`, `debug.board-stats` |
| 3 Delta (optional) | `debug.actor-hud` | `{ ptr, actorHud }` on invalidation |
| 4 Fold | `lawnProjectorFold.foldActorHud` | Observe events only |
| 5 Model | `Occupant.hud` | [lawnViewModel.ts](../../web/fusion-rpg-web/src/features/lawn/lawnViewModel.ts) |
| 6 Render | Unity / Phaser / Inspector | **Read model only — never rebuild SSOT** |

Renderers (Unity pool, Phaser, Inspector) **must not** call `ShieldRuntime`, `StatusRuntime`, or REST —
they consume builder output / fold model only ([spec-actor-hud-unity.md](../architecture/actor-hud/spec-actor-hud-unity.md)).

---

## 6. Invalidation event kinds

Rebuild or dirty `ActorHudCache` for affected `ptr` on:

| Kind family | Examples |
|-------------|----------|
| Shield | `shield.apply`, `shield.absorbed`, `shield.broken`, `shield.expired` |
| Status | `debug.status.apply`, `debug.status.clear`, status expiry prune |
| Lifecycle | `plant.spawn`, `zombie.spawn`, `plant.die`, `zombie.die` |
| Binding | `ScopeMembership` Bound/Cleared (`MatchUniqueBindingsFacet`) |
| Derived refresh | `entity.stats` after EntityApply (pin updated) |

**Not** full-board `FindObjectsOfType` every frame when dirty set is empty.

---

## 7. Duplicate sources — retirement schedule

| Legacy path | Today | Retire when |
|-------------|-------|-------------|
| `GameDumps.AddRpgShield` → `rpgShieldHp`/`Max` | Parallel observe SSOT | Fold + Inspector use `hud.resources.shield` |
| `OBSERVE_CHIPS` + Inspector comma chips | Parallel status text | `hud.statuses` + `ActorHudInspector` |
| `ShieldBarPool.TickSync` + direct runtime read | Parallel render | `shield-slot-migration` module |
| `InjectorDerivedOverride` cheat-only pin | **Retired** — production pin at EntityApply (P1.5, slice 1) | — |

During transition, fold may **read** legacy keys only to populate `Occupant.hud` when `actorHud` absent —
fold must not **author** parallel semantics.

---

## 8. Forbidden reads (builder and renderers)

| Forbidden | Why |
|-----------|-----|
| `FusionRpg.Data` / SQL mid-match | Cold loop on Hot path |
| REST `/api/unique/*`, `/api/actors/*/derived` during match | Network + wrong loop |
| Unity `theLevel`, `zombie.level` for `levelBand` | PvZ field, not RPG ladder |
| Unity `theShieldHealth` for RPG shield | Vanilla armor — [shield-system-spec.md](../architecture/shield-system-spec.md) §2.6 |
| `FindObjectsOfType` to discover HUD inputs | Perf + duplicate of BoardProjection |
| Direct `ShieldRuntime` in `ActorHudPool` | Builder/render separation |
| Second authoritative fold (`statusChips` + `hud.statuses`) | SSOT drift |

**Fallback (documented only):** If derived pin missing for ptr, builder may call
`InjectorStatusBridge.ResolveDerived(ptr, false)` once per build — same math as StatusRuntime — then
optionally backfill pin. This is a **wiring gap handler**, not the primary path.

---

## 9. Feature gate rule (program-level)

> **A HUD slot ships in a module only if its SSOT row is `wired` or `shipped` and `ActorHudBuilder` reads
> exclusively through the Read API in §3.** Empty slot OK; banned fallback not OK.

Module acceptance shares in [actor-hud-map.md](../architecture/actor-hud-map.md) must include pipeline
compliance where applicable.

---

## 10. Incidents this audit prevents

| Failure | Without pipeline SSOT |
|---------|---------------------|
| Level badge from SQLite mid-wave | Stale or RTT-broken HUD |
| Unity pool reads shield while fold uses `rpgShieldHp` | Unity vs Phaser disagree |
| Inspector builds its own chip layout | GG-25 schema viewer on lawn |
| `theLevel` on badge | Wrong frame — RPG uses `progression.power` |
| Status from fold chips only on web, VFX only in Unity | User sees split brain |

---

## 11. Wiring gaps vs real gaps

| Finding | Classification |
|---------|----------------|
| No `ActorHudBuilder` | Real gap — new module |
| Derived pin not at EntityApply | **Wiring gap** — P1.5 |
| `statusChips` partial (9/13 ids) | Wiring gap — extend fold |
| Boss tier signal on lawn | Real gap — expedition program |
| Elite from demon profile | Wiring gap when profile path exists |

---

## Related documents

| Doc | Role |
|-----|------|
| [actor-hud-map.md](../architecture/actor-hud-map.md) | Capability map — pipeline section |
| [spec-actor-hud-dump.md](../architecture/actor-hud/spec-actor-hud-dump.md) | Read surface contract |
| [tasks/actor-hud-plan.md](../../tasks/actor-hud-plan.md) | P1.5 EntityApply pin |
