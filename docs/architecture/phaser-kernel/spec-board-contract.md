# Spec: `board-contract`

**Module id:** `board-contract` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Specify complete 2026-09-06 — Wave 1b; **lock 5a freeze artifact**.  
**Depends on:** `stage-bus` (bus names); existing `src/game/board/` · **Blocks:** `lawn-plane`; base-defense unpause

**Ideal:** [phaser-kernel-ideal.md](../phaser-kernel-ideal.md) §Named unpause surface · Decision 40 honesty.  
**Anti-pattern:** do not repeat [spec-board-render.md](../base-defense/spec-board-render.md) “byte-identical”
or “exactly one Phaser integration” gates.

---

## Objective

Freeze the **import list** siege/battle may use so 21.3 can write
`import { … } from "@/game/…"` without opening `LawnWorldScene.ts`. Types and thin helpers only —
**no** `SiegeBoardScene`, no Decision 40 discharge, no lawn paint flip (lock **4c**).

**Success:** the Unpause bar one-liner from the ideal is true: siege can import `destroyGame`,
`usePhaserIslandHost`, `siegeBus*`, `BoardActorRecord`, `createBoardLayers`, and a live-board apply
helper by **name**.

---

## What already exists (verified)

**Built (grid — on disk, mostly test-only consumers):**

| Symbol | Path |
|---|---|
| `makeGridSpec` / `GridSpec` | `src/game/board/GridSpec.ts` |
| `pickCell` | `src/game/board/pickCell.ts` |
| `createBoardLayers` | `src/game/board/BoardLayers.ts` |
| `createTerrainCache` / terrain helpers | `src/game/board/terrainCache.ts` |
| `wireKeyboardNav` | `src/game/board/keyboardNav.ts` |
| `visualFor` | `src/game/board/visualMapping.ts` |
| `bindCamera` | `src/game/camera/bindCamera.ts` |
| `EntityRegistry` | `src/game/entities/EntityRegistry.ts` |
| `noClientPrediction` tripwire | `src/game/board/noClientPrediction.test.ts` (+ camera scope per ideal) |

**Gap:** no `BoardActorRecord`, `applyLiveBoard`, `applyPlaybackFrame`, structure HP helper,
`createSiegeGame` thin wrapper, documented do-not-import list, tripwire not yet split
confirmed-lerp vs extrapolation.

---

## Contract — frozen import list (lock 5a)

### A. Grid (re-export or document as canonical imports)

Siege/battle **may** import:

- `makeGridSpec`, `GridSpec`, `pickCell`
- `createBoardLayers`, `createTerrainCache` (or current terrain sink factory name)
- `bindCamera`, `wireKeyboardNav`, `visualFor`
- `EntityRegistry<TKey, TRecord>`

### B. Island (shipped by sibling modules — list for freeze completeness)

- `destroyGame` (`destroy-game`)
- `usePhaserIslandHost` (`island-host`) — React host path, not from `game/`
- `createStageBus` / `siegeBus*` / `battleBus*` (`stage-bus`)

### C. Siege / battle types and thin helpers (this module ships)

```ts
/** Sync grammar on model payloads */
type SyncSeq =
  | { kind: "revision"; value: number }
  | { kind: "modelSeq"; value: number }
  | { kind: "playbackCursor"; value: number };

type BoardActorRecord = {
  key: string;
  row: number;
  col: number;
  kind: string;
  hp: bigint; // magnitude — long/bigint on FE contract; never float
  isStructure: boolean;
  showInitiative: boolean;
};

type BoardSelectPayload = {
  generation: number;
  kind: "cell" | "actor" | "structure";
  actorKey?: string;
  row?: number;
  col?: number;
  // NEVER ptr
};

/** Live cell board apply — NOT SyncFromModelSystem / LawnViewModel */
declare function applyLiveBoard(
  view: /* BoardLayers + registry */ unknown,
  actors: readonly BoardActorRecord[],
  seq: SyncSeq
): void;

/** Battle playback — BoardView produced outside kernel (stages/battle/) */
declare function applyPlaybackFrame(view: unknown /* BoardView */, cursor: number): void;

/** Thin facade — createGame({ scenes }) only */
declare function createSiegeGame(opts: {
  parent: HTMLElement;
  generation: number;
  scenes: Phaser.Types.Scenes.SceneType[];
}): Phaser.Game;

/** Structure HP chrome ≠ ActorHudDisplay (identity/shield/status) */
declare function paintStructureHpBar(/* … */): void;
```

