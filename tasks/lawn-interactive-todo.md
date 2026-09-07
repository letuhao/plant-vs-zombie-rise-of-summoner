# Tasks: lawn-interactive program

Plan: [lawn-interactive-plan.md](lawn-interactive-plan.md) · Map:
[../docs/architecture/lawn-interactive-map.md](../docs/architecture/lawn-interactive-map.md).

**IMPLEMENT (this session):** FE authorized by goal; specs remain SSOT for behavior.

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

### Checklist

- [x] T0 Presentation tokens
- [x] T1 ActorCollection
- [x] T2 lawn-occupant-adapt
- [x] Checkpoint A — foundation list
- [x] T3 cell-stack (Phaser) — `stackDrawPlan` wired, player `viewMode="stack"`, +K / Bound pip; tile confirm emits `kind: "tile"`
- [x] T4 lawn-match-hud (connection, Field CTA; `clockLabel` prop ready when observe supplies it)
- [x] Checkpoint B — monitor without click
- [x] T5 cell-occupancy-dock + mute via `boardArrowsLive` (Spawn/Action targeting keep arrows)
- [x] T6 sheet push + URL + retire player KeyValue inspect dump
- [x] Checkpoint C — inspect stack
- [x] T7 spawn-tray (Bound uniques locked-in-tray with reason; no player typeId)
- [x] Checkpoint D — field a unique
- [x] T8 ActionTargeting chrome — Enter confirms (Space does not); Esc LIFO cancel; tile-dock Enter
- [x] T9 commander-action-bar UI (nine locked-visible empty-corpus slots)
- [ ] Checkpoint E — combat book full arm→Intent (deferred until action corpus; chrome + Esc cancel armed only)
- [x] T10 CreaturesLayer → ActorCollection
- [x] T11 scope picker → ActorCollection (`targetPtr` explicit; UniqueDemon keeps `instanceId`)
- [x] T12 actor-hud click → dock
- [x] T13 focus / safe-area / reduced-motion (§10) — `.safe-area-*` in tokens; dock focuses first collection item
- [x] Checkpoint F — program acceptance (`e2e/lawn-interactive.spec.ts` + multi-viewport) for chrome paths proven above
