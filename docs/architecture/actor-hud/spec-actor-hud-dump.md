# Spec: `actor-hud-dump`

**Module id:** `actor-hud-dump` · **Program:** [../actor-hud-map.md](../actor-hud-map.md) ·
**Ideal:** [../actor-hud-ideal.md](../actor-hud-ideal.md) ·
**Pipeline:** [../../research/actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
**Depends on:** `actor-hud-core`, P1.5 EntityApply pin · **Blocks:** `actor-hud-fold`, `actor-hud-unity`
**Status:** implemented 2026-08-31 — shipped; builder/cache + golden tests green.

---

## Assumptions

1. **Presentation-only** — builder reads Hot runtimes and pins; never writes gameplay state (same boundary as VFX).
2. **Single entry** — `ActorHudBuilder.Build(normalizedPtr)` is the only injector gather path for HUD fields.
3. **Read surface contract** — every HUD field reads through the API column below; no banned sources
   ([pipeline audit §3](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)).
4. **Existing shield totals** on entity payloads (`GameDumps.AddRpgShield` → `rpgShieldHp`/`Max`) remain
   during transition; **`actorHud`** is the forward SSOT for fold + renderers.
5. **Invalidation:** rebuild snapshot for affected ptr on listed event kinds — not full-board
   `VisitOwners` every frame when dirty set is empty (audit perf note).
6. **Fallback tick:** at most one lazy reconcile per frame for dirty ptr set if event path misses edge case.

---

## Objective

Build `ActorHudSnapshot` per living entity and attach to observe payloads so web fold and Unity pool consume
one model.

**Success:** Apply fire shield + `expose` + `command` to a zombie → `entity.stats` / `debug.board-stats`
row includes `actorHud` with element stacks and two status tokens → golden JSON matches.

---

## Program acceptance share

Golden file test: fixture runtimes → `ActorHudBuilder.Build(ptr)` JSON under
`tests/FusionRpg.Core.Tests/Goldens/actor-hud/` (or Injector test project). Invalidation test: second build
without shield event returns cached snapshot until dirty flag set. Static review or test doubles assert
builder does not reference banned read paths.

---

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter ActorHudBuilder
dotnet test tests\FusionRpg.Guard.Tests
```

---

## Project structure

| Path | Change |
|------|--------|
| `src/FusionRpg.Injector/Hud/ActorHudBuilder.cs` | **new** — build per ptr |
| `src/FusionRpg.Injector/Hud/ActorHudCache.cs` | **new** — dirty ptr set + cache |
| `src/FusionRpg.Injector/Hud/ActorHudObserve.cs` | **new** — serialize to dictionary |
| `src/FusionRpg.Injector/Stats/EntityApply.cs` | edit — production derived pin (P1.5) |
| `src/FusionRpg.Injector/GameDumps.cs` | edit — call builder in `Plant`/`Zombie` after `AddRpgShield` |
| `src/FusionRpg.Injector/DebugRuntime.cs` | edit — `BoardEntityStats` rows include `actorHud` |
| `src/FusionRpg.Injector/GameHooks` or emit path | edit — optional `debug.actor-hud` delta emit |

---

## Read surface contract

Mandatory builder reads — copy of [pipeline audit §3](../../research/actor-hud-data-pipeline-audit-2026-08-30.md).
A slot ships only when its row is wired and the Read API is used exclusively.

| HUD field | Authoritative SSOT | Read API (`ActorHudBuilder`) | v1 status |
|-----------|-------------------|------------------------------|-----------|
| `identity.role` | `MatchUniqueBindingsFacet` | `MatchHost.Runtime.UniqueBindings.TryGetByPtr(ptr)` | wired |
| `identity.levelBand` | `ActorDerivedSnapshot` pin | `InjectorDerivedOverride.TryGet(ptr)` → `progression.power` → `PowerBandDisplay` | wired at EntityApply |
| `identity.tier` unique | BoardProjection / spawn observe | `flags.unique` on occupant path | partial |
| `identity.tier` elite | Demon/rarity derived | TBD — **omit v1** if no pin/signal | inert |
| `identity.tier` boss | Expedition spawn | **omit v1** | inert |
| `resources.shield` | `ShieldRuntime` | `EffectRuntime.Bag.ShieldGate.Runtime` totals/stacks — same path as `GameDumps.AddRpgShield` | partial |
| `statuses[]` | `StatusRuntime` | Instance enumeration by owner ptr (combat owner key) | partial |
| `overflow.statusCount` | `ActorHudLayout` | `Prioritize` on full status list | not built |

### Binding read

`MatchHost.Runtime` → `MatchUniqueBindingsFacet.TryGetByPtr(normalizedPtr)` —
[UniqueBindings.cs](../../../src/FusionRpg.Core/Match/UniqueBindings.cs). When Bound, set
`identity.role = specimen`; otherwise `vanilla`.

### Shield read

`EffectRuntime.Bag.ShieldGate.Runtime` — totals and per-element stacks. Reuse aggregation helpers from
existing `GameDumps.AddRpgShield`; builder is authoritative forward path for fold.

### Status read

`StatusRuntime` instance list for owner ptr — same owner key as combat/shield. Map to tokens via core
`ActorHudLayout.Prioritize` and magnitude band rules.

### Level band read

**Not** `plant.theLevel`, `zombie.level`, raw Θ, or REST. Input to `PowerBandDisplay.FromTheta` is
`progression.power` from pinned derived snapshot only (see EntityApply pin below). If pin missing,
`levelBand` omitted — no fallback to Unity fields.

---

## EntityApply derived pin (P1.5 — prerequisite for `levelBand`)

Production wiring elevates `InjectorDerivedOverride` from cheat-only to Hot pin cache.

After `ActorHub.Resolve` in `EntityApply.RunPlant` and `EntityApply.RunZombie`:

```csharp
InjectorDerivedOverride.Pin(normalizedPtr, resolved.Derived);
```

- **When:** every EntityApply resolve (spawn, gear change, stat refresh) — same moment derived stats reach combat.
- **Clear:** existing `InjectorDerivedOverride.Clear()` on match end / board teardown (same hook as today).
- **Doc comment:** update `InjectorDerivedOverride` from "LIVE prove" / debug-only to production Hot cache.
- **Builder:** `ActorHudBuilder` calls `TryGet(ptr)`; on miss, omit `levelBand` (empty badge), never SQL/REST.

Verified: `EntityApply` already calls `ActorHub.Resolve` — pin is one line per Run path, not new math
([EntityApply.cs](../../../src/FusionRpg.Injector/Stats/EntityApply.cs)).

**Acceptance:** pin survives from apply until entity die or match end; golden test with pinned fixture
→ stable `levelBand` across rebuilds until pin dirty event.

---

## Design

### v1 tier rules

| Condition | `identity.tier` |
|-----------|-----------------|
| `flags.unique` | `unique` |
| Bound demon specimen (when profile wired) | `elite` (interim) |
| Default | `normal` |
| Expedition boss | **omit** — do not emit `boss` until signal exists |

### Observe payload

Nested on each plant/zombie dictionary (camelCase JSON):

```json
{
  "ptr": "1A2B3C",
  "hp": 100,
  "rpgShieldHp": 50,
  "rpgShieldMax": 80,
  "actorHud": {
    "identity": { "tier": "normal", "role": "vanilla", "levelBand": 12, "flags": [] },
    "resources": {
      "shield": {
        "hp": 50,
        "max": 80,
        "stacks": [{ "element": "fire", "hp": 50, "max": 80 }]
      }
    },
    "statuses": [
      { "id": "expose", "cc": false, "magnitudeBand": "mid" },
      { "id": "command", "cc": false, "magnitudeBand": "low" }
    ],
    "overflow": { "statusCount": 0 }
  }
}
```

**Transport paths:**

1. `debug.board-stats` — full board refresh (existing poll path)
2. `entity.stats` — delta updates (existing fold handler)
3. `debug.actor-hud` — optional `{ ptr, actorHud }` only when cache invalidates single entity

### Shield stack colors

Reuse element ids and aggregation from existing `ShieldBarColor` / `ShieldBarVisual` — builder shares
Core/injector shield read helpers; **element id required** on each stack for accessibility (audit §1.3).

### Invalidation

Register dirty ptr on event kinds from [events.md](../../protocol/events.md) and pipeline audit §6:

| Kind family | Examples |
|-------------|----------|
| Shield | `shield.apply`, `shield.absorbed`, `shield.broken`, `shield.expired` |
| Status | `debug.status.apply`, `debug.status.clear`, status expiry prune |
| Lifecycle | `plant.spawn`, `zombie.spawn`, `plant.die`, `zombie.die` |
| Binding | `ScopeMembership` Bound/Cleared (`MatchUniqueBindingsFacet`) |
| Derived refresh | `entity.stats` after EntityApply (pin updated) |

`ActorHudCache` clears entry on dirty; builder rebuilds on next observe read for that ptr.

---

## Forbidden reads (hard ban)

Builder and cache **must not** use these sources for any HUD field:

| Banned source | Why |
|---------------|-----|
| `FusionRpg.Data` / SQLite mid-match | Cold loop on Hot path — UniqueActor FSM |
| REST `/api/unique/*`, `/api/actors/*/derived` | Server FSM; ActorPanel owns full sheet |
| Unity RPG fields (`theLevel`, `theShieldHealth`, etc.) | PvZ foundation — not overlay SSOT |
| `FindObjectsOfType` / scene scan | Perf + ad-hoc observe |
| `typeId` alone for tier or level | Almanac species ≠ power band |
| Live re-resolve of derived every frame | Use pin; fallback only when pin missing = omit field |

Renderers (Unity pool, Phaser, Inspector) **must not** bypass builder — see fold/unity specs.

---

## Boundaries

- No new SQL, no REST endpoints in this module.
- Do not remove `rpgShieldHp`/`Max` until fold + Inspector migrate (fold spec).
- Do not write to `ShieldRuntime` or `StatusRuntime`.

---

## Test plan

| Test | Assert |
|------|--------|
| `Build_shield_stacks` | Two elements → two stack entries |
| `Build_status_priority` | Uses `ActorHudLayout.Prioritize` |
| `Build_omits_boss_tier` | No boss signal → tier not boss |
| `Build_levelBand_from_pin` | Pinned `progression.power` → stable band |
| `Build_omits_levelBand_without_pin` | No pin → no `levelBand`; no `theLevel` fallback |
| `Cache_invalidates_on_shield_event` | Dirty after shield delta |
| Golden `elite_shield_dual_status.json` | Full snapshot match |
| Read surface compliance | No banned API references in builder (static review or test double) |

---

## Related

- [spec-actor-hud-core.md](spec-actor-hud-core.md)
- [actor-hud-data-pipeline-audit-2026-08-30.md](../../research/actor-hud-data-pipeline-audit-2026-08-30.md)
- [GameDumps.cs](../../../src/FusionRpg.Injector/GameDumps.cs) — `AddRpgShield`
- [EntityApply.cs](../../../src/FusionRpg.Injector/Stats/EntityApply.cs)
- [shield-system-spec.md](../shield-system-spec.md) §2.6
