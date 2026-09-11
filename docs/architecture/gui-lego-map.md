# Capability map: gui-lego

**Status:** **Binding design standard** for player menus (2026-09-09 harden). Piece/recipe
contracts hardened — **React still blocked** until owner accepts P0 assembled surface + payloads.  
**Program id:** `gui-lego`  
**Ideal:** [gui-lego-ideal.md](gui-lego-ideal.md)  
**Authoring:** [gui-lego-authoring.md](gui-lego-authoring.md)  
**Queue:** [gui-lego/menu-refactor-queue.md](gui-lego/menu-refactor-queue.md)  
**Design plates:** [../design/gui-lego/README.md](../design/gui-lego/README.md)  
**Assembled P0:** [../design/gui-lego/surfaces/derived-console.html](../design/gui-lego/surfaces/derived-console.html)  
**Plan/todo:** [tasks/gui-lego-plan.md](../../tasks/gui-lego-plan.md) ·
[tasks/gui-lego-todo.md](../../tasks/gui-lego-todo.md)  
**Decision:** `decisions.md` **GUI Lego — menu composition** · DESIGN-GATE **Player menus** row

Sibling programs (do not absorb):

- [actor-sheet-map.md](actor-sheet-map.md) — sheet shell, cook catalogs, tab kinds  
- [condition-glance-map.md](condition-glance-map.md) — Condition glance (P1)  
- [derived-cook-map.md](derived-cook-map.md) — Derived cook harden (P0 Harden)  
- [shield-sheet-map.md](shield-sheet-map.md) — Shield tab + stack projection (P1b)  
- [fe-game-foundation.md](fe-game-foundation.md) — DPLP  
- [phaser-kernel-map.md](phaser-kernel-map.md) — canvas runtime (analogue only)  
- [html-design-implementation.md](html-design-implementation.md) — plate fidelity when SSOT is a plate  

---

## What this program is

The **repo standard for composing player menus**: pieces + recipes + theme packs + pure ViewModel
folds. First vertical slice = Derived combat console (`derived-console`). Later surfaces follow the
[menu-refactor-queue](gui-lego/menu-refactor-queue.md).

Not a new stage. Not a Phaser dependency. Not an incremental CSS fidelity patch on the stop-gap
Derived port.

---

## Ownership splits (binding)

| Concern | Owner | Must not |
|---|---|---|
| Piece contracts + HTML drafts | `gui-lego` | Hard-code element colors in pieces |
| Theme pack shapes + demos | `gui-lego` | Fork a second palette outside `_kit` / `src/theme` |
| Surface recipes | `gui-lego` | God TSX that bypasses recipe |
| Menu refactor queue | `gui-lego` | Skip queue / boil all layers at once |
| Derived cook catalog / `/sheet` join math | `actor-sheet` / Core | Move Hub compose into FE pieces |
| ActorPanel / PanelShell host | `actor-sheet` | Second band-2 shell from this program |
| Phaser island / FX pool | `phaser-kernel` | Menu HTTP inside Phaser |
| React mount of accepted pieces | **Later stream** after owner gate | Start before review pass |

---

## Modules

| Module id | Responsibility | Depends on | Spec / artifact |
|---|---|---|---|
| `authoring` | Binding procedure for new/refactored menus | ideal | [gui-lego-authoring.md](gui-lego-authoring.md) |
| `composition` | Slot grammar, three registries, recipe schema | ideal | [gui-lego/spec-composition.md](gui-lego/spec-composition.md) |
| `payload-types` | Shared Phase / ThemeRef / MagnitudeDisplay | composition | [gui-lego/payload-types.md](gui-lego/payload-types.md) |
| `derived-surface-vm` | `foldDerivedSurfaceVm` inputs/outputs, Pending, GG-46 | composition, actor-sheet cook | [gui-lego/spec-derived-surface-vm.md](gui-lego/spec-derived-surface-vm.md) |
| `theme-packs` | Taxonomy, css/paint/vfx, full element/status/side packs | composition | [gui-lego/spec-theme-packs.md](gui-lego/spec-theme-packs.md) |
| `menu-refactor-queue` | Ordered surface migration for the whole FE | authoring | [gui-lego/menu-refactor-queue.md](gui-lego/menu-refactor-queue.md) |
| `assembled-surfaces` | Whole-menu HTML reviews (`surfaces/`) | pieces, recipes | [design/gui-lego/surfaces/](../design/gui-lego/surfaces/) |
| `piece-*` | Per-piece contracts | composition, theme-packs, payload-types | [gui-lego/spec-*.md](gui-lego/) |

Piece inventory and dependency graph: [design/gui-lego/README.md](../design/gui-lego/README.md).

---

## Build order

```text
Wave A (done)   ideal + map + composition + VM + themes + piece drafts
Wave B (done)   harden: binding docs, fixed specs, assembled surface, queue, full packs
── Owner accept P0 ──
Wave C          React Derived mount (separate stream)
Wave D+         Queue P1 Condition → P2 Creatures → …
```

---

## Explicitly not in this program

| Out | Owner |
|---|---|
| Production React rewrite of Derived | Later stream post-acceptance |
| Changing cook catalog / channel registry | actor-sheet / Core |
| Phaser UIScene for menus | Forbidden (DPLP) |
| All rail layers in one stream | Queue — one surface at a time |
| CV patch plan as strategy | Rejected |

---

## Locked assumptions

1. First surface = `derived-console` recipe.  
2. Mount host = ActorPanel tab body.  
3. ERM ladder is binding.  
4. Closed Derived bus catalog in the ideal — no ad-hoc events.  
5. Theme packs expose both `css` and `paint`.  
6. Stop-gap `ui/actor/derived/*` remains until replacement stream.  
7. New player menus follow authoring procedure + queue.

---

## Anti-patterns

- Porting one HTML page as a single React god component without pieces.  
- Tailwind mood-board of the combat console.  
- Offense/Pools as primary rail when cook tabs are SSOT.  
- Wrappers that break CSS `>` (including `display: contents`).  
- Inventing a third channel classification beyond UnitClass + six render states.  
- Hard-coded `--el-*` inside piece drafts.  
- Skipping the menu-refactor queue.
