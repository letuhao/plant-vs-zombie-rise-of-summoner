# Spec: world-map-gaps

**Status: Specify complete 2026-09-06 — Plan ready
([world-map-runtime-gaps-plan.md](../../../tasks/world-map-runtime-gaps-plan.md) ·
[world-map-runtime-gaps-todo.md](../../../tasks/world-map-runtime-gaps-todo.md)); Implement after
owner plan review.** Module id `world-map-gaps` in the
[world-map-runtime capability map](../world-map-runtime-map.md).

**Parent HOW (not reopened):** [spec-world-map-runtime.md](spec-world-map-runtime.md). This module
closes that spec's success criteria **1–12** against **shipped code**, plus the chrome composition
the Phaser swap left unhooked. Coverage of each parent SC is in the matrix below.

**Ideal / catalog / GUI:** same as the parent. Loops: [the-loops.md](../../guide/the-loops.md) §4
World map — adventure and §5 World stage — empire building.

**Audit this spec encodes (2026-09-06):** four code reviews of host, objects/overlays, camera/pick/e2e,
and leftover SVG/chrome. Coverage pass same day locked underspecified close-withs (D11/D14/D15/D18/D19)
and added D26–D32. File:line citations were verified against `web/fusion-rpg-web/src/` that session.
Code beats this document if they drift — re-verify before implementing.

This spec does **not** respecify `step()`, fog *rules*, inspector fields, or channel matrices. It
names **defects**, **contracts that close them**, and **how we know**.

---

## Assumptions (correct now or the implementer will)

1. Parent architecture stays: Phaser dual-plane, host `modelSeq`, four systems only, channels under
   `stages/world/render/`, no `WorldStateDto.Revision`, no xyflow, no Phaser UIScene.
2. Owner chose **full-stage chrome composition**: mount already-built `Rail`, `NotifyRail`, and
   `Outliner` on `WorldStage`. This is wiring, not a HUD redesign (`world-stage` W90–W93 / notify
   already exist). It does **not** reopen `world-commands`, recruitment, or fog rules.
3. Mid-lane march uses existing [`LegionView.position`](../../../web/fusion-rpg-web/src/contract/types.ts)
   (`kind: "lane"` + `progress` per-mille). `ForceView` has no position and is not the source.
   `AdaptedWorldState` does **not** carry legions — the host takes sibling `legions` +
   `playerFactionId` props (D15/D26). Do **not** merge `LegionView` into `ForceView`.
4. GG-18 mute of arrows / lens `1`–`6` is a **React keymap** check (top stack band), not a fifth
   Phaser system.
5. Playwright may read pin **coordinates** from a DEV/test probe. Emitting `world:select` from
   `window` (`emitSelect` / self-emitting `pickAt`) is forbidden in production and is not an E2E
   pass.
6. Delete SVG `WorldScene` / `camera.ts` / `cameraGestures.ts` only after pin/lane/overlay
   **paint-ops** tests hold the encodings those files currently oracle (D19 is a hard delete —
   no dead-export escape hatch).
7. There is **no** `npm run lint` in `web/fusion-rpg-web`. Verification is `npm test` + `npm run build`
   + Playwright.
8. Targeting (reachable / pending routes / blocked) rides `world:interaction`, **not** the graph
   fingerprint / `modelSeq`. Do not hash targeting into `worldModelProjection`.
9. Hover ring stays **optional v1** (parent already allows Phaser-local hover; no `world:hover`).
   Explicit deferral — no D-id.

---

## Objective

**User.** The summoner on World. They must read ownership/health/fog on compact pins (GG-27), pan and
pick without fighting chrome, and find the same corner HUD Lawn/Sanctum already have (rail,
outliner, notify).

**Why this module exists.** `world-map-runtime` R0–R16 landed a Phaser host on `#/world` and then
marked complete. The parent success criteria are **not** true of current code: pins are hue+border,
dirty-flag misses turn updates, pick/E2E cheat, SVG camera files remain, Rail/Outliner/NotifyRail
are uncomposed. Several close-withs in the first gaps draft were underspecified (notably mid-lane:
`AdaptedWorldState` has no legions).

