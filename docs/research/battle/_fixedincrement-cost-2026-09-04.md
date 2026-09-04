# FixedIncrement vs NextEvent — resolve cost, measured

**battle-timeline B32 (T15 gate b).** Measured 2026-09-04. The question this gate exists to answer:
`hybrid-atb` is the only shipped profile on `FixedIncrement` advance, and T15 moves expeditions and
web matches onto it. Does stepping the clock instead of jumping between events make a server-side
resolve materially more expensive?

**Answer: no. 1.2× wall-clock, 1.0× allocation. The pre-agreed `galaxy-sync` fallback is not needed.**

## The estimate this replaces was wrong by ~17×

`spec-profile-migration.md` §5 and `battle-timeline-todo.md` B32 both carried this figure, explicitly
labelled an estimate:

> A 50-round battle at `roundDurationMs = 1000` is on the order of **50,000 clock steps** against a
> few hundred event pops.

That assumed `FixedIncrement` advances one tick at a time. It does not.
`FixedIncrementAdvance.NextAdvance(now, queue, frames)` advances **frames × ticks-per-frame**,
carry-corrected — at 60 fps that is 1000/60 = 16.667 ms per step. So the real figure is **3,000
steps**, not 50,000, and the driver chooses `frames`, so even that is an upper bound.

## Method

Kernel-level, and deliberately so: the clock and the queue are the *only* things that differ between
the two advance policies, so measuring them measures the difference. No tuning hubs are involved,
which is also what let this run while `FusionRpg.Data` was failing to build (see Caveats).

Battle shape: **50 rounds × 6 actors = 300 events over 50,000 ticks** — `battle.v{n}.json`'s own
`maxRounds: 50` and `roundDurationMs: 1000`. 200 iterations per policy, 3 warm-up runs, GC settled
before each measurement, allocation via `GC.GetAllocatedBytesForCurrentThread()`.

## Results

| Policy | ms/battle | bytes/battle | clock steps | events drained |
|---|---:|---:|---:|---:|
| `NextEvent` (jump) | **0.2911** | 49,984 | 50 | 300 |
| `FixedIncrement`, 1 frame/step | **0.3442** | 50,000 | 3,000 | 300 |
| `FixedIncrement`, 4 frames/step | **0.3084** | 50,000 | 750 | 300 |

**Ratio at the worst setting: 1.2× time, 1.0× bytes.**

Scaled to the two cases the gate names:

| Workload | `NextEvent` | `FixedIncrement` | Delta |
|---|---:|---:|---:|
| One expedition (4 battles) | 1.165 ms | 1.377 ms | **+0.2 ms** |
| A 500-match boot sweep | 145.6 ms | 172.1 ms | **+26 ms** |

## Why the extra 2,950 steps cost so little

A clock step that drains nothing is an integer add plus a heap peek. It allocates nothing — the
identical byte counts are the evidence, not an assumption. The 50 KB both policies allocate is the
event set itself (300 scheduled events plus the queue's backing store), which is a property of the
battle, not of how time advances.

Batching frames removes even the small gap: 4 frames/step is 750 steps and 0.3084 ms. The driver
picks `frames`, so if this ever mattered it is a driver parameter, not a profile problem.

## Conclusions

1. **T15 proceeds on `hybrid-atb` for both surfaces.** The measured cost does not justify a fallback.
2. **The `galaxy-sync`-for-expeditions fallback is not needed**, which also means the per-surface
   profile axis it would have required (`WaveDef.Profile` is shared by expeditions and web matches —
   `WebMatchService.cs:39,50`) does not need building. That was scoped as a *change*, not a flag flip;
   it is now moot.
3. **The estimate in the spec and the todo is corrected**, not quietly dropped — both said "never
   measured", and this is the measurement.

## Caveats, stated rather than buried

- This measures the **advance mechanism**, not a full `BattleEngine.Resolve` under `hybrid-atb`. Two
  reasons: the mechanism is the only thing that differs between the policies, and a full resolve needs
  22 tuning hubs plus `FusionRpg.Data`, which was failing to build on another stream's in-flight
  refactor at the time (`RpgStore.Items.cs` calling `RarityLadder.Rungs`, which does not exist yet).
- **A full-resolve measurement under `hybrid-atb` is still worth taking during T15 itself**, because
  it would exercise `W = 4`, `EarlyBoundWithFallback` and the ActionPoints economy — none of which is
  an advance-policy cost, but all of which are new production paths. That belongs to B34's staged
  sweep, which runs those configurations anyway.
- Wall-clock numbers are from one machine and are only meaningful as a **ratio**; the ratio is what
  the decision rests on, and the allocation figures are exact.
