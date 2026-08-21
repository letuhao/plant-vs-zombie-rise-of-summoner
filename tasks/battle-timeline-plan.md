# Plan: battle-timeline — the virtual-time battle kernel

Map: [docs/architecture/battle-timeline-map.md](../docs/architecture/battle-timeline-map.md) · Ideal: [battle-turn-ideal.md](../docs/architecture/battle-turn-ideal.md) · Specs: [docs/architecture/battle/](../docs/architecture/battle/spec-virtual-time-core.md) · Audit: [audit-2026-08-21.md](../docs/architecture/battle/audit-2026-08-21.md) · Perf: [spec-kernel-performance.md](../docs/architecture/battle/spec-kernel-performance.md)

Task list: [battle-timeline-todo.md](battle-timeline-todo.md). *(`tasks/plan.md` and `tasks/todo.md` hold the Perf v3 program; this stream uses the `battle-timeline-*` pair.)*

**Revised 2026-08-22** after the performance decision. What changed and why is in "The gap this revision closes" below.

## What we're building

A virtual-time kernel — simulation clock, event queue, per-actor state machine — on which every battle mode is configuration rather than a code path, and on which combat action management (skills, attack, defence, movement) is later scheduled.

## Owner decisions this plan is built on

1. **Full kernel** — reaction lane, side-scheduled press-turn, multi-actor link-strikes (2026-08-21).
2. **T9 after the gate, versioned** — T5 preserves today's sub-round under-delivery; T9 fixes it deliberately.
3. **Full live sessions** — real interactive input, which makes the determinism trace mandatory.
4. **Profile chosen by content**, resolved from the existing `WaveId` / tier id so nothing new is serialized.
5. **The kernel runs per-frame inside the injector** (2026-08-22) — and performance is a first-class feature, not a later pass.

## The gap this revision closes

Decision 5 named where the kernel runs, and **the previous plan had no module that runs it there.** T7 observes the game; nothing *drove* the kernel inside the frame. That module now exists as **T13 `injector-kernel-drive`**, and it is not a thin adapter — it is the task that replaces the injector's ad-hoc timing grids (the 100 ms shield tick, the 100 ms DoT grid) with the kernel's event queue. Those grids *are* a primitive scheduler, so this is the same single-source-of-truth argument that motivated the whole program, applied to the injector.

Three consequences:

- **Performance became a constraint on type design**, not a tuning pass. Frame-critical Unity code cannot allocate per tick, so the expensive mistakes are structural (a class where a struct belongs, a string key on a tick path) and cost a rewrite rather than a tune. Phase P now sits *before* the kernel work rather than after it.
- **The riskiest module is now T13, not T5.** T5 is protected by goldens; T13 touches the hot path this repo has already had to rescue once, where the failure mode is stutter that no unit test sees.
- **So T13 gets a measurement gate in front of it.** P2 measures the kernel at stress scale in a synthetic harness *before* any injector code is written. If the kernel cannot hold its slice offline, it will not hold it in the frame, and we learn that for the cost of a harness.

## Scope honesty

Thirteen modules across seven phases. The ordering protects the existing game early (the gate at Phase 2) and puts every piece of new machinery behind its own checkpoint. **If the program stops at any checkpoint, what shipped is coherent and green.** Five modules (T6, T7, T8, T10, T11) are still unspecced; their tasks begin by writing the spec.

## Slicing principle

Vertical: each task carries one complete path — types, behaviour, and its tests — not "all the records, then all the logic". The kernel stays provable with zero real actions until Phase 1's last task, which deliberately drives a **real** attack through it, because the audit's sharpest finding was that the seam would otherwise reach the gate with no consumer.

## Phases

### Phase 0 — prerequisites *(complete)*
The `decisions.md` row, and the RNG observation seam plus pre-adoption fixtures — which could only be captured before the kernel existed.

### Phase P — the performance contract *(P1a complete)*
Budgets inherited from `perf-probe-plan.md`, never restated. Zero steady-state allocation, asserted in CI as **bytes not milliseconds** — a wall-clock assertion in CI measures the build agent's mood and gets muted the first time it flakes. P1d (tick-path source guard) and P2 (stress harness) can run now; P1b/P1c need something ticking in the injector and land with T13.

### Phase 1 — the kernel (T1–T4) *(B2–B4 complete)*
Pure, no game attached, ending with a real attack under `galaxy-sync`.
**Checkpoint A — capability.**

### Phase 2 — the gate (T5)
`BattleEngine` gives up its loop. Byte-identical: eight goldens still, six suites green with no test edits.
**Checkpoint B — safety.** Nothing proceeds past a drift.

### Phase 3 — the deliberate change (T9)
Status pulses and shield upkeep move onto kernel ticks and start firing at true times. A **behaviour change**: version bump, one re-bless, win-rate sweep.
**Checkpoint B2 — versioned.**

### Phase 4 — interactive battles (T8, T6, T10, T11)
Forecast, then the dwell, then the trace, then live sessions. **T6 and T10 ship together** — an interactive battle without a persisted decision trace is precisely the hole where a boot sweep silently overwrites a player's win.
**Checkpoint C.**

### Phase 5 — the observer (T7)
Stateless projection of live PvZ events into the same vocabulary. No queue, no scheduling, no per-actor machine injector-side.
**Checkpoint D.**

### Phase 6 — the injector drive (T13) — *the highest-risk phase*
The kernel ticks inside the frame and takes over the injector's timing grids, under a bounded resumable drain with live probe sections. Gated by P2's measurement and verified against the B1–B9 matrix at 200+ entities.
**Checkpoint E — program complete.**

## Risks and how the plan handles them

| Risk | Handling |
|---|---|
| Kernel cost shows up as stutter no unit test sees | P2 measures at stress scale **before** T13 exists; CI asserts zero-allocation continuously; probe sections measure live |
| A frame held hostage by a large drain | Bounded and resumable, following event-pipeline-v2's frame-budgeted precedent; simulated time is decoupled from wall-clock, so a deferred drain is a pacing effect, not a correctness one |
| "Runs in the injector" quietly becomes "drives the game" | Written boundary: the kernel schedules *our* timeline; Unity still owns its own actors. PvZ stays observed |
| T13 regresses working timing grids | Sequenced last, behind the gate and the observer, with the existing grids' behaviour as the acceptance baseline |
| The gate fails with an opaque hash diff | The parity ladder localises drift to a stream, phase, round, or event before the hash is consulted |
| Live sessions silently corrupt match history | T10 ships with T6; timeouts recorded **as decisions at a tick**; the sweep refuses incomplete traces rather than healing them |

## Verification standard

Every task: its own tests green, the full Core suite green, no edits to existing tests. From Phase 2 on, also the four boundary guards. From Phase 6, also the kernel's probe sections within budget. Golden hashes move exactly **twice** in the whole program — never at Phase 2, once at Phase 3, and once more only if Phase 4 changes report shape.

## Not in this program

Action *content* — specific skills, attacks, defences, damage numbers, targeting shapes, AOE, projectiles. That is the next program, and this one exists to give it a timeline. E1 (riders) and E2 (skills) rebase onto the timeline after T9; E3 (hybrid payloads) is resolver-side and independent.
