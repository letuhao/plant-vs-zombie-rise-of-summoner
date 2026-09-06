import { describe, expect, it } from "vitest";
import { DRAG_THRESHOLD_PX, PIN_DISC_PX } from "../objects/pinConstants";
import { hitRadiusWorld, nearestSectorId } from "./worldPickHit";

describe("world pick hit radius (gaps D6)", () => {
  it("uses PIN_DISC_PX CSS half-disc divided by camera zoom", () => {
    expect(hitRadiusWorld(1)).toBe(PIN_DISC_PX / 2);
    expect(hitRadiusWorld(2)).toBe(PIN_DISC_PX / 4);
    expect(hitRadiusWorld(0.5)).toBe(PIN_DISC_PX);
  });

  it("selects the nearest pin within the zoom-scaled hit disk", () => {
    const pins = [
      { id: "homeworld", x: 110, y: 95 },
      { id: "ash-waste", x: 990, y: 95 }
    ];
    const r1 = hitRadiusWorld(1);
    expect(nearestSectorId(pins, 112, 97, r1)).toBe("homeworld");
    expect(nearestSectorId(pins, 990, 95, r1)).toBe("ash-waste");
    expect(nearestSectorId(pins, 500, 500, r1)).toBeNull();
    // At zoom 2 the world-space disk shrinks — a point 30wu away misses.
    expect(nearestSectorId(pins, 110 + 30, 95, hitRadiusWorld(2))).toBeNull();
  });
});

describe("world pick ignore / drag rules (gaps D4/D5)", () => {
  it("treats a drag past threshold as suppress-pick", () => {
    const shouldSuppress = (dx: number, dy: number) => Math.hypot(dx, dy) >= DRAG_THRESHOLD_PX;
    expect(shouldSuppress(2, 2)).toBe(false);
    expect(shouldSuppress(DRAG_THRESHOLD_PX, 0)).toBe(true);
  });
});
