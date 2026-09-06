# Tasks: world-map-runtime

**Status:** **not complete** — R0–R16 landed a Phaser host; parent SC 1–12 remain open under
[world-map-runtime-gaps-todo.md](world-map-runtime-gaps-todo.md) (D1–D32). Owner git commit of R0–R16
history is separate from gaps closure.

Plan: [world-map-runtime-plan.md](world-map-runtime-plan.md)  
**Map:** [world-map-runtime-map.md](../docs/architecture/world-map-runtime-map.md)  
**Spec:** [spec-world-map-runtime.md](../docs/architecture/world-map-runtime/spec-world-map-runtime.md)  
**Ideal:** [world-map-runtime-ideal.md](../docs/architecture/world-map-runtime-ideal.md)

Task ids: **R0** then **R1** upward. Contiguous. Not `world-stage` **W\*** and not bare `tasks/todo.md`.

---

## Standing rules

1. Spec + capability map win over any shorthand here.
2. **Git hands-off** — no commit/push; leave a message draft for the owner.
3. Mock Phaser in unit tests; no real `Phaser.Game` in Vitest.
4. `game/world` may import `@/contract/types` and `stages/world/render/{sector,lane,fog,slot}*`; must
   not import React, `@/lib/bus`, or `*Dto`.
5. Dirty flag is host **`modelSeq`** — do not add `Revision` to `WorldStateDto`.
6. Verification: `cd web\fusion-rpg-web; npm test; npm run build`. **There is no `npm run lint`.**
7. Structural consts get a comment that they are control/a11y, not balance tunables.
8. **Phase checkpoints (CPA–CPD):** Playwright `e2e/world-map-runtime.spec.ts` + PNGs under
   `e2e/.artifacts/world-map-runtime/` + agent `Read` (CV) against the plan checklist — **no owner
   eyeball**. Fail the checkpoint if CV fails.
9. Locked visuals: §O pin language + fog-on-pin density table (forces strip never on pin).

---

## R0 — E2E harness (first task)

### R0: Host testids + `world-map-runtime.spec.ts` stub

**Description:** Add stable `data-testid` hooks the Phaser host will mount onto (host root + canvas
wrapper). Create `e2e/world-map-runtime.spec.ts` that mocks world APIs like
`e2e/world-stage.spec.ts` (first-light fixture), navigates `#/world`, and documents the artifact
directory `e2e/.artifacts/world-map-runtime/`. Stub may assert stage chrome until R4 lands the
canvas; expand describes `CPA` / `CPB` / `CPC` / `CPD` as phases complete.

**Acceptance criteria:**
- [x] `data-testid="world-game-host"` (and canvas wrapper id) reserved / present when host mounts
- [x] Spec file exists; loads `#/world` with mocked header/state; writes at least one PNG under
      `e2e/.artifacts/world-map-runtime/`
- [x] Artifact path documented in this todo / plan

**Verification:**
- [x] `npx playwright test e2e/world-map-runtime.spec.ts` green (stub level)
- [x] `npm test` / `npm run build` unchanged green

**Dependencies:** None  
**Files likely touched:** `e2e/world-map-runtime.spec.ts`; later `WorldGameHost.tsx` testids (or a
placeholder comment until R4)  
**Estimated scope:** S–M

---

## Phase A — Host island

### R1: `world:*` EventBus beside lawn

**Description:** Parallel world event union and `worldBusOn` / `worldBusEmit` / `worldBusClearAll`
without widening `LawnBusEvent`. Share `allocGameGeneration()`. `world:model` carries `modelSeq`.

**Acceptance criteria:**
- [x] Events: `world:model|select|camera|lens|interaction|ready|resized|destroyed`
- [x] Foreign `generation` droppable; clearing world listeners does not wipe lawn (or separate maps)
- [x] Lawn event name strings unchanged

**Verification:**
- [x] Colocated EventBus world tests + existing lawn bus tests
- [x] `npm test` / `npm run build` green

**Dependencies:** None (parallel with R0/R2)  
**Files likely touched:** `web/fusion-rpg-web/src/game/EventBus.ts`, tests  
**Estimated scope:** S

---

### R2: layout, zoomTier, snapshotTheme

