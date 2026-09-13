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

| # | Trigger | Today |
|---|---|---|
| 1 | Match change (`matchKey`) | **handled** — wholesale clear |
| 2 | Hypno / charm — zombie becomes plant-side | **missing** — this module |
| 3 | Entity death + IL2CPP ptr reuse — a new creature at the same address | must not inherit the old entry; verify against the existing `ForgetEntity` ordering (`GameHooks.cs:664/676`) |
| 4 | Species/element retune mid-run (`reforge-world`) | confirm whether a catalog revision bump must invalidate; if it cannot happen mid-match, say so explicitly rather than leaving it unexamined |

## Boundaries

- **Always:** invalidate per ptr; keep the Core half Unity-free; leave the match-scoped clear intact.
- **Ask first:** widening this into a general-purpose cache-invalidation framework — three other
  caches share the §2.16 shape, but fixing them is not this module's job.
- **Never:** clear the whole cache on a side change; add a second element cache; resolve element from
  anything but the existing `LawnElementIndex` path.

## Success criteria

- [ ] A hypnotised zombie resolves its new side on the next element read, proven by a Core unit test.
- [ ] Trigger 3 (ptr reuse) has a test and a stated answer, even if the answer is "already safe
      because `ForgetEntity` runs first" — with the file:line that makes it true.
- [ ] Trigger 4 has a stated answer, even if that answer is "cannot happen mid-match".
- [ ] No whole-cache clear added to any per-entity path.
- [ ] `guard-secondary-no-unity.ps1` green.