**Success.** Parent SC **1–12** are true **and proven** (honest Playwright, no `emitSelect` fallback;
agent CV on artifacts per [world-map-runtime-plan.md](../../../tasks/world-map-runtime-plan.md) §6).
Chrome table in [spec-world-hud.md](../world-stage/spec-world-hud.md) §1 is composed on the live
stage. Stale SVG e2e (`world-stage.spec.ts`, `checkpoint-f.spec.ts`) targets the Phaser host.

---

## Parent SC coverage matrix

| Parent SC | Status vs shipped code | Closed by |
|---|---|---|
| **1** GG-11 mount (inspector does not destroy Game) | **Holds** | CPA mount probe stays; no new D |
| **2** Pins / fog density / no opacity | **Broken** | D11–D13 |
| **3** Inspector left dock (§A card React) | **Holds** | No new D |
| **4** Wheel about pointer; drag; edge-scroll; arrows when map owns input; `W` cycles | **Partial** | D4, D8, D9; edge-scroll holds + D7 (no-op inside ignoreRects) |
| **5** Pick ignores rail + open inspector + HUD; right-click empty | **Broken** | D5–D7, D10, D22 |
| **6** `world:*` union; Sync keys on `modelSeq` | **Partial** | D1, D26 (legions on model); bus union holds |
| **7** Import / hex / snapshotTheme | **Holds** | Import guard test stays |
| **8** Phaser draws all map-plane overlays | **Partial** | D16–D18, D26–D29 |
| **9** SVG `WorldScene` / `camera.ts` gone | **Broken** | D19 (strict delete) |
| **10** `npm test` + `npm run build` | Gate | This module SC 9 |
| **11** CPA–CPD Playwright + CV | **Partial** | D20–D21; greyscale CV now requires D11 paint |
| **12** R16 T3 HOW = Phaser | **Holds** | [`tech-stack.md`](../../design/tech-stack.md) T3 already Phaser |

---

## Tech stack

Unchanged from the parent: Phaser `^4.2.1` (Scenes + GameObjects), React HUD, existing
`sectorChannels` / `laneChannels` / `fogTreatments`. **No new npm packages.** No fifth system.

---

## Defect register (SSOT for this module)

Each row is in-scope. Closing it is the work. "Keep as oracle" means unit tests on the React file
may remain until **paint-ops** tests replace them; the **live stage** must not mount the React map.
D1–D25 ids stay stable; D26–D32 append.

### Host / dirty flag / generation

| Id | Defect | Evidence | Close-with |
|---|---|---|---|
| D1 | `modelFingerprint` under-hashes; ownership/health/forces/lanes/layout/legion position can change without bumping `modelSeq` | `WorldGameHost.tsx` `modelFingerprint` uses sector count, lane count, `overlayEpoch`, `sectorId+intel` only | Canonical projection (below); unit test: same intel, changed owner → emit |
| D2 | Fit / arrow pan read `window.__fusionRpgWorldGen`; never cleared on destroy | `WorldStage.tsx` pan/Fit; `WorldMapScene.ts` `create` writes the global; `shutdown` does not delete | Host publishes generation to React (callback/state). Clear window keys in `shutdown` / host unmount |
| D3 | Production `emitSelect` / `pickAt` emit `world:select` | `WorldMapScene.ts` probe object | Production: coordinate helpers only, or omit. DEV/Playwright: coordinates OK; emit forbidden in prod bundle |
| D26 | Host never publishes `LegionView[]` / `playerFactionId` on `world:model` | `AdaptedWorldState` has no entities; `WorldStage` adapts legions beside the host; Sync never sees `position` | Sibling props on `WorldGameHost` (Contracts — Mid-lane); publish on `world:model`; fingerprint includes them (D30) |
| D30 | Fingerprint omits `slotsBySectorId` and `playerFactionId` | Projection list in first draft; slots never reach pin LOD (D12) | Include sorted slot keys + `playerFactionId` in `worldModelProjection` |
| D31 | `console.info` on every host / overlay draw | `WorldGameHost.tsx`, `worldOverlaySystem.ts` ~279 | DEV-only or remove; not a production hot-path log |

