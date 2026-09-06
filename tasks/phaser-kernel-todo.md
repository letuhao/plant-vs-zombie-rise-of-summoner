# Tasks: phaser-kernel

Plan: [phaser-kernel-plan.md](phaser-kernel-plan.md) · Map:
[../docs/architecture/phaser-kernel-map.md](../docs/architecture/phaser-kernel-map.md) · Specs:
[../docs/architecture/phaser-kernel/](../docs/architecture/phaser-kernel/).

**Scope:** **S** ≈ under an hour · **M** ≈ one focused session · **L** = split further (avoid).  
**Paths:** `tasks/phaser-kernel-plan.md` · `tasks/phaser-kernel-todo.md` only.

> ## Binding rules
>
> 1. Dual-track: **add → switch-and-delete same commit**; never two live bodies / two Games.  
> 2. No slice waits on a person for an answerable default (Gates vs checkpoints).  
> 3. Acceptance is a command, grep, or test — not owner eyeball.  
> 4. Do not write `SiegeBoardScene` or unpause base-defense todos from these tasks.  
> 5. Do not discharge Decision 40. Do not delete `ensureGrid` before T8.

---

## Phase 0 — `lock-doc-sync`

- [x] **T0: Patch stale SSOTs** · **S** · Spec: `spec-lock-doc-sync.md`
  - Patch `docs/architecture/decisions.md` Lawn projector row (no longer “Implementation deferred”).
  - Rewrite `docs/architecture/base-defense/spec-board-render.md` opening: lawn **and** world islands;
    drop “exactly one Phaser integration … lawn-shaped.”
  - Extend `docs/architecture/fe-game-foundation.md` §8 tree: `createGame`, `world/`, `board/`,
    `camera/`, `game-host/`.
  - Fix occupant budget prose that still claims 50/80 as current → **96**
    (`PHASER_OCCUPANT_BUDGET` in `pickPhaserOccupants.ts`); DPLP sprites row if it still says ≤50/≤80
    as if it were that constant.
  - **Acceptance:**
    - [x] `rg "exactly one Phaser|lawn-shaped" docs/architecture/base-defense/spec-board-render.md` → no stale claim
    - [x] Lawn projector row in `decisions.md` does not say Implementation deferred for FE Phaser lawn
    - [x] DPLP §8 mentions `createGame` and `game-host/` (or equivalent accurate tree)
  - **Verify:** `rg` checklist above; no code change required.
  - **Files:** `decisions.md`, `spec-board-render.md`, `fe-game-foundation.md`, `lawn-projector.md`,
    `phaser-kernel-ideal.md` (header + gate status).
  - **Deps:** None.

### Checkpoint CP0 — after T0

- [x] Stale claims gone (`rg` recorded).
- [ ] Proceed to Wave 1 code (no owner gate).

---

## Phase 1a — `destroy-game` ∥ `stage-bus`

- [x] **T1a: Add `destroyGame` + mutex (zero production importers)** · **M** · Spec: `spec-destroy-game.md`
  - Add `web/fusion-rpg-web/src/game/destroyGame.ts` + `destroyGame.test.ts`.
  - API: `destroyGame({ game, sceneKey, shutdown, emitDestroyed, generation })` → Promise; order
    tweens → shutdown → emit → `destroy(true)` `noReturn` false; page mutex until DESTROY.
  - **Acceptance:**
    - [x] Mutex refuses overlapping create while pending (unit test)
    - [x] Order spy covers the sequence
    - [x] Landed then immediately switched (T1b/c) — dual-track same session
  - **Verify:** `npm test -- src/game/destroyGame.test.ts`
  - **Deps:** T0.

- [x] **T1b: Switch-and-delete lawn destroy** · **S** · Spec: `spec-destroy-game.md`
  - `destroyLawnGame` becomes thin caller of `destroyGame`; old body deleted **same commit**.
  - **Acceptance:**
    - [x] No duplicate destroy checklist body in `createLawnGame.ts`
    - [x] Existing lawn destroy/GG-11 tests still pass
  - **Verify:** `npm test -- src/game/createLawnGame src/stages/lawn/LawnStage.test.tsx`
  - **Deps:** T1a.

