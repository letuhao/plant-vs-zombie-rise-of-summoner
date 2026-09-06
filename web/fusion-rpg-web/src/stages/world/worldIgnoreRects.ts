import type { WorldIgnoreRect } from "@/game/EventBus";

/** Shell `Rail` column width (`Rail.tsx` `w-[92px]`). */
export const SHELL_RAIL_WIDTH_PX = 92;

export type BuildWorldIgnoreRectsInput = {
  /** Map pane size in CSS px (the Phaser canvas parent). */
  width: number;
  height: number;
  /** Inspector dock open — covers left of the canvas (DockShell is viewport-fixed at 92px). */
  dockOpen: boolean;
  /**
   * When true, the map canvas already sits in a flex child beside the shell rail.
   * Phaser pointer coords are canvas-relative, so a 92px left strip must **not** be added
   * (that would eat homeworld / left pins). Gaps D7 / D22.
   */
  canvasBesideRail: boolean;
};

/**
 * HUD / chrome ignore regions in **canvas CSS space** for pick + edge-scroll (gaps D7).
 * Callers that mount `Rail` beside the map pass `canvasBesideRail: true`.
 */
export function buildWorldIgnoreRects(input: BuildWorldIgnoreRectsInput): WorldIgnoreRect[] {
  const { width: w, height: h, dockOpen, canvasBesideRail } = input;
  const rects: WorldIgnoreRect[] = [
    // Top strip (calendar / loam)
    { left: 0, top: 0, width: w, height: 56 },
    // Bottom-left: Fit / +/− / LensPicker
    { left: 0, top: Math.max(0, h - 200), width: 200, height: 200 },
    // Right column: NotifyRail → Outliner → Playback (+ bottom-right turn cluster)
    { left: Math.max(0, w - 280), top: 0, width: 280, height: h }
  ];

  if (!canvasBesideRail) {
    // Only when the canvas still underlays the shell rail (legacy full-bleed). After G12 flex
    // mount, the canvas is already inset — skip this strip.
    rects.push({ left: 0, top: 0, width: SHELL_RAIL_WIDTH_PX, height: h });
  }

  if (dockOpen) {
    // DockShell is fixed at left-[92px] w-[380px]; canvas starts at the rail's right edge, so the
    // dock covers roughly the left 380px of the canvas — not 92+380.
    rects.push({ left: 0, top: 0, width: 380, height: h });
  }

  return rects;
}
