# Spec: injector-kernel-drive

Module id `injector-kernel-drive` (T13) in the [battle timeline map](../battle-timeline-map.md).
Delivers todo items **B24** (this document), **B25** (per-frame drive + bounded drain, which delivers
**P1c**), **B26** (shield + DoT grids onto the kernel) and **B27** (probe sections, which delivers
**P1b** — ⛔ owner-run).

Inherits and does not restate: [spec-kernel-performance.md](spec-kernel-performance.md) (budgets,
allocation contract), [spec-virtual-time-core.md](spec-virtual-time-core.md) (clock, queue),
[../event-pipeline-v2-ssot.md](../event-pipeline-v2-ssot.md) (record-then-drain),
[../overlay-control-loops.md](../overlay-control-loops.md) (Hot loop authority).

**Status:** written 2026-08-31. **Not yet owner-reviewed — B24's own acceptance line is "spec reviewed
before any injector edit", so no code from B25/B26/B27 may land until that review happens.**

---

## Design gate checklist (§5 of [../../DESIGN-GATE.md](../../DESIGN-GATE.md))

```
[x] I identified the subsystem(s) this touches.
    — battle/turns, injector↔game, performance, effects (shield + DoT hosts).
[x] I read every doc in the §1 row(s) for those subsystems, this session.
    — software-architecture (index), decisions.md (Battle time model row),
      event-pipeline-v2-ssot.md, overlay-control-loops.md, battle-timeline-map.md,
      spec-kernel-performance.md, DESIGN-GATE.md §1/§2/§3/§5.
[x] I checked decisions.md for a lock covering this.
    — decisions.md:42 "Battle time model" locks tick = 1 ms, integer `long`, (dueTick, seq)
      ordering, and mode-as-data. Nothing below contradicts it; §5 below is written to satisfy it.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments.
[x] I read the surrounding section of every rule I quoted.
[x] I tested (not assumed) any constraint I am reporting.
    — "CI never builds the injector" is not assumed: `.github/workflows/ci.yml:62-77` runs ten
      `dotnet test` calls and none of them is an injector project; there is no `dotnet build` of
      `src/FusionRpg.Injector` anywhere in the workflow. This is the single load-bearing constraint
      behind §6's Core-vs-Injector split.
[x] Nothing contradicts a §2 invariant, or I named the contradiction explicitly.
    — §4 below names the one place this spec deliberately CHANGES today's behaviour (the DoT grid's
      discarded carry) and says how it is proven rather than asserted.
[x] Corrections are propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    — `battle-timeline-map.md` T13 now links here and is marked "awaiting owner review";
      `tasks/battle-timeline-todo.md` B24/B25 record what was built and what is still gated.
      B24 stays UNCHECKED on purpose — its acceptance is the review, which has not happened.
      One correction has already been propagated here from the build: §3.3's time source is
      required rather than defaulted, because the purity guard rejects a wall-clock default in
      this directory.
```

---

## 1. Objective

Today the injector runs **three independent timing mechanisms** on the same frame, none of which
knows about the others:

| Mechanism | Where | How it paces |
|---|---|---|
| Event pipeline v2 drain | `InjectorLoop.cs:79` → `EventDrainHost.Tick` | wall-clock budget, 10 % of frame clamped to [0.2 ms, 2 ms] (`EventDrainHost.cs:155`) |
| DoT / status pulse grid | `InjectorLoop.cs:80` → `EffectRuntime.TickDots` | float accumulator, fires at ≥ 0.1 s (`EffectRuntime.cs:361-363`) |
| Shield upkeep grid | `InjectorLoop.cs:82` → `EffectRuntime.TickShields` | float accumulator, `while` catch-up at 0.1 s steps (`EffectRuntime.cs:467-474`) |

The last two **are a primitive scheduler** — an accumulator, a fixed period, and a fire callback.
The battle-timeline program already shipped a real one (`SimulationClock` + `EventQueue`,
`src/FusionRpg.Core/Battle/Timeline/`), proved it O(log n) and allocation-free, and adopted it inside
`BattleEngine` at Checkpoint B. T13 applies the program's own SSOT argument to the injector: **one
scheduler, not three.**

