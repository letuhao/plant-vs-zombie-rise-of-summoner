# Spec: `derived-tab`

**Module id:** `derived-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Ideal bindings:**
[spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) ·
**Composition kit:** [gui-lego-map.md](../gui-lego-map.md) ·
**Status:** Implemented structure (P0 React — fold → bind → RecipeMount).  
**Harden:** [../derived-cook-map.md](../derived-cook-map.md) — six states / CAP wire / paint SSOT / player copy (specs written 2026-09-10).

---

## Assumptions

1. This tab **is** the derived-stat sheet — not a doorway button to another panel.
2. **Family catalog ≠ channel registry.** `derived-stat-catalog.v2.json` lists **families**
   (presentation rows: expand, sheetGroup, compose, unitClass, locale displayName). Core
   `DerivedStatRegistry` holds the live channel set. Tabbed cook:
   `GET /api/catalogs/derived-surface`.
3. **Expand × join is the contract.** The fold joins cook families to sheet/lean channels; sparse
   values use the six render states — never invent magnitudes.
4. Six render states only + closed `UnitClass` ledger — magnitudes via `formatMagnitude` / fold
   adapter (`formatDerivedMagnitude`).
5. Icons = **`lucide-react`** via CatalogIcon + `glyphRef` (never `idWords`).
6. **Surface = recipe + fold + bus.** Host is a thin `DerivedTab`; tree comes from
   `derived-console` recipe + piece registry — not a god TSX console.

→ Correct these now or this spec proceeds as written.

---

## Objective

Browsable derived combat/status/progression families as the **gui-lego derived-console**, covering
the expanded channel surface `/sheet` (preferred) or `/derived` returns.

**Success:** Zero player-band `channelId` / `idWords`; cook primary rail only; join never drops
omni; `CAP` only when registry cap exists; Show unchanged toggles defaults; left|right inspect
split matches the Lego landmark tree (`:scope > .inspect-split`).

---

## Tech Stack / Commands / Structure

```text
web/.../ui/actor/DerivedTab.tsx              # thin host: hooks → fold → bind → RecipeMount
web/.../features/gui-lego/                   # types, registries, bindSurface, fold, cook/
web/.../ui/gui-lego/                         # RecipeMount, pieces/, recipes/, derivedConsole.css
docs/design/gui-lego/surfaces/derived-console.html   # visual SSOT
npm test -- --run src/ui/actor/DerivedTab src/ui/actor/DerivedCombatConsole.contract src/features/gui-lego
```

Port process: [html-design-implementation.md](../html-design-implementation.md).
Authoring: [gui-lego-authoring.md](../gui-lego-authoring.md).

---

## Design

**Visual SSOT (Derived pane):**
[derived-console.html](../../design/gui-lego/surfaces/derived-console.html)
(gui-lego assembled surface). Pre-Lego peer:
[derived-combat-console.html](../../design/derived-combat-console.html) (history).

**CSS:** `ui/gui-lego/derivedConsole.css` under `.derived-combat-console` / `.console`. Landmark
class names closed; contract asserts `:scope > .inspect-split` (no `display:contents`). Dual
identity: ActorPanel header + `identity-hd` inside the console — intentional.

**FE path:** `foldDerivedSurfaceVm` → `bindSurface(derived-console)` → `RecipeMount`. Closed bus:
`derived.search.set` | `showUnchanged.set` | `tab.set` | `variant.set` | `channel.select` | `retry`.

- Cook primary tabs (Elements / Status / Resources / Other) + variant rail.
- Magnitudes: fold owns `valueText` via `formatDerivedMagnitude` → `formatMagnitude`.
- Gauges: SVG / stack bars use theme **paint hex**, never `fill="var(--…)"`.

---

## Tunables

`categoryResistCap` in `derived-stats.v2.json`. Cap paint from registry, not CSS guess.

---

## Testing / Boundaries / Success

- Unit: fold + bind (`$bindArray`, overlays) + RecipeMount `>` DOM.
- Contract: cook IA; `:scope > .inspect-split`; no `.contents`.
- E2e SSOT side-by-side targets Lego assembled HTML.
- Never: third classification; god console under `ui/actor/derived/*`; private magnitude formatters.

---

## Open Questions

UniqueDemon baseline merge on `/derived` is a wiring gap — note Pending if omitted, do not invent.
