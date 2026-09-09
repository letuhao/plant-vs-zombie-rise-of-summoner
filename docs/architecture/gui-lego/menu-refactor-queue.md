# Menu refactor queue — gui-lego

**Program:** `gui-lego`  
**Ideal:** [../gui-lego-ideal.md](../gui-lego-ideal.md)  
**Authoring:** [../gui-lego-authoring.md](../gui-lego-authoring.md)  
**Decision:** `decisions.md` **GUI Lego — menu composition (2026-09-09)**

This queue is how the repo refactors many menus **without** boiling the ocean. One surface per
stream after P0’s piece contracts are accepted for React.

---

## Priority table

| Priority | Surface | Host | First pieces to reuse | Notes |
|---|---|---|---|---|
| **P0** | ActorSheet Derived | `ActorPanel` tab | Full `derived-console` recipe | **Done** — FE Wave 0 + C (`features/gui-lego`, `ui/gui-lego`, thin `DerivedTab`) |
| **P1** | ActorSheet Condition | `ActorPanel` tab | `phase-*`, condition glance pieces, shell identity | Plate-13 glance grammar (`cond-hero` + `stand-row`); no root `split-inspect` |
| **P2** | Creatures layer | `PanelShell` | `tool-search`, `chip`, `phase-*` + Actor ERM rows | Filter chrome first; keep `ActorCard`/`ActorRow` |
| **P3** | Relics · Commanders | `PanelShell` | search, chips, Card/Row rungs | Card via ERM — not a new density |
| **P4** | Other rail layers + remaining Actor tabs | `PanelShell` / `ActorPanel` | Shared chrome | Fusion, Pacts, Expeditions, Almanac, Chronicle; Aptitudes, Shield, Status, Elements, Kit, Paths — **one surface per stream** |
| **Later** | Delve / Siege / World inspectors | Stage hosts | Composition grammar | Not rail v1 |

---

## Explicitly out

| Out | Why |
|---|---|
| Developer tree / cheats | Separate tree (GG) |
| Phaser islands | DPLP — canvas, not menu Lego |
| Forking `PanelShell` / second band-2 shell | Hosts stay hosts |
| Growing `DataTable` / `KpiStat` as a parallel kit | Absorb via ERM / pieces |
| Refactoring all eight rail layers in one stream | Queue discipline |

---

## Entry criteria for a queue row

1. DESIGN-GATE **Player menus** docs read in-session.  
2. Recipe JSON drafted (or reuse existing pieces only).  
3. Fold / bus catalog named.  
4. HTML piece or assembled surface for owner gate.  
5. React only after accept.

---

## Status

| Item | Status |
|---|---|
| P0 design pack (pieces, recipe, assembled surface, themes) | Hardened 2026-09-09 — awaiting owner accept for React |
| P1+ | Not started |
