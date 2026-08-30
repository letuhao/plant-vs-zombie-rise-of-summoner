import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getJson, sendJson } from "./rest";

// ---- DTOs (aura-skill T18c wire shapes -- AuraCatalogEndpoints.cs / AuraRuntimeEndpoints.cs /
// AuraDerivedEndpoints.cs) ----

export type AuraUpkeepCostDto = {
  resourceId: string;
  amountMin: number;
  amountMax: number;
  when: string;
};

export type AuraCatalogItemDto = {
  auraId: string;
  aptitudeId: string;
  upkeep: AuraUpkeepCostDto[];
};

export type AuraCatalogDto = {
  items: AuraCatalogItemDto[];
};

export type AuraRuntimeStateDto = {
  playerId: number;
  activeAuraIds: string[];
  equippedAuraIds: string[];
  maxActiveAuras: number;
};

export type AuraEnableResultDto = {
  playerId: number;
  enabledAuraId: string;
  evictedAuraId: string | null;
  activeAuraIds: string[];
};

export type AuraDisableResultDto = {
  playerId: number;
  disabledAuraId: string;
  wasActive: boolean;
  activeAuraIds: string[];
};

export type DerivedContributionDto = {
  sourceId: string;
  op: string;
  value: number;
};

export type DerivedChannelDto = {
  channelId: string;
  value: number;
  contributions: DerivedContributionDto[];
};

export type ActorDerivedDto = {
  instanceId: string;
  channels: DerivedChannelDto[];
};

// ---- Queries ----

export function useAuraCatalog() {
  return useQuery({
    queryKey: ["auraCatalog"] as const,
    queryFn: () => getJson<AuraCatalogDto>("/api/auras"),
    staleTime: Infinity // authored data, never changes at runtime
  });
}

export function useAuraRuntime(playerId: number) {
  return useQuery({
    queryKey: ["auraRuntime", playerId] as const,
    queryFn: () => getJson<AuraRuntimeStateDto>(`/api/aura-runtime/${playerId}`),
    enabled: playerId > 0
  });
}

export function useActorDerived(instanceId: string | null | undefined) {
  return useQuery({
    queryKey: ["actorDerived", instanceId ?? ""] as const,
    queryFn: () => getJson<ActorDerivedDto>(`/api/actors/${encodeURIComponent(instanceId!)}/derived`),
    enabled: !!instanceId
  });
}

// ---- Mutations ----

export function useEnableAura(playerId: number) {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Aura" },
    mutationFn: (auraId: string) =>
      sendJson<AuraEnableResultDto>(`/api/aura-runtime/${playerId}/enable`, "POST", { auraId }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["auraRuntime", playerId] });
    }
  });
}

export function useDisableAura(playerId: number) {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Aura" },
    mutationFn: (auraId: string) =>
      sendJson<AuraDisableResultDto>(`/api/aura-runtime/${playerId}/disable`, "POST", { auraId }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["auraRuntime", playerId] });
    }
  });
}
