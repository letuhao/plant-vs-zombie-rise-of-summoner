# Spec: `kit-tab`

**Module id:** `kit-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Status:** Draft — pending owner review. Supersedes trail
`spec-gear-tab.md` for paper-doll chrome.

---

## Assumptions

1. Paper-doll **role ids** + display words from `actor-sheet.v1.json` as
   `kitRoles[{ roleId, labels: { humanoid, plant } }]`; **weights** stay item `core.v1.json` /
   `ItemRole` — this program does not append a 16th role.
2. Action corpus sealed — ActionSlot row is chrome; regular slots locked-with-reason until corpus.
   **Auras:** one Kit chrome with aura row **above** regular slots (plate 13) — not a separate deep-link.
3. Equipment GET exists; empty doll is honest EmptyState, not fake gear.

---

## Objective

Loadout glance: action slots + 15-role doll + equipped pieces when known.

**Success:** Labels switch by frame vocabulary; empty slots say why; no action corpus reopen.

---

## Tech Stack / Commands / Structure

Reuse battle `ActionSlot` chrome classes if present. `npm test -- --run src/ui/actor/KitTab`.

---

## Design

- Top: aura row (live when present) then ActionSlot row (locked reason string from catalog or fixed
  “corpus not shipped”).
- Doll: iterate `kitRoles`; labels from `labels.humanoid` | `labels.plant` by frame; bind equipment
  GET by roleId.
- Inspector: item card summary when selected (existing item surfaces if available).

---

## Tunables

Role display words in `actor-sheet.v1.json`. Budget ‰ remain item registry.

---

## Testing / Boundaries / Success

- Unit: plant vs humanoid label swap; 15 roles from fixture; Standard/commander-only not forced onto
  body doll unless catalog lists it.
- Always: honest empty; sealed action corpus; aura row above slots.
- Never: invent skills; fork ItemRole enum; Ward-as-shield-tab confusion in copy.
- Success: Plate Kit pane; GearTab empty-state retired.

---

## Open Questions

None — aura row on Kit chrome is locked.
