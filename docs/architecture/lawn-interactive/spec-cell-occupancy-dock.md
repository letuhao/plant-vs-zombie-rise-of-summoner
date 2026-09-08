# Spec: `cell-occupancy-dock`

**Module id:** `cell-occupancy-dock` · **Program:** [lawn-interactive-map.md](../lawn-interactive-map.md) ·
**Design landing:** [spec-lawn-interactive.md](../../design/spec-lawn-interactive.md) §4 · §9 · §10.1 ·
**Status:** Draft — pending owner review. **No build authorized until approved.**

**Depends on:** `actor-collection`, `lawn-occupant-adapt`, actor-sheet (open sheet) ·
**Coordinates with:** `commander-action-bar` / InteractionMode mute rules.

---

## Assumptions

1. Band 2 **reserved left column** — one width token. Phaser camera **shrinks** so all **12**
   columns stay visible (including spawn lanes 9–11). Never overlay the right edge.
2. Cell click **always** opens the occupancy list, even for count = 1 (design §14).
3. Select row → push ActorSheet (GG-10). Dock query survives sheet open (GG-12). Esc pops sheet
   then dock; Esc while order armed cancels order first (owned with action-bar).
4. Opening dock/sheet does **not** pause Unity. GG-18 mutes board arrows/confirm when dock or sheet
   open (must join `isLawnKeyboardMuted`).
5. Pointer clicks may still retarget inspect cell except in Spawn/Action targeting modes.
6. No new `#/actor/:id` route — `?cell=` + optional `?sel=<instanceId>` only.

→ Correct these now or this spec proceeds as written.

---

## Objective

Let the player page every living occupant on a focused cell and drill into ActorSheet without
unmounting the Phaser Game (GG-1 / GG-11).

**Success:** At 1280×720 with dock open, 12 cells remain visible; mixed Fielded/Wave list via
collection; unique `sel` only for Bound; generals inspect in-memory.

---

## Tech Stack

| Layer | Choice |
|---|---|
| FE | React Band 2 + PanelShell / reserved rail |
| List | `ActorCollection` + adapt |
| Sheet | ActorSheet push (actor-sheet program) |

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/lawn/CellOccupancyDock
# Later Playwright: 1280/1440/1920, scrollWidth <= clientWidth, 12 cells visible
```

---

## Project Structure

```text
web/.../ui/lawn/CellOccupancyDock.tsx
web/.../ui/lawn/CellOccupancyDock.test.tsx
```

---

## Design

- Reserved left width token; camera inset; board remains center.
- Rows from adapt → ActorCollection (`showHpSliver` on).
- Selection → ActorSheet with bind role from adapt (Cold never from dock).
- URL: `#/lawn/{matchKey}?cell=r,c` and `&sel=<instanceId>` for Bound only.
- Focus: dock open → first collection row (design §10; full focus/safe-area/reduced-motion = **T13**).
- Inspect **Enter** / keyboard confirm on a focused cell opens this dock for the **tile** (GG-21) —
  coordinates with `cell-stack` retarget.
- Spawn tray may share the same column (composition §9) — mutual exclusive chrome, not two lists
  fighting.
- Reduced motion: instant dock open (GG-32) — T13.

---

## Tunables

Dock width — one presentation token.

---

## Testing Strategy

| Level | What |
|---|---|
| Unit | Open for count=1 still shows collection |
| Unit | Query persists across sheet push; resets on cell change |
| Unit | Mute flag includes dock/sheet |
| Unit | Tile confirm / cell click opens dock even for count=1 |
| Layout (later) | 12 columns visible with dock reserved |

---

## Boundaries

- **Always:** Left reserved; adapt before rows; GG-10 push; no Unity pause.
- **Ask first:** Sharing column animation with SpawnTray.
- **Never:** Right-edge overlay; React sprite list; `#/actor/:id`; `sel` for generals.

---

## Success Criteria

- [ ] Dock + sheet stack over live Phaser Game
- [ ] Mixed stack lists Fielded/Wave
- [ ] Keyboard mute contract documented and tested with dock open

---

## Open Questions

None blocking. Exact token name for dock width is presentation.
