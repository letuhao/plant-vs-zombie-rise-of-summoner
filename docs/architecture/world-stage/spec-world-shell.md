# Spec: world-shell

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-shell` in the
[world-stage capability map](../world-stage-map.md). **Level 2, depends on `world-contract`** — it is
built in parallel with `world-numbers`, and `world-render` and `world-hud` both sit on top of it.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.1, §4.2, §4.3, §8c.6, §8e.1.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §G.1, §G.2.

---

## Objective

Make the world map a **stage**: a component that fills the viewport, owns its own camera, and sits
inside a page that never scrolls. Everything above this module — nodes, HUD, inspector, lenses —
assumes a surface with those three properties, and none of them exists today.

Two defects, both structural, both cheap to state and expensive to leave:

### 1. The map's own bottom edge is below the fold before anything else renders

`WorldPage.tsx:222` sizes the canvas `h-[620px]`, inside `AppShell.tsx:30`'s
`<main className="min-w-0 flex-1 overflow-auto p-5">`. At the declared 1280×720 floor (GG-36's
measured range, from `OverlaySwitchLayout`) the shell's header band takes roughly 70px and `p-5`
takes 20px top and bottom, so the outlet has on the order of 680px of content height for a 620px map
plus the page's own banner, title and controls. **The map's bottom edge is already past the fold at
the floor, with an empty world.** The current answer is `overflow-auto` on `<main>`, which is not an
answer — it is the failure GG-36 forbids, dressed as a feature.

The fix is not a smaller number. It is that a stage is measured against the viewport, not against a
document: `StageHost` already renders `h-full w-full` (`stageHost.tsx:34`), so the stage inherits the
viewport's height and the map's extent is handled by the camera rather than by the page.

### 2. Two scroll models are fighting over the same wheel

`@xyflow/react` defaults `preventScrolling = true`
(`node_modules/@xyflow/react/dist/esm/index.js:1298`), and `WorldPage.tsx` never sets it. So the
wheel zooms the map when the pointer is over the canvas and scrolls the document when it is one pixel
outside — the same gesture, two meanings, decided by pointer position. There is no configuration of
those two systems that is correct, because the page should not scroll at all.

**Success is that at 1280×720 the whole stage is on screen with nothing clipped, `document` scroll
height equals viewport height, and every navigation gesture belongs to the camera.**

## Design

### 1. The stage mounts under `StageHost`, like the other two

`SanctumStage.tsx:182` and `LawnStage.tsx:74` both wrap their content in `StageHost` and call
`useStageMountGuard`. The world does the same, for the same reason: GG-11's guard asserts the stage
component is never recreated when a layer opens over it, and every band-2 surface this program builds
(the inspector, the confirms) depends on that promise holding.

`#/world` keeps working until the replacement lands (map assumption 2). The new stage lives at
`src/stages/world/`; the old `features/world/` tree is deleted in one commit when the route flips,
and its three exemptions — the hex guard (`hexGuard.ts:23-27`), GG-7 reachability, and the shell's
redirects — retire with it. Not before, and not one at a time.

### 2. The camera is an SVG `viewBox` transform, and that is the whole model

The stage renders one `<svg>` whose `viewBox="x y w h"` is the camera. Pan changes `x`/`y`; zoom
changes `w`/`h` about the pointer; nothing in the scene graph moves. This is one piece of state
(`{x, y, w, h}`), it is trivially testable without a DOM, and it makes the four navigation methods
§4.2 requires all express the same thing.

| Gesture | Effect | Note |
|---|---|---|
| Click-drag on empty map | Pan | Drag on a node selects instead; a drag threshold separates the two |
| Wheel | Zoom about the pointer | The page cannot scroll, so there is no second meaning to disambiguate |
| Arrow keys / WASD | Pan by a fixed step | The map's first keyboard affordance of any kind (§2.3) |
| Fit control | Zoom-to-extent | Bottom-left map-controls cluster, per §4.3 |

