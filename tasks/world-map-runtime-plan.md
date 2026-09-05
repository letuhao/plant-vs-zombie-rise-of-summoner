# World map runtime — implementation plan

**Program:** `world-map-runtime` · **One module**, three build slices (Host → Objects → Scene).
**Map:** [docs/architecture/world-map-runtime-map.md](../docs/architecture/world-map-runtime-map.md)
**Spec:** [docs/architecture/world-map-runtime/spec-world-map-runtime.md](../docs/architecture/world-map-runtime/spec-world-map-runtime.md)
**Ideal:** [docs/architecture/world-map-runtime-ideal.md](../docs/architecture/world-map-runtime-ideal.md)
**Catalog:** [docs/design/11-world-stage.html](../docs/design/11-world-stage.html) §O
**Tasks:** [world-map-runtime-todo.md](world-map-runtime-todo.md)

**Status:** plan drafted 2026-09-06. Awaits owner review of this plan (and the strengthened spec it
implements). Approving this plan authorizes build against the **defaults already written in the
spec** (§O drawing calls, fog-on-pin density table). Overturns amend pin factories — they do not
rewind Host.

**Paths written:** `tasks/world-map-runtime-plan.md` · `tasks/world-map-runtime-todo.md`.
Never `tasks/plan.md` / `tasks/todo.md`.

---

## 1. What this plans

**Plans:** the full `world-map-runtime` module spec — bus, host lifetime, pin/lane/force factories,
camera (incl. edge-scroll), pick with chrome occlusion, one-camera overlays (range / routes /
blocked / supply / lifeline / six lenses), SVG retirement, import guard, and the non-blocking T3
HOW doc follow-up.

**Does not plan:** turn engine (`world-map-program`), HUD/inspector/commands/playback field work
(`world-stage`), fog *rules*, wire `WorldStateDto.Revision`, moving channels to `src/lib/world-view/`
(later slice), minimap, Phaser UIScene, art beyond D10 placeholders.

**Does not restate the spec.** Order, vertical slices, checkpoints, risks, and defaults for open
questions live here. Contracts stay in the spec.

---

## 2. Dependency graph

```text
EventBus world:* ──┐
layout / zoomTier ─┼── createWorldGame + WorldMapScene (empty)
snapshotTheme ─────┘              │
                                  ▼
                         WorldGameHost + modelSeq
                                  │
                                  ▼
                    WorldStage mounts host (canvas alive)
                                  │
              ┌───────────────────┼───────────────────┐
              ▼                   ▼                   ▼
         WorldRegistry      sectorPin /          laneStroke /
         syncWorldSystem    fog-on-pin           forceMarker
              │                   │                   │
              └───────────────────┴───────────────────┘
                                  │
                                  ▼
                         Graph visible on Phaser
                                  │
              ┌───────────────────┼───────────────────┐
              ▼                   ▼                   ▼
         Camera/LOD            Pick +              Overlay system
         edge-scroll           ignoreRects         (routes→lenses)
                                  │
                                  ▼
                    Retire SVG WorldScene + camera.ts
                                  │
                                  ▼
                         T3 HOW doc (parallel, non-blocking)
```

**Build order matches the capability map:** Host → Objects → Scene. Overlay drawing is Scene, not a
fourth product.

---

## 3. Vertical slicing

| Phase | Vertical outcome (player-visible or CI-provable) |
|---|---|
| **A Host** | Enter World → Phaser soil canvas; leave destroys Game; open inspector → mount count stays 1 |
| **B Objects** | first-light (or live) sectors/lanes/forces draw as §O pins; unknown = diamond; intel-first |
| **C Camera + pick** | Drag / wheel / edge-scroll / arrows / Fit; click pin → inspector; right-click → empty; dock ignored |
| **D Overlays + retire SVG** | Range/routes/supply/lifeline/lenses/blocked on Phaser only; SVG composer + unused camera gone |
| **E Docs** | T3 HOW sentence amended (Ask-first; does not block A–D) |

Wrong (horizontal): "all systems stubs" then "all factories" then "wire once." Right: each phase
leaves `#/world` playable (fixture or live) with a clearer map plane than before.

---

## 4. Gates vs checkpoints

**No hard pre-work gate.** Spec/map still say "pending owner review" — **reviewing and approving
this plan is that decision.** Fog-on-pin and §O ship as the tables in the strengthened spec; if the
owner overturns them later, amend Objects tasks only.

| Kind | What | Resolver / default |
|---|---|---|
| Checkpoint A–D | Review work already done | Owner |
| Non-blocking follow-up | T3 HOW in `tech-stack.md` / `decisions.md` | Owner when ready; Phaser build does not wait |
| Reversible default | §O pin language + fog-on-pin density | Spec tables; overturn → patch factories |
| Out of v1 | Channels move to `src/lib/world-view/` | Later program slice |

Do **not** invent a gate that freezes Phase A on "wait for plate §O sign-off" — the map already
assumes those drawing calls.

