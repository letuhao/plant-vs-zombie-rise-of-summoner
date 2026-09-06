# Spec: world-map-runtime

**Status: Specify complete 2026-09-06 — HOW locked.** Shipped R0–R16 did **not** close success
criteria 1–11; completion contract is
[spec-world-map-gaps.md](spec-world-map-gaps.md) (Specify awaiting owner review).

**Status (original):** Specify complete 2026-09-06 — open questions locked; build authorized at R0/R1
([world-map-runtime-plan.md](../../../tasks/world-map-runtime-plan.md)). Module id
`world-map-runtime` in the [world-map-runtime capability map](../world-map-runtime-map.md).

**Ideal:** [world-map-runtime-ideal.md](../world-map-runtime-ideal.md).
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §O (pin, LOD, dual-plane);
§A card remains the inspector; §B remains the lane-stroke **legend**.
**Foundation copied, not reinvented:** [fe-game-foundation.md](../fe-game-foundation.md) (DPLP).
**Visual / stage rules:** [game-gui-principles.md](../game-gui-principles.md) GG-1, GG-11, GG-18,
GG-27, GG-29, GG-38, GG-50, D2, D10.
**Sibling HOW still owned elsewhere:** [spec-world-render.md](../world-stage/spec-world-render.md)
(channels, fog treatments, type floor); [spec-world-shell.md](../world-stage/spec-world-shell.md)
(StageHost, gestures as product rules); [spec-world-inspector.md](../world-stage/spec-world-inspector.md)
(left dock); [spec-world-lenses.md](../world-stage/spec-world-lenses.md) (picker + six encodings);
[spec-world-targeting.md](../world-stage/spec-world-targeting.md) (pure `worldSelection`).

This spec is the software architecture for the Phaser world map: **which patterns, how data flows,
where code lives.** It does not respecify HUD copy, inspector fields, or `step()`.

---

## Objective

Put the player on the rift the way Endless Space 2 puts them on a galaxy: **typed pins, typed
starlanes, a camera they drag and wheel, detail in a panel.** The player map HOW is Phaser
dual-plane under `src/game/world/` (T3 amended by R16). The SVG `viewBox` camera and React
`WorldScene` composer are **retired leftovers** — closing them is
[spec-world-map-gaps.md](spec-world-map-gaps.md) D19, not a second architecture choice.

**User.** The summoner on World — adventure and empire loops
([the-loops.md](../../guide/the-loops.md) §4–§5). Not a level author. Not a graph-editor user.

**Success.** Opening World creates one Phaser.Game for that stage, destroys it on leave, and never
destroys it when the inspector opens (GG-11). Pins match §O. The inspector still shows the §A card.
Phaser never calls `lib/bus`. React never holds GameObject refs. **One map camera** owns every
map-plane overlay (range, routes, supply, lifelines, lenses, blocked marks) — React SVG overlays
must not float over a Phaser camera. Parent SC 1–12 vs shipped code: see the gaps coverage matrix.

---

## Tech stack

| Piece | Choice | Why |
|---|---|---|
| Map renderer | **Phaser `^4.2.1`**, already in `package.json` | Lawn/siege/battle factory exists (`createGame.ts:3-9`) |
| Chrome | Existing React HUD / inspector | Dual-plane already locked |
| View-model | Existing `sectorChannels` / `laneChannels` / `fogTreatments` | Pure, unit-tested, **no Phaser types** |
| Host factory | `createGame` + thin `createWorldGame` | Same as `createLawnGame.ts:22-24` |
| Scene list | **`WorldMapScene` only** in v1 | Lawn `BootScene` exists for an icon atlas; world v1 is framed placeholders in `create` (D10). Revisit Boot when art lands |
| New npm packages | **None** | Pixi would duplicate WebGL; xyflow is a node-editor; Cytoscape is a stylesheet |

Phaser 4 marketing sometimes says "ECS core." **This repo's 4.2 island is Scenes + GameObjects +
ordered systems** (`LawnWorldScene.ts:68-70`: Sync → Layout → StatusFx → Pick). This module copies
that, and does **not** add bitECS, a third-party ECS, or a Phaser UIScene.

**GG-38:** World is already `lazy()` in `routes.tsx`. Only `WorldGameHost` may import
`createWorldGame` / Phaser. Phaser stays in the World chunk — never StageHost, never the entry
bundle.

---

## Software architecture

### Layers (four, one direction)

```text
Server turn engine ── REST/SignalR ── lib/bus (React only)
        │
        ▼
  adaptWorldState  →  SectorView / LaneView / ForceView     (pure; sealed contract)
        │
        ▼
  sectorChannels / laneChannels / fogTreatments             (pure; Phaser-free)
        │
        ├──────────────────────────────┐
        ▼                              ▼
  React WorldStage               EventBus world:model
  HUD, inspector, lenses picker,        │
  turn, commands via lib/bus            ▼
        ▲                         Phaser WorldMapScene
        └──── world:select ──     camera, pins, lanes, overlays, pick
```

