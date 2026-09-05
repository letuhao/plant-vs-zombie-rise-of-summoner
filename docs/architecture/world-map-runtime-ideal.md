# World map runtime — the ideal

**Status:** idea phase, 2026-09-05. Spec drafted 2026-09-06:
[world-map-runtime/spec-world-map-runtime.md](world-map-runtime/spec-world-map-runtime.md)
(pending owner review). **No build authorized** until that spec is approved.

**Program id:** `world-map-runtime`.

This document reconciles a renderer decision that already shipped wrong, and records the long-term
tech stack for the world map plane. It does **not** reopen the world turn engine, the sealed FE
view contract, the HUD/inspector catalog, or recruitment.

**It supersedes, and only these:**

| What | Where it was locked | What survives |
|---|---|---|
| The map camera is an SVG `viewBox` plus a hand-rolled pan/zoom hook | [tech-stack.md](../design/tech-stack.md) T3 (*"Render the world map as SVG with a small pan/zoom hook"*); [world-stage-ideal.md](world-stage-ideal.md) §4.1–§4.2 *implementation*; [world-stage-map.md](world-stage-map.md) assumption 3 | The **gestures** (drag pan, wheel zoom, arrows pan, Fit). No minimap on `small`/`medium`. Authored `{x,y}`, no auto-layout |
| Dropping `@xyflow/react` *and therefore drawing the player map in DOM/SVG* | T3; world-stage-ideal §8 and §8c.6 | xyflow stays **out of the player map and out of the entry chunk**. It may still return later as a **developer authoring** tool in an unbudgeted `dev` chunk — T3's one remaining legitimate clause |
| Plate 11 §B schematic (rounded rects joined by strokes) as the *map* visual language | [design/11-world-stage.html](../design/11-world-stage.html) §B | §B remains a **lane-stroke legend**. Plate 11 §A **full card** remains the inspector/panel catalog. §O is the compact **map pin** catalog. GG-27 channels remain binding on whatever the map draws |

**It does not supersede:** world-stage HUD, inspector, turn cluster, playback, lenses, confirms,
GG-1…GG-61, D10 (framed chrome now, art later), the dual-plane lawn architecture, or
[world-graph-ideal.md](world-graph-ideal.md)'s simulation.

---

## Which loop this extends

**World map — adventure** and **world stage — empire building** ([the-loops.md](../guide/the-loops.md)
places 4 and 5). Same virtual-turn clock. Same rift: sectors joined by lanes, you go and you are.

This is not a new loop. It is the place those two loops are *seen*. A feature that only changed the
turn engine would not need this document; a feature that only changed the inspector would not either.

Rise of Summoner is an RPG plus empire-building game. The lawn is the first core loop, not the whole
war. This runtime is how the rift looks and moves under the player's hand.

---

## Load-bearing principles (restated, not linked)

A downstream session reads this file, not its neighbours.

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** The world
   map is a web stage. Fusion's `Plant`/`Zombie` fields, `EntityStatWriter`, and the Unity write
   surface do not constrain node types, cameras, shaders, or Phaser GameObjects. "Does the lawn
   support a galaxy map?" is the wrong question.
2. **The PvZ write surface constrains only persistent vanilla stat changes.** This program writes
   none.
3. **Two async systems.** The turn engine is server-side: `state(N+1) = step(state(N), commands(N))`.
   The FE observes a projection and issues commands. Phaser displays the last projection; it does
   not step the world and it does not own HTTP or SignalR.
4. **Gameless-first is capability, not the pitch.** The world stage stays fully playable with Fusion
   closed. A Phaser canvas in the browser is standalone. The injector must not gate the map.
5. **One power ladder.** This runtime introduces no `f(level)`. Camera min/max scale are structural
   (whether the control works), not balance. Node-count ranges already live in
   `data/tuning/world.v5.json` `worldSizeNodes`.
6. **The balance surface is data.** Lane width, march range, loam figures stay in existing tuning.
   Stroke weights derived from `Width` per-mille are already specified. Do not hide a new yield curve
   inside a shader.
7. **A game interface is a stage with layers.** React owns chrome (HUD, inspector, layers). The map
   plane is the stage's body. Opening the inspector must not destroy the map's camera, selection, or
   Game instance (GG-11).
8. **D10.** Framed chrome now; art later; the layout is already art-ready. Illustration drops into
   the same GameObjects. v1 is designed placeholders, not a missing renderer.

---

## What this is

