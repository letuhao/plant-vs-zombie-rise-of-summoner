# Tasks: battle-timeline

Plan: [battle-timeline-plan.md](battle-timeline-plan.md) · Map: [../docs/architecture/battle-timeline-map.md](../docs/architecture/battle-timeline-map.md) · Audit: [../docs/architecture/battle/audit-2026-08-21.md](../docs/architecture/battle/audit-2026-08-21.md)

Scope: S ≈ under an hour, M ≈ a focused session, L ≈ multi-session.

## Phase 0 — prerequisites (nothing builds before these)

- [x] **B0: `decisions.md` row — battle time model**
  - Locks: virtual-time DES kernel, tick = 1 ms, mode-as-data, profile resolved from content id (never serialized).
  - Acceptance: row present and cross-linked to the map before any code task starts (AGENTS.md hard boundary).
  - Verify: doc review. Files: `docs/architecture/decisions.md`. Scope: S.

- [x] **B1: RNG observation seam + pre-adoption fixtures** *(must precede every engine edit)*
  - **Landed better than specced: `SeededRng` was not touched at all.** Call-site recording plus an `ICombatRng` decorator (which keeps the shared `OverlayCombatCalculator` hot path untouched) capture the same draws, so `RngAlgoVersion` stays **1** by construction rather than by assertion. `BattleTrace` records draw *values in sequence*, phase order, and per-round state; `Resolve` gained an optional `BattleTrace?` (all 53 call sites still compile) and every record site is null-conditional, so tracing cannot change an outcome.
  - Fixtures captured at `tests/fixtures/battle-traces/{stomp,close,wipe}.trace.txt` (301 lines total).
  - **Deviation:** the planned *mid-battle summon* fixture could not be captured — summons do not exist in the engine yet, so there is nothing to trace. Re-filed as a **forward guard**: the summon fixture must be written by whichever enrichment wave first adds a summon, because appending an actor mid-round changes the initiative draw count for that round and every round after.
  - Verified: 7 new tests green; **Core 1350/1350** with all eight goldens unchanged, proving the instrumentation is behavior-neutral. The sub-round lock empirically confirms today's under-delivery (100 HP lost = 4 pulses × 25, not 16).

## Phase P — performance contract (cross-cutting, owner-directed 2026-08-21)

Spec: [spec-kernel-performance.md](../docs/architecture/battle/spec-kernel-performance.md). The kernel runs **per-frame in the injector**, so it is frame-critical Unity code sharing the existing ≤1 ms/frame injector budget.

- [x] **P1a: allocation contract + CI assertions** — `ActionIntent` was a record **class**: one heap object per actor per turn, at 200+ entities, on the Unity main thread. Now a `readonly record struct` with `IsNone` (a `Nullable<ActionIntent>` would have re-introduced the same wrapper). `EventQueue` pre-sizes both structures so a battle never resizes mid-tick. Eight CI tests assert **zero bytes** across schedule/drain, 6 000 reschedules, 5 000 clock advances, 10 000 intent declarations, 50 000 transitions, and slot churn — deterministic, unlike wall-clock timing.
- [x] **P1d: tick-path source guard** — the scan now carries **two rule sets**: purity (wall clock, RNG, floating point, dictionary enumeration — **no file exempt**) and tick-path cost (LINQ, `FindObjectsOfType`, `GetComponent`, `StatSystem.Resolve`). Diagnostics are exempt from the *cost* rules only and never from purity, via a **named list rather than a pattern**, so the exemption cannot grow silently — a test proves a similarly-named neighbouring file gets no relief. Every new category is proven detectable against a planted violation. Comment-stripping means prose may still name a banned construct, so the guard doesn't punish documentation. It caught one real violation on first run: `ActionEnvelope` used `SequenceEqual`, now a manual loop (also one enumerator allocation lighter).

