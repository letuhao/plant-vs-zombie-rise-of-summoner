# Performance probe + optimization plan (combat/element hot path)

Status: **Phase A implemented** (2026-08-20) — PerfProbe + wiring + `POST /api/perf` / `GET /api/perf/recent` + `scripts/probe-perf.ps1` are built and smoke-tested; Phase B baselines are next. Findings that motivated this: see audit summary at bottom.
Owner runbooks: [`debug-live-checklist.md`](debug-live-checklist.md), [`debug-pipeline.md`](debug-pipeline.md).

## 0. Frame budget (targets)

| Mode | Frame budget | Injector budget (target) |
|---|---|---|
| 60 fps normal | 16.6 ms | ≤ 1.0 ms/frame avg, ≤ 2 ms p99 |
| 120 fps / high-speed mode | 8.3 ms | ≤ 0.8 ms/frame avg |
| Stress (200+ entities, max speed) | 16.6 ms | ≤ 2 ms/frame avg, no GC gen2 during level |

Non-negotiable invariant after optimization: **per-hit cost is O(1)** — no `FindObjectsOfType`, no full stat resolve, no allocation-heavy payloads on the damage path. Scene scans happen at most **once per frame**, ideally never (incremental ptr→entity map).

Note: the base game itself lags on some levels (its own cost, not ours). The probe must separate **base-game frame cost** from **injector frame cost** so we optimize what we own and can prove our share is near zero.

## 1. Phase A — Instrumentation (PerfProbe)

New `src/FusionRpg.Injector/Host/PerfProbe.cs` — static, allocation-free record path
(`Stopwatch.GetTimestamp()` + `Interlocked` longs; snapshot-and-reset on flush).

### 1.1 Sections timed (count, total ms, max ms per 5s window)

| Section | Wraps | Proves |
|---|---|---|
| `loop.tick` | whole `InjectorLoop.Tick` | total injector share per frame |
| `board.capture` | `InjectorBoardSnapshot.Capture` | scan storm (finding 1) |
| `stats.resolve` | `StatSystem.Resolve` | per-hit resolves (finding 2) |
| `hub.resolveDerived` | `ActorHub.ResolveDerived` / `Resolve` | derived re-resolve cost |
| `effect.onCapture` | `EffectRuntime.OnCapture` | per-event effect pipeline |
| `effect.tickDots` | `EffectRuntime.TickDots` | 100ms DoT grid cost |
| `takeDamage.prefix` | Zombie/Plant TakeDamage prefixes | Harmony hook overhead |
| `fx.show` | `DamageFxOverlay.Show` (incl. TryResolve) | floater scans |
| `grants.scan` | `HasGrantWithTrigger` / `HasAnyGrant` | LINQ grant scans (finding 3) |

### 1.2 Counters (per 5s window)

- `emit.count.<kind-class>` — events emitted, bucketed: `combat.*`, `debug.*`, `*.damage`, other.
- `board.capture.count` — captures/sec (goal after fix: ≤ fps, then ~0).
- `queue.depth`, `queue.dropped` — from RpgClient (transport headroom proof).

### 1.3 Frame + memory metrics

- Frame-time histogram from `unscaledDeltaTime`: buckets `<8.4ms`, `<16.7ms`, `<33ms`, `≥33ms` + max. Gives real fps (answers "60 or 120?") and stutter shape.
- `GC.CollectionCount(0/1/2)` deltas and `GC.GetTotalAllocatedBytes()` delta per window → allocation rate MB/s.
- Board census (plants / zombies / bullets counts) once per window — reuses one capture, so the probe itself doesn't scan per frame.

### 1.4 Emission

- Every 5s: one `perf.window` metric batch through the existing `/api/metrics` path + one-line BepInEx log (works even if server down).
- Server: expose last N windows at `GET /api/perf/recent` (simple ring buffer in `DebugEndpoints`), so probing is one `Invoke-RestMethod`.
- Probe overhead budget: < 0.05 ms/frame. No dictionaries or strings allocated on the record path.

## 2. Phase B — Baseline measurement matrix

Script: `scripts/probe-perf.ps1` — samples `/api/perf/recent` over 60s per scenario, writes `docs/research/perf/_baseline-<scenario>.json`.

