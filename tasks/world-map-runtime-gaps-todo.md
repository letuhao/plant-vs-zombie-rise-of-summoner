# World map runtime gaps — task list

**Program:** `world-map-runtime` · **Module:** `world-map-gaps`
**Plan:** [world-map-runtime-gaps-plan.md](world-map-runtime-gaps-plan.md)
**Spec:** [docs/architecture/world-map-runtime/spec-world-map-gaps.md](../docs/architecture/world-map-runtime/spec-world-map-gaps.md)

**Status:** Ready for Implement after owner plan review. Defects **D1–D32**.

**Verify (every task unless noted):**

```powershell
cd web\fusion-rpg-web
npm test
npm run build
# No npm run lint
```

Playwright when a checkpoint says so:

```powershell
npx playwright test e2e/world-map-runtime.spec.ts e2e/world-stage.spec.ts e2e/checkpoint-f.spec.ts
```

---

## Phase 0 — Triage

### G0: Reopen complete status; retarget SVG e2e; routes comment

**Closes:** D20, D32 (routes); parent todo/plan “complete” reopen

**Status:** DONE 2026-09-06

**Description:** Mark [world-map-runtime-todo.md](world-map-runtime-todo.md) /
[world-map-runtime-plan.md](world-map-runtime-plan.md) as **not complete** pending gaps.
Retarget `e2e/world-stage.spec.ts` and `e2e/checkpoint-f.spec.ts` off `world-stage-svg` onto
`world-game-host` / canvas; fix fixture path to `stages/world/fixtures/`. Fix `routes.tsx` GG-38
comment (World is Phaser dual-plane, not SVG stage).

**Acceptance criteria:**
- [x] Parent plan/todo headers no longer claim SC 1–12 closed
- [x] Those two e2e files do not require `world-stage-svg` (may still be red on pick until G14)
- [x] `routes.tsx` comment names Phaser / lazy World chunk correctly

**Verification:**
- [ ] `npm test` + `npm run build` green
- [x] Grep: no “SVG stage” for World in `routes.tsx`

**Dependencies:** None  
**Files likely touched:** `tasks/world-map-runtime-*.md`, `e2e/world-stage.spec.ts`,
`e2e/checkpoint-f.spec.ts`, `src/app/routes.tsx`  
**Estimated scope:** S

---

## Phase A — Host truth

### G1: Canonical worldModelProjection + fingerprint tests

**Closes:** D1, D30 (partial with G3 for legion fields)

**Status:** DONE 2026-09-06

**Description:** Replace thin `modelFingerprint` with `worldModelProjection` per gaps Contracts
(sectors paint fields, lanes, slotsBySectorId, forces, playerFactionId). Targeting must **not** be
hashed. Unit: same intel, changed owner → emit; slots change → emit; targeting-only → no modelSeq
bump.

**Acceptance criteria:**
- [x] Projection includes slots + playerFactionId + sector paint-driving fields
- [x] Unit proves owner / slots dirty; targeting-only does not bump `modelSeq`

**Verification:**
- [x] `npx vitest run src/stages/world/host`
- [x] `npm run build` (full `npm test` has unrelated GG-55 fail in CommandersLayer — out of boundary)

**Dependencies:** G0  
**Files likely touched:** `WorldGameHost.tsx`, new `worldModelProjection.ts` (+ test)  
**Estimated scope:** M

---

### G2: Generation callback; clear globals; probe; DEV logs

**Closes:** D2, D3, D31

**Status:** DONE 2026-09-06

**Description:** Host publishes generation to React (`onGeneration` / state). Fit/arrows/
`world:interaction` use it — not `window.__fusionRpgWorldGen`. Shutdown/unmount clears test globals.
Production probe: coordinates OK; **no** `emitSelect` / self-emitting `pickAt`. Host/overlay
`console.info` DEV-only or removed.

**Acceptance criteria:**
- [x] Production path does not read `__fusionRpgWorldGen` for Fit/pan
- [x] Prod bundle / non-DEV: probe cannot `worldBusEmit("world:select")`
- [x] Hot-path `console.info` gated or gone

**Verification:**
- [x] Host + scene unit/tests; grep for `__fusionRpgWorldGen` in production pan/Fit
- [x] `npm run build`

