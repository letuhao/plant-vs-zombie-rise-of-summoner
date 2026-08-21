# Spec: virtual-time-core

Module id `virtual-time-core` (T1) in the [battle timeline map](../battle-timeline-map.md). Depends on nothing. Ideal: [battle-turn-ideal.md](../battle-turn-ideal.md). **Amended 2026-08-21** after the [structured review](audit-2026-08-21.md) — dilation deleted, `TryAdvance` replaces `Advance`, unit claim corrected.

## Objective

The simulation clock and the Future Event List — the two pieces that make turn-based and real-time the same architecture. Pure: no actors, no battle, no game. Everything downstream schedules on this.

## Design (locked on approval)

### The tick

- **1 tick = 1 millisecond**, stored as `long`. Chosen because most durations the codebase persists are in ms (`RoundDurationMs`, status `PeriodMs`/`DurationMs`, shield `DurationMs` at the content boundary). `long` ms overflows in ~292 million years.
- **Integer only.** No `double` reaches scheduling math — floating-point is the documented root cause of replay desync.

**Correction (audit):** 1 tick = 1 ms does **not** mean "no unit translation anywhere." Two subsystems this kernel drives speak other units, and adapters must translate:

| Subsystem | Its unit | Consequence |
|---|---|---|
| `ShieldRuntime` | **round-ticks** (`BattleEngine.cs:182-183` ceilings ms→rounds; `Tick` takes a round index) | Its regen carry is `ratePm × deltaMs / 1000`, which **truncates to zero** for any rate below 1000‰ if driven at 1 ms. Shield regen would silently vanish. |
| `StatusRuntime` | `DateTimeOffset` (`ExpiresAt`, `NextPulse`) | Adoption maintains a `DateTimeOffset` shadow clock beside the tick clock. The ms→`DateTimeOffset` conversion is exact (100 ns resolution), so no precision is lost. |

### The clock

`SimulationClock` holds `Now` (ticks). It cannot read the wall clock.

```
TryAdvance(queue) → Advanced | Blocked(reason)
```

**`TryAdvance`, not `Advance`** (audit). A jumping clock has no wall-time to dwell in, so an interactive battle under next-event advance would leap straight past its input window. The clock must be able to report that it *cannot* advance because something external is pending. This is a signature decision, not an implementation detail — it has to exist before anything calls it.

Two advance mechanisms, the DES-standard pair:

| Mechanism | Behavior | Serves |
|---|---|---|
| `NextEventAdvance` | `Now = queue.PeekDueTick()` | turn-based |
| `FixedIncrementAdvance` | `Advance(long frames)` — frames × tickPerFrame, carry-corrected | real-time, hybrid |

**Dilation is deleted** (audit D1). It lived in the clock while the ideal's own §4 says pacing is *"a playback decision, not a simulation one."* Server resolution is instantaneous and never needs it; slow-motion input windows belong to the session layer. This removes the rational arithmetic and its carry from the kernel entirely.

**Frame→tick carry stays, and it is not optional.** At 60 fps a frame is 16.667 ms. With `Δ = 16` the clock loses 0.667 ms/frame — **40 ms per second, 2.4 seconds of drift per minute**. The conversion therefore carries its remainder *inside* the clock, and the signature takes **`long frames`** — never a float, and never a millisecond count derived from one.

### The Future Event List

```
ScheduledEvent { long DueTick; long Seq; string OwnerKey; int Kind; long Tag; }
```

`Kind`/`Tag` are opaque here — T2 assigns meaning — which is what keeps this module testable with no game attached.

- **Total ordering is `(DueTick, Seq)`**, `Seq` a monotonic insertion counter. Never dictionary enumeration order. *(A live instance of order leaking from `Dictionary` internals into report bytes was found and fixed 2026-08-21.)*
- Operations: `Schedule`, `Cancel(handle)`, `Reschedule(handle, newDueTick)`, `PeekDueTick`, `PopDue(now, buffer)` into a caller-owned buffer.
- **Cancellation is by handle with tombstones**, skipped on pop, and must not renumber `Seq`.
- **`Reschedule` preserves `Seq`** (audit). T1 previously forbade *cancel* from renumbering but was silent on reschedule. Implemented as cancel+insert with a fresh `Seq`, a delay effect would silently reorder unrelated actors and make every golden history-dependent.
- **No allocation on either hot path** — the drain uses caller-owned buffers, and `Schedule` uses pooled records or a struct heap. The boot sweep re-resolves every unresolved match at server start, and expeditions resolve four battles each.

### Non-negotiables

No `DateTime`, `Random`, `float`/`double`, or dictionary-order iteration. The purity guard is a reflection scan — **extended to callers**, because a float entering at the call site (`Advance((long)(deltaTime * 1000))`) left the guard green while enforcing the invariant exactly where it was not at risk.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~VirtualTime"
```

## Structure

```
src/FusionRpg.Core/Battle/Timeline/SimulationClock.cs   (Now, TryAdvance, frame carry)
src/FusionRpg.Core/Battle/Timeline/TimeAdvance.cs       (NextEvent / FixedIncrement)
src/FusionRpg.Core/Battle/Timeline/EventQueue.cs        (FEL: heap, handles, tombstones)
src/FusionRpg.Core/Battle/Timeline/ScheduledEvent.cs
tests/FusionRpg.Core.Tests/Battle/Timeline/             (clock, queue, determinism, purity guard)
```

## Testing strategy

Clock: next-event jumps exactly to the next due tick; fixed-increment over 10 000 frames at 60 fps drifts **zero** ticks (the carry test); `TryAdvance` returns `Blocked` when the queue is empty and something external is pending. Queue: pop order under equal `DueTick` follows insertion; cancel removes without disturbing others; **reschedule preserves `Seq`** — asserted by rescheduling one event and proving unrelated tie-break order is unchanged; cancelling a fired handle is a no-op. Determinism: the same op script replays identically. Purity: the reflection guard covers the module *and* its call sites.

## Boundaries

- **Always:** integer ticks; total ordering by `(DueTick, Seq)`; caller-owned drain buffers; allocation-free schedule and drain.
- **Ask first:** changing the tick unit from 1 ms — it re-units every stored duration.
- **Never:** wall-clock reads, RNG, floating-point (including at call sites), or dictionary-order iteration; a queue whose ordering depends on cancellation or reschedule history; dilation in the kernel.
- **Out of scope, stated so nobody infers otherwise from `W = N`:** simultaneous resolution. The kernel is strictly sequential — total ordering means one actor always resolves first, so no profile can produce "both die." A `SimultaneousBatch` pop is a kernel feature, not a profile row, and is not in this program.

## Success criteria

1. Both advance mechanisms drive the same queue with no branching in the queue. 2. Zero tick drift over a long fixed-increment run. 3. `TryAdvance` can report `Blocked`. 4. The purity guard is green across module and callers. 5. Zero references to battle, actors, or the game.