- [x] **T1c: Switch-and-delete world destroy** · **S** · Spec: `spec-destroy-game.md`
  - Same for world destroy helper / `createWorldGame.ts`.
  - **Acceptance:**
    - [x] World destroy goes through `destroyGame`; old body deleted same commit
    - [x] World host/stage tests pass
  - **Verify:** `npm test -- src/game/createWorldGame src/stages/world/host/WorldGameHost.test.tsx`
  - **Deps:** T1a (may parallel T1b after T1a).

- [x] **T2a: Extract `createStageBus` + keep lawn/world wrappers** · **M** · Spec: `spec-stage-bus.md`
  - Factory `{ on, emit, clearAll }` + shared `allocGameGeneration`; rewire lawn/world maps; delete
    duplicated private map machinery same commits.
  - **Acceptance:**
    - [x] Existing EventBus/lawn/world bus tests green
    - [x] Single factory path for maps
  - **Verify:** `npm test -- src/game/EventBus src/game/stageBus`
  - **Deps:** T0 (∥ T1*).

- [x] **T2b: Freeze `siegeBus*` / `battleBus*` names (stubs OK)** · **S** · Spec: `spec-stage-bus.md`
  - Export named siege/battle bus symbols or factories (stub emit OK).
  - **Acceptance:**
    - [x] `import { siegeBusEmit }` (or equivalent) resolves from `@/game/…`
    - [x] Required stems documented; payloads carry `generation`
  - **Verify:** unit/import test; `tsc --noEmit` clean for new exports
  - **Deps:** T2a.

### Checkpoint CP1 — after T1* + T2*

- [x] `npm test` for destroy + bus paths green.
- [x] Dual-track: no leftover twin destroy bodies.
- [x] Review before host hook (checkpoint, not a stall gate).

---

## Phase 1b — `island-host`

- [x] **T3a: Add `usePhaserIslandHost` under `src/game-host/`** · **M** · Spec: `spec-island-host.md`
  - **Acceptance:**
    - [x] Hook tests: StrictMode / deferred DESTROY
    - [x] `game/` still has zero React imports
  - **Verify:** `npm test -- src/game-host/usePhaserIslandHost.test.ts`

- [x] **T3b: Switch-and-delete `LawnGameHost` onto the hook** · **M** · Spec: `spec-island-host.md`
  - **Acceptance:**
    - [x] GG-11 lawn tests still mock facade / `createLawnGame` names
    - [x] LawnStage GG-11 panel test green

- [x] **T4: Switch-and-delete `WorldGameHost` onto the hook** · **M** · Spec: `spec-island-host.md`
  - **Acceptance:**
    - [x] World host tests green; GG-11 world panel survival holds

### Checkpoint CP2 — after T3–T4

- [x] Both facades use hook; mount skeletons deleted.
- [x] `npm run build` + host tests green.

---

## Phase 1c — `host-data-lifecycle`

- [x] **T5: Production host lifecycle suite (cutover gate)** · **M** · Spec: `spec-host-data-lifecycle.md`
  - **Acceptance:**
    - [x] Assertions 1–4 from spec have tests that fail if broken
    - [x] Both lawn and world paths covered
  - **Verify:** `npm test -- src/game-host/hostDataLifecycle.test.tsx`

### Checkpoint CP3 — after T5

- [x] Cutover suite green.
- [x] Proceed to board-contract / lawn-plane.

---

## Phase 1d — `board-contract`

- [x] **T6a: Ship freeze types + thin helpers (full unpause list)** · **M** · Spec: `spec-board-contract.md`
  - **Acceptance:**
    - [x] Ideal Unpause bar imports resolve without opening `LawnWorldScene.ts`
    - [x] Guard ready for banned imports (`doNotImport.test.ts`)
    - [x] `hp` is `bigint` — never float
    - [x] No `SiegeBoardScene`

