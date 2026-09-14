# Spec: `aptitudes-tab`

> **Superseded for UniqueActor allocate (2026-09-10).** Player Aptitudes delivery is now
> [aptitude-sheet-map.md](../aptitude-sheet-map.md): UniqueActor → UniqueCreature (Mode A), commander
> role → Mode C, shared species Mode B. Do **not** implement “v1 = commander only” from this file.
> Historical actor-sheet shell notes below may still inform GG-63 footer wiring until
> `host-role-gate` lands.

**Module id:** `aptitudes-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Status:** **Superseded** by aptitude-sheet (see banner).

---

## Assumptions

1. Tiles come from `aptitude-catalog` (today twelve). Posture is a **read** grouping, not storage.
2. ~~**v1 allocate = commander scope**~~ **Overturned** — see aptitude-sheet Modes A/B/C.
3. Reuse `useAllocationDraft`; Confirm is **shell footer** decision control (GG-63), shown **only**
   while this tab is active or the draft is dirty — not a nested dialog.
4. Aspect-scope reverted — do not ship.
5. Leftover bar uses **`recharts`** (shell `LeftoverBar`).

---

## Objective

Spend aptitude points with leftover visibility: tiles, `+`/`−`, leftover bar, Reset/Confirm.

**Success:** Leftover empty is legal; Confirm disabled when draft overspends; inspector shows
catalog `reading` + fed family displayNames (not channel ids).

---

## Tech Stack / Commands / Structure

```text
web/.../ui/actor/AptitudesTab.tsx
# reuses AptitudeTile, LeftoverBar, useAllocationDraft
npm test -- --run src/ui/actor/AptitudesTab
dotnet test tests\FusionRpg.Server.Tests --filter Aptitude
```

---

## Design

- 3×4 (or catalog count) tiles; posture columns from catalog posture field.
- Scope chip: “Commander” until UniqueCreature POST exists.
- Inspector: reading + share contribution sentence using derived-stat-catalog names.
- Draft local; Confirm POST; Reset restores server snapshot.

---

## Tunables

PointBudget / edges stay in `aptitudes.v7.json`. No new numbers. Copy in `aptitude-catalog` only.

---

## Testing / Boundaries / Success

- Unit: leftover math; Confirm disabled at negative leftover; tiles from catalog fixture.
- Always: refuse overspend; catalog names; draft+Confirm.
- Never: clamp; Aspect scope; idWords; Instant-commit Grim Dawn style.
- Success: Matches ProgressionTab allocate behaviour on commander; plate leftover UX.

---

## Open Questions

~~Ideal Q2: UniqueCreature on this sheet later — out of scope for v1 wiring.~~ **Overturned** — UniqueCreature
is a Done gate under [aptitude-sheet-map.md](../aptitude-sheet-map.md) Mode A.
