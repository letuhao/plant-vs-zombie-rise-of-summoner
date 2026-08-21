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

## Phase 1 — the kernel (T1–T4)

- [x] **B2: T1 clock + event queue** — 13 tests. Zero drift over 10 000 frames at 60 fps; `Reschedule` preserves `Seq` (proven by showing unrelated tie-break order is untouched); `TryAdvance` reports `Blocked` on an empty queue; clock clamps rather than rewinding on a past-due event. Purity guard implemented as a **source scan** rather than reflection — reflection cannot see `DateTime.UtcNow` inside a method body, which is exactly where it would hide.
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

- [ ] **B5: T2c action envelope + published pending resolve**
  - Full envelope (`ActionId`, `TimeCostTicks`, `SpeedChannel`, `WindupTicks`, `ResolveOffsets`, `RecoveryTicks`, `CooldownKey`/`Class`/`StartsAt`, `SlotConsuming`, `PriorityBand`, `Interruptible`, `Commitment`); resolve published as a cancellable handle; cooldown state owned here.
  - Acceptance: a non-zero-wind-up action traverses commit → resolve → recover; a `SlotConsuming = false` action runs at `W=1` without taking a slot; EarlyBound-onto-a-dead-target **fizzles** consuming economy and recovery.
  - Verify: `--filter ~TurnFsm`. Scope: M.

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

### ✅ Checkpoint D — program complete
- [ ] All modes on stamped `RulesetVersion` history; ban test green; expeditions resolve; commit drafts handed over per task group (no git writes).
