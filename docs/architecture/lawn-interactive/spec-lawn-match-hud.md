# Spec: `lawn-match-hud`

**Module id:** `lawn-match-hud` · **Program:** [lawn-interactive-map.md](../lawn-interactive-map.md) ·
**Design landing:** [spec-lawn-interactive.md](../../design/spec-lawn-interactive.md) §8 ·
**Extends:** [commander-surface/spec-lawn-hud-chip.md](../commander-surface/spec-lawn-hud-chip.md) ·
**Status:** Draft — pending owner review. **No build authorized until approved.**

**Depends on:** `actor-collection` (ActorChip for deployed uniques) · **Sibling:** actor-hud (Band B
tokens are not this strip) · **Opens:** `spawn-tray` via Field CTA.

---

## Assumptions

1. Band 1 **top** match strip only — not per-unit HUD, not the commander combat book.
2. Sun bank is match-scoped **`pvz.*`**. Actor `hunger` (Sun label on plants) never appears here.
3. Every cluster either has an observe path or is **omitted / locked-with-reason** — no invented
   second sun or fake aura names.
4. Deployed uniques use **`ActorChip`**, not custom `#typeId` spans over living plants.
5. Aura chip display names come from the **action / aura corpus**, not hardcoded "Sun Blessing"
   unless that string is authored.
6. GG-60: legibility under a live wave; no ornament. HUD stays interactive over panel scrim (GG-5).
7. Safe-area inset on the strip (design §10); full viewport/focus/reduced-motion contract is **T13**.

→ Correct these now or this spec proceeds as written.

---

## Objective

Give the mid-wave player match stocks and identity chrome (sun bank, wave/clock/phase, commander +
aura, deployed unique chips, connection / transport, selection coords) and a **Field** control that
opens the spawn tray — without leaving the lawn stage.

**Success:** Player can read match sun vs never confuse it with sheet hunger; Bound unique chips
open the same ActorSheet path the dock uses; Field opens SpawnTray; missing stocks do not invent
values.

---

## Tech Stack

| Layer | Choice |
|---|---|
| FE | React Band 1 over Phaser island |
| Chips | Existing actor ladder + [spec-lawn-hud-chip.md](../commander-surface/spec-lawn-hud-chip.md) |
| Data | Observe / SignalR match snapshot only |

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run src/ui/lawn/LawnMatchHud
```

---

## Project Structure

```text
web/.../ui/lawn/LawnMatchHud.tsx
web/.../ui/lawn/LawnMatchHud.test.tsx
# Compose: sun bank, wave+clock+phase, commander chip, deployed chips,
# connection, transport, selection coords, Field CTA
```

---

## Design

| Cluster | Shows | Scope | Do not confuse with |
|---|---|---|---|
| Sun bank | `pvz.*` sun | Match | Actor `hunger` |
| Wave | Index + next-in | Match | |
| Clock | Match / wave clock readout when observe provides it | Match | UniqueActor Cold timers |
| Phase | Starting / InMatch / Paused / Ending | Match | UniqueActor Cold |
| Commander | This-match leader + aura display name | Snapshot at board.start | Mid-run default change; Patron |
| Deployed | Bound unique `ActorChip`s | Match bindings | Full roster; generals |
| Connection | Observe / injector link honesty (Fusion optional) | Session | Invented “online” |
| Souls (session) | Only if ledger already attributes lawn kills | Run | Sanctum wallet |
| Transport | Pause / speed | Overlay pause contract | |
| Selection | Focused cell in player words ("Lane 3 · Column 5") | UI | |
| **Field** | Opens `SpawnTray` (design §6 step 1) | UI → spawn-tray | Debug `typeId` spawn |

Commander tap → deferred open of **same** ActorSheet with "this match" banner unless chip spec
already requires it (design landing §13).

---

## Tunables

Presentation tokens for strip density / chip overflow. Match sun rules stay in existing owners.
Safe-area / height-scale: see T13 / design §10.

---

## Testing Strategy

| Level | What |
|---|---|
| Unit | Fixture with sun bank does not render hunger label |
| Unit | Deployed chips only Bound uniques; generals absent |
| Unit | Missing souls / clock omitted when observe silent |
| Unit | Field CTA opens spawn-tray entry (mock) |
| Unit | Connection honest when Fusion optional |

---

## Boundaries

- **Always:** Observe-only; `pvz.*` vs hunger split; ActorChip for deployed; Field → SpawnTray.
- **Ask first:** Souls-on-HUD if ledger attribution is incomplete; commander chip → sheet in v1.
- **Never:** Second sun; `#typeId` plant spans; adventure-map spell chrome; pause Unity just because
  a panel opened.

---

## Success Criteria

- [ ] Strip matches plate 04 §A skeleton + §8 inventory (including clock, connection, Field)
- [ ] Sun bank vs Condition hunger documented in UI copy/tests
- [ ] Connection / phase honest when Fusion optional
- [ ] Field opens spawn tray without typing `typeId`

---

## Open Questions

Whether commander chip opens ActorSheet in v1 (design §13) — default: deferred unless chip spec
already requires it.
