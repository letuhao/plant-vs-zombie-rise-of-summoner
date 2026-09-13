# Implementation Plan: Debug MCP server

**Spec:** [`docs/architecture/debug-mcp/spec-debug-mcp.md`](../docs/architecture/debug-mcp/spec-debug-mcp.md) ·
**Map:** [`docs/architecture/debug-mcp-map.md`](../docs/architecture/debug-mcp-map.md) ·
**Tasks:** [`debug-mcp-todo.md`](debug-mcp-todo.md) (this plan's task list target).

## Overview

Build `tools/debug-mcp/` — a per-session Python MCP server (`fastmcp==4.0.3`,
stdio default, local-HTTP flag) exposing 7 unified, scope-labeled,
budget-enforcing diagnostic tools over the real server/game. No C# changes;
no domain logic; every tool adapts an existing route, service, store API, CLI,
or file. Roster is locked at 7 by the spec — an 8th tool amends the spec first.

## Architecture Decisions

- **Adapter-only, enforced by `test_registry.py`.** Every tool maps to an
  existing endpoint/service/CLI; orphans fail the suite. (SOLID §2.15: no
  parallel implementation.)
- **Allowlist generated at build** from `DebugEndpoints.cs` registrations
  filtered by guard-debug-scope relay semantics — a reviewable diff, re-checked
  live by the registry test. Never a hand list.
- **Budgets structural** (limit 20 / cap 100 / cursor / truncated flag); silent
  truncation is a test failure, not a fallback.
- **Scope labels derived, not hand-labeled** (relay ⇒ injector-shaped); banner
  annotations land under their own program and are not a dependency.
- **No pre-work gates.** Nothing here is irreversible (all code, all
  revertible); SIM-server availability is a per-task skip condition, not a
  gate — checked against "Gates vs. checkpoints": no halt survives both
  questions.

## Dependency Graph

```text
registry.py + budget.py + evidence.py + server scaffold (T1)
    │
    ├── debug_call + generated allowlist (T2)
    │       │
    │       ├── debug_events (T3) ─┐
    │       ├── debug_match (T4, budgeted digest; logs cut — no source) ─┤
    │       ├── debug_actor (T5) ──────────────┤ parallelizable after T2
    │       ├── debug_verify (T6, needs T5 ladder pattern)
    │       ├── debug_preflight (T7, needs T1 only)
    │       └── debug_lawn_setup (T10, needs T1+T2: adapter over
    │           POST /api/debug/lawn/quick-start; runs with Phase 2/3)
    │
    └── HTTP mode + README + full live pass (T8–T9)
```

## Task List (summary — full template per task lives in `debug-mcp-todo.md`)

- **Phase 1 — Foundation:** T1 scaffold + shared modules + envelope/registry
  skeleton tests; T2 `debug_call` end to end (stdio → :5088 → scope-stamped).
- **Checkpoint 1:** pytest green; `tools/list` shows 1 tool; one live call
  against SIM server returns a scope-stamped body.
- **Phase 2 — Reads:** T3 `debug_events`; T4 `debug_match` (logs cut at build:
  no stable process-log source — see todo);
  T5 `debug_actor` (T3/T4/T5 parallelizable after T2; T7 may run alongside).
- **Checkpoint 2:** budget tests refuse silent truncation; actor snapshot shows
  DB + runtime + Hub + events for a real summoned specimen.
- **Phase 3 — Verdicts:** T6 `debug_verify` ladder (first broken link +
  file:line evidence on a seeded break); T7 `debug_preflight` readiness audit;
  T10 `debug_lawn_setup` (one-call live-board setup; needs T1+T2 only, runs
  with Phase 2/3).
- **Checkpoint 3:** seeded break reported with the rung named; preflight
  reports PASS/FAIL + fix command per check on this machine.
- **Phase 4 — Transports + acceptance:** T8 HTTP mode + README Inspector
  checklist (confirm Inspector flags against upstream docs — carried caveat);
  T9 full live pass, guards green, roster-stability test (exactly 7 tools).
- **Checkpoint Complete:** all spec success criteria met; owner completes the
  README walkthrough; ready for merge review.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| FastMCP 4 API drift | Med | Pin `fastmcp==4.0.3`; changelog verified 2026-09-13 |
| Inspector CLI flags unverified | Low | Confirm at T8 against upstream docs before writing README |
| Scope banners land mid-build | Low | Registry derives from relay rule, never from banners |
| Unbounded upstream dump (board-stats) | Med | Budgets in tools 2/5/6; digest-not-dump shapes |
| No SIM server at verify time | Low | Reachability-gated skip, never fail; owner live pass at T9 |

## Parallelization

- Safe parallel after T2: T3, T4, T5, T7 (disjoint files, shared foundation).
- Sequential: T1 → T2 → checkpoints; T6 after T5; T8–T9 last.
- No task touches C# — concurrent repo streams cannot conflict on these paths.

## Open Questions

None. Spec resolved users, roster, transport, and shape; micro-decisions
(port 8899, generated allowlist) locked in-spec.
