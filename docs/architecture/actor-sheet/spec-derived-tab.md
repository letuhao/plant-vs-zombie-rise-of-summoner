# Spec: `derived-tab`

**Module id:** `derived-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Ideal bindings:**
[spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) ·
**Status:** Draft — pending owner review. Supersedes trail `spec-derived-stats-tab.md` (doorway).

---

## Assumptions

1. This tab **is** the derived-stat sheet — not a doorway button to another panel.
2. Rows = `derived-stat-catalog` families × live `/api/actors/{id}/derived` snapshot.
3. Six render states only + thirteen `UnitClass` — no third classification.
4. Sparks = **`react-tiny-sparkline`**; icons = **`lucide-react`** via CatalogIcon.
5. `channelLabel` / adapt **must** read catalog `displayName` — never `idWords`.

---

## Objective

Browsable derived combat/status/progression families with StatRow + InspectSplit detail.

**Success:** Zero player-band `channelId` / `idWords`; `CAP` only when registry cap exists;
`status.resist.omni` never paints CAP; Show unchanged toggles defaults.

---

## Tech Stack / Commands / Structure

```text
web/.../ui/actor/DerivedTab.tsx
npm test -- --run src/ui/actor/DerivedTab
```

---

## Design

- Category collapses from catalog `sheetGroup`; **one group open**.
- Combat matrix shape: family rows × element pips (omni first, separated).
- StatRow → inspector: unit sentence, compose sentence, cap or “more still counts”, sources (GG-49).
- Show unchanged persisted per player (`localStorage`).

---

## Tunables

`categoryResistCap` in `derived-stats.v2.json`. Cap paint from registry, not CSS guess.

---

## Testing / Boundaries / Success

- Unit: six states render distinctly; omni uncapped sibling next to capped `.dot`.
- Always: catalog names; matrix shape; GG-64 spark policy; buy-before-build sparks/icons.
- Never: third classification; disabled “open full sheet” doorway; hand-rolled spark instead of
  tiny-sparkline without reason.
- Success: Replaces DerivedStatsTab raw-id list; plate Derived grammar.

---

## Open Questions

UniqueDemon baseline merge on `/derived` is a wiring gap — note Pending if omitted, do not invent.