You stand on the rift. Sectors sit in space the way Endless Space 2's star systems sit in a galaxy:
typed nodes, typed lanes, a camera you drag and wheel, fleets on the graph, **detail in a panel**.
You do not scroll a document of flowchart boxes.

Each sector kind and each lane kind is its own view, bound to the existing view-model
(`SectorView` / `LaneView` / `sectorChannels` / `laneChannels`). The inspector remains the plate 11
card. The map pin is compact: ownership ring, crest, name, pip row, fog silhouette.

That is the player sentence. The engineering sentence is: **the world map plane is a Phaser island
under the same dual-plane the lawn already locked**, not a graph-editor library and not a second
hand-rolled SVG camera.

---

## What already exists

Sorted with the words this phase requires. A default-off path, a never-called function, or a
stylesheet that does not consume its own data attributes is a **wiring gap**, not a wall.

### Built

| What | Proof |
|---|---|
| World as a stage under `StageHost`, no page scroll, Esc/right-click clears selection | `WorldStage.tsx` mounts `StageHost` + `useStageMountGuard("world")`; `claimStageEscape` at `:80-83`; SVG `onContextMenu` → `handleEscape` at `:207-210` |
| Sealed FE views, adapters, contract guard | `adaptWorldState`; `stages/` banned from DTO imports |
| View-model channels (ownership, health, fog, lanes, slots) as pure functions | `sectorChannels.ts`, `laneChannels.ts`, `fogTreatments.ts`, `slotSilhouettes.ts` — unit-tested; **no Phaser types in those files** |
| HUD, inspector, turn cluster, playback, targeting overlays as React | `stages/world/hud`, `inspector`, `turn`, `playback`, `targeting` |
| Phaser 4.2 already in the app; generic `createGame` for non-lawn boards | `package.json` `"phaser": "^4.2.1"`; `createGame.ts:3-9` names siege and battle as callers; `createLawnGame.ts` is a thin wrapper |
| Dual-plane lawn: React chrome, EventBus, Phaser GameObjects, destroy on leaving the stage | [fe-game-foundation.md](fe-game-foundation.md) DPLP; `LawnGameHost.tsx:50-63` creates the Game on mount; plane lock *"React never owns GameObjects; Phaser never owns HTTP"* |
| Authored sector positions, no auto-layout | `WorldScene.tsx` `GRID_X = 220`, `GRID_Y = 190`; `layoutX`/`layoutY` from the view |
| Five map-size tiers; only `small`/`medium` available | `WorldSizeCatalog.cs:48-58`; ranges in `data/tuning/world.v5.json:23-42` — small 6–10, medium 14–18, large 28–36, huge 56–72, giant 112–144. Huge measured ~52–80ms at 64 nodes; giant ~0.6–0.7s at 128 (`WorldSizeCatalog.cs:53-57`) |
| `@xyflow/react` already removed from `package.json` | grep of `web/fusion-rpg-web/package.json` — Phaser present, xyflow absent; `xyflowGuard.test.ts:6-7` forbids a quoted import under `stages/` |
| Hex-guard exemption for old `features/world/` retired; `game/` remains the one Phaser exemption | `hexGuard.ts:15-26` — `SKIPPED_PATH_PREFIXES = ["game/"]` only |

### Wiring gap

These are **inert machinery**, not missing architecture.

| What | The inert line |
|---|---|
| Camera math (pan, zoom-about-pointer, fit, clamps) | `camera.ts:51-71` `zoomAbout`; `camera.ts:74-76` `panBy`; `camera.ts:93-111` `fitToExtent`. **Never subscribed.** `WorldStage.tsx:77` is `useMemo(() => fitToExtent(...), [world.sectors])` — a frozen viewBox |
| Gesture map (wheel, drag-on-empty, arrows, Fit) | `cameraGestures.ts:13-21` `wheelZoom` and the rest of that file. `WorldStage.tsx` does not import `cameraGestures`. The SVG at `:200-217` has no `onWheel` / pointer-drag / key handler |
| Zoom tier on the scene | `WorldStage.tsx:217` hardcodes `zoom="map"`, so `SectorNode` drops slots at every scale (`SectorNode.tsx:18-19`, `:68`) |
| Channel attributes with no paint | `scene.css:8-14` states hatches and slot silhouettes are tested data that still render as plain text/borders |
| Lane geometry | `WorldScene.tsx:128-129` passes `sourceX={from.x}` / `sourceY={from.y}` (card top-left). Range overlays already use `CENTER_X`/`CENTER_Y` at `:102-105`. Lanes miss the cards |
| Mid-lane legion animation | `LegionMarker` still walks a `<path>` by id; `WorldScene.tsx:80-85` records forces on lanes as deliberately unwired |

