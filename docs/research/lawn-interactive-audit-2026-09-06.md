# Lawn interactive GUI — multi-perspective audit

**Date:** 2026-09-06
**Scope:** Draft player surface in `docs/design/spec-lawn-interactive.md` and plate
[12-lawn-stage.html](../design/12-lawn-stage.html). Shared widgets (`ActorCollection`, `ActorSheet`),
cell overlap, spawn tray, off-board combat book, match HUD.
**Out of scope:** React/Phaser implementation, SiegeBoard, paint goldens.
**Status:** Research reference. **Audit only — no build authorized.** Findings below were folded
into the spec and plate the same day. Do not implement from the first-pass plate E aptitude names
or the adventure-spell label.

**Governed by:** [game-gui-principles.md](../architecture/game-gui-principles.md) ·
[information-architecture.md](../design/information-architecture.md) ·
[fe-game-foundation.md](../architecture/fe-game-foundation.md) · DPLP observe≠control ·
[spec-primary-stats.md](../architecture/class-system/spec-primary-stats.md) ·
[definitions.md](../architecture/effect-atom/definitions.md) §7 standing axes ·
[creature-system-map.md](../architecture/creature-system-map.md) Vocabulary ·
[resource-hub-ssot.md](../architecture/resource-hub-ssot.md) ·
[ssot-equip-slots.md](../architecture/item/ssot-equip-slots.md) §2.10

**Why this file exists.** Five perspectives ran on the first draft (UX / game-ui-ux, DPLP,
character-sheet systems, GG-9 FE duplication, creature / HoMM3 vocabulary). They agreed the *split*
(one collection, one sheet, overlap on canvas, commander off-tile, two Suns) and rejected the
*wording* that contradicted closed vocabularies.

---

## Executive summary

**Verdict after fold:** still **draft for owner review**. Architecture split holds. First-pass
plate E and §7 adventure labelling do **not**.

| Lens | First-pass verdict | After fold |
|---|---|---|
| UX / game-ui-ux | fix-before-review | Dock reserved left + 12-col board + input table. Still verify at 1280×720 in the plate |
| DPLP | revise before `/spec` | Intent enqueue; no FE A2; `sel` = `instanceId` only; Unity does not pause under sheet |
| Character sheet | do not implement from plate E | Closed twelve, five Standing axes, HP not Vitality, unlock-honest doll |
| GG-9 FE | does not hold as extract-and-wire | New `ActorCollection`; Creatures consumes; occupant adapter required |
| Creature / HoMM3 | do not graduate | Combat book, not adventure; Fielded / Wave, not Yours / Wild |

---

## P0 that changed the draft

| # | Finding | Fold |
|---|---|---|
| 1 | Right overlay ~320px hid spawn columns on a **12-col** lawn at 1280×720 | Reserved **left** column; camera shrinks; one dock-width token |
| 2 | Inspect, spawn, and orders shared Enter / Space / click. IA: Space = pause. `wireKeyboardNav` also confirms on Space | §10.1 `ActionTargeting`. Space = pause. Enter confirms. Esc cancels armed order first. Occupied click does not clobber targeting |
| 3 | `?sel=` for general creatures | `sel` is `instanceId` only. Generals are in-memory, die-cleared |
| 4 | “Range is real here” / “confirm casts” implied FE battle A2 on the PvZ lawn | Enqueue Intent with declared target kind; server/injector re-resolves; highlights are observe chrome |
| 5 | Plate primaries were six invented names (Grace, Ward, Insight, Fury, Calm) | Closed twelve from `spec-primary-stats.md` |
| 6 | Standing drew three bars / truncated labels | All five `definitions.md` axes. Stale `PowerCategory` in `types.ts` called out |
| 7 | Bar labelled HoMM3 **adventure** (View Air) | Off-board **combat** book (hero not a hex). Adventure verbs stay on world-map Orders |
| 8 | Yours / Wild failed GG-23 (`Wild` = capture) | **Fielded** vs **Wave**. Engine generals include sun-planted plants |
| 9 | ActorCollection treated as a CreaturesLayer extract | New widget. Creatures is list-only UniqueActor. Occupant → row adapter required (`ActorView` wants `instanceId`) |
| 10 | Two ActorSheet programs (six-tab shell vs landing-is-the-character) | Landing **supersedes** actor-sheet-shell. `actor-sheet-map.md` carries a successor banner, not a retire |
| 11 | KeyValue inspector only named at `LawnPage.tsx:754-767` | Player path retires dumps; GG-41 developer inspector may keep them |

## P1 folded when cheap

Unlock-honest paper-doll at Lv 14 (`sheath` / `ward-array` opens 24). One HP pool (hub label **HP**).
Unique pip on stacked Bound sprites. HP sliver on dock rows. Cost chips name the payer (`40 sun`).
Aura chip **Might**, not “Sun Blessing”. Order slot not named Ward. Overlay pause is F10, not
sheet-over-Phaser. HUD fielded chips are `ActorChip` on bound uniques, not `#typeId` spans.

## Manufactured owner questions (struck)

Always-list on singleton cells, and wave Paths = read-only species tree, were already decided in
the first draft. Real leftover: **Dave `CommanderId` vs unique-creature Commander** (Vocabulary
2026-09-06).

---

## What still must not be built from this plate

- React/Phaser until owner review.
- A second `ActorSheet.tsx` beside `ActorPanel.tsx`.
- Copying `VirtualCreatureList` into the dock.
- FE range legality on the PvZ lawn.
- Putting `ptr` or a general occupant key in the address bar.

---

## What this audit did not re-verify

`actor-hub-ssot.md` and `action-ideal.md` were not read cover-to-cover in the drafting session
(declared DESIGN-GATE gap). Constraint tests were not run. Playwright 1280 composite of the *app*
does not exist yet — only the catalog plate can be resized.

Code citations in the spec (`ActorPanel.tsx`, `types.ts:575-591`, `ResourceView` five ids,
`CreaturesLayer.tsx` 24/240, `LawnPage.tsx` `typeId` spawn) were verified against source in the
drafting session, not re-opened for this fold.

**Catalog plate, Playwright 1280×720 (2026-09-06, after fold):** `documentElement.scrollWidth ===
clientWidth` (no horizontal page scroll, after `minmax(0,1fr)` on the contrast grid). Board is 12
columns. Reserved left dock (`right` 452) does not overlap spawn cells (column 12 at `left` 1076).
Closed twelve aptitude names and five Standing axes are in the sheet DOM. `sheath` is locked, not
filled, at the Lv 14 Emberling. This is the HTML catalog, not the app.
