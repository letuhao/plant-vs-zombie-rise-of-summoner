/**
 * Generation-scoped Mediator between React host and Phaser scenes.
 * Foreign generation events are dropped by subscribers (RT-02 / RT-11).
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

type Handler = (payload: unknown) => void;

const listeners = new Map<LawnBusEvent, Set<Handler>>();

export function lawnBusOn(event: LawnBusEvent, handler: Handler): () => void {
  let set = listeners.get(event);
  if (!set) {
    set = new Set();
    listeners.set(event, set);
  }
  set.add(handler);
  return () => {
    set!.delete(handler);
  };
}

export function lawnBusEmit(event: LawnBusEvent, payload: unknown): void {
  const set = listeners.get(event);
  if (!set) return;
  for (const h of [...set]) h(payload);
}

/** Test / destroy helper — clears all bus listeners. */
export function lawnBusClearAll(): void {
  listeners.clear();
}

let nextGeneration = 1;

export function allocGameGeneration(): number {
  return nextGeneration++;
}
