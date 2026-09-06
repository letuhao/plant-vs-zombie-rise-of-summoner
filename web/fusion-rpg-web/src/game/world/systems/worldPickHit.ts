/**
 * Pure pick geometry — shared by worldPickSystem and unit tests (gaps D6).
 */
import { PIN_DISC_PX } from "../objects/pinConstants";

/** World-unit radius of the a11y pin disc at the given camera zoom. */
export function hitRadiusWorld(zoom: number): number {
  const z = zoom > 0 ? zoom : 1;
  return PIN_DISC_PX / 2 / z;
}

export function nearestSectorId(
  pins: Array<{ id: string; x: number; y: number }>,
  worldX: number,
  worldY: number,
  hitR: number
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
