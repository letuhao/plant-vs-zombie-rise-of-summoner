# Spec: `condition-tab`

**Module id:** `condition-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Status:** Draft — pending owner review.

---

## Assumptions

1. Resource **ids and labels** come from `resource-catalog` (includes `poise`). FE must not use the
   five-string `ResourceId` union as a roster.
2. Live pool values may be pending until `ActorView` gains resources — show honest empty / pending,
   not fake meters (wiring gap, not a wall).
3. Standing five-axis vector is **owned by this tab** — Pending/lifecycle until a server producer
   exists; do not invent values and do not reuse `channelSummary` as fake Standing.
4. Level is shown in shell identity, while **current progression status** (level + current XP +
   honest pending `xpToNext` gauge) lives on Condition — not a separate Progression tab.
5. Meters/radials use **`recharts`**; glyphs use **`lucide-react`** + GG-58 (map tech stack).

---

## Objective

First-paint **Condition** glance: progression gauge, HP radial with shield overlay, one meter per
resource-catalog row, Standing if present (else Pending), live status glyphs from status-catalog.

**Success:** Opening Condition never prints a dotted channel id; every catalog resource has a meter
slot; plant `hunger` label resolves to **Sun** from catalog; no five-string `ResourceId` roster.

---

## Tech Stack / Commands / Structure

Shared shell widgets + `recharts` radial/radar/progress primitives + `lucide-react` StatusGlyph.
Tests:
`npm test -- --run src/ui/actor/ConditionTab`.

```text
web/.../ui/actor/ConditionTab.tsx
```

---

## Design

- **Glance grammar, not InspectSplit:** plate-13 `cond-hero` + `stand-row`; no dock/aside root.
- Progression gauge first: level chip + `xp` + `xpToNext` + fill when known; otherwise honest pending
  bar and text.
- HP radial + nested shield radial (plate 13) via recharts.
- Vertical pool column: iterate `surface.resources` (faction label by actor side).
- Standing block: five-axis radar + bars when producer exists; else lifecycle/pending (this tab owns it).
- Status glyph strip: live instances ∩ catalog; `StatusGlyph` uses lucide/`hudToken`/`color`.
- Clicking a status glyph routes to the Status tab; selecting a pool updates local Condition emphasis
  only — not a second inspector column.

**Sun bank (`pvz.*`) never appears** — only actor `hunger`.

---

## Tunables

None new. Colors/icons from catalogs. Shield max stacks remain shield policy.

---

## Testing / Boundaries / Success

- Unit: six meters from fixture catalog including `poise`; Sun label for plant hunger.
- Unit: progression gauge renders `xp`; keeps `xpToNext` honest pending when wire is missing.
- Always: catalog iteration; GG-64 gauges first.
- Never: hardcoded five resources; lawn sun bank meter.
- Never: duplicate full identity strip inside Condition.
- Success: Condition matches plate grammar; pending fields use lifecycle/pending, not invented numbers.

---

## Open Questions

Backend seam: extend `GET /api/actors/{id}/sheet` for progression/current-state fields rather than
enriching thin `ActorView` with ad-hoc sheet-only joins.
