# Spec: `island-host`

**Module id:** `island-host` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Specify complete 2026-09-06 — Wave 1.  
**Depends on:** `destroy-game`, `stage-bus` · **Blocks:** `host-data-lifecycle`, `lawn-plane`

**Ideal:** [phaser-kernel-ideal.md](../phaser-kernel-ideal.md) lock **3a**, §Named unpause Island.  
**Hosts today:** [LawnGameHost.tsx](../../../web/fusion-rpg-web/src/features/lawn/LawnGameHost.tsx) ·
[WorldGameHost.tsx](../../../web/fusion-rpg-web/src/stages/world/host/WorldGameHost.tsx)

---

## Objective

One React hook skeleton for every Phaser island mount: generation, ResizeObserver, buffer-until-ready,
DESTROY wait via `destroy-game` mutex. Lawn and world stay **named facades** with island-specific props.

**Success:** `usePhaserIslandHost` lives under `src/game-host/`; facades call it; GG-11 tests still
mock **facade** names; `game/` has zero React imports.

---

## What already exists (verified)

**Built:** both hosts mount with `[]` effect, generation alloc, buffer until ready, resize emit,
destroy on unmount — but duplicated and incomplete DESTROY wait.

**Gap:** no shared hook; DESTROY race; prop surfaces diverge (lawn `viewMode` vs world lens/targeting/…).

---

## Contract

### Path (lock 3a)

```text
web/fusion-rpg-web/src/game-host/
  usePhaserIslandHost.ts
  usePhaserIslandHost.test.ts
```

`src/game/` remains Phaser-only (RT-09). Only lazy stage hosts import create facades (GG-38).

### Hook props (frozen — shared only)

```ts
type UsePhaserIslandHostArgs = {
  parentRef: RefObject<HTMLElement | null>;
  create: (opts: { parent: HTMLElement; generation: number }) => Phaser.Game;
  destroy: (game: Phaser.Game | null, generation: number) => void | Promise<void>;
  /** Called when stage bus :ready fires for this generation */
  onReady?: (generation: number) => void;
};
```

**Not on the hook:** `lens`, `targeting`, `ignoreRects`, `overlayEpoch`, `viewMode`, fingerprint —
those stay on `LawnGameHost` / `WorldGameHost` and emit via stage bus after ready.

### Destroy ownership

Facades’ `destroy` **must** call `destroyGame` (or thin wrapper). Hook awaits promise / mutex from
`destroy-game` before allowing a subsequent create in the same page session. **No second mutex.**

### Folder / import law

| May import Phaser / create*Game | Must not |
|---|---|
| Lazy lawn/world/siege stage hosts, `game-host/` | `StageHost`, app entry, `game/` importing React |

---

## Dual-track

1. Add `usePhaserIslandHost` + unit tests (mock create/destroy).
2. Switch `LawnGameHost` to the hook; delete duplicated mount skeleton **same commit**.
3. Switch `WorldGameHost` likewise.

GG-11 tests continue to mock `createLawnGame` / `createWorldGame` (or facade modules) — **not** a
generic host component name that stages do not use.

---

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- src/game-host/usePhaserIslandHost.test.ts
npm test -- src/features/lawn/LawnGameHost.test.tsx src/stages/lawn/LawnStage.test.tsx
npm test -- src/stages/world/host/WorldGameHost.test.tsx
```

---

## Testing strategy

- Mount once; StrictMode double-invoke does not leak two Games (generation / destroy called correctly).
- Foreign generation dropped at facade emit layer (existing host tests stay green).
- Panel open: Game identity stable (existing GG-11 stage tests).
- Unmount awaits destroy settle before effect cleanup completes (fake deferred DESTROY).

---

## Tunables

None beyond existing host buffer policy (owned by facade modules / later lifecycle suite).

---

## Boundaries

- **Always:** hook outside `game/`; facades keep island props; await destroy mutex.
- **Ask first:** moving LawnGameHost path from `features/lawn/` to `stages/lawn/`.
- **Never:** React inside `game/`; Phaser in entry chunk; second DESTROY mutex; Phaser `UIScene` for HUD.

---

## Success criteria

1. `usePhaserIslandHost` exists under `src/game-host/` with the prop surface above.
2. Lawn and World facades use it; duplicated mount/destroy skeleton deleted.
3. GG-11 lawn/world stage tests pass without renaming their mock targets away from facades.
4. `game/` import guard: zero React imports.
5. Bundle: Phaser still lazy via stage routes (GG-38) — `check:bundle` / existing split tests hold.

---

## Out of module

Lifecycle proof suite (`host-data-lifecycle`); board types; paint; siege scene.
