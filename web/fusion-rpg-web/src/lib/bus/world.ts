import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getJson, sendJson, tryGetJson } from "./rest";
import type {
  WorldStateDto,
  WorldTurnReportDto
} from "@/features/world/worldTypes";

/**
 * The world map's bus layer (spec-world-model.md §Server). Everything the map page does goes through
 * here — no page fetches directly, so caching, invalidation, and the SIM base URL all live in one
 * place.
 */

export type WorldHeaderDto = {
  worldId: string;
  templateId: string;
  currentTurn: number;
  state: string;
  createdUtc: string;
  revision: number;
};

export type WorldCommandRequest = {
  commandId: string;
  kind: string;
  entityId?: string | null;
  sectorId?: string | null;
  slotIndex?: number | null;
  lanePath?: string[];
};

export type WorldCommandResultDto = {
  commandId: string;
  ok: boolean;
  reason: string;
  replayed: boolean;
};

export type WorldSubmitResultDto = {
  turn: number;
  commanderId: string;
  results: WorldCommandResultDto[];
};

export type WorldTurnCommitDto = {
  ok: boolean;
  reason: string;
  advanced: boolean;
  stateHash: string | null;
  currentTurn: number;
};

export const worldKeys = {
  header: (playerId: number) => ["world", "header", playerId] as const,
  state: (worldId: string) => ["world", "state", worldId] as const,
  turn: (worldId: string, turn: number) => ["world", "turn", worldId, turn] as const
};

/** The player's active world, or null when they have not started one. */
export function useWorldHeader(playerId: number) {
  return useQuery({
    queryKey: worldKeys.header(playerId),
    queryFn: () => tryGetJson<WorldHeaderDto>(`/api/world/${playerId}`),
    enabled: playerId > 0
  });
}

/**
 * The map as one faction knows it. Omitting `asFaction` asks as the player, which is what the map
 * view wants — passing someone else's id is for debugging fog, not for playing.
 */
export function useWorldState(
  worldId: string | null | undefined,
  options?: { asFaction?: string; lifelines?: boolean }
) {
  const asFaction = options?.asFaction;
  const lifelines = options?.lifelines ?? false;

  return useQuery({
    queryKey: [...worldKeys.state(worldId ?? ""), asFaction ?? "player", lifelines],
    queryFn: () => {
      // Reconnection cost is an expensive sweep on the server, so it is only asked for while the
      // overlay is actually showing.
      const query = new URLSearchParams();
      if (asFaction) query.set("asFaction", asFaction);
      if (lifelines) query.set("lifelines", "true");
      const suffix = query.toString();

      return getJson<WorldStateDto>(`/api/world/${worldId}/state` + (suffix ? `?${suffix}` : ""));
    },
    enabled: !!worldId
  });
}

/**
 * One turn's report. Turns outside the store's hot tail are re-derived by replay, and the server
 * refuses rather than fabricating across an engine version change — so an old turn can legitimately
 * come back with no entries.
 */
export function useWorldTurnReport(worldId: string | null | undefined, turn: number | null) {
  return useQuery({
    queryKey: worldKeys.turn(worldId ?? "", turn ?? -1),
    queryFn: () => tryGetJson<WorldTurnReportDto>(`/api/world/${worldId}/turn/${turn}`),
    enabled: !!worldId && turn != null && turn >= 0
  });
}

export function useSubmitWorldCommands(worldId: string | null | undefined) {
  return useMutation({
    mutationFn: (vars: { commanderId?: string; commands: WorldCommandRequest[] }) =>
      sendJson<WorldSubmitResultDto>(`/api/world/${worldId}/commands`, "POST", vars)
  });
}

/**
 * End this commander's turn. The world steps when the *last* commander commits, so `advanced` is
 * true at most once a turn — and only then is there anything new to read.
 */
export function useCommitWorldTurn(worldId: string | null | undefined) {
  const qc = useQueryClient();
  return useMutation({
    // `turn` is required by the server: a commit names the turn it means to end, so a resend the
    // client never saw the answer to is refused rather than resolving the *next* turn.
    mutationFn: (vars: { turn: number; commanderId?: string }) =>
      sendJson<WorldTurnCommitDto>(`/api/world/${worldId}/commit`, "POST", vars),
    onSuccess: (result) => {
      if (!result.advanced) return;
      void qc.invalidateQueries({ queryKey: ["world"] });
    }
  });
}
