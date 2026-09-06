# Spec: `elements-tab`

**Module id:** `elements-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Status:** Draft — pending owner review.

---

## Assumptions

1. Concrete elements + presentation from `element-catalog`. Omni is baseline, **not** a seventh type
   chip on the actor.
2. Actor’s two concrete types from DemonProfile / UniqueActor.
3. Mastery StatRows from derived-stat-catalog element-expanded families where present.

---

## Objective

Element typing glance: coloured radials for concrete roster + mastery rows; profile types highlighted.

**Success:** Omni not offered as a type; colors from catalog; names authored.

---

## Tech Stack / Commands / Structure

**`recharts`** radials + StatRow sparks. `npm test -- --run src/ui/actor/ElementsTab`.

---

## Design

- Six (or catalog count) coloured radials; active types emphasized.
- Mastery StatRows below; inspector explains matchup at leisure (GG-60 for lawn — full text here).
- Do not re-author element matrix values here — read existing element hub.

---

## Tunables

None new. Matrix values stay in existing element/combat tuning.

---

## Testing / Boundaries / Success

- Unit: omni absent from type picker chips; catalog colors applied.
- Always: catalog iteration.
- Never: hardcoded six-element union once catalog injects; seventh type chip for omni.
- Success: Plate Elements pane.

---

## Open Questions

None.