### Pick / camera / GG-18

| Id | Defect | Evidence | Close-with |
|---|---|---|---|
| D4 | Drag does not suppress pick | `worldCameraSystem` resets `dragging` on up; pick never reads it | Drag latch until pointerup handled; pick no-ops if drag exceeded `DRAG_THRESHOLD_PX` |
| D5 | `gameobjectup` ignores `ignoreRects` | `worldPickSystem.ts` named-pin path | Same occlusion check as pointerup/DOM |
| D6 | Hit radius is `28` world units, not 44px a11y | `worldPickSystem.ts` / `worldPickHit.test.ts` / probe `pickAt` | Hit disk is `PIN_DISC_PX` in **CSS pixels**: world radius = `(PIN_DISC_PX / 2) / camera.zoom` |
| D7 | `ignoreRects` omit HUD corners (and rail after D22); edge-scroll ignores them | `WorldStage.tsx` dock-only 380×10000 | Publish dock + top strip + bottom-left (Fit/`+/−`/lenses) + bottom-right (turn) + rail strip in **canvas CSS space**. Edge-scroll **no-ops** when pointer is inside any ignoreRect |
| D8 | Arrows and lens `1`–`6` fire with inspector open | `useGlobalKeys` always `dispatchGlobalVerb`; pan verbs always registered; `useLensHotkeys` always registers | Handlers no-op when top dismissible band is `panel` / `dialog` (GG-18). `W` cycle same rule |
| D9 | No HUD `+/−`; `op:"zoom"` is not about-pointer | Fit only in `WorldStage.tsx` | Fit + plus + minus. Command zoom about viewport centre. Wheel stays about pointer |
| D10 | Dual right-click (host `onContextMenu` + Phaser) | `WorldGameHost.tsx` + `worldPickSystem` | One owner: Phaser canvas contextmenu → `kind: "empty"`; host does not also call `onSelect` |

### Visual contract (parent Visual contract + Sync §6)

| Id | Defect | Evidence | Close-with |
|---|---|---|---|
| D11 | Pin paint ignores crest, pattern, glyph, meter, word | `sectorPin.ts` `drawDisc` fill+border only; `pinDescriptor` already carries `channels` | Factory emits a **paint-ops** list consumed by draw (crest / border / hatch / glyph / meter / word from `channelsFor`). Greyscale yours vs enemy without hue. **SSOT for default ownership view** — see D18 |
| D12 | LOD slot dots/shapes and net-loam chip never drawn | `zoomTier.ts` lists them; sync never passes slots | Pass `slotsBySectorId` into pin; draw only when `lodChannels` includes them; type floor: omit if &lt;12px |
| D13 | Fog pip is literal `·`, not hearsay vs dated stamp | `sectorPin.ts` pip text | Stamp from `fogTreatments` (map+); wash detail-only (already gated) |
| D14 | Lane missing gap+✕, arrow, lock/gate, badges | `laneStroke.ts` draws gap stroke but not full `LaneChannels` | Paint-ops for **every** field: `severedGlyph`, `arrowheads`, `gateGlyph`, `noSupplyMark`, `wardBadge`, `hazardBadge` (+ existing stroke/gap) |
| D15 | Mid-lane forces parked at sector centre; `pointOnLane` unused | `syncWorldSystem.ts` `upsertForces`; `ForceView` has no position; legions never on model | Sync from host-published `LegionView.position`; `pointOnLane(..., progress.value)` **set**, no tween; dedup by `entityId` (Contracts) |
| D16 | Supply cutoff words (and envelope — see D28) incomplete | Phaser cutoff X only vs `SupplyOverlay.tsx` | Draw “cut off” words (GG-23/27); picker stays React. Envelope is D28 |
| D17 | Lifeline caption planned, not painted | `worldOverlayPlan.ts` vs `worldOverlaySystem.ts` | Draw the caption the plan already carries (type floor ≥12px or omit) |
| D18 | Ownership lens marks skipped while pins lack GG-27 | `planLensMarks` returns `[]` for ownership | **D11 is SSOT.** Ownership lens marks stay empty **after** D11 paint-ops tests pass. Do **not** ship both. Closing D18 by restoring lens marks without D11 is a fail |
| D27 | Overlay text (`caption` / `label` / range hops) never drawn | Plan carries them; `worldOverlaySystem` Graphics-only | Paint planned text; type floor ≥12px at 720p or omit the label |
| D28 | Supply envelope unused | `SupplyOverlay.tsx` + `supplyEnvelope.ts` vs Phaser cutoff-only | Phaser draws hull/per-lane envelope from existing `supplyEnvelope.ts` (Phaser-free math may stay under `stages/world/render/`) |
| D29 | Blocked treatments + hop numbers vs oracles | `BlockedTarget.tsx` blocked/inert/available + `reasonFor`; `RangeOverlay` hop numbers | Blocked: distinct blocked vs inert (available = absent). Range rings carry hop numbers. Caption via `reasonFor`, not raw tokens |