| # | Scenario | What it isolates |
|---|---|---|
| B1 | Light level, few entities, normal speed | baseline sanity |
| B2 | Heavy level (aim 200+ entities), normal speed | scaling with board size |
| B3 | Same heavy level, max speed mode | event-rate multiplier |
| B4 | B2 with `SYS-DAMAGE-FX` off | floater scan share |
| B5 | B2 with `OVERLAY-COMBAT` off | overlay resolve share |
| B6 | B2 with LogDamage off + no debug session | telemetry share |
| B7 | B2 with all grants cleared (`ClearAll`) | grant-scan share |
| B8 | Injector loaded, ALL features off | our floor vs base-game lag |
| B9 | (optional) vanilla, injector not loaded | base game's own cost on that level |

Deliverable: table of ms/frame + calls/sec + alloc MB/s per scenario → ranked cost centers with numbers, filed as `docs/research/perf/00-baseline.md`. **Decision gate:** optimizations below proceed in measured-impact order; anything the data doesn't implicate gets dropped from scope.

## 3. Phase C — Optimizations (each lands separately, re-measured against B2/B3)

| # | Change | Expected effect | Risk |
|---|---|---|---|
| O1 | **Per-frame board cache**: `InjectorBoardSnapshot.Get()` returns cached snapshot keyed by `Time.frameCount`; all callers (FreezeBoard, ResolveActor, ResolveDerived, ResolveElementTypesFromHub) use it | scans: ~6/hit → ≤1/frame | low |
| O2 | **Incremental ptr→entity map** maintained from existing Start/InitHealth/Die/Destroy hooks; replaces scans entirely, incl. `DamageFxOverlay.TryResolve` | scans → 0; O(1) ptr lookup | medium (lifecycle edge cases: mind control, reused pointers — map validated against O1 snapshot in debug builds) |
| O3 | **Gate telemetry**: all `debug.*` emits behind `DebugRuntime.SessionActive`; `HitCapture` default false outside sessions; cache `FUSIONRPG_OVERLAY_COMBAT` env read; skip payload building when no consumer | removes per-hit dict/string allocs + TryCasts | low |
| O4 | **Grant trigger index**: dictionary trigger→count maintained on Grant/Withdraw; `HasGrantWithTrigger` becomes O(1); pre-sort `def.Actions` at catalog load | removes 2× LINQ scan/hit | low |
| O5 | **Resolve cache**: `ActorDerivedSnapshot` + element types cached per ptr, invalidated by (stats revision, grant/status revision, match key) | per-hit resolves → cache hits | medium (invalidation correctness — covered by existing ActorHub/status tests + new cache tests) |
| O6 | **Ptr string cache**: `IntPtr → "X" string` dictionary in GameDumps, cleared on match end | kills top allocation source | low |
| O7 | TakeDamage prefix: cache `EffectiveStats()` result by cheat-document revision; skip `StatSystem.Resolve` when no non-identity mods | trims hook floor cost | low |

Order of landing: O1 → O3 → O4 → O6 → O7 (all low risk, likely ~90% of the win), then O2/O5 only if B-matrix rerun still shows scans/resolves above budget.

## 4. Phase D — Verify + lock in

- Rerun B1–B8. Pass criteria: budgets in §0 met; `board.capture.count ≤ fps` (O1) or ≈0 (O2); alloc rate < 1 MB/s during combat; frame p99 unchanged vs B8 floor.
- Keep PerfProbe permanently (near-zero cost) — it becomes the regression tripwire; add a `perf` row to the LIVE checklist.
- High-speed mode is the acceptance environment: if budgets hold at max speed on a 200-entity board, normal speed is free.

## Explicitly out of scope (until data says otherwise)

- **Rust server rewrite / SignalR→gRPC**: transport is already batched, async, capped, off the frame path (`RpgClient.Enqueue/TryFlush`). No probe data implicates it. Revisit only if Phase B shows `queue.*` pressure or server CPU affecting the game — which current evidence contradicts.
- Base-game lag (B9 delta): not fixable from the injector; we only commit to not adding to it.

## Audit summary (2026-08-20, what motivated this plan)

Per damage event today: up to ~6 `FindObjectsOfType` scene scans (FreezeBoard + attacker/defender resolve ×2 each + floater), 2–3 full stat resolves, ≥2 LINQ grant scans, unconditional `debug.combat.overlay` emission, and per-hit dictionary/string allocation. Cost ≈ hits/sec × entities × interop — superlinear in board activity; GC churn causes stutter even on small boards.