---

## 5. Task list (index)

Full acceptance criteria and verification live in [world-map-runtime-todo.md](world-map-runtime-todo.md).

### Phase A — Host island
- [ ] **R1** — `world:*` EventBus beside lawn; foreign generation dropped; lawn union unchanged
- [ ] **R2** — `layout.ts` + `zoomTier.ts` (`FIT_MAX` / `DETAIL_MIN`) + `snapshotTheme.ts` (`--soil`, font)
- [ ] **R3** — `createWorldGame` / `destroyWorldGame` + empty `WorldMapScene` (no BootScene); Phaser mocked
- [ ] **R4** — `WorldGameHost` (buffer until ready, `modelSeq`, ResizeObserver → `world:resized`) mounts in `WorldStage`; SVG map pane replaced by host; HUD/inspector stay

### Checkpoint A
- [ ] Lifetime + GG-11 + bus green; Phaser only from World lazy chunk

### Phase B — Graph objects
- [ ] **R5** — `WorldRegistry` + `syncWorldSystem` (`modelSeq` monotonic; intel-first upsert/destroy)
- [ ] **R6** — `sectorPin` factory from channels + fog-on-pin density; descriptor tests; no opacity
- [ ] **R7** — `laneStroke` + `forceMarker` (centres; mid-lane `getPointAt` linear set-on-apply)
- [ ] **R8** — Host emits `world:model`; scene draws graph; React `SectorNode` / Fog-on-card **off the stage**

### Checkpoint B
- [ ] Pins/lanes/forces visible; unknown diamond; greyscale matrix still owned by channel tests

### Phase C — Camera + pick
- [ ] **R9** — Camera system: drag threshold, wheel-about-pointer, edge-scroll, Fit, clamps, LOD in place
- [ ] **R10** — React → `world:camera` (arrows when map owns input; Fit/+/−); `W` still cycles (GG-18)
- [ ] **R11** — Pick: 44px disc, `ignoreRects` (rail + left dock + HUD), right-click → `empty`, hover local-only

### Checkpoint C
- [ ] Gestures + select/deselect; pick does not fire through left inspector

### Phase D — Overlays + SVG retirement
- [ ] **R12** — `world:interaction` selection halo + range rings from `worldSelection` points; retire stage `RangeOverlay`
- [ ] **R13** — Overlay: queued routes + blocked marks
- [ ] **R14** — Overlay: supply + lifeline + six lens drawings via `world:lens`; retire stage Supply/Lifeline
- [ ] **R15** — Delete `camera.ts` / `cameraGestures.ts` / SVG `WorldScene` stage path; import guard; z-order locked

### Checkpoint D
- [ ] Spec success criteria 1–10 (verify uses `npm test` + `npm run build` — **no `npm run lint`**, none in package.json)

### Phase E — Doc follow-up (parallel with B–D)
- [ ] **R16** — Amend T3 HOW in `tech-stack.md` (and `decisions.md` if needed); Ask-first before edit

### Checkpoint: Complete
- [ ] All R1–R15 done; R16 done or explicitly deferred by owner; ready for playtest

---

## 6. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Dual camera (React SVG overlay over Phaser) | High | R12–R14 move drawing before R15 deletes SVG; never compose both |
| Fake `revision` on adapted state | High | Spec lock: host `modelSeq` only; R4/R5 assert no wire change |
| Import guard too strict (`game/` ↛ `stages/`) | Med | Spec import law: channels allowed; guard forbids `lib/bus` / React / `*Dto` |
| GG-11 remount when inspector opens | High | R4 copies LawnGameHost; mount-count test |
| Phaser in entry chunk | Med | Only `WorldGameHost` imports `createWorldGame`; World already `lazy()` |
| Fog strip crammed on 44px pin | Med | Fog-on-pin table; forces strip inspector-only |
| Task >5 files | Med | Todo splits factories / systems; if a task balloons, split before coding |
| Spec says `npm run lint` | Low | Todo verification matches world-stage finding: no lint script |

---

## 7. Open questions (tracked, not gates)

1. §O three drawing calls — **default: implement as plate/spec.** Overturn → amend R6.
2. Fog-on-pin density table — **default: implement as spec.** Overturn → amend R6.
3. T3 HOW — **R16**, non-blocking.
4. Channels → `src/lib/world-view/` — **out of v1.**

---

## 8. Standing rules (every task)

1. Spec + map win over this plan’s shorthand when they disagree on a contract detail.
2. Git hands-off — no commits/pushes from agents; hand the owner a message draft.
3. Magnitudes stay `long` via existing `world-numbers`; no new `P(Θ)`.
4. Mock Phaser at the Game boundary in unit tests (copy `createGame.test.ts`).
5. `#/world` stays a stage under `StageHost` after every task; fixture fallback remains until live data exists.
6. Verification: `cd web/fusion-rpg-web && npm test && npm run build` (no lint script).