- [x] **P2: stress harness — the measurement gate before T13** — 200 entities, 600 frames (10 s at 60 fps), drain-and-re-arm shaped like a live board against a **contended** slot pool (`W = 4`, 1 966 denials) with per-frame reschedules. **Result: median-of-5 0.0093 ms/frame against a 0.15 ms slice (~16× headroom) and 0 bytes allocated.** T13 is unblocked on measurement. Baseline: [kernel-timeline-baseline.md](../docs/research/perf/kernel-timeline-baseline.md).
  - The pass/fail gate is **allocation**, which is deterministic; wall clock is reported precisely but asserted only against a catastrophic-regression ceiling, since a tight timing assertion in CI measures the build agent and gets muted at the first flake. A third test asserts per-event comparison count stays flat as the board grows, so a quadratic regression cannot hide behind a fixed-size case.
  - The harness allocated its own instruments **twice** — first a `Stopwatch` (40 bytes), then a 200-element `EventHandle` scratch array (3 224 bytes) — both inside the measured region. Worth recording: the gate is sensitive down to a single small object, and the harness is as easy to get wrong as the thing it measures.
  - Liveness is asserted alongside the byte count: drained > 0, transitions ≥ 5× entities, reschedules > 0, slot denials > 0. Without those, deleting the work left the allocation gate green.
  - **Known gap, not covered here:** `PopDue` is unbounded while the spec requires bounded resumable drain. At 2.8 events/frame this workload cannot reveal it. Tracked as **P1c**.

- [ ] **P1b: `PerfProbe` sections** — `kernel.tick` / `kernel.drain` / `kernel.schedule` in the existing 5 s window beside `loop.tick`; probe overhead within its own 0.05 ms budget. **Blocked on T13** (nothing to measure until the kernel ticks in-process). Scope: S.
- [ ] **P1c: bounded resumable drain** — per-frame work capped and resumed next frame, following event-pipeline-v2's frame-budgeted precedent. **Blocked on T13.** Scope: M.

## Phase 1 — the kernel (T1–T4)

- [x] **B2: T1 clock + event queue** — rewritten 2026-08-21 as an **indexed** binary heap after the owner challenged the algorithm. The first draft used lazy deletion (reschedule pushed a duplicate, old copy marked stale), which made re-timing O(n) with permanent growth: **20k events + 20k reschedules took 3157 ms**, and 12k reschedules over 200 live events left a **12 200-entry heap**. Maintaining `seq → heapIndex` through every swap makes cancel and reschedule O(log n) in place: the same workloads now run in **48 ms** and **11 ms**, with the heap holding exactly the live count. It also deleted a whole correctness class — no `_rescheduled` map, no `IsStale`, no duplicate entries, and no tombstone set to grow unboundedly; `Count` is now the heap's length so it cannot drift. Tests assert the global sort invariant under adversarial churn rather than scripted sequences. 13 tests. Zero drift over 10 000 frames at 60 fps; `Reschedule` preserves `Seq` (proven by showing unrelated tie-break order is untouched); `TryAdvance` reports `Blocked` on an empty queue; clock clamps rather than rewinding on a past-due event. Purity guard implemented as a **source scan** rather than reflection — reflection cannot see `DateTime.UtcNow` inside a method body, which is exactly where it would hide.
  - `SimulationClock` with `TryAdvance → Advanced | Blocked`; `NextEventAdvance` and `FixedIncrementAdvance(long frames)` with internal carry; `EventQueue` on `(DueTick, Seq)` with handles, tombstones, and `Reschedule` that **preserves `Seq`**. No dilation.
  - Acceptance: zero tick drift over 10 000 frames at 60 fps; rescheduling one event provably leaves unrelated tie-break order untouched; `TryAdvance` can report `Blocked`; purity guard green across module **and call sites**.
  - Verify: `--filter ~VirtualTime`. Scope: M.

- [x] **B3: T2a FSM core** — 17 tests. `Downed` proven: `Resolving → Downed → Charging` (the immortal revive) does not throw. **Design refinement:** `Incapacitated` is not a state at all — since CC is a derived read from StatusRuntime, adding it to the enum would have re-created the two-sources-of-truth problem the audit rejected. A test asserts no such state exists. The meta-test binds the transition table to the documented diagram.
  - Six states incl. **`Downed`**; full transition table with every exit (`Ready|Committed|Recovering|Incapacitated → Withdrawn`, `Resolving|Incapacitated → Downed`); illegal transitions throw naming both states; `Incapacitated` is a **derived read** from `StatusRuntime`, never a cached remainder.
  - Acceptance: **meta-test binds the table to the documented diagram**; the immortal path `Resolving → Downed → Charging` does not throw (the scenario that would otherwise crash Phase 2); a CC instance expiring exactly at `now` still locks.
  - Verify: `--filter ~TurnFsm`. Scope: M.

