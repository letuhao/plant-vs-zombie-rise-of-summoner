# Plan: battle-timeline — the virtual-time battle kernel

Map: [docs/architecture/battle-timeline-map.md](../docs/architecture/battle-timeline-map.md) · Ideal: [battle-turn-ideal.md](../docs/architecture/battle-turn-ideal.md) · Specs: [docs/architecture/battle/](../docs/architecture/battle/spec-virtual-time-core.md) · Audit: [audit-2026-08-21.md](../docs/architecture/battle/audit-2026-08-21.md)

Task list: [battle-timeline-todo.md](battle-timeline-todo.md). *(`tasks/plan.md` and `tasks/todo.md` hold the Perf v3 program; this stream uses the `battle-timeline-*` pair.)*

## What we're building

A virtual-time kernel — simulation clock, event queue, per-actor state machine — on which every battle mode is configuration rather than a code path. It exists so combat action management (skills, attack, defence, movement) has a timeline to be scheduled on.

## Owner decisions this plan is built on (2026-08-21)

1. **Full kernel** — reaction lane, side-scheduled press-turn, and multi-actor link-strikes all in scope.
2. **T9 after the gate, versioned** — T5 preserves today's sub-round under-delivery; T9 fixes it deliberately.
3. **Full live sessions** — real interactive input, which makes the determinism trace mandatory.
4. **Profile chosen by content**, resolved from the existing `WaveId` / tier id so nothing new is serialized.

## Scope honesty

Decisions 1 and 3 roughly double the program: eleven modules across five phases. The ordering below is deliberate — **the existing game is protected at Phase 2 and everything novel lands behind its own checkpoint afterwards.** If the program stops at any checkpoint, what shipped is coherent and green.

Five modules (T6, T7, T8, T10, T11) are **not yet specced**. Their tasks begin with writing the spec, following the house pattern where each wave elaborates at wave start rather than being speculated about now.

## Slicing principle

Vertical, not horizontal. Each task carries one complete path — types, behavior, and its tests — rather than "all the records, then all the logic." The kernel is provable with zero real actions until Phase 1's last task, which deliberately drives a **real** attack through it, because the audit's sharpest finding was that the seam would otherwise reach the gate with no consumer.

## Phases

### Phase 0 — prerequisites (nothing builds before these)

The `decisions.md` row is a hard boundary in AGENTS.md. The observation seam and fixtures **must precede any engine edit** — pre-adoption traces cannot be captured after the engine changes, and the parity ladder is worthless without them.

### Phase 1 — the kernel (T1–T4)

Pure, no game attached. Ends by driving a real basic attack under `galaxy-sync`.
**Checkpoint A — capability.**

### Phase 2 — the gate (T5)

`BattleEngine` gives up its loop. Byte-identical: eight goldens still, six suites green with no test edits, four guards green.
**Checkpoint B — safety.** Nothing proceeds past a drift.

### Phase 3 — the deliberate change (T9)

Status pulses and shield upkeep move onto kernel ticks and start firing at their true times. This is a **behavior change**: `RulesetVersion` bump, one re-bless with a predicted delta, win-rate sweep for sign-off.
**Checkpoint B2 — versioned.**

### Phase 4 — interactive battles (T8, T6, T10, T11)

Forecast first (cheap, and it validates that the queue really is the single source of truth), then the dwell, then the trace, then live sessions. **T6 and T10 ship together** — an interactive battle without a persisted decision trace is precisely the hole the audit found, where a boot sweep silently overwrites a player's win.
**Checkpoint C.**

### Phase 5 — the observer (T7)

Stateless projection of live PvZ events into the same vocabulary. Last because it touches the injector hot path, where this repo has documented perf sensitivity.
**Checkpoint D — program complete.**

## Risks and how the plan handles them

| Risk | Handling |
|---|---|
| The gate fails with an opaque hash diff | The parity ladder localizes drift to a stream, phase, round, or event before the hash is ever consulted |
| Pre-adoption traces impossible to capture | Phase 0 does it first, with an `internal` draw log on `SeededRng` — observation only, `RngAlgoVersion` stays 1 |
| The seam is wrong and we find out at T5 | Phase 1 ends with a real action under a demanding profile; `W` is proven by contrast, not vacuously |
| An immortal revive crashes adoption | `Downed` and the full transition table land in Phase 1, with a meta-test binding table to diagram |
| Sub-round timing "fixed" accidentally at T5 | Named hazard, plus a sub-round fixture captured in Phase 0 that would fail loudly |
| Live sessions silently corrupt match history | T10 ships with T6; timeouts are recorded **as decisions at a tick**, never evaluated against wall-clock; the sweep refuses incomplete traces rather than healing them |
| Rendezvous deadlock at `W=1` | Bounded timeout is mandatory, with an explicit fallback-to-solo test |
| Injector regression | T7 is stateless projection — no queue, no scheduling, no per-actor machine injector-side — with the existing perf budget as acceptance |

## Verification standard

Every task: its own tests green, the full Core suite green, and no edits to existing tests. From Phase 2 on, every task also runs the four boundary guards. Golden hashes move exactly twice in the whole program — never at Phase 2, once at Phase 3, and once more only if Phase 4 changes report shape.

## Not in this program

Action *content* — specific skills, attacks, defences, damage numbers, targeting shapes, AOE, projectiles. That is the next program, and this one exists to give it a timeline. Enrichment waves E1 (riders) and E2 (skills) are rebased onto the timeline after T9; E3 (hybrid payloads) is resolver-side and independent of all of this.
