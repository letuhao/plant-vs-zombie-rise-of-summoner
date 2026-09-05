# Tasks: world-map-runtime

**Status:** proposed 2026-09-06 — pending owner review of [world-map-runtime-plan.md](world-map-runtime-plan.md).
Approving that plan authorizes these tasks against the strengthened
[spec](../docs/architecture/world-map-runtime/spec-world-map-runtime.md).

**Program id:** `world-map-runtime`
**Map:** [world-map-runtime-map.md](../docs/architecture/world-map-runtime-map.md)
**Ideal:** [world-map-runtime-ideal.md](../docs/architecture/world-map-runtime-ideal.md)

Task ids: **R1** upward (runtime). Contiguous. Not `world-stage` **W\*** and not bare `tasks/todo.md`.

---

## Standing rules

1. Spec + capability map win over any shorthand here.
2. **Git hands-off** — no commit/push; leave a message draft for the owner.
3. Mock Phaser in unit tests; no real `Phaser.Game` in Vitest.
4. `game/world` may import `@/contract/types` and `stages/world/render/{sector,lane,fog,slot}*`; must
   not import React, `@/lib/bus`, or `*Dto`.
5. Dirty flag is host **`modelSeq`** — do not add `Revision` to `WorldStateDto`.
6. Verification: `cd web\fusion-rpg-web; npm test; npm run build`. **There is no `npm run lint`**
   (`package.json` has no lint script — same finding as world-stage todo).
7. Structural consts (`FIT_MAX`, `DETAIL_MIN`, pin 44px, camera clamps, drag threshold, edge-scroll
   margin) get a comment that they are control/a11y, not balance tunables.

---

## Phase A — Host island

### R1: `world:*` EventBus beside lawn

**Description:** Add a parallel world event union and `worldBusOn` / `worldBusEmit` /
`worldBusClearAll` in `EventBus.ts` without widening `LawnBusEvent`. Share `allocGameGeneration()`.
Payload types include `modelSeq` on `world:model` (not a DTO revision).

**Acceptance criteria:**
- [ ] Events: `world:model|select|camera|lens|interaction|ready|resized|destroyed`
- [ ] Foreign `generation` droppable by subscribers; `worldBusClearAll` does not clear lawn listeners
      (or documents separate maps — lawn tests still green)
- [ ] Lawn event name strings unchanged

**Verification:**
- [ ] Tests: colocated EventBus world tests + existing lawn bus tests
- [ ] `npm test` / `npm run build` green

**Dependencies:** None
**Files likely touched:** `web/fusion-rpg-web/src/game/EventBus.ts`, `EventBus*.test.ts`
**Estimated scope:** S

---

### R2: layout, zoomTier, snapshotTheme

**Description:** Pure modules under `game/world/`: GRID centres from `layoutX/Y`, named `FIT_MAX` /
`DETAIL_MIN` zoom tiers (strict supersets documented), CSS-var snapshot into `WorldTheme` including
`--soil` and font family for type floor.

**Acceptance criteria:**
- [ ] Lane/pin math uses centres, not top-left
- [ ] `zoomTier` uses named structural consts (placeholder numbers OK until Scene feels them)
- [ ] Snapshot is the only colour/font ingress for Phaser world objects

**Verification:**
- [ ] `layout.test.ts`, `zoomTier.test.ts` (and snapshot unit if feasible without canvas)
- [ ] `npm test` / `npm run build` green

**Dependencies:** None (parallel with R1)
**Files likely touched:** `game/world/layout.ts`, `zoomTier.ts`, `snapshotTheme.ts`, `*.test.ts`
**Estimated scope:** M

---

### R3: createWorldGame + empty WorldMapScene

**Description:** Thin facade over `createGame` with scene list `[WorldMapScene]` only (no
`WorldBootScene`). Destroy checklist mirrors `destroyLawnGame`. Empty scene paints theme backdrop and
emits `world:ready`.

**Acceptance criteria:**
- [ ] `createWorldGame` / `destroyWorldGame` exist; destroy order: tweens → shutdown → `world:destroyed`
      → `game.destroy(true)`
- [ ] Unit tests mock Phaser like `createGame.test.ts`
- [ ] No BootScene file

