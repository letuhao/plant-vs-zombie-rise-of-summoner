# Capability map: gui-lego

**Status:** Draft — pending owner review of piece boundaries / payloads. **No React build
authorized until approved.**  
**Program id:** `gui-lego`  
**Ideal:** [gui-lego-ideal.md](gui-lego-ideal.md)  
**Design plates:** [../design/gui-lego/README.md](../design/gui-lego/README.md)  
**Plan/todo:** [tasks/gui-lego-plan.md](../../tasks/gui-lego-plan.md) ·
[tasks/gui-lego-todo.md](../../tasks/gui-lego-todo.md)

Sibling programs (do not absorb):

- [actor-sheet-map.md](actor-sheet-map.md) — sheet shell, cook catalogs, tab kinds  
- [fe-game-foundation.md](fe-game-foundation.md) — DPLP  
- [phaser-kernel-map.md](phaser-kernel-map.md) — canvas runtime (analogue only)  
- [html-design-implementation.md](html-design-implementation.md) — port discipline after acceptance  

---

## What this program is

A **from-scratch game-menu Lego kit**: composable pieces with structure + payload + data flow +
theme slots, assembled by **recipes**, painted by **theme packs**, driven by **pure ViewModel
folds**. First vertical slice = Derived combat console.

Not a new stage. Not a Phaser dependency. Not an incremental CSS fidelity patch on the stop-gap
Derived port.

---

## Ownership splits (binding)

| Concern | Owner | Must not |
|---|---|---|
| Piece contracts + HTML drafts | `gui-lego` | Hard-code element colors in pieces |
| Theme pack shapes + demos | `gui-lego` | Fork a second palette outside `_kit` / `src/theme` |
| Surface recipes | `gui-lego` | God TSX that bypasses recipe |
| Derived cook catalog / `/sheet` join math | `actor-sheet` / Core | Move Hub compose into FE pieces |
| ActorPanel / PanelShell host | `actor-sheet` | Second band-2 shell from this program |
| Phaser island / FX pool | `phaser-kernel` | Menu HTTP inside Phaser |
| React mount of accepted pieces | **Later stream** after owner gate | Start before review pass |

---

## Modules

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| `composition` | Slot grammar, three registries, recipe schema | ideal | [gui-lego/spec-composition.md](gui-lego/spec-composition.md) |
| `derived-surface-vm` | `foldDerivedSurfaceVm` inputs/outputs, Pending, GG-46 | composition, actor-sheet cook | [gui-lego/spec-derived-surface-vm.md](gui-lego/spec-derived-surface-vm.md) |
| `theme-packs` | Taxonomy, css/paint/vfx, demos | composition | [gui-lego/spec-theme-packs.md](gui-lego/spec-theme-packs.md) |
| `piece-*` | Per-piece contracts (see design index) | composition, theme-packs | [gui-lego/spec-*.md](gui-lego/) |

Piece inventory and dependency graph live in the design README (single index). Specs are one file
per piece-id under `gui-lego/`.

---

## Build order (design-only)

```text
1  ideal + map
2  composition + recipes/derived-console.json
3  design README (index, reuse matrix)
4  derived-surface-vm + theme-packs specs
5  per-piece specs + HTML drafts + theme demos
6  tasks handback → owner review
── React implementation is a separate program phase after acceptance ──
```

---

## Explicitly not in this program

| Out | Owner |
|---|---|
| Production React rewrite of Derived | Later stream post-acceptance |
| Changing cook catalog / channel registry | actor-sheet / Core |
| Phaser UIScene for menus | Forbidden (DPLP) |
| All nine IA layers as Lego v1 | Deferred until Derived set accepted |
| CV patch plan as strategy | Rejected |

---

## Locked assumptions

1. First surface = `derived-console` recipe.  
2. Mount host = ActorPanel tab body.  
3. ERM ladder is binding.  
4. Closed Derived bus catalog in the ideal — no ad-hoc events.  
5. Theme packs expose both `css` and `paint`.  
6. Stop-gap `ui/actor/derived/*` remains until replacement stream.

---

## Anti-patterns

- Porting one HTML page as a single React god component without pieces.  
- Tailwind mood-board of the combat console.  
- Offense/Pools as primary rail when cook tabs are SSOT.  
- Wrappers that break CSS `>` (including `display: contents`).  
- Inventing a third channel classification beyond UnitClass + six render states.  
- Hard-coded `--el-*` inside piece drafts.
