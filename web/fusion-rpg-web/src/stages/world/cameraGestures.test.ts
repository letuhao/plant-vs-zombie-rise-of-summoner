import { describe, expect, it } from "vitest";
import type { Camera, Extent } from "./camera";
import { beginDrag, dragTo, fit, keyToCameraOp, wheelZoom } from "./cameraGestures";

const camera: Camera = { x: -200, y: -100, w: 1000, h: 600 };

describe("wheelZoom", () => {
  it("zooms in about the pointer on a negative deltaY", () => {
    const zoomed = wheelZoom(camera, -100, 0.5, 0.5);
    expect(zoomed.w).toBeLessThan(camera.w);
  });

  it("zooms out about the pointer on a positive deltaY", () => {
    const zoomed = wheelZoom(camera, 100, 0.5, 0.5);
    expect(zoomed.w).toBeGreaterThan(camera.w);
  });

  it("keeps the pointed-at world coordinate fixed", () => {
    const pointerFracX = 0.2;
    const pointerFracY = 0.8;
    const worldBefore = {
      x: camera.x + pointerFracX * camera.w,
      y: camera.y + pointerFracY * camera.h
    };

    const zoomed = wheelZoom(camera, -100, pointerFracX, pointerFracY);

    expect(zoomed.x + pointerFracX * zoomed.w).toBeCloseTo(worldBefore.x, 6);
    expect(zoomed.y + pointerFracY * zoomed.h).toBeCloseTo(worldBefore.y, 6);
  });
});

describe("drag", () => {
  it("pans when the drag began on empty map", () => {
    const drag = beginDrag("empty", 400, 300, camera);
    const panned = dragTo(drag, 350, 300, 1280, 720);

    expect(panned).not.toBeNull();
    expect(panned!.x).not.toBe(camera.x);
    expect(panned!.w).toBe(camera.w);
    expect(panned!.h).toBe(camera.h);
  });

  it("produces no pan when the drag began on a node — selection, not panning", () => {
    const drag = beginDrag("node", 400, 300, camera);
    const panned = dragTo(drag, 350, 300, 1280, 720);

    expect(panned).toBeNull();
  });
});

describe("keyToCameraOp", () => {
  it("pans by a fixed step for each arrow key", () => {
    const up = keyToCameraOp("ArrowUp", camera)!;
    const down = keyToCameraOp("ArrowDown", camera)!;
    const left = keyToCameraOp("ArrowLeft", camera)!;
    const right = keyToCameraOp("ArrowRight", camera)!;

    expect(up.y).toBeLessThan(camera.y);
    expect(down.y).toBeGreaterThan(camera.y);
    expect(left.x).toBeLessThan(camera.x);
    expect(right.x).toBeGreaterThan(camera.x);

    // A fixed step: opposite keys move the camera by the same magnitude, and it never scales
    // with the camera's own current size the way a percentage-based pan would.
    expect(Math.abs(up.y - camera.y)).toBe(Math.abs(down.y - camera.y));
    expect(Math.abs(left.x - camera.x)).toBe(Math.abs(right.x - camera.x));
  });

  it("W is not bound to anything — it reaches no camera op", () => {
    expect(keyToCameraOp("w", camera)).toBeNull();
    expect(keyToCameraOp("W", camera)).toBeNull();
  });

  it("an arbitrary unbound key also reaches no camera op", () => {
    expect(keyToCameraOp("q", camera)).toBeNull();
  });
});

describe("fit", () => {
  it("shows the full extent", () => {
    const extent: Extent = { minX: -220, minY: -190, maxX: 880, maxY: 570 };
    const fitted = fit(extent, 1280, 720);

    expect(fitted.x).toBeLessThan(extent.minX);
    expect(fitted.y).toBeLessThan(extent.minY);
    expect(fitted.x + fitted.w).toBeGreaterThan(extent.maxX);
    expect(fitted.y + fitted.h).toBeGreaterThan(extent.maxY);
  });
});
