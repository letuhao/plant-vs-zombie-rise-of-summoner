# Live-test maintain

How to enrich and keep honest the LIVE harness and docs. Agent rule: [`.cursor/rules/live-test-maintain.mdc`](../../.cursor/rules/live-test-maintain.mdc). Operator SSOT: [runbook/live-test-ssot.md](../runbook/live-test-ssot.md).

## Two stacks

| Stack | Role |
|---|---|
| `tools/live_test` (Python) | One-liner smoke — shield/lab first; tip cursor, lawn gate, deploy |
| Checklists + `scripts/prove-*.ps1` / `smoke-*.ps1` | Full regression (F/C rows, Melon host, effects) until Python has hard-assert parity |

`status.catalog` is Unity CC smoke — **not** StatusRuntime L2 (`prove-status-full.ps1`). `combat.probe` is a single probe — **not** C1–C10 (`prove-overlay-combat.ps1`).

## When to update what

| Change | Also do |
|---|---|
| New/changed debug route or event kind | Update live-test-ssot encyclopedia + scenario matrix or mark checklist-only |
| Clear / bar / absorb semantics | Fix Python scenario asserts + SSOT traps |
| New operator one-liner | Add `scenarios/*.py` with `require` on product fields; register; map PS1 with honest parity |
| Porting a prove script | Only delete PS1 after parity ≈ and SSOT says so |

A matrix row alone is not coverage. Coverage = `Report.require` on the fields that encode the product rule.

## Assert strength

- Event ack → smoke (`check` or require event, soft payload).
- Product fields → `require`.
- Visual / F9 → explicit SKIP.
- No vacuous passes (`len(cues) >= 0`).

## Traps to keep documenting

1. HTTP `{queued:1}` ≠ success — poll events after tip.
2. `afterId=0` + `kinds=` → oldest page, not latest.
3. Shield clear is **per-target**; emit kind `debug.shield.cleared`.
4. Assistant sessions: start server with `Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe`.

## When to add a new pack

Add `overlay.matrix`, `status.l2.*`, `effect.scope.*`, econ/env/tile only when:

1. You can hard-assert product fields (not ack-only), and
2. SSOT matrix + PS1 parity table are updated in the same change.
