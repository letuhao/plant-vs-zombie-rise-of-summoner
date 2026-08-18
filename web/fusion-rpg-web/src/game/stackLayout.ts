/** Pure stack offsets for grid+stack view — plants left, zombies right. */

export type StackSide = "plant" | "zombie";

export function stackOffset(
  side: StackSide,
  index: number,
  count: number
): { dx: number; dy: number; depth: number } {
  const n = Math.max(1, count);
  const i = Math.max(0, index);
  const mid = (n - 1) / 2;
  const dx = side === "plant" ? -14 : 14;
  const dy = (i - mid) * 10;
  return { dx, dy, depth: 10 + i };
}
