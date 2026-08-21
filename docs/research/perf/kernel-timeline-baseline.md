# Perf baseline — battle timeline kernel (offline harness)

Plan + budgets: [`../../runbook/perf-probe-plan.md`](../../runbook/perf-probe-plan.md) · Spec: [`../../architecture/battle/spec-kernel-performance.md`](../../architecture/battle/spec-kernel-performance.md) · Combat hot path baselines: [`00-baseline.md`](00-baseline.md)

**This is not a live probe run.** It is the offline gate (task P2) that must pass *before* the kernel is wired into the injector — the argument being that a kernel which cannot hold its slice in a synthetic harness certainly will not hold it inside a real frame, and learning that costs a harness rather than an integration. Live `kernel.*` probe sections land with T13/P1b and belong in a separate, owner-run baseline.

## Budget this is measured against

**The game is frame-capped at 60 fps**, so the frame is **16.6 ms** and the injector target is **≤ 1.0 ms/frame avg** (`perf-probe-plan.md` §0). The kernel's slice is **≤ 0.15 ms/frame — 15 % of that**.

Source of truth is the code, not either document: `InjectorLoop.ApplyFpsCap` sets `Application.targetFrameRate = 60` by default at startup, and uncapping requires an explicit `FUSIONRPG_FPS_CAP=0`.

> **Careful with [`00-baseline.md`](00-baseline.md)'s "hardware truth" section.** It revises the budget to 4.16 ms / ≤0.5 ms from a **~240 fps uncapped** measurement dated 2026-08-20 — *before* the frame cap shipped. It is accurate only for the uncapped case, and a first draft of this file planned against it by mistake. Check the code before trusting either doc.

## Harness shape

`KernelStressHarnessTests` — 200 entities, 600 frames (10 s at 60 fps), deterministic (no RNG, no clock in the logic). Each frame: advance the clock by one frame's ticks (carry-corrected), drain due events into a reused buffer, and for every drained event drive its actor through `Charging → Ready → Committed → Resolving → Recovering → Charging` taking and releasing an `ActionSlots` slot, then re-arm it.

The FSM and slot pool are included deliberately: an earlier version measured only clock + queue and reported the result as "the kernel's cost", which understated it by ~4×.

## Results (2026-08-22)

| Metric | Budget | Measured | Headroom |
|---|---|---|---|
| Cost per frame @ 200 entities | ≤ 0.15 ms | **0.0336 ms** | ~4.5× |
| Steady-state allocation, 600 frames | 0 bytes | **0 bytes** | — |
| Events drained over the run | — | 4 384 | — |

Complexity, asserted by counting comparisons rather than by stopwatch (`EventQueue.ComparisonCount`):

| Property | Result |
|---|---|
| Reschedule, n = 2 000 → 16 000 | log-linear; well under the quadratic threshold |
| Drain, n = 2 000 → 16 000 | log-linear |
| Comparisons **per event**, 100 → 400 entities | flat within 3× (heap growth is log n) |

## Caveats, so these numbers are not over-read

- **Offline, not in-frame.** No Unity, no interop, no GC pressure from the rest of the game, no tiered-compilation behaviour under a real workload. The live figure will be worse; how much worse is what T13/P1b measures.
- **Wall-clock is reported, not gated.** The pass/fail assertion is **allocation**, which is deterministic. Timing is asserted only against a catastrophic-regression ceiling, because a tight timing assertion in CI measures the build agent and gets muted at the first flake.
- **One workload shape.** Uniform re-arm intervals; a real board is burstier. The per-event scaling test covers board size, not burstiness.

## What would invalidate this

A change that reintroduces per-tick allocation, or that makes scheduler cost grow faster than log n. Both are guarded in CI — allocation by byte assertions, complexity by comparison counts — so this baseline should stay true until the workload shape itself changes.
