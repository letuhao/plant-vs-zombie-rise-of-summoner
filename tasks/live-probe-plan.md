# Implementation plan: `live-probe`

**Program:** `live-probe` · **Map:** [../docs/architecture/live-probe-map.md](../docs/architecture/live-probe-map.md)
**Specs:** `docs/architecture/live-probe/spec-{debug-scope-guard,live-probe-tool,actor-hub-live-proof}.md`
**Task list:** [live-probe-todo.md](live-probe-todo.md)

---

## Overview

Three modules, two of them independent builds and one an operation that consumes the second:
`debug-scope-guard` (a PowerShell guard), `live-probe-tool` (a C# console tool doing real HTTP against
a live Server, in two modes), and `actor-hub-live-proof` (running that tool live to finally close the
T12/T14 evidence gap that started this whole program). No shared files between the two build modules —
they touch entirely disjoint paths (`scripts/guard-debug-scope.ps1` + `tests/FusionRpg.Guard.Tests/*`
vs. `tools/ProveLiveProbe/*` + `scripts/prove-live-probe.ps1`), so they parallelize with zero merge
risk.

## Architecture decisions

- **Classification rule for the guard is "any Injector relay anywhere in the handler body wins"** —
  not the original binary rule, corrected in the spec's own audit. Implementing anything else
  reintroduces the false-positive problem the audit found against the real file.
- **The tool is two modes, not one flow.** Mode A (persisted-state only) needs no live game; Mode B
  (full proof) needs a real summon and a live board. These are built as one CLI surface with a
  `-Mode` switch, not two separate tools — the two modes share every step through 5, and duplicating
  the acquire/allocate/equip/deploy logic across two binaries would be the exact "second orchestrator"
  risk the spec's own audit flagged for a different reason (a duplicate compose of the same real
  calls).
- **`actor-hub-live-proof` is an operation, not a build.** It produces no new source files — its
  "implementation" is running the finished tool against a real game+server and writing down the
  result. It cannot be parallelized with the other two modules (it depends on the tool existing) and
  it cannot be delegated to an ordinary background coding subagent (see Orchestration model below).

## Orchestration model (multi-agent, per owner's request)

**One lead agent, two parallel worker agents, then a lead-run (or owner-run) live operation.**

```
Lead agent
  │
  ├─ dispatches Worker A ─ debug-scope-guard  (Tasks 1-3)  ─┐
  │                                                          ├─ Checkpoint 1 (lead reviews both)
  ├─ dispatches Worker B ─ live-probe-tool    (Tasks 4-8)  ─┘
  │
  └─ (after Checkpoint 1) runs actor-hub-live-proof itself, or hands off to the owner
       (Tasks 9-12) — see "Why this phase is not delegated" below
```

- **Worker A and Worker B run concurrently** (independent subagents/background agents) — no file
  overlap, no ordering constraint between them. The lead does not need to babysit either mid-flight;
  it collects both results at Checkpoint 1.
- **The lead reviews each worker's actual diff and test output before Checkpoint 1 passes** — not just
  the worker's own summary. This session's own established practice (verifying a subagent's claims by
  re-running the test command directly) applies here too: a worker reporting "guard green" or "tool
  builds" is a claim, not a checkpoint pass, until the lead confirms it.
- **Why Tasks 9-12 (`actor-hub-live-proof`) are not delegated to an ordinary subagent:** this phase
  needs a real game + real server running, and `CLAUDE.md`'s own "Server lifetime" hard rule states an
  agent-launched server dies when a synchronous tool call's process tree is reaped — it must be started
  via `Start-Process` from a context that survives, which in practice means the owner's own terminal,
  or a background agent the lead deliberately keeps alive across the whole operation (never a plain
  foreground `Bash` call). The lead either runs this phase itself under those constraints, or hands it
  to the owner explicitly — it is the one phase in this plan that is genuinely operational rather than
  a coding task a fresh subagent can pick up cold.
- **Parallelization classification** (per this skill's own categories): Worker A/B tasks are
  independent feature slices — safe to parallelize. Tasks 9-12 are a dependency chain on a live,
  stateful system — must be sequential, and largely un-parallelizable by nature (one game, one server,
  one specimen at a time).

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Worker A's guard rule still misclassifies some route neither audit pass found | Guard fails CI on a legitimate route, blocking `deploy-play.ps1` | Task 2's fixture tests cover the exact shapes the audit found (mixed body, `MapPost` helper, non-`"debug.*"` relay names) before the guard is wired into `deploy-play.ps1` (Task 3) |
| Worker B's tool works against Mode A but Mode B's async poll never actually reaches a real game in testing | Tool ships un-provable against its own headline claim | Task 8 requires a recorded manual Mode B run (even a minimal one) before Checkpoint 2, not just Mode A automation |
| `actor-hub-live-proof` finds T14 still broken (expected, per the known incident) | Could be mistaken for this plan failing | Named explicitly in the spec's own success criteria — an honest FAIL, correctly reported and left for `bound-loadout-hub` to fix, is this phase succeeding at its actual job, not failing it |
| Owner's terminal/session availability for Tasks 9-12 | Phase 2 could stall indefinitely | Not a hard gate — Tasks 1-8 (both build modules) ship and are useful regardless of when Phase 2 runs; Phase 2 waits on the owner's own schedule, never blocks the rest of this plan from being marked done up to Checkpoint 1 |

## Open questions

None — resolved during `/spec`. No pre-work gate in this plan blocks on an external decision; Phase
2's dependency on Phase 1 is a real code dependency (the tool must exist to run it), not a
manufactured approval gate.
