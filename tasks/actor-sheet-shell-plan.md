# ActorSheet shell — vertical rail — plan

**Program:** `actor-sheet-shell` · **Index:** [docs/architecture/actor-sheet-map.md](../docs/architecture/actor-sheet-map.md) ·
**Spec:** [docs/architecture/actor-sheet/spec-actor-sheet-shell.md](../docs/architecture/actor-sheet/spec-actor-sheet-shell.md) ·
**Visual:** [docs/design/actor-sheet-shell-rail.html](../docs/design/actor-sheet-shell-rail.html)

## Goal

Refactor ActorSheet **root container** only: left vertical rail (actor summarize + tabs with
expand/collapse) and right tab panel. Reclaim height wasted by the horizontal pill tab row and dual
identity header. Tab bodies stay as-is; each tab gets its own later plan.

## Locked decisions

- Summarize: name · level · role only (collapsed = initial glyph + tooltip).
- Role: commander → `Commander`; creature → `Plant` / `Zombie`.
- Rail toggle: Lucide `PanelLeftClose` / `PanelLeftOpen`.
- Tab icons from `actor-sheet.v1.json` (version 2).
- Persist collapse: `localStorage` `fusionRpg.actorSheet.railCollapsed`.
- `PanelShell` `headerMode="slim"` — no competing name hero.
- Leftover footer policy unchanged.

## Out of scope

Per-tab Lego migrations, leftover/Confirm rules, near-fullscreen bound changes, lawn chrome.
