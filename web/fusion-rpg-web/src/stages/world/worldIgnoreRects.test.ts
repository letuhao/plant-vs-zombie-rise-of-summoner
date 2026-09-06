import { describe, expect, it } from "vitest";
import { buildWorldIgnoreRects, SHELL_RAIL_WIDTH_PX } from "./worldIgnoreRects";

describe("buildWorldIgnoreRects — canvas CSS space (gaps D7 / D22)", () => {
  it("omits the 92px shell-rail strip when the canvas already sits beside the rail", () => {
    const rects = buildWorldIgnoreRects({
      width: 1188,
      height: 720,
      dockOpen: false,
      canvasBesideRail: true
    });

    expect(rects.some((r) => r.left === 0 && r.width === SHELL_RAIL_WIDTH_PX && r.height === 720)).toBe(
      false
    );
    // Homeworld / left pins stay pickable: nothing claims only the leftmost 92px of the canvas.
    const leftStrip = rects.filter((r) => r.left === 0 && r.width <= SHELL_RAIL_WIDTH_PX);
    expect(leftStrip).toEqual([]);
  });

  it("includes a 92px left strip only when the canvas still underlays the rail", () => {
    const rects = buildWorldIgnoreRects({
      width: 1280,
      height: 720,
      dockOpen: false,
      canvasBesideRail: false
    });

    expect(rects).toContainEqual({ left: 0, top: 0, width: SHELL_RAIL_WIDTH_PX, height: 720 });
  });

  it("dock ignore is 380px from canvas left — not 92+380", () => {
    const rects = buildWorldIgnoreRects({
      width: 1188,
      height: 720,
      dockOpen: true,
      canvasBesideRail: true
    });

    expect(rects).toContainEqual({ left: 0, top: 0, width: 380, height: 720 });
    expect(rects.some((r) => r.left === 0 && r.width === 92 + 380)).toBe(false);
  });

  it("publishes top strip, bottom-left HUD, and right chrome column", () => {
    const rects = buildWorldIgnoreRects({
      width: 1000,
      height: 800,
      dockOpen: false,
      canvasBesideRail: true
    });

    expect(rects).toContainEqual({ left: 0, top: 0, width: 1000, height: 56 });
    expect(rects).toContainEqual({ left: 0, top: 600, width: 200, height: 200 });
    expect(rects).toContainEqual({ left: 720, top: 0, width: 280, height: 800 });
  });
});
