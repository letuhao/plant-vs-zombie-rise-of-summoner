/**
 * Generation-scoped Mediator between React host and Phaser scenes.
 * Foreign generation events are dropped by subscribers (RT-02 / RT-11).
 *
 * Lawn and world use **separate** listener maps so `worldBusClearAll` never wipes lawn
 * subscribers (and the reverse). They share `allocGameGeneration()`.
 */

export type LawnBusEvent =
  | "lawn:model"
  | "lawn:select"
  | "lawn:interaction"
  | "lawn:viewMode"
  | "lawn:resized"
  | "lawn:ready"
  | "lawn:destroyed";

export type LawnSelectPayload = {
  generation: number;
  kind: "tile" | "occupant";
  row?: number;
  col?: number;
  ptr?: string;
};

export type LawnInteractionPayload = {
  generation: number;
  mode: string;
  row?: number;
  col?: number;
  ptr?: string;
};

export type LawnModelPayload = {
  generation: number;
  revision: number;
  /** Opaque LawnViewModel from features/lawn — kept as unknown at bus edge. */
  model: unknown;
};

export type LawnViewModePayload = {
  generation: number;
  viewMode: string;
};

export type LawnResizedPayload = {
  generation: number;
  width: number;
  height: number;
};

export type WorldBusEvent =
  | "world:model"
  | "world:select"
  | "world:camera"
  | "world:lens"
  | "world:interaction"
  | "world:ready"
  | "world:resized"
  | "world:destroyed";

export type WorldSelectPayload = {
  generation: number;
  kind: "sector" | "lane" | "force" | "empty";
  id?: string;
};

export type WorldCameraPayload = {
  generation: number;
  op: "pan" | "zoom" | "fit" | "centre";
  dx?: number;
  dy?: number;
  /** Absolute zoom target (HUD +/− may instead use `factor`). */
  scale?: number;
  /** Relative zoom multiplier about viewport centre (gaps D9). */
  factor?: number;
  /** World coords for `op: "centre"` (gaps D25). */
  x?: number;
  y?: number;
  /** Fit padding / safe insets in screen px. */
  padLeft?: number;
  padRight?: number;
  padTop?: number;
  padBottom?: number;
};

export type WorldLensPayload = {
  generation: number;
  lens: string;
};

export type WorldIgnoreRect = {
  left: number;
  top: number;
  width: number;
  height: number;
};

export type WorldInteractionPayload = {
  generation: number;
  selectedId?: string | null;
  selectedKind?: "sector" | "lane" | "force" | null;
  targeting?: unknown;
  ignoreRects?: WorldIgnoreRect[];
};

export type WorldModelPayload = {
  generation: number;
  /** Host monotonic dirty flag — not a wire DTO revision. */
  modelSeq: number;
  /** Adapted world view (+ overlay inputs) — opaque at the bus edge. */
  model: unknown;
};

export type WorldResizedPayload = {
  generation: number;
  width: number;
  height: number;
};

type Handler = (payload: unknown) => void;

const lawnListeners = new Map<LawnBusEvent, Set<Handler>>();
const worldListeners = new Map<WorldBusEvent, Set<Handler>>();

function on<E extends string>(
  map: Map<E, Set<Handler>>,
  event: E,
  handler: Handler
): () => void {
  let set = map.get(event);
  if (!set) {
    set = new Set();
    map.set(event, set);
  }
  set.add(handler);
  return () => {
    set!.delete(handler);
  };
}

function emit<E extends string>(map: Map<E, Set<Handler>>, event: E, payload: unknown): void {
  const set = map.get(event);
  if (!set) return;
  for (const h of [...set]) h(payload);
}

export function lawnBusOn(event: LawnBusEvent, handler: Handler): () => void {
  return on(lawnListeners, event, handler);
}

export function lawnBusEmit(event: LawnBusEvent, payload: unknown): void {
  emit(lawnListeners, event, payload);
}

/** Test / destroy helper — clears lawn bus listeners only. */
export function lawnBusClearAll(): void {
  lawnListeners.clear();
}

export function worldBusOn(event: WorldBusEvent, handler: Handler): () => void {
  return on(worldListeners, event, handler);
}

export function worldBusEmit(event: WorldBusEvent, payload: unknown): void {
  emit(worldListeners, event, payload);
}

/** Test / destroy helper — clears world bus listeners only (lawn untouched). */
export function worldBusClearAll(): void {
  worldListeners.clear();
}

let nextGeneration = 1;

export function allocGameGeneration(): number {
  return nextGeneration++;
}

/** Test helper — reset generation counter. */
export function resetGameGenerationForTests(): void {
  nextGeneration = 1;
}
