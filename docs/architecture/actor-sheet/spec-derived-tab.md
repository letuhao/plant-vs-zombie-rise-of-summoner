# Spec: `derived-tab`

**Module id:** `derived-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Ideal bindings:**
[spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) ·
**Status:** Draft — pending owner review. Supersedes trail `spec-derived-stats-tab.md` (doorway).

---

## Assumptions

1. This tab **is** the derived-stat sheet — not a doorway button to another panel.
2. **Family catalog ≠ channel registry.** `derived-stat-catalog.v2.json` lists **families**
   (presentation rows: expand, sheetGroup, compose, unitClass, locale displayName). Core
   `DerivedStatRegistry` holds the live channel set — **269** registered today
   (`SeedCatalogTests.CatalogResolves269`). The sheet never claims "backend cannot cover 200+"
   because the catalog is ~50 families; that count is expected. Tabbed cook:
   `GET /api/catalogs/derived-surface` ([actor-sheet-derived-plan.md](../../../tasks/actor-sheet-derived-plan.md)).
3. **Expand × join is the contract.** Element / status-category / resource / action-category
   families expand over their variant axes. The FE joins catalog `channelPattern` ids to
   `GET /api/actors/{id}/sheet` (preferred) or lean `GET /api/actors/{id}/derived` — both ship
   contributions + `composeKind`. Sparse / missing live values use the six render states
   (`no-producer`, unchanged, …) — they do not invent magnitudes. Status expand is **category
   slots** (omni/dot/cc/contagion), not status-catalog × family.
4. Six render states only + closed `UnitClass` ledger — no third classification.
5. Icons = **`lucide-react`** via CatalogIcon in the draft `.glyph` slot. Contribution gauges follow
   the HTML SSOT (`.share-donut` / `.stack-row` / `.sources`) — not plate-13 StatRow sparks.
6. `channelLabel` / adapt **must** read catalog `displayName` — never `idWords`.

→ Correct these now or this spec proceeds as written.

---

## Objective

Browsable derived combat/status/progression families as the **combat console** (HTML SSOT), covering
the **expanded** channel surface that `/sheet` (preferred) or `/derived` returns — not a truncated
toy list.

**Success:** Zero player-band `channelId` / `idWords`; expand produces the same id set shape the
registry uses for element-axis families; join never drops omni; `CAP` only when registry cap exists;
`status.resist.omni` never paints CAP; Show unchanged toggles defaults; left|right inspect split
matches the HTML landmark tree.

---

## Tech Stack / Commands / Structure

```text
web/.../ui/actor/DerivedTab.tsx                 # compose only
web/.../ui/actor/derived/derivedCook.ts         # pure join/expand/state
web/.../ui/actor/derived/DerivedCombatConsole.tsx
web/.../ui/actor/derived/DerivedChannelRow.tsx
web/.../ui/actor/derived/DerivedInspector.tsx
web/.../ui/actor/derived/derivedGauges.tsx
web/.../ui/actor/DerivedCombatConsole.css
tasks/actor-sheet-derived-structure.md          # landmark + component contract
npm test -- --run src/ui/actor/DerivedTab src/ui/actor/DerivedCombatConsole.contract
```

Port process: [html-design-implementation.md](../html-design-implementation.md).

---

## Design

**Visual SSOT (Derived pane):** [derived-combat-console.html](../../design/derived-combat-console.html)
— combat-scientist console bar from [modern-stat-hud.md](../../design/modern-stat-hud.md).
Plate [13-actor-sheet.html](../../design/13-actor-sheet.html) remains shell/history; do not port its
flat Derived utility look as the target. **Do not** wrap this pane in React `InspectSplit` /
`StatRow` — those are plate-13 kit for other tabs.

**CSS sync:** Edit the HTML draft first, then port `<style>` into `DerivedCombatConsole.css` scoped
under `.derived-combat-console`. Landmark class names are closed (see
[actor-sheet-derived-structure.md](../../../tasks/actor-sheet-derived-structure.md)). Contract unit
asserts `:scope > .inspect-split` (no intervening wrapper). Intentional embed delta: console height
`min(640px, 72vh)` vs standalone HTML page height.

**FE structure:** cook primary tabs (Elements / Status / Resources / Other) + **variant sub-tabs**;
list is **one channel per family** for the selected variant; sheetGroups are **section headers**
inside the active cook tab — never the primary rail. Inspect pane: big value, reading,
compose/unit/join sentences, contribution donut+stack, GG-49 sources, soft-cap note.

- Expand helper: `family + variant → channelId` must match Core expand rules. Resource-touching
  families cover **all** resource-catalog ids.
- Join: look up live sheet/`Channels[id]`; render state from `spec-derived-stat-sheet` §3 — never
  invent a magnitude because a family row exists.

---

## Tunables

`categoryResistCap` in `derived-stats.v2.json`. Cap paint from registry, not CSS guess. Family
membership / displayNames live in `derived-stat-catalog` only.

---

## Testing / Boundaries / Success

- Unit: six states render distinctly; omni uncapped sibling next to capped `.dot`.
- Unit: expand fixture for one combat family yields omni + every concrete element id; join against
  a 268-shaped snapshot leaves no silent hole for authored expand ids (missing → `no-producer` /
  empty state, not omit-without-trace).
- Always: catalog names; matrix shape; GG-64 spark policy; buy-before-build sparks/icons.
- Never: third classification; disabled “open full sheet” doorway; hand-rolled spark instead of
  tiny-sparkline without reason; treat family count as channel count; truncate the matrix to “a
  few” channels for fear of 268.
- Success: Replaces DerivedStatsTab raw-id list; plate Derived grammar; expand/join locked.

---

## Open Questions

UniqueDemon baseline merge on `/derived` is a wiring gap — note Pending if omitted, do not invent.