### Leftover SVG / stale e2e / chrome / doc drift

| Id | Defect | Evidence | Close-with |
|---|---|---|---|
| D19 | `camera.ts`, `cameraGestures.ts`, `WorldScene.tsx` still on disk | Folder law / parent SC 9; `WorldScene.test.tsx` still imports composer | **Hard delete** those three files **and** tests whose only job is those files. No “dead export / CI soft fail” escape. Keep overlay/`SectorNode` oracles until D11–D17 + D26–D29 paint-ops exist |
| D20 | `e2e/world-stage.spec.ts` + `checkpoint-f.spec.ts` require `world-stage-svg` | those files; `#/world-stage` is Phaser `WorldStage` | Retarget to `world-game-host` / canvas; fixture `stages/world/fixtures/` |
| D21 | E2E `clickPin` mouse then `pickAt`/`emitSelect` | `world-map-runtime.spec.ts` | Mouse/contextmenu only; fail if `data-selected-sector` unset |
| D22 | No `Rail` on World | `LawnStage.tsx` / `SanctumStage.tsx` mount `Rail`; `WorldStage` does not; DockShell `left-[92px]` | Mount rail like Lawn (navigate layers to Sanctum). Re-measure ignoreRects in **canvas CSS space** — do not double-subtract 92px if the canvas already sits beside the rail |
| D23 | UnresolvedCount not clickable | `WorldHud` `pointer-events-none`; bottom-right wrapper has no `pointer-events-auto` | `pointer-events-auto` on the wrapper (and UnresolvedCount if needed) |
| D24 | Outliner + NotifyRail uncomposed | Components exist; `WorldStage` right edge is PlaybackPanel only | Right column **order locked**: NotifyRail → Outliner → Playback. Bottom-left: Fit, `+/−`, LensPicker |
| D25 | Outliner `onCentreRequest` still named against SVG `centreOn` | `Outliner.tsx` comment | `world:camera` `{ op: "centre", x, y }` from `sectorCenter` — **not** arrow pin-hop |
| D32 | Stale GG-38 / SVG comments | `routes.tsx` still says World is “the SVG stage”; parent HOW Objective still claimed live SVG cards | Fix comments; parent HOW Objective + Event table already amended this pass |

**Explicitly deferred (no D):** Phaser-local hover ring (optional v1).

**Out of this module (do not "fix" by expanding):** turn engine, fog rule tables, `WorldStateDto.Revision`,
new npm renderer, Phaser HUD scene, world-commands/cede UX, NotifyRail **store** behaviour (already
specced in `spec-world-notify`), merging `LegionView` into `ForceView`.