**Dependencies:** G1  
**Files likely touched:** `WorldGameHost.tsx`, `WorldStage.tsx`, `WorldMapScene.ts`,
`worldOverlaySystem.ts`  
**Estimated scope:** M

---

### G3: Publish all LegionViews + playerFactionId on world:model

**Closes:** D26 (enables D15)

**Status:** DONE 2026-09-06

**Description:** `WorldGameHost` takes sibling `legions: LegionView[]` and `playerFactionId`.
`WorldStage` adapts **all** `dto.entities` (not only `myLegions`) and passes them. Publish on
`world:model`. Fingerprint includes legion `position` (finishes D30 legion half).

**Acceptance criteria:**
- [x] Model payload carries legions + playerFactionId
- [x] Enemy/open legion with lane position can reach Sync (not filtered to player-only)
- [x] Fingerprint bumps when legion `progress` changes

**Verification:**
- [x] Host unit + sync input type tests
- [x] `npm run build`

**Dependencies:** G2  
**Files likely touched:** `WorldGameHost.tsx`, `WorldStage.tsx`, `EventBus` model typing if needed,
`syncWorldSystem.ts` input type  
**Estimated scope:** M

---

## Checkpoint CPG-A (after G0–G4)

### G4: Mid-lane sync + ForceView dedup

**Closes:** D15

**Status:** DONE 2026-09-06

**Description:** Sync markers from `LegionView.position`; `pointOnLane(..., progress.value)` **set**,
no tween. Dedup by `entityId`: legion wins over `forcesBySectorId`. Keep fog-band ForceView for
inexact.

**Acceptance criteria:**
- [x] Fixture legion `position.kind === "lane"` is not drawn on a sector centre
- [x] Same `entityId` not double-drawn

**Verification:**
- [x] `npx vitest run src/game/world/systems/syncWorldSystem.test.ts` (extend)
- [x] `npm run build`

**Dependencies:** G3  
**Files likely touched:** `syncWorldSystem.ts`, `forceMarker.ts` if needed, tests  
**Estimated scope:** M

- [x] **CPG-A:** G0–G4 done; fingerprint + mid-lane units green; build green

---

## Phase C — Input

### G5: Drag suppress; ignoreRects all paths; 44px hit

**Closes:** D4, D5, D6 (D7 geometry partially — HUD rects; rail after G12)

**Status:** DONE 2026-09-06

**Description:** Drag latch until pick handled; `gameobjectup` uses ignoreRects; hit radius =
`(PIN_DISC_PX/2)/zoom` in world units from CSS px disc.

**Acceptance criteria:**
- [x] Drag past threshold → no select
- [x] Named-pin path respects ignoreRects
- [x] Hit disk matches a11y pin size across zoom

**Verification:**
- [x] `npx vitest run src/game/world/systems/worldPick*`
- [x] `npm run build`

**Dependencies:** G2  
**Files likely touched:** `worldPickSystem.ts`, `worldCameraSystem.ts`, `worldPickHit.test.ts`  
**Estimated scope:** M

---

### G6: GG-18 mute; Fit +/−; camera centre

**Closes:** D8, D9, D25 (payload); Outliner wire in G13

**Status:** DONE 2026-09-06

**Description:** Pan verbs + lens hotkeys + `W` no-op when top dismissible band is `panel`/`dialog`.
Bottom-left Fit + plus + minus; HUD zoom about viewport centre. Add `world:camera` `op: "centre"`
in EventBus + camera system.

**Acceptance criteria:**
- [x] Inspector open → Arrow / `1` do not pan/switch lens
- [x] Fit, `+`, `−` present and emit camera ops
- [x] `centre` accepted by camera system

**Verification:**
- [x] Verb / WorldStage tests; camera system unit
- [x] `npm run build`

**Dependencies:** G2  
**Files likely touched:** `EventBus.ts`, `worldCameraSystem.ts`, `WorldStage.tsx`,
`useLensHotkeys.ts` / `worldVerbs`, `useGlobalKeys` or stack check site  
**Estimated scope:** M

---

### G7: One contextmenu; edge-scroll ∩ ignoreRects

**Closes:** D10; D7 edge-scroll half

**Status:** DONE 2026-09-06