**Verification:**
- [ ] `createWorldGame` mock tests
- [ ] `npm test` / `npm run build` green

**Dependencies:** R1, R2
**Files likely touched:** `game/createWorldGame.ts`, `game/world/scenes/WorldMapScene.ts`, tests
**Estimated scope:** M

---

### R4: WorldGameHost + stage mount

**Description:** Facade host copying `LawnGameHost` lifetime: alloc generation, buffer until ready,
bump **`modelSeq`** when adapted payload (or overlay inputs) change, ResizeObserver → `world:resized`.
`WorldStage` mounts host in place of the SVG map pane; HUD / inspector / turn / playback stay React.
Only this host imports `createWorldGame` (GG-38).

**Acceptance criteria:**
- [ ] Enter World creates one Game; leave destroys; opening inspector does not remount host (GG-11)
- [ ] `modelSeq` monotonic; no use of `WorldHeaderDto.revision` as pin dirty-flag
- [ ] Fixture `first-light` still loads when no live world

**Verification:**
- [ ] `WorldGameHost.test.tsx` (buffer, destroy on unmount, modelSeq)
- [ ] Manual or mount-guard: inspector open → stage mount count 1
- [ ] `npm test` / `npm run build` green

**Dependencies:** R3
**Files likely touched:** `stages/world/host/WorldGameHost.tsx`, `WorldStage.tsx`, host tests
**Estimated scope:** M

---

## Checkpoint A — after R1–R4

- [ ] `npm test` and `npm run build` green
- [ ] World shows Phaser canvas (soil); leave destroys WebGL context cleanly
- [ ] Inspector / confirm does not destroy Game
- [ ] Owner review before Phase B

---

## Phase B — Graph objects

### R5: WorldRegistry + syncWorldSystem

**Description:** Registry keyed by sectorId / laneId / forceId. Sync applies `world:model` only when
`modelSeq > lastApplied`; upsert present ids; destroy absent; intel-first branch before paint.

**Acceptance criteria:**
- [ ] Equal/lower `modelSeq` no-ops
- [ ] Publish-by-replace semantics on the host side respected (no in-place mutate expectation)
- [ ] Unknown never inferred from empty payload fields

**Verification:**
- [ ] Sync unit tests with fake registry
- [ ] `npm test` / `npm run build` green

**Dependencies:** R4
**Files likely touched:** `game/world/entities/WorldRegistry.ts`, `systems/syncWorldSystem.ts`, tests
**Estimated scope:** M

---

### R6: sectorPin factory + fog-on-pin

**Description:** Factory maps `channels.shape` card→disc / unknown→diamond; applies fog-on-pin density
table (Rumored/Scouted ring+pip; wash at detail only; **no forces strip on pin**). No opacity encoding.
Descriptor tests reuse channel matrices.

**Acceptance criteria:**
- [ ] GG-27: ≥2 non-colour channels on descriptors; no `opacity` field
- [ ] Fog four intel states distinguishable per spec table; inspector still owns full fog card
- [ ] Type floor: fact-bearing Phaser Text ≥12px at 720p or label omitted

**Verification:**
- [ ] `objects/sectorPin` descriptor tests + fog matrix
- [ ] `npm test` / `npm run build` green

**Dependencies:** R5 (types), R2 (theme)
**Files likely touched:** `game/world/objects/sectorPin.ts`, tests; may read `sectorChannels` /
`fogTreatments`
**Estimated scope:** M

---

### R7: laneStroke + forceMarker

**Description:** Lanes from `laneChannels` as Graphics; endpoints at pin centres. Forces: three shapes
before three colours; mid-lane `getPointAt(progressMilli/1000)` linear; set position on sync apply
(no tween across missing lane).

**Acceptance criteria:**
- [ ] Centres used (fixes SVG top-left miss)
- [ ] SVG `getElementById(pathId)` not required
- [ ] Object pool not introduced

**Verification:**
- [ ] Object descriptor/unit tests
- [ ] `npm test` / `npm run build` green

**Dependencies:** R5, R6 (pin centres for lane ends)
**Files likely touched:** `game/world/objects/laneStroke.ts`, `forceMarker.ts`, tests
**Estimated scope:** M

---

### R8: Graph on stage; retire SectorNode from map