| Layer | Owns | Must not own |
|---|---|---|
| **Server / `FusionRpg.Data`** | `step()`, fog rules, loam math | Pixels, cameras |
| **React chrome** | StageHost, HUD, inspector, hotkeys, `lib/bus` commands; may hold live `WorldStateDto` then adapt | GameObjects, RAF game loop |
| **Pure view-model** | Channels, zoom-tier function, camera *math* if still needed for tests | Phaser imports, `fetch` |
| **Phaser island** | Scenes, GameObjects, pointer, wheel, draw, map-plane overlays | `lib/bus`, SignalR, `*Dto`, React |

This is DPLP with `lawn:*` renamed `world:*`. It is **not** a third plane.

**Adapter lock (corrected):** wire `*Dto` types already enter `stages/world/` at the host
(`WorldStage.tsx` holds `live.data` then calls `adaptWorldState`). The real boundary is
**`game/` never sees `*Dto` or `lib/bus`**. Saying "DTOs never enter `stages/`" is false against
shipped code and must not be re-asserted.

**RPG-layer rule:** the map is a web stage. Fusion `Plant` fields do not constrain pins or cameras.

### Folder law

```text
web/fusion-rpg-web/src/
  game/                          # Phaser island — no React imports (RT-09 analogue)
    EventBus.ts                  # lawn:* AND world:* unions; shared allocGameGeneration
    createGame.ts                # unchanged generic factory
    createWorldGame.ts           # Facade: WorldMapScene only + destroy checklist
    world/
      snapshotTheme.ts           # read CSS vars once → named Phaser colours (incl. --soil backdrop)
      layout.ts                  # GRID_X/Y, world position from layoutX/Y (pure)
      zoomTier.ts                # scale → "fit" | "map" | "detail" (pure); FIT_MAX / DETAIL_MIN
      entities/WorldRegistry.ts  # sectorId / laneId / forceId → GameObject
      objects/sectorPin.ts       # factory from Channels + fog + zoom tier
      objects/laneStroke.ts
      objects/forceMarker.ts
      systems/syncWorldSystem.ts # modelSeq-monotonic apply
      systems/worldPickSystem.ts
      systems/worldCameraSystem.ts   # pan / zoom / Fit / edge-scroll / LOD
      systems/worldOverlaySystem.ts  # routes, range, supply, lifeline, lens, blocked
      scenes/WorldMapScene.ts    # persistent while World is current; no WorldBootScene in v1
  stages/world/
    host/WorldGameHost.tsx       # Facade host (copy LawnGameHost lifetime); only Phaser importer
    WorldStage.tsx               # mounts host instead of <svg>
    render/sectorChannels.ts     # STAYS — never import Phaser; game/world MAY import these
    render/fogTreatments.ts      # STAYS — Phaser-free; pin uses a density-dropped subset
    render/SectorNode.tsx        # RETIRE from the stage (inspector does not use it)
    render/SupplyOverlay.tsx     # RETIRE from the stage (encoding oracle until Phaser tests)
    render/LifelineOverlay.tsx   # RETIRE from the stage (same)
    targeting/RangeOverlay.tsx   # RETIRE from the stage (geometry stays in worldSelection)
    camera.ts / cameraGestures.ts# DELETE once Phaser camera is the one camera
```

Do **not** nest Phaser under `stages/world/phaser/`. Do **not** add a `WorldBootScene` in v1.

### Import law (copy the lawn — do not invent a stricter plane)

Live lawn already imports view-model from outside `game/`:
`LawnWorldScene.ts` pulls `@/features/lawn/lawnViewModel`. World does the same for channels.

| `game/world` **may** import | `game/world` **must not** import |
|---|---|
| `@/contract/types` (views only) | React / JSX |
| `stages/world/render/sectorChannels` (and siblings: `laneChannels`, `fogTreatments`, `slotSilhouettes`) | `@/lib/bus`, any `*Dto` |
| Phaser, `@/game/*` | `WorldStage`, `WorldGameHost`, inspector / HUD components |

**Reject a second paint-descriptor type for v1** — two matrices will drift. Prefer one channel matrix
consumed by both React (until retired) and Phaser factories. If a future guard wants
`game/` ↛ `stages/`, **move** the channel files to `src/lib/world-view/` in a later slice; do not
duplicate them now.

### Lifetime (D2 + GG-11)

- Create `Phaser.Game` on entering the World stage (`WorldGameHost` mount).
- Destroy on leaving World (`destroyWorldGame`: tweens → scene shutdown → `world:destroyed` →
  `game.destroy(true)`). Copy `createLawnGame.ts:26-48`.
- **Never** destroy when the inspector, confirm, or playback layer opens.
- GG-1: lawn Game and world Game never coexist. Switching Sanctum ↔ World ↔ Lawn tears down the
  previous island.

### Systems allow-list (RT-08 analogue)