**Description:** Phaser owns empty right-click → `kind: "empty"`; host does not also `onSelect`.
Edge-scroll no-ops inside ignoreRects.

**Acceptance criteria:**
- [x] Single empty select on contextmenu
- [x] Pointer in ignoreRect → no edge-scroll pan

**Verification:**
- [x] Pick + camera units
- [x] `npm run build`

**Dependencies:** G5, G6  
**Files likely touched:** `WorldGameHost.tsx`, `worldPickSystem.ts`, `worldCameraSystem.ts`  
**Estimated scope:** S

---

## Checkpoint CPG-B (after G5–G7)

- [x] **CPG-B:** Drag/ignore/GG-18/Fit± units green; build green

---

## Phase D — Paint

### G8: Pin paint-ops + slots/loam LOD + fog stamp

**Closes:** D11, D12, D13, D18 (pin SSOT)

**Status:** DONE 2026-09-06

**Description:** Factory paint-ops for crest, border, hatch, glyph, meter, word; fog stamp from
`fogTreatments` (not literal `·`); pass `slotsBySectorId` / net-loam at LOD with type floor.
Ownership lens stays empty (assert in G10).

**Acceptance criteria:**
- [x] Paint-ops tests fail if draw ignores channels
- [x] Greyscale-distinguishable yours vs enemy without hue
- [x] Slots/loam only when lod allows; text ≥12px or omitted

**Verification:**
- [x] `npx vitest run src/game/world/objects/sectorPin*`
- [x] `npm run build`

**Dependencies:** G1  
**Files likely touched:** `sectorPin.ts`, sync pin upsert, tests  
**Estimated scope:** M

---

### G9: Lane full LaneChannels paint-ops

**Closes:** D14

**Status:** DONE 2026-09-06

**Description:** Draw `severedGlyph`, `arrowheads`, `gateGlyph`, `noSupplyMark`, `wardBadge`,
`hazardBadge` (+ existing stroke/gap).

**Acceptance criteria:**
- [x] Paint-ops cover every `LaneChannels` field
- [x] Severed shows gap **and** ✕

**Verification:**
- [x] `npx vitest run src/game/world/objects/laneStroke*`
- [x] `npm run build`

**Dependencies:** G8  
**Files likely touched:** `laneStroke.ts`, tests  
**Estimated scope:** S

---

### G10: Overlay text, envelope, blocked/inert, hops

**Closes:** D16, D17, D27, D28, D29; locks D18

**Status:** DONE 2026-09-06

**Description:** Paint planned captions/labels; “cut off” words; `supplyEnvelope` hull/per-lane;
blocked vs inert via `reasonFor`; range hop numbers. Ownership lens marks remain `[]`.

**Acceptance criteria:**
- [x] Lifeline caption / labels drawn or omitted for type floor — not silently dropped while planned
- [x] Envelope drawn for fed components
- [x] `planLensMarks` ownership still empty; test locks it

**Verification:**
- [x] Overlay plan/system tests
- [x] `npm run build`

**Dependencies:** G8  
**Files likely touched:** `worldOverlaySystem.ts`, `worldOverlayPlan.ts`, tests  
**Estimated scope:** M

---

## Phase E — Retire SVG

### G11: Hard-delete SVG camera trio

**Closes:** D19

**Status:** DONE 2026-09-06

**Description:** Delete `camera.ts`, `cameraGestures.ts`, `render/WorldScene.tsx`, and tests whose
**only** job is those files (`WorldScene.test.tsx`, `camera.test.ts`, `cameraGestures.test.ts`).
Keep overlay/`SectorNode` oracles. CI/absence: files must not exist.

**Acceptance criteria:**
- [x] Those three source files absent
- [x] No imports of them from live stage
- [x] Suite green without them

**Verification:**
- [x] Glob shows files gone; `npm run build`

**Dependencies:** G8, G9, G10  
**Files likely touched:** delete listed + fix any stray imports  
**Estimated scope:** S

---

## Checkpoint CPG-C (after G8–G11)

- [x] **CPG-C:** Paint-ops green; SVG trio absent; optional greyscale artifact + agent CV

---

## Phase F — Chrome

### G12: Mount Rail; remeasure ignoreRects

**Closes:** D22; finishes D7 rail strip