- [x] **B4: T2b slots + intent seam** — 14 tests. `W` proven **by contrast** (`W=1` serializes, `W=2` provably overlaps); `WScope.PerSide` covered; all five exit paths release, each its own case; release is idempotent. `IIntentSource` + `SeatOutcome.Passed` close the no-legal-intent deadlock. **B5's `ActionEnvelope` landed here** — `ActionIntent` cannot be typed without it — so B5 is partly done; its remaining behavioral wiring (cooldown state, fizzle, resolve offsets) stays open.
  - `W`, `WScope : Global | PerSide`, deterministic contention by `(readyTick, seq)`; `IIntentSource.TryDeclare`; no-legal-intent ⇒ no slot, `action.passed`, reschedule at `now + PassQuantum`.
  - Acceptance: with every actor unable to declare, the battle **terminates** rather than hanging; slots release on death, withdrawal, interrupt, and fizzle (four tests — a leak deadlocks `W=1`).
  - Verify: `--filter ~TurnFsm`. Scope: M.

- [x] **B5: T2c action envelope + published pending resolve** — 24 tests, kernel filter **159/159 green**. `ActionRunner` drives commit → wind-up → published resolve(s) → recovery, plus fizzle and interrupt; `CooldownLedger` owns cooldown state. All three acceptance criteria met, with a `LateBound` contrast test so the fizzle assertion is not vacuous.
  - Three rules the spec left to the implementer, decided and documented in code: **cooldowns keep running while their owner is suspended** (absolute ticks; pausing needs a remainder, which is the design the audit rejected for CC); **recovery is scheduled when resolution ends, not at commit** (otherwise a combo that fizzled on hit one stays locked out as if all three had landed); **`OnDamage` also yields to CC** (a stun stops a swing whatever the envelope says about damage; the reverse does not hold).
  - `ActorTurnMachine.HoldsSlot` → **`IsMidAction`**: it derived a slot claim from state, which became a lie the moment the envelope gained `SlotConsuming` — movement is mid-action holding no slot, and `ActionSlots.Holds` already answers that exactly.
  - **Known gap, deliberately not closed here:** the envelope's fields were chosen from FFX/SMT/FF15, and **no real action has been driven through them**. Missing versus the Chaos `action-core` grounding: duration min/max bounds, a cooldown-reduction channel, and `interrupt_affects_cooldown`. Owner decision 2026-08-22: define the action architecture before building B6+ on an unvalidated seam — see [combat-action-map.md](../docs/architecture/combat-action-map.md). **B6–B12 are on hold behind that map.**

- [ ] **B6: T2d reaction lane**
  - Separate `WReact` pool; bounded nested-resolution stack; depth limit following the `ProcDepthLimit` precedent; reaction budget distinct from the turn budget.
  - Acceptance: a defender in `Recovering` can still react; exceeding depth drops the reaction with telemetry and never recurses; `WReact = 0` is byte-identical to no lane at all.
  - Verify: `--filter ~TurnFsm`. Scope: M.

- [ ] **B7: T2e rendezvous (link-strikes)**
  - N-actor atomic `SlotReservation`; `WaitingForPartner` dwell; **bounded timeout** with fallback to solo intent.
  - Acceptance: two actors commit together and produce **one** `Resolving`; a partner that never arrives times out and both act solo — **no hang at `W=1`**; partial acquire never leaves a held slot.
  - Verify: `--filter ~TurnFsm`. Scope: M.

- [ ] **B8: T2f post-apply trigger phase**
  - Slot-free, FSM-neutral point after every HP delta; listeners in deterministic order; death resolution is **veto-capable**.
  - Acceptance: `immortal` (veto), `soul-eater` (on-kill), and `coward` (threshold) all express as listeners with no engine branch; ordering is replay-stable.
  - Verify: `--filter ~TurnFsm`. Scope: M.

