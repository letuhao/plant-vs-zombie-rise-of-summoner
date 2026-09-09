# GUI Lego — authoring procedure

**Status: binding for new and refactored player menus.**  
**Ideal:** [gui-lego-ideal.md](gui-lego-ideal.md) · **Map:** [gui-lego-map.md](gui-lego-map.md)  
**Design index:** [../design/gui-lego/README.md](../design/gui-lego/README.md)  
**Queue:** [gui-lego/menu-refactor-queue.md](gui-lego/menu-refactor-queue.md)

This is how Rise of Summoner authors **player menu chrome** (band-2 layer bodies, ActorSheet
tabs, filter/inspect panels). It does not replace GG principles, IA, or ERM — it is how those
rules become composable pieces.

---

## 1. When this applies

| Use gui-lego | Do not use gui-lego |
|---|---|
| New or refactored **player** band-2 layer body | Developer tree / cheats |
| ActorSheet **tab body** | Phaser canvas islands (lawn/world/siege/battle/delve stages) |
| Filter rails, inspect splits, sheet consoles | Forking `PanelShell` / inventing a second band shell |
| Extending shared chrome (`tool-search`, `chip`, `phase-*`) | Growing `DataTable` / `KpiStat` as a parallel kit |

**Hosts stay hosts.** `PanelShell`, `ActorPanel`, `TabList`, Actor ERM ladder (`ActorCard` /
`ActorRow`) remain. Lego fills **slots inside** them.

**HTML draft ports** of an approved plate still follow
[html-design-implementation.md](html-design-implementation.md). Prefer expressing the plate as a
**recipe of pieces** when the surface is a menu (not a one-off marketing plate).

---

## 2. Authoring steps (design → React)

0. **Idea-UI first when the surface is still a shape or a bug list** — run `/idea-ui` (procedure:
   [idea-ui-phase.md](idea-ui-phase.md)). Deliverable is `docs/architecture/<program>-ideal.md`
   with a **bug → module** map. Do not open a “fix the tab CSS” plan. Example:
   [condition-glance-ideal.md](condition-glance-ideal.md).
1. **Find the queue slot** — [menu-refactor-queue.md](gui-lego/menu-refactor-queue.md). New surfaces
   add a row; do not skip the queue.
2. **Reuse before invent** — check [design/gui-lego/README.md](../design/gui-lego/README.md) piece
   index + ERM rung. Missing density ⇒ amend ERM, not a one-off component.
3. **Recipe** — JSON under `docs/design/gui-lego/recipes/<surfaceId>.json` (slots + binds).
4. **Fold contract** — pure VM inputs/outputs (see [spec-derived-surface-vm.md](gui-lego/spec-derived-surface-vm.md)
   as the pattern). Closed **bus catalog** per surface in the ideal or surface spec.
5. **ThemeRefs** — packs from [themes/packs/](../design/gui-lego/themes/packs/); css + paint; no
   hard-coded `--el-*` in pieces.
6. **HTML drafts** — per-piece under `pieces/`; assembled surface under `surfaces/` when the menu
   is reviewable as a whole.
7. **Owner gate** — accept piece boundaries / payloads / assembled look.
8. **React** — registry + fold + mount inside the existing host. Contract tests for landmark/`>`
   DOM. Side-by-side when SSOT-locked.

Do **not** start at step 8. Do **not** skip step 0 when auditing a live menu that “looks wrong.”

---

## 3. Three registries (reminder)

| Registry | Key | Owns |
|---|---|---|
| Piece | `pieceId` | Contract + draft (+ later React factory) |
| Theme | `kind.id` | css / paint / vfx |
| Recipe | `surfaceId` | Slot tree + binds |

A surface = **recipe + fold + bus**. Never a god TSX.

---

## 4. Hard bans

- Piece-level fetch / SignalR  
- SVG `fill="var(--token)"` without a **paint** hex  
- Illicit wrappers under CSS `>` parents  
- Private density ladder beside ERM  
- Phaser owning menu HTTP  
- Expanding the whole IA in one stream before P0 Derived acceptance for React  

---

## 5. Related

- Payload fragments: [gui-lego/payload-types.md](gui-lego/payload-types.md)  
- Composition: [gui-lego/spec-composition.md](gui-lego/spec-composition.md)  
- Decision: `decisions.md` **GUI Lego (menu composition)**  
- DESIGN-GATE: UI row + **Player menus** row  
