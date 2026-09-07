# Spec: `derived-tab`

**Module id:** `derived-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Ideal bindings:**
[spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) ·
**Status:** Draft — pending owner review. Supersedes trail `spec-derived-stats-tab.md` (doorway).

---

## Assumptions

1. This tab **is** the derived-stat sheet — not a doorway button to another panel.
2. **Family catalog ≠ channel registry.** `derived-stat-catalog.v{n}.json` lists **families**
   (presentation rows: axis, compose, unitClass, displayName, expand policy). Core
   `DerivedStatRegistry` holds the live channel set — **268** registered today
   (`SeedCatalogTests.CatalogResolves268`). The sheet never claims "backend cannot cover 200+"
   because the catalog is ~30 families; that count is expected.
3. **Expand × join is the contract.** Combat (and other expand) families expand over the injected
   element axis the same way the registry generator does (omni + concrete elements). The FE joins
   expanded catalog channel ids to `GET /api/actors/{id}/sheet` (preferred for InspectSplit) or lean
   `GET /api/actors/{id}/derived` — both ship contributions + `composeKind`. Sparse /
   missing live values use the six render states (`no-producer`, unchanged, …) — they do not invent
   magnitudes.
4. Six render states only + closed `UnitClass` ledger — no third classification.
5. Sparks = **`react-tiny-sparkline`**; icons = **`lucide-react`** via CatalogIcon.
6. `channelLabel` / adapt **must** read catalog `displayName` — never `idWords`.

→ Correct these now or this spec proceeds as written.

---

## Objective

Browsable derived combat/status/progression families with StatRow + InspectSplit detail, covering
the **expanded** channel surface that `/derived` already returns — not a truncated toy list.

**Success:** Zero player-band `channelId` / `idWords`; expand produces the same id set shape the
registry uses for element-axis families; join never drops omni; `CAP` only when registry cap exists;
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
- Combat matrix shape: family rows × element pips (**omni first, separated**).
- Expand helper: `family + elementAxis → channelId[]` must match Core expand rules (same suffixes /
  omni treatment as registry). Resource-touching families must cover **all** resource-catalog ids
  (DESIGN-GATE resources row).
- Join: for each expanded id, look up live `Channels[id]`; render state from
  `spec-derived-stat-sheet` §3 — never invent a value because a family row exists.
- StatRow → inspector: unit sentence, compose sentence, cap or “more still counts”, sources (GG-49).
- Show unchanged persisted per player (`localStorage`).

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
