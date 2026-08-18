import Phaser from "phaser";
import type { PtrEntityRegistry } from "../entities/PtrEntityRegistry";
import { worldToCell } from "../gridMath";
import { lawnBusEmit, type LawnSelectPayload } from "../EventBus";

export { worldToCell };

/**
 * Wire pick handlers. Returns unsubscribe (RT-07 / pick leak fix).
 * Emits at most one lawn:select per pointer down.
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
    for (const rec of registry.entries()) {
      if (go === rec.go) {
        handledThisDown = true;
        pointer.event?.stopPropagation?.();
        lawnBusEmit("lawn:select", {
          generation,
          kind:
            rec.side === "grid"
              ? "tile"
              : rec.side === "mower" || rec.side === "pet"
                ? "occupant"
                : "occupant",
          ptr: rec.side === "grid" ? undefined : rec.ptr,
          row: rec.row,
          col: rec.col
        } satisfies LawnSelectPayload);
        return;
      }
    }
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
