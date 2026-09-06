# Phaser-kernel freeze (lock 5a) — 2026-09-06

**Program:** `phaser-kernel`  
**Checkpoint:** CPF landed same session as Wave 1b + Wave 2+ implement.

## Frozen for base-defense unpause

Siege/battle may import without opening `LawnWorldScene`:

- Island: `destroyGame`, `usePhaserIslandHost` (`@/game-host/`), `createStageBus`, `siegeBus*` / `battleBus*`
- Board: `makeGridSpec`, `pickCell`, `createBoardLayers`, `createTerrainCache`, `bindCamera`, `wireKeyboardNav`, `visualFor`, `EntityRegistry`
- Contract: `BoardActorRecord` (`hp: bigint`), `SyncSeq`, `BoardSelectPayload`, `applyLiveBoard`, `applyPlaybackFrame`, `paintStructureHpBar`, `createSiegeGame`

## Explicitly not done here

- `SiegeBoardScene` / battle scene — **base-defense**
- Decision 40 discharge — needs live siege **and** battle callers
- Delve island — `party-dungeon`

## Proof

- Unit: destroy/bus/host/lifecycle/board/importGuard/Boot/focus/paint suites green
- E2E: `e2e/phaser-kernel-islands.spec.ts` passed; screenshots under `web/fusion-rpg-web/tmp/phaser-kernel-*.png`
- Obs: `window.__fusionRpgKernelObs` ring via `emitKernelObs`