**Description:** Pure modules under `game/world/`: GRID centres, named `FIT_MAX` / `DETAIL_MIN`,
CSS-var snapshot into `WorldTheme` (`--soil`, font).

**Acceptance criteria:**
- [x] Centres, not top-left
- [x] Named structural zoom consts
- [x] Snapshot is the only colour/font ingress for Phaser world objects

**Verification:**
- [x] `layout.test.ts`, `zoomTier.test.ts`
- [x] `npm test` / `npm run build` green

**Dependencies:** None  
**Files likely touched:** `game/world/layout.ts`, `zoomTier.ts`, `snapshotTheme.ts`, tests  
**Estimated scope:** M

---

### R3: createWorldGame + empty WorldMapScene

**Description:** Facade over `createGame` with `[WorldMapScene]` only. Destroy checklist mirrors
lawn. Empty scene paints theme backdrop and emits `world:ready`.

**Acceptance criteria:**
- [x] `createWorldGame` / `destroyWorldGame`; destroy order documented
- [x] Phaser mocked in unit tests
- [x] No BootScene file

**Verification:**
- [x] `createWorldGame` mock tests
- [x] `npm test` / `npm run build` green

**Dependencies:** R1, R2  
**Files likely touched:** `game/createWorldGame.ts`, `game/world/scenes/WorldMapScene.ts`, tests  
**Estimated scope:** M

---

### R4: WorldGameHost + stage mount

**Description:** Copy `LawnGameHost` lifetime: generation, buffer until ready, **`modelSeq`**,
ResizeObserver → `world:resized`. Mount host in place of SVG map pane; HUD/inspector stay. Only this
file imports `createWorldGame` (GG-38). Apply R0 testids.

**Acceptance criteria:**
- [x] Enter creates Game; leave destroys; inspector open does not remount (GG-11)
- [x] `modelSeq` monotonic; not header revision
- [x] Fixture `first-light` still loads with no live world

**Verification:**
- [x] `WorldGameHost.test.tsx`
- [x] Playwright CPA (after Checkpoint A recipe): canvas/host present; GG-11 probe
- [x] `npm test` / `npm run build` green

**Dependencies:** R3, R0  
**Files likely touched:** `stages/world/host/WorldGameHost.tsx`, `WorldStage.tsx`, host tests, e2e  
**Estimated scope:** M

---

## Checkpoint A (CPA) — after R0–R4

**Automated — no owner review.**

- [x] `npm test` and `npm run build` green
- [x] `npx playwright test e2e/world-map-runtime.spec.ts` — describe/tag **CPA** green
- [x] Artifacts: `e2e/.artifacts/world-map-runtime/cpa-*.png`
- [x] **Agent CV:** `Read` CPA PNGs — soil-toned map plane; React HUD corners present; no full-page
      SVG sector-card grid as the map; host/canvas visible
- [x] Proceed to Phase B only if CV checklist passes

---

## Phase B — Graph objects

### R5: WorldRegistry + syncWorldSystem

**Description:** Registry by sectorId / laneId / forceId. Sync when `modelSeq > lastApplied`;
upsert/destroy; intel-first.

**Acceptance criteria:**
- [x] Equal/lower `modelSeq` no-ops
- [x] Unknown never inferred from empty fields

**Verification:**
- [x] Sync unit tests
- [x] `npm test` / `npm run build` green

**Dependencies:** R4  
**Files likely touched:** `entities/WorldRegistry.ts`, `systems/syncWorldSystem.ts`, tests  
**Estimated scope:** M

---

### R6: sectorPin factory + fog-on-pin

**Description:** card→disc / unknown→diamond; fog-on-pin density (locked). No opacity. Descriptor
tests reuse channel matrices.

**Acceptance criteria:**
- [x] GG-27 ≥2 non-colour channels; no `opacity`
- [x] Four intel states per locked table; forces strip absent on pin
- [x] Type floor: Text ≥12px at 720p or omit

**Verification:**
- [x] Descriptor + fog matrix tests
- [x] `npm test` / `npm run build` green

**Dependencies:** R5, R2  
**Files likely touched:** `objects/sectorPin.ts`, tests  
**Estimated scope:** M

---

### R7: laneStroke + forceMarker

