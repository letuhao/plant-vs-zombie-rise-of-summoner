# Spec: `spawn-tray`

**Module id:** `spawn-tray` · **Program:** [lawn-interactive-map.md](../lawn-interactive-map.md) ·
**Design landing:** [spec-lawn-interactive.md](../../design/spec-lawn-interactive.md) §6 · §10.1 ·
**Status:** Draft — pending owner review. **No build authorized until approved.**

**Depends on:** `actor-collection`, DPLP Intent / `SpawnTargeting` · **Coordinates with:**
`cell-occupancy-dock` (same Band 2 column).

---

## Assumptions

1. Fielding uses existing `enterSpawnTargeting` + ghost on cell. This module is **honest chrome**,
   not a new sim.
2. Rows are **unique demons** legal to deploy this match — not expedition-locked, not already Bound.
3. **Forbidden:** numeric `typeId` field on the player tray; side toggle plant/zombie (specimen has
   a side); debug general inject on the player tray (GG-40 — stays developer tree).
4. Ack immediately on confirm (GG-15); occupant appears on observe fold; reject → toast + ghost clear
   (GG-16 / GG-54).
5. Illegal phase / lock → control disabled **with reason** (GG-55). `canEnterSpawnTargeting` already
   gates phase.
6. Spawn targeting **keeps board arrows** even if Band 2 would mute under naive GG-18 (design §10.1
   exception).

→ Correct these now or this spec proceeds as written.

---

## Objective

Field a unique onto a cell via ActorCollection recognition — no typing engine ids.

**Success:** Player picks a Card/Row, ghosts a cell, enqueues Intent; no `typeId` box on the player
path.

---

## Tech Stack

| Layer | Choice |
|---|---|
| FE | React Band 2 + ActorCollection (grid default) |
| Mode | Existing `interactionMode` SpawnTargeting |
| Authority | Intent enqueue only |

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/lawn/SpawnTray
```

---

## Project Structure

```text
web/.../ui/lawn/SpawnTray.tsx
web/.../ui/lawn/SpawnTray.test.tsx
# Retire player-path typeId input on LawnPage when this ships
```

---

## Design

Flow:

1. Open Field from HUD or with cell selected.
2. ActorCollection lists deployable uniques (grid default).
3. Choose → `enterSpawnTargeting(instanceId)`.
4. Confirm → Intent; ghost already in LawnWorldScene.
5. Observe Admit paints occupant; reject clears ghost + toast.

Empty / locked: visible locked slots or empty state with reason — do not hide the tray (GG-44).

---

## Tunables

None new for balance. Collection page size follows `actor-collection`.

---

## Testing Strategy

| Level | What |
|---|---|
| Unit | No typeId control in SpawnTray tree |
| Unit | Already-Bound unique disabled with reason |
| Unit | Phase Idle/Ending cannot enter targeting |
| Mode | SpawnTargeting click confirms spawn, does not open dock |

---

## Boundaries

- **Always:** ActorCollection; Intent; reason on refuse; spawn arrow exception.
- **Ask first:** Allowing Cold roster uniques that fail match eligibility rules.
- **Never:** `typeId` typing; general debug inject on player tray; FE predicting Admit.

---

## Success Criteria

- [ ] Player path spawn has zero numeric typeId fields
- [ ] Ghost + Intent path reused
- [ ] Dock not opened on spawn confirm click

---

## Open Questions

None. Unique-Commander ineligible to spawn when promotion lands (design §13) — note when Vocabulary
changes.
