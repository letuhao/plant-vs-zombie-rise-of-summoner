# World map runtime gaps — implementation plan

**Program:** `world-map-runtime` · **Module:** `world-map-gaps`
**Map:** [docs/architecture/world-map-runtime-map.md](../docs/architecture/world-map-runtime-map.md)
**Spec (SSOT):** [docs/architecture/world-map-runtime/spec-world-map-gaps.md](../docs/architecture/world-map-runtime/spec-world-map-gaps.md)
**Parent HOW:** [docs/architecture/world-map-runtime/spec-world-map-runtime.md](../docs/architecture/world-map-runtime/spec-world-map-runtime.md)
**Tasks:** [world-map-runtime-gaps-todo.md](world-map-runtime-gaps-todo.md)

**Status:** **COMPLETE** 2026-09-06 — CPG-A…D closed; D1–D32 done (hover deferred).
**Paths written:** `tasks/world-map-runtime-gaps-plan.md` · `tasks/world-map-runtime-gaps-todo.md`.
Never `tasks/plan.md` / `tasks/todo.md`.

---

## 1. What this plans

Close parent SC **1–12** against shipped Phaser code by fixing defects **D1–D32** in the gaps
spec: host dirty-flag / generation / legions on model, pick+camera+GG-18, pin/lane/overlay
paint-ops, SVG retirement, chrome composition (Rail / NotifyRail / Outliner), honest Playwright.

**Does not plan:** turn engine, fog *rules*, `WorldStateDto.Revision`, fifth Phaser system,
xyflow, Phaser UIScene, Sanctum-in-place layers, NotifyRail *store* redesign, hover ring (deferred
v1), merging `LegionView` into `ForceView`.

**Does not restate the spec.** Contracts stay in `spec-world-map-gaps.md`. This file is order,
vertical slices, checkpoints, risks.

**Relation to R0–R16:** [world-map-runtime-todo.md](world-map-runtime-todo.md) “implementation
complete” is **false** until this module’s success criteria hold. Do not rewrite R0–R16 history;
reopen status on the parent plan/todo headers when G0 lands.

---

## 2. Dependency graph

```text
G0 status + SVG e2e retarget (D20, D32)
        │
        ▼
G1–G2 fingerprint + generation + probe (D1–D3, D30, D31)
        │
        ▼
G3 legions on world:model (D26) ──► G4 mid-lane sync (D15)
        │
        ▼
G5–G7 pick / drag / ignoreRects / GG-18 / Fit± / centre (D4–D10, D25)
        │
        ▼
G8–G10 pin + lane + overlay paint-ops (D11–D14, D16–D18, D27–D29)
        │
        ▼
G11 hard-delete SVG trio (D19)     [only after paint-ops green]
        │
        ▼
G12–G13 Rail + NotifyRail/Outliner + pointer-events (D22–D24)
        │                              │
        └──────────► ignoreRects remeasure (finishes D7)
        │
        ▼
G14–G15 honest Playwright + unit gaps + CPA–CPD CV (D21; SC 10–11)
```

**Must be sequential:** G3 before G4; G8–G10 before G11; G12 before final ignoreRects assert.
**Safe to overlap after G2:** G5–G7 (pick) can start while G8 (pin paint) is in flight if two agents
coordinate on `WorldStage.tsx` — prefer one owner for that file.

---

## 3. Vertical slicing

| Phase | Vertical outcome | Defects |
|---|---|---|
| **0 Triage** | Stale SVG e2e retargeted; parent “complete” reopened; routes comment fixed | D20, D32 |
| **A Host truth** | Full projection bumps `modelSeq`; generation via host callback; probe cannot emit select; DEV logs only | D1–D3, D26, D30, D31 |
| **B March** | Legion on lane draws off sector centre; ForceView dedup | D15 (+ D26) |
| **C Input** | Drag≠click; ignoreRects+HUD; 44px hit; GG-18 mute; Fit±; centre; one contextmenu | D4–D10, D25 |
| **D Paint** | Pin GG-27 paint-ops; full LaneChannels; overlay text/envelope/blocked/hops; D11 SSOT for ownership | D11–D14, D16–D18, D27–D29 |
| **E Retire SVG** | `camera.ts` / `cameraGestures.ts` / `WorldScene.tsx` (+ their-only tests) **absent** | D19 |
| **F Chrome** | Rail; NotifyRail→Outliner→Playback; UnresolvedCount clickable; ignoreRects remeasured | D22–D24, D7 finish |
| **G Prove** | Mouse-only e2e; world-stage + checkpoint-f green; CPA–CPD CV with greyscale crest | D21; SC 10–11 |

---

## 4. Gates vs checkpoints

**No hard pre-work gate.** Owner already chose full-stage chrome (spec assumption 2). Spec is
Specify-complete; this plan’s human review is a normal **checkpoint**, not an irreversible halt.

**No owner-eyeball gate.** Checkpoints = `npm test` + `npm run build` + Playwright + agent CV on
PNGs under `e2e/.artifacts/world-map-runtime/` (same convention as R0–R16 CPA–CPD;
[world-map-runtime-plan.md](world-map-runtime-plan.md) §6).

### Checkpoint ids (gaps)

