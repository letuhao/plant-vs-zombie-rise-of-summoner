import Phaser from "phaser";
import type { PtrEntityRegistry, PtrViewRecord } from "../entities/PtrEntityRegistry";
import { worldToCell } from "../gridMath";
import { lawnBusEmit, type LawnSelectPayload } from "../EventBus";

export { worldToCell };

/** Walk GameObject → parentContainer chain to find a registered lawn entity (HUD children included). */
export function findRegistryRecordForGameObject(
  registry: PtrEntityRegistry,
  go: Phaser.GameObjects.GameObject
): PtrViewRecord | undefined {
  let cur: Phaser.GameObjects.GameObject | null = go;
  while (cur) {
    for (const rec of registry.entries()) {
      if (rec.go === cur) return rec;
    }
    const next: Phaser.GameObjects.GameObject | null =
      ((cur as unknown as { parentContainer?: Phaser.GameObjects.GameObject | null }).parentContainer) ??
      null;
    cur = next;
  }
  return undefined;
}

/**
 * Wire pick handlers. Returns unsubscribe (RT-07 / pick leak fix).
 * Emits at most one lawn:select per pointer down.
 * T12: Band B HUD / child hits resolve to the parent occupant container.
 */
export function wirePickSystem(
  scene: Phaser.Scene,
  registry: PtrEntityRegistry,
  generation: number,
  getGrid: () => { rows: number; cols: number }
): () => void {
  let handledThisDown = false;

  const onGoDown = (
    pointer: Phaser.Input.Pointer,
    go: Phaser.GameObjects.GameObject
  ) => {
    if (handledThisDown) return;
    const rec = findRegistryRecordForGameObject(registry, go);
    if (!rec) return;
    handledThisDown = true;
    pointer.event?.stopPropagation?.();
    lawnBusEmit("lawn:select", {
      generation,
      kind: rec.side === "grid" ? "tile" : "occupant",
      ptr: rec.side === "grid" ? undefined : rec.ptr,
      row: rec.row,
      col: rec.col
    } satisfies LawnSelectPayload);
  };

  const onPointerDown = (pointer: Phaser.Input.Pointer) => {
    if (handledThisDown) {
      handledThisDown = false;
      return;
    }
    handledThisDown = false;
    const { rows, cols } = getGrid();
    const cell = worldToCell(pointer.worldX, pointer.worldY, rows, cols);
    if (!cell) return;

    // Prefer topmost occupant in cell (highest depth after stack layout)
    let hit: { ptr: string; row?: number; col?: number } | undefined;
    let bestDepth = -Infinity;
    for (const rec of registry.entries()) {
      if (rec.side !== "plant" && rec.side !== "zombie") continue;
      if (rec.row !== cell.row || rec.col !== cell.col) continue;
      const depth = typeof rec.go.depth === "number" ? rec.go.depth : 0;
      if (depth >= bestDepth) {
        bestDepth = depth;
        hit = { ptr: rec.ptr, row: rec.row, col: rec.col };
      }
    }
    if (hit) {
      lawnBusEmit("lawn:select", {
        generation,
        kind: "occupant",
        ptr: hit.ptr,
        row: hit.row,
        col: hit.col
      } satisfies LawnSelectPayload);
      return;
    }

    lawnBusEmit("lawn:select", {
      generation,
      kind: "tile",
      row: cell.row,
      col: cell.col
    } satisfies LawnSelectPayload);
  };

  const onPointerUp = () => {
    handledThisDown = false;
  };

  scene.input.on("gameobjectdown", onGoDown);
  scene.input.on("pointerdown", onPointerDown);
  scene.input.on("pointerup", onPointerUp);

  return () => {
    scene.input.off("gameobjectdown", onGoDown);
    scene.input.off("pointerdown", onPointerDown);
    scene.input.off("pointerup", onPointerUp);
  };
}
