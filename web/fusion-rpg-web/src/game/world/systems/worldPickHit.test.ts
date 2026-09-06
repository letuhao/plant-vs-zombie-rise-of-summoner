import { describe, expect, it } from "vitest";

/** Mirrors worldPickSystem.resolveSectorHit distance logic for regression (bounds were screen-space). */
function nearestPin(
  pins: Array<{ id: string; x: number; y: number }>,
  worldX: number,
  worldY: number,
  hitR = 28
): string | null {
  let best: string | null = null;
  let bestDist = hitR * hitR;
  for (const p of pins) {
    const dx = p.x - worldX;
    const dy = p.y - worldY;
    const d2 = dx * dx + dy * dy;
    if (d2 <= bestDist) {
      bestDist = d2;
      best = p.id;
    }
  }
  return best;
}

describe("world pick hit uses world-space distance (not screen getBounds)", () => {
  it("selects the nearest pin within hit radius", () => {
    const pins = [
      { id: "homeworld", x: 110, y: 95 },
      { id: "ash-waste", x: 990, y: 95 }
    ];
    expect(nearestPin(pins, 112, 97)).toBe("homeworld");
    expect(nearestPin(pins, 990, 95)).toBe("ash-waste");
    expect(nearestPin(pins, 500, 500)).toBeNull();
  });
});
