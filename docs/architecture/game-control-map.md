# Capability map: `game-control`

**Status:** implemented 2026-09-15. The idea document records the original exploration;
the four module specs and the program [plan](../../tasks/game-control-plan.md) /
[todo](../../tasks/game-control-todo.md) record the reconciled, shipped shape. Standard:
[../contributing/live-probe-standard.md](../contributing/live-probe-standard.md).

Playwright-shaped interactive control of the live Unity game for agents: inspect the
control tree, click controls, run lawn verbs, move the real cursor — every response
structured, every action scope-labeled.

| Module id | Responsibility | Depends on |
|---|---|---|
| `control-inspect` | Budgeted control tree of the current screen (menus as named controls, lawn as cells/cards/entities); snapshot-scoped refs (`cN`); cursor paging; truncation markers | — |
| `control-click` | White-box invoke on a ref: re-resolve ptr, confirm type, call the closed dispatch (`CardUI` → `Mouse.ClickOnCard`, uGUI `Button` → `onClick`, measured custom `*Btn` → `OnMouseDown`/`OnMouseUp`); stale ref → fresh-snapshot error | `control-inspect` |
| `control-act` | Lawn verb chains with telemetry read-back (card-pick→cell-place, shovel, hammer) through the existing debug command path; one receipt per verb | `control-click` |
| `control-cursor` | Real-cursor tier: Win32 `SendInput` move/click at coordinates behind an explicit opt-in, with foreground check + rate limit; P/Invoke isolated per the `Hud/Win32.cs` rule | `control-inspect` |

**Build order:** `control-inspect` → `control-click` + `control-cursor` (parallel) → `control-act`.

**Module specs:** `docs/architecture/game-control/spec-control-inspect.md` ·
`spec-control-click.md` · `spec-control-act.md` · `spec-control-cursor.md`.

**Shared response contract** (owner decision 2026-09-14: richer default — snapshots bundled
with every action response, contrary to the community `return_snapshot:false` lesson; the
budget below is what keeps that decision affordable):

- Every verb response carries `{ok, scope, ref?}`; actions additionally bundle the FULL
  current snapshot of the acted scope (owner decision; truncation-marked past budget).
- Snapshots: interactables only get refs; refs die on the next snapshot; over-budget
  output truncates with a `[TRUNCATED]` marker and a `next_cursor` (never a silent cut).
- Eight distinguished MCP adapters front the existing routes: `debug_inspect`, `debug_click`,
  `debug_act`, `debug_cursor`, `debug_evaluate_search`, `debug_evaluate_methods`,
  `debug_evaluate_call`, and `debug_evaluate_text`. They are adapter-only; there is no
  `debug_control` dispatcher.
- Exception to the bundling rule: `control-cursor` responses stay minimal receipts
  (`{x, y, clicked, foreground}`) — cursor acts on raw coordinates, often with no snapshot
  in hand; the screenshot endpoint is its proof path.
- The four `evaluate` adapters are Game Injector Debug discovery/control facilities. They
  resolve a live object and use the documented primitive-argument contract; they are not
  evidence for server-domain correctness.
- Scope labels follow `live-probe-standard.md` §1 on every response: white-box verbs and
  cursor moves are Game Injector Debug (prove engine state only); persisted reads are
  RPG Server Debug.

**Out of scope for this initiative:** splitting `DebugEndpoints.cs`, touching combat/derived
magnitudes (ActorHub gate N/A — control verbs trigger engine operations, never compose
numbers), any `data/tuning` file (dev tooling; structural consts only), player-facing UI.
