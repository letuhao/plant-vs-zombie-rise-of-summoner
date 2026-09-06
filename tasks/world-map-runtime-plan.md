# World map runtime — implementation plan

**Program:** `world-map-runtime` · **One module**, three build slices (Host → Objects → Scene) + **R0** e2e harness.
**Map:** [docs/architecture/world-map-runtime-map.md](../docs/architecture/world-map-runtime-map.md)
**Spec:** [docs/architecture/world-map-runtime/spec-world-map-runtime.md](../docs/architecture/world-map-runtime/spec-world-map-runtime.md)
**Ideal:** [docs/architecture/world-map-runtime-ideal.md](../docs/architecture/world-map-runtime-ideal.md)
**Catalog:** [docs/design/11-world-stage.html](../docs/design/11-world-stage.html) §O
**Tasks:** [world-map-runtime-todo.md](world-map-runtime-todo.md)

**Status:** **not complete** — R0–R16 Phaser host shipped, but parent SC 1–12 are **not** closed.
Completion is [world-map-runtime-gaps-plan.md](world-map-runtime-gaps-plan.md) /
[world-map-runtime-gaps-todo.md](world-map-runtime-gaps-todo.md) (D1–D32). Do not treat CPA–CPD
as done until gaps CPG-D is green.

**Paths written:** `tasks/world-map-runtime-plan.md` · `tasks/world-map-runtime-todo.md`.
Never `tasks/plan.md` / `tasks/todo.md`.

---

## 1. What this plans

**Plans:** the full `world-map-runtime` module spec — e2e harness, bus, host lifetime, pin/lane/force
factories, camera (incl. edge-scroll), pick with chrome occlusion, one-camera overlays (range /
routes / blocked / supply / lifeline / six lenses), SVG retirement, import guard, and T3 HOW (R16).

**Does not plan:** turn engine (`world-map-program`), HUD/inspector/commands/playback field work
(`world-stage`), fog *rules*, wire `WorldStateDto.Revision`, moving channels to `src/lib/world-view/`
(out of v1), minimap, Phaser UIScene, art beyond D10 placeholders, owner eyeball playtests.

**Does not restate the spec.** Order, vertical slices, automated checkpoints, risks, and locked
decisions live here. Contracts stay in the spec.

---

## 2. Dependency graph

```text
R0 e2e harness (testids + world-map-runtime.spec stub)
        │
EventBus world:* ──┐
layout / zoomTier ─┼── createWorldGame + WorldMapScene (empty)
snapshotTheme ─────┘              │
                                  ▼
                         WorldGameHost + modelSeq
                                  │
                                  ▼
                    WorldStage mounts host (canvas alive)
                                  │
                           *** CPA (e2e+CV) ***
                                  │
              ┌───────────────────┼───────────────────┐
              ▼                   ▼                   ▼
         WorldRegistry      sectorPin /          laneStroke /
         syncWorldSystem    fog-on-pin           forceMarker
              │                   │                   │
              └───────────────────┴───────────────────┘
                                  │
                           *** CPB (e2e+CV) ***
                                  │
              ┌───────────────────┼───────────────────┐
              ▼                   ▼                   ▼
         Camera/LOD            Pick +              Overlay system
         edge-scroll           ignoreRects         (routes→lenses)
                                  │
                    *** CPC then CPD (e2e+CV) ***
                                  │
                    Retire SVG WorldScene + camera.ts
                                  │
                         T3 HOW doc (R16, parallel OK)
```

**Build order:** R0 → Host → Objects → Scene. Overlay drawing is Scene, not a fourth product.

---

## 3. Vertical slicing

| Phase | Vertical outcome (CI-provable) |
|---|---|
| **R0** | `#/world` e2e stub + host testids + artifact dir convention |
| **A Host** | Enter World → Phaser soil canvas; leave destroys Game; inspector open → mount count 1 |
| **B Objects** | first-light sectors/lanes/forces as §O pins; unknown = diamond; intel-first |
| **C Camera + pick** | Drag / wheel / edge-scroll / arrows / Fit; click pin → left inspector; dock ignored |
| **D Overlays + retire SVG** | Range/routes/supply/lifeline/lenses/blocked on Phaser only; SVG camera gone |
| **E Docs** | T3 HOW sentence amended (R16; parallel with B–D) |

---

## 4. Gates vs checkpoints

**No human visual gate.** Owner git commit remains human-only (AGENTS.md). Phase checkpoints are
automated.

### Checkpoint convention (CPA–CPD)

1. `cd web/fusion-rpg-web && npm test && npm run build` (no lint script)
2. `npx playwright test e2e/world-map-runtime.spec.ts` (filter by describe / tag for the phase)
3. Specs write PNGs to `web/fusion-rpg-web/e2e/.artifacts/world-map-runtime/`
4. **Agent CV:** `Read` each required PNG; assert the written checklist. Fail the checkpoint if CV
   fails — do not ask the owner to open images.
5. Functional Playwright asserts are primary for interaction; screenshots cover Phaser appearance.

