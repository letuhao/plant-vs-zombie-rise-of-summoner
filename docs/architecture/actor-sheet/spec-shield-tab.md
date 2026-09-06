# Spec: `shield-tab`

**Module id:** `shield-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Status:** Draft — pending owner review.

---

## Assumptions

1. Noun is **Shield**, never Ward (paper-doll / aptitude vocabulary).
2. Max three layers — `ShieldPolicy` / shield tuning; empty wells dashed.
3. Player `GET /api/actors/{id}/shields` may be missing today — PendingNote until wired; do not invent
   stacks in the FE.
4. Full 28×7 shield matrix stays under Derived → Shield group; this tab is instance radials + omni
   shield StatRows.

---

## Objective

Visual shield stack readout: up to three coloured radials + omni shield family StatRows.

**Success:** Empty wells visible; element color from element-catalog; no paragraph wall of text.

---

## Tech Stack / Commands / Structure

`ShieldLayer` via **`recharts`** RadialBar from shell kit. `npm test -- --run src/ui/actor/ShieldTab`.

---

## Design

- Three wells in drain priority order when live.
- Inspector: layer hp/max/element + catalog reading for omni shield channels.
- F9 / lawn mute is HUD-only — sheet always shows numbers when data exists.

---

## Tunables

Shield max / priorities — existing shield tuning. No new keys.

---

## Testing / Boundaries / Success

- Unit: 0–3 layers; dashed empty; Ward string absent from copy.
- Always: GG-64 radials; catalog colors.
- Never: fake caps on uncapped magnitudes; Ward noun.
- Success: Matches plate 13 Shield pane.

---

## Open Questions

Server shields GET shape — follow Core `GetShields` / Totals when wiring.