- [x] **T6b: Rewrite `noClientPrediction` tripwire (confirmed vs extrapolation)** · **S** · Spec: `spec-board-contract.md`
  - **Acceptance:**
    - [x] Tripwire tests encode both cases
    - [x] Camera/`board/` scope covered

---

## Phase 1e — `lawn-plane`

- [x] **T7: Lawn importGuard + generation/init hygiene** · **M** · Spec: `spec-lawn-plane.md`
  - **Acceptance:**
    - [x] Lawn importGuard test fails on planted `@/lib/bus` under `game/`
    - [x] World gen in `init` (restart-safe)
    - [x] Icon epoch / apiBase mirrored from React (no `@/lib/bus` in Phaser)
    - [x] T5 still green
  - Note: `ensureGrid` deleted in T8 flip (same program session) after Graphics-compatible BoardLayers paint.

### Checkpoint CPF — Freeze (lock 5a)

- [x] T5 + T6a + T7 done; Unpause import list frozen.
- [x] Record: [docs/research/phaser-kernel-freeze-2026-09-06.md](../docs/research/phaser-kernel-freeze-2026-09-06.md)
- [x] **Follow-up (non-blocking):** base-defense may resume 21.3+ / 22.x against frozen names.
- [x] Wave 2+ started same session.

---

## Phase 2+ — modules

- [x] **T8: `lawn-paint` — Graphics-compatible flip** · Spec: `spec-lawn-paint.md`
  - `paintLawnTerrainGraphics` + `BoardLayers`; `ensureGrid` deleted same flip.
  - Decision 40 still undischarged.
  - **Verify:** unit + E2E lawn projector screenshots.

- [x] **T9: `focus-input` — focus-gate** · Spec: `spec-focus-input.md`
  - First pass only set `setLawnKeyboardMuted` from React; Phaser never read it (`wireKeyboardNav` had zero lawn callers) — **hollow**.
  - **Audit fix 2026-09-06:** `wireKeyboardNav({ isEnabled })` in `LawnWorldScene`; mute when panel open; tests in `keyboardNav` + `LawnStage`.

- [x] **T10: `cell-boot` — `Boot({ nextScene })`** · Spec: `spec-cell-boot.md`
  - BootScene accepts `nextScene` / `bootNextScene`; world stays Boot-less.
  - **Audit fix 2026-09-06:** `createLawnGame` sets `registry.bootNextScene = "LawnWorldScene"`.

- [x] **T11: `fx-facade` — wire `FxPool`** · Spec: `spec-fx-facade.md`
  - First pass wired `acquireRing` but module-level `selectRings` Map survived destroy (`drain` frees pool only) — **hollow leak**.
  - **Audit fix 2026-09-06:** `clearStatusFxRings` before `fx.drain()` on shutdown; FxPool + StatusFx unit tests.

---

## Phase R — `retire-clones`

- [x] **T12: Delete leftover destroy/host clone bodies** · Spec: `spec-retire-clones.md`
  - Facades thin; destroy via `destroyGame`; host via `usePhaserIslandHost`; `createGame.ts` unmoved.
  - **Verify:** `rg` + host unit slice green.

---

## E2E / visual proof (goal gate)

- [x] Playwright `e2e/phaser-kernel-islands.spec.ts` executed green.
- [x] Screenshots desktop/tablet/mobile + panel + world inspected (`tmp/phaser-kernel-*.png`).
- [x] Observability: `window.__fusionRpgKernelObs` + `emitKernelObs` exercised in E2E.
- [x] Testids: `lawn-game-host`, `world-game-host`, `lawn-stage-panel`, etc.

---

## Out of this todo (explicit)

| Item | Owner |
|---|---|
| Unpause siege 21.3+ / battle 22.x | `base-defense` after CPF |
| Decision 40 two-caller proof | siege + battle ship |
| Delve island consumer | `party-dungeon` |
| Memory probe | research later |
