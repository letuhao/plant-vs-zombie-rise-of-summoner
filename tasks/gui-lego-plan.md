# gui-lego — plan

**Status:** P0 React **done**; Wave D audit fixes **done**; Wave E Derived chrome trim **done**. Design Waves A–B shipped.  
**Ideal / map / authoring:** [docs/architecture/gui-lego-ideal.md](../docs/architecture/gui-lego-ideal.md) ·
[docs/architecture/gui-lego-map.md](../docs/architecture/gui-lego-map.md) ·
[docs/architecture/gui-lego-authoring.md](../docs/architecture/gui-lego-authoring.md)  
**Queue:** [docs/architecture/gui-lego/menu-refactor-queue.md](../docs/architecture/gui-lego/menu-refactor-queue.md)  
**Design index:** [docs/design/gui-lego/README.md](../docs/design/gui-lego/README.md)  
**Assembled P0:** [docs/design/gui-lego/surfaces/derived-console.html](../docs/design/gui-lego/surfaces/derived-console.html)  
**Todo:** [gui-lego-todo.md](gui-lego-todo.md)

---

## Goal

Make **gui-lego** the durable standard for refactoring player menus. P0 = shared FE runtime +
Derived consumer replacing stop-gap `ui/actor/derived/*` — **shipped**.

---

## Delivered

### Wave A–B (design)

Ideal, map, authoring, recipes, pieces, themes, queue, DESIGN-GATE / decisions.

### Wave 0 + Wave C (React P0)

| Wave | Work | Status |
|---|---|---|
| **0** | registries, `bindSurface` (`$bindArray`, overlays), `RecipeMount`, bus, fixtures | done |
| **C1** | `foldDerivedSurfaceVm` + cook under `features/gui-lego` + `formatMagnitude` | done |
| **C2** | Derived pieces + CatalogIcon + paint gauges | done |
| **C3** | Thin `DerivedTab`; stop-gap deleted | done |
| **C4** | Contract/unit/ActorPanel; e2e SSOT → Lego surface; queue P0 done | done |

### Wave D — P0 audit fixes + coverage

| Fix | Status |
|---|---|
| Unsigned channel/hero totals (`role: total\|delta`) | done |
| Overlay binds → `vm.phasePayload`; empty → dock `phase-empty` | done |
| Retry refetch; availability OR; piece validate; `.contrib` wrap | done |
| Targeted unit/integration tests | done |

### Wave E — Derived chrome trim + chip presentation

| Fix | Status |
|---|---|
| Show unchanged → `button role=switch` | done |
| Drop Derived identity / `.console-hd`; tools on primary cook rail | done |
| Empty dock copy without debug `phase-empty` label | done |
| Variant chip accent + Lucide glyphs + select VFX; resource theme packs | done |

FE homes: `web/fusion-rpg-web/src/features/gui-lego/` · `web/fusion-rpg-web/src/ui/gui-lego/`.  
Theme/recipe JSON under FE are **copies** — re-copy from `docs/design/gui-lego/` when design packs change.

---

## Preview

```powershell
start docs\design\gui-lego\surfaces\derived-console.html
start docs\design\gui-lego\themes\swap-lab.html
```
