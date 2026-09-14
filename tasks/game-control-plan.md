# Implementation plan: `game-control`

**Program:** `game-control` · **Map:** [../docs/architecture/game-control-map.md](../docs/architecture/game-control-map.md)
**Specs:** `docs/architecture/game-control/spec-{control-inspect,control-click,control-act,control-cursor}.md`
**Task list:** [game-control-todo.md](game-control-todo.md)
**Session:** `game-control-idea-20260914` (plan phase)

> Owner decisions encoded: full-scope snapshots bundled per action, distinguished MCP adapters
> (one tool per verb), cursor tier in v1, Button arm provisional verify-or-drop. The earlier
> unified `debug_control` proposal was explicitly dropped during reconciliation.

---

## Overview

Five vertical slices in dependency order: inspect first (everyone consumes refs), then
click and cursor in parallel (disjoint files, disjoint risk), then act (composes click),
then the distinguished MCP adapters and walkthrough. Each slice is independently testable;
live checkpoints remain evidence gates rather than being inferred from a build.

## Architecture decisions

- **Snapshot id = `debug.inspect` event id** (spec contract) — no separate id service, no
  clock to skew.
- **Ref verification is ptr+type+position** — native ptrs are reused after death
  (`match-runtime.md`); a same-type squatter must not receive another entity's click.
- **Cursor coordinates clamp to the game window rect** — refused outside, never silently
  cornered; foreground check + rate limit + per-call opt-in stay as specified.
- **MCP uses distinguished tools per verb**, not a unified dispatcher: `debug_inspect`,
  `debug_click`, `debug_act`, `debug_cursor`, and the four bounded evaluate adapters. Each is
  adapter-only over the existing route and follows the `debug_screenshot.py` scope/envelope
  convention. This is the reconciled owner decision; do not reintroduce `debug_control`.
- **No pre-work gates.** Nothing here is irreversible (dev-tooling commands on localhost,
  rate-limited; the cursor tier moves a real mouse but only under the owner's own hand at
  their terminal). Live checks name the owner terminal as their venue instead of gating
  on it.

## Task List

### Slice 1: Inspect (`control-inspect`)

- [x] Task 1: `ControlInspect` + `debug.inspect` case + routes + snapshot contract (implemented)
- [ ] Checkpoint 1: menu + lawn snapshots with refs; truncation + paging proven live

### Slice 2a: Click (`control-click`)

- [x] Task 2: `ControlRefs` + `ControlClick` + `debug.click` route + stale-refusal (implemented)
- [ ] Checkpoint 2: menu/card/cell clicks read back; stale + reused-ptr refusals proven

### Slice 2b: Cursor (`control-cursor`, parallel with 2a)

- [x] Task 3: `Win32Input` + `debug.cursor` case + route + triple refusal (implemented)
- [ ] Checkpoint 3: opt-in foreground click lands via telemetry; all three refusals proven

### Slice 3: Act (`control-act`)

- [x] Task 4: `ControlAct` place/shovel/item + `debug.act` route + named-link receipts (implemented)
- [ ] Checkpoint 4: place ends telemetry-proven; bad inputs fail at resolve with names

### Slice 4: Distinguished MCP adapters

- [x] Task 5: distinguished inspect/click/act/cursor/evaluate adapters + registration + walkthrough + tests
- [x] Checkpoint 5: live MCP chain and refusal evidence proven (owner-confirmed 2026-09-15).
  A new deployment is a future regression check, not an open implementation gate.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Menu buttons are custom types, not uGUI `Button` | Click's Button arm is fiction | Provisional verify-or-drop in Task 2 (spec); `UIMgr` arm covers menus regardless |
| `Mouse.Instance` cell-field writes behave differently than reads | Place verb misplaces | Task 4 proves placement via telemetry; direct-method fallback pre-named in spec |
| Snapshot bundling blows up action responses on dense boards | Token cost (Playwright #395 lesson) | Truncation markers + page budget in every response (spec contract); measured live in Task 2/4 |
| Cursor tier startles the operator | Real mouse moves during someone else's session | Opt-in + foreground + rate limit (spec); live checks run at the owner's terminal only |
| `SendInput` needs a P/Invoke pattern the IL2CPP host dislikes | Cursor tier stalls on interop | Win32 P/Invoke already ships in `Hud/Win32.cs` on both hosts — same shape, no new risk class |

## Open Questions

None — all four idea-phase questions answered and encoded; Button arm and place-path
fallbacks are implementation-level with pre-named defaults, not gates.
