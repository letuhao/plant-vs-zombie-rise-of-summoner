# Spec: `condition-tab`

**Module id:** `condition-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Status:** Draft — pending owner review.

---

## Assumptions

1. Resource **ids and labels** come from `resource-catalog` (includes `poise`). FE must not use the
   five-string `ResourceId` union as a roster.
2. Live pool values may be pending until `ActorView` gains resources — show honest empty / pending,
   not fake meters (wiring gap, not a wall).
3. Standing five-axis vector is **owned by this tab** — PendingNote until a server producer exists;
   do not invent values.
4. Level lives in the sheet header; **XP count** (and honest pending `xpToNext`) live on Condition —
   not a separate Progression tab in the catalog-era sheet.
5. Meters/radials use **`recharts`**; glyphs use **`lucide-react`** + GG-58 (map tech stack).

---

## Objective

First-paint **Condition** glance: HP radial with shield overlay, one meter per resource-catalog row,
Standing if present (else Pending), live status glyphs from status-catalog, XP readout (count +
pending `xpToNext` when known).

**Success:** Opening Condition never prints a dotted channel id; every catalog resource has a meter
slot; plant `hunger` label resolves to **Sun** from catalog; no five-string `ResourceId` roster.

---

## Tech Stack / Commands / Structure

Shared shell widgets + `recharts` RadialBar / pool meters + `lucide-react` StatusGlyph. Tests:
`npm test -- --run src/ui/actor/ConditionTab`.

```text
web/.../ui/actor/ConditionTab.tsx
```

---

## Design

- HP radial + nested shield radial (plate 13) via recharts.
- Vertical pool column: iterate `surface.resources` (faction label by actor side).
- XP row: real `xp` count; progress only when `xpToNext` is known — else PendingNote.
- Standing block: five-axis when producer exists; else PendingNote (this tab owns it).
- Status glyph strip: live instances ∩ catalog; `StatusGlyph` uses lucide/`hudToken`/`color`.
- Inspector: selected pool, status, Standing axis, or XP reading from catalog / honest pending.

**Sun bank (`pvz.*`) never appears** — only actor `hunger`.

---

## Tunables

None new. Colors/icons from catalogs. Shield max stacks remain shield policy.

---

## Testing / Boundaries / Success

- Unit: six meters from fixture catalog including `poise`; Sun label for plant hunger.
- Always: catalog iteration; GG-64 gauges first.
- Never: hardcoded five resources; lawn sun bank meter.
- Success: Condition matches plate grammar; pending fields use `PendingNote`, not invented numbers.

---

## Open Questions

None blocking. Live HP wiring is a separate adapter task.