None of these mean "the web cannot host a game map." They mean the SVG replacement for xyflow was
stopped after the pure functions and before the host.

### Real gap

No mechanism exists yet for:

- A Phaser `WorldMapScene` (or equivalent) that owns cameras, sector objects, and lane objects
- `world:model` / `world:select` / `world:ready` / `world:destroyed` on the mediator — `EventBus.ts:6-13`
  is a **closed lawn union** (`LawnBusEvent`). The generation allocator (`allocGameGeneration`, `:78-80`)
  is reusable; the event names are not
- `createWorldGame` wrapping `createGame` the way `createLawnGame` does
- Typed GameObject factories keyed by sector intel/ownership/health and by lane kind×state
- Endless Space–shaped map chrome (compact system pin, glow lanes, starfield/grid backdrop) — plate 11
  §A is the inspector card; §O is the pin catalog (HTML stand-in). Neither is the Phaser object yet
- Reading CSS tokens into Phaser (`hexGuard.ts:15-18` names this as real separate work). Without it,
  `src/game/world/` will grow a second palette inside the hex exemption
- Map-node LOD driven by Phaser camera zoom (the `map`/`detail` flag exists on `SectorNode` but is
  not derived from a live camera)

**Not a real gap:** "we need a graph library for typed nodes." The type×view table is a registry
pattern the lawn already uses (`SyncFromModelSystem` switching on occupant kind). Phaser is already
paid for.

---

## Prior art

Concrete numbers, named failures, sources. Unverified claims are marked.

### Endless Space 2 — the structural match

