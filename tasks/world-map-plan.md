# Implementation Plan: world map — wave 1 (foundation)

Specs: [../docs/architecture/world/spec-world-model.md](../docs/architecture/world/spec-world-model.md) · [spec-turn-engine.md](../docs/architecture/world/spec-turn-engine.md) · [spec-world-movement.md](../docs/architecture/world/spec-world-movement.md). Map: [../docs/architecture/world-map-program.md](../docs/architecture/world-map-program.md). Ideal: [../docs/architecture/world-graph-ideal.md](../docs/architecture/world-graph-ideal.md). Tasks: [world-map-todo.md](world-map-todo.md).
Named pair per repo convention — `tasks/plan.md` / `todo.md` hold the perf-v3 stream.

**Gate:** the three specs are **Draft — pending owner review**. This plan is written against them; if module boundaries move in review, the plan is rewritten rather than patched. No code until the owner approves both.

## Overview

Build the map's foundation: the places, the clock, and the first verbs. Wave 1 ends when a legion can march across a hand-authored six-sector world, meet an enemy mid-lane, clear a guarded slot, claim ground, and lose supply when the chain is cut — with the whole campaign replaying byte-identically from `(seed, template, command log)`.

Nothing on the map earns anything yet (economy is wave 3), nothing generates maps yet (wave 4), and combat is a deliberately disposable placeholder until the combat stream's seam lands.

## Options adopted in review (2026-08-21)

Four alternatives were taken over the first draft's defaults. All four **reduce** wave-1 work or defer decisions to the module that owns them; each is reversible by the owner.

| Decision | Instead of | Why |
|---|---|---|
| **Discrete-event movement resolution** | a fixed `StepsPerTurn = 12` | no arbitrary constant baked into `RulesetVersion`; crossings are exact rather than quantised; a quiet turn costs almost nothing |
| **Per-slot guards named by encounter id** | a `guard_strength` scalar on the sector | wave 1 never invents combat numbers it is unqualified to balance; guards defend the vein, not the ground; clearing a rich sector becomes several fights |
| **Turn reports re-derived, hot tail stored** | `report_json` on every turn forever | the engine is deterministic, so a report is reproducible; the log keeps hashes and versions, which are what detect drift |
| **FE renders from fixtures first** | FE blocked behind the state endpoint | W13 can start immediately, in parallel with all server work — the map becomes visible before movement exists |

## Architecture decisions (from the specs — locked)

- **`TurnEngine.Step` is pure**: no I/O, no clock, no ambient state. A guard test enforces the no-wall-clock half.
- **The barrier ships with exactly one policy** (`WaitForAllCommitted`). The interface exists so `Step` never learns why it fired — that keeps the RTS and idle policies reachable — but no second policy is built here.
- **Movement is a discrete-event queue** ordered by `(timeMilli, entityId)`, integer per-mille within the turn, with a monotonicity assert: processing an event may enqueue later events, never earlier ones.
- **One turn is one transaction**, correlation-idempotent, mirroring `ExecuteSummon` / `ExecuteFusion`.
- **Determinism discipline is `BattleEngine`'s**, reused verbatim: integer per-mille, stable ordering by entity id, named derived RNG streams, version stamps, golden hashes.
- **One entity table with a `kind`**; **derived state is recomputed, never stored** (supply, banner element, claimable).
- **Combat is a port.** `IBattleResolver` with a placeholder that wave 3 deletes; the map never reads combat internals and never authors combat numbers.
- **The map FE uses React Flow (`@xyflow/react`, MIT)** — owner decision after a library survey. Sectors are custom React nodes, lanes custom edges, positions authored, dragging and connecting off: a viewer, not an editor. Rejected: Cytoscape/Sigma/G6 (analysis or 100k-node scale we do not have), vis-network (physics fights authored positions), from-scratch SVG (re-solving pan/zoom for no gain). Leaflet `CRS.Simple` remains the fallback if the map ever wants a painted backdrop.
- **All new server code lives under `Core/World/*`** — no edits to `Core/Battle`, `Core/Combat`, `Core/Status`, or any injector path, so concurrent streams stay untouched.

## Dependency graph

