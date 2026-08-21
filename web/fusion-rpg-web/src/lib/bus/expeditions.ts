import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getJson, sendJson } from "./rest";
import type { DemonSpecimenDto } from "./demons";

// ---- DTOs (spec-expeditions.md wire shapes) ----

export type ExpeditionTierDto = {
  tierId: string;
  name: string;
  durationMinutes: number;
  tickCount: number;
  battleCount: number;
  squadSlots: number;
  hasBossWave: boolean;
  tickMinutes: number;
};

// Projected on the server: the sealed seed and correlation bookkeeping never reach the wire.
export type ExpeditionRowDto = {
  id: number;
  state: "Dispatched" | "Collected" | "Recalled";
  tierId: string;
  squadInstanceIds: string[];
  dispatchedUtc: string;
  dueUtc: string;
  collectedUtc?: string | null;
};

export type ExpeditionListDto = {
  serverUtc: string;
  tiers: ExpeditionTierDto[];
  items: ExpeditionRowDto[];
};

export type ExpeditionTickDto = {
  tickIndex: number;
  kind: "battle" | "boss-battle" | "quiet" | "found-souls" | "wild-demon-met" | "injury";
  battleIndex: number;
  souls: number;
  wildSpeciesId?: string | null;
  wildJoins: boolean;
  wildVariant?: string | null;
  injuredKey?: string | null;
};

export type ExpeditionBattleResultDto = {
  battleIndex: number;
  boss: boolean;
  outcome: "victory" | "defeat" | "stalemate";
  runId?: number | null;
  matchKey: string;
};

export type ExpeditionCollectDto = {
  state: "Collected" | "Recalled";
  elapsedTicks: number;
  ticks: ExpeditionTickDto[];
  battles: ExpeditionBattleResultDto[];
  soulsAwarded: number;
  materials: { materialId: string; qty: number }[];
  wildJoins: DemonSpecimenDto[];
  specimenXp: { instanceId: string; xp: number }[];
};

export type DemonMaterialsDto = { items: { materialId: string; qty: number }[] };

// ---- Keys ----

export const expeditionKeys = {
  list: (playerId: number) => ["expeditions", playerId] as const,
  materials: (playerId: number) => ["demonMaterials", playerId] as const
};

// ---- Queries ----

export function useExpeditions(playerId: number) {
  return useQuery({
    queryKey: expeditionKeys.list(playerId),
    queryFn: () => getJson<ExpeditionListDto>(`/api/expeditions/${playerId}`),
    enabled: playerId > 0
  });
}

export function useDemonMaterials(playerId: number) {
  return useQuery({
    queryKey: expeditionKeys.materials(playerId),
    queryFn: () => getJson<DemonMaterialsDto>(`/api/expeditions/${playerId}/materials`),
    enabled: playerId > 0
  });
}

// ---- Mutations ----

export function invalidateExpeditionQueries(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: ["expeditions"] });
  void qc.invalidateQueries({ queryKey: ["demonMaterials"] });
  void qc.invalidateQueries({ queryKey: ["demonRoster"] });
  void qc.invalidateQueries({ queryKey: ["souls"] });
}

export function useDispatchExpedition() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: { playerId?: number; correlationId: string; tierId: string; squad: string[] }) =>
      sendJson<{ replayed: boolean; expedition: ExpeditionRowDto }>("/api/expeditions/dispatch", "POST", req),
    onSuccess: () => invalidateExpeditionQueries(qc)
  });
}

export function useCollectExpedition() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: { expeditionId: number; playerId?: number; recall?: boolean }) =>
      sendJson<ExpeditionCollectDto>(
        `/api/expeditions/${req.expeditionId}/${req.recall ? "recall" : "collect"}`,
        "POST",
        { playerId: req.playerId }
      ),
    onSuccess: () => invalidateExpeditionQueries(qc)
  });
}
