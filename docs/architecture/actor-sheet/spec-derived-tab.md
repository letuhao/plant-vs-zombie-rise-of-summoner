# Spec: `derived-tab`

**Module id:** `derived-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Harden:** [../derived-cook-map.md](../derived-cook-map.md)  
**Depends on:** `actor-sheet-shell` · **Ideal bindings:**
[spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) ·
**Composition kit:** [gui-lego-map.md](../gui-lego-map.md) ·
**Status:** Structure Implemented (P0 React). **Harden in progress** — wire truth / six states / paint /
player copy per derived-cook (specs strengthened 2026-09-10).

---

## Assumptions

1. This tab **is** the derived-stat sheet — not a doorway button to another panel.
2. **Family catalog ≠ channel registry.** `derived-stat-catalog.v2.json` lists **families**;
   Core `DerivedStatRegistry` holds the live channel set. Cook: `GET /api/catalogs/derived-surface`.
3. **Expand × join is the contract.** Fold joins cook families to sheet channels; six render states —
   prefer wire `renderState` (**D2**).
4. Sheet channel DTO carries `unitClass` / `cap` / `defaultValue` / `renderState` after
   `derived-sheet-projection`.
5. Icons = **`lucide-react`** via CatalogIcon + `glyphRef`.
6. **Surface = recipe + fold + bus.** Thin `DerivedTab` only.
7. UniqueDemon baseline omit on lean `/derived` → **Pending** (closed via projection module) — never invent.

---

## Objective

Browsable derived combat/status/progression families as the **gui-lego derived-console**, covering
the expanded channel surface `/sheet` returns (preferred).

**Success:** Zero player-band `channelId` / `GG-49`; cook primary rail only; join never drops omni;
`CAP` only from wire; Show-unchanged = **default only** (**D4**); Shared from BE (**D3**);
`:scope > .inspect-split` landmark.

---

## Tech Stack / Commands / Structure

```text
web/.../ui/actor/DerivedTab.tsx
web/.../features/gui-lego/           # fold, cook/, bind
web/.../ui/gui-lego/                 # RecipeMount, pieces, recipes
docs/design/gui-lego/surfaces/derived-console.html
npm test -- --run src/ui/actor/DerivedTab src/ui/actor/DerivedCombatConsole.contract src/features/gui-lego
```

---

## Design

Visual SSOT: [derived-console.html](../../design/gui-lego/surfaces/derived-console.html).  
FE path: `foldDerivedSurfaceVm` → `bindSurface(derived-console)` → `RecipeMount`.  
Closed bus: search | showUnchanged | tab | variant | channel.select | retry.

Wire fields: see [spec-derived-sheet-projection.md](../derived-cook/spec-derived-sheet-projection.md).

---

## Tunables

`categoryResistCap` in `derived-stats.v2.json`. Cap paint from wire/registry, not CSS guess.

---

## Testing / Boundaries / Success

- Unit: fold + bind + RecipeMount.
- Contract: cook IA; inspect-split; no player jargon.
- Always: six states; element-paint-ssot; no god console.
- Never: third classification; FE CAP literals; invent UniqueDemon baseline.

## Open Questions

**Closed 2026-09-10:** UniqueDemon baseline → Pending when omitted (`derived-sheet-projection`).  
**D7** spark/pips deferred — not tab Done gate.
