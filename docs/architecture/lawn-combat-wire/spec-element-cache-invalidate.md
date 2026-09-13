# Spec: `element-cache-invalidate`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** — (leaf, parallelisable)

---

## Objective

`LawnElementResolver` caches `(side, elements)` per ptr for a whole match and invalidates only when
`matchKey` changes (`LawnElementResolver.cs:23,43-67`). A hypnotised zombie changes side and keeps its
stale cached entry forever, because `zombie.hypno` (`GameCaptureHooks.cs:283-294`) never invalidates.

Success: a side change invalidates that ptr's cache entry, and the next resolve returns the new side.

**This is `DESIGN-GATE.md` §2.16's fourth shipped instance** — *cache populated on trigger X, state
changed on trigger Y*. The prior three: commander reallocation reaching only entities spawned after it
(`CheatState.cs:98-102`), a SignalR reconnect not re-syncing caches (`RpgClient.cs:143-147`), and
`unique-lawn-wire`'s missing bind edge. §2.16 requires an edge-refreshed cache to **enumerate its full
trigger set and test every one** — that enumeration is this module's real deliverable, not just the
hypno fix.

Fixing this is worth landing regardless of the rest of the program: it is a live correctness defect
today, wherever element matters (shields, overlay damage, VFX tint).

## Tech stack

`FusionRpg.Core` (`LawnElementResolver`) + `FusionRpg.Injector` (the capture site that signals a side
change). Unity-free on the Core side.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~LawnElementResolver"
.\scripts\guard-secondary-no-unity.ps1
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Core/Creatures/LawnElementResolver.cs` | Gains a per-ptr invalidation entry point beside the existing match-scoped clear |
| `src/FusionRpg.Injector/Effects/LawnElementResolverHost.cs` | Calls it |
| `src/FusionRpg.Injector/GameCaptureHooks.cs` | The `zombie.hypno` site signals the side change |

## Code style

Invalidation is **by ptr**, not a whole-cache clear — a full clear on every hypno would re-resolve
every entity on the board, which is the per-hit-scan cost pattern the perf baseline already blames for
lag. Mirror the existing match-change clear's shape; do not add a second cache.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | Resolve ptr → side A; invalidate; resolve again → side B. Invalidating an unknown ptr is a no-op, never a throw. A match change still clears wholesale (existing behaviour unregressed) |
| Core unit (§2.16 enumeration) | One test per trigger in the enumerated set below, asserting the cache is fresh after each |
| Guard | `guard-secondary-no-unity.ps1` — Core half stays Unity-free |

## The full trigger set (§2.16 requirement)

Every edge that changes what a ptr's `(side, elements)` resolves to. The spec is not done until each
has a test.

**Corrected 2026-09-13 during build — rows 2 and 3 described the opposite of what the code does.**

| # | Trigger | Reality |
|---|---|---|
| 1 | Match change (`matchKey`) | **handled** — wholesale clear |
| 2 | Hypno / charm | **NOT a trigger, and wiring one would be a regression.** The cached `side` is *object kind*, not allegiance: `InjectorEntityRegistry.CollectSnaps` writes `"plant"`/`"zombie"` as literals and carries control state separately as `MindControlled`, which `MechanicalOwnSideOracle.cs:50` folds in **at read time** (*"mind control flips which side an entity fights FOR, not which side it visually belongs to"*). Flipping it here would make `_index.TryGet("plant", zombieTypeId)` **miss**, degrading every charmed zombie to Neutral element, and would break `GateCounterHost.ResolveOwnerFromPtr`, which depends on spawn kind. Left uninvalidated, with a test asserting the absence so it is not "fixed" back |
| 3 | Entity death + IL2CPP ptr reuse | **This was the real defect.** `ForgetEntity` cleared `Applied`, `EntityStatWriter`, `CheatState.Stats` and both HUD caches — but not the element cache, so a reused pointer inherited the dead entity's species element. Now wired at `GameHooks.cs:1293` |
| 4 | Species/element retune mid-run (`reforge-world`) | **Cannot fire**, proven by two tests: `reforge-world` re-rolls one *player's* rolled species rows and never touches `CreatureSpeciesCatalog`, which is configured once per host (`RpgHost.cs:125`) and documented immutable for the process lifetime |

**Also fixed in the same pass:** split cache keys — `GateCounterHost` passed `CombatPtr.Normalize(ptr)`
while both combat bridges passed the raw `key`, so one entity held **two entries under two spellings**.
An invalidation would have cleared one and left the other serving a dead entity. Keys are canonical
inside the resolver now.

**Found, not fixed — worth its own task:** `LawnElementResolverHost.Resolve` calls `BoardFactsFor(key)`
**eagerly on every call**, before consulting the cache, then passes the already-computed result as the
`Func`. The `Func` was designed to be lazy so the board scan only runs on a miss — so the cache
currently saves the element-map lookup but **not** the board scan it exists to remove. Fixing it means
caching `typeId` too, which is a behaviour change rather than a tidy-up.

## Boundaries

- **Always:** invalidate per ptr; keep the Core half Unity-free; leave the match-scoped clear intact.
- **Ask first:** widening this into a general-purpose cache-invalidation framework — three other
  caches share the §2.16 shape, but fixing them is not this module's job.
- **Never:** clear the whole cache on a side change; add a second element cache; resolve element from
  anything but the existing `LawnElementIndex` path.

## Success criteria

- [ ] A hypnotised zombie resolves its new side on the next element read, proven by a Core unit test.
- [ ] Trigger 3 (ptr reuse) has an **executable test**: resolve ptr P → element A; kill that entity;
      register a new entity at the same address with a different species; resolve P → element B. It
      must not return A. *(If the answer is "already safe because `ForgetEntity` runs first", the test
      still exists and proves it — a prose answer is not a check.)*
- [ ] Trigger 4 (catalog revision / `reforge-world` mid-run) is resolved one of two ways, both
      checkable: either an invalidation test like trigger 3's, **or** a test asserting the catalog
      revision cannot change while a match is live (so the trigger genuinely cannot fire). Prose alone
      does not close it.
- [ ] No whole-cache clear added to any per-entity path.
- [ ] `guard-secondary-no-unity.ps1` green.