---

## Contracts (new or tightened)

### Dirty flag — canonical projection

Host increments `modelSeq` and emits `world:model` when this projection **string** changes (or
`overlayEpoch` changes). Do **not** use `WorldHeaderDto.revision`.

Include, sorted by id:

- `playerFactionId` (sibling prop — not on `AdaptedWorldState` alone)
- every sector: `sectorId`, `intel`, `intelAge`, `ownerFactionId`, `layoutX`, `layoutY`, fields
  `channelsFor` / `healthOf` / `ownershipOf` read, `loamNet`, `dangerBand`, `lifeline`, `lifelineCost`
- every lane: `laneId`, endpoints, `state`, kind, width, ward, hazard
- every entry in `slotsBySectorId`: `sectorId` + sorted `slotIndex` / `slotTypeId` / `state` (enough
  for D12 LOD to dirty)
- every force in `forcesBySectorId`: `entityId`, `kind`, `ownerFactionId`, exactness
- every **legion** from the host sibling `legions` prop: `entityId`, `position` (sector id **or**
  `laneId` + `towardSectorId` + `progress.value`)

`overlayEpoch` remains a bump for lens-4 fetch / overlay inputs that do not change the graph.

**Targeting is not in this projection.** Reachable / pending / blocked ride `world:interaction` and
may change without bumping `modelSeq`.

### Generation — host owns it

`WorldGameHost` holds `generationRef` from `allocGameGeneration()`. It passes `generation` to
`WorldStage` via a callback (`onGeneration`) or equivalent React state. Fit, arrows, and
`world:interaction` from the stage **must** use that value. `window.__fusionRpgWorldGen` is not the
production path. Destroy clears any test globals.

### Mid-lane — host sibling props (D15 / D26)

`AdaptedWorldState` stays `{ sectors, lanes, slotsBySectorId, forcesBySectorId }`. Do **not** add
`Revision` to the DTO. Do **not** fold `LegionView` into `ForceView`.

```ts
// Intent — WorldGameHost props (names may match existing SyncWorldModel extras)
legions: LegionView[];          // ALL adapted entities, not only myLegions
playerFactionId: string | null;
```

- `WorldStage` adapts `dto.entities` via `adaptWorldLegion` and passes **all** of them (enemy/open
  marches must sit on a lane when intel makes them exact). `myLegions` remains the turn-cluster
  filter; the map host is wider.
- Publish `legions` + `playerFactionId` on `world:model` (alongside adapted graph).
- Sync **dedups by `entityId`:** if a `LegionView` exists, that marker uses `position` (sector centre
  or `pointOnLane`); do **not** also draw that id from `forcesBySectorId`. Fog-band / inexact
  `ForceView` stays for occupancy that is not an exact legion.
- Mid-lane: `pointOnLane(..., progress.value)` **set** on apply — no tween.

### Camera payload

```ts
op: "pan" | "zoom" | "fit" | "centre"
```

- `centre` uses world coordinates (`sectorCenter`). Outliner Enter is the consumer. Arrows remain
  pan-only (parent plate §O.6).
- `zoom` from HUD `+/−` is about the **viewport centre**. Wheel remains about pointer.

### Pick

1. If pointer in any `ignoreRect` (canvas CSS px) → no select (all pick paths).
2. If this gesture was a drag (`hypot >= DRAG_THRESHOLD_PX`) → no select.
3. Else nearest pin whose **screen** disc (radius `PIN_DISC_PX/2`) contains the pointer.
4. Right-click empty → `kind: "empty"` once.
5. Hover highlight is Phaser-local (parent). Optional v1 one-channel hover ring; no `world:hover`.
6. Edge-scroll no-ops while the pointer is inside any published ignoreRect (D7).

### Probe (test-only)

Allowed in `import.meta.env.DEV` or when `window.__PLAYWRIGHT === true`: `pinCount`, `pinScreen`
(CSS px relative to canvas). **Forbidden in production builds:** functions that `worldBusEmit`
`world:select`.

