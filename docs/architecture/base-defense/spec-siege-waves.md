# Spec: `siege-waves`

**Module 10 of 29 · level 4 · depends on `combatant-kind` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Let reinforcements arrive mid-battle, on a clock, without unbounded work.**

A siege is long. Both sides feed units in — that is what makes it a siege rather than a skirmish.
Three things stand in the way, and the audit found all three:

- **Rosters are fixed at setup.** `BattleSetup.Squad` / `.Wave` are built once and never grow.
- **F8: a state-based reinforcement trigger is turtle-exploitable.** *"Send the next batch when the
  current one is under 30% strength"* means a defender who never engages never faces the next batch.
  The optimal play becomes standing still, which is the least interesting thing a player can do.
- **F9/C7: the drain is unbounded.** The kernel baseline's one named hole — a growing roster processed
  to completion in one pass can spike arbitrarily.

**Success looks like:** batches arrive on schedule regardless of how the defender plays, the drain is
bounded per round and resumable across rounds, and a boardless battle is byte-identical.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `WaveCatalog` — wave definitions, including `WaveDef.Profile`, which `BattleModeProfileCatalog`'s
  doc comment names as the resolver's own input (`WaveCatalog.Get(waveId).Profile ?? classic-round`).
- `BattleEngine`'s `EventQueue` + `SimulationClock` + `NextEventAdvance` — the kernel already schedules
  two distinct event kinds (`RoundEventKind = 0`, `StatusPulseEventKind = 1`) on one queue, recomputing
  and rescheduling after each fires. **A third kind is a row, not a branch.**
- `maxBattleTick` — the horizon every scheduled event is already bounded by, whose comment records
  that status pulses not respecting it *"schedules forever once round events stop — a real infinite
  loop this exact scenario hit and was caught by."*
- `BattleRunState.AnyActive` — with `combatant-kind`'s `Animate` filter.

**Real gaps.** No roster growth. No reinforcement schedule. No bounded drain.

---

## The contract

### 1. Batches arrive on a clock — F8's fix, and it is one word

```csharp
/// <summary>
/// One reinforcement batch: WHEN it arrives and WHAT arrives.
///
/// <para><b>A tick, not a condition.</b> Audit F8: a state-based trigger ("when the current batch
/// drops below 30%") is turtle-exploitable — a defender who never engages never advances the
/// trigger, so the dominant strategy becomes standing still. A clock cannot be gamed by declining to
/// play. The pressure is the siege; the player's choice is what to do under it, not whether to face
/// it.</para>
/// </summary>
public sealed record ReinforcementBatch
{
    /// <summary>Simulation tick of arrival, absolute from battle start.</summary>
    public long AtTick { get; init; }
    public string Side { get; init; } = "";
    public IReadOnlyList<BattleActorSetup> Actors { get; init; } = Array.Empty<BattleActorSetup>();
    /// <summary>Which board edge or zone they enter from. Ignored in a boardless battle.</summary>
    public BoardEdge Edge { get; init; }
}
```

#### ⛔ Correction: the trigger is a HYBRID, not a pure clock

**The first draft rejected state-based batching outright, and that misread F8's own verdict.** F8 says:

> *"**Make it clock, OR field cleared, whichever first** — one tunable row."*

And decision 6 is explicit about the state half: *"the field resolves, then the next batch of both
sides enters together."* Decision 24 repeats it: *"the field cap cycles batches until one side is spent
or the objective falls."*

So both fire, whichever comes first:

```csharp
// F8's actual verdict. The CLOCK is what makes the trigger un-gameable — "one surviving unit behind a
// rampart blocks the next batch and wins on MaxRounds" is the exploit, and a deadline closes it. The
// FIELD-CLEARED half is what makes waves legible as waves (decision 6) and gives the defender the
// rebuild window that makes rebuilding meaningful.
//
// Neither alone is right: pure clock loses the visible wave rhythm; pure state is exploitable.
var due = Math.Min(nextScheduledTick, fieldClearedTick ?? long.MaxValue);
```

**Bounded by `maxBattleTick`, like every other scheduled event.** A batch scheduled past the horizon
never fires — the same `if (tick > maxBattleTick) return;` guard `ScheduleNextStatusPulse` already
applies, and for the identical reason.

### 2. A third event kind

```csharp
const int RoundEventKind        = 0;   // existing
const int StatusPulseEventKind  = 1;   // existing
const int ReinforcementEventKind = 2;  // new — a row, matching the two above exactly
```

Same schedule-recompute-reschedule shape. **Exactly one reinforcement event pending at a time**, which
is the invariant `BattleEngine`'s own comment states for the existing two: *"Exactly one event of EACH
kind is ever pending at a time, recomputed and rescheduled after it fires."*

### 3. Roster growth

`BattleRunState` gains `AddActor(BattleActorSetup, GridPos?)`:

- appends to `Actors` and `ByKey`,
- validates the key with **the same rules `Resolve` already applies at setup** — non-empty,
  lower-case, no `entity:` or `0x` prefix, no duplicate, `MaxHp >= 1`. Not a re-implementation: the
  validation is extracted from `Resolve`'s loop into a method both call. A mid-battle actor that
  bypasses those checks is silently unhittable at the shield gate, which is the exact failure the
  original loop's comment describes.