**Zoom is clamped, and the clamp is structural, not a balance number.** A camera that can zoom to a
single pixel or to a thousand screens of empty grid is a broken control, not a tuning choice — so the
limits stay `const` with a comment saying why, per [tunables-ssot.md](../tunables-ssot.md)'s own test
(*would a balance pass ever change this?* No — it changes whether the control works).

**Extent comes from the world, not from a layout pass.** Node positions are
`layoutX * GRID_X` / `layoutY * GRID_Y` (`worldViewModel.ts:9-11`, applied at `:287-345`), so the
extent is the bounding box of the authored grid plus a padding margin.

### 3. Deleting `@xyflow/react` is cheaper than it sounds, and the spec should say where it is not

Locked at `decisions.md:93` (*"drop `recharts` and `@xyflow`"*) and already the recorded acceptance
criterion at `tasks/game-gui-todo.md:616`. Four production files name it — `LaneEdge.tsx:2`,
`SectorNode.tsx:2` and `WorldPage.tsx:2-3` import it; `routes.tsx:9` names it in a comment about the
lazy chunk split. Two tests mock it: `SectorFog.test.tsx:12` and `SectorNode.test.tsx:18`.

**What costs nothing:** `worldViewModel.ts` is already library-agnostic. It declares its **own**
`SectorNode`, `LaneEdge` and `WorldGraph` types (`:180-195`) holding plain `{x, y}` from the authored
grid. There is no auto-layout to replace, no `dagre`, no measured-node feedback loop. The fold's
output is already the shape a hand-rolled renderer wants.

**What costs something, and is the one real migration risk in this module:** `LegionMarker.tsx`
animates along a `<path>` by id —
`document.getElementById(pathId)` (`:46`) → `getTotalLength()` (`:50`) → `getPointAtLength()` (`:55`),
writing `transform` inside a `requestAnimationFrame` loop so a marching legion costs zero React
re-renders. **The technique survives; the id contract does not.** `pathId` is documented as
*"the `<path>` element id React Flow gives this lane's edge"* (`:17-18`), and `LaneEdge` passes React
Flow's own `id` through at `:51`. When the library goes, nothing supplies those ids. `world-shell`
therefore declares the lane-path id scheme (`lane-path-${laneId}`) as part of the stage's DOM
contract, and `world-render` renders paths that honour it. Getting this wrong is silent: markers
simply never move, with no error.

### 4. No minimap — and the decision is scoped to the two available tiers

§4.2, matching all three Amplitude games. Six to ~18 sectors on an authored grid is inside what one
zoom-out shows whole, and a minimap is a second camera to keep in sync for no gain.

**The scope is load-bearing, not a hedge.** `WorldSizeCatalog.cs:48-58` declares five tiers.
`small` and `medium` are `Available = true`; `large` (~32), `huge` (~64) and `giant` (~128) are
`Available = false`, gated on `world-generator`, and their costs were **measured** rather than
guessed — the file's own comments record 64 nodes at ~52–80ms and 128 nodes at ~0.6–0.7s, the latter
needing a Tarjan-first optimisation before it ships. **The first tier above `medium` becoming
available reopens this decision and `world-outliner`'s shape together.** A camera model sized for ≲20
nodes is a rebuild at 64, and saying so now costs one sentence.

### 5. Zoom tiers simplify; they never remove a fact

§4.2's rule, and it is Endless Legend's documented failure: if zooming out hides tile yields while
revealing resource icons, the player oscillates between depths. **Each zoom tier is a strict superset
of the legibility below it.** Dropping labels for banners is allowed; dropping a fact that only the
other tier shows is not. `world-render` owns which rows drop at which tier; this module owns the tier
boundaries and the rule they must satisfy, and the test that proves it.

### 6. Esc, and the dismissal gesture the stage owes the layers above it