**Overlays:** painters draw into `layers.overlays` only (container from `createBoardLayers`).

**Suggested paths:**

```text
src/game/board/BoardActorRecord.ts
src/game/board/syncSeq.ts
src/game/board/applyLiveBoard.ts      # thin / stub OK until lawn-plane wires
src/game/board/applyPlaybackFrame.ts  # signature + guard tests
src/game/board/structureHpBar.ts      # thin helper
src/game/createSiegeGame.ts           # one-liner over createGame
src/game/board/doNotImport.md or *.test.ts listing banned lawn imports
```

### D. Do not import (siege / battle)

- `SyncFromModelSystem`
- `PickSystem` (lawn)
- `ActorHudDisplay`
- `PtrEntityRegistry` as the siege record shape
- `LawnWorldScene` / lawn `ensureGrid` as the siege paint path

Guard: test or documented scan that fails if `stages/siege` (when it appears) imports the banned set.

### E. Prediction tripwire (SC in this module — no separate map id)

Before any **confirmed** position lerp lands:

1. Rewrite `noClientPrediction.test.ts` (and camera scope) to distinguish:
   - **Allowed:** interpolate between two **server-confirmed** positions
   - **Forbidden:** extrapolate past last confirmed state
2. Fixtures for both; do not silently delete the tripwire.

Until that rewrite, **no lerp** in `board/` or `camera/`.

---

## Dual-track

Types/helpers are **new**. Do not leave a second apply path in production. When `applyLiveBoard`
gains a real body, lawn wiring is `lawn-plane` / `lawn-paint` — this module freezes the **name**.

---

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- src/game/board/
npm test -- src/game/board/noClientPrediction.test.ts
```

---

## Testing strategy

- Type/helper unit tests (record shape, sync seq narrowing, select payload rejects ptr fields).
- `createSiegeGame` builds config via `buildGameConfig` (pure) without constructing Game in jsdom if
  matching repo discipline.
- Do-not-import scan test (string/AST) for banned modules from a fixture siege path.
- Tripwire tests updated before any lerp PR.

**Never:** Vitest as paint golden; “byte-identical lawn” as this module’s SC.

---

## Tunables / numeric

| Field | Type rule |
|---|---|
| `hp` on `BoardActorRecord` | `bigint` (or documented long-equivalent) — never `float` magnitude |
| Cell pixels / depths | Visual-design structural consts in board modules |

No `data/tuning/phaser-kernel.v*.json` in this module.

---

## Boundaries

- **Always:** freeze full list above; tripwire before lerp; no `SiegeBoardScene`.
- **Ask first:** changing `BoardActorRecord` fields after freeze (breaks base-defense).
- **Never:** discharge Decision 40; import lawn Sync/Pick/Hud into siege; claim paint flipped.

---

## Success criteria

1. Documented + importable list includes every A–C symbol (stubs allowed for apply bodies).
2. Do-not-import guard exists and fails on banned imports.
3. `applyPlaybackFrame` / `applyLiveBoard` / `BoardActorRecord` / `createSiegeGame` / structure HP
   helper are named under `src/game/`.
4. Tripwire rewrite SC recorded; no lerp merges without it.
5. Ideal Unpause bar sentence is true without opening `LawnWorldScene.ts`.
6. Does **not** claim Decision 40 discharged; does **not** delete `ensureGrid`.

---

## Out of module

Writing siege/battle scenes; lawn paint golden; FxService; host hook implementation (sibling).