**Description:** Wire Sync → factories in `WorldMapScene` update loop (Sync only of allow-list for
now). Host emits adapted model. Remove React `SectorNode` / Fog-wrapping-card from the stage
composition; keep their tests as oracles until descriptors fully replace matrices.

**Acceptance criteria:**
- [ ] first-light (or live) shows pins + lanes on Phaser
- [ ] Unknown sectors are diamonds
- [ ] Inspector §A card still React when a sector is selected (selection may still be temporary until R11)

**Verification:**
- [ ] Manual 1280×720: pins visible
- [ ] `npm test` / `npm run build` green

**Dependencies:** R5–R7
**Files likely touched:** `WorldMapScene.ts`, `WorldStage.tsx`, `render/WorldScene.tsx` (strip or bypass)
**Estimated scope:** M

---

## Checkpoint B — after R5–R8

- [ ] Pins / lanes / forces match §O + fog-on-pin defaults
- [ ] No value-driven opacity
- [ ] Owner review before Phase C

---

## Phase C — Camera + pick

### R9: worldCameraSystem (drag, wheel, edge-scroll, Fit, LOD)

**Description:** Phaser `Cameras.Scene2D` only camera. Drag-empty with structural pixel threshold;
wheel zoom about pointer; edge-scroll while map owns input; Fit with HUD safe insets; min/max scale
structural; on scale change update pin LOD in place (no registry rebuild).

**Acceptance criteria:**
- [ ] Page does not scroll; ultrawide expands map (`world:resized` / scale resize)
- [ ] LOD strict supersets; fit never drops ownership/health/unknown identity
- [ ] Backdrop from theme `--soil`

**Verification:**
- [ ] Camera unit tests where pure; manual pan/zoom/edge-scroll
- [ ] `npm test` / `npm run build` green

**Dependencies:** R8
**Files likely touched:** `game/world/systems/worldCameraSystem.ts`, `WorldMapScene.ts`, tests
**Estimated scope:** M

---

### R10: React → `world:camera` (arrows, Fit cluster)

**Description:** Wire keymap / HUD Fit and +/− to `world:camera`. Arrows pan only when React says the
map owns input (GG-18). `W` remains cycle — never pan. Arrows do not hop pin-to-pin.

**Acceptance criteria:**
- [ ] Layer owning input suppresses arrow pan
- [ ] Fit control recentres extent
- [ ] No WASD pan binding

**Verification:**
- [ ] Host/keymap tests or integration assert
- [ ] `npm test` / `npm run build` green

**Dependencies:** R9, R4
**Files likely touched:** `WorldGameHost.tsx`, `WorldStage.tsx` and/or HUD map-controls, tests
**Estimated scope:** S–M

---

### R11: worldPickSystem + chrome occlusion

**Description:** 44px hit disc; ignore rail (92px), left inspector dock (~380px when open), HUD
corners via `ignoreRects` on `world:interaction` (or equivalent). Emit `world:select`; right-click /
contextmenu → `kind: "empty"`. Hover highlight Phaser-local (no `world:hover`). Wire select into
`worldUiReducer` so inspector opens.

**Acceptance criteria:**
- [ ] Click through open left inspector does not select
- [ ] Right-click clears selection (same stack as Esc)
- [ ] Selection halo driven by interaction/select — distinct from hover/focus outline

**Verification:**
- [ ] Pick occlusion unit tests
- [ ] Manual: select → left inspector; right-click clears
- [ ] `npm test` / `npm run build` green

**Dependencies:** R9, R10
**Files likely touched:** `systems/worldPickSystem.ts`, host interaction payload, `WorldStage.tsx`, tests
**Estimated scope:** M

---

## Checkpoint C — after R9–R11

- [ ] Spec success criteria 4–5 satisfied
- [ ] Owner review before Phase D

---

## Phase D — Overlays + SVG retirement

### R12: Selection halo + range rings on Phaser

**Description:** `world:interaction` carries targeting points from pure `worldSelection`. Overlay
system draws range rings. Retire React `RangeOverlay` from stage composition (keep tests as oracle).

**Acceptance criteria:**
- [ ] No React SVG range layer over the canvas
- [ ] Reachable encoding still has non-colour channel (hop text / pattern per targeting spec)