```
W1 catalogs ──► W2 WorldState + first-light template + validation
                      │
                      ▼
                W3 schema + CreateWorld/Load (Data)
                      │
                      ▼
                W4 DTOs + read endpoints + SIM create
                      │
                W5 command model + command store
                      │
                W6 TurnEngine (phases, barrier, event queue, report, hash)
                      │
                W7 turn transaction + turn log (hot tail + re-derive)
                      │
                W8 determinism guards ── Checkpoint 2 (the SSOT gate)
                      │
      ┌───────────────┼───────────────┐
      ▼               ▼               ▼
 W9 movement     W12 supply      (W10 needs W9)
      │            traversal
      ▼
 W10 ZOC + contact + clear + placeholder resolver
      │
      ▼
 W11 claim ──────► Checkpoint 3 (wave-1 acceptance)

W13 FE map (fixtures) ──► W14 FE orders + playback     [parallel from day one]
```

**W13 has no server dependency** — it renders from a checked-in fixture, so the FE and the engine progress independently and meet at W14. **W12 is parallel-safe** with W9–W11: supply is a traversal over ownership, needing the phase pipeline rather than movement.

## Phases

1. **Model + storage (W1–W4).** The nouns exist, validate, persist, and read back. Zero behavior.
2. **The clock (W5–W8).** Commands, phases, event queue, turn transaction, hashes, guards. No gameplay verbs beyond `stand-fast`. Ends at **Checkpoint 2 — the SSOT gate**, the highest-value checkpoint in the wave.
3. **The first verbs (W9–W12).** Movement, contact, clear, claim, supply. Ends at Checkpoint 3, the wave-1 acceptance scenario.
4. **FE (W13–W14), running in parallel from the start.** Fixture-driven map first, live orders once W11 lands.

High-risk work is deliberately early: determinism and the turn transaction land before any gameplay depends on them.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Determinism drift via dictionary iteration, float, or wall clock | High | W8 guard tests (symbol scan, hash stability, replay), stable-ordering asserts in every golden |
| Event-queue bugs — a missed or mis-ordered crossing is silent | High | monotonicity assert on every enqueue; property test over random speed/progress pairs; crossing point must lie between both start positions |
| Seven new tables land before gameplay proves the shape | Medium | W2 proves the model in memory first; W3 persists only what W2 validated; `EnsureColumn` keeps later columns cheap |
| Placeholder resolver quietly becomes permanent | Medium | behind `IBattleResolver`, named `Placeholder*`, test asserts no production registration outside the world module; wave 3 deletes it as an explicit task |
| Report re-derivation is slower than expected on long campaigns | Low | hot tail keeps recent turns instant; if replay cost bites, periodic state snapshots are an additive change, not a redesign |
| React Flow re-render churn on pan with rich sector cards | Medium | memoized or module-scope node/edge components; render-count test in W13; playback writes transforms through refs, never React state |
| Concurrent streams (combat unification, VFX, perf) touching Core | Medium | everything new under `Core/World/*`; full suites at every checkpoint, not just filtered runs |

## Definition of done for the wave

- `dotnet test tests\FusionRpg.Core.Tests`, `...Data.Tests`, `...E2E.Tests`, `...Guard.Tests` green; `cd web\fusion-rpg-web; npm run test` green.
- `.\scripts\guard-dal.ps1` green (run all four guards at the final checkpoint).
- Wave-1 acceptance scenario passes: 20 turns, golden state hash, byte-identical replay, one transaction and one turn-log row per turn.
- No file modified outside `Core/World/*`, `Data/Sqlite/RpgStore.World*.cs`, `Contracts/WorldDtos.cs`, `Server/World*.cs`, `web/.../features/world/*`, and their tests.
- Owner receives a commit message draft and touched paths — **git hands-off; the agent never commits.**

## Open questions

1. **Guard wave ids: validated or opaque in wave 1?** Opaque strings are cheaper now and let the combat stream define the catalog; validation is a one-line change when it does.
2. **Report hot-tail depth** (proposed 50) — a guess until a real campaign measures replay cost.
3. **Homeworld loss consequences** (ideal §10.5) — the menu now includes **partial capture** across the homeworld's four sites and **a per-world difficulty setting**; still the owner's tone call, still not blocking this wave.
4. **The commander slot before heroes exist** — candidate: reuse the shipped **patron** demon rather than inventing a second concept. Ideal-level, not wave-1.
