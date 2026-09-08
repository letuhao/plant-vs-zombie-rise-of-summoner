# Spec: pvz-observer (T7)

Module id `pvz-observer` (T7) in the [battle timeline map](../battle-timeline-map.md). **Written
2026-09-04** to satisfy B23's *"spec first"* gate. Sequenced last in the program, deliberately: it is
the only module that touches the injector hot path.

## Objective

Make a live PvZ lawn *describable* in the same vocabulary as an owned-clock battle, so telemetry, VFX
and the turn-order forecast speak one language across all modes.

**An adapter, not a scheduler.** The map is explicit: *"we never pretend to schedule PvZ."* The Unity
game owns that clock (`battle-turn-ideal.md` §1), and this module never schedules, never advances a
clock, and never holds a queue or a per-actor machine injector-side.

## Design

### 1. It is a pure projection, and that is what makes it safe

`PvzObserverProjection.Project(observed) → TurnState`. No state, no allocation, no dictionary — one
observed fact in, one vocabulary word out.

Statelessness is the whole safety argument. A stateful observer on the injector's hot path would need
a per-entity map, which is exactly the per-hit `FindObjectsOfType`-shaped cost the 2026-08 perf audit
already had to remove once. A pure function cannot acquire that cost by accident.

### 2. The mapping, and where it deliberately loses information

| Observed on the lawn | Projected | Why |
|---|---|---|
| Entity spawned, not yet acting | `Charging` | It exists and will act; nothing is committed |
| Mid-attack / mid-animation | `Resolving` | PvZ resolves continuously; there is no distinct commit |
| Post-attack cooldown | `Recovering` | The one PvZ concept that maps cleanly |
| Idle and able to act | `Ready` | Able, not scheduled — we are not claiming a turn |
| Dead | `Dead` | Terminal |
| Removed / despawned | `Withdrawn` | Terminal, distinct from death |

⛔ **`Committed` is never projected**, and that is a finding rather than an omission. `Committed` means
*intent locked, wind-up running* — a turn-based concept. PvZ has no observable moment between "decided"
and "resolving", so projecting it would be inventing a fact the lawn cannot supply. **The vocabulary is
shared; the coverage is not**, and pretending otherwise would make a forecast over live PvZ look
meaningful when it is not.

### 3. Exactness is `Absent`, and it is already declared

`ForecastExactness.Absent` exists for this (T8, B19): *"We do not own the clock, so there is nothing to
project."* An observed lawn has no scheduled future to roll forward — only a present to describe.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~PvzObserver"
```

## Structure

```
src/FusionRpg.Core/Battle/Timeline/PvzObserverProjection.cs
tests/FusionRpg.Core.Tests/Battle/Timeline/PvzObserverProjectionTests.cs
```

The projection lives in **Core, not the injector** — CI never builds injector projects, so logic
placed there is untested forever. The injector calls it; it holds no Unity type.

## Testing strategy

1. **Every observed kind maps to exactly one state**, asserted per kind rather than in aggregate.
2. **Zero allocation** over a long observe loop, measured with
   `GC.GetAllocatedBytesForCurrentThread()` and a liveness assertion so zero cannot be trivially true.
3. **`Committed` is never produced** — the shared-vocabulary honesty check.
4. **Terminal states are terminal**: `Dead` and `Withdrawn` project as such and are recognised by
   `TurnTransitions.IsTerminal`, so an observed lawn agrees with the kernel about what "gone" means.
5. **No scheduling API is reachable** from this file — asserted by the existing kernel purity scan,
   which already bans wall clock, RNG, floating point and dictionary enumeration here.

## Boundaries

- **Always:** keep it pure and allocation-free; project only what the lawn can actually supply.
- **Ask first:** projecting `Committed`, which would require inventing a lawn concept.
- **Never:** schedule, advance a clock, hold a queue or a per-actor machine injector-side.

## Success criteria

1. Every observed kind projects to one state, and `Committed` is never among them. 2. Zero allocation
on the observe path. 3. Terminal states agree with `TurnTransitions.IsTerminal`. 4. The module holds no
Unity type and no scheduling call.

## Out of scope, and owner-run

**The live frame-budget verification** (*"the documented frame budget holds at 200+ entities"*) needs a
deploy and a stress scenario, which are the owner's — the same boundary B27 already carries. This
module ships the projection and its allocation proof; the live number is measured with B27's probe run.
