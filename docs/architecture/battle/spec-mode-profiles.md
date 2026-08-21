# Spec: mode-profiles

Module id `mode-profiles` (T4) in the [battle timeline map](../battle-timeline-map.md). Depends on `virtual-time-core`, `turn-fsm`, `readiness-model`.

## Objective

Make a battle mode **data**. A profile binds the four knobs — time advance, concurrency width, commitment, readiness — plus its turn economy, and that is the entire definition of a mode. This module is where the central claim of the ideal gets tested: if a mode cannot be expressed as a row, the abstraction is wrong, and T4 is much cheaper to fail at than T7.

## Design (locked on approval)

### The profile

```
BattleModeProfile {
  string Id;
  ITimeAdvance Advance;          // NextEvent | FixedIncrement(framesToTicks)
  int ConcurrencyWidth;          // W
  WScope WScope;                 // Global | PerSide
  long PassQuantumTicks;         // reschedule delay when no legal intent exists
  Commitment DefaultCommitment;
  IReadinessFunction Readiness;
  ITurnEconomy Economy;
  long? RoundQuantumTicks;       // set only by round-structured profiles
}
```

`WScope` exists because a single global integer cannot express "one actor per side, alternating" — arguably what Galaxy Online actually is. `InputPolicy` is **not** a field here: a record slot with one legal value and a `// none today` comment is a placeholder, so it lands with T6 when it has meaning.

### The three shipped profiles

| Id | Advance | `W` | Commitment | Readiness | Shape |
|---|---|---|---|---|---|
| `classic-round` | next-event, 1000-tick quantum | N | late-bound | `Constant` | today's engine, exactly |
| `galaxy-sync` | next-event | **1** | early-bound | `SpeedScaled` | one actor acts, everyone watches |
| `hybrid-atb` | fixed-increment + dilation | N | late-bound | `SpeedScaled` | FF15 feel, still turn-based underneath |

Commitment is not arbitrary per row. `galaxy-sync` is early-bound because a declared turn-based action should land where it was declared; `hybrid-atb` is late-bound because action-feel combat re-targets on resolve. `classic-round` is late-bound because that is what today's engine does — it selects a target at swing time.

### `classic-round` is a compatibility profile, and it is the hard one

It must reproduce the current engine to the byte, which means the profile has to express today's *implicit* round structure explicitly:

1. round start → every active actor becomes `Ready` simultaneously
2. status ticks and regenerator pulses resolve (one funnel window)
3. actors seated in initiative order — a per-round `NextInt(1000)` draw each, minus swift bonus, stable-sorted
4. death cleanup
5. shield upkeep — **after** dispatch, so an expiring shield still absorbed that round
6. clock advances exactly 1000 ticks

Today that ordering is implicit in the order statements appear in a method. Here it becomes **scheduled events at fixed intra-round offsets**, which is strictly better: the ordering becomes a declared fact that a test can read, rather than a property of source layout that a careless edit could change.

### `W` is a knob, not *the* knob — and it only binds with wind-up

Three corrections from the review, because the ideal oversold this dial:

1. **It has one real user.** `classic-round` and `hybrid-atb` both set `W = N`, where it never binds. `galaxy-sync` is the only profile where `W` is observable. What actually separates `classic-round` from `hybrid-atb` is the advance policy and the readiness function.
2. **It only binds when `WindupTicks > 0`.** Under next-event advance with a strict total order and atomic `Resolving`, events pop one at a time — a battle is *already* serialized regardless of `W`. `W` becomes meaningful only when a `Committed` dwell can overlap another actor's.
3. **Therefore the obvious test is vacuous.** "With three ready actors, exactly one is ever in `Committed`/`Resolving`" passes for *any* `W` when actions are zero-length. See the testing strategy — `W` must be proven by contrast, with real wind-up.

### Validation: a real action, before the gate

`ActionEnvelope.NoOp` proves the FSM plumbing but validates **nothing** about the seam — every field is zero. The review's sharpest structural finding was that `ActionEnvelope` would otherwise reach the T5 gate with **no consumer at all**, since `classic-round` also zeroes every field.

So this module ships a **validation profile**: the basic attack driven through the envelope with non-zero `WindupTicks`, `RecoveryTicks`, and a real `SpeedChannel`, running under `galaxy-sync`. It has no goldens to protect and no byte-identity constraint, so it is the cheapest possible place to discover that the envelope is wrong — and the only thing that surfaces it before the seam is locked.

### Content chooses the profile — without touching the setup

**Owner decision: per wave / per tier.** The profile is **looked up from a content id that already exists**, never stored on the battle:

```
profile = WaveCatalog.Get(setup.WaveId).Profile ?? ModeProfiles.ClassicRound
```

`BattleSetup` already carries `WaveId`, and the expedition tier is known at collect time, so content gets full control with **zero serialization change** — which is precisely what keeps the four expedition hashes still. A profile id field on `BattleSetup` stays a named **Never** in T5, because any new property there serializes into every embedded battle plan and moves all four.

Unknown or absent profile ⇒ `classic-round`, so every wave authored to date keeps today's behavior by construction.

### The structural acceptance criterion

**Adding a mode adds a row, never a branch.** Enforced, not asserted in prose: an architecture test scans the kernel namespace (`Timeline/`) and fails if any type references a profile id string or switches on a profile enum. The kernel may read the profile's *knobs*; it may never ask *which profile* it is.

This is the single test that proves the whole program's thesis, and it should be written before the profiles are.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ModeProfile"
```

## Structure

```
src/FusionRpg.Core/Battle/Timeline/BattleModeProfile.cs (the record)
src/FusionRpg.Core/Battle/Timeline/ModeProfiles.cs      (classic-round, galaxy-sync, hybrid-atb)
tests/FusionRpg.Core.Tests/Battle/Timeline/             (per-profile behavior + the no-branch architecture test)
```

## Testing strategy

**`W` proven by contrast, in one file, with non-zero wind-up:** three ready actors at `W=1` never overlap; the *same scenario* at `W=2` provably does. Asserting only the first is vacuous. `WScope = PerSide` gets its own case (one actor per side concurrently). `classic-round` reproduces the round skeleton with intra-round phase ordering asserted explicitly. **The validation profile runs a real basic attack end to end** — commit, wind-up, resolve, recover — and its report is inspected, not just its non-crashing. Structural: the no-branch test above, written *before* the profiles. Determinism: each profile replays byte-identically from the same seed. No dilation tests — dilation was deleted from the kernel (T1).

## Boundaries

- **Always:** a mode is data; the kernel reads knobs, never profile identity.
- **Ask first:** whether `W` is content-configurable per encounter (ideal §10 open question 3) — the profile record supports it, but exposing it to content widens the test matrix; adding a fourth shipped profile.
- **Never:** a profile-id branch anywhere in the kernel; a profile that needs a kernel change to work; `classic-round` diverging from the current engine in any observable way.

## Success criteria

1. Three profiles, one kernel, zero branches — proven by the architecture test. 2. `galaxy-sync` and `hybrid-atb` differ only by knob values, demonstrating that `W` really is the parallelism dial the ideal claims. 3. `classic-round`'s intra-round ordering is a declared, tested fact rather than an emergent property of source order.