| Id | After | Assert |
|---|---|---|
| **CPG-A** | G0–G4 | Unit: fingerprint / legion mid-lane; build green; probe has no prod emit |
| **CPG-B** | G5–G7 | Unit: drag suppress, ignoreRects, GG-18 mute; Fit± present |
| **CPG-C** | G8–G11 | Paint-ops green; greyscale unit/CV intent; SVG files **absent** |
| **CPG-D** | G12–G15 | Rail + outliner visible; honest pick e2e; world-stage + checkpoint-f green; CPA–CPD CV |

---

## 5. Architecture decisions (locked in spec — do not reopen)

1. Host sibling `legions` + `playerFactionId` on `world:model` — not `AdaptedWorldState` widen, not
   `WorldStateDto.Revision`.
2. Dedup markers by `entityId`; fog-band `ForceView` for inexact only.
3. D11 pin paint is SSOT for default ownership; ownership lens marks stay empty after D11.
4. Paint-ops tests required — descriptor-only green while draw is hue+border is a fail.
5. Targeting rides `world:interaction`, never the graph fingerprint.
6. D19 is a **hard delete** (no dead-export escape).
7. Right column order: NotifyRail → Outliner → Playback. Bottom-left: Fit, `+/−`, LensPicker.
8. After Rail: ignoreRects in canvas CSS space — do not double-subtract 92px if canvas is already
   beside the rail.

---

## 6. Task index

| Id | Title | Depends | Size |
|---|---|---|---|
| G0 | Reopen parent complete; retarget SVG e2e; fix routes GG-38 comment | — | S |
| G1 | Canonical `worldModelProjection` + fingerprint tests (slots, owner, playerFactionId) | G0 | M |
| G2 | Host generation callback; clear window globals; DEV-only logs; probe no emit | G1 | M |
| G3 | Publish all `LegionView`s + `playerFactionId` on `world:model` | G2 | M |
| G4 | Mid-lane sync via `pointOnLane`; ForceView dedup | G3 | M |
| G5 | Drag latch suppresses pick; ignoreRects on all pick paths; zoom-scaled hit radius | G2 | M |
| G6 | GG-18 mute pan/lens/`W` under panel; Fit `+/−`; `op:"centre"` | G2 | M |
| G7 | Single contextmenu owner (Phaser); edge-scroll ∩ ignoreRects | G5, G6 | S |
| G8 | Pin paint-ops (crest/hatch/glyph/meter/word/fog stamp) + slots/loam LOD | G1 | M |
| G9 | Lane full `LaneChannels` paint-ops | G8 | S |
| G10 | Overlay text, supply envelope, blocked/inert, hops; keep ownership lens empty | G8 | M |
| G11 | Hard-delete SVG camera trio + their-only tests | G8–G10 | S |
| G12 | Mount Rail like Lawn; remeasure ignoreRects | G7 | M |
| G13 | NotifyRail → Outliner → Playback; UnresolvedCount clickable; wire `centre` | G6, G12 | M |
| G14 | Honest Playwright (no emitSelect); retarget suites green | G12–G13, G5 | M |
| G15 | Unit coverage sweep + CPG-D CV (incl. greyscale crest) | G14 | M |

Detailed acceptance criteria: [world-map-runtime-gaps-todo.md](world-map-runtime-gaps-todo.md).

---

## 7. Per-checkpoint e2e / CV checklist

| CP | After | Playwright | Screenshot + CV |
|---|---|---|---|
| **CPG-A** | G0–G4 | Host/canvas present; optional mid-lane fixture assert if cheap | Soil canvas; no SVG cards as map |
| **CPG-B** | G5–G7 | Drag then release does not select; click dock/lens no select; Fit± | Left dock; bottom-left cluster |
| **CPG-C** | G8–G11 | Lens still switches drawing; no React Range/Supply on stage; `camera.ts` gone | Greyscale: yours≠enemy without hue (crest/stroke/hatch) |
| **CPG-D** | G12–G15 | Rail visible; notify+outliner; mouse-only select; world-stage + checkpoint-f green | Full HUD corners; one WebGL map |

---

## 8. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Honest pick flakes (was cheated via emitSelect) | High | Fix Phaser input (G5/G7); never restore probe emit |
| Double-subtract rail 92px after G12 | Med | Canvas-space measure; unit/e2e for homeworld pick |
| Delete WorldScene before paint-ops | High | G11 blocked on G8–G10 green |
| Closing D18 via lens marks instead of D11 | High | Spec forbids; G10 acceptance asserts ownership lens empty |
| `WorldStage.tsx` merge conflicts across agents | Med | One agent owns WorldStage for G6/G12/G13 |
| Mid-lane only myLegions | Med | G3 passes **all** adapted entities |

---

## 9. Standing rules (every task)

1. Gaps spec + parent HOW win over this plan’s shorthand on contracts.
2. Git hands-off — no commits/pushes; hand the owner a message draft.
3. Magnitudes stay `long` via `world-numbers`; no new `P(Θ)`.
4. Mock Phaser at the Game boundary in unit tests.
5. Verification: `npm test` + `npm run build` (no lint) + Playwright for phase CPs.
6. Phase checkpoints: Playwright + PNG artifacts + agent `Read` CV — **no owner eyeball**.

---

## 10. Open questions

None that block Plan. If owner cuts D22–D25 (chrome), drop G12–G13 and keep G0–G11 + G14–G15
(map-plane still required).