**Status:** DONE 2026-09-06

**Description:** Copy Lawn rail frame on `WorldStage`; navigate layers to Sanctum with `?panel=`.
Publish ignoreRects including rail strip in **canvas CSS space** — do not double-subtract 92px if
canvas already sits beside rail. Edge-scroll still respects rects (G7).

**Acceptance criteria:**
- [x] `#/world` shows Rail
- [x] Homeworld (or left pins) remain pickable; dock still ignored when open

**Verification:**
- [x] WorldStage test + pick ignoreRects unit
- [x] Focused vitest + `npm run build` (full `npm test` may include unrelated stream fails)

**Dependencies:** G7  
**Files likely touched:** `WorldStage.tsx`, ignoreRects helpers  
**Estimated scope:** M

---

### G13: NotifyRail → Outliner → Playback; UnresolvedCount; centre wire

**Closes:** D23, D24, D25

**Status:** DONE 2026-09-06

**Description:** Compose right column in locked order. `pointer-events-auto` on UnresolvedCount
wrapper and other controls. Outliner Enter → `world:camera` `{ op: "centre", x, y }` via
`sectorCenter` — not arrow pin-hop.

**Acceptance criteria:**
- [x] Notify above Outliner above Playback
- [x] UnresolvedCount clickable
- [x] Outliner centre pans camera to sector

**Verification:**
- [x] WorldStage / Outliner tests
- [x] Focused vitest + `npm run build` (full `npm test` may include unrelated stream fails)

**Dependencies:** G6, G12  
**Files likely touched:** `WorldStage.tsx`, `Outliner.tsx`, HUD wrappers  
**Estimated scope:** M

---

## Phase G — Prove

### G14: Honest Playwright; suites green

**Closes:** D21; finishes D20

**Description:** Remove `emitSelect` / cheating `pickAt` fallbacks from `world-map-runtime.spec.ts`.
Mouse/contextmenu only; fail if selection unset. `world-stage` + `checkpoint-f` green against Phaser
host.

**Acceptance criteria:**
- [ ] No E2E path calls probe emit to pass
- [ ] Three Playwright files green

**Verification:**
- [ ] Playwright commands in header
- [ ] Grep e2e for `emitSelect` → none (or DEV-only unused)

**Dependencies:** G5, G12, G13  
**Files likely touched:** `e2e/world-map-runtime.spec.ts`, e2e suites from G0  
**Estimated scope:** M

---

### G15: Coverage sweep + CPG-D CV

**Closes:** module SC; parent SC 10–11

**Description:** Fill any missing unit coverage called out in gaps Testing strategy. Run CPA–CPD /
CPG-D Playwright artifacts; agent `Read` CV against plan §7 (greyscale crest/stroke/hatch). Update
gaps/parent todo status when green.

**Acceptance criteria:**
- [ ] Gaps success criteria 1–10 tickable with evidence
- [ ] Greyscale CV: yours≠enemy without hue
- [ ] `npm test` + `npm run build` + Playwright green

**Verification:**
- [ ] Full verify commands; artifact PNGs Read
- [ ] Mark gaps plan/todo complete only when CV passes

**Dependencies:** G14  
**Files likely touched:** tests, task status headers, artifacts  
**Estimated scope:** M

---

## Checkpoint CPG-D (complete)

- [ ] **CPG-D:** Rail + notify + outliner; honest pick; all three e2e green; CV checklist pass
- [ ] D1–D32 closed (hover deferred only)
- [ ] Parent SC 1–12 true and proven
- [ ] Ready for owner git commit (agent does not commit)

---

## Defect → task map

| Defects | Task |
|---|---|
| D20, D32 | G0 |
| D1, D30 | G1 (+ G3 for legion half) |
| D2, D3, D31 | G2 |
| D26 | G3 |
| D15 | G4 |
| D4–D6 | G5 |
| D8, D9, D25 payload | G6 |
| D10, D7 edge | G7 |
| D11–D13, D18 SSOT | G8 |
| D14 | G9 |
| D16–D17, D27–D29, D18 lock | G10 |
| D19 | G11 |
| D22, D7 rail | G12 |
| D23–D25 | G13 |
| D21 | G14 |
| SC prove | G15 |
