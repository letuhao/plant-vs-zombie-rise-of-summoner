import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { OnboardingState } from "@/contract/types";
import { queryKeys } from "./keys";
import { getJson, sendJson } from "./rest";

type CheckpointDto = {
  checkpointId: string; state: string; earnedRunId?: number | null; rewardRef?: string | null;
  payloadJson?: string | null; earnedUtc: string; claimedUtc?: string | null; revision: number;
};
type StateDto = { playerId: number; playerLevel: number; revision: number; checkpoints: CheckpointDto[] };

function adapt(dto: StateDto): OnboardingState {
  return {
    playerId: dto.playerId,
    playerLevel: dto.playerLevel,
    revision: dto.revision,
    checkpoints: dto.checkpoints.map((row) => ({
      checkpointId: row.checkpointId, state: row.state, earnedRunId: row.earnedRunId ?? null,
      rewardRef: row.rewardRef ?? null, payloadJson: row.payloadJson ?? null,
      earnedUtc: row.earnedUtc, claimedUtc: row.claimedUtc ?? null, revision: row.revision
    }))
  };
}

export function useOnboarding(playerId: number) {
  return useQuery({
    queryKey: queryKeys.onboarding(playerId),
    queryFn: async () => adapt(await getJson<StateDto>(`/api/onboarding/${playerId}`)),
    enabled: playerId > 0,
    staleTime: 0
  });
}

export function useClaimOnboarding(playerId: number) {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Onboarding" },
    mutationFn: (checkpointId: string) =>
      sendJson(`/api/onboarding/${playerId}/checkpoints/${encodeURIComponent(checkpointId)}/claim`, "POST", {}),
    onSuccess: () => void qc.invalidateQueries({ queryKey: queryKeys.onboarding(playerId) })
  });
}
