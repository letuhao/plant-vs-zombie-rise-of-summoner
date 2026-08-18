/** Pure lawn grid math — no Phaser (unit-testable). */

export const CELL_W = 64;
export const CELL_H = 72;
export const ORIGIN_X = 48;
export const ORIGIN_Y = 56;

export function cellToWorld(
  row: number,
  col: number
): { x: number; y: number } {
  return {
    x: ORIGIN_X + col * CELL_W + CELL_W / 2,
    y: ORIGIN_Y + row * CELL_H + CELL_H / 2
  };
}

export function worldToCell(
  x: number,
  y: number,
  rows: number,
  cols: number
): { row: number; col: number } | null {
  const col = Math.floor((x - ORIGIN_X) / CELL_W);
  const row = Math.floor((y - ORIGIN_Y) / CELL_H);
  if (row < 0 || col < 0 || row >= rows || col >= cols) return null;
  return { row, col };
}

/** Zoom so the model grid fills the canvas (contain). */
export function lawnWorldSize(
  rows: number,
  cols: number
): { width: number; height: number } {
  return {
    width: ORIGIN_X + cols * CELL_W + 24,
    height: ORIGIN_Y + rows * CELL_H + 24
  };
}

export function lawnCameraZoom(
  viewW: number,
  viewH: number,
  rows: number,
  cols: number
): number {
  if (viewW <= 0 || viewH <= 0) return 1;
  const { width: gw, height: gh } = lawnWorldSize(rows, cols);
  return Math.min(viewW / gw, viewH / gh);
}
