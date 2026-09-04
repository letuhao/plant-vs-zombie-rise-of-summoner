import { fitToExtent, panBy, zoomAbout, type Camera, type Extent } from "./camera";

/**
 * Raw input events mapped onto camera ops — one meaning per gesture (world-stage W32). The page
 * itself never scrolls on the stage route (W34), so the wheel has no second interpretation to
 * disambiguate: it always means zoom.
 */

/** How much one wheel notch changes scale. A feel constant, not a balance number — nothing here
 * reads `data/tuning/`, the same way no other pixel-level FE interaction constant does. */
const WHEEL_ZOOM_STEP = 1.15;

export function wheelZoom(
  camera: Camera,
  deltaY: number,
  pointerFracX: number,
  pointerFracY: number
): Camera {
  const factor = deltaY < 0 ? WHEEL_ZOOM_STEP : 1 / WHEEL_ZOOM_STEP;
  return zoomAbout(camera, pointerFracX, pointerFracY, factor);
}

/** Where a drag gesture began — the split that separates *pan on empty map* from *select on a node*. */
export type DragOrigin = "empty" | "node";

export type DragState = {
  origin: DragOrigin;
  startScreenX: number;
  startScreenY: number;
  camera: Camera;
};

export function beginDrag(
  origin: DragOrigin,
  startScreenX: number,
  startScreenY: number,
  camera: Camera
): DragState {
  return { origin, startScreenX, startScreenY, camera };
}

/**
 * The pan a drag produces, or `null` when it produces none at all. A drag that began on a node is
 * never a pan — it is a selection gesture, and the caller (`WorldStage`, W33) is the one that turns
 * it into a `select-sector`/`select-entity` dispatch, not this module.
 */
export function dragTo(
  drag: DragState,
  currentScreenX: number,
  currentScreenY: number,
  viewportW: number,
  viewportH: number
): Camera | null {
  if (drag.origin === "node") return null;

  const worldPerScreenX = drag.camera.w / viewportW;
  const worldPerScreenY = drag.camera.h / viewportH;
  const dx = (drag.startScreenX - currentScreenX) * worldPerScreenX;
  const dy = (drag.startScreenY - currentScreenY) * worldPerScreenY;

  return panBy(drag.camera, dx, dy);
}

/** World units an arrow-key pan moves per press. A feel constant, not a balance number. */
const ARROW_PAN_STEP = 80;

const ARROW_DELTAS: Readonly<Record<string, readonly [number, number]>> = {
  ArrowUp: [0, -1],
  ArrowDown: [0, 1],
  ArrowLeft: [-1, 0],
  ArrowRight: [1, 0]
};

/**
 * The camera op one key press produces, or `null` for a key this stage does not bind. **`W` is not
 * bound to anything** — the map's arbitration row is "arrows pan, `W` cycles" (`WASD` was removed
 * on 2026-09-03 for exactly this collision) — so `keyToCameraOp("w", ...)` simply isn't in the
 * lookup table, the same way any other unbound key isn't.
 */
export function keyToCameraOp(key: string, camera: Camera): Camera | null {
  const delta = ARROW_DELTAS[key];
  if (!delta) return null;
  return panBy(camera, delta[0] * ARROW_PAN_STEP, delta[1] * ARROW_PAN_STEP);
}

/** Thin re-export so every camera gesture — including `fit` — is reachable from one module. */
export function fit(extent: Extent, viewportW: number, viewportH: number): Camera {
  return fitToExtent(extent, viewportW, viewportH);
}
