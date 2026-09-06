# Spec: `actor-collection`

**Module id:** `actor-collection` · **Program:** [lawn-interactive-map.md](../lawn-interactive-map.md) ·
**Design landing:** [spec-lawn-interactive.md](../../design/spec-lawn-interactive.md) §4 ·
**Status:** Draft — pending owner review. **No build authorized until approved.**

**Depends on:** — · **Blocks:** `lawn-match-hud` (chips), `spawn-tray`, `cell-occupancy-dock`,
Creatures / scope-picker consumers.

---

## Assumptions

1. This is a **new shared widget**, not a lift of `VirtualCreatureList` / CreaturesLayer internals.
   CreaturesLayer becomes a **consumer**.
2. Densities are existing rungs only: **Row** (list) and **Card** (grid). No third card shape.
3. Volume policy (GG-50) is declared **once** here: ≤24 all, 25–240 window/virtualize, >240
   search-first. Dock must not restate cutoffs.
4. Query state (GG-51) survives close/reopen of a dock within the stage session; cell source may
   reset query when the **cell** changes.
5. Rows may lack `instanceId` (generals). The widget never invents one; selection payloads come from
   the adapter / source.

→ Correct these now or this spec proceeds as written.

---

## Objective

One Actor Row/Card list with density, volume, and query so Creatures, cell occupancy dock, spawn
tray, and the scope picker never grow a private list copy (GG-9).

**Users:** Mid-wave lawn players and roster screens that already need creature browsing.

**Success:** Four consumers render the same component; cell stacks ≤24 do not virtualize away rows;
spawn tray obeys 24/240; no `typeId` chrome; no second card design.

---

## Tech Stack

| Layer | Choice |
|---|---|
| FE | React in `web/fusion-rpg-web` — compose existing `ActorRow` / `ActorCard` |
| Icons | `lucide-react` via CatalogIcon / actor chrome (buy-before-build) |
| No Phaser | List chrome is React only |

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/actor/ActorCollection
npm run build
```

---

## Project Structure

```text
web/.../ui/actor/ActorCollection.tsx       # widget
web/.../ui/actor/ActorCollection*.test.tsx
# Consumers (later modules / layers — do not fork lists):
#   CreaturesLayer, CellOccupancyDock, SpawnTray, ActorListPickerPanel body
```

---

## Design

### Props (conceptual)

`source`, `density: list | grid`, `query` (search, side, sort), `page` / window, `selection`,
`onSelect`, `empty`, `lockedReason`, optional `showHpSliver` (dock on; Creatures Cold off).

### Sources (same chrome)

| Source | Rows | Select |
|---|---|---|
| Cell | Living occupants via `lawn-occupant-adapt` | Open ActorSheet / dock selection |
| Roster deployable | Uniques legal to field | Enter SpawnTargeting |
| Creatures | Full roster | Open ActorSheet Cold |
| Scope picker | Candidates | Set picker value |

### Mixed Fielded / Wave

Chip on every row: **Fielded** (unique Bound) vs **Wave** (general). Default sort: fielded first,
then plants, then zombies — overridable.

### Four states (GG-17)

Loading / Empty / Error / Locked — locked controls carry reason (GG-55). Empty collection ≠ hide
chrome.

---

## Tunables

Presentation only (not Core Policy): page size inside the 25–240 window. Cutoffs 24 / 240 are
**structural volume policy** — comment if kept as constants; do not invent a Core cap.

---

## Testing Strategy

| Level | What |
|---|---|
| Unit | ≤24 renders all; 25 triggers window; >240 search-first empty grid until query |
| Unit | Density toggles Row ↔ Card without remounting selection id |
| Consumer smoke (later) | Creatures + dock share one import path |

---

## Boundaries

- **Always:** One implementation; GG-50/51; Fielded/Wave chips; consume adapter for lawn rows.
- **Ask first:** A third density; changing 24/240 cutoffs; Fusion/Expedition as v1 consumers.
- **Never:** Copy CreaturesLayer and call GG-9 done; feed raw `LawnViewModel` without adapt;
  `typeId` typing; put `ptr` or general ids in `?sel=`.

---

## Success Criteria

- [ ] Widget exported once; Creatures consumes it (no private list)
- [ ] Volume policy tested at the three magnitude bands
- [ ] Dock and spawn tray can pass distinct sources without forking UI

---

## Open Questions

None for this module. Commander-list / Pacts as consumers stay out of v1 (design landing §13).
