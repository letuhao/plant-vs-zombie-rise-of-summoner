/**
 * The whole navigation model as pure data: an SVG `viewBox` and pure functions over it. No DOM
 * anywhere in this file — every gesture, every zoom tier and every fit control resolves to a
 * `Camera`, so the map's pan/zoom correctness is testable without a canvas (world-stage W31).
 *
 * `worldViewModel.ts` already places sectors on a plain `{x,y}` authored grid
 * (`GRID_X`/`GRID_Y`, no auto-layout), so there is no layout pass to port — the camera is the
 * entire navigation surface.
 */

/** World-space viewport: the SVG `viewBox` an `<svg>` element is given directly. */
export type Camera = { x: number; y: number; w: number; h: number };

/** The bounding box of the authored grid, in the same world units as `Camera`. */
export type Extent = { minX: number; minY: number; maxX: number; maxY: number };

/**
 * Below this, a wheel notch or two would already turn the whole map into a handful of pixels —
 * the control stops *working*, not merely feeling different. Structural, not a balance number
 * (tunables-ssot.md's own test): it changes whether zoom-out functions at all, never how the game
 * feels.
 */
export const MIN_SCALE = 0.25;

/**
 * Above this, one sector fills the viewport and lane geometry stops reading as a map at all.
 * Structural for the identical reason `MIN_SCALE` is.
 */
export const MAX_SCALE = 4;

/** World-unit width a `scale` of exactly 1 maps to — the neutral, un-zoomed reference. */
const REFERENCE_WIDTH = 1200;

/** Breathing room around the authored grid at `fitToExtent`'s default padding, in world units. */
const DEFAULT_PADDING = 160;

function scaleOf(camera: Camera): number {
  return REFERENCE_WIDTH / camera.w;
}

function clampScale(scale: number): number {
  return Math.min(MAX_SCALE, Math.max(MIN_SCALE, scale));
}

/**
 * Zoom by `factor`, keeping the world coordinate under the pointer fixed on screen — the one
 * assertion that is the whole correctness of wheel zoom. `pointerFracX`/`pointerFracY` are the
 * pointer's position as a fraction of the viewport (0..1), not a pixel coordinate, so this stays
 * independent of the actual screen size.
 */
export function zoomAbout(
  camera: Camera,
  pointerFracX: number,
  pointerFracY: number,
  factor: number
): Camera {
  const nextScale = clampScale(scaleOf(camera) * factor);
  const aspect = camera.h / camera.w;
  const nextW = REFERENCE_WIDTH / nextScale;
  const nextH = nextW * aspect;

  const worldX = camera.x + pointerFracX * camera.w;
  const worldY = camera.y + pointerFracY * camera.h;

  return {
    x: worldX - pointerFracX * nextW,
    y: worldY - pointerFracY * nextH,
    w: nextW,
    h: nextH
  };
}

/** A plain drag: move the viewport by a world-space delta, no zoom change. */
export function panBy(camera: Camera, dx: number, dy: number): Camera {
  return { ...camera, x: camera.x + dx, y: camera.y + dy };
}

/**
 * Put the whole extent on screen with padding, at whatever aspect ratio the viewport actually is —
 * grows the tighter-constrained dimension to match the viewport's aspect rather than cropping the
 * other one, so `fit` never hides part of the authored grid.
 */
export function fitToExtent(
  extent: Extent,
  viewportW: number,
  viewportH: number,
  padding = DEFAULT_PADDING
): Camera {
  const extentW = Math.max(1, extent.maxX - extent.minX) + padding * 2;
  const extentH = Math.max(1, extent.maxY - extent.minY) + padding * 2;

  const viewportAspect = viewportW / viewportH;
  const extentAspect = extentW / extentH;

  const w = extentAspect > viewportAspect ? extentW : extentH * viewportAspect;
  const h = w / viewportAspect;

  const cx = (extent.minX + extent.maxX) / 2;
  const cy = (extent.minY + extent.maxY) / 2;

  return { x: cx - w / 2, y: cy - h / 2, w, h };
}
