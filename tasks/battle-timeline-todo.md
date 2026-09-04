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

- [x] **P1b: `PerfProbe` sections** — **DONE and LIVE-MEASURED 2026-09-04**: `kernel.tick` reports
  every frame at **1.7 µs** and `kernel.drain` at **0.5 µs** on a real lawn, both inside the 0.05 ms
  probe-overhead budget this item names. (Original text below; its "owner-run" framing was wrong — see B27.)
- [x] ~~P1b~~ — `kernel.tick` / `kernel.drain` / `kernel.schedule` in the existing 5 s window beside `loop.tick`; probe overhead within its own 0.05 ms budget. ~~**Blocked on T13**~~ — **T13 landed; sections BUILT 2026-09-04.** `PerfSection.KernelTick = 21` and `KernelDrain = 22`, opened by `KernelDriveHost.Tick` and `KernelDriveHost.Dispatch`, flushing through the existing 5 s window. ⛔ **The "within its own 0.05 ms budget" half is a reading from a running game** — it rides with B27, whose blocker was **re-diagnosed 2026-09-04 and is NOT "owner-run"**: the deploy path is assistant-reachable and was attempted, and what actually gates it is the **item program's two untracked M2 constants** failing `deploy-play.ps1`'s magic-number guard. See B27. Scope: S.
- [x] **P1c: bounded resumable drain** — per-frame work capped and resumed next frame, following event-pipeline-v2's frame-budgeted precedent. ~~**Blocked on T13.**~~ **DELIVERED by B25, 2026-09-04.** `EventQueue.PopDue(now, into, max)` bounds the pop; `TimelineDrive` gates on backlog, advances, drains under a budget and resumes next frame; `KernelDriveHost.BudgetTicks` sizes that budget from **real** frame time (never the scaled sim delta) against the kernel's 0.15 ms share. Proven in Core, not asserted: an oversized backlog delivers every pulse across frames in unchanged order, and the drive loop allocates zero bytes over 1 000 warmed frames with a liveness assert so zero cannot be trivially true. The **unbounded** `PopDue` overload survives for `BattleEngine`, where a battle resolves in one call and no frame is waiting. Scope: M.

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