Done means: the kernel ticks once per Unity frame under a bounded, resumable work budget; the two
100 ms grids are gone, replaced by scheduled events with identical player-visible behaviour except
where §4 names a deliberate fix; and the kernel's cost is visible as its own `PerfProbe` sections
measured against the B1–B9 matrix.

### Who this is for

Nobody sees this feature. Its user-visible success condition is that **nothing changes** — no stutter,
no altered DoT cadence, no shield that expires a frame early. That is why every acceptance criterion
below is a substitution test against current behaviour rather than a new capability.

---

## 2. Scope

| In scope | Out of scope |
|---|---|
| A per-frame drive: advance the clock, drain due events, resume next frame | Scheduling PvZ's own actors (T7 stays a stateless projection) |
| Moving `TickDots`' status/DoT pulses onto scheduled events | Changing what a DoT pulse *does* — the damage path is unchanged |
| Moving `TickShields`' regen/expiry/prune onto scheduled events | Shield math, absorb order, the element matrix |
| `kernel.tick` / `kernel.drain` / `kernel.schedule` probe sections | New probe infrastructure — `PerfProbe` already exists |
| Backpressure policy when the drain cannot keep up | Interactive battles (T6), forecast (T8), decision trace (T10) |

**Explicitly staying where they are** — these are *not* simulation timing and moving them would be
scope creep: the v2 event drain (`InjectorLoop.cs:79` — a different pipeline, game events rather than
our timeline), the network cadence timers (heartbeat 2 s, command pull 0.25 s, perf flush, cheat push
— `InjectorLoop.cs:84-107`), `VfxDirector.Tick` (`InjectorLoop.cs:74`), and the FPS cap
(`InjectorLoop.cs:123`).

---

## 3. The drive

### 3.1 Frame order

The kernel tick is inserted **between** the v2 drain and the shield grid it replaces, preserving the
existing ordering rule recorded at `EffectRuntime.cs:453-456` (shield upkeep runs after the frame's
dispatch and DoTs so an expiring shield still absorbs its final frame's damage):

```
EventDrainHost.Tick          (unchanged — game events)
KernelDriveHost.Tick         (NEW — advances the clock, drains due timeline events)
  ├─ status/DoT pulse events (was EffectRuntime.TickDots)
  └─ shield upkeep events    (was EffectRuntime.TickShields)
Hud.ActorHudCache.ReconcileDirty   (unchanged)
```

### 3.2 Advancing the clock — and why not by frame count

`FixedIncrementAdvance` (`SimulationClock.cs:49-78`) advances by a **fixed ticks-per-frame ratio**
with an integer carry, and its own doc comment names why the carry exists: truncating 1000/60 to 16
loses 2.4 s per minute.

That is the right mechanism and the wrong input for this module. Both grids being replaced accumulate
**`unscaledDeltaTime`** — measured wall time (`EffectRuntime.cs:361`, `:467`), reached from
`InjectorLoop.TickFromUnity` (`InjectorLoop.cs:156`). A nominal-60 fps drive would make every DoT and
shield tick run fast or slow by exactly the frame-rate error, which fails B26's acceptance ("behaviour
identical to the grids they replace") on any machine that is not holding 60 fps — i.e. the weak-PC
case the whole event-pipeline-v2 contract exists for.

**Decision: the drive advances by measured unscaled wall time, in integer microseconds.**

- Unity hands us a `float` seconds value. It is converted **once, at the injector boundary**, to whole
  microseconds as a `long`, and no floating-point value crosses into Core. This satisfies
  `SimulationClock`'s own stated contract ("no floating-point value reaches it",
  `SimulationClock.cs:81-83`) and keeps the new Core file green under `KernelPurityScan`'s
  floating-point ban (`tests/FusionRpg.Core.Tests/Battle/Timeline/KernelPurityScan.cs:58`).
- A new `ITimeAdvance` implementation carries the **sub-millisecond remainder in integer
  microseconds**, exactly as `FixedIncrementAdvance` carries its fractional tick. 1 tick = 1 ms is
  unchanged, so `decisions.md:42` is satisfied.
- The overflow refusal `FixedIncrementAdvance` already performs (`SimulationClock.cs:70-71`) is
  reproduced rather than dropped: overflow throws, per the repo's magnitude rule.