- places on the board when one exists,
- **never reorders existing actors** — an index shift mid-battle would invalidate every in-flight
  effect that captured one.

### 4. Bounded, resumable drain — F9/C7

```csharp
/// <summary>
/// Structural per-round work bound, not a progression ceiling (AGENTS.md exempts per-frame/runtime
/// caps). Arrivals beyond it are NOT dropped — they carry to the next round with their queue
/// position intact, so a large batch lands over several rounds rather than spiking one.
/// </summary>
public int MaxArrivalsPerRound { get; }   // from tuning
```

The pending queue is **FIFO, and ties within one tick break by ordinal actor key** — otherwise two
actors arriving on the same tick could land in a different order on a different machine, which is a
replay divergence that reproduces nowhere.

**Carry-over, not drop.** Dropping is simpler and it silently deletes content a designer authored;
carrying is a few lines and it means the batch always arrives, just later.

### 5. Both sides reinforce

Owner decision: *"Both sides move."* Attacker batches are the assault; defender batches are the
garrison mustering. **One mechanism, `Side` as data** — not two code paths.

### 6. Wave composition becomes data — §3.5

> *"Wave composition is a code const today and **there is no wave data file at all** — this feature
> should fix that rather than add a second hand-written array."*

So reinforcement batches are authored in `data/`, not in a `WaveCatalog` literal. **This module creates
the wave data file the repo does not have**, and existing wave definitions move into it unchanged — a
migration whose acceptance is that every existing battle golden is byte-identical afterward.

Adding a second hand-written array beside the first is the specific outcome §3.5 names and forbids.

### 7. Boardless battles are unaffected

An empty batch list is the default. `BattleSetup` gains
`IReadOnlyList<ReinforcementBatch> Reinforcements = Array.Empty<...>()`, and with it empty the
reinforcement event is never scheduled — so the queue behaves exactly as it does today.

> This is the byte-identity argument and it is structural: a never-scheduled event kind cannot change
> a tick sequence.

---

## Tunables

`data/tuning/siege.v1.json`.

| Key | Unit | Default | Why |
|---|---|---|---|
| `waves.maxArrivalsPerRound` | actors | `8` | **Structural** (a work bound) but configurable because the right value depends on board size — comment must say which it is |
| `waves.batchIntervalTicks` | sim ticks | **unset** | Balance — the clock half of F8's hybrid. Decision 29 keeps pacing numbers unset until a real board exists to measure on |
| `waves.fieldClearedThreshold` | units alive | `0` | Balance — the state half. `0` = fully cleared; a positive value means "nearly cleared" |
| `waves.batchSize` | actors | **unset** | Same |

## Numeric types

| Value | Type | Why |
|---|---|---|
| `AtTick` | **`long`** | it is compared against `maxBattleTick`, which is already `long` — a narrower type here is a silent truncation at exactly the horizon that matters |
| arrival counts | `int` | bounded by `maxArrivalsPerRound` |

## Boundaries

**Always:** clock-based triggers · bound every schedule by `maxBattleTick` · reuse `Resolve`'s actor
validation rather than re-implementing it · FIFO with ordinal tie-break.

**Ask first:** setting `batchIntervalTicks` / `batchSize` (decision 29) · a state-based trigger of any
kind — F8 is a finding, and re-opening it needs the counter-argument.

**Never:** drop an over-cap arrival · reorder existing actors · schedule past the horizon · add a
branch in the round loop instead of a third event kind.

---

## Testing

| Test | Asserts |
|---|---|
| `Empty_reinforcements_are_byte_identical` | **the gate** — all twelve goldens |
| `Batch_arrives_on_schedule_regardless_of_defender_behaviour` | **F8's clock half.** A turtling defender cannot delay the deadline |
| `Clearing_the_field_early_pulls_the_next_batch_forward` | **F8's state half** and decision 6 — the wave rhythm |
| `Whichever_comes_first_wins` | both triggers armed; assert the earlier one fires and the later is cancelled |
| `Turtling_does_not_delay_reinforcements` | F8 stated as the exploit it prevents |
| `Arrivals_are_capped_per_round` | a 30-actor batch at cap 8 |
| `Over_cap_arrivals_carry_over_and_none_are_lost` | **F9/C7.** All 30 eventually present, none duplicated |
| `Carry_over_preserves_queue_order` | resumable, not restarted |
| `Same_tick_arrivals_order_by_ordinal_key` | replay stability |
| `Batches_past_the_horizon_never_fire` | the `maxBattleTick` guard, as for status pulses |
| `Mid_battle_actor_passes_the_same_key_validation` | a mixed-case key throws, as at setup |
| `Adding_an_actor_does_not_reorder_existing_ones` | in-flight effect indices survive |
| `Both_sides_reinforce_through_one_path` | attacker and defender batches, same code |
| `Reinforcement_scheduling_is_deterministic_over_10000_runs` | |

## Success criteria

1. All twelve goldens byte-identical with no reinforcements.
2. Arrival ticks are provably independent of defender behaviour.
3. No arrival is ever dropped; per-round work is bounded.
4. Actor validation is shared with `Resolve`, not duplicated.
5. Three event kinds, zero new branches in the round loop.

## Open questions

None open in this module. `batchIntervalTicks` and `batchSize` are deliberately unset under decision
29 — an answered question with "unset" as the answer.
