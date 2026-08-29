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
