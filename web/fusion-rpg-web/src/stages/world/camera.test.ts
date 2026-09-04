import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { MAX_SCALE, MIN_SCALE, fitToExtent, panBy, zoomAbout, type Camera, type Extent } from "./camera";

const camera: Camera = { x: -200, y: -100, w: 1000, h: 600 };

describe("zoomAbout", () => {
  it("keeps the pointed-at world coordinate fixed on screen", () => {
    const pointerFracX = 0.3;
    const pointerFracY = 0.7;
    const worldBefore = {
      x: camera.x + pointerFracX * camera.w,
      y: camera.y + pointerFracY * camera.h
    };

    const zoomed = zoomAbout(camera, pointerFracX, pointerFracY, 2);

    const worldAfter = {
      x: zoomed.x + pointerFracX * zoomed.w,
      y: zoomed.y + pointerFracY * zoomed.h
    };

    expect(worldAfter.x).toBeCloseTo(worldBefore.x, 6);
    expect(worldAfter.y).toBeCloseTo(worldBefore.y, 6);
  });

  it("zooms in when the factor is greater than 1", () => {
    const zoomed = zoomAbout(camera, 0.5, 0.5, 2);
    expect(zoomed.w).toBeLessThan(camera.w);
    expect(zoomed.h).toBeLessThan(camera.h);
  });

  it("clamps the zoomed-in direction at MAX_SCALE", () => {
    const zoomed = zoomAbout(camera, 0.5, 0.5, 1_000_000);
    const wAtMax = zoomAbout({ x: 0, y: 0, w: 1200 / MAX_SCALE, h: 600 }, 0.5, 0.5, 1_000_000).w;
    expect(zoomed.w).toBeCloseTo(wAtMax, 6);
  });

  it("clamps the zoomed-out direction at MIN_SCALE", () => {
    const zoomed = zoomAbout(camera, 0.5, 0.5, 0.000_001);
    const wAtMin = zoomAbout({ x: 0, y: 0, w: 1200 / MIN_SCALE, h: 600 }, 0.5, 0.5, 0.000_001).w;
    expect(zoomed.w).toBeCloseTo(wAtMin, 6);
  });

  it("MIN_SCALE and MAX_SCALE actually bound opposite directions", () => {
    expect(MIN_SCALE).toBeLessThan(1);
    expect(MAX_SCALE).toBeGreaterThan(1);
  });
});

describe("panBy", () => {
  it("moves the viewport without changing its size", () => {
    const panned = panBy(camera, 40, -25);
    expect(panned).toEqual({ x: camera.x + 40, y: camera.y - 25, w: camera.w, h: camera.h });
  });
});

describe("fitToExtent", () => {
  const extent: Extent = { minX: -220, minY: -190, maxX: 880, maxY: 570 };

  it.each([
    [1280, 720],
    [1440, 900]
  ])("puts the full extent on screen with padding at %ix%i", (viewportW, viewportH) => {
    const fitted = fitToExtent(extent, viewportW, viewportH);

    // The extent must sit entirely inside the fitted viewBox — that is the whole point of "fit".
    expect(fitted.x).toBeLessThan(extent.minX);
    expect(fitted.y).toBeLessThan(extent.minY);
    expect(fitted.x + fitted.w).toBeGreaterThan(extent.maxX);
    expect(fitted.y + fitted.h).toBeGreaterThan(extent.maxY);

    // And the viewBox itself must match the viewport's aspect ratio, or the map would letterbox
    // or crop instead of actually filling the screen.
    expect(fitted.w / fitted.h).toBeCloseTo(viewportW / viewportH, 6);
  });
});

describe("camera.ts stays DOM-and-library-free", () => {
  it("imports no react, @xyflow/react or document", () => {
    const source = readFileSync(join(__dirname, "camera.ts"), "utf8");
    expect(source).not.toMatch(/from ["']react["']/);
    expect(source).not.toMatch(/@xyflow\/react/);
    expect(source).not.toMatch(/\bdocument\b/);
  });
});
