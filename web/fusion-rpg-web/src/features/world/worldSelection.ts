import type { WorldCommandRequest } from "@/lib/bus/world";
import type { WorldEntityDto } from "./worldTypes";
import type { WorldGraph } from "./worldViewModel";

/**
 * Selection and the pending-order queue — the map page's entire interaction state, as a pure
 * reducer. Keeping it out of the component is what makes "can I claim this?" testable without a
 * canvas, and it is the same rule the engine applies, so a refusal is never a surprise.
 */

export type PendingOrder = {
  commandId: string;
  kind: "move" | "clear" | "claim";
  entityId: string;
  sectorId?: string;
  slotIndex?: number;
  lanePath?: string[];
  /** What the order does, in one line, for the queue list. */
  label: string;
};

export type WorldUiState = {
  selectedSectorId: string | null;
  selectedEntityId: string | null;
  pending: PendingOrder[];
};

export type WorldUiAction =
  | { type: "select-sector"; sectorId: string | null }
  | { type: "select-entity"; entityId: string | null }
  | { type: "queue"; order: PendingOrder }
  | { type: "unqueue"; commandId: string }
  | { type: "clear-queue" };

export const initialWorldUi: WorldUiState = {
  selectedSectorId: null,
  selectedEntityId: null,
  pending: []
};

export function worldUiReducer(state: WorldUiState, action: WorldUiAction): WorldUiState {
  switch (action.type) {
    case "select-sector":
      return { ...state, selectedSectorId: action.sectorId };
    case "select-entity":
      return { ...state, selectedEntityId: action.entityId };
    case "queue": {
      // One order per legion per turn, the same rule the engine's movement phase applies: a second
      // order for the same force replaces the first rather than stacking behind it.
      const kept = state.pending.filter(
        (p) => !(p.entityId === action.order.entityId && p.kind === action.order.kind)
      );
      return { ...state, pending: [...kept, action.order] };
    }
    case "unqueue":
      return { ...state, pending: state.pending.filter((p) => p.commandId !== action.commandId) };
    case "clear-queue":
      return { ...state, pending: [] };
    default:
      return state;
  }
}

/** Wire shape for the whole queue — the exact payload `POST /commands` expects. */
export function toRequests(pending: PendingOrder[]): WorldCommandRequest[] {
  return pending.map((order) => ({
    commandId: order.commandId,
    kind: order.kind,
    entityId: order.entityId,
    sectorId: order.sectorId ?? null,
    slotIndex: order.slotIndex ?? null,
    lanePath: order.lanePath ?? []
  }));
}

/**
 * The lane path from one sector to another, as a breadth-first walk over open lanes in stable id
 * order. Wave 1 has no lane costs on the client — this finds *a* legal route, and the engine is the
 * one that decides how far along it the legion actually gets this turn.
 */
export function routeBetween(graph: WorldGraph, fromSectorId: string, toSectorId: string): string[] | null {
  if (fromSectorId === toSectorId) return null;

  const neighbours = new Map<string, { laneId: string; to: string }[]>();
  for (const edge of graph.edges) {
    if (edge.data.severed) continue;
    for (const [a, b] of [
      [edge.source, edge.target],
      [edge.target, edge.source]
    ]) {
      const list = neighbours.get(a);
      const step = { laneId: edge.id, to: b };
      if (list) list.push(step);
      else neighbours.set(a, [step]);
    }
  }

  const cameFrom = new Map<string, { laneId: string; from: string }>();
  const seen = new Set<string>([fromSectorId]);
  const queue: string[] = [fromSectorId];

  while (queue.length > 0) {
    const current = queue.shift()!;
    if (current === toSectorId) break;

    for (const step of (neighbours.get(current) ?? []).slice().sort((x, y) => (x.laneId < y.laneId ? -1 : 1))) {
      if (seen.has(step.to)) continue;
      seen.add(step.to);
      cameFrom.set(step.to, { laneId: step.laneId, from: current });
      queue.push(step.to);
    }
  }

  if (!cameFrom.has(toSectorId)) return null;

  const path: string[] = [];
  let cursor = toSectorId;
  while (cursor !== fromSectorId) {
    const step = cameFrom.get(cursor)!;
    path.unshift(step.laneId);
    cursor = step.from;
  }

  return path;
}

/**
 * The route to file for one particular force, which is not the same question as the route between
 * two sectors.
 *
 * A legion caught in mid-stride is the awkward case: the engine resumes a march from the lane the
 * legion is already on and **refuses any path that does not contain it** (`path.not-contiguous`).
 * So the current lane goes at the head of the route and the rest is walked from the end it is
 * heading toward — otherwise the order looks fine in the queue and is silently dropped when the
 * turn resolves.
 */
export function routeForLegion(
  graph: WorldGraph,
  legion: WorldEntityDto,
  toSectorId: string
): string[] | null {
  if (legion.atSectorId) return routeBetween(graph, legion.atSectorId, toSectorId);

  const currentLane = legion.onLaneId;
  const heading = legion.onLaneTowardSectorId;
  if (!currentLane || !heading) return null;

  // It is already walking into the destination — the lane it is on *is* the whole order.
  if (heading === toSectorId) return [currentLane];

  const onward = routeBetween(graph, heading, toSectorId);
  return onward ? [currentLane, ...onward] : null;
}

/** A stable, readable id for an order — unique per commander per turn, which is what the store keys on. */
export function orderId(turn: number, kind: string, entityId: string): string {
  return `t${turn}-${kind}-${entityId}`;
}
