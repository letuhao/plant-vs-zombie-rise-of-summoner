# Plan: Event Pipeline v2 — Phase 2 implementation

Spec: [docs/architecture/event-pipeline-v2-spec.md](../docs/architecture/event-pipeline-v2-spec.md)
Design SSOT: [docs/architecture/event-pipeline-v2-ssot.md](../docs/architecture/event-pipeline-v2-ssot.md)

## Key scoping decision (risk cutter)

**The ring buffer carries the per-hit tier only** — `combat.hit`, `*.damage`, per-hit status
hooks, and effect-consumed spawn triggers (`bullet.init` when an OnSpawn grant is live), plus
drain-generated chain records. Everything else — board lifecycle, `match.*`, spawn/die
membership, mower/card/travel — **stays synchronous exactly as today**: those kinds are
per-spawn/per-death/per-match rate (cheap), and they carry every hard ordering hazard the
audit found (die-before-withdraw, board.start-first, MatchHost membership, XP per-instance).
By draining only the hot tier, the barrier logic shrinks to: "a synchronous death/lifecycle
event flushes ring records referencing that ptr / that match before proceeding."

This preserves: MatchHost timing (unchanged), UniqueBoundLoadout same-frame stat writes
(unchanged), XP per-instance events (never enter the ring), `ForgetEntity` ordering (death
flushes the ptr's pending hits first — bounded, per-death).

## Components and order

| # | Component | Depends on | Parallel? |
|---|---|---|---|
| A | `GameEventRec` struct + kind enum + string interning (matchKey/grantId) | — | with B |
| B | Proc math takes `HitCount`: counters accumulate by N, one burst per crossing; chance = single roll vs `1−(1−p)^n`; `max_stacks` consumes per hit. Default N=1 → v1 behavior byte-identical | — | with A |
| C | `GameEventRing` (fixed cap 4096, single-writer, drop-with-counter overflow policy for droppable kinds) + `EventCoalescer` (key per SSOT §4b.2; chain/`SourceGrantId` never merge) | A | — |
| D | `EventDrain`: budgeted FIFO (budget = 10% of frame, from measured frame time), cost classes (expensive actions 1/frame), ptr-flush barrier API, generation cap (≤3), chain records inherit depth+1 (clamped limit 1..8, default 6), session bypass (no budget, no coalescing) | A,B,C | — |
| E | Injector wiring: record sites replace direct `OnCapture` calls for hot kinds; drain runs in `InjectorLoop.Tick` before `TickDots`, sharing one board freeze; death/lifecycle hooks call ptr-flush; transport payloads for hot kinds built at drain time | D | — |
| F | FPS cap default 60 (`FUSIONRPG_FPS_CAP` unset → 60; `0` → uncapped); PerfProbe `drain.tick` section + ring depth/dropped counters | E | with E |
| G | Live verification: stress board at max speed, LIVE checklist F-rows in a session, probe scenarios before/after | E,F | — |

## Risks and mitigations

1. **Dealt/taken pairing across the ring** — `PairId` stamped by the hook that emits both
   records of one physical hit; replaces the fragile 8-event window. Test group 1.
2. **Death flush cost** — a death flushing that ptr's pending hit records is bounded by
   per-target pending count (small); test with burst scenarios.
3. **Chain semantics change** (depth tightening) — owner-approved; explicit test + SSOT note.
4. **Coalescer over-merge tripping the funnel's 1e9 cap** — test with extreme amounts.
5. **Session fidelity** — bypass path tested offline + LIVE F-rows re-run (checkpoint 4).
6. **Behavior drift in v1-shared code (B)** — `HitCount=1` default keeps every existing test
   green untouched; new behavior only activates for merged records.

## Verification checkpoints

1. After A+B: new unit tests green, all 733 existing Core tests green untouched.
2. After C+D: full offline test groups 1–6 (spec Testing Strategy) green.
3. After E+F: injector builds vs game dir; 4 boundary guards green; all suites green.
4. Deploy: stress probe hits spec success criteria (≤5% frame share, ≤3-frame effect latency,
   gen2=0); LIVE checklist F-rows pass in a session.

## Out of scope

Launcher UI for the fps setting; server/web changes (none needed — hot kinds either don't
reach the server or arrive as today, batched); Phase 3 niceties (adaptive coalescing windows).