### 3.3 Bounded, resumable drain

`EventQueue.PopDue` (`EventQueue.cs:132-146`) drains **every** due event in a `while` loop with no
budget — the gap `tasks/battle-timeline-todo.md` P2 recorded and could not reveal at 2.8 events/frame.

The bounded form follows `EventDrain` rather than reinventing one, because that is this repo's proven
precedent and it is already testable without a stopwatch:

| Property | `EventDrain` today | The kernel drain |
|---|---|---|
| Budget unit | `Stopwatch` timestamp ticks (`EventDrainHost.cs:156`) | same |
| Time source | injected `Func<long>?`, defaulting to `Stopwatch.GetTimestamp` (`EventDrain.cs:113-116`) | injected and **required — no default** |
| Starvation guard | `stats.Processed > 0 &&` before the budget check (`EventDrain.cs:197`) | same — at least one event always drains, so the drive can never spiral on a single expensive event |
| Leftovers | carried to the next frame | same |
| Buffer | caller-owned | same — `PopDue`'s existing contract (`EventQueue.cs:127-131`) |

**Why the time source is required rather than defaulted** — found by building it, not by reasoning:
`EventDrain` can afford `?? Stopwatch.GetTimestamp` because it lives in `Core/Events/`. This drive
lives in `Core/Battle/Timeline/`, which `TimelinePurityGuardTests` scans for wall-clock references
**with no file exempt**. A convenience default there is a red guard. Making the host name its own
clock is the honest fix, and it has a second benefit: no test can accidentally read a real clock and
end up measuring the build agent.

**Backpressure — the one rule that is genuinely new.** Under sustained overload the v2 drain lets a
backlog grow and drops droppable kinds. The kernel must not: a dropped shield expiry or DoT pulse is a
correctness bug, not a lost telemetry row. Instead, **the clock does not advance while a carry
exists.** Simulated time slows down; ordering is untouched because every event still fires in
`(DueTick, Seq)` order; and nothing is ever dropped.

