# Plan: VFX v2 — producibility, performance, visual pack

Spec: [../SPEC.md](../SPEC.md) (audit findings F1–F8, work items W1–W6)
SSOT (unchanged, still locked): [../docs/architecture/vfx-ssot.md](../docs/architecture/vfx-ssot.md)
Parallel round: [plan.md](plan.md) / [todo.md](todo.md) hold perf-v3 (untouched). Overlap: perf-v3 A5 lists `VfxDirector` as a loop-cost suspect — T2 here is that fix; strike VfxDirector from A5's scope note when T2 lands.

## Context

The VFX layer's architecture audit (2026-08-20) found the cue → recipe → primitive design sound. Three gaps remain: the `status.*` producer path was never built (SSOT phase 4), the LIVE probe has never run, and two perf leaks grew after the fact — `Camera.main` resolved every tick even with zero live VFX (a named suspect in the 9.2 ms `loop.tick` finding), and `AnchorResolver` duplicating the newer `InjectorEntityRegistry` including its own `FindObjectsOfType` sweep. Visually: no floater shadows, one burst shape for everything, `Flash` primitive unused.

## Dependency graph

```
T1 anchor-via-registry ─┐   T3 floater pack ─┐   T6 status producer path ─┐
T2 idle-cheap tick ─────┤   T4 shape math    │                            │
   (independent)        │      └→ T5 pool+   │                            │
                        ▼         recipes ───┤                            │
                       CP1                   ▼                            ▼
                                            CP2                          CP3 → T7 docs/prove → LIVE gate
```

T1/T2/T3/T4/T6 are mutually independent; T5 needs T4; T7 needs everything. Each task is a vertical slice (code + tests + guard/build) shipping green on its own.

## Key facts locked by exploration

- `InjectorEntityRegistry.FindZombie/FindPlant(ptrHex)` (`Injector/Effects/InjectorEntityRegistry.cs:116-120`) — O(1), hook-fed, frame-resynced; T1's whole mechanism already exists.
- Status success is definitively known at `Core/Status/StatusRuntime.cs:189` (post-`UpsertInstance`); only callback today is `OnResisted` (line 84); `OnApplied` must be a settable property (Unity-free ctor sites: `FoundationHarness.cs:33`, `SimEffectHost.cs:32`, tests).
- Injector wiring point: `Injector/Effects/EffectRuntime.cs:59-68`, beside the `OnResisted` assignment; `VfxDirector.Sink` is thread-safe enqueue-only.
- All **21** catalog statuses (`StatusCatalogBootstrap.cs`) have fixture coverage in `tests/fixtures/effects/scenarios/status-*.json`; LIVE harness is `scripts/prove-status-full.ps1` — the SSOT §4.2 seeding criterion is met for the full roster at once.
- `PerfSection.VfxTick` already instrumented (`InjectorLoop.cs:66`); budget number lands with T7, data from the next perf-v3 stress run.

## Risks

| Risk | Mitigation |
|---|---|
| Registry misses transforms the old sweep found (odd spawn paths) | Registry frame-resync backstop; LIVE ptr case in prove-vfx exercises it |
| Shadow pass doubles label draw cost | 64-floater cap, Repaint-only; single Draw block to revert |
| 21 status recipes overwhelm visually on dense boards | Per-(cueId, ptr) rate limiter already bounds it; `SYS-DAMAGE-FX` master + `fx.mute` per cue |
| vfx.tick budget unmet at 300z after T2 | perf-v3 A5 takes over with instrumented data — no blind fixing here |