- [ ] **B9: T3a readiness**
  - `turn.speed` / `turn.haste` as flat consts **plus `DerivedStatRegistry` defaults** (100 / 1000 — zero would divide-by-zero or mean instant actions); work-based accrual `(accruedWork, rate)`; rebase via `Reschedule` on speed/haste mutation; `RoundDiv` half-up; `max(1, …)`.
  - Acceptance: an actor half-way through a 1000-tick wait who gains haste 500 arrives at **t+750**, not t+1000 (the audit's I1 lock); suspension stores work so resuming with haste is faster; `AllCombatChannelIds` still **84**; a `turn.*` modifier through the compose path does not throw.
  - Verify: `--filter ~Readiness`. Scope: M.

- [ ] **B10: T3b turn economies**
  - `ITurnEconomy` with `Scope`, `TryAcquire`, `OnActionResolved`; ships `OneActionPerTurn`, `ActionPoints`, and side-scoped `PressTurn`.
  - Acceptance: `PressTurn` writes cleanly — **it is the implementation that would have broken the original interface, so it is the proof the interface is right**; weakness refunds and miss penalties adjust the side budget; readiness never reads a budget.
  - Verify: `--filter ~Economy`. Scope: M.

- [ ] **B11: T4a profile record + the no-branch architecture test** *(test written first)*
  - `BattleModeProfile`; architecture test failing if any kernel type references a profile id or switches on a profile enum.
  - Acceptance: the test exists and is green **before** any profile is defined.
  - Verify: `--filter ~ModeProfile`. Scope: S.

- [ ] **B12: T4b three profiles + a real action** *(the capability proof)*
  - `classic-round`, `galaxy-sync`, `hybrid-atb`; profile resolved from `WaveCatalog.Get(waveId).Profile ?? classic-round`; **a basic attack driven through the envelope with non-zero wind-up under `galaxy-sync`.**
  - Acceptance: `W` proven **by contrast** — `W=1` never overlaps, `W=2` in the same file provably does; `WScope = PerSide` covered; a real battle resolves end to end and its report is inspected, not merely non-crashing.
  - Verify: `--filter ~ModeProfile`. Scope: L.

### ✅ Checkpoint A — capability
- [ ] Every state and transition covered; `Downed` revive proven; `W` proven by contrast; a real attack runs under `galaxy-sync`; `PressTurn` written; no-branch test green; **zero production code rewired**.

## Phase 2 — the gate (T5)

- [ ] **B13: `BattleRunState` extraction** *(no behavior change)*
  - Lift actors, `byKey`, host, shields, gate, sink, events, RNG streams, and the eight closures out of `Resolve` into a state object.
  - Acceptance: all eight goldens **unchanged**; suites green with no test edits. Pure refactor — if a golden moves here, stop.
  - Verify: full Core + guards. Scope: M.

- [ ] **B14: kernel drive under `classic-round`**
  - Round skeleton as scheduled intra-round events; `Resolve(setup, seed)` **survives verbatim** as an overload defaulting to classic-round (53 call sites across 11 files); `Resolve(setup, seed, profile)` added.
  - Acceptance: the seven byte-identity hazards each have a passing fixture — draw order, active-set timing, list-order targeting, **CC-locked actors still draw initiative**, status under-delivery preserved, one funnel window per round, early exit cancels shield upkeep.
  - Verify: parity ladder + full Core. Scope: L.

- [ ] **B15: gate verification**
  - Acceptance: eight goldens byte-identical; **six** suites green (Core, Data, Guard, CheatCore, Launcher, **E2E**) with **no test edits**; four boundary guards green; `RulesetVersion` still 2.
  - Verify: all suites + all guards + injector/server build. Scope: M.

### ⛔ Checkpoint B — safety
- [ ] Byte-identical. **Nothing proceeds past a drift here** — a re-bless at this checkpoint costs a win-rate sweep and owner sign-off, and it would mean the refactor is unproven.

## Phase 3 — the deliberate change (T9)

- [ ] **B16: status pulses on the timeline**
  - Kernel schedules pulses at true times and drives `StatusRuntime.Tick` at those ticks with an exact ms→`DateTimeOffset` conversion. (Its bounded catch-up loop already exists — this may need no `StatusRuntime` rewrite.)
  - Acceptance: a `PeriodMs=250, DurationMs=4000` status delivers **16** pulses, not 4.
  - Verify: `--filter ~Status`. Scope: M.

- [ ] **B17: shield upkeep on the timeline**
  - Round-ticks → ms ticks; **fix the regen carry**, which truncates to zero below 1000‰ if driven at 1 ms.
  - Acceptance: shield durations honour true ms; regen accrues correctly at fine granularity; shield suite green.
  - Verify: `--filter ~Shield`. Scope: M.

- [ ] **B18: version bump + re-bless + sweep**
  - `RulesetVersion` 2 → 3; goldens re-blessed **once** with a written predicted delta; win-rate sweep produced.
  - Acceptance: every re-blessed hash justified against a predicted delta; shape tests hold without seed re-selection, or the exception is named. **⛔ owner sign-off on the sweep.**
  - Verify: full suites. Scope: M.

### ⛔ Checkpoint B2 — versioned change
- [ ] Timing correct, one re-bless, sweep signed off by the owner.

## Phase 4 — interactive battles

- [ ] **B19: T8 turn-order forecast** *(spec first)*
  - Pure projection: roll the queue forward `K` events with no mutation. Exact for `galaxy-sync`, soft-bounded for `hybrid-atb`, absent for real-time.
  - Acceptance: the forecast never mutates state; it matches what actually happens under `galaxy-sync`.
  - Verify: `--filter ~Forecast`. Scope: M.

- [ ] **B20: T6 interactive turns** *(spec first)* — ships with B21
  - `Ready` dwell; intent declaration; `input_window_ms` / `afk_timeout_ms` / `round_time_ms`; **timeout recorded as a decision at a tick**, never evaluated against wall-clock.
  - Acceptance: an AFK timeout produces an identical battle on replay — the sharpest determinism trap in the program.
  - Verify: `--filter ~Interactive`. Scope: L.

- [ ] **B21: T10 decision trace** *(spec first)* — ships with B20
  - `decisions_json` on `rpg_web_match_log`, appended **as the battle progresses**; `Resolve` gains an `IIntentSource` (replay-from-trace for the sweep, live for play); determinism becomes `(setup, seed, trace)`.
  - Acceptance: the boot sweep **refuses and marks `Abandoned`** for an interactive match with an incomplete trace — never heals it, reusing the platform-stamp refusal path; **expeditions are barred from interactive profiles by assertion**; a completed trace replays byte-identically.
  - Verify: `--filter ~Trace` + Data suite. Scope: L.

- [ ] **B22: T11 live sessions** *(spec first)*
  - SignalR session lifecycle, reconnect, AFK handling over T6 + T10.
  - Acceptance: a disconnect mid-battle resumes or abandons deterministically; no session path can write a battle whose trace is incomplete.
  - Verify: server + E2E. Scope: L.

### ✅ Checkpoint C — interactive
- [ ] Determinism holds under input: every interactive battle replays from its trace; the sweep cannot overwrite a played result.

## Phase 5 — the observer

- [ ] **B23: T7 PvZ observer** *(spec first)*
  - Stateless projection of injector-observed events into the state vocabulary. **No queue, no scheduling, no per-actor machine injector-side.**
  - Acceptance: zero dictionary/string allocation on the observe path; the documented frame budget holds at 200+ entities; telemetry, VFX, and forecast speak one vocabulary across modes.
  - Verify: guards + a perf probe run. Scope: L.

### ✅ Checkpoint D — observer
- [ ] Live game events project into the shared state vocabulary; frame budget held; no queue or per-actor machine injector-side.

## Phase 6 — the injector drive (T13) — the highest-risk phase

The kernel ticks **inside the Unity frame** and takes over the injector's ad-hoc timing grids. Sequenced last because it touches the hot path this repo has already had to rescue once, and because its failure mode is stutter that no unit test sees. **Gated by P2.**

- [ ] **B24: T13 spec** *(spec first)*
  - Scope the takeover precisely: which existing grids move (the 100 ms shield tick, the 100 ms DoT grid), what stays, and the acceptance baseline — those grids' current *behaviour* is the contract, so this is a substitution, not a redesign.
  - Restate the boundary in the spec: the kernel schedules **our** timeline; Unity still owns when its own actors act.
  - Acceptance: spec reviewed before any injector edit. Scope: M.

- [ ] **B25: per-frame drive + bounded drain** (delivers P1c)
  - `InjectorLoop` advances the clock by the frame's ticks (carry-corrected — a truncating conversion loses 2.4 s/minute at 60 fps) and drains due events under a work budget, resuming next frame.
  - Acceptance: a deliberately oversized backlog **never** blows the frame — it drains across frames and the tick order is unchanged, because simulated time is decoupled from wall-clock so deferral is pacing, not correctness. Zero allocation in the drive loop, asserted.
  - Verify: allocation tests + a backlog scenario. Scope: L.

- [ ] **B26: shield + DoT grids onto the kernel**
  - Replace the 100 ms grids with scheduled events. Shield regen carry must survive the change — it truncates to zero below 1000‰ if driven at 1 ms granularity.
  - Acceptance: shield and DoT behaviour identical to the grids they replace (existing suites unedited); no second scheduler remains in the injector.
  - Verify: Core + injector build + guards. Scope: L.

- [ ] **B27: probe sections + live verification** (delivers P1b)
  - `kernel.*` sections; rerun the B1–B9 matrix.
  - Acceptance: kernel share **≤0.15 ms/frame avg at 200+ entities**, injector total still within ≤2 ms stress budget, **no gen2 GC during a level**, allocation rate unchanged versus the pre-T13 baseline. **⛔ owner-run** — deploys and stress scenarios are the owner's, not mine.
  - Verify: probe run + baseline comparison. Scope: M.

### ✅ Checkpoint E — program complete
- [ ] All modes on stamped `RulesetVersion` history; ban test green; expeditions resolve; one scheduler in the injector, measured inside budget; commit drafts handed over per task group (no git writes).