Ordered pipeline on `WorldMapScene`, copying the lawn's "no God Scene" rule:

1. **Sync** — apply `world:model` when `modelSeq > lastApplied`.
2. **Camera / LOD** — pan, zoom, Fit, edge-scroll, `zoomTier` pin updates.
3. **Pick** — hit-test with chrome occlusion; emit `world:select`.
4. **Overlay** — routes, range rings, supply envelope, lifeline halo, six lens drawings, blocked mark.

A **fifth** system is a sentence in this spec (or an ADR), not a convenience class.

### Camera, pick, and chrome occlusion

Phaser `Cameras.Scene2D` is the only camera. Ultrawide: extra width is more map (`Scale.RESIZE`);
Fit padding uses HUD safe insets; no letterbox. Backdrop colour comes from `snapshotTheme`
(`--soil`), not the hardcoded `#16120e` in `createGame.ts` alone.

| Gesture | Owner | Note |
|---|---|---|
| Drag empty map | Phaser | Named structural pixel threshold (copy `cameraGestures` intent) so a click still selects |
| **Edge-scroll** | Phaser | Plate I.3 / ES2+EL: pointer near viewport edge pans while map owns input |
| Wheel | Phaser | Zoom about pointer; page does not scroll (`world-shell` already forbids document scroll) |
| Pin click | Phaser → `world:select` | React opens inspector |
| Right-click / contextmenu on canvas | Phaser | `preventDefault` + emit `world:select` `kind: "empty"` (match `WorldStage` `handleEscape`) |
| Arrows | React keymap → `world:camera` | Pan only. **Not** pin-to-pin hop (plate §O.6). `W` stays cycle. GG-18: if a layer owns input, do not emit pan |
| Fit, +/− | React HUD cluster → `world:camera` | Bottom-left, plate §I |

**Hover ≠ focus outline ≠ selection halo** (plate §O.3 / §O.6):

- **Hover** highlight — Phaser-local only. No `world:hover` bus event.
- **Keyboard / gamepad spatial focus** outline — Phaser-local; gamepad focus is later, not a v1 bus event.
- **Selection** halo — driven by `world:interaction` / pick → React selection. Esc deselects then pops.

**Hit target.** Pin disc is **44px** at map zoom (structural a11y const, plate §O.6). Name and pips
are labels, not the only hit area.

**Pick ignores chrome rectangles** (pins may still **draw** under panels — GG-11 keeps the stage
mounted):