**Verification:**
- [ ] Overlay descriptor or scene tests; manual march targeting
- [ ] `npm test` / `npm run build` green

**Dependencies:** R11
**Files likely touched:** `systems/worldOverlaySystem.ts`, `WorldStage.tsx`, targeting composition
**Estimated scope:** M

---

### R13: Queued routes + blocked marks

**Description:** Draw queued move routes and blocked-target marks on Phaser (display list above
forces). Remove corresponding SVG/`foreignObject` stage paths.

**Acceptance criteria:**
- [ ] Queued route uses pin centres
- [ ] Blocked mark placed at decision sector, not covering inspector card

**Verification:**
- [ ] Manual queue + blocked refusal
- [ ] `npm test` / `npm run build` green

**Dependencies:** R12
**Files likely touched:** `worldOverlaySystem.ts`, `WorldScene.tsx` / stage cleanup
**Estimated scope:** S–M

---

### R14: Supply, lifeline, six lenses

**Description:** `world:lens` drives Phaser drawings for all six lens encodings
(`spec-world-lenses`). Supply envelope + lifeline halo on canvas (closes wiring gap where overlays
existed but were not composed). Picker stays React. Retire stage `SupplyOverlay` / `LifelineOverlay`.

**Acceptance criteria:**
- [ ] Exactly one lens drawing active; picker still names it
- [ ] Lens 4 still triggers server `lifelines=true` from React; Phaser only paints result
- [ ] GG-27: no hue-only lens encoding

**Verification:**
- [ ] Lens encoding tests (descriptor or reused catalog asserts)
- [ ] Manual: keys 1–6 change map drawing
- [ ] `npm test` / `npm run build` green

**Dependencies:** R12
**Files likely touched:** `worldOverlaySystem.ts`, host lens emit, lenses wiring, tests
**Estimated scope:** M (split to R14a supply/lifeline + R14b lenses if >5 files)

---

### R15: Delete SVG camera path + import guard

**Description:** Delete `camera.ts` / `cameraGestures.ts` and retire SVG `WorldScene` stage composer
(or make it unreachable so CI fails if reimported). Assert z-order: backdrop → lanes → pins → forces
→ lens/supply/lifeline → range/routes/blocked. Guard: `game/world` ↛ `lib/bus` / React / `*Dto`.

**Acceptance criteria:**
- [ ] No second camera module left beside Phaser
- [ ] Spec success criteria 7–9
- [ ] Systems allow-list still four (Sync, Camera/LOD, Pick, Overlay)

**Verification:**
- [ ] Import/guard test; `rg` shows no stage import of deleted camera host
- [ ] `npm test` / `npm run build` green

**Dependencies:** R13, R14
**Files likely touched:** delete/retire SVG files, guard test, `WorldStage.tsx`
**Estimated scope:** M

---

## Checkpoint D — after R12–R15

- [ ] Spec success criteria 1–10 (with build/test, not lint)
- [ ] One Phaser camera owns all map-plane overlays
- [ ] Owner playtest at 1280×720 + greyscale squint on pin gallery

---

## Phase E — Doc follow-up (non-blocking)

### R16: T3 HOW sentence

**Description:** Amend `docs/design/tech-stack.md` T3 (and `decisions.md` if it still names the SVG
hook) so HOW is Phaser dual-plane, WHAT survives is xyflow off player map / entry chunk. **Ask
owner before editing** those files.

**Acceptance criteria:**
- [ ] T3 no longer instructs SVG pan/zoom as the player map HOW
- [ ] xyflow remains forbidden on player map / entry chunk
- [ ] Chunk budget row for `stage-map` updated if it still says "SVG map renderer"

**Verification:**
- [ ] Doc review only
- [ ] No code change required

**Dependencies:** None (may run parallel with B–D)
**Files likely touched:** `docs/design/tech-stack.md`, possibly `docs/architecture/decisions.md`
**Estimated scope:** S

---

## Checkpoint: Complete

- [ ] R1–R15 done
- [ ] R16 done or explicitly deferred by owner
- [ ] Capability map status updated to reflect build authorized / in progress
- [ ] Ready for ongoing world-stage chrome work on the dual-plane stage
