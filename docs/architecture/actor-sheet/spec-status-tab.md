# Spec: `status-tab`

**Module id:** `status-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Status:** Draft — pending owner review.

---

## Assumptions

1. Roster + names + `hudToken`/`color` from `status-catalog`.
2. Segs: live / catalog / mastery (mastery = player-lifetime, not live stack) — mastery may Pending.
3. Same glyphs as Condition strip and Band B HUD (shared `StatusGlyph` / catalog).

---

## Objective

Status identity grid with authored glyphs and inspector readings.

**Success:** No id-sliced initials; every catalog status has a glyph slot; live ones marked.

---

## Tech Stack / Commands / Structure

`StatusGlyph` + CatalogIcon (**`lucide-react`** + GG-58). `npm test -- --run src/ui/actor/StatusTab`.

---

## Design

- Glyph grid from catalog ordinal.
- Live overlay badge when instance present on actor.
- Inspector: displayName + reading; no engine `statusId` on player band (dev tree may show id).

---

## Tunables

None. Policy numbers stay in `status.v1.json`.

---

## Testing / Boundaries / Success

- Unit: fixture catalog renders hudToken, not `StatusInitials`.
- Always: GG-62 / GG-58 fallback.
- Never: hashed RGB; hardcoded 24 ids in FE.
- Success: Plate Status pane grammar.

---

## Open Questions

Mastery data source — Pending until a lifetime mastery API exists.
