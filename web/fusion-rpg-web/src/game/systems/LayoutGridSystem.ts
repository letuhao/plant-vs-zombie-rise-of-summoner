import type { LawnViewMode } from "@/features/lawn/lawnViewMode";
import type { LawnViewModel } from "@/features/lawn/lawnViewModel";
import type { PtrEntityRegistry, PtrViewRecord } from "../entities/PtrEntityRegistry";
import { cellToWorld } from "../gridMath";
import { stackOffset, type StackSide } from "../stackLayout";

const ORPHAN_Y = 24;

function cellSideKey(row: number, col: number, side: string): string {
  return `${row},${col}:${side}`;
}

/** Layout living sprites onto the grid; orphans along a top strip. */
export function layoutGrid(
  registry: PtrEntityRegistry,
  _model: LawnViewModel,
  viewMode: LawnViewMode = "split"
): void {
  const stack = viewMode === "stack";
  const buckets = new Map<string, PtrViewRecord[]>();
  const rest: PtrViewRecord[] = [];

  for (const rec of registry.entries()) {
    const onCell = rec.row != null && rec.col != null && rec.col >= 0;
    if (onCell && (rec.side === "plant" || rec.side === "zombie")) {
      const key = cellSideKey(rec.row!, rec.col!, rec.side);
      const list = buckets.get(key);
      if (list) list.push(rec);
      else buckets.set(key, [rec]);
    } else {
      rest.push(rec);
    }
  }

  for (const recs of buckets.values()) {
    const side = recs[0]!.side as StackSide;
    const n = recs.length;
    recs.forEach((rec, i) => {
      const { x, y } = cellToWorld(rec.row!, rec.col!);
      if (stack) {
        const o = stackOffset(side, i, n);
        rec.go.setPosition(x + o.dx, y + o.dy);
        rec.go.setDepth(o.depth);
        rec.go.setScale(0.72);
      } else {
        rec.go.setPosition(x, y);
        rec.go.setDepth(5 + i);
        rec.go.setScale(1);
      }
    });
  }

  let orphanIndex = 0;
  for (const rec of rest) {
    if (rec.row != null && rec.col != null && rec.col >= 0) {
      const { x, y } = cellToWorld(rec.row, rec.col);
      if (rec.side === "grid") {
        rec.go.setPosition(x - 22, y + 18);
        rec.go.setDepth(1);
      } else if (rec.side === "mower") {
        rec.go.setPosition(x - 36, y);
        rec.go.setDepth(2);
      } else if (rec.side === "pet") {
        rec.go.setPosition(x + 22, y - 16);
        rec.go.setDepth(3);
      } else {
        rec.go.setPosition(x, y);
        rec.go.setDepth(5);
      }
      rec.go.setScale(1);
    } else {
      const x = 40 + orphanIndex * 56;
      rec.go.setPosition(x, ORPHAN_Y);
      rec.go.setDepth(4);
      rec.go.setScale(stack ? 0.72 : 1);
      orphanIndex++;
    }
  }
}