The ES2 galaxy is **nodes plus starlanes**, not a hex grid. The manual: *"The galaxy of Endless Space
2 is presented as a set of nodes connected by starlanes. Each node corresponds to a point of interest
(star system, asteroid field, etc.)"*
([Steam user manual](http://cdn.akamai.steamstatic.com/steam/apps/392110/manuals/User's_Manual_-_Endless_Space_2.pdf)).
Lanes come in kinds (normal, wormhole, "black" lanes visible only after both ends are visited) —
Amplitude GDD 3, 2016
([community thread](https://community.amplitude-studios.com/amplitude-studios/endless-space-2/forums/65-general/threads/18858-es2-gdd-3-galaxy-exploration)).
Scan View (Space) swaps the **lens**, not the camera model. Zoom-out shows constellation names;
zoom-in shows system FIDSI rings then the system management panel
([Scan View wiki](https://endless-space-2.fandom.com/wiki/Scan_View)).

**What transfers:** typed nodes, typed connectors, camera as navigation, **detail in a panel**,
zoom tiers that simplify without inventing a second fact set. **What does not:** ES2's 40-system /
25-fleet volume (already rejected as an interaction-pattern transfer in world-stage-ideal §8c.1).
Our available maps are 6–18 nodes.

**Recorded absence:** no Amplitude GDC talk on *how they render* the galaxy (engine, shaders, batching)
was found. Do not infer Unity internals.

### Endless Legend — the failure to keep

EL's documented zoom failure: zooming out hides tile yields while revealing resource icons, so the
player oscillates between depths (already captured in world-stage-ideal §4.2; EL manual p.10 camera
methods). **Zoom tiers must be strict supersets of legibility.** This runtime inherits that rule; it
does not reopen it.

EL is a **hex tile** map. Our world is not. Borrowing EL's camera *gestures* is right; borrowing a
hex renderer is a different game.

### React Flow / `@xyflow/react` — the maintainers' own limit

React Flow and `@xyflow/react` are the same library. Maintainer (moklick), 2023-11-23, on
[xyflow#723](https://github.com/xyflow/xyflow/issues/723):

> Normally rendering dozens is always fine, hundreds is OK and thousands is too much. React Flow
> wasn't built for visualizations but for editors. If you want to render visualizations, I would
> use a canvas based library.

Issue #5442 (2025): a user with **~100 complex nodes** reports sluggish pan even after hiding
off-screen HTML; they recommend a canvas renderer at low zoom.
[xyflow#5442](https://github.com/xyflow/xyflow/issues/5442).

Our `giant` tier is 112–144 nodes (`world.v5.json:39-41`). That is already inside the band the
library's own users call painful for *complex* nodes — and plate 11 cards are complex. `medium`
(14–18) would be "dozens, always fine" **as a flowchart**. It would still look like a flowchart.

T3 was right that xyflow is a node-editor and we do not drag sectors. T3 was wrong that the
replacement is therefore a small SVG hook. The honest reading of the same evidence: **the player map
is a visualization, so it wants a canvas/WebGL host we already have.**

### Stellaris — lag is usually the sim, not the map draw

Stellaris late-game slowness is widely reported as **simulation** (pops, jobs, pathfinding, ship
counts), not the galaxy mesh
([Guild Order playbook](https://guildorder.com/games/stellaris/guides/late-game-performance-discipline)).
Paradox 4.3 work is a per-tick processing budget, not a new map renderer.

**What transfers:** do not confuse engine-step cost with draw cost. Our giant-tier 0.6–0.7s is
**topology** (`WorldSizeCatalog.cs:56-57`), already hashed, not a Phaser problem. The renderer must
not become the excuse to skip Tarjan-first. **What does not transfer:** Stellaris-scale object
counts. We will not have thousands of ships on this graph.

### Dual-plane in this repo

The lawn already split "frame loop / sprites / FX" (Phaser) from "chrome / inspector / layers"
(React) with a generation-scoped EventBus. D2 in game-gui-principles §20.1: Phaser Game is created on
entering the lawn stage and destroyed on leaving it — never when a panel opens. GG-1: one stage at a
time, so **two Phaser.Games never coexist**. World copies that lifetime. It does not invent a third
plane.

### Cytoscape.js / PixiJS — rejected with reasons

Cytoscape is a graph-theory canvas with a stylesheet, not a React/Phaser component per type. Pixi is
a thinner WebGL layer that would duplicate Phaser 4.2 already in `package.json`. Neither earns a
second runtime.

---

## The real question

Feasibility is not the question. The channels, the views, the HUD, and Phaser all exist.

The question was **which shape** hosts the map plane for the next years — including `large`/`huge`/`giant`
when `world-generator` ships — without painting the player a flowchart and without throwing the host
away when art lands.

**Answer, owner-directed 2026-09-05:** Phaser map island + existing React HUD. Typed GameObjects per
sector kind and lane kind. Endless Space structure. Plate 11 full cards stay in the inspector.

---

## The shape

### Dual-plane (the lawn's, not a new one)

```text
Server turn engine  →  REST/SignalR  →  adaptWorldState (pure)
                                          │
                    ┌─────────────────────┴─────────────────────┐
                    ▼                                           ▼
         React: StageHost, HUD,                      EventBus world:model
         inspector, turn, playback                   world:select / ready
                    ▲                                           │
                    └──────── world:select ─────────────────────┤
                                                                ▼
                                                   Phaser WorldMapScene
                                                   camera, SectorObject,
                                                   LaneObject, ForceObject
```

- **Model:** `SectorView`, `LaneView`, `ForceView`, `SlotView` — unchanged, sealed.
- **View-model:** `sectorChannels`, `laneChannels`, `fogTreatments` — unchanged, still Phaser-free.
- **Map view:** Phaser GameObjects, one factory per kind. Intel branches first (unknown is a
  different silhouette, never a zeroed card). Lane kind and lane state stack as separate graphics.
- **Chrome view:** today's React inspector and HUD. Selecting a pin emits `world:select`; React opens
  the band-2 inspector. Phaser does not draw the plate 11 card on the canvas.
- **Host:** `createWorldGame` → existing `createGame`. Folder law: Phaser under `src/game/world/`;
  React under `src/stages/world/`.
- **Lifetime:** create on entering the world stage, destroy on leaving it, survive the inspector
  (GG-11). Same as `LawnGameHost`.
- **Mediator:** a `world:*` bus beside the lawn union, sharing `allocGameGeneration`. Do not stuff
  world events into `LawnBusEvent`. Do not let Phaser call `lib/bus`.
- **Camera:** Phaser `Cameras.Scene2D`. Wheel zoom about pointer, drag pan, arrow pan (`W` is not
  pan — world-stage arbitration: arrows pan, `W` cycles). Fit control in the bottom-left cluster.
  Delete the unused SVG `viewBox` host and `camera.ts` / `cameraGestures.ts` once the Phaser camera
  is the one camera — two cameras is how this defect happened last time.
- **Tokens:** snapshot CSS custom properties once at boot into named Phaser colours. The `game/`
  hex-guard skip is because `var(--color-*)` cannot resolve inside WebGL (`hexGuard.ts:15-18`), not
  permission to invent a second palette.
- **Zoom LOD:** derive map/detail (and later constellation-style labels) from Phaser camera scale.
  Each tier is a strict superset of the one below. Ownership, health, net never drop.

### Map visual language (Endless Space, compact)

v1 chrome, art-ready (D10):

- Backdrop: designed grid or starfield placeholder, not `#000` void
- Sector: ownership ring + crest + name + pip row (five silhouettes). Unknown: other silhouette
- Lane: stroke language already specified (solid / dash / twin-rail / gap+✕ / arrow / lock) as
  Phaser graphics, with glow as a later art pass on the same objects
- Force: three shapes before three colours, on the pin or on the lane at `LaneProgressMilli`

The inspector remains the dense card. Duplicating plate 11 §A onto eighteen DOM nodes was the
flowchart look.

### What this rejects

| Option | Why not, long-term |
|---|---|
| Keep custom SVG and wire `cameraGestures` | Fixes zoom this week. Still HTML-in-`foreignObject` cards. Still thrown away when art/FX/fleets-on-lanes arrive. Sequencing already failed once |
| Bring `@xyflow/react` back for the player map | Best React MVVM for *editors*. Maintainers tell visualization authors to use canvas. `giant` (112–144) sits where complex DOM nodes hurt. Looks like a tool even at 18 |
| Cytoscape.js | Stylesheet glyphs, not a component per type |
| PixiJS beside Phaser | Second WebGL stack. Phaser 4.2 is already the lawn/siege/battle factory |
| One eternal `Phaser.Game` across lawn and world | Contradicts D2 and GG-1. One stage at a time; destroy on leave |

### What T3 still got right

xyflow is a node-editor. The player never drags a sector or draws an edge. Putting 41.4 KB gz on the
**entry** chunk was a GG-38 defect. Phaser must load with the **world stage chunk**, not boot.
Developer graph authoring may still use xyflow in a `dev` chunk.

---

## Tunables

No new power curve. No new stock.

| Number | Kind | Home |
|---|---|---|
| World size node ranges | Tunable (already) | `data/tuning/world.v5.json` `worldSizeNodes` |
| `GRID_X` / `GRID_Y` | Layout constants — would a balance pass change them? Only if the authored maps feel cramped. Prefer a named layout tunable if they move | today `WorldScene.tsx:19-20`; if they become feel, `world.v*.json` |
| Camera min/max scale | **Structural** — whether zoom still works as a control | Phaser camera config, commented as not a balance number (same test as `camera.ts:18-29`) |
| Lane stroke from `Width` per-mille | Already specified | existing width mapping; feel constants stay commented |
| Legion count 6–10 | Already a world-stage tunable | not this program's |

---

## What this deliberately does not decide

- Turn order, cede, recruitment, fog rules, supply math — `world-map-program` / `world-stage`
- Inspector field list — plate 11 §A / `spec-world-inspector`
- Whether `large`+ gets a minimap — still reopened with `world-generator`, as §4.2 already said.
  This runtime is chosen so that reopen is **culling and a possible minimap**, not a third renderer
- Illustration, shaders, nebula art — D10 later
- Siege/battle Phaser scenes sharing texture atlases with the world — nice, not required for v1

---

## Open questions

None. The owner pick is the shape above. Spec:
[world-map-runtime/spec-world-map-runtime.md](world-map-runtime/spec-world-map-runtime.md).
Build waits on that review.

---

## DESIGN-GATE §5 (this session)

Read in this session before proposing: `the-game.md`, `the-loops.md`, `CLAUDE.md` RPG-layer rule,
`DESIGN-GATE.md` index, `software-architecture.md` (top-level + DPLP row), `decisions.md` Product
vision / Standalone-first / Game GUI, `game-gui-principles.md` (GG-1, GG-11, GG-38, D2, D10),
`information-architecture.md` §1–§2.2, `design/README.md`, `fe-game-foundation.md` DPLP,
`world-map-program.md`, `world-stage-ideal.md` (renderer clauses), `world-stage-map.md` assumption 3,
`tech-stack.md` T3, `world-graph-ideal.md` L1 premise. Verified against `WorldStage.tsx`,
`camera.ts`, `cameraGestures.ts`, `WorldScene.tsx`, `scene.css`, `EventBus.ts`, `createGame.ts`,
`LawnGameHost.tsx`, `hexGuard.ts`, `WorldSizeCatalog.cs`, `world.v5.json`, `package.json`.

Honest gap: `software-architecture.md` was not re-read end-to-end; the web/Phaser row and overlay
principle were.