**Description:** Lanes from `laneChannels`; endpoints at centres. Forces: three shapes first;
mid-lane `getPointAt(progressMilli/1000)` linear set-on-apply.

**Acceptance criteria:**
- [x] Centres used
- [x] No SVG path id contract
- [x] No object pool

**Verification:**
- [x] Object unit/descriptor tests
- [x] `npm test` / `npm run build` green

**Dependencies:** R5, R6  
**Files likely touched:** `objects/laneStroke.ts`, `forceMarker.ts`, tests  
**Estimated scope:** M

---

### R8: Graph on stage; retire SectorNode from map

**Description:** Wire Sync → factories. Emit `world:model`. Remove React SectorNode/Fog-card from
stage; keep tests as oracles.

**Acceptance criteria:**
- [x] first-light shows pins + lanes on Phaser
- [x] Unknown = diamond
- [x] Inspector §A still React when selected

**Verification:**
- [x] Playwright CPB asserts (pin/lane presence)
- [x] `npm test` / `npm run build` green

**Dependencies:** R5–R7  
**Files likely touched:** `WorldMapScene.ts`, `WorldStage.tsx`, `render/WorldScene.tsx`, e2e  
**Estimated scope:** M

---

## Checkpoint B (CPB) — after R5–R8

**Automated — no owner review.**

- [x] `npm test` and `npm run build` green
- [x] Playwright **CPB** green
- [x] Artifacts: `e2e/.artifacts/world-map-runtime/cpb-*.png`
- [x] **Agent CV:** disc pins; diamond if Unknown in fixture; lanes at centres; no 192px card grid
- [x] Proceed to Phase C only if CV passes

---

## Phase C — Camera + pick

### R9: worldCameraSystem

**Description:** Drag threshold, wheel-about-pointer, edge-scroll, Fit, clamps, in-place LOD.

**Acceptance criteria:**
- [x] No page scroll; ultrawide expands map
- [x] LOD strict supersets
- [x] Backdrop from `--soil`

**Verification:**
- [x] Camera unit tests where pure; Playwright CPC camera probes
- [x] `npm test` / `npm run build` green

**Dependencies:** R8  
**Files likely touched:** `systems/worldCameraSystem.ts`, `WorldMapScene.ts`, tests, e2e  
**Estimated scope:** M

---

### R10: React → `world:camera`

**Description:** Arrows pan when map owns input; Fit/+/−; `W` cycles; no WASD pan; no pin-hop.

**Acceptance criteria:**
- [x] Layer owning input suppresses arrow pan
- [x] Fit recentres extent

**Verification:**
- [x] Keymap/host tests + Playwright CPC
- [x] `npm test` / `npm run build` green

**Dependencies:** R9, R4  
**Files likely touched:** host / WorldStage / HUD controls, tests, e2e  
**Estimated scope:** S–M

---

### R11: worldPickSystem + chrome occlusion

**Description:** 44px hit; ignoreRects (rail, left dock, HUD); `world:select`; right-click → empty;
hover local-only; wire reducer → inspector.

**Acceptance criteria:**
- [x] Click through open left inspector does not select
- [x] Right-click clears selection
- [x] Selection halo ≠ hover/focus outline

**Verification:**
- [x] Pick occlusion unit tests
- [x] Playwright CPC: select → left dock; right-click empty; click-over-dock no select
- [x] `npm test` / `npm run build` green

**Dependencies:** R9, R10  
**Files likely touched:** `systems/worldPickSystem.ts`, host, `WorldStage.tsx`, tests, e2e  
**Estimated scope:** M

---

## Checkpoint C (CPC) — after R9–R11

**Automated — no owner review.**

- [x] `npm test` and `npm run build` green
- [x] Playwright **CPC** green (gestures + pick + ignoreRects)
- [x] Artifacts: `e2e/.artifacts/world-map-runtime/cpc-*.png` (include before/after pan or zoom)
- [x] **Agent CV:** frames differ after pan/zoom; inspector dock on **left** beside rail
- [x] Proceed to Phase D only if CV passes

---

## Phase D — Overlays + SVG retirement

### R12: Selection halo + range rings on Phaser

**Description:** `world:interaction` points from `worldSelection`; draw range on Phaser; retire
stage `RangeOverlay`.

