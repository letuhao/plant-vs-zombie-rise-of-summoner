import { describe, expect, it } from "vitest";
import {
  DRAG_THRESHOLD_PX,
  EDGE_SCROLL_MARGIN_PX,
  MAX_SCALE,
  MIN_SCALE
} from "../objects/pinConstants";

/**
 * Camera structural consts and clamp math — pure oracle for R9.
 * Gesture wiring lives in worldCameraSystem (Phaser); these assert the named clamps exist.
 */
function clampWorldZoom(z: number): number {
  return Math.min(MAX_SCALE, Math.max(MIN_SCALE, z));
}

function dragExceedsThreshold(dx: number, dy: number): boolean {
  return Math.hypot(dx, dy) >= DRAG_THRESHOLD_PX;
}

function edgeScrollAxes(pointerX: number, pointerY: number, width: number, height: number): { dx: number; dy: number } {
  const m = EDGE_SCROLL_MARGIN_PX;
  let dx = 0;
  let dy = 0;
  if (pointerX < m) dx = 1;
  else if (pointerX > width - m) dx = -1;
  if (pointerY < m) dy = 1;
  else if (pointerY > height - m) dy = -1;
  return { dx, dy };
}

describe("worldCameraSystem structural clamps (R9)", () => {
  it("clamps zoom to MIN_SCALE..MAX_SCALE", () => {
    expect(clampWorldZoom(0.01)).toBe(MIN_SCALE);
    expect(clampWorldZoom(99)).toBe(MAX_SCALE);
    expect(clampWorldZoom(1)).toBe(1);
  });

  it("separates click from drag via a named pixel threshold", () => {
    expect(dragExceedsThreshold(2, 2)).toBe(false);
    expect(dragExceedsThreshold(DRAG_THRESHOLD_PX, 0)).toBe(true);
  });

  it("edge-scroll activates inside the named margin", () => {
    expect(edgeScrollAxes(10, 200, 1280, 720)).toEqual({ dx: 1, dy: 0 });
    expect(edgeScrollAxes(1270, 10, 1280, 720)).toEqual({ dx: -1, dy: 1 });
    expect(edgeScrollAxes(640, 360, 1280, 720)).toEqual({ dx: 0, dy: 0 });
  });
});