### GG-18

When `useLayerStack` top **dismissible** band is `panel` or `dialog`, world pan verbs and lens
hotkeys no-op. Inspector (`DockShell`) is that case. Stage Esc/right-click empty still go through
the existing stack (`handleEscape`).

### Paint-ops (D11 / D14 / overlays)

Descriptor tests that only assert `descriptor.channels` can go green while draw stays hue+border.
**Required:** tests assert a **paint-ops** list (or equivalent) that the factory's draw path
consumes:

- **Pin:** crest, border style/weight, hatch/pattern, glyph, meter, word, fog stamp text — not fill
  hue alone.
- **Lane:** full `LaneChannels` including `severedGlyph`, `arrowheads`, `gateGlyph`, `noSupplyMark`,
  `wardBadge`, `hazardBadge`.
- **Overlays:** planned `caption` / `label` / hop numbers / “cut off” words / envelope geometry;
  blocked vs inert distinction.

Type floor: fact-bearing Phaser Text ≥ **12px at 720p**; omit the label if it cannot meet the floor
(parent / `spec-world-render`).

**D11 vs D18:** default map ownership is pin paint (D11). Ownership lens marks remain empty once
D11 paint-ops pass. Do not restore `planLensMarks` ownership marks as a substitute for D11.

### Chrome composition (authorized wiring)

Copy [LawnStage.tsx](../../../web/fusion-rpg-web/src/stages/lawn/LawnStage.tsx) rail frame: `Rail` +
flex child. Unlock inputs may duplicate Sanctum's queries (react-query dedupes). Rail click that
opens a layer **navigates to Sanctum** with `?panel=` (same as Lawn) — do not invent in-place layer
mounting on World.

Corner occupancy ([spec-world-hud.md](../world-stage/spec-world-hud.md) §1), **order locked:**

| Anchor | Occupants (top → bottom / left → right as applicable) |
|---|---|
| **Top-left** | `Rail` (shell) |
| **Left** | Inspector dock when open |
| **Right** | `NotifyRail` → `Outliner` → Playback |
| **Bottom-left** | Fit, `+/−`, LensPicker |
| **Bottom-right** | Turn cluster (incl. UnresolvedCount, clickable) |

`pointer-events-auto` on every HUD occupant that is a control (Fit, `+/−`, lenses, turn cluster,
UnresolvedCount, outliner, notify, rail).