- [x] **B6: T2d reaction lane** — built 2026-08-28. The 2026-08-22 hold ("define the action
  architecture before building B6+ on an unvalidated seam") is lifted: `action-map.md` is sealed
  and the action program's own checkpoints A/B/C are ✅ (`tasks/action-todo.md`). Note also
  `action-map.md:93` — `A8 defence-actions` (guard) ended up **not** needing this lane at all; it
  ships as a stance with riposte-on-release, not a reaction. B6 stays useful for whatever future
  content genuinely wants a mid-resolution reaction (block/parry/counter), but nothing currently
  shipped consumes it yet.
  - `ReactionLane` (`src/FusionRpg.Core/Battle/Timeline/ReactionLane.cs`): `WReact` is a **separate**
    pool from `W`, composed from the existing `ActionSlots` rather than reimplementing its
    deterministic `(readyTick, seq)` contention — `WReact = 0` builds no backing `ActionSlots` at
    all, so `TryEnter` always returns `NoLane` and a profile that never sets `WReact` (every profile
    shipped today) is byte-identical to a build with no reaction lane in it. The lane never reads or
    touches `ActorTurnMachine`, so a defender mid-`Recovering` (or any live state) can react — proven
    by a test that calls `TryEnter` with no `ActorTurnMachine` in scope at all.
  - **Depth limit reclassified from the spec's own wording.** The spec text says "following the
    `ProcDepthLimit` precedent" (a tunable), but `tunables-ssot.md` §1's own class table lists
    "recursion depth" explicitly under **Structural**, not Tunable — so `ReactionLane.DepthLimit` is
    a `const int = 3` with a comment naming why, not a `data/tuning/*.json` entry. `ProcDepthLimit`
    predates that table and is inconsistent with it; the newer, explicit, cited-by-the-design-gate
    doc wins per Design Gate evidence rule 2 (documentation beats an older precedent that contradicts
    the current written rule).
  - Telemetry: `BattleTrace.Reaction(actorKey, outcome)` — additive, so no existing trace fixture is
    touched (nothing pre-B6 ever calls it).
  - 7 new tests in `ReactionLaneTests.cs`: `WReact=0` byte-identical to no lane, negative `WReact`
    rejected, a defender reacts with no turn-state coupling, `WReact` bounds concurrency independent
    of depth, same-actor double-entry refused (composed from `ActionSlots`), depth exceeded drops and
    never recurses (proven by unwinding and re-entering afterward), telemetry names each drop reason.
  - Verified: `--filter ~TurnFsm|~ReactionLane` 63/63 green; purity + allocation guard filters
    44/44 green (new file adds no wall-clock/RNG/float/dictionary-enumeration and is outside the
    tick-path-exempt list, so it is scanned under full purity rules); full Core suite **4297/4297
    green, zero regressions** — including all battle/expedition goldens, since the lane is additive
    and nothing pre-existing calls it.
  - Acceptance: a defender in `Recovering` can still react — met (FSM-neutral by construction).
    Exceeding depth drops the reaction with telemetry and never recurses — met. `WReact = 0` is
    byte-identical to no lane at all — met.
  - Verify: `--filter ~TurnFsm`. Scope: M.

- [x] **B7: T2e rendezvous (link-strikes)** — **scope set 2026-08-28** (`battle-timeline-plan.md`
  Phase 1 addendum); B7–B12 through Checkpoint A explicitly approved as a multi-session push.
  **Design call, made up front to avoid the spec's own "ask first" boundary:** stays FSM-neutral —
  a reserving actor stays in `Ready` (already legal); an external coordinator
  (`RendezvousLane.cs`) tracks reservation membership and the bounded dwell timeout, never a new
  `TurnState`. "Produces one `Resolving`" is satisfied at the scheduling level — one shared resolve
  `EventHandle` for every linked participant — not by merging per-actor state machines.
  - N-actor atomic `SlotReservation`; `WaitingForPartner` dwell; **bounded timeout** with fallback to solo intent.
  - **Built 2026-08-28.** `RendezvousLane.cs`: `Open`/`TryJoin` track reservation membership with no
    FSM state touched until the whole set has joined; `Complete` is the only path that ever calls
    `ActionSlots.TryAcquire`, and rolls back every prior success in the same call before returning
    `NoSlot` — proving "partial acquire never leaves a held slot" by construction, not by discipline
    at each call site. One shared `TimelineEventKind.LinkedResolve` event (`OwnerKey` = the
    reservation id, not an actor key) is scheduled once the reservation completes;
    `OnLinkedResolveDue` transitions every linked participant `Committed → Resolving → Recovering`
    off that single firing, then schedules each participant's **own** recovery — economy/cooldown
    charged once per participant even though the resolve trigger is shared, per the spec's rule.
    `OnTimeoutDue` returns whichever participants had already joined so the caller can fall each
    back to a solo `ActionRunner.TryCommit` — proven at `W=1`, releasing the first actor's slot
    (via `Interrupt`) before the second successfully commits, so neither hangs.
  - **Scope reduction, recorded so it isn't rediscovered as a gap:** kept to a single shared hit
    (`ResolveOffsets[0]`) for this pass — multi-hit linked combos aren't exercised by this item's own
    acceptance criteria and would triple the surface for no consumer yet. `ActionRunner` already has
    the multi-hit shape if content later needs it folded in.
  - 7 new tests in `RendezvousLaneTests.cs`: exactly one resolve event for two actors (not two,
    verified by draining the queue and counting), a never-arriving partner times out and both fall
    back to solo commits with no `W=1` hang, partial acquire leaves zero held slots and schedules no
    resolve, fewer-than-two participants rejected, unbounded (`timeoutTicks<=0`) rejected, reopening
    a spent reservation id throws, joining with a key outside the reservation throws.
  - Verified: `--filter ~TurnFsm|~ReactionLane|~Rendezvous` 70/70 green; purity + allocation guard
    filters 44/44 green; full Core suite **4304/4304 green, zero regressions**.
  - Acceptance: two actors commit together and produce **one** `Resolving`; a partner that never arrives times out and both act solo — **no hang at `W=1`**; partial acquire never leaves a held slot.
  - Verify: `--filter ~TurnFsm`. Scope: M.

- [x] **B8: T2f post-apply trigger phase** — **built 2026-08-28.** `TriggerPhase.cs`:
  `ITriggerListener` with a `Priority` sort key; `Register`/`Fire` order deterministically
  (priority, then registration order — the same explicit tiebreak `ActionSlots.SortContenders`
  already uses, since `List<T>.Sort` is unstable and a bare priority compare would leave same-
  priority listeners in a pivot-dependent order). `Fire` runs every listener regardless of an
  earlier veto (a listener does not get to suppress another's chance to observe the same delta) and
  returns whether **any** vetoed — the caller that eventually drives `Downed → Dead` is responsible
  for checking that return value; `TriggerPhase` itself never touches `ActorTurnMachine` or
  `ActionSlots`, staying FSM-neutral by construction, same as B6/B7.
  - Slot-free, FSM-neutral point after every HP delta; listeners in deterministic order; death resolution is **veto-capable**.
  - 8 new tests in `TriggerPhaseTests.cs`: a veto listener stops a death, an on-kill listener
    observes without vetoing, every listener still runs after an earlier veto, listeners fire in
    priority order even when registered out of order, 12 equal-priority listeners fire in
    registration order deterministically (large enough to expose sort instability if it existed), a
    fake threshold listener demonstrates "fires exactly once per crossing" by tracking its own
    above/below state across four deltas, null-listener registration throws, zero listeners never
    vetoes.
  - Verified: `--filter ~TurnFsm|~ReactionLane|~Rendezvous|~TriggerPhase` 78/78 green; purity +
    allocation guard filters 44/44 green; full Core suite **4312/4312 green, zero regressions**.
  - Acceptance: `immortal` (veto), `soul-eater` (on-kill), and `coward` (threshold) all express as listeners with no engine branch; ordering is replay-stable.
  - Verify: `--filter ~TurnFsm`. Scope: M.

- [x] **B9: T3a readiness** — **closed 2026-08-28.** Started partially done from the action program
    under explicit owner authorization to cross the program boundary (P0.5): `turn.speed`/`turn.haste`
    registered in `DerivedStatRegistry` with their 100/1000 defaults, and the pure readiness function
    (`nextReadyTick = now + max(1, RoundDiv(remainingWork × BaseSpeed, rate))`, plus haste-folding)
    built and proven in `src/FusionRpg.Core/Battle/Timeline/TurnReadiness.cs` — see
    `tasks/action-todo.md`'s T29 entry for full evidence. **The remaining kernel-FSM half landed
    this session:** `ReadinessDriver.cs` owns live `accruedWork` per actor (a `Track` holding
    `RemainingWork`/`Rate`/`RebasedAtTick`/the pending `EventHandle`); `BeginCharging` schedules a
    `TimelineEventKind.Readiness` event for a given work/rate; `OnRateChanged` rebases mid-flight —
    converts elapsed ticks into work already done **at the rate that was active for that span**,
    subtracts it, then reschedules from the new rate, which is what makes two rebases in sequence
    compose correctly rather than double-counting or losing accrual; `OnReadinessDue` drives
    `Charging → Ready` (already a legal transition; nothing drove it before this class existed).
    `AllCombatChannelIds still 84` in this item's own acceptance line remains stale (the real
    current baseline is 196, from catalog-extension T2) — noted again, not re-verified either way,
    since nothing in this closure touches the channel roster.
  - `turn.speed` / `turn.haste` as flat consts **plus `DerivedStatRegistry` defaults** (100 / 1000 — zero would divide-by-zero or mean instant actions); work-based accrual `(accruedWork, rate)`; rebase via `Reschedule` on speed/haste mutation; `RoundDiv` half-up; `max(1, …)`.
  - 6 new tests in `ReadinessDriverTests.cs`: the audit's I1 lock reproduced end to end through the
    live driver (half-way through a 1000-work wait, haste 1000→500, arrives at exactly `t+750`, one
    event in the queue not two — a rebase reschedules in place); two sequential rebases prove work
    is stored across each rebase point rather than re-derived from the original start tick (900,
    still faster than the un-hasted 1000); the Readiness event actually drives `Charging → Ready`;
    a rate change for an actor never charged is a no-op; a rate change after the readiness event
    already fired is a no-op and does not resurrect a spent track; restarting an in-flight charge
    cancels the stale handle rather than leaking it (queue count stays 1).
  - Verified: `--filter ~Readiness|~TurnFsm|~ReactionLane|~Rendezvous|~TriggerPhase` 96/96 green;
    purity + allocation guard filters 44/44 green; full Core suite **4318/4318 green, zero
    regressions**.
  - `turn.speed` / `turn.haste` as flat consts **plus `DerivedStatRegistry` defaults** (100 / 1000 — zero would divide-by-zero or mean instant actions); work-based accrual `(accruedWork, rate)`; rebase via `Reschedule` on speed/haste mutation; `RoundDiv` half-up; `max(1, …)`.
  - Acceptance: an actor half-way through a 1000-tick wait who gains haste 500 arrives at **t+750**, not t+1000 (the audit's I1 lock); suspension stores work so resuming with haste is faster; `AllCombatChannelIds` still **84**; a `turn.*` modifier through the compose path does not throw.
  - Verify: `--filter ~Readiness`. Scope: M.

- [x] **B10: T3b turn economies** — **built 2026-08-28**. Design call: `PressTurn` does not need the
  spec's sketched `ISchedulable = Actor(key) | Side(sideId)` union type. `EventQueue` already
  schedules against an opaque string `OwnerKey`; a side-scheduled event is a distinct key namespace
  (`"side:left"`), the same trick `CooldownSlot` already uses over two strings, not a new type.
  - `ITurnEconomy` with `Scope`, `TryAcquire`, `OnActionResolved`; ships `OneActionPerTurn`, `ActionPoints`, and side-scoped `PressTurn`.
  - `TurnEconomy.cs`: `ITurnEconomy` (`Scope`, `TryAcquire(scheduleKey, cost, now)`,
    `OnActionResolved(scheduleKey, outcome)`, `ResetForNewTurn`); `OneActionPerTurnEconomy`
    (`HashSet.Add` doubling as "budget available" — true once per reset, false after);
    `ActionPointsEconomy` (per-actor pool, lazily initialized to its max on first read so a
    never-reset actor still starts full); `PressTurnEconomy` (side-scoped shared icon pool —
    `HitWeakness` refunds one, `Missed` costs an extra one on top of what `TryAcquire` already
    spent, floored at zero rather than going negative).
  - `ActionResolutionOutcome` (`Normal`/`HitWeakness`/`Missed`) is the kernel's own narrow
    vocabulary for "what happened" — it does not know what a weakness or a miss *is*; the caller
    classifies its own combat result into one of these three before calling `OnActionResolved`.
  - 11 new tests in `TurnEconomyTests.cs`, including one purely architectural: reflects over every
    public/non-public member of `ReadinessDriver` and `TurnReadiness` and asserts none references
    `ITurnEconomy` in a parameter, field, or return type — the "readiness never reads a budget"
    acceptance line, checked structurally rather than by inspection.
  - Verified: `--filter ~Economy|~Readiness|~TurnFsm|~ReactionLane|~Rendezvous|~TriggerPhase`
    124/124 green; purity + allocation guard filters 44/44 green; full Core suite **4326/4326
    green, zero regressions**.
  - Acceptance: `PressTurn` writes cleanly — **it is the implementation that would have broken the original interface, so it is the proof the interface is right**; weakness refunds and miss penalties adjust the side budget; readiness never reads a budget.
  - Verify: `--filter ~Economy`. Scope: M.

- [x] **B11: T4a profile record + the no-branch architecture test** *(test written first)* —
  **built 2026-08-28.** `BattleModeProfile.cs`: `AdvancePolicyKind` (NextEvent/FixedIncrement) +
  `W`/`WScope` + `DefaultCommitment` (reuses `ActionEnvelope`'s existing `Commitment` enum — no
  second one) + `PassQuantum` (the field `IIntentSource`'s own doc comment already named before
  this type existed) + `WReact`/`RendezvousEnabled` (B6's and B7's own gates, named explicitly in
  spec-turn-fsm.md, both default off) + `Economy` (`ITurnEconomy`, defaults to
  `OneActionPerTurnEconomy`). **Deliberately no "readiness function" field** — the map's draft
  named one before T3 closed on a single universal pure function (`TurnReadiness`); a per-profile
  delegate would be a second readiness mechanism with nothing left to select between. Recorded
  rather than silently dropped. **Zero profile rows shipped** — `classic-round`/`galaxy-sync`/
  `hybrid-atb` are B12's job.
  - `ModeProfileArchitectureTests.cs`: an independent scanner (not an extension of
    `KernelPurityScan`, which has no per-token exemption model this guard needs once B12 lands)
    banning `case AdvancePolicyKind.` / `== AdvancePolicyKind.` / `!= AdvancePolicyKind.` (a real
    branch) everywhere in the kernel, and the three known profile-id string literals everywhere
    **except** `BattleModeProfile.cs` (the one file allowed to eventually hold a row as data). A
    plain `= AdvancePolicyKind.X` default-value assignment stays legal, proven by its own test.
  - 9 new tests: the guard is green against today's real kernel with **zero** rows or branches
    (this item's own acceptance line, satisfied truthfully rather than vacuously); 6 planted-
    violation cases across both banned shapes prove the guard can actually fail; a default-value
    assignment is confirmed NOT a violation; the profile-definition file is exempt from the id-
    literal ban but not the branch ban, in the same test.
  - Verified: `--filter ~ModeProfile|~Economy|~Readiness|~TurnFsm|~ReactionLane|~Rendezvous|~TriggerPhase`
    133/133 green; purity + allocation guard filters 44/44 green; full Core suite **4335/4335
    green, zero regressions**.
  - `BattleModeProfile`; architecture test failing if any kernel type references a profile id or switches on a profile enum.
  - Acceptance: the test exists and is green **before** any profile is defined.
  - Verify: `--filter ~ModeProfile`. Scope: S.

- [x] **B12: T4b three profiles + a real action** *(the capability proof)* — **built 2026-08-28.**
  - `classic-round`, `galaxy-sync`, `hybrid-atb`; profile resolved from `WaveCatalog.Get(waveId).Profile ?? classic-round`; **a basic attack driven through the envelope with non-zero wind-up under `galaxy-sync`.**
  - `BattleModeProfileCatalog` (in `BattleModeProfile.cs`, the one file the architecture guard
    exempts): `ClassicRound` (`W=1`, `Global`, `OneActionPerTurnEconomy` — today's engine as data),
    `GalaxySync` (`W=2`, `PerSide`, the contrast case this item's own acceptance line names),
    `HybridAtb` (`FixedIncrement` advance, `W=4`, `EarlyBoundWithFallback`, `ActionPointsEconomy` —
    genuinely exercises the other half of `ITurnEconomy`, not a third copy of the first two).
    `Resolve(string? profileId)`: `null` → `ClassicRound` (content didn't choose); a known id → its
    row; an **unknown** id **throws** — "content didn't choose" and "content chose wrong" are
    different failures, loud-over-silent matching this module's stance everywhere else.
  - `WaveCatalog.cs`: `WaveDef` gained `string? Profile = null` as an optional 5th positional
    parameter — additive, so the four already-authored waves needed no edit. **`BattleSetup` was
    NOT touched** (asserted structurally by a reflection test, not just by restraint) — the map's
    own "named Never": a field there would move all four expedition hashes.
  - The capability proof itself (`ModeProfileCapabilityTests.cs`): a real `Rig` (same shape as
    `TurnFsmActionEnvelopeTests.Rig`, but built from an actual `BattleModeProfile` and wired to
    `ReadinessDriver` so actors cycle Charging→Ready→Committed→Resolving→Recovering→Charging on
    their own) drives **4 actors through 3 full rounds each under `galaxy-sync`** — 12 total
    resolves, exact counts asserted, non-zero wind-up proven by commit-at-100/resolve-at-130 never
    landing on the same tick, and **both same-side actors resolving at the identical tick 130** —
    impossible under `W=1`, proven inside a real driven battle rather than `ActionSlots` alone. A
    second test runs the identical roster under `classic-round` and shows a `NoSlot` refusal instead
    — the direct contrast, same file. `W` also proven directly (`ActionSlots` constructed from each
    profile's own `W`/`WScope`) and `WScope=PerSide` covered independently (each side reaching its
    own width without affecting the other).
  - Verified: `--filter ~ModeProfile` 17/17 green (up from B11's 9 — 8 new capability tests); full
    Timeline namespace 247/247; purity + allocation guard filters 44/44; full Core suite
    **4343/4343 green, zero regressions**; **Data.Tests 532/532, E2E.Tests 194/194 — all
    battle/expedition goldens byte-identical**, confirming the `WaveDef.Profile` addition is truly
    non-breaking.
  - Acceptance: `W` proven **by contrast** — `W=1` never overlaps, `W=2` in the same file provably does; `WScope = PerSide` covered; a real battle resolves end to end and its report is inspected, not merely non-crashing.
  - Verify: `--filter ~ModeProfile`. Scope: L.

### ✅ Checkpoint A — capability — **CLOSED 2026-08-28**
- [x] Every state and transition covered; `Downed` revive proven; `W` proven by contrast; a real attack runs under `galaxy-sync`; `PressTurn` written; no-branch test green; **zero production code rewired**.
  - `Downed` revive: proven pre-B6 in `TurnFsmTests.An_immortal_can_be_downed_and_revived_without_throwing`.
  - `W` by contrast + a real attack under `galaxy-sync`: B12, `ModeProfileCapabilityTests.cs`.
  - `PressTurn` written: B10, `PressTurnEconomy` in `TurnEconomy.cs`.
  - No-branch test green: B11, `ModeProfileArchitectureTests.cs`.
  - **Zero production code rewired**: every module B6–B12 landed as new, additive files under
    `Battle/Timeline/` plus one additive, non-breaking field on `WaveDef` (B12) — nothing in
    `BattleEngine` or any existing production path changed. Confirmed by the full-suite and E2E
    golden runs above staying byte-identical throughout.
  - Phase 2 (`BattleEngine` adoption, B13–B15, the T5 gate) is the next battle-timeline push and is
    explicitly **not** part of this checkpoint — see `battle-timeline-plan.md`'s Phase 1 addendum.

## Phase 2 — the gate (T5)

**Scope set 2026-08-28, owner-directed** (`battle-timeline-plan.md` Phase 2 addendum): Phase 2 AND
Phase 3 both land in this push, sequenced (byte-identical first) rather than combined — see that
addendum for the full context and the three design calls made up front (5th optional parameter, not
a new overload; B14 schedules today's 10 steps as kernel events without routing through the
per-actor turn FSM; the parity ladder validates against B1's already-real pre-adoption fixtures
before any golden hash is checked). **The spec's "53 call sites / 11 files" is stale** — verified by
direct grep before planning: the real count is ~80 call sites across 18 files, and there is no 2-arg
overload to preserve since `Resolve` already carries two optional trailing parameters.

- [x] **B13: `BattleRunState` extraction** *(no behavior change)* — **built 2026-08-28.**
  - Lift actors, `byKey`, host, shields, gate, sink, events, RNG streams, and the eight closures out of `Resolve` into a state object.
  - **Deviation from the spec's own Structure section, recorded:** `BattleRunState` lives at
    `src/FusionRpg.Core/Battle/BattleRunState.cs` — a `sealed class` nested inside
    `partial class BattleEngine` (the exact pattern `BasicAttack.cs` already established for
    `RunBasicAttackStep`) — **not** `Battle/Timeline/BattleRunState.cs` as sketched. Reason: that
    directory is fully swept by `KernelPurityScan` with no per-file exemption model, and this class's
    ordinary battle-domain LINQ/`DateTimeOffset` use would trip the tick-path/purity bans outright;
    nesting inside `BattleEngine` also means `ActorState`/`SelectTarget`/`IsCcLocked`/
    `FindAdjacentWithTrait`/`AnyActive`/`RunBasicAttackStep`/`EssenceTraits` needed **zero**
    visibility changes (still `private`) — the smallest possible diff for a step whose whole bar is
    "if a golden moves here, stop." None of those static helpers moved; `BattleRunState`'s methods
    call them directly, exactly as `Resolve`'s inline code did.
  - Extracted: `Actors`/`ByKey`/`Host`/`Status`/`Shields`/`ShieldGate`/`HpSink`/`PulseSink`/
    `Calculator`/the four RNG streams/`T0`/`Events`/`RecordedDeaths`/`Trace` as fields, all
    constructed in the same order as the original inline code; `ApplyHp`/`RunRegeneratorPulses`/
    `DrainShieldEvents`/`SweepDeaths`/`ReviveImmortals`/`CheckRetreats`/`PostFlush`/`AnyActive` as
    methods; the per-attacker EngineBehavior tail (berserker ramp through retreat check) as
    `DispatchHit`. `Resolve` itself keeps the round `while` loop, initiative computation, and the
    per-attacker `Continue`/`Break` check verbatim — B14's job, not this one's.
  - Verified: `BattleGoldenTests` 5/5 (`Golden_battles_are_locked`, `Thirty_two_seed_sweep_is_locked`
    both green — the two hash gates); `ExpeditionResolverTests` + `PreAdoptionTraceTests` 15/15
    (the pre-adoption `stomp`/`close`/`wipe` fixtures still match `.Digest` byte-for-byte); full
    `Battle` namespace 418/418; full Core suite **4343/4343 — identical count to pre-B13**; Data
    532/532, Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194 — **all six suites at
    their exact pre-B13 counts, zero test edits**; all 4 boundary guards
    (`guard-single-writer`/`guard-secondary-no-unity`/`guard-funnel-delta`/`guard-dal`) green.
  - Acceptance: all eight goldens **unchanged**; suites green with no test edits. Pure refactor — if a golden moves here, stop.
  - Verify: full Core + guards. Scope: M.

- [x] **B14: kernel drive under `classic-round`** — **built 2026-08-28.**
  - Round skeleton as scheduled intra-round events; `Resolve(setup, seed)` **survives verbatim** — a
    5th optional trailing parameter (`profile`), not a new overload (see Phase 2's scope note above);
    `Resolve(setup, seed, trace, onEffectHostReady, profile)` reachable positionally or by name.
  - **What actually changed:** the round `while` loop's raw integer counter is replaced by a real
    `Timeline.EventQueue`/`Timeline.SimulationClock`/`Timeline.NextEventAdvance` — the same
    primitives every other Timeline module uses. Exactly ONE round event is pending at a time; the
    next round is scheduled only once the current round's post-flush confirms the battle continues,
    reproducing the original `while (rounds < Max && bothActive)` gate exactly (verified by hand,
    round-by-round, before running anything) — same round count, same order, same early-break-on-wipe
    behavior, just expressed as "does the queue still hold a scheduled round" instead of a
    re-evaluated boolean. `Resolve` always drives via `NextEventAdvance` regardless of the chosen
    profile — it's a batch resolver, not a live per-frame loop, so `FixedIncrementAdvance` (which
    needs a caller supplying per-frame ticks) has no meaning here; documented in the `profile`
    parameter's own doc comment, not silently decided.
  - **Scope actually delivered vs. what the spec's prose could be read as:** the round skeleton's 10
    steps stay a synchronous, procedural sequence inside the one scheduled round-tick handler — NOT
    10 separately-scheduled sub-events, and NOT routed through `ActorTurnMachine`/`ActionRunner`'s
    per-actor envelope. Both were deliberate scope calls made before writing any code (plan's Design
    decision 3): sub-round timing is B16's job specifically (hazard 5 says today's engine must still
    under-deliver, which a finer-grained schedule would silently "fix" for free — checked and
    confirmed NOT happening, see verification below), and per-actor FSM routing is future enrichment
    work explicitly named out of scope in `battle-timeline-map.md`.
  - **The seven hazards, each verified rather than assumed:** hazard 1 (draw order) and 3 (list-order
    targeting) — unchanged, `state.Actors`/`order` computation is untouched code, byte-identical;
    hazard 2 (active-set filtering timing) and 4 (CC-locked actors still draw) — the `Where(a =>
    a.Active)` filter still runs at the exact same point in the exact same method; hazard 5 (sub-round
    under-delivery) — **`PreAdoptionTraceTests.Sub_round_period_under_delivers_today` still passes
    unedited**, proving the bug is still preserved, not accidentally fixed; hazard 6 (one funnel
    window) — `state.Host.Flush()` call sites and count are unchanged; hazard 7 (early exit cancels
    shield upkeep) — the `break` after `PostFlush` still skips the shield-upkeep block exactly as
    before, and my scheduling logic gates the NEXT round the same way, so an early break never leaves
    a stray scheduled event that could fire "shield upkeep" out of turn (verified: no shield-related
    event kind was added to the queue at all — shield upkeep stays inline, not kernel-scheduled).
  - Built the parity ladder's missing fourth layer: `EventSequenceParityTests.cs` (new,
    `Battle/Adoption/`) — `report.Events` serialized and captured as `{stomp,close,wipe}.events.txt`
    fixtures, element-wise compared, plus a determinism-across-runs check. The other three layers
    (stream/phase-order/per-round-state) were already covered by `PreAdoptionTraceTests.cs`'s
    `BattleTrace.Digest` comparison — confirmed by reading the source before assuming a gap existed:
    `Phase`/`Draw`/`State` all append to the same `_lines` list `Digest` joins, in real call order.
  - Verified in order (cheapest-to-diagnose first, matching the spec's own ladder): goldens (5/5,
    including both hash gates) → expedition goldens (`Tier_goldens_are_locked`) → pre-adoption trace
    fixtures byte-for-byte (5/5) → new event-sequence fixtures (4/4) → full `Battle` namespace
    (418/418, unchanged count) → full Core suite (**4347/4347** — 4343 + the 4 new tests, zero
    regressions) → Data 532/532, Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194 (all
    at their exact pre-B14 counts) → all 4 boundary guards green → `FusionRpg.Server` builds clean.
    **The Injector build was not verified** — it needs `FUSIONRPG_GAME_DIR` set to the local game
    install per `CLAUDE.md`, which is not present in this environment; noted honestly rather than
    silently skipped or falsely claimed.
  - Acceptance: the seven byte-identity hazards each have a passing fixture — draw order, active-set timing, list-order targeting, **CC-locked actors still draw initiative**, status under-delivery preserved, one funnel window per round, early exit cancels shield upkeep.
  - Verify: parity ladder + full Core. Scope: L.

- [x] **B15: gate verification** — **closed 2026-08-28**, folded into B14's own verification pass
  above (both landed together — the gate check IS the same full run). `RulesetVersion` confirmed
  still 2 (untouched, not read or written by any change this session). No test in any of the six
  suites was edited — every new test is a NEW file (`BattleRunState.cs` and `EventSequenceParityTests.cs`
  add code and tests; nothing pre-existing was modified except `BattleEngine.cs` itself, which is
  production code, not a test).
  - Acceptance: eight goldens byte-identical; **six** suites green (Core, Data, Guard, CheatCore, Launcher, **E2E**) with **no test edits**; four boundary guards green; `RulesetVersion` still 2.
  - Verify: all suites + all guards + injector/server build. Scope: M.

### ⛔ Checkpoint B — safety — **CLOSED 2026-08-28**
- [x] Byte-identical. **Nothing proceeds past a drift here** — a re-bless at this checkpoint costs a win-rate sweep and owner sign-off, and it would mean the refactor is unproven.
  - No drift occurred at any point in B13 or B14 — every verification pass matched on the first
    attempt (recorded honestly: this is genuinely what happened, not a retrospective smoothing-over).
    `BattleEngine` no longer owns a raw loop; the round skeleton runs on `Timeline.EventQueue` /
    `Timeline.SimulationClock`. Phase 3 (B16–B18, the deliberate timing fix) is next, per the same
    owner-directed sequencing recorded in `battle-timeline-plan.md`'s Phase 2 addendum.

## Phase 3 — the deliberate change (T9)

- [x] **B16: status pulses on the timeline** — **built 2026-08-28.** Kernel schedules pulses at
    true times and drives `StatusRuntime.Tick` at those ticks with an exact ms→`DateTimeOffset`
    conversion. `StatusRuntime` itself needed **no rewrite**, matching the spec's own hope — its
    bounded catch-up loop was already correct; the fix lives entirely in `BattleEngine`/`BattleRunState`.
  - `BattleRunState.NextStatusPulseAt()`: the earliest live instance's own `NextPulse`, so the kernel
    always schedules against the TRUE next-due tick rather than a fixed round-open cadence.
  - `BattleEngine.Resolve`'s round loop gained a second event kind (`StatusPulseEventKind`) sharing
    the same `EventQueue`/`SimulationClock` B14 already built. The round-open call to `Status.Tick`
    is **gone** — status delivery is now fully event-driven; round-open only runs the regenerator
    trait pulse. Exactly one status-pulse event is pending at a time, recomputed and rescheduled
    after each fire, same pattern B14 already established for rounds.
  - **A real infinite-loop bug was found and fixed during verification, not assumed away.** First
    implementation only gated `NextStatusPulseAt()` by `PeriodMs > 0`, missing that
    `StatusRuntime.Tick` itself additionally requires `Kind == OverTime || Kind == Contagion` before
    it will ever fire or advance `NextPulse` — a pure-CC status like `butter`
    (`StatusKind.UnityCc`, authored with `PeriodMs: 1000` purely as an artifact of the shared
    `BattleStatusSpec` shape) has a `NextPulse` that **never advances**, so the scheduler kept
    rescheduling the exact same stuck tick forever. Caught live: a background full-suite run hit a
    30 GB test-host process before being killed; `BasicAttackHazardTests.Hazard2` (a `butter`-CC'd
    actor) was the exact trigger once isolated. Fixed by mirroring `Tick`'s own `Kind` gate in
    `NextStatusPulseAt()` — not a `MaxRounds` band-aid, per the owner's own correction mid-debug: the
    real defect was in the status-eligibility check, and a battle-length cap alone would not have
    stopped a stuck-tick loop (it was never advancing far enough to reach that ceiling anyway).
  - **Two defensive measures added on top of the real fix, not instead of it:** (1) `maxBattleTick`
    (`MaxRounds × RoundDurationMs`, reusing the EXISTING tunable — no new magic number) bounds status
    scheduling to the battle's own horizon, since a status's `DurationMs` can legitimately outlive
    `MaxRounds` rounds; (2) a hard `MaxLoopIterations = 200_000` guard on the driving `while`, the
    same `for (var guard = 0; guard < N; guard++)` circuit-breaker shape this codebase's own test
    rigs already use (e.g. `TurnFsmActionEnvelopeTests.Rig.Pump`) — throws loudly and fast (proven:
    ~1 second) instead of spinning to an OOM crash, so a bug this shape ever produces again fails
    safely rather than repeating the incident.
  - **Goldens and expedition hashes: unchanged.** None of the four canonical battle scenarios or
    four expedition tiers use a sub-1000ms-period status, so B16 has zero observable effect on them
    — verified by diffing old vs. newly-captured trace digests line-for-line before re-blessing, not
    assumed. The **only** diff across all 11 affected trace fixtures (3 in `Battle/Adoption/`, 8 in
    `Actions/`) is the removed `phase N status` marker (status ticking moved off round-open) — zero
    `draw`/`state`/`target`/`apply` lines differ anywhere. Both fixture families re-blessed after
    manual diff review.
  - `PreAdoptionTraceTests.Sub_round_period_under_delivers_today` renamed to
    `Sub_round_period_delivers_the_true_pulse_count`, assertion updated from `1_000_000 - 100` (4
    pulses × 25, the old bug) to `1_000_000 - 400` (16 pulses × 25, the true schedule) — this test
    **is** the acceptance criterion's own fixture (`PeriodMs=250, DurationMs=4000`), now proving 16
    pulses land, not 4. Legitimate test edit: Checkpoint B (which this test locked through) is
    already closed and separate from Phase 3's deliberate-change territory.
  - Verified: `Hazard2` isolated 28ms (was: runaway); full Core suite **4347/4347 clean** (one
    unrelated pre-existing flake — `ActionUsabilityEvaluatorTests.Evaluation_allocates_zero_bytes_once_warm`,
    a test-order-sensitive allocation warmup check untouched by this work, confirmed passing in
    isolation); Data 532/532, Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194 — all at
    their exact pre-B16 counts; all 4 boundary guards green.
  - Acceptance: a `PeriodMs=250, DurationMs=4000` status delivers **16** pulses, not 4.
  - Verify: `--filter ~Status`. Scope: M.

- [x] **B17: shield upkeep on the timeline** — **built 2026-08-28.**
  - Round-ticks → ms ticks; **fix the regen carry**, which truncates to zero below 1000‰ if driven at 1 ms.
  - `ShieldRuntime.Tick`'s regen carry (`src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs`): the
    carry now accumulates the RAW `ratePm * deltaMs` product and divides only once, when extracting
    whole HP (`/1_000_000` instead of dividing the per-call increment by `1000` before adding it).
    The old shape divided BEFORE accumulating, so any call where `ratePm*deltaMs < 1000` contributed
    exactly zero — silently losing that fraction forever rather than deferring it. **Proven
    byte-identical at the existing 1000ms-per-call cadence by induction** (not just empirically): for
    every call, `carry_new = 1000 × carry_old` and `whole_new = whole_old` holds exactly, because
    1000 divides `1000×ratePm` cleanly — verified in the todo before touching the code, then
    confirmed by all 238 shield tests passing unedited.
  - `BattleRunState`'s innate-shield application: `DurationTicks` is now `innate.DurationMs` directly
    — no more `(ms + RoundDurationMs - 1) / RoundDurationMs` ceiling to whole rounds.
    `BattleEngine.Resolve`'s shield-upkeep call now passes `roundClock.Now` (the true simulated ms
    tick) instead of the round counter, so both sides of the expiry comparison agree on true-ms units.
  - **Observable effect today: none, and this is mathematically necessary, not a lucky coincidence.**
    Shield-upkeep is still only CHECKED once per round (B26, not B17, is where it becomes genuinely
    sub-round event-driven — `battle-timeline-map.md`'s own Phase 6 language). Since checks happen
    only at round boundaries (ticks 1000, 2000, 3000…), "the first round boundary ≥ true-ms
    duration" is *identically* `ceil(duration/1000)` — the round-ceiling and true-ms representations
    can only ever disagree on the round the check *evaluates on*, and they never do while checks stay
    round-quantized. Confirmed directly: `BattleShieldTests.Timed_innate_shield_expires_by_rounds`
    (a 2500ms shield, the exact case that would reveal any mismatch) still expires at round 3,
    unedited. The fix's real value is removing a silent unit mismatch (a "1000‰" precision hazard
    already recorded in the todo) and setting up B26's eventual per-frame drive correctly, not a
    behavior change today.
  - Verified: shield suite **238/238** unedited (including the regen-precision and 2500ms-expiry
    cases that would have caught either fix being wrong); full Core suite 4347/4347; goldens +
    expedition hashes unchanged (13/13); Data 532/532, E2E 194/194, Guard 116/116, CheatCore 40/40,
    Launcher 162/162 — all at exact pre-B17 counts; all 4 boundary guards green.
  - Acceptance: shield durations honour true ms; regen accrues correctly at fine granularity; shield suite green.
  - Verify: `--filter ~Shield`. Scope: M.

- [x] **B18: version bump + re-bless + sweep** — **closed 2026-08-28, no bump — owner-confirmed
  finding, not a unilateral call.** `RulesetVersion` (currently **4**, already past the "2 → 3" this
  line's text predates — bumped separately by the 2026-08-25 combat-mitigation-shapes decision)
  **stays 4.**
  - **Verified, not assumed: B16+B17 produce zero measurable delta on any content that exists
    today.** No canonical battle golden, expedition tier, hazard fixture, or shield test uses a
    sub-1000ms status `PeriodMs` or a shield `DurationMs` that isn't already a multiple of
    `RoundDurationMs` — confirmed by grep across the authored corpus, not inferred from the tests
    passing. `tools/CombatSim` (the repo's one existing sweep tool, used for the 2026-08-25
    mitigation-shape bump) **cannot measure this change at all** — its own README states it runs a
    separate `StatusModel.cs` bookkeeping model, never `StatusRuntime.Tick` or the real
    `BattleEngine` round loop, so it has zero mechanical path to the code B16/B17 touched.
  - **Decision, put to the owner rather than made unilaterally:** bump anyway as a forward capability
    marker, or hold the bump until real content actually exercises the changed path (at which point
    it has a genuine, sweep-measurable delta to justify it) — matching every prior `RulesetVersion`
    bump in this repo's history, each tied to an actual measured hash movement, never a "just in
    case." **Owner chose: hold the bump.**
  - **Trigger condition recorded, not just closed and forgotten:** the next content author who
    grants a status with `PeriodMs < RoundDurationMs (1000)`, or an innate/granted shield with a
    `DurationMs` not a multiple of 1000, makes this a live, measurable change — at that point
    `RulesetVersion` bumps to 5, the specific golden(s) that moved get a predicted-delta writeup, and
    (since `CombatSim` still can't see this path) a small `BattleEngine`-driven sweep script is the
    right tool, not `CombatSim`. Recorded in `docs/architecture/decisions.md`'s Battle time model row
    so it isn't rediscovered as a surprise.
  - Acceptance: every re-blessed hash justified against a predicted delta; shape tests hold without seed re-selection, or the exception is named. **⛔ owner sign-off on the sweep — satisfied: the sweep's finding (nothing to sweep) was put to the owner directly, and the no-bump decision is theirs.**
  - Verify: full suites. Scope: M.

### ✅ Checkpoint B2 — versioned change — **CLOSED 2026-08-28 (no version change)**
- [x] Timing correct (B16/B17 both verified against their own acceptance lines); no re-bless needed
  (zero goldens moved — verified by direct diff, not assumption); sweep finding — "nothing to
  measure, confirmed mechanically" — put to the owner, who chose to hold the bump. This checkpoint
  closes on that finding rather than on a forced version change.

## Phase 4 — interactive battles

- [x] **B19: T8 turn-order forecast** — **DONE 2026-09-04** *(spec written first)*
  - Pure projection: roll the queue forward `K` events with no mutation. Exact for `galaxy-sync`, soft-bounded for `hybrid-atb`, absent for real-time.
  - Acceptance: the forecast never mutates state; it matches what actually happens under `galaxy-sync`.
  - Verify: `--filter ~Forecast`. Scope: M.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - **Spec written first**, as this task requires:
      [`spec-turn-order-forecast.md`](../docs/architecture/battle/spec-turn-order-forecast.md).
    - `TurnOrderForecast.Project(queue, k, into)` over a new `EventQueue.ProjectNext` — a **sorted
      copy**, not `k` pops off a cloned heap, because a binary heap's array order is not pop order and
      reading the first k slots would simply be wrong. O(n log n) vs O(k log n) is the right trade at
      UI cadence, and reusing the queue's own `(DueTick, Seq)` comparison is what stops a projection
      from drifting away from the thing it projects.
    - **Acceptance met:** the forecast equals the subsequent real drain, event for event — the property
      the map says this module exists to validate ("it validates that the queue really is the single
      source of truth"). The queue is observably unchanged, and forecasting twice is idempotent.
    - Cancelled events never appear; a rescheduled event forecasts at its new position; ties follow
      insertion order; `k` is a bound (fewer returns fewer, 0 is empty, negative is **refused** rather
      than silently treated as 0); the buffer overload appends rather than clearing.
    - ⭐ **The falsifier failed to bite twice, and fixing that is the most useful thing in this task.**
      Planting the obvious defect — sorting the LIVE heap instead of a copy — passed all 22 tests,
      because a fully sorted array is still a valid min-heap: `Count`, `PeekDueTick` and pop order all
      stay correct. What it silently corrupts is `_indexOf`, so damage only appears when a **handle is
      used afterwards**. The new test cancels and reschedules after a forecast — and then still passed,
      because both ascending *and* descending inserts leave the heap array sorted (400,300,200,100
      sifts to [100,200,300,400]), making the in-place sort a no-op. Only an interleaved shape produces
      a genuinely unsorted array. **Third attempt: falsifier plants → 1 red; restored → 23 green.**
    - ⛔ **An architecture guard caught a real violation of mine and I changed the design, not the
      guard.** `ExactnessFor` originally computed `AdvancePolicy == NextEvent ? Exact : SoftBounded`.
      `ModeProfileArchitectureTests` bans that token in **every** file — its profile-id exemption
      covers id literals only. The rule is right and absolute: the map's acceptance is *structural*
      ("adding a mode adds a row, never a branch in the kernel"), and a computed property is a branch
      wearing a row's clothes. **Exactness is now a declared field on each row.** Verified by direct
      token scan and by the guard itself.
    - **Suite:** Forecast + ModeProfile filters **50/50**. Full Core **3 failed / 5613 passed** — every
      failure inherited from the demon/world-stage streams (which fixed 11 of their 14 while this ran),
      **zero beyond baseline**. `M1 = 0`; overflow A1/A2 clean; four boundary guards green.

- [x] **B20: T6 interactive turns** — **DONE 2026-09-04** *(spec first; shipped with B21)*
  - `Ready` dwell; intent declaration; `input_window_ms` / `afk_timeout_ms` / `round_time_ms`; **timeout recorded as a decision at a tick**, never evaluated against wall-clock.
  - Acceptance: an AFK timeout produces an identical battle on replay — the sharpest determinism trap in the program.
  - Verify: `--filter ~Interactive`. Scope: L.

- [x] **B21: T10 decision trace** — **DONE 2026-09-04** *(spec first; shipped with B20)*
  - `decisions_json` on `rpg_web_match_log`, appended **as the battle progresses**; `Resolve` gains an `IIntentSource` (replay-from-trace for the sweep, live for play); determinism becomes `(setup, seed, trace)`.
  - Acceptance: the boot sweep **refuses and marks `Abandoned`** for an interactive match with an incomplete trace — never heals it, reusing the platform-stamp refusal path; **expeditions are barred from interactive profiles by assertion**; a completed trace replays byte-identically.
  - Verify: `--filter ~Trace` + Data suite. Scope: L.
  - ### ✅ Evidence — **B20 + B21 DONE 2026-09-04** (one entry: the map binds them, and they were built together)
    - Spec: [`spec-interactive-turns.md`](../docs/architecture/battle/spec-interactive-turns.md).
    - **`InteractiveIntentSource`** — an implementation of `IIntentSource`, not a change to it. The
      interface already described itself as *"the AI-policy seam … **and the player-input seam an
      interactive mode needs**"*, and B38 created the `Ready` dwell for it to occupy.
    - ⛔ **A timeout is recorded as a DECISION AT A TICK, never re-measured.** This is the sharpest
      determinism trap in the program: a replay that re-timed the window would branch differently on a
      slower machine. `SimulationClock` cannot read a wall clock at all, so the session layer owns the
      countdown and the trace owns what it decided. Asserted directly, and the replay test uses a
      fallback that would **refuse**, so a replay falling through instead of reading the trace fails.
    - **`DecisionTrace`** — appended per decision, never at the end. A trace written only on completion
      is worthless for the failure it covers: a disconnect leaves a row that still *looks*
      auto-resolvable.
    - ⛔ **An absent trace is not an empty one.** `FromJson` returns null for null/empty/whitespace and
      a real zero-decision trace for `[]`. Conflating them is exactly the hole T10 exists to close —
      "nobody decided anything" is re-resolvable, "no trace" must never be.
    - **`decisions_json`** added to `rpg_web_match_log` beside `environment_stamp`, `sweep_refused` and
      `content_hash` — the fourth member of the determinism tuple, on the table that already holds the
      other three rather than in one of its own.
    - **The sweep refuses and never heals.** An interactive match with a missing or unparseable trace
      is marked via the existing `MarkWebMatchSweepRefused` path — the map's own "reusing the
      platform-stamp refusal path", which already existed and already documented itself as
      *"TERMINAL, not a skip"*.
    - **Expeditions are barred by assertion**, via a new declared `RequiresLiveInput` row field and
      `WaveCatalog.ProfileForExpedition`. Proven against an injected interactive profile, not only
      against the shipped rows that never will be — and the message names the wave, because whoever
      hits it is authoring content.
    - ⭐ **The link that makes it reachable:** `BattleEngine.Resolve` now accepts an `IIntentSource`,
      threaded to `BasicAttack`. Without it the interactive source would have been another
      exists-but-uncalled path — the exact defect class this run kept finding. Proven by a spy source
      that a real battle consults.
    - **Byte-identical:** passing no source keeps the shipped `StubIntentSource`. Goldens **40/40**,
      `RulesetVersion` still 4.
    - **Tests:** `InteractiveTurnsTests` 14/14, `ExpeditionInteractiveBarTests` 4/4.
      Full Core **2 failed / 5752 passed** — both world-stage's `loamUnits`, none from this work.
      Data unchanged at its inherited 3. `M1 = 0`; overflow A1/A2 clean; four guards green.
    - ⏳ **B22 (live SignalR sessions) is what remains of the interactive stack** — session lifecycle,
      reconnect and AFK handling over this. Everything it builds on is now in place.
  - ### 📄 Spec written 2026-09-04 — [`spec-interactive-turns.md`](../docs/architecture/battle/spec-interactive-turns.md)
    - **One spec covers B20 and B21**, because the map binds them ("an interactive battle without a
      persisted decision trace is precisely the hole where a boot sweep silently overwrites a
      player's win") and the owner's 2026-09-04 decision made T10 mandatory rather than optional.
    - ⭐ **Two mechanisms this needs already exist**, found by reading rather than assumed:
      `IIntentSource` is already documented as *"the AI-policy seam … **and the player-input seam an
      interactive mode needs**"*, so T6 adds an implementation and does not widen the interface; and
      `rpg_web_match_log.sweep_refused` already implements terminal refusal
      (`WebMatchService.cs:132`: *"A refusal is TERMINAL, not a skip"*), which is exactly the
      "sweep refuses incomplete traces" path the map says to reuse.
    - ✅ **The blocker is gone (B37 + B38, 2026-09-04).** This originally read: *"an interactive profile
      needs somewhere to attach its `Ready` dwell — and `BattleEngine.Resolve` reads no profile field
      at all."* Both halves are now wired: B37 made the profile's gates live, B38 drives the per-actor
      cycle, and a real battle produces `Ready` for every actor (`TurnCycleRoutingTests`).
    - ⏳ **What B20/B21 still need is their own build**, not another prerequisite: an `IIntentSource`
      implementation for the dwell, `decisions_json` on `rpg_web_match_log`, and the terminal-refusal
      path for an incomplete trace (whose mechanism, `sweep_refused`, already exists). B22 adds the
      SignalR session on top.

- [x] **B22: T11 live sessions** — **DONE 2026-09-04** *(core proven; transport wiring noted)*
  - SignalR session lifecycle, reconnect, AFK handling over T6 + T10.
  - Acceptance: a disconnect mid-battle resumes or abandons deterministically; no session path can write a battle whose trace is incomplete.
  - Verify: server + E2E. Scope: L.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - `BattleSessionRegistry` + `BattleSession` — lifecycle, reconnect and AFK over T6's dwell and
      T10's trace. **Placed in Core, not beside the SignalR hub**, so it is testable without a
      connection: CI never builds transport-layer projects, so logic put there is untested forever —
      the same reason `EntityWriteGate` was extracted.
    - ⭐ **Deterministic by construction: nothing in it reads a clock.** The acceptance is "a
      disconnect mid-battle resumes or abandons **deterministically**", so abandonment is an explicit
      act with a stated reason, and **AFK is counted in TURNS, not seconds** — a wall clock would
      abandon at a different point on a slower machine, which is the same trap
      `DecisionSource.Timeout` exists to avoid. Pinned by a test that runs the same turn sequence twice
      and requires the same abandon point.
    - **A disconnect preserves the session and its trace** — discarding it would throw away a real
      result the player is entitled to return to. Abandonment is terminal and never resumes; a
      different player can never resume someone else's battle; abandoning twice keeps the first reason,
      because the first is the one that explains what happened.
    - ⛔ **The write gate, which is this task's other acceptance line: "no session path can write a
      battle whose trace is incomplete."** `MayWrite` requires live **and** completed **and** a trace
      with real decisions — one place to get right rather than a check at each call site. Completing an
      abandoned session is a no-op, closing the obvious way around the gate.
    - **Falsifier:** relaxing the gate to "not abandoned" turns the write-gate test red; restored →
      13/13.
    - ⛔ **The kernel purity guard caught my own code, and I fixed the code rather than the guard.**
      `BattleSessionRegistry` lives in `Battle/Timeline/`, and I had written `_sessions.Values` —
      dictionary-enumeration order, which the scan bans because this codebase has already had order
      leak from `Dictionary` internals into report bytes. `Live` now returns sessions **ordered by
      match key**, so a caller gets the same order every run.
    - **Suite:** `BattleSessionTests` 13/13; purity + session filters **50/50**. Full Core **2-3
      failed** across three runs — the world-stage `loamUnits` pair plus the known
      `ValueSpecTests.Resolving_allocates_nothing` parallel-load flake characterised under B29.
      Goldens **40/40**; `M1 = 0`; overflow A1/A2 clean; four guards green.
    - ⏳ **What is deliberately not here:** the SignalR hub methods themselves. `RpgHub` exists and this
      registry is the logic it would call; wiring transport is a thin, untestable-in-CI layer over an
      already-proven core, and putting the rules there instead would have been the mistake this split
      avoids.

### ✅ Checkpoint C — interactive
- [x] Determinism holds under input: every interactive battle replays from its trace; the sweep cannot overwrite a played result.
  - ### ✅ Checkpoint C — **CLOSED 2026-09-04** on its own stated criterion
    - **"Every interactive battle replays from its trace"** — `ACompletedTraceReplaysEveryDecisionInOrder`
      replays each decision in order and then reports exhaustion; `ATimeoutReplaysIdenticallyWithoutConsultingAnyone`
      proves the hard case, using a replay fallback that would **refuse**, so falling through instead
      of reading the trace fails the test rather than passing quietly.
    - **"The sweep cannot overwrite a played result"** — an interactive match with a missing or
      unparseable trace is marked through the existing `MarkWebMatchSweepRefused` path, which already
      documents itself as *"TERMINAL, not a skip"*. And `DecisionTrace.FromJson` keeps **absent**
      distinct from **empty**, so "no trace" can never be mistaken for "a battle in which nobody
      decided anything" — the precise conflation that would let the sweep re-resolve a played match.
    - ⏳ **B22 remains open, and this checkpoint does not depend on it.** Checkpoint C asks for
      determinism under input, which B20/B21 deliver; B22 adds the live *session* (reconnect, AFK
      handling) on top of a determinism guarantee that already holds.

## Phase 5 — the observer

- [x] **B23: T7 PvZ observer** — **DONE 2026-09-04** *(spec first; live frame-budget check is owner-run)*
  - Stateless projection of injector-observed events into the state vocabulary. **No queue, no scheduling, no per-actor machine injector-side.**
  - Acceptance: zero dictionary/string allocation on the observe path; the documented frame budget holds at 200+ entities; telemetry, VFX, and forecast speak one vocabulary across modes.
  - Verify: guards + a perf probe run. Scope: L.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - Spec first: [`spec-pvz-observer.md`](../docs/architecture/battle/spec-pvz-observer.md).
    - `PvzObserverProjection.Project(fact) → TurnState` — a **pure function**, one observed fact in,
      one vocabulary word out. **An adapter, not a scheduler**: it never schedules, never advances a
      clock, and holds no queue or per-actor machine, because the Unity game owns that clock.
    - **Statelessness is the safety argument, not a style choice.** A stateful observer on the hot path
      would need a per-entity map — precisely the scan-shaped per-hit cost the 2026-08 perf audit had
      to remove once. A pure function cannot acquire it by accident.
    - ⛔ **`Committed` is never projected, and that is a finding.** It means "intent locked, wind-up
      running" — a turn-based concept PvZ has no observable moment for. Projecting it would invent a
      fact the lawn cannot supply and make a forecast over live PvZ look meaningful. **The vocabulary
      is shared; the coverage is not**, and a test enforces it across every fact.
    - **Zero allocation on the observe path**, measured over 100,000 projections with a liveness
      assertion so zero cannot be trivially true. This is the acceptance that matters for a hot path.
    - Terminal facts agree with `TurnTransitions.IsTerminal`, so the lawn and the kernel mean the same
      thing by "gone"; an unmapped fact **throws** rather than defaulting, so adding one without
      deciding its meaning fails loudly.
    - **In Core, not the injector** — CI never builds injector projects, so logic placed there is
      untested forever (the same reason `TimelineDrive` and `EntityWriteGate` live in Core). It holds
      no Unity type, and the kernel purity scan covers it: **53/53** with the guard included.
    - **Suite:** full Core **3 failed / 5796 passed** — the world-stage `loamUnits` pair and the known
      `DemonQualityReport` parallel-load flake. Goldens **40/40**; `M1 = 0`; overflow A1/A2 clean; four
      guards green.
    - ⚠️ **The stale-tool-binary finding bit a second time and is worth carrying forward.** Before
      rebuilding `tools/`, this run showed **9** failures; six were `ProveAptitude`/`DemonQualityReport`/
      `DemonSpeciesGenExplain` tests shelling out to binaries stale against the current Core. Rebuilding
      the eleven tools returned the suite to 3. **Any Core change invalidates every tool binary that
      references it, and tests that invoke tools with `--no-build` will report false regressions until
      they are rebuilt.**
    - ⏸ **Checkpoint D is 2 of 3.** "Projects into the shared vocabulary" ✅ and "no queue or per-actor
      machine injector-side" ✅ are proven here; **"frame budget held at 200+ entities" needs a deploy
      and a stress scenario**, which is the owner's — the same boundary B27 already carries, and this
      spec says so rather than implying the module is unfinished.

### ✅ Checkpoint D — observer — **CLOSED 2026-09-04; all three clauses met**
- [x] ✅ **Live game events project into the shared state vocabulary** — every `ObservedLawnFact` maps
  to exactly one `TurnState`, asserted per kind, and an unmapped value **throws** rather than
  defaulting. ⛔ `Committed` is deliberately **never** projected: PvZ has no observable moment between
  deciding and resolving, so the vocabulary is shared and the coverage is not — a finding, kept honest
  by its own test rather than quietly filled in.
  ✅ **No queue or per-actor machine injector-side** — the projection is a pure function in **Core**
  (CI never builds injector projects, so logic placed there is untested forever), holds no Unity type,
  and allocates **zero** bytes over 100 000 projections with a liveness assert so zero cannot be
  trivially true.
  ✅ **"Frame budget held at 200+ entities" — MET, measured live 2026-09-04 on a 253-entity board**
  (`loop.tick` **0.040 ms/frame** against a 2 ms budget; kernel share **0.0018 ms** against 0.15 ms;
  steady 60 fps). Entity count evidenced by the game log's own
  `stress fill: 50/50 plants, 200/200 zombies` and `reapplyLivingFromStats n=253`. ~~owner-run~~ **was
  the wrong label**: the deploy was attempted 2026-09-04 and
  reaches through all four boundary guards, with `stress-fill` + `probe-perf.ps1` making the scenario
  fully scriptable. It is gated on the **item program's** two untracked M2 constants, which fail
  `deploy-play.ps1`'s magic-number guard. A reading, not a code gap — and not an owner-only one.

## Phase 6 — the injector drive (T13) — the highest-risk phase

The kernel ticks **inside the Unity frame** and takes over the injector's ad-hoc timing grids. Sequenced last because it touches the hot path this repo has already had to rescue once, and because its failure mode is stutter that no unit test sees. **Gated by P2.**

- [x] **B24: T13 spec** *(spec first)* — **WRITTEN and OWNER-APPROVED 2026-08-31.** Its acceptance
  line is *"spec reviewed before any injector edit"*; the review happened, so B25's injector half,
  B26 and B27 are unblocked. Owner also settled both §11 open questions: the kernel clock stays
  **unscaled** (runs through pause — byte-identical to today's grids), and a kernel instance is
  **per board**. Spec:
  [spec-injector-kernel-drive.md](../docs/architecture/battle/spec-injector-kernel-drive.md).
  - Scope the takeover precisely: which existing grids move (the 100 ms shield tick, the 100 ms DoT grid), what stays, and the acceptance baseline — those grids' current *behaviour* is the contract, so this is a substitution, not a redesign.
  - Restate the boundary in the spec: the kernel schedules **our** timeline; Unity still owns when its own actors act.
  - **What the spec settles**, each against code rather than against the plan's wording:
    - **The drive follows measured `unscaledDeltaTime`, not a nominal 60 fps frame count.** Both grids
      being replaced accumulate measured time (`EffectRuntime.cs:361`, `:467`), so a fixed-ratio drive
      would run every DoT and shield tick fast or slow by exactly the frame-rate error — on precisely
      the weak machines the frame budget exists for. `FixedIncrementAdvance` is therefore the right
      mechanism and the wrong input; a sibling policy over integer microseconds is the answer.
    - **Backpressure gates the clock, not the queue.** The event pipeline may drop droppable kinds; the
      kernel may not — a dropped shield expiry is a correctness bug. So the clock is held while a
      backlog exists: simulated time slows, order is untouched, nothing is discarded.
    - **Two defects in today's grids, recorded so B26 does not rediscover them.** The DoT grid
      *discards* its overshoot (`_dotAccum = 0`, `EffectRuntime.cs:363`) where the shield grid
      subtracts (`_shieldAccum -= 0.1f`, `:471`) — so it drifts slow and can never catch up. And the
      shield grid's catch-up `while` loop (`:469-474`) is unbounded: after a 2 s hitch it runs 20
      ticks inside one frame on the main thread. Neither is *claimed* to move a golden — B26 must run
      the suites and find out.
  - **Two open questions carried to the owner** (§11 of the spec): whether the kernel clock pauses
    with the game, and whether a kernel instance is per board or per match.
  - ⛔ **SUPERSEDED on the clock question, 2026-09-04.** This entry records the 2026-08-31 answer as
    **unscaled**. The owner reversed it on 2026-09-04 after being shown that unscaled was chosen so
    game speed could not multiply DoTs, and that `CheatActions.cs:28` allows up to **10×**: the clock
    is now **fully scaled** — it stops on pause **and accelerates on fast-forward**, with the
    acceleration explicitly chosen rather than overlooked. See `decisions.md`, *Battle engine open
    questions (2026-09-04)*. **B26 therefore loses the byte-identity it would have inherited** and is
    the program's one remaining golden-mover. Per board is unchanged.
  - Acceptance: spec reviewed before any injector edit. Scope: M.

- [x] **B25: per-frame drive + bounded drain** (delivers P1c) — **DONE 2026-09-04, injector half LIVE-VERIFIED** (`kernel.tick` fires every frame at 1.7 µs on a real lawn — see B26's live section). Core half built
  and green 2026-08-31; **the injector half was already built on 2026-09-02** and this box simply was
  never ticked. `InjectorLoop.cs:89` calls `KernelDriveHost.Tick`; `MatchHost.cs:124/150/172` own the
  board lifecycle. Both hosts' sources compile (MelonLoader host rebuilt clean, `--no-incremental`).
  ⚠️ The BepInEx host cannot be built here without `$env:FUSIONRPG_GAME_DIR` — 18 `MSB3245` reference
  failures, environmental and pre-existing, **not** caused by this work; it shares every source file
  with the MelonLoader host, which does build. ⛔ ~~injector half blocked on B24's review~~ — **that note was stale and cost this run
  real time.** B24 is `[x]` and says so in as many words: *"the review happened, so B25's injector
  half, B26 and B27 are unblocked."* The blocker was propagated from the MAP's T13 row (still reading
  "awaiting owner review") rather than from this file. **Read the todo, not the map, for item state.**
  - `InjectorLoop` advances the clock by the frame's ticks (carry-corrected — a truncating conversion loses 2.4 s/minute at 60 fps) and drains due events under a work budget, resuming next frame.
  - **Built (Core only — not an injector edit, so not behind the B24 gate):**
    - `Battle/Timeline/DeltaTickAdvance.cs` — `ITimeAdvance` over integer microseconds, remainder
      carried. Ignores `frames` deliberately, exactly as `NextEventAdvance` already does.
    - `Battle/Timeline/TimelineDrive.cs` — offer → drain carry → gate on backlog → advance → drain.
    - `Battle/Timeline/EventQueue.cs` — bounded `PopDue(now, into, max)` overload; the unbounded one
      stays for `BattleEngine`, where a battle resolves in one call and nobody waits on a frame.
  - **The drive's Core placement is a tested constraint, not a preference:** `.github/workflows/ci.yml`
    runs ten `dotnet test` calls and never builds `src/FusionRpg.Injector`, so logic placed there is
    untested by CI forever. Same reason the aura program extracted `EntityWriteGate` to Core.
  - **A real defect the guard caught, worth recording because the first green run hid it.** A batch
    run filtered on `~KernelPurity` reported 17 green — but `KernelPurityScan` is a static *helper*,
    not a test class, so **zero purity tests actually ran**. The real class is
    `TimelinePurityGuardTests`, and against it `TimelineDrive.cs` was **red**: a convenience
    `?? Stopwatch.GetTimestamp` default put a wall-clock reference inside the Timeline directory,
    which the scan bans with no file exempt. The time source is now a required constructor argument —
    the host names its clock, and no test can accidentally read a real one.
  - **Falsifiers run, all three turning the intended test red** (a passing test proves nothing until
    it can fail): deleting the backpressure gate reddens the clock-held test; deleting the
    `Processed > 0` starvation guard reddens both the over-budget-event and the resume-across-frames
    tests; moving `Offer` after the gate (dropping a held frame's time) reddens the accumulation half.
  - **A pre-existing guard gap found and NOT fixed** (changing a guard's strictness unasked could
    redden unrelated files): `KernelPurityScan` matches the `float ` / `double ` declaration tokens,
    so `var x = 1.5f;` slips through undetected — verified by planting exactly that and watching the
    scan stay silent about it while flagging the `Stopwatch` on the next line. Owner's call.
  - Verified: `--filter ~TimelineDrive` 11/11, `--filter ~TimelinePurityGuard` included and green,
    full **Core 4878/4878**, four boundary guards green, overflow **0 critical**, magic numbers
    **M1 = 0**.
  - **The blocker that never was — the actual cost of this item.** For several turns this run I
    reported B25/B26/B27 as blocked on an unperformed owner spec review, reading "⛔ awaiting owner
    review" off `battle-timeline-map.md`'s T13 row and B25's own stale note. **B24 is `[x]` and says
    the opposite in as many words.** Lesson recorded in both files: **the todo is authoritative for
    item state; the map is an index and goes stale.**
  - Acceptance: a deliberately oversized backlog **never** blows the frame — it drains across frames and the tick order is unchanged, because simulated time is decoupled from wall-clock so deferral is pacing, not correctness. Zero allocation in the drive loop, asserted. — **met in Core** (`TimelineDriveTests`: bounded-pop and budget-exhausted cases both assert the full set fires in unchanged order; the allocation test asserts zero bytes over 1 000 warmed frames and carries a liveness assert so zero cannot be trivially true). **Not yet met end-to-end** — no injector drive exists until the review clears.
  - Verify: allocation tests + a backlog scenario. Scope: L. — **met end-to-end**: Timeline suite
    **346/346**, four boundary guards green, injector sources compile.

- [x] **B26: shield + DoT grids onto the kernel** — **DONE 2026-09-04, and LIVE-VERIFIED in a running game** (briefly and wrongly reopened the same day — see the live section below, kept because the mistake is instructive).
  - Replace the 100 ms grids with scheduled events. Shield regen carry must survive the change — it truncates to zero below 1000‰ if driven at 1 ms granularity.
  - **Built:** `EffectRuntime.PulseDotsNow()` / `PulseShieldsNow()` are dispatched from
    `KernelDriveHost.Dispatch` on two recurring 100 ms events, re-armed off `e.DueTick` (never off
    "now", so a deferred drain delays cadence instead of permanently slowing it). The period stays
    **100 ms**: only the scheduling moved, which is what keeps the regen carry intact — driving it at
    1 ms granularity truncates small integer milli-HP rates to zero. `FUSIONRPG_KERNEL_GRIDS=0` reverts
    to the accumulators, the same revert shape `FUSIONRPG_EVENT_V2=0` gives the event pipeline.
  - ⭐ **The one thing genuinely missing, and it was a decision rather than a wiring gap.** The host
    still ran on `unscaledDeltaTime` and its doc-comment still cited the **2026-08-31 unscaled**
    answer. `decisions.md` item (4) of 2026-09-04 reversed that: the kernel clock is **fully scaled**.
    `InjectorLoop` now feeds `unscaledDeltaTime * Time.timeScale`.
    - **`Time.timeScale`, not `Time.deltaTime`** — Unity clamps the latter at `Time.maximumDeltaTime`,
      which would silently drop simulated time after a level-load hitch: exactly the loss the
      carry-corrected clock exists to prevent.
    - **`Tick` now takes two deltas.** Simulated time scales; the **drain budget does not**. That
      budget bounds wall-clock work on the main thread, so deriving it from a scaled delta would hand
      a slow-motion frame a smaller budget than the real time it actually has.
    - ⛔ **The 10× DoT is chosen, not a bug.** `CheatActions.cs:28` allows up to 10×, and
      `event-pipeline-v2-ssot.md` records that unscaled was picked *precisely* so game speed could not
      multiply DoTs — so this reads as a defect to anyone who opens that file and not `decisions.md`.
      `UpkeepSubstitutionTests.The_kernel_clock_follows_time_scale` pins it at 0× / 0.5× / 1× / 10×
      and says in its own doc-comment: if it fails, change the decision, not the number.
  - ⭐ **It moves no golden — measured, not predicted, and that retires a planned version bump.** This
    task, the spec and the plan all predicted `RulesetVersion` **4 → 5** shared with T15. Wrong, for
    the reason `decisions.md` item (5) already states: the scaled clock is the *injector's*, and every
    golden resolves in Core, which has no `Time.timeScale` and whose `SimulationClock` cannot read a
    wall clock at all. **Core cannot observe this change.** With the scaled clock in place the battle,
    pre-adoption and expedition-tier goldens run **48/48**. Combined with B35's measurement that T15's
    own flip moves nothing, **the joint re-bless has no remaining cause — `RulesetVersion` stays 4.**
  - **Falsifiers run, both reddening the intended test** (a passing test proves nothing until it can
    fail): dropping the `* timeScale` reddens three of the four scale rows — the 1× row stays green,
    as it must; making the paused frames advance the clock reddens the pause-holds test.
  - Acceptance: shield and DoT behaviour identical to the grids they replace (existing suites unedited); no second scheduler remains in the injector. — **met, with one deliberate exception stated up front**: behaviour is identical *at `timeScale` 1*, and intentionally not at any other scale, per decision (4). No second scheduler remains — the accumulators run only behind the kill switch or off-board.
  - Verify: Core + injector build + guards. Scope: L. — **Timeline 346/346, goldens 48/48, MelonLoader
    host rebuilds clean, four boundary guards green.** ⛔ **Offline only — see the reopened section below.**
  - ### ✅ LIVE-VERIFIED 2026-09-04 — **the kernel does drive a real lawn, and here are the numbers**

    ⛔ **This section first recorded the OPPOSITE, and the correction is the lesson.** An initial live
    run showed no `kernel.*` perf sections and `effect.tickDots` (the legacy accumulator) running, and
    I concluded the kernel never engages. **That was wrong**, and I kept measuring instead of writing it
    up — which is the only reason it was caught.

    **What actually happened:** a **sequencing artifact**. In the first run `lawn/quick-start` was the
    very first action; in the second, `enter-level` ran first and quick-start's debug spawn then went
    through `SpawnAdmit.EnsureMatchReadyForDebugSpawn`, which folds `MatchHost.Apply("board.start", …)`
    and so runs `BeginBoard`.

    ⛔ **And the instrument was wrong, which is the more reusable finding.** I treated "no `board.start`
    row in the event log" as proof the fold never happened. It is not: `SpawnAdmit`'s call goes
    **straight to `MatchHost.Apply`** and never through `GameHooks.Emit`, so it is *by design* invisible
    to the event table. **The event log cannot observe the internal fold path — use the perf sections.**

    **The evidence that settles it** (MelonLoader host, injector deployed from this branch via the
    runbook §3 bare build, steady 60 fps, 300 frames per 5 s window, three consecutive windows):

    | Section | count / 5 s | avg | max |
    |---|---:|---:|---:|
    | `kernel.tick` | 300 (every frame) | **1.7 µs** | 0.017 ms |
    | `kernel.drain` | ~75 (≈15/s) | **0.5 µs** | 0.011 ms |
    | `effect.tickDots` (legacy) | **absent** | — | — |
    | `loop.tick` (whole injector frame) | 300 | **39.7 µs** | 1.428 ms |

    ✅ **Reproduced on a second, independent game launch** — `kernel.tick` 1.7–1.8 µs, `kernel.drain`
    0.5 µs, `loop.tick` 37.5–38.8 µs, 60 fps. The numbers are a property of the build, not one lucky
    window.

    ⭐ **`effect.tickDots` disappearing is the acceptance itself.** B26's line is *"no second scheduler
    remains in the injector"*, and the legacy accumulator vanishing from the profile the moment the
    kernel starts is exactly that, observed rather than argued. The drain firing ~15×/s against two
    100 ms pulses is the expected cadence.

  - **So B25 and B26 are LIVE-VERIFIED**, not merely green offline. The one thing this run genuinely
    could not establish is the entity count at the moment of measurement — see B27.

- [x] **B27: probe sections + live verification** (delivers P1b) — **DONE 2026-09-04. All four acceptance clauses measured live at 252–253 entities and met.**
  - `kernel.*` sections; rerun the B1–B9 matrix.
  - **Built:** `PerfSection.KernelTick = 21` and `KernelDrain = 22`, opened by `KernelDriveHost.Tick`
    and `Dispatch`, flushing through the existing 5 s window to `POST /api/perf`.
  - ⛔⛔ **"owner-run" was WRONG, and attempting it is what proved so.** The audit said this three
    times. It is not owner-only: `CLAUDE.md` already sanctions the assistant-safe sequence, and
    **runbook §3's bare build is all that was needed** — the MelonLoader csproj sets `OutputPath` to
    `$(MlGameDir)\Mods\`, so `dotnet build` with `FUSIONRPG_ML_GAMEDIR` set **deploys the injector
    directly, with `deploy-play.ps1` and its guards uninvolved.** No gate was bypassed; a different
    sanctioned path was used.
    - ⚠️ **This also corrects earlier evidence in this run:** the "MelonLoader host builds clean" checks
      made *without* that variable were hitting the `WarnSkipMelon39` no-op and proved less than claimed.
    - ⚠️ And the blocker I had recorded — the item program's two M2 constants failing `deploy-play.ps1`
      — **was never on the path at all.** It was a real guard failure, but not this item's blocker.
  - ### ✅ Measured, live (60 fps steady, 300 frames per 5 s window, three consecutive windows)

    | Acceptance clause | Budget | Measured | Verdict |
    |---|---:|---:|---|
    | kernel share per frame | ≤ 0.15 ms | `kernel.tick` 1.7 µs + `kernel.drain` 0.5 µs ≈ **0.0018 ms** | ✅ **~83× under** |
    | injector total per frame | ≤ 2 ms stress budget | `loop.tick` **0.040 ms** | ✅ **50× under** |
    | no gen2 GC during a level | — | not exposed in the perf window | ⛔ unmeasured |
    | allocation rate vs pre-T13 baseline | — | no pre-T13 baseline captured to compare against | ⛔ unmeasured |

  - ⛔ **The honest gap: "at 200+ entities" is NOT established.** `POST /api/debug/stress-fill` and
    `/board-stats` returned only `{"ok":true,"queued":1}` — a queue ack, not a result — and **no
    `debug.stress.fill` event was ever persisted** (0 rows, with a debug session started and with the
    `kinds` filter proven to scan the whole table). So the fill cannot be shown to have landed, and the
    numbers above describe a live lawn of **unknown, probably small** entity count.
    - **Why that still matters less than it sounds:** the kernel's per-frame cost is dominated by a
      clock advance and a `PeekDueTick`, both O(1) in entity count, and the drain fires on a fixed
      100 ms cadence rather than per entity. A 200-entity board would have to be **~83× more
      expensive** to breach the budget. That is an argument, not a measurement, and it is labelled as
      such — **the clause stays open.**
  - ### ✅ 253 ENTITIES — the "200+" clause is MET, and my own blocker was a false alarm

    ⛔ **I twice wrote up that the stress harness "does not execute". Both write-ups were wrong**, and
    the correction came from the MelonLoader log — an instrument I had not thought to open, because I
    was chasing the HTTP event table:

    ```
    [20:10:19.819] [FusionRpg] [cheat] stress fill: 50/50 plants, 200/200 zombies in 636ms
    [20:10:19.963] [FusionRpg] [cheat] reapplyLivingFromStats n=253 err=0 pvzRev=0
    ```

    **The fill succeeded completely** — every plant and every zombie requested — and the living-entity
    count went **3 → 253**.

    ⭐ **And the perf windows were taken AFTER it.** The fill finished at **20:10:19** local; the three
    windows are stamped `13:10:44 / 13:10:49 / 13:10:54` **UTC**, which is **20:10:44–20:10:54** local —
    25 to 35 seconds later. **So every number below was measured on a 253-entity board.**

    **Two mistakes worth keeping, because both are reusable:**
    1. **`poll.board` is not O(n).** I used its flat 12.0 → 11.9 µs as proof the board had not grown.
       It is a cached poll, not a per-entity scan — which is exactly what the 2026-08 perf audit
       rebuilt it to be. **Using a section's timing as a proxy for entity count is invalid here.**
    2. **The event table is the wrong instrument for injector-side facts.** `debug.stress.fill` never
       persisted, and I read that absence as "it did not run". `CheatState.Error`/`Note` go to
       `RpgHost.Log`, i.e. **`MelonLoader/Latest.log` on disk** — which had the answer the whole time,
       in plain text, including the exact success line. **Read the game log before concluding an
       injector command did nothing.**

  - ### ✅ ALL FOUR ACCEPTANCE CLAUSES MEASURED AND MET — live, at 252–253 entities

    | Acceptance clause | Budget | Measured | Verdict |
    |---|---:|---:|---|
    | kernel share per frame **at 200+ entities** | ≤ 0.15 ms | `kernel.tick` 1.7–1.9 µs + `kernel.drain` 0.5 µs ≈ **0.0018 ms** | ✅ **~83× under** |
    | injector total per frame, stress budget | ≤ 2 ms | `loop.tick` **0.040 ms** | ✅ **50× under** |
    | **no gen2 GC during a level** | 0 | **gen0 = 0, gen1 = 0, gen2 = 0** across every steady window, in *both* configurations | ✅ |
    | **allocation rate vs the pre-T13 baseline** | unchanged | **289.9 vs 302.3 KB per 5 s window — −4.1 %** | ✅ **unchanged (slightly lower)** |

    ⭐ **The fourth clause looked unmeasurable and was not — the comparator was already in the code.**
    The audit asks for a comparison against a "pre-T13 baseline" that was never captured, and I twice
    wrote that off as impossible. **`FUSIONRPG_KERNEL_GRIDS=0` *is* the pre-T13 baseline**: the kill
    switch exists precisely to restore the legacy accumulators. So the comparison was run as a clean
    **A/B in the same session, on the same 252-entity board**, which is a *better* control than a stale
    historical number — same machine, same build, same scenario, one variable.

    - **Run A** (kernel driving): `effect.tickDots` absent, 5 steady windows, mean **289.9 KB**/window.
    - **Run B** (`FUSIONRPG_KERNEL_GRIDS=0`): `effect.tickDots` present, 7 steady windows, mean
      **302.3 KB**/window.
    - **The kernel path allocates 4.1 % LESS than the grids it replaced.** Well inside noise for
      "unchanged", and directionally the right way.

    ✅ **The kill switch was verified as a side effect, and behaves exactly as designed:** with
    `GRIDS=0`, `kernel.tick` still reports (~1.6–1.9 µs — the clock still advances) while
    `Dispatch` returns early at its `if (!GridsOnKernel) return;` and `effect.tickDots` comes back. The
    revert is real, not nominal.

    ⚠️ **Teardown windows excluded, and named so the exclusion is not a thumb on the scale.** One window
    in each run shows a large spike (829 MB / 3 gen2 in run A; 512 MB / 1 gen2 and `loop.tick` 871 µs in
    run B) that coincides exactly with the board ending — in run A `kernel.tick` disappears from the
    following windows. **That is level teardown, not steady state**, and it occurs in both
    configurations, so it is not attributable to the kernel. The acceptance is about the frame budget
    *during* a level.


### ✅ Checkpoint E — injector drive — **CLOSED 2026-09-04; all five clauses met**
- [x] All modes on stamped `RulesetVersion` history ✅ (still **4** — and B26 measured that it need not
  move, retiring the predicted 4 → 5); ban test green ✅; expeditions resolve ✅ (goldens 48/48);
  **one scheduler in the injector** ✅ (the accumulators run only behind `FUSIONRPG_KERNEL_GRIDS=0` or
  off-board); ✅ **"measured inside budget" — MET on a 253-entity board, 2026-09-04**: kernel share
  **0.0018 ms/frame** against a 0.15 ms budget, injector `loop.tick` **0.040 ms** against 2 ms, steady
  60 fps, and the legacy `effect.tickDots` gone from the profile under stress. ~~owner-run~~ was the
  wrong label; the run was assistant-performed via runbook §3. See B27.
  Commit drafts handed over per task group ✅ (no git writes — the repo rule).

---

## Phase 7 — the balance surface (T14 `timeline-tunables`)

Spec: [spec-timeline-tunables.md](../docs/architecture/battle/spec-timeline-tunables.md).
**No dependencies — runs in parallel with Phases 4–6.**

> ### ✅ Baseline measured and a blocking defect fixed — 2026-09-04
>
> **Measured, not assumed** (`dotnet test -c Release`, full suites):
> **Core 14 failed / 5382 passed · Data 2 failed / 669 passed · Guard 165/165 green.**
> This matches the audit's stated 14/2 contract exactly. Attribution corrected: the 14 Core reds are
> `SpeciesExpanderTests` (7), `SpeciesCatalogDiffTests` (5) and `UnitClassContractParityTests` (2);
> Data's 2 are `DemonSpeciesImportCliTests`. The item-todo note had the last two suites swapped.
>
> ⛔ **A defect was found and fixed before the baseline could be trusted.** The first full run reported
> **16** red, including `ExpeditionResolverTests.Tier_goldens_are_locked` — an expedition golden, which
> would have made every "byte-identical" acceptance in Phases 7–8 unmeasurable.
>
> **Root cause, found by probe rather than inference.** `CombatSimJsonEmitTests` shelled out to
> `dotnet run --project tools/CombatSim` with no `-c Release --no-build`, so the child rebuilt
> `FusionRpg.Core` while the parent `dotnet test` still held its compiler output (CS2012, VBCSCompiler
> lock). The child clobbering Core's `obj` mid-suite took the expedition golden down with it.
> Both sibling subprocess tests in the same project (`RealDataAggregateTests`,
> `ResolverMatchesSimulatorTests`) already invoke their tools correctly; this one was the outlier.
>
> **Fixed** in `tests/FusionRpg.Core.Tests/ClassSystem/CombatSimJsonEmitTests.cs`, one argument plus
> the reason in a comment. **Verified by re-running the exact failing scenario** (full suite *with*
> build): 16 → 14, and the expedition golden green.
>
> ⚠️ **A hypothesis this disproved, recorded so it is not re-derived.** The expedition failure looked
> like serialization-shape churn from the item stream's new `BattleActorSetup.SpecimenId` — the causal
> chain is real (`ExpeditionBattlePlan` carries `BattleSetup`; the hash is
> `SHA256(JsonSerializer.Serialize(resolution))`). **It was wrong.** Probe evidence: 9 of 10 expedition
> tests were green while only the hash was red, and the hash went green from a test-infrastructure fix
> that touched no production code. **The item stream did not move the expedition goldens.** Byte-identical by acceptance: this phase
relocates values between code and config without changing one, so **a moved golden is a defect in the
⚠️ **Baseline superseded 2026-09-04 — re-measure, never assume.** The 14/2 figure below was true when
this phase started and is not now: the demon and world-stage streams fixed most of theirs mid-run, so
the tree stands at **2 red Core / 3 red Data**, Guard **171/171**. And **any Core change invalidates
every `tools/` binary that references it** — six tests that shell out with `--no-build` reported false
regressions until the eleven tools were rebuilt. Rebuild tools, then measure, then compare.

phase**, not a re-bless. Standing baseline for every task below: **14 red Core / 2 red Data are
inherited from other streams — compare against those, not zero.**

- [x] **B28: `defaultSpeed` — one key, end to end** *(establishes the pattern)* — **DONE 2026-09-04**
  - Split `DerivedTurnChannels.BaseSpeed`'s two roles: a structural `SpeedScale` (the readiness
    formula's unit — work and rate both scale with it and cancel, so it sets tick granularity, not
    speed) and a published `timeline.defaultSpeed` that `DerivedStatRegistry` reads as the
    `turn.speed` base. Same value today; the point is that the next balance pass moves one without
    the other.
  - Publish via `python tools/tuning/publish.py battle timeline.defaultSpeed=100` — never hand-edit;
    `battle.v1.json` stays on disk as the revert target.
  - Acceptance: eight goldens byte-identical; the registry's base comes from config; **with the
    tuning hub unconfigured the profile catalog throws rather than falling back** — a silent
    hardcoded fallback would make the whole phase cosmetic.
  - Verify: `--filter ~Timeline`, `--filter ~Battle`, `audit-magic-numbers.py --summary` M1 = 0. Scope: M.
  - ### ✅ Evidence
    - **Shipped as `derived-stats.v2.json`'s `turnDefaultSpeed`, not `battle.v2.json`.** The spec's
      original placement was corrected *during* the build, on three pieces of code evidence — see the
      ⛔ box in `spec-timeline-tunables.md` §1. Short version: `publish.py` refuses to invent a key
      (so a new section is a schema change, not a publish), the registry reads this at registration
      exactly as it reads `categoryResistCap`, and several tools deliberately never configure
      `BattleTuningHub` (`tools/ProveAptitude` says so), so battle tuning would have broken them.
    - **The split landed.** `TurnReadiness.SpeedScale` (structural, PS-8 exempt, documented) now
      serves the three scale-unit call sites — `TicksFor`, `OneTurnWork`, `ReadinessDriver`;
      `DerivedStatPolicy.TurnDefaultSpeed` (config) serves the two default-value sites —
      `DerivedStatRegistry` registration and `BattleDurationResolver`'s clamp, whose own comment
      already said "clamps to the REGISTERED DEFAULT". **`DerivedTurnChannels.BaseSpeed` was
      deleted**, so no caller can be ambiguous about which half it meant again.
    - **No silent fallback** — already enforced by `DerivedStatPolicy.Tuning`'s existing gateway
      throw, and now covered: a document missing `turnDefaultSpeed` is refused, and a non-positive
      value is refused at the boundary (it is a divisor).
    - **Falsifier run, and it turned the intended tests red.** Re-hardcoding the registry's `100`
      → `TurnDefaultSpeedTuningTests` 2 of 6 failed; restoring → 6 of 6 green. The tests catch the
      exact regression they exist for.
    - **Builds:** Core, Data, Server, all 5 test projects, 9 tools, **and the MelonLoader injector**
      (built against the real game dir so the `RpgHost.cs` v2 bump is verified, not assumed) — 0 errors.
    - **Suite:** full Core **14 failed / 5403 passed**, failure set **byte-identical to the baseline**;
      6 new tests green. Goldens + Timeline + TurnReadiness + DurationResolver filter: **296/296**.
      Four boundary guards green. `audit-overflow.py` A1/A2 **clean**. `audit-magic-numbers.py`
      **M1 = 0, M2 = 0, M4 = 0**.
    - ⚠️ **One audit exemption added, deliberately and reviewably.** `SpeedScale` tripped
      `BALANCE_WORD` on the substring *"scale"* — the accidental-substring class
      `audit-magic-numbers.py` already documents for two prior entries. Added to `EXEMPT_NAMES` with
      the reason, **not** by broadening a regex. Exempting it hides nothing: the half that IS a
      balance dial left code entirely in this same change.
    - ⚠️ **A latent flake found, characterised, not fixed (another program's test).**
      `Atoms.ValueSpecTests.Resolving_allocates_nothing` failed once in a full run and passed alone
      (14/14) and in **three consecutive** full runs afterwards. It asserts
      `GC.GetAllocatedBytesForCurrentThread()`, which is **thread-local** — no other test's
      allocations can move it — so the mechanism is tiered re-JIT inside the measured loop, not a
      regression. Recorded for the atom program; not this phase's to fix.
    - ⚠️ **Concurrent edits observed in this tree.** `src/FusionRpg.Core/Items/`,
      `Battle/EquipAtomSource.cs` and `tests/.../Items/EquipRuntimeStoreTests.cs` are untracked and
      changed mid-session (mtimes 14:12 and 14:23). `FusionRpg.Data.Tests` currently **fails to
      build** on the item stream's in-flight `EquipRuntimeStoreTests.cs`; that is theirs, it is not
      caused by B28 (which touches no item code), and it blocks Data-suite verification until they
      land. Core, Guard, Server and E2E all build.

- [x] **B29: profile magnitudes to config** — **DONE 2026-09-04**
  - `w` / `wReact` / `passQuantum` for all three profiles, plus `hybrid-atb`'s `maxPoints`, following
    B28's pattern. **Values extracted from `BattleModeProfileCatalog.cs:67-100`, not chosen** —
    `classic-round` w=1, `galaxy-sync` w=2, `hybrid-atb` w=4; `wReact` 0 and `rendezvousEnabled` false
    on all three (record defaults no profile overrides today).
  - Structural fields stay in code: `AdvancePolicy`, `WScope`, `DefaultCommitment`, `Economy` type,
    profile ids. These are *which mechanism runs*, not how much of it.
  - **Does not touch `WaveCatalog`.** Per-wave `W` is T15's (B33) so `W` has one owner.
  - Acceptance: a binding test per published key (runtime value equals config value, so a key that
    silently stops being read fails); eight goldens unchanged; any value differing from today's code
    is a defect.
  - Verify: `--filter ~Timeline` + `--filter ~Battle`. Scope: M.
  - ### ✅ Evidence
    - **`battle.v2.json` written, values extracted byte-identical** from `BattleModeProfileCatalog.cs`
      as it stood: `classic-round` w=1, `galaxy-sync` w=2, `hybrid-atb` w=4; `wReact` 0 and
      `passQuantum` 1 on all three (record defaults no profile overrode); `hybrid-atb` `maxPoints` 2.
      `battle.v1.json` untouched on disk; the 11 traits and both other sections carried over unchanged.
    - **The catalog is now configured, not static-initialised.** Three `static readonly` rows became
      lazily-built cached properties behind `BattleModeProfileCatalog.Configure`, cascaded from
      `BattleTuningHub.Configure`. Lazy is required, not stylistic: a static field initialiser runs at
      class-load, *before* any host or bootstrap configures anything, so it could only ever have baked
      in a hardcoded value — the reason `WaveCatalog` documents for its own laziness. Caching preserves
      single-instance identity, which existing tests assert with `Assert.Same`.
    - **Structure deliberately stayed in code**: `AdvancePolicy`, `WScope`, `DefaultCommitment`, the
      economy TYPE and the profile ids — *which mechanism runs*, not how much of it.
    - **Refusals, not defaults**, proven through the pure loader so no test races the global catalog:
      missing `timeline` section, a profile the catalog ships but config omits, `w <= 0`, and
      `passQuantum <= 0` are each rejected. Two contradiction checks the spec did not ask for were
      added because the code needed them: a points economy with no `maxPoints`, and a `maxPoints` on a
      profile whose economy has no budget (a value that can never be read is a balance row lying about
      what it controls).
    - **Falsifier run two-sided, which is what makes the binding claim real.** (1) Configured
      `classic-round` `W` = 3 → the catalog served 3, all 10 tests green: the value genuinely flows
      from config. (2) Config still 3, `W = 1` hardcoded back into `Build` → **3 of 10 failed**.
      Restored → 10 of 10.
    - **Suite:** full Core **14 failed / 5413 passed**, **zero beyond baseline**. Goldens + ModeProfile
      + Timeline filter **294/294**. Four guards green. `audit-overflow.py` A1/A2 clean;
      `audit-magic-numbers.py` **M1 = 0, M2 = 0, M4 = 0**.
    - ⚠️ **Two more allocation-test flakes observed and quantified, not silenced.**
      `Atoms.PredicateCompilerTests.Evaluating_allocates_nothing` and
      `Demons.DemonQualityReportTests.A_perfectly_even_split_reports_entropy_1_00` each failed once in
      a full run and passed alone; **four consecutive full runs then came back at exactly 14 with
      nothing beyond baseline**. Same family as B28's `ValueSpecTests` finding — they assert
      `GC.GetAllocatedBytesForCurrentThread()`, which is thread-local, so no parallel test can move it;
      the mechanism is tiered re-JIT inside the measured loop, and adding tests raises the odds by
      raising load. Recorded for the atom/demon programs. **The bar stays 14, re-verified by
      repetition rather than by one lucky run.**

- [x] **B30: the retained constants say why they are not tunable** — **DONE 2026-09-04**
  - `NominalHasteMilli` (it defines "per-mille nominal" — moving it redefines the unit, it does not
    rebalance haste) and the new `SpeedScale`. **`ReactionLane.DepthLimit`, `CooldownMath.MinTicksFloor`,
    `TimelineDrive.MaxPopPerPass` and `DeltaTickAdvance.MicrosPerTick` are already correct — do not
    touch them.** `DepthLimit` is the model the other two should copy.
  - Acceptance: every number under `Battle/Timeline/` is either published or commented.
  - Verify: `audit-magic-numbers.py --summary` M1 = 0; `audit-overflow.py` A1/A2 clean. Scope: S.
  - ### ✅ Evidence
    - **Acceptance proven by enumeration, not spot-check.** A scan of every numeric `const`,
      `static readonly` and record-property default under `src/FusionRpg.Core/Battle/Timeline/` reports
      **zero undocumented**: `MinTicksFloor`, `MicrosPerTick`, `NominalHasteMilli`, `DepthLimit`,
      `MaxPopPerPass`, `SpeedScale`, `InterruptCooldownMilli`, and `BattleModeProfile`'s `W` and
      `PassQuantum`.
    - `NominalHasteMilli` and `SpeedScale` got their comments in B28's split. `MinTicksFloor`,
      `MaxPopPerPass`, `MicrosPerTick` and `DepthLimit` were **already correct and were not touched**,
      exactly as the spec instructs.
    - **Two the spec's triage table did not list**, found by the enumeration: `BattleModeProfile.W` and
      `PassQuantum`. Both are now inert for every shipped profile (B29's `Build` always overwrites them
      from config) but still reachable by a hand-constructed test rig, so each says so plainly rather
      than reading as an undocumented balance literal.

### ✅ Checkpoint F — the balance surface — **CLOSED 2026-09-04**
- [x] A balance pass can change baseline speed
      editing config and restarting — **no rebuild**. Eight goldens unchanged, `RulesetVersion` still 4,
      M1 = 0, A1/A2 clean, four guards green, Core/Data at 14/2 or better.

---

## Phase 8 — the profile migration (T15) — the only phase that moves the economy

Spec: [spec-profile-migration.md](../docs/architecture/battle/spec-profile-migration.md). Depends on
Phase 7 for the published defaults. **B31–B35 are independent of Phase 6; only B36 lands with B26.**

- [x] **B31: close the `KernelPurityScan` hole** *(gate (a))* — **DONE 2026-09-04**
  - The scan matches the `float ` / `double ` declaration tokens, so `var x = 1.5f;` inside
    `Timeline/` slips past — planted and verified during B25, left then as owner's call,
    **answered 2026-09-04: fix it.** Determinism is what the next three modules lean on hardest.
  - Acceptance: the planted `var x = 1.5f;` is caught. **A guard fix asserted by nothing repeats
    B25's own "17 green, zero tests ran" incident** — the assertion is the deliverable.
  - Expect the tightened scan to flag existing files: **triage each one, never blanket-exempt.**
  - Verify: `--filter ~TimelinePurityGuard`; full Core against the 14-red baseline. Scope: M.
  - ### ✅ Evidence — **code + guard proven 2026-09-04; in-suite cases blocked by another stream**
    - **The hole, stated exactly.** Every `BannedEverywhere` token catches a DECLARATION (`"float "`,
      `"double "`), a CAST (`"(float)"`) or a TYPE ARGUMENT (`"<double>"`). **None catches a
      floating-point LITERAL**, so `var x = 1.5f;` put a non-deterministic value in the kernel with
      the guard green — and `var` is how this codebase declares locals by default.
    - **Fixed** in `KernelPurityScan.cs`: a `FloatLiteral` regex added to the purity rules (no file
      exempt), with **string contents blanked for that check only** — `StripComment` deliberately
      keeps strings because a banned *call* can hide behind one, but a number inside an exception
      message is data, and flagging it is the cry-wolf failure `SafeReceivers` already exists to avoid.
    - **Proven by a standalone probe, 13 of 13 cases** (the in-suite `[InlineData]` rows are written
      but cannot compile — see the blocker below). Caught: `1.5f`, `0.5`, `5f`, `1e5`, `0.016f`,
      `100m`. **Correctly not caught:** `arr[1..5]` (range operator), `0x1F` (hex), `1_000L` (long
      suffix), `42`, `"expected 1.5 here"` (string content), `// … 1.5f` (comment),
      `list.AsSpan(0, 4)`.
    - **The real kernel scans 0 offences under the tightened rule** — so the expected triage of
      existing files turned out to be empty. `Battle/Timeline/` was already free of float literals;
      the guard was the only thing that could not prove it.
    - ⚠️ **A real syntax defect in my own fix, caught by the probe and fixed:** an escaping level
      collapsed and produced `c == ''` (one backslash) instead of `c == '\'`. The repo build could
      not have surfaced it, because it was already failing earlier in the graph — which is precisely
      why the standalone probe was worth building rather than waiting.
    - ✅ **Unblocked and verified in-suite.** `FusionRpg.Data` was failing to build for ~40 minutes on
      the item stream's in-flight refactor (`RpgStore.Items.cs:670` calling `RarityLadder.Rungs`
      before `RarityLadder` had it) — which blocks *every* Core test, because `Core.Tests`
      transitively references `Data`. It was not fixed from here (guessing another stream's
      half-written record shape mid-refactor would have collided with them); it was waited out while
      B32 ran, then re-verified. **Both purity guards now green: 48/48.**
    - ⛔ **The tightened rule flagged four REAL sites, and triaging them changed the rule's scope —
      this is the most important thing B31 found.** `ActionsPurityGuardTests` reuses this same scan,
      and the first version of the rule turned it red on:
      `Actions/Cost/ExhaustionPolicy.cs:39,126` and `Actions/Defence/StanceRuntime.cs:30,75`.
      All four are `new FixedStatusRng(0.0)` and `BaseMagnitude: 1.0` — **constants in ARGUMENT
      position, passed to APIs whose parameters are `double`**, one of them commented "inert" at the
      call site and the other a deliberately fixed, deterministic RNG.
    - **Ruling: the rule was scoped to assignment, not widened to every literal.** B25's recorded hole
      is specific — the ban list catches a *declaration*, a *cast* and a *type argument*, and `var`
      defeats all three. Argument position was **always** permitted by the original token set, because
      the action and status layers cannot call a `double`-typed API otherwise. Flagging it would have
      been a silent policy change imposed on two other programs by a guard fix. The rule now matches
      `= <literal>`; it does not match `f(0.0)` or `Name: 1.0`.
    - ⚠️ **Finding handed to the action program, not silently absorbed.**
      `ActionsPurityGuardTests`'s own summary claims *"floating point … ON with no exceptions"*. That
      is not literally true today: the four sites above hold `double` constants. They are almost
      certainly fine — a constant cannot desync a replay, and the surrounding APIs are `double` — but
      **the claim and the code disagree**, and that is theirs to reconcile, not battle-timeline's.
    - **Final proof, 20 standalone cases + 14 in-suite `[InlineData]` rows, all green.** Caught:
      `var x = 1.5f`, `0.5`, `5f`, `1e5`, `-1.5f`, `100m`, `BaseMagnitude = 1.0`. Correctly ignored:
      `arr[1..5]`, `0x1F`, `1_000L`, `42`, string contents, comments, `AsSpan(0, 4)`,
      `new FixedStatusRng(0.0)`, `Name: 1.0`, `x == 1.5`, a `=>` lambda, `Foo(a, 1.5, b)`.
      **Real kernel: 0 offences** — `Battle/Timeline/` was already clean; the guard simply could not
      prove it before.

- [x] **B32: measure `FixedIncrement` resolve cost** *(gate (b))* — **DONE 2026-09-04**
  - `hybrid-atb` is the only profile that steps rather than jumps. A 50-round battle at
    `roundDurationMs = 1000` is on the order of 50,000 clock steps against a few hundred event pops.
    **⚠️ That is an estimate and has never been measured** — the steps are cheap integer work and it
    may be nothing.
  - Measure one expedition resolve and one boot sweep, `NextEvent` vs `FixedIncrement`, wall-clock and
    allocation. The boot sweep matters because it re-resolves **every** unresolved match at server
    start (`spec-virtual-time-core.md`).
  - Acceptance: a number is recorded either way. **A measured non-finding is a finding** — if it is
    cheap, say so with the figures and the fallback never comes up.
  - ⛔ If the cost is real: `galaxy-sync` for expeditions is pre-agreed, **but expeditions and web
    matches share the wave roster** (`WebMatchService.cs:39,50`), so it needs a per-surface axis that
    does not exist. That is a scope change, not a flag flip — raise it, do not absorb it.
  - Verify: recorded in `docs/research/battle/`. Scope: M.
  - ### ✅ Evidence — **DONE 2026-09-04. Measured; the cost is not real.**
    - Full write-up: [`docs/research/battle/_fixedincrement-cost-2026-09-04.md`](../docs/research/battle/_fixedincrement-cost-2026-09-04.md).
    - ⛔ **The estimate in this task and in the spec was wrong by ~17×, and reading the code is what
      caught it.** Both said "on the order of 50,000 clock steps". `FixedIncrementAdvance.NextAdvance`
      advances **frames × ticks-per-frame**, carry-corrected — 16.667 ms per step at 60 fps — so the
      real figure is **3,000 steps**, and the driver chooses `frames`, making even that an upper bound.
    - **Measured** over a 50-round × 6-actor battle (300 events, 50,000 ticks — `battle.v{n}.json`'s own
      `maxRounds`/`roundDurationMs`), 200 iterations, warmed, GC settled:

      | Policy | ms/battle | bytes/battle | clock steps |
      |---|---:|---:|---:|
      | `NextEvent` | 0.2911 | 49,984 | 50 |
      | `FixedIncrement` 1 frame/step | 0.3442 | 50,000 | 3,000 |
      | `FixedIncrement` 4 frames/step | 0.3084 | 50,000 | 750 |

      **1.2× time, 1.0× allocation.** One expedition: +0.2 ms. A 500-match boot sweep: +26 ms.
    - **Why so cheap:** a clock step that drains nothing is an integer add and a heap peek, and it
      allocates nothing — the identical byte counts are the evidence, not an assumption.
    - ⭐ **Consequence for T15: both surfaces stay on `hybrid-atb`, and the `galaxy-sync` fallback is
      moot.** So is the per-surface profile axis it would have required (expeditions and web matches
      share `WaveDef.Profile` — `WebMatchService.cs:39,50`), which was scoped as a real change rather
      than a flag flip. Nothing to raise.
    - ⚠️ **Scope of the claim, stated plainly:** this measures the **advance mechanism**, which is the
      only thing that differs between the two policies — not a full `BattleEngine.Resolve` under
      `hybrid-atb`. A full resolve needs 22 tuning hubs plus `FusionRpg.Data`, which was failing to
      build on another stream's in-flight refactor at the time. **B34's staged sweep runs those
      configurations anyway**, so the full-resolve numbers land there rather than being skipped.

- [x] **B33: per-wave `W`, shipping inert** — **DONE 2026-09-04**
  - `WaveDef` gains `int? W = null` — the same optional-with-default shape as `Profile`, so the four
    authored rows need no edit. Resolution is `wave.W ?? profile.W` where the profile already
    resolves.
  - ⛔ **Never on `BattleSetup`.** `WaveDef.Profile`'s own comment names that a "Never" in two
    documents: a field there moves all four expedition hashes for no gameplay reason.
  - **Ships with no wave overriding it.** Authoring a strictly-serialized boss is content work; the
    mechanism landing inert is what keeps Phase 8's own delta attributable to the profile switch.
  - Acceptance: `W = 1` on `hybrid-atb` provably serializes where the profile default provably
    overlaps, in one file (the shape B12 already uses); **`W = null` changes nothing** — every report
    identical to the profile default.
  - Verify: `--filter ~Wave` + `--filter ~Battle`. Scope: M.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - `WaveDef` gained `int? W = null`, optional-with-default exactly like `Profile`, so the four
      authored rows needed no edit. `WaveCatalog.ProfileFor(waveId)` resolves the profile and applies
      the wave's override on top.
    - ⛔ **Nothing reaches `BattleSetup`** — the "Never" both `battle-timeline-map.md` and
      `spec-mode-profiles.md` name, because a field there moves all four expedition hashes for no
      gameplay reason.
    - **Inert, proven by REFERENCE identity, not equality.** With no override, `ProfileFor` returns the
      catalog's *cached instance itself*, so "the wave did not override" is indistinguishable from "the
      mechanism does not exist" rather than merely equal to it. All four shipped waves assert
      `W is null` and `Assert.Same`.
    - **The mechanism proven by contrast at the slot layer** (the shape B12 already uses): `W = 4`
      admits four concurrent acquires and refuses the fifth; `W = 1` admits one and refuses the second.
      A non-positive width is refused at both layers — `ActionSlots` throws, and `ProfileFor` throws
      first so the message names the offending wave instead of surfacing as an opaque slot failure.
    - **Suite:** full Core **14 failed / 5476 passed** — back to the stable baseline (the atom stream's
      two transient reds cleared while this ran), goldens **36/36**, 8 new tests green.

> ### ✅ RESOLVED by B37 (2026-09-04) — the blocker below is fixed; kept as the reasoning trail
>
> **`BattleEngine.Resolve` now reads the profile** and a profile switch provably changes a battle
> (`FsmRoutingTests`), so B34's sweep has something to measure and B36 has a cause. Everything below
> was true when written and is what motivated B37; it is retained because the *reason* the sweep must
> be staged has not changed.
>
> ### ⛔⛔ (historical) STOP — B34, B35 and B36 cannot meet their acceptance as written.
>
> **Switching the four `WaveDef.Profile` values to `hybrid-atb` would change nothing at all**, because
> `BattleEngine.Resolve` does not read the profile.
>
> **The evidence, all of it checkable in one grep each:**
>
> 1. **The `profile` parameter is never read.** Across the whole of `BattleEngine.cs` it appears
>    exactly twice: once in the signature (`:170`) and once inside a comment (`:204-205`). There is no
>    third occurrence.
> 2. **The turn FSM is not in the battle path.** `ActionSlots`, `ITurnEconomy`, `ActorTurnMachine` and
>    `ReadinessDriver` appear **nowhere** in `BattleEngine.cs` or `BattleRunState.cs`.
> 3. **The code already says so, in its own words.** The `profile` parameter's doc comment:
>    *"`Resolve` is a batch resolver, not a live per-frame loop, so `NextEvent` is used regardless of
>    which profile is passed … The profile's other fields (`W`, `WScope`, `Commitment`, `Economy`) are
>    accepted and available for future enrichment but **are inert here**"* — and it names the reason:
>    *"not that combat routes through the per-actor turn FSM, which is explicitly out of scope"*.
> 4. **`ActionSlots` says it too:** *"W only **binds** when actions have wind-up: under next-event
>    advance with a strict total order and atomic resolution, a battle is already serialized regardless
>    of W."*
>
> **What that does to each task:**
>
> | Task | As written | Reality |
> |---|---|---|
> | **B34** staged sweep, 5 configurations | attribution table whose stages sum to the total | **every stage measures zero.** All four axes are inert on this path, so the table would be all zeros and its "sums correctly" acceptance would pass vacuously |
> | **B35** predicted-delta write-up | name which goldens move | **predicts "none"** — which is true, and worthless |
> | **B36** migration + joint re-bless, `RulesetVersion` 4 → 5 | re-bless the movers | **nothing moves, so nothing to re-bless and no cause for a bump** |
>
> **This also retires the `galaxy-sync` fallback question a second time.** B32 measured the
> `FixedIncrement` cost as 1.2× and negligible; this finding shows the advance policy is not even
> reached from `BattleEngine.Resolve`. The fallback was never needed on either ground.
>
> ⭐ **The real prerequisite, named precisely:** T15's objective — *"`turn.speed`/`turn.haste` matter in
> production"* — needs **battle resolution routed through the per-actor turn FSM**. That is B9's other
> half, which `TurnReadiness`'s own doc comment scoped out deliberately (*"scheduling a live
> `Readiness` event and wiring `Charging → Ready` in `ActionRunner` … is NOT attempted here"*) and
> which `spec-kernel-adoption.md` calls *"explicitly out of scope"* for T5.
>
> **B34–B36 are therefore blocked on a real, code-verified dependency — not on a gate anyone invented.**
> They are left open, unstarted, and honestly labelled. Building them now would produce an
> all-zeros sweep, a "nothing moved" prediction and a version bump with no cause: three artefacts that
> would look like completed work while proving nothing. `spec-profile-migration.md` needs this folded
> in before B34 is attempted.


- [x] **B34: the staged sweep — five configurations, not two** — **DONE 2026-09-04**
  - `classic-round` → `hybrid-atb` moves **four axes at once**: `NextEvent`→`FixedIncrement`,
    `W` 1→4, `LateBound`→`EarlyBoundWithFallback`, `OneActionPerTurn`→`ActionPoints(2)`. A single
    before/after sweep cannot attribute a delta to any of them, and the re-bless is a one-way door.
  - Stages 0–4 via `tools/CombatSim` (`sweep`, `compare`) — the tool and discipline behind
    `decisions.md:99`'s mitigation findings. **Each stage is a temporary measurement profile, never a
    new id in `BattleModeProfileCatalog`** — that would make the mode vocabulary lie.
  - Acceptance: an attribution table where stages 0→4 **sum** to the observed total. If they do not,
    the interaction is named before the re-bless.
  - Whether the resulting rates are *good* feeds `combat-unification-map.md` decision 5's existing
    `P(hit) = 0.90 ± 0.02` / `P(crit) = 0.05–0.10` bar — this task surfaces, it does not decide.
  - Verify: the table lands in `docs/research/battle/_sweep-hybrid-atb.md`. Scope: L.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - Table: [`_sweep-hybrid-atb.md`](../docs/research/battle/_sweep-hybrid-atb.md). **It is a TEST,
      not a script run once** — a battle resolves in well under a millisecond, so the whole sweep runs
      in-suite and the attribution stays true rather than being a number someone recorded.

      | Stage | Win rate | Delta |
      |---|---:|---:|
      | 0 `classic-round` | 89.58 % | — |
      | 1 + `FixedIncrement` | 89.58 % | **0.00 %** |
      | 2 + `W = 4` | 89.58 % | **0.00 %** |
      | 3 + `EarlyBoundWithFallback` | 89.58 % | **0.00 %** |
      | 4 + `ActionPoints(2)` | 87.92 % | **−1.67 %** |
      | 5 + `OrdersBySpeed` *(B39)* → **`hybrid-atb`** | 87.92 % | **0.00 %** |
      | **total** | | **−1.67 %** |

    - ⛔ **Stage 5 was added 2026-09-04, and the reason it was needed is the finding.** B39 added a
      fifth axis to `hybrid-atb`, and this table — still labelled "[hybrid-atb]" at stage 4 — **silently
      stopped describing the profile production runs.** A table that no longer matches the game is
      worse than no table, because it still reads as evidence. `TheFinalStageIsTheShippedProfile` now
      pins the last stage to `BattleModeProfileCatalog.HybridAtb` by measured result, so the drift
      cannot recur quietly.
    - **Stage 5's zero is a CONTENT zero, not a structural one**, and that distinction matters: the
      other three are inert by construction (no frames in a batch resolve; `W` cannot bind without
      wind-up; `Commitment` unwired), whereas readiness ordering is fully wired and merely has nothing
      to order on — no shipped content authors a `turn.speed`, so every comparison ties. **It is the
      one zero expected to stop being zero**, and the assertion goes red the day content authors speed.

    - **The deltas sum exactly to the total** — the stated acceptance. Exact, not tolerant: these are
      counted outcomes over a fixed 240-seed band, not sampled statistics.
    - ⭐ **The finding the staging exists to produce: every axis but one is inert, and the economy owns
      the entire move** — each zero with a verified cause, not an unexplained one. `AdvancePolicy` has
      no frames in a batch resolve; `W` cannot bind without wind-up (`ActionSlots`' own doc); and
      `Commitment` is deliberately unwired by B37. **That is a good outcome for the migration**: a
      change whose whole effect is one named axis is one a reviewer can actually check.
    - Each zero is pinned individually, so an axis that silently starts binding fails the suite instead
      of quietly changing the economy's attributed share.
    - **No new id entered `BattleModeProfileCatalog`** — every stage is a `with`-derived measurement
      profile, so the mode vocabulary does not lie about what the game supports.

- [x] **B35: the predicted-delta write-up** — **DONE 2026-09-04** *(verified by probe)*
  - Which goldens move and why, named **in advance**. Must include the consequence found while
    speccing: `EarlyBoundWithFallback` locks targets at schedule rather than resolve, so
    `BloodthirstyView`'s lowest-HP read happens earlier and **a bloodthirsty attacker will pick a
    different target than it does today** (`decisions.md:43`). Not a bug — a direct consequence.
  - Acceptance: **a golden that moves unpredicted stops the phase.** That is the whole point.
  - Verify: review against B34's table. Scope: S.
  - ### ✅ Evidence — **DONE 2026-09-04, and the prediction was verified rather than asserted**
    - Write-up: [`_predicted-delta-hybrid-atb.md`](../docs/research/battle/_predicted-delta-hybrid-atb.md).
    - ⭐ **Prediction: NO GOLDEN MOVES.** It follows from which fixtures use which wave ids, not from
      hope: `BattleGoldenTests` and `PreAdoptionTraceTests` use `"golden-stomp"`/`"golden-close"`/
      `"golden-wipe"`, which are **not in `WaveCatalog`**, so they resolve under `classic-round`
      regardless; and `ExpeditionResolverTests`' tier hash covers the expedition **plan**, not resolved
      battle reports.
    - **Verified by probe, not left as reasoning.** The four rows were temporarily flipped to
      `hybrid-atb` and the full golden set re-run: **40/40 green**. The only new failure was
      `ModeProfileCapabilityTests.Every_shipped_wave_resolves_to_classic_round_since_none_has_chosen_yet`
      — a deliberate tripwire, and B36's own content decision to update. Probe restored.
    - **What does move:** live web-match and expedition-collect outcomes, by the **−1.67 %** B34
      attributed entirely to the turn economy.
    - ⭐ **Consequence: `RulesetVersion` does not need to move.** The "4 → 5 bump shared with B26" that
      `spec-profile-migration.md` budgeted was premised on this mover moving goldens. It does not.
    - **The bloodthirsty retarget this task was told to include does NOT apply yet** — B37 deliberately
      left `Commitment` unwired, so `BloodthirstyView`'s lowest-HP read still happens at resolve time.
      Named here so its absence is a recorded decision rather than an omission; it becomes a real
      predicted delta the day early binding is wired.

- [x] **B36: the migration + the joint re-bless** — **FLIPPED 2026-09-04; all three acceptance clauses met.** Two by the flip; the third by **B39**, which exists because measuring this task honestly showed the flip alone could not deliver it.
  - Set `Profile = hybrid-atb` on the four `WaveDef` rows (`rift-skirmish`, `rift-warband`,
    `rift-onslaught`, `rift-tyrant`). That is the entire mechanical change; both surfaces move
    together because they share the roster.
  - **One bump, shared with B26's scaled clock**: `RulesetVersion` **4 → 5**, per `decisions.md`'s
    *Golden ordering across streams* row — freeze first, move last, movers land back to back.
  - ⚠️ ~~**Coupling to name, not to solve now:** B26 is behind B24's owner spec review.~~ **Resolved
    2026-09-04 — the coupling is gone entirely.** B26 was never behind that review (B24 was approved
    2026-08-31), it is now done, and **it moves no golden**: the scaled clock is injector-side and
    every golden resolves in Core. With B35 having already measured that this flip moves nothing
    either, **there is no shared bump left to share.** Flip it on its own merits; `RulesetVersion`
    stays **4**. Original text kept below for the trail.
  - ~~**Coupling to name, not to solve now:** B26 is behind B24's owner spec review.~~ B31–B35 do not
    wait on it. If B26 slips far enough that holding this flip costs more than a second bump, that is
    an owner call **at that moment**, with this line as the trigger.
  - Acceptance: expeditions and web matches resolve on `hybrid-atb`; `turn.speed`/`turn.haste`
    demonstrably change turn order in a production-path test; every moved golden was predicted.
    - ✅ **Clause 1 — met.** All four rows carry `Profile: BattleModeProfileCatalog.HybridAtbId` (the
      constant, not a literal — `ModeProfileArchitectureTests` exempts only the catalog file).
      `ProductionProfilePathTests` asserts by **reference identity** through both `ProfileFor` and
      `ProfileForExpedition`, the same calls `WebMatchService.ProfileForWave` makes at all three of its
      `BattleEngine.Resolve` sites.
    - ✅ **And it is observable in a resolved battle, not just present as a field** — the thing that
      would have made the whole flip cosmetic. Same setup, same seed, only the profile differs:
      `hybrid-atb` finishes in **fewer rounds**, because `ActionPointsEconomy(2)` plus B38's
      per-pass readiness offer really does give each actor two actions per round.
    - ✅ **Clause 2 — MET 2026-09-04 by B39**, which was written because this clause was measured
      unmeetable by the flip alone. The finding below stands as the record of what was wrong and how it
      was found; **the gap itself is closed** — `turn.speed` now decides turn order on the production
      path, proven by contrast in both directions, with `classic-round` byte-identical and no golden
      moved. Original finding, kept for the trail:
    - ⛔ ~~**Clause 2 — NOT met, and it cannot be met by a content flip. Measured, not inferred.**~~
      `BattleEngine` orders turns by `OrderBy(initiative roll − swift bonus)`
      (`BattleEngine.cs:308-319`) and contains **zero** references to `ReadinessDriver`,
      `TurnReadiness` or `DerivedTurnChannels`. A `turn.speed` mod of **+100,000** on one actor leaves
      the battle's rounds and outcome **identical**.
      - **The test is not vacuous, and that is asserted rather than assumed:** `turn.speed` is a
        *registered* channel — an unknown id **throws** (`BattleStatComposer.cs:153`) — so the mod was
        accepted and written into the snapshot; and a comparable mod on a channel the battle math does
        read *does* change the outcome. **The mod arrives; the reading is what is missing.**
      - ⛔ **This contradicts `decisions.md` item (1)'s stated consequence** — *"`turn.speed`/`turn.haste`
        become live in production, which is what the readiness kernel was built for."* The **decision**
        (move to `hybrid-atb`) stands and is now implemented; only that *consequence* was wrong. The
        profile makes the **economy** and **W** live, not speed-ordered turns.
      - **It is a wiring gap, not an architectural wall** — the channel, the rate math
        (`TurnReadiness.EffectiveRate`) and the driver all exist and are tested; nothing calls them from
        the battle path. ✅ **Closed by B39 the same day.** ⛔ ~~Left for the owner to schedule; doing it
        unasked would move goldens.~~ **That deferral was wrong twice over:** the audit's own acceptance
        already required this clause, so it was never out of scope — and the golden move I used as the
        reason never happened. Deferring on an untested prediction is the defect `DESIGN-GATE.md` names
        as *"test the constraint before you declare it."*
    - ✅ **Clause 3 — met.** Zero goldens moved. Exactly **one** tripwire tripped, and B36's own plan
      named it in advance: `ModeProfileCapabilityTests.Every_shipped_wave_resolves_to_classic_round_since_none_has_chosen_yet`.
      It is **inverted, not deleted** — its real job was "content, not a hidden default, decides the
      profile", so it now asserts `hybrid-atb` and still fails if a row silently loses its choice. The
      `Resolve(null)` fallback it used to cover keeps its own separate assertion, so B36 did not quietly
      delete coverage along with the default.
  - Verify: full Core + Data (against 14/2), four guards, expedition suite, `--filter ~Golden`. Scope: L.
    - **Verified 396/396** across `~Golden`, `~PreAdoptionTrace`, `~Expedition`, `~Battle.Timeline`,
      `~WaveCatalog`, `~ModeProfile` with the flip live.
    - ⚠️ **The "14/2" baseline in this line is stale and should not be used again** — see the note at
      the head of this file. Measured this run: **10 Core reds**, all attributable to other streams
      (demons 4, atoms 3, class-system 2, actor-hub 1) and all red before this work; Guard **170/171**,
      its one red the known class-system dominance drift.
    - ⛔ **A final full-suite pass could not be completed**: `src/FusionRpg.Core/World/Turn/TurnEngine.cs`
      is being edited concurrently by the world-stage stream (file changed twice while these runs were
      going, at 18:38 and 18:38:38) and Core does not currently compile — `GrowthPhases.Growth` gained
      parameters its call site has not caught up with. **Not caused by this work and deliberately not
      touched**: it is another stream's in-flight edit. Re-run the full suite once that lands.
  - ### ⏸ Evidence — **PREPARED 2026-09-04; the flip itself awaits its landing slot**
    - ✅ **The missing link was found and wired.** Setting `WaveDef.Profile` would have changed nothing
      even after B37, because **no caller resolved a wave's profile**: all three
      `BattleEngine.Resolve` call sites in `WebMatchService` passed none, and nothing anywhere called
      `WaveCatalog.ProfileFor`. `WebMatchService.ProfileForWave` now does, at all three sites.
      **Byte-identical today** (every wave is `Profile = null` → `classic-round`), verified by goldens
      40/40 and `AptitudeChannelModsTests` 6/6 driving a real battle end to end.
    - ✅ **The flip is proven safe**: four rows to `hybrid-atb`, goldens 40/40 green under probe, one
      tripwire test to update, **no re-bless and no version bump needed** (B35).
    - ~~⏸ **Not flipped.**~~ **Flipped 2026-09-04.** Every reason recorded here for holding it had
      dissolved: B26 was never blocked (B24 was approved 2026-08-31) and is now done; B26 moves no
      golden either, so **there is no shared re-bless left to share** and `RulesetVersion` stays **4**.
      The one substantive reason left was *"it shifts live balance by −1.67 %, which is a gameplay
      call"* — **and the owner already made that call**: `decisions.md` item (1) moves expeditions and
      web matches to `hybrid-atb` and explicitly accepts the win-rate sweep as its cost. Holding an
      approved decision for an approval it already had is the manufactured gate this repo's rules
      forbid. Original text kept below.
    - ~~**What remains is two lines and one test edit**~~ — **done: four rows, one inverted tripwire,
      one new `ProductionProfilePathTests`.** The delta was as predicted, plus the clause-2 finding
      above, which no amount of measurement of *this* task could have produced — it took reading
      `BattleEngine`'s ordering.

- [x] ⭐ **B37: route battle resolution through the per-actor turn FSM** — **DONE 2026-09-04** (added and built in the same run)
  - **What it is:** `BattleEngine.Resolve` runs the round skeleton on the kernel's clock and queue, but
    combat itself does not go through `ActorTurnMachine` / `ReadinessDriver` / `ActionSlots` /
    `ITurnEconomy`. Those four types appear **nowhere** in `BattleEngine.cs` or `BattleRunState.cs`,
    and the `profile` parameter is **never read** (twice in the file: the signature and a comment).
  - **This is not new scope invented here — it is B9's own deliberately deferred half**, and both the
    code and the spec say so. `TurnReadiness`'s doc comment: *"B9's own remaining half (scheduling a
    live `Readiness` event and wiring `Charging → Ready` in `ActionRunner`) … is NOT attempted here."*
    `spec-kernel-adoption.md` calls combat routing through the per-actor FSM *"explicitly out of
    scope"* for T5. It was correctly deferred; it was never scheduled.
  - ⛔ **Six open items and three checkpoints are blocked behind it**, which is why it belongs in the
    plan rather than in a comment: **B20, B21, B22** (an interactive dwell needs a turn to occupy) and
    **B34, B35, B36** (a profile switch changes nothing while every profile field is inert), plus
    Checkpoints C, E and G.
  - **Acceptance:** `W`, `WScope`, `Commitment` and `Economy` demonstrably change a resolved battle —
    proven by contrast, the same way B33 proved `W` at the slot layer. Zero-content byte-identity still
    holds for `classic-round`, because that profile pins every knob to today's values.
  - **Verify:** goldens byte-identical under `classic-round`; a `galaxy-sync` battle provably differs.
    Scope: **L** — and it should get its own spec first, like every other module of this size.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - **Spec first**, as the task itself required:
      [`spec-fsm-routing.md`](../docs/architecture/battle/spec-fsm-routing.md).
    - **`BattleEngine.Resolve` now reads the profile.** The action phase is gated by the profile's own
      `ITurnEconomy` and `ActionSlots`: budgets reset at the round boundary, an actor acts only if it
      can afford a turn and take a slot, and the slot is released in a `finally` on every exit path.
    - **The gate held: `classic-round` is byte-identical**, and by construction rather than luck —
      `OneActionPerTurnEconomy.TryAcquire` is `_spent.Add(key)`, so every actor succeeds exactly once
      on pass 1 and fails on pass 2, which is precisely the loop it replaced. Goldens + traces
      **40/40**; `RulesetVersion` still 4.
    - **The economy demonstrably binds**, proven by contrast *and* direction: the same battle at the
      same seed differs under `hybrid-atb`'s `ActionPointsEconomy(2)`, and resolves in **no more**
      rounds — two actions per round cannot take longer.
    - ⭐ **Falsifier: replacing the economy gate with a hardcoded one-action rule turned the contrast
      test red (1 of 8); restored → 8 of 8.** The gate is load-bearing, not decorative.
    - ⛔ **A real concurrency defect found and fixed, which the golden gate caught within minutes.**
      `BattleModeProfile.Economy` handed out a **shared instance** on a **cached singleton** profile,
      and every economy holds mutable per-key budget state. Battle actor keys repeat — `"squad:0"` is
      `"squad:0"` in every battle — so two battles running at once shared one budget and starved each
      other's actors of turns. **Reproduced precisely:** the trace goldens passed when run alone
      (7/7, 5/5) and failed inside the parallel suite, with actors 2..n never acting. Fixed at the
      root: the profile now exposes `Func<ITurnEconomy> NewEconomy`, the engine makes **one economy per
      battle**, and a test pins that two calls never return the same object and never share budget.
      A factory makes the hazard unrepresentable rather than merely fixed.
    - **Suite:** full Core **2 failed / 5656 passed** across **three consecutive runs**, nothing beyond
      the inherited world-stage `loamUnits` pair. Four boundary guards green; `M1 = 0`; overflow A1/A2
      clean.
    - ### ⚠️ What this unblocks, and what it does NOT — stated precisely
      - ✅ **B34, B35, B36 are unblocked.** The profile is read and a profile switch provably changes a
        battle, so the staged sweep now has something real to measure and the migration has a cause.
      - ⛔ **B20, B21, B22 are NOT unblocked.** An interactive dwell needs a `Ready` state to occupy,
        and `ActorTurnMachine` / `ReadinessDriver` are still not in the engine — this module wired the
        *economy and slot gates*, which is the ceiling for a batch resolver (see the spec's scope
        table: `W` cannot bind without wind-up, and `AdvancePolicy` has no frames to step). Routing the
        per-actor STATE MACHINE is a further slice and needs its own module.
      - **`Commitment` was deliberately deferred**, not forgotten: `classic-round` and `galaxy-sync`
        are both `LateBound`, so nothing shipped needs early binding, and its golden delta (the
        `BloodthirstyView` retarget B35 already flags) is better predicted in the same pass as the
        migration that selects a profile using it.

- [x] ⭐ **B38: drive the per-actor turn cycle** — **NEW + DONE 2026-09-04.** The second half of the
  routing gap; B37 wired the profile's GATES, this wires its STATE MACHINE.
  - **What it is:** `ActorTurnMachine` was fully built and fully tested but appeared **nowhere** in
    `BattleEngine` or `BattleRunState`. A battle never produced a `Ready` state, which is precisely
    why an interactive dwell had nothing to occupy. Every actor now walks
    `Charging → Ready → Committed → Resolving → Recovering → Charging` around its action, with the
    kernel's own "passed turn" edge (`Ready → Charging`) taken for anyone who never got one.
  - **Byte-identical:** with `classic-round`'s zero wind-up and zero recovery the cycle collapses to
    bookkeeping over the same attacks in the same order drawing the same RNG. **Goldens 40/40**,
    `RulesetVersion` still 4.
  - **OBSERVE:** transitions are recorded via a new `BattleTrace.Turn`, deliberately kept **out of
    `Digest`** for the reason `Target`/`Apply` already are — the digest is the parity ladder's fixture,
    so writing to it would move every trace golden and make an observability addition
    indistinguishable from a behaviour change.
  - ⛔ **Two real defects found and fixed during the build, both caught by tests rather than review:**
    1. **A leaked slot.** The turn-state gate was placed *after* `slots.TryAcquire`, so every rejected
       actor kept its slot — with `W = 1` that starves every later actor in the round. The gate now
       precedes resource acquisition.
    2. **Readiness offered once per round silently capped every economy at one action**, which made
       `hybrid-atb` identical to `classic-round` and would have quietly invalidated B34's entire sweep.
       Readiness is now offered at the start of **every pass**, so the ECONOMY decides action count and
       the state machine only tracks it. **B34's sweep caught this within seconds of the change.**
  - **Falsifier:** restoring the once-per-round offer turns 2 tests red; restored → 7/7 green.
  - **Tests:** `TurnCycleRoutingTests` 5/5 — the full cycle appears for a real actor, every recorded
    transition is one the kernel's own table allows, a points economy commits more often than a
    one-action economy, nobody is stranded in `Ready`, and tracing is inert.
  - **Suite:** full Core **4 failed / 5730 passed** — 2 atom-stream (in-flight `ModifyMatch`) and 2
    world-stage (`loamUnits`), none from this work. `M1 = 0`; overflow A1/A2 clean; four guards green.
  - ⭐ **This unblocks the FSM half of B20/B21/B22**: a `Ready` dwell now exists for an interactive
    intent source to occupy. What those still need is their own build (an `IIntentSource`
    implementation, `decisions_json` persistence, and — for B22 — a SignalR session), all specced in
    [`spec-interactive-turns.md`](../docs/architecture/battle/spec-interactive-turns.md).

- [x] ⭐ **B39: order turns by readiness — the program's last feature gap** — **BUILT AND VERIFIED 2026-09-04**
  - **Why it exists.** Checkpoint G's very first clause is *"speed matters in production."* B36 flipped
    the four waves to `hybrid-atb` expecting that to deliver it, and **measurement says it does not**:
    a `turn.speed` mod of **+100,000** leaves a resolved battle's rounds and outcome identical
    (`ProductionProfilePathTests.Turn_speed_does_not_yet_change_turn_order_on_the_battle_path`). This
    task is the gap that measurement exposed. It was never scheduled by the original audit, which
    assumed the content flip would carry it.
  - **What is actually missing — a wiring gap, not a wall, and the distinction is load-bearing here.**
    Everything the feature needs already exists in the RPG layer and is tested:
    - `DerivedTurnChannels.Speed` (`turn.speed`) is a **registered** channel — an unknown id throws at
      compose (`BattleStatComposer.cs:153`), so a mod already arrives and lands in the snapshot.
    - `TurnReadiness.EffectiveRate` is the rate math, with `SpeedScale` and
      `DerivedStatPolicy.TurnDefaultSpeed` behind it.
    - `ReadinessDriver` drives readiness and responds live to a `turn.speed`/`turn.haste` mutation.
    - **Nothing calls any of them from the battle path.** `BattleEngine` orders by
      `OrderBy(initiative roll − swift bonus)` (`BattleEngine.cs:308-319`) and contains **zero**
      references to all three types.
  - **The change**: `BattleEngine` orders by readiness — not by the initiative roll — when the resolved
    profile's advance policy says so. `classic-round` must keep the initiative ordering it has, because
    it pins readiness to a constant by design (`battle-turn-ideal.md` §10) and its byte-identity is
    load-bearing for every existing golden.
  - ⛔ ~~**This one really does move goldens** … it carries a `RulesetVersion` 4 → 5 bump, a re-bless, a
    win-rate sweep, and an owner sign-off.~~ **I predicted that, and the measurement says NO. Zero
    goldens moved.** I had used the prediction as a reason to defer the work; it was wrong on both
    counts — wrong that it would move goldens, and wrong to defer on an untested prediction. **Test the
    constraint before declaring it** — the rule this repo already has, and the one I broke.
  - ⭐ **Why nothing moved, which is the interesting part.** Readiness only reorders a round when
    speeds actually *differ*, and **no shipped content authors a `turn.speed` at all** — every actor
    reads 0 from the snapshot and clamps to the same `TurnDefaultSpeed`, so every comparison is a tie
    and falls through to the same initiative jitter as before. The feature is **live and inert**, the
    identical shape every other module in this program shipped. `RulesetVersion` stays **4**; a bump is
    earned by a moved golden, and none moved. It is pinned by
    `Equal_speed_actors_order_exactly_as_they_did_before_readiness_ordering`, which will go red the day
    a content pass starts authoring speed — **that** is when the bump is earned.
  - **What was built.**
    - `BattleModeProfile.OrdersBySpeed` — a **declared row field**, `true` only on `hybrid-atb`.
      ⛔ Deliberately **not** computed as `AdvancePolicy == FixedIncrement`: that is the exact branch
      `ModeProfileArchitectureTests` bans, and it would silently decide the question for every future
      mode sharing an advance policy. Same correction `ForecastExactness` already carries. An ATB mode
      that ignored speed would be one in name only, which is why that row is the `true` one.
    - `BattleEngine`: the initiative draw is **hoisted out of the sort key** so both orderings consume
      the RNG identically — one draw per Active actor in source order. That is not tidying: a different
      draw count or sequence would shift every downstream roll and the delta would stop being
      attributable to turn *order* alone.
    - `BattleEngine.ReadyTicks` — the readiness kernel's own math
      (`TicksFor(OneTurnWork, EffectiveRate(speed, haste))`), with both channels clamped to their
      declared defaults. **The clamp is required, not defensive:** `EffectiveRate` throws on a
      non-positive input and every actor today reads speed 0, so an unclamped call would throw on the
      first ordinary battle. `long` and **rounded, not truncated** — truncation would read a speed of
      99.9 as 99 and make a faster actor sort slower.
    - The initiative jitter survives as the **tie-break**. Equal speeds are the common case, and
      discarding it there would replace a fair random order with setup-list order — proven load-bearing
      by falsifier F2.
  - Acceptance: a fast actor demonstrably acts before a slow one on the production path (the assertion
    B36's acceptance actually wanted) ✅; `classic-round` stays byte-identical ✅; every moved golden
    predicted in advance ✅ (**none moved**); ~~the sweep run and signed off~~ — **not owed: no golden
    moved, no re-bless happened, so the *Golden ordering across streams* sign-off never triggers.**
    - ✅ `A_faster_actor_acts_before_a_slower_one` proves it **by contrast in both directions** — the
      same seed and setup run twice with the speed advantage swapped between two actors, so a lucky
      initiative roll cannot pass it. Order is read off the turn-state trace
      (`Ready->Committed` sequence), so the assertion is about ordering and not about who won.
    - ✅ `Classic_round_still_ignores_speed` is the mirror that **proves the gate closes**, not merely
      that it opens — a +100,000 speed mod leaves a `classic-round` battle's rounds and outcome
      identical.
  - **Falsifiers run, both reddening the intended tests:** setting `ordersBySpeed: false` on
    `hybrid-atb` reddens the fast-actor and gate-closes tests; replacing the jitter tie-break with a
    constant reddens the equal-speed ordering test **while the goldens stay green** — which is itself
    the proof that the goldens run `classic-round` and never touch this path.
  - Verify: replace `Turn_speed_does_not_yet_change_turn_order_on_the_battle_path` with the positive
    assertion — **do not simply delete it** ✅ (replaced, and the class doc-comment updated to point at
    the replacement); full Core + Data, four guards, `--filter ~Golden`. Scope: L.

### ✅ Checkpoint G — program complete — **CLOSED 2026-09-04. Every buildable clause met; one retired by measurement.**
- [x] Clause by clause, each against code rather than against this plan's own predictions:
  - ✅ **"Speed matters in production" — YES, as of B39.** It was measured **false** first: with only
    B36's flip live, a `turn.speed` mod of **+100,000** left rounds and outcome identical, because
    `BattleEngine` ordered by `OrderBy(initiative roll − swift bonus)` and held zero references to the
    readiness kernel. B36's flip made the **economy** and **W** live; it never touched ordering.
    **B39 closed it**: `BattleEngine` now orders by readiness when the profile's own declared
    `OrdersBySpeed` row says so, proven by contrast in both directions, with `classic-round`
    byte-identical and **no golden moved** — because no shipped content authors a `turn.speed` yet, so
    the feature is live and inert, exactly like every other module here.
  - ✅ The attribution table sums (B35).
  - ✅ Per-wave `W` resolves and ships inert (B33; `PerWaveWidthTests` proves inertness by **reference
    identity**, so "did not override" is indistinguishable from "no mechanism exists").
  - ✅ The purity hole is closed **and asserted** (B31).
  - ✅ `FixedIncrement` cost is **measured** (B32).
  - ⭐ ~~Exactly one `RulesetVersion` bump (4 → 5) shared with B26~~ — **retired: ZERO bumps, and that
    was measured twice.** B35 showed the profile flip moves no golden (the fixtures use `"golden-*"`
    wave ids absent from `WaveCatalog`; the expedition tier hash covers the *plan*), and B26 showed the
    scaled clock cannot move one either (it is injector-side; Core has no `Time.timeScale`).
    `RulesetVersion` stays **4**. **Every moved golden was predicted in advance — because none moved,
    and exactly the one predicted tripwire tripped.**
  - ✅ Commit drafts handed over per task group; **no git writes** (the repo's hard rule).
