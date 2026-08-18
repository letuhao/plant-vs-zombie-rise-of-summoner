import type { LawnPhase } from "./lawnViewModel";

/** UI-only interaction FSM — never CapPolicy / UniqueActor. */
export type InteractionMode =
  | "Idle"
  | "TileSelected"
  | "OccupantSelected"
  | "SpawnTargeting";

export type InteractionState = {
  mode: InteractionMode;
  row?: number;
  col?: number;
  ptr?: string;
};

export function idleInteraction(): InteractionState {
  return { mode: "Idle" };
}

/** RT-06: SpawnTargeting disabled in Idle / Ending. */
export function canEnterSpawnTargeting(phase: LawnPhase): boolean {
  return phase === "Starting" || phase === "InMatch" || phase === "Paused";
}

export type InteractionEvent =
  | { type: "selectTile"; row: number; col: number }
  | { type: "selectOccupant"; ptr: string; row?: number; col?: number }
  | { type: "clear" }
  | { type: "enterSpawnTargeting" }
  | { type: "phaseChanged"; phase: LawnPhase };

export function reduceInteraction(
  state: InteractionState,
  event: InteractionEvent,
  phase: LawnPhase
): InteractionState {
  switch (event.type) {
    case "clear":
      return idleInteraction();
    case "selectTile":
      // Keep SpawnTargeting while picking cells (W7 ghost + Intent enqueue).
      if (state.mode === "SpawnTargeting") {
        return {
          mode: "SpawnTargeting",
          row: event.row,
          col: event.col,
          ptr: undefined
        };
      }
      return { mode: "TileSelected", row: event.row, col: event.col };
    case "selectOccupant":
      return {
        mode: "OccupantSelected",
        ptr: event.ptr,
        row: event.row,
        col: event.col
      };
    case "enterSpawnTargeting":
      if (!canEnterSpawnTargeting(phase)) return state;
      return {
        mode: "SpawnTargeting",
        row: state.row,
        col: state.col,
        ptr: undefined
      };
    case "phaseChanged":
      if (state.mode === "SpawnTargeting" && !canEnterSpawnTargeting(event.phase)) {
        return idleInteraction();
      }
      return state;
    default:
      return state;
  }
}
