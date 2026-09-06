# Spec: `cell-stack`

**Module id:** `cell-stack` · **Program:** [lawn-interactive-map.md](../lawn-interactive-map.md) ·
**Design landing:** [spec-lawn-interactive.md](../../design/spec-lawn-interactive.md) §5 ·
**Status:** Draft — pending owner review. **No build authorized until approved.**

**Depends on:** lawn projector / Phaser lawn scene · **Sibling:** actor-hud (Band B on topmost).

---

## Assumptions

1. Stack presentation is **Phaser-only** (DPLP). React must not duplicate occupant sprites.
2. Offset is a **presentation tunable** (e.g. 6–8 px down-right). Max drawn sprites before `+K` is a
   **structural draw cap**, not a progression ceiling — comment it as such.
3. Per-unit HUD stays actor-hud on the **topmost** occupant; under-stack full HUD default off v1.
4. Bound uniques keep the identity **pip on the sprite**, including under-stack (monitor without
   click).
5. Click / confirm: topmost pick via existing PickSystem **or** tile → dock; keyboard confirm on a
   focused cell opens the **dock for the tile**, not only the top occupant (GG-21). Inspect-mode
   confirm must **retarget to the tile** (today `LawnWorldScene` may emit an occupant — change it).
6. Focus / reduced-motion / safe-area for docks and bars: see design §10 — tasked as **T13**; this
   module only owns stack paint + tile-confirm hook.

→ Correct these now or this spec proceeds as written.

---

## Objective

Draw every living occupant in a cell with stable overlap so a mid-wave player can **see** a stack
without clicking.

**Success:** 2-wide / 3-deep stacks read as stacks; `+K` when over N; unique pip visible under
stack; no React name overlay on cells.

---

## Tech Stack

| Layer | Choice |
|---|---|
| Phaser | Lawn world scene / layoutGrid depth order |
| React | None for sprites; dock is a different module |

---

## Commands

```powershell
# After implement — projector / lawn scene tests as existing package conventions
cd web\fusion-rpg-web
npm test -- --run src/lawn  # filter to CellStack / layout once named
```

---

## Project Structure

```text
# Phaser lawn presentation (exact path follows existing LawnWorldScene / layout modules)
web/.../lawn/.../CellStack*.ts   # or methods on existing layout
```

---

## Design

| Rule | Detail |
|---|---|
| Plane | Phaser only |
| Overlap | Stable offset per occupant after first; depth = layout order |
| Cap visible | Show N sprites + `+K` pip; N presentation tunable |
| HUD | actor-hud on topmost; thinner slivers only if that program enables |
| Bound pip | On sprite under stack |
| Pick | Topmost or empty-ish → dock; confirm retargets to **tile** in inspect mode |
| Selection | Existing ring; dock open ⇔ cell selected |

Glance only — numbers stay in the sheet (GG-60).

---

## Tunables

| Token | Role |
|---|---|
| Stack offset px | Feel of overlap |
| Max sprites before `+K` | Structural draw cap (comment) |

Not Core Policy / not a power curve.

---

## Testing Strategy

| Level | What |
|---|---|
| Unit / scene | Ordered offsets for N=1..Nmax+1; `+K` count correct |
| Unit | Unique pip present when Bound under stack |
| Guard | No React occupant-name overlay on cell |

---

## Boundaries

- **Always:** Phaser owns board paint; structural +K comment; unique pip.
- **Ask first:** Enabling under-stack HP slivers by default.
- **Never:** Progression ceiling disguised as draw N; FE predicting occupants before Admit.

---

## Success Criteria

- [ ] Stack visible without click on fixture cells
- [ ] Keyboard **tile** confirm opens dock with full list (not topmost-only)
- [ ] actor-hud still only full chrome on topmost (v1)
- [ ] Bound unique pip visible under stack

---

## Open Questions

Exact N default left to presentation pass after approval — not a product fork.