**Acceptance criteria:**
- [x] No React SVG range over canvas
- [x] Non-colour channel on reachable encoding

**Verification:**
- [x] Playwright CPD targeting leg; unit/descriptor tests
- [x] `npm test` / `npm run build` green

**Dependencies:** R11  
**Files likely touched:** `systems/worldOverlaySystem.ts`, stage composition, e2e  
**Estimated scope:** M

---

### R13: Queued routes + blocked marks

**Description:** Draw queued routes + blocked marks on Phaser; remove SVG/`foreignObject` paths.

**Acceptance criteria:**
- [x] Routes use pin centres
- [x] Blocked mark at decision sector

**Verification:**
- [x] Playwright CPD queue/blocked scenarios
- [x] `npm test` / `npm run build` green

**Dependencies:** R12  
**Files likely touched:** `worldOverlaySystem.ts`, stage cleanup, e2e  
**Estimated scope:** S–M

---

### R14: Supply, lifeline, six lenses

**Description:** `world:lens` → Phaser drawings for six lenses; supply/lifeline on canvas; picker
stays React; retire stage Supply/Lifeline overlays.

**Acceptance criteria:**
- [x] One lens drawing active; picker names it
- [x] Lens 4 still requests `lifelines=true` from React
- [x] GG-27: no hue-only lens encoding

**Verification:**
- [x] Playwright CPD: keys 1–6 change map drawing
- [x] `npm test` / `npm run build` green

**Dependencies:** R12  
**Files likely touched:** `worldOverlaySystem.ts`, host lens emit, lenses wiring, e2e  
**Estimated scope:** M (split R14a/R14b if >5 files)

---

### R15: Delete SVG camera path + import guard

**Description:** Delete `camera.ts` / `cameraGestures.ts`; retire SVG `WorldScene` stage path;
z-order locked; import guard for `game/world`.

**Acceptance criteria:**
- [x] No second camera module
- [x] Spec SC 7–9
- [x] Four systems only (Sync, Camera/LOD, Pick, Overlay)

**Verification:**
- [x] Import/guard test; `rg` shows no live stage import of deleted camera
- [x] Playwright CPD final
- [x] `npm test` / `npm run build` green

**Dependencies:** R13, R14  
**Files likely touched:** delete/retire SVG files, guard test, `WorldStage.tsx`, e2e  
**Estimated scope:** M

---

## Checkpoint D (CPD) — after R12–R15

**Automated — no owner review / no human playtest.**

- [x] Spec success criteria 1–11
- [x] `npm test` and `npm run build` green
- [x] Playwright **CPD** green
- [x] Artifacts: `e2e/.artifacts/world-map-runtime/cpd-*.png` including optional greyscale
      (`page.evaluate` CSS filter) for GG-27 squint
- [x] **Agent CV:** one WebGL/canvas map; overlay strokes visible; no React Range/Supply/Lifeline on
      stage; greyscale shot still shows non-colour channels
- [x] Proceed to Complete only if CV passes

---

## Phase E — Doc follow-up (non-blocking vs A–D)

### R16: T3 HOW sentence

**Description:** Amend `docs/design/tech-stack.md` T3 so HOW is Phaser dual-plane; WHAT survives is
xyflow off player map / entry chunk. Update `decisions.md` only if a row still names SVG pan/zoom
HOW. Update chunk budget row if it still says "SVG map renderer". **Authorized** — not Ask-first.

**Acceptance criteria:**
- [x] T3 no longer instructs SVG pan/zoom as player map HOW
- [x] xyflow remains forbidden on player map / entry chunk
- [x] Chunk budget `stage-map` wording updated if stale

**Verification:**
- [x] Doc review (grep T3 / SVG hook)
- [x] No product code required

**Dependencies:** None (parallel with B–D)  
**Files likely touched:** `docs/design/tech-stack.md`, possibly `docs/architecture/decisions.md`  
**Estimated scope:** S

---

## Checkpoint: Complete

- [x] R0–R16 done
- [x] CPA–CPD green (Playwright + agent CV)
- [x] Capability map status remains build-authorized / in progress as appropriate
- [x] Ready for world-stage chrome work on the dual-plane stage