After Rail mounts, re-measure ignoreRects in **canvas CSS space**. If the map canvas already sits
beside the rail, do **not** double-subtract 92px (current `WorldStage` comment is the right instinct).

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test
npm run build
# No npm run lint
npx playwright test e2e/world-map-runtime.spec.ts e2e/world-stage.spec.ts e2e/checkpoint-f.spec.ts
```

Descriptor / unit focus (examples):

```powershell
npx vitest run src/stages/world/host/WorldGameHost.test.tsx src/game/world src/stages/world/WorldStage.test.tsx
```

Repo guards unchanged (`guard-dal.ps1`; hex skip `game/`). Artifacts:
`web/fusion-rpg-web/e2e/.artifacts/world-map-runtime/`.
CV checklist: [world-map-runtime-plan.md](../../../tasks/world-map-runtime-plan.md) §6 — greyscale
shot must show D11 non-hue channels (crest / stroke / hatch), not hue alone.

---

## Project structure

No new top-level folders. Work stays in:

```text
web/fusion-rpg-web/src/game/world/          # factories, systems, scene, importGuard
web/fusion-rpg-web/src/game/EventBus.ts     # add op "centre" only
web/fusion-rpg-web/src/stages/world/host/   # fingerprint, generation callback, legions props
web/fusion-rpg-web/src/stages/world/WorldStage.tsx  # rail, chrome, ignoreRects, verbs, legions→host
web/fusion-rpg-web/e2e/                     # honest specs; retarget SVG suites
docs/architecture/world-map-runtime/        # this spec
```

**Delete (D19 — hard):** `stages/world/camera.ts`, `cameraGestures.ts`, `render/WorldScene.tsx`
**and** `WorldScene.test.tsx` (or any test whose only job is those files). Keep
`RangeOverlay.tsx` / `SupplyOverlay.tsx` / `LifelineOverlay.tsx` / `BlockedTarget.tsx` /
`SectorNode.tsx` / `supplyEnvelope.ts` as **encoding oracles** until paint-ops tests fully replace
them; they must not be imported by `WorldStage`.

Colocated tests: `objects/sectorPin.test.ts` (paint-ops), `laneStroke.test.ts` (full LaneChannels),
`forceMarker.test.ts`, pick occlusion unit, fingerprint unit (slots + legion position + playerFactionId),
GG-18 verb mute test, overlay text/envelope units.

---

## Code style

Parent style: factories map channel records; Scene does not `switch (dto.typeId)`. Structural
consts keep the "not a balance tunable" comment ([tunables-ssot.md](../tunables-ssot.md)).

Fingerprint (intent, not required verbatim):

```ts
/** Projection the dirty flag hashes. Structural completeness, not a yield. */
export function worldModelProjection(
  model: SyncWorldModel, // adapted graph + legions + playerFactionId
  overlayEpoch: number
): string {
  // sorted ids; include paint-driving fields listed in Contracts — never header.revision
  // never targeting (that is world:interaction)
}
```

Hit radius (intent):

```ts
const screenR = PIN_DISC_PX / 2; // a11y disc; not a yield
const worldR = screenR / scene.cameras.main.zoom;
```

Magnitudes on pins (net loam) stay `long` via `world-numbers`. No `float` HP. No new `P(Θ)`.
Camera min/max, `FIT_MAX` / `DETAIL_MIN`, `PIN_DISC_PX`, drag threshold, edge-scroll margin remain
**structural**.

---

## Testing strategy

| Level | Proves | Must not |
|---|---|---|
| Vitest paint-ops | Channel matrix → pin/lane/overlay ops ≥2 non-colour; crest/hatch/word drawn; no `opacity`; fog stamp; intel-first diamond; full LaneChannels | Real `Phaser.Game`; descriptor-only green while draw is hue+border |
| Vitest host | Fingerprint D1/D30 (slots, owner, legion position, playerFactionId); buffer until ready; GG-11 remount; generation callback; legions on model | Window gen as SSOT; hashing targeting into modelSeq |
| Vitest pick | ignoreRects all paths; drag suppresses; zoom-scaled radius; edge-scroll respects ignoreRects | Screen `getBounds` as world hit |
| Vitest verbs | Inspector panel open → Arrow / `1` do not pan/switch lens | Changing global keymap for Sanctum |
| Vitest overlay | Caption/label/hops/cut-off words/envelope; blocked vs inert; ownership lens empty after D11 | Restoring ownership lens marks instead of D11 |
| Import guard | `game/world` ↛ React / `lib/bus` / `*Dto` | Forbidding channel imports |
| Playwright | Real mouse/wheel/drag/contextmenu; rail visible; left inspector; right NotifyRail→Outliner; no SVG testids | `emitSelect` to make the assertion pass |
| Agent CV | PNGs: soil canvas, pins not 192px cards, left dock, greyscale shows crest/stroke/hatch (D11) | Owner eyeball gate; greyscale that only differs by hue |

**Anti-cheat.** An E2E that clicks then calls `worldBusEmit("world:select")` does not prove D4–D6 or
parent SC 5.

**Stale suite.** `world-stage.spec.ts` fixture path is `stages/world/fixtures/`, not
`features/world/fixtures/`.

---

## Boundaries

- **Always:** dual-plane locks; intel-first; GG-27 two channels; tokens via `snapshotTheme`; destroy
  checklist; mock Phaser in unit tests; `modelSeq` dirty-flag; pick ignores rail + dock + HUD;
  Phaser only from the World lazy chunk; honest Playwright; compose Rail/Outliner/NotifyRail from
  existing modules; paint-ops tests for pin/lane/overlay; host sibling `legions` + `playerFactionId`;
  hard-delete SVG camera trio after paint-ops land.
- **Ask first:** a fifth Phaser system; Phaser UIScene; xyflow on the player map; adding
  `Revision` to `WorldStateDto`; moving channels to `src/lib/world-view/`; mounting Sanctum layers
  in-place over World instead of navigating; changing fog *rules*; merging `LegionView` into
  `ForceView`.
- **Never:** Phaser `fetch` / SignalR / `*Dto` in `game/`; React GameObject refs; health as opacity;
  infer unknown from emptiness; two `Phaser.Game`s; restore SVG `viewBox` as the player camera;
  E2E completion via probe emit; hard progression cap; private `f(level)`; `npm run lint` as a gate;
  owner-eyeball checkpoint; closing D18 without D11; hashing targeting into the graph fingerprint.

---

## Success criteria

Parent SC **1–12** remain binding (see coverage matrix). This module adds:

1. D1–D32 closed or explicitly deferred in this spec (hover ring deferred; none of D1–D32 deferred).
2. Fingerprint unit: identical intel, changed `ownerFactionId`, `slotsBySectorId`, or legion
   `position` → `modelSeq` increments; targeting-only change does **not**.
3. Greyscale pin shot: yours vs enemy distinguishable without hue (crest / stroke / hatch — CV
   checklist §6).
4. Mid-lane: a fixture legion with `position.kind === "lane"` is not drawn on a sector centre;
   host publishes all `LegionView`s.
5. `#/world` shows `Rail`; inspector still left; right column NotifyRail → Outliner → Playback;
   bottom-left Fit + `+/−` + LensPicker.