`keymap.ts` already has the machinery: `claimStageEscape` (`:113`) lets a stage register its own
close, `handleEscape` (`:125`) pops one layer. §4.4 requires **one gesture, no exceptions**: Esc pops
one layer, and right-click on the map pane does the same. The stage registers both and dispatches
`select-sector: null` when the stack is empty — which is the dead end the current map has, where
nothing ever clears a selection.

### 7. The unlisted dependency: the layer stack lives inside `SanctumStage`

`LawnStage.tsx:45` records it plainly — the rail's layers only exist inside `SanctumStage.tsx`,
because its `mountedLayers` gate is not shared globally, and *"moving the whole layer stack up to
AppShell is real future scope, not this task's."* The Lawn's answer was to navigate to where the
layer lives rather than open it in place.

**A world stage that opens a band-2 inspector faces the same fork, and it must be decided here rather
than discovered in `world-inspector`.** Two options, and this spec recommends the first:

1. **The world stage owns its own layer gate**, exactly as the Lawn worked around it — one
   `mountedLayers`-shaped set local to `stages/world/`. Costs a small duplication; blocks nothing;
   keeps this program inside its own boundary.
2. **Lift the layer stack to `AppShell`.** The right end state, and out of scope: it changes the
   Sanctum and the Lawn, and it is a shell refactor, not a world feature.

Recommending (1) is not deferring the problem — it is refusing to make a two-stage refactor a
prerequisite of a map. The duplication is one `useState<Set<string>>` and is deleted by whoever does
the lift.

## What stays out

- **Drawing anything.** No sector node, no lane, no fog treatment — `world-render` owns all of it.
  This module renders an `<svg>` with a camera and a slot for a scene.
- **The HUD.** `world-hud` owns band 1 and the corner roles. This module owns only the fact that band
  1 is anchored to the viewport rather than to the scrolling document.
- **The inspector.** `world-inspector` owns the left-docked shell (§8e.1); this module owns the
  stage's guarantee that opening it never makes the stage scroll (GG-61).
- **The minimap.** Not built, by decision. Recorded here so nobody re-derives it.
- **Any tier above `medium`.** Gated on `world-generator`, wave 4.

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run
npm run build            # tsc --noEmit && vite build
npm run lint
npm run test:e2e         # the viewport sweep lives here
```

The hex guard and the contract guard both run inside `npm test`, so a violation fails the suite
rather than needing a separate script.

## Project structure

```
web/fusion-rpg-web/src/
  stages/world/
    WorldStage.tsx          → StageHost + useStageMountGuard + the camera host
    WorldStage.test.tsx
    camera.ts               → viewBox state, pan/zoom/fit — pure, no DOM
    camera.test.ts
    cameraGestures.ts       → wheel / drag / key → camera ops
    cameraGestures.test.ts
    stageIds.ts             → the DOM id scheme, incl. `lane-path-${laneId}`
  features/world/           → deleted when the route flips, not before
web/fusion-rpg-web/src/theme/hexGuard.ts   → `features/world/` exemption removed with it
package.json                → `@xyflow/react` removed from dependencies
```

## Code style

The camera is data and pure functions; the component is a thin host. Nothing in `camera.ts` touches
the DOM, so the whole navigation model is testable without a browser — the same split
`worldViewModel.ts` already uses and §7 of the ideal says survives.

```ts
/** The camera. One object, and the only thing pan and zoom mutate. */
export type Camera = { x: number; y: number; w: number; h: number };

/**
 * Structural, not tunable: a camera that can zoom to one pixel or to a thousand
 * screens of empty grid is a broken control, not a balance choice.
 * (tunables-ssot.md's own test — this changes whether the control works, not how
 * the game feels.)
 */
const MIN_SCALE = 0.35;
const MAX_SCALE = 2.5;

