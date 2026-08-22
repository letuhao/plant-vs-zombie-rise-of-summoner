# Spec: atom-runner (E15)

**Status: BUILT 2026-08-22.** Deviations found while building are marked **[built]** below.

Module **E15** in the [atom effect map](../effect-atom-map.md). Depends on **E7**, **E13**, **E3**, **E2** (chance rolls and `OnApply` values use E2's named streams).

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

> Added by the gap-clearing round (2026-08-22). **Not optional** — the map as first written had no runtime home for per-binding state, so predicate-tree atoms and `capPerMatch` had nowhere to live.

## Objective

The **Secondary effect runner**: the runtime half of the compile/run split. It holds runner bindings, tracks per-binding state, evaluates predicate trees against live state, and **dispatches through the Funnel when a condition matches**.

## Design (locked on approval)

### Where it lives, and the law it inherits

**Hot.** Core code, on the injector game thread, next to `EffectBag`. It **never blocks** on SignalR, HTTP, or SQLite ([overlay-control-loops.md](../overlay-control-loops.md)). The server is SSOT for content and pushes compiled bindings; that is Cold.

**Not because of frame latency.** The pipeline is **record-then-drain** — hooks record and return, the drain decides effects later under a budget, and [event-pipeline-v2-ssot.md](../event-pipeline-v2-ssot.md) **G5** makes *delayed effects* the designed worst case rather than frame drops. The RPG works from past events and contributes signed deltas; it never reads current game state. The reasons the runner is local are **chattiness** (1,250–10,000+ events/s at the hook), **pointer lifetime** (a longer delay means more targets already dead, so more skipped procs), and **offline resilience** — engineering tradeoffs, not a timing law. Determinism comes from the server owning the **seed** (E19), not from where the dice are thrown.

**It dispatches; it does not apply.** `Funnel.Enqueue` only — no `Bag.Grant`, no direct Writer call, no Unity *(spell it that way — `guard-funnel-delta.ps1` fails on the literal Writer type name appearing anywhere in a Core file, comments included)*, no `StatusExecutor`. Secondary law is unchanged and both existing guards cover it without modification.

### The unit is the atom

Items have no behaviour; actors do (definitions §0). The runner holds **runner entries** — one per atom the compiler could not express — not per binding. A binding is bookkeeping for how the atom arrived.

**Iteration order across entries on one event is `(priority DESC, binding_id ASC)`** — the order `InMemoryEffectGrantStore.Sorted()` already uses. That is what makes two atoms rolling `OnApply` on one hit consume the stream reproducibly, regardless of how or when a binding arrived.

### What it owns

| State | Scope | Note |
|---|---|---|
| Grant ICD clocks | per binding | the L1 proc gate — **not** the status ICD, and never merged with it. **The only clock here.** A separate "cooldown" row was struck 2026-08-22: the map's §7 scope table assigns *"action activation, cooldown, wind-up"* to the **turn kernel**, and no kind schema declares a cooldown param. For a triggered atom an ICD *is* the cooldown; a distinct one only means something for an activated ability, which is not this layer's |
| Counters / charges | per binding | e.g. death-refusal charges, every-N-hits meters |
| **`capPerMatch`** | per binding, per `match_key` | the economy cap that is in the FA9 allowlist today with **no implementation anywhere**. `match_key` is the existing match/session id from `match-runtime`; counters are cleared on `board.end`, alongside every other per-match structure |

All of it is **session RAM**. No durable runtime table (E6) — `entity:{ptr}` state is meaningless across a restart, and per-match counters die with the match by definition.

### The three ICD clocks stay separate

[status-ssot.md](../status-ssot.md) locks this and the runner must not blur it:

| Clock | Owner | Question |
|---|---|---|
| Grant `icd_ms` | **this module** | may this *listener* try again? |
| Status `icd_ms` | StatusRuntime | may this *status* be re-applied to this host? |
| `periodMs` | StatusRuntime | pulse cadence — not an ICD at all |

### Evaluation order, per event

```text
1. index lookup: bindings listening to this trigger      (no scan)
2. cheap gates first: ICD → cooldown → charges   (pre-proc: check only)
3. compiled predicate evaluation                          (E3; no alloc)
4. chance roll                                            (E2 named stream)
5. resolve OnApply values                                 (E2)
6. stamp ICD, commit charges  (the proc succeeded)
7. cap check (capPerMatch)    (post-proc: suppresses dispatch only)
8. Funnel.Enqueue
```

Gates are ordered **cheapest-first on purpose**: an ICD check is an integer compare, a predicate walk is not.

**When a gate *consumes* versus merely *checks* — the principle, not two ad-hoc rules:**

> A **pre-proc** gate (ICD, cooldown, charges, predicate, chance) consumes nothing when it fails: the atom never procced.
> A **post-proc** gate (cap) fires *after* the proc succeeded, so the ICD is stamped and the roll is consumed before the cap suppresses the dispatch.

That is why the cap sits last: a capped atom and an uncapped one occupy the same RNG stream position, so replay holds. The earlier spec stated the two outcomes as separate rules and never said when consumption happens — which made two of its own acceptance rows mutually exclusive.

### `capPerMatch`

Counter keyed `(binding_id, match_key)`. Incremented on **dispatch**, not on trigger. On reaching the cap the binding stops dispatching for that match and emits one `skipped: cap` telemetry record — once, not per attempt, so a capped economy effect cannot spam the event log.

Cleared on match end, alongside every other per-match structure.

### Re-entry and depth

Foundation's rule is unchanged: nested `Flush` is a no-op, dispatch happens at **depth 0**, and the Funnel drains anything `OnDeath` adds inside the same window. The runner respects `ProcDepthLimit` (6) and never re-enters its own dispatch.

### Hot-path discipline

Per the E13 budget: **≤ 50 ns per atom evaluation, zero allocation.** No dictionaries keyed by string, no LINQ, no per-event lists. The trigger index and the per-binding state arrays are allocated once and reused, exactly as `InjectorEntityRegistry` and the coalescer already do.

### [built] What building it changed

**E7's `RunnerEntry` did not carry the state that routed an atom here.** `Compilability` sends an atom
to the runner *because* it declares `capPerMatch`, `charges`, `everyHits` or `maxStacks` — and the
compiler then dropped every one of them, along with all non-value params. The classifier and the
payload disagreed, so `capPerMatch` was literally unimplementable. `RunnerEntry` now carries
`Limits` and `Params`, populated from `when_json` and `params_json` alike.

**Absent is not zero, and `new()` is not absent.** `RunnerLimits` encodes absent as `-1`, because a
cap of 0 ("never dispatch") and 0 charges ("already spent") are both legal content. The first cut
wrote `None = new()`, which on a record struct calls the implicit parameterless constructor and
zeroes every field — turning every unlimited atom into the most restrictive one. Positional defaults
were removed so the trap cannot reappear, and a test pins `default != None`.

**No cooldown field.** The design table lists a cooldown as distinct from an ICD, but no kind schema
declares a cooldown param and nothing routes on one. Inventing a key would widen a closed vocabulary
by convenience, so the ICD is the only clock until a schema declares otherwise.

**The `everyHits` meter advances even when it does not fire.** "A pre-proc gate consumes nothing"
cannot mean the meter freezes, or it never reaches N. It means no ICD stamped and no roll drawn.

**The dispatch overlay carries only the rolled values.** Static params belong on the def's action
rows, which is where E7 already puts them on the compiled path. Pushing them through the overlay
sends keys like `element` into an allowlist with no slot for them, and the grant is refused mid-flush
far from the enqueue that caused it.

**Open, and owned elsewhere: nothing emits a def for a runner atom.** A dispatch names the atom id as
its `effectId`, and `EffectBag.Grant` throws on an unknown one. E7 emits defs only for the compiled
path. **E19** must ship one def per runner entry, built from `RunnerEntry.Params` — until then a host
has to have the def in its catalog already.

**The runner runs BEFORE the bag, not after.** `EffectBag.OnEvent` calls `Funnel.Flush()` inside
itself, so a dispatch enqueued after the bag has already run sits in the mailbox until the *next*
event — a silent one-event lag on every proc. Found by a test asserting the funnel was drained rather
than merely pending; the first wiring had it backwards on both the sim host and the injector.

**The caller is `SimEffectHost`**, the Core-hosted double this spec names: it drives the runner from
the same event the bag sees, and `BeginMatch` resets the per-match counters. The injector's
`EffectRuntime` is E19's wiring.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Runner"
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-secondary-no-unity.ps1
```

## Structure

```
src/FusionRpg.Core/Effects/Atoms/AtomRunner.cs           (OnEvent, the gate ladder, dispatch)
src/FusionRpg.Core/Effects/Atoms/RunnerState.cs          (ICD, charges, meters, caps)
src/FusionRpg.Core/Effects/Atoms/TriggerIndex.cs         (trigger ordinal → slots, no scan)
src/FusionRpg.Core/Effects/Atoms/RunnerEventMapper.cs    (event → the flat facts, once per event)
src/FusionRpg.Core/Effects/Atoms/RunnerEntry.cs          (+ RunnerLimits, + Params — E7's contract)
src/FusionRpg.Core/Effects/SimEffectHost.cs              (the caller)
tests/FusionRpg.Core.Tests/Atoms/AtomRunnerTests.cs      (26 tests)
tests/FusionRpg.Core.Tests/Atoms/CapPerMatchTests.cs     (8 tests)
```

## Testing strategy

| Case | Expect |
|---|---|
| Atom with ICD 250 ms, two events 100 ms apart | one dispatch; second skipped with reason `icd` |
| Skip reasons | one per gate (`icd`, `cooldown`, `charges`, `predicate`, `chance`, `cap`); **only `cap` is emitted as telemetry**, once per binding per match. The rest would spam at hit rate |
| `icd_ms: 0` explicit | every event dispatches |
| Predicate false | no dispatch, **no ICD consumed, no roll drawn** — pre-proc gate |
| Cap reached | no dispatch, but **ICD stamped and roll drawn** — post-proc gate |
| Chance 800‰ over 10⁴ events, fixed seed | **exact expected count**, reproducible — not a tolerance |
| `capPerMatch: 5`, 20 events | 5 dispatches, **one** `skipped: cap` record |
| Cap reached, then match end, then new match | counter reset, dispatch resumes |
| Capped binding vs uncapped | identical RNG stream position — cap is the last gate |
| Grant ICD vs status ICD | independent; one blocking never blocks the other |
| Nested dispatch from `OnDeath` | drained in the same depth-0 window; no re-entry |
| Allocation probe, 10⁵ events | zero bytes |
| Server unreachable mid-match | runner keeps dispatching; **never awaits** |
| Guards | both pass unchanged |

## Boundaries

**Always:** dispatch via the Funnel only; keep state in RAM; order gates cheapest-first with the cap last; keep the three ICD clocks separate.

**Ask first:** adding a state kind; changing gate order; raising `ProcDepthLimit`.

**Never:** await the server, SQLite, or SignalR on the hot path; call `Bag.Grant`, the Writer, or Unity; allocate per event; merge the grant ICD with the status ICD; emit a cap-skip record more than once per binding per match.
