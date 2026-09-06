# Tasks: lawn-interactive program

Plan: [lawn-interactive-plan.md](lawn-interactive-plan.md) · Map:
[../docs/architecture/lawn-interactive-map.md](../docs/architecture/lawn-interactive-map.md).

**PLAN review** — do not IMPLEMENT until owner accepts
[lawn-interactive-plan.md](lawn-interactive-plan.md). Specs remain the SSOT; this list tracks
slices only.

### Spec → task index

| Spec module | Tasks |
|---|---|
| `actor-collection` | T1, T10, T11 |
| `lawn-occupant-adapt` | T2 |
| `cell-stack` | T3 |
| `lawn-match-hud` | T4 |
| `cell-occupancy-dock` | T5, T6 |
| `spawn-tray` | T7 |
| `commander-action-bar` | T8, T9 |
| sibling HUD → dock | T12 |
| design §10 focus / safe-area / reduced-motion | T13 |
| presentation tokens | T0 |

### Binding rules (when unlocked)

1. Prefixed plan/todo only — never `tasks/plan.md` / `tasks/todo.md`.
2. Observe + Intent; no FE Admit prediction; no player `typeId` spawn.
3. Left dock reserved; 12 columns visible.
4. ActorSheet push uses existing panel until actor-sheet program unlocks (reversible default).
5. Commander = Dave v1 one bar; empty corpus = locked-visible slots.
6. Buy before build for icons/slots already locked in action-layer / actor chrome.
7. Tile confirm opens dock (GG-21); Space = pause; Esc cancels armed order first.

### Checklist

- [ ] T0 Presentation tokens
- [ ] T1 ActorCollection
- [ ] T2 lawn-occupant-adapt
- [ ] Checkpoint A — foundation list
- [ ] T3 cell-stack (Phaser) + tile confirm
- [ ] T4 lawn-match-hud (clock, connection, Field CTA)
- [ ] Checkpoint B — monitor without click
- [ ] T5 cell-occupancy-dock + camera + mute
- [ ] T6 sheet push + URL + retire player KeyValue inspect dump
- [ ] Checkpoint C — inspect stack
- [ ] T7 spawn-tray (retire player typeId)
- [ ] Checkpoint D — field a unique
- [ ] T8 ActionTargeting + Esc/Space + tile-dock Enter
- [ ] T9 commander-action-bar UI
- [ ] Checkpoint E — combat book
- [ ] T10 CreaturesLayer → ActorCollection
- [ ] T11 scope picker → ActorCollection
- [ ] T12 actor-hud click → dock
- [ ] T13 focus / safe-area / reduced-motion (§10)
- [ ] Checkpoint F — program acceptance