export function zoomAbout(cam: Camera, viewportW: number, pointer: { x: number; y: number }, factor: number): Camera;
export function fitToExtent(extent: Rect, viewport: Size, padding: number): Camera;
```

## Testing strategy

Vitest, colocated, plus one Playwright sweep. Four levels, and the last two are the ones that would
catch a regression nobody would otherwise see:

1. **Camera math** — `pan`, `zoomAbout`, `fitToExtent` and the clamps, against a fixed extent. Zoom
   about a pointer keeps the world point under the pointer fixed; that is one assertion and it is the
   whole correctness of wheel zoom.
2. **Gestures** — wheel → zoom, drag on empty map → pan, drag on a node → select not pan, arrow keys
   → pan by one step, `fit` → the full extent visible. Dispatched as events against the host, so the
   drag-threshold split between pan and select is proven rather than assumed.
3. **No page scroll** — at 1280×720 and 1440×900: `document.scrollingElement.scrollHeight` equals the
   viewport height, and no element reports horizontal overflow. This is GG-36's own testable form
   (*"the page never scrolls horizontally"*) and it is the acceptance criterion for defect 1 above.
4. **The path-id contract** — a test asserts that for every lane in the fixture there is a DOM element
   whose id matches `stageIds.lanePath(laneId)`, and that `LegionMarker` finds it. Without this, the
   library removal breaks legion animation silently: no error, no exception, markers that never move.
5. **Mount guard** — `getStageMountCount("world")` stays at 1 across opening and closing a band-2
   layer. GG-11's promise, asserted for this stage the way the other two already assert it.

`SectorFog.test.tsx` and `SectorNode.test.tsx` are rewritten without the library mock as part of this
module, not left to `world-render` — they are the two files that would otherwise keep
`@xyflow/react` in `package.json` after every production import is gone.

## Boundaries

- **Always:** keep the camera pure and the host thin; render under `StageHost`; measure against the
  viewport, never against a document; give every navigation method the same camera state; delete the
  old tree and its three exemptions together.
- **Ask first:** anything that changes `AppShell`'s layout for other stages — in particular lifting
  the layer stack out of `SanctumStage`, which is the Lawn's recorded future scope and touches three
  stages. Also any camera capability sized for a tier above `medium`; that reopens §4.2.
- **Never:** reintroduce a scrolling `<main>` under a stage. Never add a minimap. Never let a wheel
  gesture mean two things depending on pointer position. Never bind a component in `stages/world/` to
  a REST DTO (`world-contract`'s guard, widened per §8e.2). Never leave a `@xyflow/react` import
  behind a test mock.

## Success criteria

1. `#/world` renders a stage under `StageHost` that fills the viewport at 1280×720 with nothing
   clipped, and `useStageMountGuard("world")` stays at 1 across a layer open/close.
2. **The page does not scroll** — proven by an automated check at 1280×720 and 1440×900, vertically
   and horizontally.
3. Click-drag pan, wheel zoom, arrow-key pan and fit-to-extent all drive one `Camera`, and zoom about
   the pointer is asserted to keep the pointed-at world coordinate fixed.
4. `@xyflow/react` appears nowhere: not in `package.json`, not in a production import, not in a test
   mock. `grep -r "@xyflow" web/fusion-rpg-web/src` returns nothing.
5. The lane-path id contract is declared in `stageIds.ts`, honoured by the renderer and asserted by a
   test, so `LegionMarker` keeps working across the migration.
6. Esc and right-click both pop exactly one layer, and clearing the last one dispatches
   `select-sector: null` — the dead end the current map has is gone.
7. `features/world/` is deleted, and its hex-guard exemption (`hexGuard.ts:23-27`) is deleted in the
   same change.
8. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §4.2 decided the camera and the absence of a minimap; §8e.1 decided the inspector's edge;
`decisions.md:93` decided the library. The one fork this module faced — whether to duplicate the
Lawn's layer-gate workaround or lift the stack to `AppShell` — is answered above with a
recommendation and its cost, and the alternative is listed under **Ask first** rather than left open.
