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
| **P0** | ActorSheet Derived | `ActorPanel` tab | Full `derived-console` recipe | **Done** — Waves 1–3 landed ([derived-cook-map.md](../derived-cook-map.md); **D7** gauge deferred) |
| **P1** | ActorSheet Condition | `ActorPanel` tab | Full module set per map | **Done** — [condition-glance-map.md](../condition-glance-map.md) · Hot **S1–S3** shared with P1b |
| **P1b** | ActorSheet Shield | `ActorPanel` tab | `shield-console` + stack bar | **Done** — [shield-sheet-map.md](../shield-sheet-map.md) · layers on **`sheet.shieldLayers` (S1)** |
| **P2** | Creatures layer | `PanelShell` | `tool-search`, `chip`, `phase-*` + Actor ERM rows | Filter chrome first; keep `ActorCard`/`ActorRow` |
| **P3** | Relics · Commanders | `PanelShell` | search, chips, Card/Row rungs | Card via ERM — not a new density |
| **P4** | Other rail layers + remaining Actor tabs | `PanelShell` / `ActorPanel` | Shared chrome | Fusion, Pacts, Expeditions, Almanac, Chronicle; Status, Elements, Kit, Paths — **one surface per stream** (**Shield removed** — see P1b). **Aptitudes claimed** by [`aptitude-sheet-map.md`](../aptitude-sheet-map.md) (not a generic P4 grab-bag) |
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
| P0 design pack (pieces, recipe, assembled surface, themes) | **Done** 2026-09-10 — Waves 1–3 cook truth landed; **D7** gauge deferred |
| P1 Condition | **Done** — Waves A–D (Hot bag, paint SSOT, RecipeMount glance) |
| P1b Shield | **Done** — Waves A–C (`shield-console`, stack bar, RecipeMount tab) |
| P2+ | Not started (except **Aptitudes** → [aptitude-sheet-map.md](../aptitude-sheet-map.md)) |
