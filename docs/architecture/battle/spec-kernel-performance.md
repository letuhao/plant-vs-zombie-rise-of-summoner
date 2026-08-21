# Spec: kernel-performance

Module id `kernel-performance` (P-series) in the [battle timeline map](../battle-timeline-map.md). Cross-cuts T1–T7. Inherits — does not restate — the budgets and instrumentation in [perf-probe-plan.md](../../runbook/perf-probe-plan.md) and [event-pipeline-v2-spec.md](../event-pipeline-v2-spec.md).

## Objective

**Owner decision (2026-08-21): the kernel runs per-frame inside the injector.** That single answer changes its nature — it stops being server-side library code and becomes **frame-critical Unity main-thread code**, sharing a budget with everything else the injector does on a board that can hold 200+ entities.

So performance here is not a late optimisation pass. It is a **correctness constraint on the type design**, and it must be settled before T2–T4 harden, because the expensive mistakes (a class where a struct belongs, a string key on a tick path) are the ones that cost a rewrite rather than a tune.

Done means: a steady-state battle tick allocates **zero bytes**, every scheduler operation is O(log n) or better, per-frame work is **bounded and resumable**, and both properties are defended — by deterministic assertions in CI and by live probe sections in the game.

## Budgets — inherited, not invented

From `perf-probe-plan.md` §0, unchanged:

| Mode | Frame | Injector share (all of it, kernel included) |
|---|---|---|
| 60 fps normal | 16.6 ms | ≤ 1.0 ms/frame avg, ≤ 2 ms p99 |
| 120 fps | 8.3 ms | ≤ 0.8 ms/frame avg |
| Stress, 200+ entities | 16.6 ms | ≤ 2 ms/frame avg, **no gen2 GC during a level** |

**The 60 fps row is the operative one, and the code is the reason.** `InjectorLoop.ApplyFpsCap` sets `Application.targetFrameRate = 60` by default at startup; uncapping requires an explicit `FUSIONRPG_FPS_CAP=0`. So the frame is **16.6 ms** and the injector target is **≤ 1.0 ms/frame avg**.

> Note for anyone reading [`00-baseline.md`](../../research/perf/00-baseline.md): its "hardware truth" section revises the budget to 4.16 ms / ≤0.5 ms based on a **~240 fps uncapped** measurement taken 2026-08-20. That predates the frame cap. It describes the uncapped case only — **plan against the cap, and check the code before either document.**

Plus the standing invariant: **per-hit cost is O(1)** — no scene scans, no full stat resolve, no allocation-heavy payloads on the damage path.

**The kernel's own slice is ≤ 0.15 ms/frame average at 200 entities** — 15 % of the injector's 1.0 ms. It is a *share* of an already-tight allowance, not a fresh one: that budget goes mostly to hooks and the effect pipeline, and the kernel is new cost on top. Offline baseline: [`kernel-timeline-baseline.md`](../../research/perf/kernel-timeline-baseline.md) — **0.0336 ms/frame, 0 bytes**.

## What "runs in the injector" must and must not mean

This reopens a distinction the ideal drew, and it needs stating precisely or it will be violated by accident:

- **The kernel schedules OUR timeline** — RPG effects, statuses, shields, overlay actions. It gets a per-frame tick, bounded work, and a probe section.
- **The kernel never schedules the game's actors.** Unity still owns when a zombie walks or a pea fires. PvZ remains **observed**; T7 stays a stateless projection with no queue and no per-actor machine.

The existing boundaries hold unchanged: no ad-hoc Unity stat patches, every HP delta still through `EntityStatWriter`/the Funnel, and no scene scans on the tick path.

## The allocation contract

**Zero steady-state allocation.** After warm-up, a frame that ticks the kernel allocates nothing. This is measurable, so it is testable, and it is the property that actually prevents stutter — gen2 during a level is already a stated non-goal.

Concretely forbidden on the tick path:

| Forbidden | Why | Instead |
|---|---|---|
| Class-typed per-turn values (`ActionIntent` as a record) | one heap object per actor per turn, every turn | `readonly record struct` |
| String keys in tick-path dictionaries | hashing cost per lookup per actor per frame; the repo already named ptr→string its top allocation source | integer actor handles; strings only at boundaries |
| Growing a `List`/`Dictionary` mid-battle | resize allocates and copies, unpredictably | pre-size to expected actor/event counts at battle start |
| LINQ on the tick path | enumerator + delegate allocation | explicit loops |
| `ToArray()` / defensive copies per tick | one array per call | caller-owned buffers (already the `PopDue` contract) |
| String interpolation, boxing, `params` arrays | classic silent sources | precomputed ids; the trace stays null in production |

**Diagnostics are exempt but must be inert.** `BattleTrace` allocates freely — it is null in production and every record site is null-conditional. That exemption is precisely why it must never become non-null by default.

## Bounded, resumable per-frame work

A frame must never be held hostage by a large drain. The precedent exists in this codebase and should be followed rather than reinvented: event-pipeline-v2 drains a ring with a **frame-budgeted** loop (10 % of frame, clamped) and resumes next frame.

The kernel's per-frame drive:

1. Advance the clock by the frame's ticks (carry-corrected — a truncating conversion loses 2.4 s/minute at 60 fps).
2. Drain due events **under a work budget**, into a reused buffer.
3. If the budget is exhausted with events still due, **stop and resume next frame** — never spiral.
4. Never scan the scene, resolve stats, or allocate inside the drain.

Because simulated time is decoupled from wall-clock, a deferred drain is a *pacing* effect, not a correctness one — the same tick order still results.

## Testing strategy

Two surfaces, because they catch different failures.

**CI — deterministic, fast, not flaky.** Wall-clock assertions in CI are noise; these are not:

- **Allocation assertions.** `GC.GetAllocatedBytesForCurrentThread()` around a warmed steady-state loop must be **zero**. This is the exact regression class that just bit this program.
- **Operation-count assertions.** Instrumented counters prove complexity: N reschedules must cost O(N log N) comparisons, not O(N²). A count assertion catches an algorithmic regression that a timing test would blame on the machine.
- **Structural growth assertions.** Live event count equals structure size after churn — the property that caught the lazy-deletion design.

**Live — the probe.** New `PerfProbe` sections (`kernel.tick`, `kernel.drain`, `kernel.schedule`) reported in the existing 5 s window alongside `loop.tick`, so the kernel's share is visible next to everything else and measured against B1–B9. Probe overhead stays under its own 0.05 ms budget: no dictionaries or strings on the record path.

## Structure

```
src/FusionRpg.Core/Battle/Timeline/          (struct intents, handle keys, pre-sized collections)
src/FusionRpg.Injector/Host/PerfProbe.cs     (kernel.* sections)
tests/FusionRpg.Core.Tests/Battle/Timeline/  (allocation + operation-count + growth assertions)
```

## Boundaries

- **Always:** zero steady-state allocation; O(log n) scheduler operations; bounded resumable per-frame work; caller-owned buffers; integer handles on tick paths.
- **Ask first:** raising the kernel's frame share above 0.15 ms; any per-frame Unity API call from the kernel; making a diagnostic non-null by default.
- **Never:** scene scans, stat resolves, or LINQ on the tick path; unbounded drains; the kernel scheduling the game's own actors; wall-clock timing assertions in CI.

## Success criteria

1. A warmed steady-state tick allocates **zero bytes**, asserted in CI.
2. Scheduler operations are provably O(log n) by counter, not by stopwatch.
3. The kernel's probe section holds ≤ 0.15 ms/frame at 200 entities in B2/B8.
4. No gen2 GC attributable to the kernel during a level.
5. The tick path contains no scene scan, stat resolve, or string allocation — enforced by the same source-scan guard that already covers kernel purity.