This is the kernel's expression of the pipeline's own G5 ("worst case degrades to *delayed effects*,
never to frame drops" — `event-pipeline-v2-ssot.md:36`), and it is licensed by the perf spec's own
statement that a deferred drain is "a *pacing* effect, not a correctness one"
(`spec-kernel-performance.md:68`). It is stated here as a rule because the licence alone does not tell
an implementer to gate the *advance*, which is where the unbounded-backlog failure actually lives.

### 3.4 Two defects in today's grids that the substitution closes

Both were found by reading the two accumulators side by side. Neither is a reason to do T13, and
neither is asserted as harmful without measurement — they are recorded so B26 does not rediscover them
as surprises mid-build.

1. **The DoT grid discards its overshoot.** `EffectRuntime.cs:363` sets `_dotAccum = 0` after firing,
   where the shield grid subtracts (`_shieldAccum -= 0.1f`, `EffectRuntime.cs:471`). The DoT grid
   therefore drifts *slow* by the per-fire overshoot and can never catch up after a long frame; the
   shield grid is carry-correct and does catch up. The kernel is carry-correct by construction, so
   adopting it changes DoT cadence — slightly, in the direction of correctness.
2. **The shield grid's catch-up is unbounded.** `EffectRuntime.cs:469-474` is a `while` loop with no
   cap: after a 2 s hitch it runs 20 shield ticks inside one frame, on the Unity main thread. This is
   precisely the per-frame spike the bounded drain exists to prevent.

**Neither is claimed to move a golden, because that has not been tested.** Both grids are *injector*
paths; `BattleEngine` got its own event-driven status/shield upkeep at T9 (Checkpoint B2,
`decisions.md:42`), and `tools/CombatSim` runs a separate `StatusModel.cs` that never sees either.
The prediction is therefore "zero goldens move", and **B26's acceptance requires running the suites to
confirm it, not assuming it** — the same discipline the T9 and action-selection rows in
`decisions.md` both applied. If a golden does move, that is a `RulesetVersion` bump with a
predicted-delta writeup, per the trigger condition already recorded in the Battle time model row.

---

## 4. Boundaries this module must not cross

These are restated inline rather than linked, because a downstream session reads this file and not its
links.

- **The kernel schedules OUR timeline; Unity still owns when its own actors act.** A zombie's walk and
  a pea's flight are never scheduled here. PvZ stays observed (`spec-kernel-performance.md:35-36`).
- **The RPG never reads PvZ's current state and never guesses it** — it observes past events and
  contributes signed deltas later (`DESIGN-GATE.md` §1, "Where logic may live").
- **Every HP delta still flows Secondary → Funnel → FA10 Add.** Moving a DoT pulse's *trigger* onto
  the kernel changes when it fires and nothing else; the packet, the shield gate, and the Funnel are
  untouched (`overlay-control-loops.md:104`, `:150-151`).
- **Single writer.** Combat writes stay in `EntityStatWriter`. This module writes no stat.
- **No scene scan, stat resolve, LINQ, or allocation on the drain path**
  (`spec-kernel-performance.md:44-53`, `:94`).
- **Never await** SignalR, HTTP or SQLite anywhere in the drive (`overlay-control-loops.md:147`).

---

## 5. Numeric types and tunables

- **Ticks are `long`**, 1 tick = 1 ms, per `decisions.md:42`. The microsecond accumulator is `long`.
  No `float` and no `double` past the injector boundary conversion.
- **Overflow throws**, reproducing `SimulationClock.cs:70-71`. No silent `unchecked`, no clamp.
- **The work budget is a per-frame runtime cap, which `tunables-ssot.md` §1 classes as structural, not
  a balance number** — so it is a `const` with a comment naming its class, in the same way
  `ReactionLane.DepthLimit` was resolved (`tasks/battle-timeline-todo.md` B6). It is not a progression
  ceiling and the no-hard-caps rule does not apply; the comment must say so, because a future caps
  sweep will otherwise find a `Math.Min` here and have to re-derive this.
- The budget's shape follows `EventDrainHost.cs:155` — a fraction of measured frame time, clamped —
  sized against the kernel's own **0.15 ms/frame** share from `spec-kernel-performance.md:29`, not the
  event drain's 2 ms.

---

## 6. Structure

**Everything testable lives in Core. The injector file is an adapter and nothing else.** This is not
style preference: `.github/workflows/ci.yml:62-77` runs ten test projects and never builds
`src/FusionRpg.Injector`, so any logic placed there is untested by CI forever. The same split was
applied for the same reason when the aura program extracted `EntityWriteGate` and
`GrantedDerivedAtomReader` into Core.

```
src/FusionRpg.Core/Battle/Timeline/
  DeltaTickAdvance.cs        NEW — ITimeAdvance over integer microseconds, carry-preserving
  TimelineDrive.cs           NEW — advance + bounded drain + carry; injected Func<long> timestamp
  EventQueue.cs              EDIT — bounded PopDue overload (the unbounded one stays for BattleEngine)

src/FusionRpg.Injector/Effects/
  KernelDriveHost.cs         NEW — float→long micros at the boundary; owns the scratch buffer
  EffectRuntime.cs           EDIT — TickDots/TickShields bodies become scheduled handlers (B26)

src/FusionRpg.Injector/Host/
  InjectorLoop.cs            EDIT — one call, between the v2 drain and HUD reconcile

src/FusionRpg.Core/Diagnostics/
  PerfProbe.cs               EDIT — kernel.tick / kernel.drain / kernel.schedule sections (B27)

tests/FusionRpg.Core.Tests/Battle/Timeline/
  TimelineDriveTests.cs      NEW — carry, budget, backpressure, allocation, ordering
```

`PerfSection` is a contiguous enum whose count is asserted structurally
(`PerfProbe.cs:42`, `const int SectionCount = 21`). Three new sections make it 24, and **that constant
and the `SectionNames` array must move in the same edit** — they are two halves of one declaration.

---

## 7. Testing strategy

Per `spec-kernel-performance.md:70-80`, two surfaces catching different failures.

**CI — deterministic, in `FusionRpg.Core.Tests` (the only place CI will run it):**

| Test | Proves |
|---|---|
| Carry over 10 000 variable-length frames | Total simulated time equals total input time exactly — the drift class `FixedIncrementAdvance` was written to kill |
| Budget exhausted mid-drain | Remaining events carry; the next drain resumes in `(DueTick, Seq)` order; **the same total set fires as an unbudgeted drain** |
| One event costing more than the whole budget | Still drains — the `Processed > 0` starvation guard (`EventDrain.cs:197`), proven by making it fail |
| Backpressure | With a carry outstanding, the clock does not advance; once drained, it does |
| Allocation | `GC.GetAllocatedBytesForCurrentThread()` is **zero** across a warmed steady-state drive loop |
| Purity | The new Core files are scanned by `KernelPurityScan` with **no exemption** — no wall clock, no RNG, no float, no dictionary enumeration |
| Substitution (B26) | A scheduled DoT/shield sequence produces the same pulse count and the same ordering as the grid it replaces, over a scripted frame-time sequence including a 2 s hitch |

The stopwatch is injected, never read, in every one of these — a wall-clock assertion in CI measures
the build agent (`spec-kernel-performance.md:74`).

**Live — ⛔ owner-run (B27).** Deploys and stress scenarios are the owner's. `kernel.*` sections
reported in the existing 5 s window beside `loop.tick`, measured against B1–B9. Acceptance: kernel
share ≤ 0.15 ms/frame avg at 200+ entities, injector total still within the ≤ 2 ms stress budget, no
gen2 GC during a level, allocation rate unchanged versus the pre-T13 baseline
(`docs/research/perf/kernel-timeline-baseline.md`).

---

## 8. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~TimelineDrive"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~KernelPurity"
dotnet test tests\FusionRpg.Core.Tests          # full — the substitution must move no golden
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-secondary-no-unity.ps1
.\scripts\guard-dal.ps1
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --summary

# injector build — local only; CI never does this (ci.yml:62-77)
$env:FUSIONRPG_ML_GAMEDIR = "<MelonLoader pack>"
dotnet build src\FusionRpg.Injector
```

---

## 9. Boundaries (always / ask first / never)

- **Always:** zero steady-state allocation on the drive path; caller-owned buffers; integer ticks;
  the injected timestamp seam so CI never reads a real clock; the drain resumes rather than spirals.
- **Ask first:** raising the kernel's frame share above 0.15 ms; any per-frame Unity API call from the
  drive; moving a fourth timer onto the kernel that this spec did not scope; anything that would make
  the drain drop an event rather than defer it.
- **Never:** scheduling PvZ's own actors; a scene scan, stat resolve, or LINQ on the drain path; an
  unbounded drain; a wall-clock assertion in CI; awaiting anything in the drive.

---

## 10. Success criteria

1. One scheduler in the injector — `grep` finds no surviving accumulator-plus-period grid in
   `EffectRuntime.cs`.
2. A deliberately oversized backlog never blows a frame: it drains across frames, in unchanged tick
   order, with nothing dropped — asserted, not observed.
3. Zero bytes allocated in a warmed steady-state drive frame, asserted in CI.
4. Shield and DoT behaviour matches the grids they replace, **with the two §3.4 differences named,
   predicted, and then measured** — existing suites unedited.
5. `kernel.*` probe sections hold ≤ 0.15 ms/frame at 200+ entities in the owner's B2/B8 run.
6. Total simulated time equals total real unscaled time over a long variable-rate run — no drift in
   either direction.

---

## 11. Open questions (owner)

1. **Does the kernel's clock pause when the game pauses?** The grids read `unscaledDeltaTime`, so they
   run through the pause menu today — and DoTs deliberately use unscaled time so game speed does not
   accelerate them (`event-pipeline-v2-ssot.md:173-175`). Keeping unscaled is the byte-identical
   choice and is what this spec assumes; it is called out because "the battle clock keeps running
   while paused" is a gameplay statement, not just an implementation one.
2. **Is one kernel instance per board correct, or one per match?** This spec assumes **one per
   board**, torn down at `board.end` alongside the existing `ClearAll` lifecycle barrier
   (`event-pipeline-v2-ssot.md:139-141`). Nothing shipped needs a timeline that outlives a board, so
   this is the cheap default rather than a researched conclusion.

Neither blocks B25. Question 1 blocks B26's acceptance wording, and question 2 blocks nothing until a
second board can exist.