- Layer rail: `w-[92px]` (`Rail.tsx`).
- Left inspector dock when open: ~380px beside the rail (`spec-world-inspector` §8e.1 — **left**, not
  the plate §O composed mock's right).
- HUD corner clusters (top strip, bottom-left map controls, turn cluster, outliner).

Host (or React) publishes the current ignore rectangles via `world:interaction` (or a dedicated
payload field) whenever dock / HUD geometry changes. Pick tests screen-space pointer against those
rects before resolving a pin.

`world:resized`: copy the lawn path — React `ResizeObserver` on the host parent emits
`world:resized`; scene applies size. Do not rely on `Phaser.Scale.RESIZE` alone without the host
signal.

No minimap on `small`/`medium`. Reopen with `world-generator` + outliner, not a third renderer.

Port structural clamps from `camera.ts:23-29`: `MIN_SCALE = 0.25`, `MAX_SCALE = 4`, commented as
**not tunable**. Then **delete** `camera.ts` / `cameraGestures.ts` so a second camera cannot rot
beside the first.

### Tokens (GG-29)

`hexGuard.ts:15-18` skips `game/` because `var(--*)` cannot resolve in WebGL. That is not a second
palette. `snapshotTheme.ts` reads computed CSS custom properties **once at Game boot** (and on theme
change if the app ever gains one) into a `WorldTheme` record — colours **and** the font family used
for fact-bearing Phaser `Text` (CJK must match HUD). Object factories take `WorldTheme`, never a hex
literal authored in `game/world/`.

### Type floor

`spec-world-render` §7: no fact-bearing glyph uses `--text-2xs` / `--text-xs`; `--faint` is
decorative only. Phaser inherits the same floor: fact-bearing `Text` ≥ **12px at 720p**
(`--text-sm`). Detail-only labels that cannot meet the floor **do not exist** — they stay in the
inspector.

### GG-50

Pins are GameObjects, not a scrollable list. The **outliner** is already the collection surface.
This module adds **no** new `COLLECTION_SURFACES` row. Giant-tier cost remains topology
(`WorldSizeCatalog`), not pin-list virtualization.

---

## Design patterns — how many, and why

**Eleven in.** Copy the lawn catalog, add one Factory. **Eight out.** Nystrom's book
([gameprogrammingpatterns.com](https://gameprogrammingpatterns.com/contents.html)) is a menu, not a
checklist — using all of them is how a 6–18 node map grows a second engine.

Prior art that agrees, used only where it matches DPLP:

- Phaser + React: canvas for the world, DOM for chrome, **one EventEmitter**, React never mutates
  GameObjects ([Phaser official React template](https://github.com/phaserjs/template-react-ts);
  production overlay pattern).
- Turn-based Phaser: dumb display scenes + a controller outside the Scene; global UI event bus
  ([Phaser discourse, TBS lessons](https://phaser.discourse.group/t/lessons-learned-from-building-a-turn-based-strategy-game-in-phaser/12265)).
  Our "controller" is React `WorldStage` + `worldUiReducer`, not a second Scene.
- Dedicated Phaser UIScene: **rejected here** because the HUD already exists in React and must
  survive camera zoom (DPLP §9: *No Phaser HUD scene for Almanac chrome*).

### The eleven we use

| # | Pattern | Nystrom / GoF name | Where | Why this map needs it |
|---|---|---|---|---|
| 1 | **Dual-plane hybrid** | — (DPLP) | React chrome / Phaser island | Inspector is HTML; pins must pan at 60 Hz. One plane fails one of those |
| 2 | **Unidirectional data flow** | — | model → bus → scene; pick → React; commands → `lib/bus` | Stops a second sim and Phaser `fetch` |
| 3 | **Adapter / Projector** | Adapter | `adaptWorldState`, channel functions | Wire DTOs stop at the React host; `game/` sees views + channels only |
| 4 | **Mediator + Event Queue** | Observer + Event Queue | `world:*` on `EventBus.ts` | Decouples host mount from Scene; generation drops StrictMode ghosts (RT-02) |
| 5 | **Facade** | Facade | `createWorldGame`, `WorldGameHost` | One destroy checklist; Scene stays dumb |
| 6 | **Registry** | — (DPLP lite ECS) | `WorldRegistry` | Diff pins by `sectorId` without walking the display list |
| 7 | **Update Method / Systems** | Update Method | Sync → Camera/LOD → Pick → Overlay | Prevents a God Scene (allow-list above) |
| 8 | **Observer** | Observer | React Query/`lib/bus` → host emit `world:model` | Phaser does not subscribe to SignalR |
| 9 | **Factory + Type Object** | Type Object / Factory | `sectorPin`, `laneStroke` keyed by intel × ownership × health × kind | Typed views without subclass trees; unknown is a **different factory path**, not a dimmed disc |
| 10 | **Command** | Command | React inspector → `lib/bus` world commands | Server may reject; Phaser never Admit |
| 11 | **Dirty Flag** | Dirty Flag | host `modelSeq` + zoom-tier change | Do not rebuild 18 pins every frame |

**Type Object** is the catalogs we already have (`LaneType`, intel enum). The Factory maps those
types to GameObjects. We do not author a parallel type system in Phaser.

### The eight we refuse

| Rejected | Why |
|---|---|
| Full ECS library / "Phaser 4 rewrite" | Overkill vs 6–18 nodes; fights the lawn island we already paid for |
| Flyweight | 18 pins are not 10,000 trees. Adds indirection, saves nothing |
| Spatial partition | Same: giant-tier cost is **topology** (`WorldSizeCatalog.cs:56-57`), not draw |
| Object pool (v1) | Lawn uses it for hit FX. March trails can add it later on the same objects (D10) |
| Singleton besides the bus module | A `WorldManager.getInstance()` becomes the God Object |
| Service Locator soup | Phaser `registry` holds `generation` + `WorldTheme` only |
| Redux/Zustand as sprite SSOT | Dupes the registry; fights the frame loop (DPLP rejection) |
| Phaser UIScene / DOM in `foreignObject` | HUD is React; cards-on-canvas was the defect |

Bytecode, Prototype (as cloning GameObjects), Double Buffer, Data Locality: not applicable at this
scale. **State** for interaction already lives in `worldUiReducer` — do not duplicate it inside the
Scene.

**How many is enough:** eleven named, eight named-no. A twelfth needs a sentence in this spec, not a
convenient class.

### DPLP RT mapping

| ID | Verdict for world map | Lock |
|---|---|---|
| RT-01 | **Hold** (renamed truths) | Content SSOT = adapted world view the host publishes; Registry = view mirror; selection = `worldUiReducer` (clears when id absent from model) |
| RT-02 | **Hold** | Per-Game `generation`; buffer until `world:ready`; drop foreign gen; destroy checklist |
| RT-03 | **N/A** | No Unity `ptr` reuse on the web map |
| RT-04 | **N/A** | No Occupant / spawn ghost on the world graph |
| RT-05 | **N/A** | No Snapshot+event dual living sets — REST adapted state is the feed |
| RT-06 | **N/A** | No lawn InteractionMode / SpawnTargeting |
| RT-07 | **Hold** | Tweens → registry clear → listeners → `game.destroy(true)` |
| RT-08 | **Hold** | Allow-list: Sync, Camera/LOD, Pick, Overlay — else a sentence in this spec |
| RT-09 | **Hold** (with import law) | Phaser under `src/game/`; React under `stages/world/`; channels importable as lawn VM is |
| RT-10 | **Hold** | Monotonic `modelSeq`; ignore `modelSeq <= lastApplied`; optional rAF coalesce |
| RT-11 | **Hold** | Messages carry `generation`; host create/destroy idempotent |
| RT-12 | **Hold** | Commands ack from observe / HTTP error; Phaser never fakes Accept |
| RT-13 | **Hold** | Publish by replace + bump `modelSeq`; read-only after publish |
| RT-14 | **N/A** | No Bound / Cleared Binding lag on sectors |
| RT-15 | **Break rejection** | No FE prediction of turn outcomes |

---

## Dataflow

### Events (generation-scoped)

Do **not** widen `LawnBusEvent`. Add a parallel union and `worldBusOn` / `worldBusEmit`. Share
`allocGameGeneration()`. Tests call `worldBusClearAll()` without wiping lawn listeners.

| Event | Direction | Payload (min) |
|---|---|---|
| `world:model` | React → Phaser | `{ generation, modelSeq, model }` — `model` is adapted sectors/lanes/forces **plus** any lens-4 lifeline/supply inputs the overlays need; **not** a DTO |
| `world:select` | Phaser → React | `{ generation, kind: "sector" \| "lane" \| "force" \| "empty", id? }` |
| `world:camera` | React → Phaser | `{ generation, op: "pan" \| "zoom" \| "fit" \| "centre", ... }` — `centre` is world coords for Outliner Enter (gaps D25); arrows stay pan-only |
| `world:lens` | React → Phaser | `{ generation, lens }` — picker stays React; drawing is Phaser |
| `world:interaction` | React → Phaser | `{ generation, selectedId, targeting?, ignoreRects? }` — selection halo, route preview from `worldSelection` **points**, chrome occlusion |
| `world:ready` | Phaser → React | `{ generation }` — host flushes buffered model |
| `world:resized` | React → Phaser | `{ generation, width, height }` — ResizeObserver on host parent |
| `world:destroyed` | Phaser → React | `{ generation }` |

Buffer `world:model` / `world:interaction` / `world:lens` until `world:ready`, matching
`LawnGameHost.tsx:77-80`. Drop foreign `generation`.

### Dirty flag — host `modelSeq`, not a DTO revision

**Verified gap this revision closes:** `AdaptedWorldState` has no `revision`
(`adapt.ts` returns `{ sectors, lanes, slotsBySectorId, forcesBySectorId }`). `WorldStateDto` has
**no** `Revision`. `WorldHeaderDto.Revision` exists and bumps on **turn advance**
(`RpgStore.WorldTurns.cs`), which is the wrong grain for pin sync (lens-4 `lifelines=true` refetch
does not bump it).

| Token | Role |
|---|---|
| Host **`modelSeq`** (monotonic int) | Dirty flag for Phaser Sync. Increment whenever the payload about to emit differs (adapted graph **or** overlay inputs). Phaser `lastApplied` keys off this |
| `WorldHeaderDto.revision` | Turn-advance watermark for HUD / playback. **Insufficient** alone for pin dirty-flag |
| Wire `WorldStateDto.Revision` | **Do not add** in this program (wire change, ask-first) |

### Sync algorithm

1. Host emits `world:model` with a bumped `modelSeq` when the adapted payload (or overlay inputs)
   change. Publish by **replace**, not in-place mutate (RT-13).
2. `syncWorldSystem` no-ops if `modelSeq <= lastApplied`.
3. **Intel first** — `channelsFor` already branches on `intel` (`sectorChannels.ts`). Unknown →
   diamond factory; never "empty payload ⇒ unexplored" (`WorldEndpoints` still serialises defaults).
4. Upsert pins/lanes/forces in `WorldRegistry`; destroy ids absent from the model.
5. Positions: `layoutX * GRID_X`, `layoutY * GRID_Y`. Lane endpoints are **pin centres**, not
   top-left (`WorldScene.tsx:128-129` is the live miss; range overlay already uses centres).
6. Mid-lane forces: Phaser curve `getPointAt(progressMilli / 1000)`; **linear** v1 (no extra easing).
   On each `modelSeq` apply, **set** position — do not tween across a lane that disappeared. The SVG
   `getElementById(pathId)` contract in `spec-world-shell` **dies with the SVG**.
7. On camera scale change, recompute `zoomTier(scale)` and update pin LOD in place (strict
   supersets, plate §O.4). Do not rebuild the registry.
8. Overlay system redraws from the same applied model + `world:lens` / `world:interaction`.

### What stays React-only

Commands, End Turn, inspector body (§A card), outliner list, lens **picker**, confirm dialogs,
playback transcript. Route **geometry** is computed in `worldSelection.ts` (pure) and **drawn** by
Phaser from `world:interaction`.

### What moves onto Phaser (one camera)

Pins, lanes, force markers, fog-on-pin, **RangeOverlay**, queued-route flags, **BlockedTarget**
mark, **Supply** envelope, **Lifeline** halo, all **six lens** drawings
(`spec-world-lenses` encodings). React overlay components retire from the **stage** the same way
`SectorNode` does; keep their tests as encoding oracles until Phaser descriptor tests replace them.
`SupplyOverlay` / `LifelineOverlay` are already tested but not composed into live `WorldScene` —
that is a **wiring gap**, not "out of scope."

---

## Visual contract (map plane)

Inherited from plate §O and `spec-world-render` **channels**, not from `SectorNode` markup.

| Fact | Pin encoding (two+ channels, GG-27) |
|---|---|
| Ownership | Ring treatment + crest + word (detail) / ring+crest (fit) |
| Health | Hatch / inset bar / badge — **never opacity** |
| Unknown | Diamond silhouette; no name, no fake pips |
| Slots | Pip shapes at detail; dots at map; hidden at fit |
| Net loam | Chip at detail only; inspector always has the full yield |

`sectorChannels` today types `shape: "card" \| "unknown"`. The map Factory maps `card` → disc pin
and `unknown` → diamond. **Do not** retitle the inspector to a pin. Do not add Phaser types to
`sectorChannels.ts`.

### Fog on a 44px pin — drop density, never drop identity

`fogTreatments.ts` defines four treatments for the **card**. A compact pin cannot carry the forces
strip or full stamp prose. Fog and ownership still **do not share a channel**.

| Intel | Pin (fit / map / detail) | Inspector (unchanged) |
|---|---|---|
| Unknown | Diamond; no wash, no stamp, no name | n/a / empty |
| Rumored | Disc + ragged ring + hearsay pip (map+); wash only at detail | full torn + strip |
| Scouted | Disc + doubled ring + dated pip (map+); parchment wash at detail | full parchment + strip |
| Watched | Disc + ownership/health only | full card |

**Forces strip never on the pin** — "who stands here is not known" lives in the inspector only.

Lanes: existing `laneChannels` stroke language as `Graphics` (solid / dash / twin-rail / gap+✕ /
arrow / lock). Width from `Width` per-mille stays the existing mapping.

Forces: three shapes before three colours; mid-lane as Sync step 6 above.

### Display list (back → front)

1. Backdrop (theme `--soil` / placeholder grid)
2. Lanes
3. Pins (incl. fog-on-pin treatments)
4. Force markers
5. Lens / supply / lifeline overlays
6. Range rings / queued routes / blocked marks

D10: designed placeholders (grid/starfield, framed pins). Art swaps textures on the same objects.

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test
npm run build
# No npm run lint — package.json has no lint script (same as world-stage).
npx playwright test e2e/world-map-runtime.spec.ts
```

Guards (repo root, same as CI):

```powershell
.\scripts\guard-dal.ps1
# hex: game/ stays skipped; snapshotTheme must be the only colour ingress
rg -n "SKIPPED_PATH_PREFIXES" web\fusion-rpg-web\src\theme\hexGuard.ts
```

Phase checkpoints also require agent CV on PNGs under `e2e/.artifacts/world-map-runtime/` (plan CPA–CPD).

No `dotnet` work. No injector work. No wire-shape change (`WorldStateDto.Revision` not added).

---

## Project structure

See **Folder law** above. Tests colocated:

| File | Proves |
|---|---|
| `game/world/zoomTier.ts` + `.test.ts` | Strict supersets; named `FIT_MAX` / `DETAIL_MIN`; fit hides labels, never ownership/health/unknown |
| `game/world/layout.ts` + `.test.ts` | Centres, not top-left; GRID documented |
| `game/world/objects/*.test.ts` | Channel matrix → factory options; intel-first; fog-on-pin density; no `opacity` field |
| `game/EventBus.ts` world tests | Foreign generation dropped; lawn union unchanged |
| `game/createWorldGame.ts` + `createGame` mock | Same jsdom mock as `createGame.test.ts:8-13` — **no real `Phaser.Game`** |
| `stages/world/host/WorldGameHost.test.tsx` | Buffer until ready; `modelSeq` bumps; unmount runs destroy |
| Guard | `game/world/**` does not import `@/lib/bus`, React, or `*Dto`. Channel imports from `stages/world/render/*` **are allowed** (copy lawn) |

---

## Code style

Channel assignment stays a pure function. The Factory is a thin mapping. The Scene does not
`switch (dto.typeId)`.

```ts
/** Map-plane LOD. Structural thresholds, not tunables. */
export type ZoomTier = "fit" | "map" | "detail";

/** Structural: below this scale → fit. Control, not feel — not a balance tunable. */
export const FIT_MAX = 0.55;
/** Structural: at/above this scale → detail. Control, not feel. */
export const DETAIL_MIN = 1.25;

export function zoomTier(scale: number): ZoomTier {
  if (scale < FIT_MAX) return "fit";
  if (scale < DETAIL_MIN) return "map";
  return "detail";
}

/**
 * Pin factory. `channels` comes from sectorChannels (Phaser-free).
 * Unknown is a different silhouette — never a dimmed disc.
 */
export function createSectorPin(
  scene: Phaser.Scene,
  theme: WorldTheme,
  input: { id: string; channels: Channels; zoom: ZoomTier; x: number; y: number }
): Phaser.GameObjects.Container {
  if (input.channels.shape === "unknown") return unknownPin(scene, theme, input);
  return discPin(scene, theme, input);
}
```

Naming: `world:` events, `WorldMapScene` (not `WorldScene` — that filename is the SVG composer
today). `GRID_X` / `GRID_Y` keep those names when they move to `layout.ts`. Payload field is
`modelSeq`, not a fake `revision` on the adapted world.

Exact numeric values of `FIT_MAX` / `DETAIL_MIN` above are placeholders until Scene slice picks
them against plate §O.4; the **names and structural class** are locked.

---

## Testing strategy

Vitest, colocated, **Phaser mocked** at the Game boundary (`createGame.test.ts` already documents
why: Canvas 2D at module load). Object factories should take a minimal `scene` fake or return a
plain descriptor `{ kind, lod, channels, fog }` that the factory consumes — prefer a **descriptor**
test so the matrix does not need a Scene at all:

1. **State matrices** — reuse `sectorChannels` / `laneChannels` matrices; assert the pin descriptor
   has ≥2 non-colour channels and `opacity` never appears.
2. **Intel-first** — identical payloads, `Watched` vs `Unknown`, different `kind`.
3. **Fog-on-pin** — Scouted/Rumored get ring+pip (and wash only at detail); forces strip absent.
4. **LOD supersets** — `fit` ⊂ `map` ⊂ `detail` on a frozen channel record.
5. **Bus generation** — emit with generation 1 while host is 2 → handler not called.
6. **modelSeq** — equal or lower seq ignored; higher applies.
7. **Import guard** — `game/world` ↛ `lib/bus` / React / `*Dto` (channels from `stages/world/render` OK).
8. **Pick occlusion** — pointer over left dock / rail does not select.
9. **E2E + agent CV** — Playwright `e2e/world-map-runtime.spec.ts` at 1280×720: pan/zoom/edge-scroll,
   open left inspector, GG-11 mount probe; greyscale CSS-filter shot for GG-27 squint; artifacts under
   `e2e/.artifacts/world-map-runtime/`; agent `Read`s PNGs against the plan's CV checklist (no owner
   eyeball). No dual-camera SVG overlay.

`SectorNode.test.tsx` and overlay tests remain until the React map/overlays are deleted; then the
matrix lives on the descriptor tests. Do not keep two matrices that can drift.

---

## Tunables and numeric types

[tunables-ssot.md](../tunables-ssot.md). No new `P(Θ)`. No new stock.

| Number | Class | Home |
|---|---|---|
| `worldSizeNodes` | Tunable (already) | `data/tuning/world.v5.json` |
| Lane `Width` → stroke | Already specified | existing mapping |
| `GRID_X` / `GRID_Y` | Layout const today (`WorldScene.tsx:19-20`) | Move to `layout.ts` with a tunables comment. If a pass changes them for *feel*, promote into `world.v*.json` |
| Camera min/max scale | **Structural** | named const + comment |
| `FIT_MAX` / `DETAIL_MIN` | **Structural** | named const + comment in `zoomTier.ts` |
| Pin disc size (44px catalog) | Structural hit-target (a11y), not yield | named const + comment |
| Drag-vs-click pixel threshold | **Structural** | named const + comment |
| Edge-scroll margin / speed | **Structural** until a feel pass promotes them | named const + comment |

Magnitudes on the pin (net loam) stay `long` via `world-numbers`. This module does not introduce
`float` HP or a per-mille camera.

---

## Boundaries

- **Always:** dual-plane locks; intel-first; GG-27 two channels; tokens via snapshot; destroy
  checklist; mock Phaser in unit tests; `world:*` beside `lawn:*`; pin centres for lanes; one camera
  owns all map overlays; host `modelSeq` dirty-flag; delete SVG camera once Phaser camera is live;
  pick ignores left dock + rail; Phaser only from World lazy chunk.
- **Ask first:** a twelfth pattern or fifth system; a Phaser UIScene; bringing xyflow back for the
  **player** map; minimap; changing fog *rules*; promoting `GRID_*` into tuning; drawing the §A card
  on the canvas "just for zoom"; adding `Revision` to `WorldStateDto`; moving channels to
  `src/lib/world-view/` (out of v1 — locked).
- **Authorized in-program (R16):** amend T3 HOW in `tech-stack.md` (and `decisions.md` only if a row
  still names SVG pan/zoom HOW). xyflow stays off player map / entry chunk.
- **Never:** Phaser `fetch` / SignalR / DTO imports; React GameObject refs; encode health as opacity;
  infer unknown from emptiness; two `Phaser.Game`s at once; nest Phaser under `stages/`; put world
  events on `LawnBusEvent`; React SVG overlays fighting Phaser camera; new npm renderer; injector /
  PvZ field writes; hard progression cap; private `f(level)`; invent a second paint-descriptor
  matrix for v1; require owner eyeball for phase checkpoints.

---

## Success criteria

1. Entering World creates a Game; leaving destroys it; opening the inspector does **not** (`mount
   count === 1`).
2. Pins match plate §O at fit/map/detail; unknown is a diamond; fog four-treatments follow the
   pin-density table; no value-driven opacity.
3. Inspector still renders the §A card from React (left dock); the canvas does not.
4. Wheel zooms about pointer; drag pans; **edge-scroll** pans; arrows pan only when React says the
   map owns input; `W` still cycles.
5. Pick ignores rail + open left inspector + HUD corners; right-click emits `empty`.
6. `world:*` is a separate union; lawn tests unchanged in event names; Sync keys on `modelSeq`.
7. `game/world` never imports `lib/bus` / React / `*Dto`; channel imports from `stages/world/render`
   allowed; hex guard still skips `game/` and snapshot is the colour bridge.
8. No React map overlays remain on the stage (Range / Supply / Lifeline / Fog-wrapping-SectorNode);
   Phaser draws them.
9. SVG `WorldScene` composer and unused `camera.ts` / `cameraGestures.ts` are **gone** (hard delete —
   gaps D19; no dead-export escape hatch).
10. `npm test` and `npm run build` green. (**No `npm run lint`** — `web/fusion-rpg-web/package.json`
    has no lint script.)
11. Phase checkpoints CPA–CPD green: Playwright `e2e/world-map-runtime.spec.ts` + PNG artifacts +
    agent CV checklist (no owner eyeball / human playtest gate).
12. **R16** amends T3 HOW in `tech-stack.md` (and `decisions.md` if needed). Banners on
    `world-stage-map` / `spec-world-shell` / `spec-world-render` already landed 2026-09-06.

---

## What this spec supersedes (HOW only)

| Locked as | Survives |
|---|---|
| T3 "render as SVG with a small pan/zoom hook" | xyflow off player map and off entry chunk |
| `spec-world-shell` §2 SVG `viewBox` camera | Gestures (incl. edge-scroll), no minimap on medium, authored `{x,y}` |
| `spec-world-render` React `SectorNode` **on the stage** + React overlay composition | Channel modules, GG-27 matrices, fog intel-first, type floor; fog **card** treatments in inspector |
| Plate §B rounded rects as the map look | Stroke language as legend; §O pins |
| Dual React+Phaser cameras for range/supply | One Phaser camera for all map-plane drawing |

HUD, inspector, commands, playback specs are unchanged (dock side stays left per inspector spec).

---

## Locked decisions (cleared 2026-09-06)

1. **Plate §O drawing calls:** circle pin + ownership ring; unknown = diamond; LOD strict supersets.
2. **Fog-on-pin density table** (Visual contract above): forces strip never on the pin; Scouted/Rumored
   ring+pip at map+, wash at detail only.
3. **T3 HOW:** amended by task **R16** in-program (not Ask-first). xyflow remains off player map /
   entry chunk.
4. **Channels** stay under `stages/world/render/` for v1; `game/world` may import them. No move to
   `src/lib/world-view/` in this program.

---

## DESIGN-GATE §5 (this session)

```
[x] Subsystems: world map plane, FE game foundation, Game GUI, world-stage
    render/shell/inspector/lenses/targeting.
[x] Read this session (strengthen + lock pass): DESIGN-GATE world-map row, capability map, this
    spec, world-map-runtime-ideal, fe-game-foundation RT table, spec-world-render/shell/inspector/
    lenses/targeting, plate §O.3–O.6, adaptWorldState / WorldStateDto / WorldHeaderDto, lawn
    game/ → features/lawn imports, WorldScene overlay composition, routes.tsx lazy World,
    e2e/world-stage.spec.ts artifact pattern.
[x] decisions.md: Game GUI D2; T3 HOW authorized as R16 (locked, not open).
[x] Claims cite file:line (WorldStage, adapt.ts, WorldDtos, fogTreatments, Rail 92px, routes lazy).
[x] Verified against code: AdaptedWorldState has no revision; WorldStateDto has no Revision;
    Supply/Lifeline not composed in WorldScene; no npm lint script.
[x] Quoted GG-11 from stage lifetime; inspector dock from §8e.1.
[ ] Constraint / Playwright suites not re-run this doc-only lock pass. Honest gap until R0+.
[x] No §2 invariant contradicted. Gameless-first: web Phaser, Fusion closed, still plays.
[x] Open questions cleared → Locked decisions; human visual gates → Playwright + agent CV.
```

**Honest gaps:** `software-architecture.md` / `information-architecture.md` §2.2 not re-read
end-to-end this pass. No suite run until R0 lands. Fog-on-pin + §O are **locked**, not pending
sign-off.