| Kind | What | Resolver |
|---|---|---|
| CPA–CPD | Automated e2e + CV after phase work | Agent (Playwright + Read PNGs) |
| R16 | T3 HOW amend | In-program, authorized; parallel with B–D |
| Locked visual | §O + fog-on-pin | Spec tables (not reopenable without a new decision) |
| Out of v1 | Channels → `src/lib/world-view/` | Later program |

---

## 5. Task list (index)

Full acceptance criteria in [world-map-runtime-todo.md](world-map-runtime-todo.md).

### R0 — E2E harness
- [ ] **R0** — `data-testid` on host/canvas wrapper; `e2e/world-map-runtime.spec.ts` stub (first-light mock); artifact dir documented

### Phase A — Host island
- [ ] **R1** — `world:*` EventBus beside lawn
- [ ] **R2** — `layout.ts` + `zoomTier.ts` + `snapshotTheme.ts`
- [ ] **R3** — `createWorldGame` / destroy + empty `WorldMapScene`
- [ ] **R4** — `WorldGameHost` + `WorldStage` mounts host; `modelSeq`

### Checkpoint A (CPA)
- [ ] Unit + build green; Playwright CPA describe green; agent CV on CPA PNGs

### Phase B — Graph objects
- [ ] **R5** — `WorldRegistry` + `syncWorldSystem`
- [ ] **R6** — `sectorPin` + fog-on-pin
- [ ] **R7** — `laneStroke` + `forceMarker`
- [ ] **R8** — Graph on Phaser; SectorNode off stage

### Checkpoint B (CPB)
- [ ] Unit + build; Playwright CPB; agent CV (discs, diamond, centres, no card grid)

### Phase C — Camera + pick
- [ ] **R9** — Camera system (drag, wheel, edge-scroll, Fit, LOD)
- [ ] **R10** — React → `world:camera` (arrows, Fit); `W` cycles
- [ ] **R11** — Pick + ignoreRects + right-click empty

### Checkpoint C (CPC)
- [ ] Unit + build; Playwright CPC; agent CV (pan/zoom frames differ; left dock)

### Phase D — Overlays + SVG retirement
- [ ] **R12** — Selection halo + range rings on Phaser
- [ ] **R13** — Queued routes + blocked marks
- [ ] **R14** — Supply + lifeline + six lenses
- [ ] **R15** — Delete SVG camera path + import guard

### Checkpoint D (CPD)
- [ ] Spec SC 1–11; Playwright CPD + greyscale CV shot; one canvas map

### Phase E — Doc follow-up (parallel with B–D)
- [ ] **R16** — Amend T3 HOW in `tech-stack.md` (+ `decisions.md` if needed)

### Checkpoint: Complete
- [ ] R0–R16 done; CPA–CPD green

---

## 6. Per-checkpoint e2e / CV checklist

| CP | After | Playwright asserts | Screenshot + CV looks for |
|---|---|---|---|
| **A** | R0–R4 | `#/world` loads; host/canvas testid present; leave/remount; inspector open → single host | Soil-toned map plane; React HUD corners; no full-page SVG sector cards as the map |
| **B** | R5–R8 | Fixture graph; pin/lane presence | Disc pins; diamond unknown if fixture has Unknown; lanes at centres; no 192px card grid |
| **C** | R9–R11 | Wheel/drag/edge-scroll / Fit; arrow pan when map owns input; click pin → left dock; click over dock no select; right-click → empty | Before/after pan or zoom frames differ; inspector dock on **left** beside rail |
| **D** | R12–R15 | Lens keys change drawing; range/route under targeting; no React Range/Supply/Lifeline on stage; `camera.ts` unreachable | One WebGL/canvas map; overlay strokes; greyscale filter shot for GG-27 |

---

## 7. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Dual camera (React SVG over Phaser) | High | R12–R14 before R15; CPD asserts one canvas |
| Fake `revision` on adapted state | High | Host `modelSeq` only |
| Import guard too strict | Med | Channels from `stages/world/render` allowed |
| GG-11 remount | High | R4 + CPA mount probe |
| Phaser in entry chunk | Med | Only `WorldGameHost` imports `createWorldGame` |
| Phaser screenshot flake | Med | Assert + artifact + CV checklist; no `toHaveScreenshot` goldens |
| Spec said `npm run lint` | Low | SC uses test + build only |

---

## 8. Locked decisions (not open)

1. §O: circle pin + ownership ring; unknown diamond; LOD strict supersets.
2. Fog-on-pin density table as in the spec (forces strip never on pin).
3. T3 HOW amended by **R16** (authorized).
4. Channels stay under `stages/world/render/` for v1.

---

## 9. Standing rules (every task)

1. Spec + map win over this plan’s shorthand when they disagree on a contract detail.
2. Git hands-off — no commits/pushes from agents; hand the owner a message draft.
3. Magnitudes stay `long` via existing `world-numbers`; no new `P(Θ)`.
4. Mock Phaser at the Game boundary in unit tests (copy `createGame.test.ts`).
5. `#/world` stays a stage under `StageHost` after every task; fixture fallback remains.
6. Verification: `npm test` + `npm run build` (no lint script).
7. Phase checkpoints: Playwright + PNG artifacts + agent `Read` CV — **no owner eyeball**.