6. Playwright pick: `data-selected-sector` changes from **mouse** only; contextmenu clears; click on
   lens picker does not change selection.
7. `camera.ts` / `cameraGestures.ts` / `render/WorldScene.tsx` **absent** (hard delete — no soft CI).
8. `e2e/world-stage.spec.ts` and `checkpoint-f.spec.ts` green against Phaser host.
9. `npm test` and `npm run build` green.
10. Ownership lens marks remain empty once D11 paint-ops pass; supply envelope + overlay text drawn.

---

## Open questions

None that block Specify. Owner already chose full-stage chrome. If review cuts D22–D25, say so —
the map-plane defects D1–D21 and D26–D32 still stand.

---

## DESIGN-GATE §5 (this session)

```
[x] Subsystems: world map plane, FE game foundation, Game GUI, world-stage HUD/inspector/lenses.
[x] Read this session: DESIGN-GATE world-map + UI rows, software-architecture.md §1,
    decisions.md Game GUI / D2, world-map-runtime-ideal.md intro, spec-world-map-runtime.md,
    world-map-runtime-map.md, tech-stack.md T3, spec-world-hud.md §1, spec-world-lenses.md,
    SupplyOverlay / RangeOverlay / BlockedTarget / adapt.ts / WorldStage legions,
    the-loops.md §4–§5, tunables-ssot.md class test.
[x] Verified against code: fingerprint, probe emit, sectorPin paint, laneStroke gap-only,
    sync forces at centres, AdaptedWorldState shape, WorldStage myLegions filter, e2e SVG selectors.
[x] No §2 invariant contradicted. Gameless-first: web Phaser, Fusion closed.
[ ] Constraint suites not re-run this Specify pass (honest: spec-only).
[x] Assumptions listed above; chrome composition is wiring of built world-stage modules;
    D15 locked via host sibling props (not AdaptedWorldState widen).
```

**Honest gap:** `game-gui-principles.md` not re-read past §0 this coverage pass; GG-11/18/27/38 cited
from prior session + parent HOW. Overlay principle does not constrain web map pixels.
