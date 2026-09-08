# Spec: turn-order-forecast

Module id `turn-order-forecast` (T8) in the [battle timeline map](../battle-timeline-map.md). Depends
on T1 `virtual-time-core` only. **Written 2026-09-04** to satisfy B19's own *"spec first"* gate.

## Objective

Answer "who acts next, and in what order" **without changing anything**. The queue already holds that
answer; a forecast is a read of it, not a second model of it.

The map's framing: *"Pure projection of the queue: roll the queue forward `K` events with no mutation,
render the rail. Cheap, and it validates that the queue really is the single source of truth."* That
last clause is the real value — if a forecast needs its own ordering logic, the queue was not the SSOT
after all.

## Design

### 1. It is a read, and "no mutation" is the acceptance

`Forecast(queue, k)` returns up to `k` events in the exact order the queue would pop them, and the
queue is **observably unchanged** afterwards: same `Count`, same `PeekDueTick`, and a subsequent real
drain yields exactly what the forecast said it would.

**Mechanism: a copy, not a peek loop.** A binary heap's array order is not pop order, so "read the
first `k` slots" would be wrong — only the root is guaranteed. Popping `k` times from a **copy** of the
heap is the only way to get true pop order without touching the original, and it costs
`O(k log n)` on a structure the caller already owns.

**Tombstones are honoured.** Cancellation is by tombstone (`spec-virtual-time-core.md`), so a forecast
that ignored them would predict events that will never fire — the most misleading possible output for
a UI rail.

### 2. Per-profile exactness — stated, because a forecast that overpromises is worse than none

| Profile | Exactness | Why |
|---|---|---|
| `galaxy-sync` | **Exact** | Next-event advance, one actor at a time: nothing between now and the `k`th event can insert ahead of it |
| `classic-round` | **Exact**, trivially | Same next-event advance |
| `hybrid-atb` | **Soft-bounded** | Fixed-increment advance with `W > 1`: an action resolving inside the window can schedule a new event that lands ahead of a forecast entry. The projection is still the queue's current truth, and it is still useful — it is just not a promise |
| Real-time / PvZ observer | **Absent** | We do not own that clock (`battle-turn-ideal.md` §1). There is nothing to project |

The forecast **does not simulate**. It does not run readiness, resolve actions, or apply effects — it
reports what is scheduled. That is the whole point: anything more would be a second engine, and the
two would drift.

### 3. What it deliberately is not

- **Not a scheduler.** It never calls `Schedule`, `Cancel` or `Reschedule`.
- **Not a predictor.** It does not roll dice or advance readiness. An actor still `Charging` has no
  scheduled event yet and simply does not appear.
- **Not a renderer.** It returns events; drawing a rail is the UI's problem (`game-gui-principles.md`).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Forecast"
```

## Structure

```
src/FusionRpg.Core/Battle/Timeline/TurnOrderForecast.cs   (the projection)
tests/FusionRpg.Core.Tests/Battle/Timeline/TurnOrderForecastTests.cs
```

## Testing strategy

1. **The forecast matches a real drain**, event for event — the acceptance the map names, and the only
   test that proves the queue is genuinely the single source of truth.
2. **The queue is unchanged**: `Count` and `PeekDueTick` identical before and after, and forecasting
   twice returns the same answer. A projection that mutates is the one defect this module could
   plausibly ship.
3. **Tombstones**: a cancelled event never appears.
4. **Ordering under ties** follows `(DueTick, Seq)`, matching the queue's own total order.
5. **`k` is a bound, not a promise**: fewer events than `k` returns what exists; `k = 0` returns empty;
   a negative `k` is refused rather than silently treated as 0.
6. **Zero allocation on the caller's buffer path** — the drain-style overload fills a caller-owned
   list, matching `PopDue`'s existing shape, because the kernel's callers are frame-critical.

## Boundaries

- **Always:** leave the queue untouched; honour tombstones; keep the `(DueTick, Seq)` order.
- **Ask first:** extending the forecast to *predict* unscheduled arrivals (that is simulation, and it
  needs the readiness driver, not this module).
- **Never:** mutate the queue; duplicate the ordering rule; render anything.

## Success criteria

1. The forecast equals the subsequent real drain under next-event advance. 2. The queue is provably
unchanged. 3. Cancelled events never appear. 4. `k` is bounded and a negative `k` is refused.
5. No scheduling API is called anywhere in the module.
