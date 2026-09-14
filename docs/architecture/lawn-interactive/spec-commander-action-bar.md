# Spec: `commander-action-bar`

**Module id:** `commander-action-bar` · **Program:** [lawn-interactive-map.md](../lawn-interactive-map.md) ·
**Design landing:** [spec-lawn-interactive.md](../../design/spec-lawn-interactive.md) §7 · §10.1 ·
**Composes:** [spec-action-layer.md](../../design/spec-action-layer.md) ·
**Status:** Draft — pending owner review. **No build authorized until approved.**

**Depends on:** action-layer chrome + Intent targeting · **Coordinates with:** dock/sheet mute
(`ActionTargeting`).

---

## Assumptions

1. This is the HoMM3 **combat-hero** book analogue: commander **off the hex**, stacks fight, 1–9
   orders. It is **not** the adventure-map book (world-map Orders).
2. Commander **never** drawn as a lawn tile. Today identity is Crazy Dave (`CommanderId`) until
   owner promotes to unique creature (design §14) — **one** bar, not two.
3. FE does **not** run A10 / battle A2 range math. Highlights are observe chrome; Server/injector
   re-resolve. Do not say the player "casts."
4. Costs name the **stock** — match `pvz.*` sun or commander actor pools — never bare `40` (GG-46).
5. Empty corpus → locked slots with *"Orders unlock with the action corpus"* (GG-44). No fake
   Strike/Firebolt. Do not name a slot **Ward**.
6. Dock/sheet open does not pause Unity. Armed order: Esc cancels order **before** pop. Occupied-cell
   click must **not** clobber ActionTargeting into inspect (`selectOccupant`).
7. v1: no enqueue-from-sheet. Space = pause (not confirm). Confirm = Enter.
8. Inspect-mode Enter opens **tile** dock (GG-21) — shared with cell-stack / dock; ActionTargeting
   must not steal that into occupant-only confirm.
9. Safe-area inset on the bar; reduced-motion / focus landing stops: design §10 → **T13**.

→ Correct these now or this spec proceeds as written.

---

## Objective

Enqueue off-board commander combat orders onto the lawn via a Band-1 bottom-center hotbar.

**Success:** Player arms slot 1–9, targets per declared kind, Intent enqueues; unaffordable shows
reason; empty corpus shows locked-visible slots.

---

## Tech Stack

| Layer | Choice |
|---|---|
| FE | React Band 1; `ActionSlot` + cost cluster + refusal from action-layer |
| Mode | New `ActionTargeting` beside SpawnTargeting |
| Keys | `1`–`9` stage hotbar (IA §5) |

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/lawn/CommanderActionBar
npm test -- --run src/lawn/interactionMode
```

---

## Project Structure

```text
web/.../ui/lawn/CommanderActionBar.tsx
web/.../lawn/interactionMode.ts          # ActionTargeting + mute table
```

---

## Design

### Layout

Band 1, bottom-center, above safe-area; does not cover the rail. Each slot: icon, player name, cost
cluster, cooldown veil, unaffordable/refused reason (GG-55).

### Targeting table (with dock)

| Mode | Arrows | Enter | Esc | Click occupied |
|---|---|---|---|---|
| Inspect | Cell focus (muted if dock/sheet) | Open dock for **tile** | Pop sheet then dock | Open / retarget dock |
| SpawnTargeting | Ghost (arrows live — exception) | Spawn Intent | Cancel targeting | Confirm spawn; no dock |
| ActionTargeting | Per target kind | Order Intent | Cancel armed order first | Select target; **no** inspect clobber |

### Payer / verbs

Membership from action corpus / action-ideal. Passive aura ≠ bar slot. Summon-to-cell may be an
action. Payer stock labeled on cost chips.

---

## Tunables

Slot count **9** is structural (keymap) — comment if not tunable. Costs/cooldowns stay in existing
action tuning owners.

---

## Testing Strategy

| Level | What |
|---|---|
| Unit | Empty corpus → nine locked-visible, no fake verbs |
| Unit | Unaffordable → `bad` cost chips + reason |
| Mode | ActionTargeting click does not `selectOccupant` into inspect |
| Mode | Esc cancels armed order before dock pop |
| Mode | Space pauses; does not confirm |
| Mode | Inspect Enter opens tile dock; ActionTargeting click does not clobber into inspect |

---

## Boundaries

- **Always:** Intent only; labeled costs; combat book not adventure; one commander bar.
- **Ask first:** Promoting CommanderId to unique creature; enqueue-from-sheet.
- **Never:** Draw commander on a tile; FE range oracle; Ward slot name; hide empty bar.

---

## Success Criteria

- [ ] ActionTargeting exists beside Spawn/Inspect
- [ ] Mute + Esc precedence match design §7.3 / §10.1
- [ ] No fake placeholder verbs when corpus empty

---

## Open Questions

Commander identity promotion (Dave vs unique) — owner §14; recommendation keep Dave for v1 lawn GUI.
