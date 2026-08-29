import { useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "./keys";
import { clearLogEvents } from "./log-store";
import { sendJson } from "./rest";
import { clearCheatFloatDirty } from "./cheat-dirty";
import type {
  CheatEntry,
  CheatSnapshot,
  PlayerDto,
  ProbeRunResult,
  StatsConfig,
  StoragePurgeResult,
  UniqueActorDeployResultDto,
  UniqueActorDto,
  UniqueEquipmentListDto,
  AptitudesState
} from "./types";

/**
 * `meta.entity` on every mutation below (T11) is what the toast layer
 * (`shell/Toasts.tsx`) names in a failure — "Creature update failed —
 * nothing changed", not just "Something went wrong". A global
 * `MutationCache` listener (`app/providers.tsx`) reads it, so every
 * mutation gets a band-4 result without each call site wiring its own.
 */

export function useSaveStats() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Stats" },
    mutationFn: async (stats: StatsConfig) => {
      await sendJson("/api/stats", "PUT", stats);
      await sendJson("/api/commands/reload-stats", "POST", {});
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.stats });
    }
  });
}

function patchCheatEntry(
  snap: CheatSnapshot | undefined,
  id: string,
  patch: Partial<CheatEntry>
): CheatSnapshot {
  const entries = [...(snap?.entries ?? [])];
  const i = entries.findIndex((e) => e.id === id);
  if (i >= 0) {
    entries[i] = { ...entries[i]!, ...patch, isSet: true };
  } else {
    entries.push({
      id,
      kind: patch.floatValue != null ? "number" : "toggle",
      enabled: patch.enabled ?? true,
      floatValue: patch.floatValue,
      isSet: true
    });
  }
  return { ...(snap ?? {}), entries };
}

export function useSaveCheats() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Cheats" },
    mutationFn: (snap: CheatSnapshot) => sendJson("/api/cheats", "PUT", snap),
    onSuccess: (_data, snap) => {
      qc.setQueryData(queryKeys.cheats, snap);
    }
  });
}

export function useToggleCheat() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Cheat" },
    mutationFn: (body: { id: string; enabled: boolean }) => sendJson("/api/cheats/toggle", "POST", body),
    onMutate: async (body) => {
      await qc.cancelQueries({ queryKey: queryKeys.cheats });
      const prev = qc.getQueryData<CheatSnapshot>(queryKeys.cheats);
      qc.setQueryData(queryKeys.cheats, patchCheatEntry(prev, body.id, { enabled: body.enabled }));
      return { prev };
    },
    onError: (_e, _b, ctx) => {
      if (ctx?.prev) qc.setQueryData(queryKeys.cheats, ctx.prev);
    }
  });
}

export function useSetCheatFloat() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Cheat" },
    mutationFn: (body: { id: string; value: number }) => sendJson("/api/cheats/set-float", "POST", body),
    onMutate: async (body) => {
      await qc.cancelQueries({ queryKey: queryKeys.cheats });
      const prev = qc.getQueryData<CheatSnapshot>(queryKeys.cheats);
      qc.setQueryData(
        queryKeys.cheats,
        patchCheatEntry(prev, body.id, { enabled: true, floatValue: body.value, isSet: true })
      );
      return { prev };
    },
    onSuccess: (_data, body) => {
      clearCheatFloatDirty(body.id);
    },
    onError: (_e, body, ctx) => {
      clearCheatFloatDirty(body.id);
      if (ctx?.prev) qc.setQueryData(queryKeys.cheats, ctx.prev);
    }
  });
}

export function useClearCheatField() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Cheat" },
    mutationFn: (body: { id: string }) => sendJson("/api/cheats/clear-field", "POST", body),
    onMutate: async (body) => {
      await qc.cancelQueries({ queryKey: queryKeys.cheats });
      const prev = qc.getQueryData<CheatSnapshot>(queryKeys.cheats);
      if (prev?.entries) {
        qc.setQueryData(queryKeys.cheats, {
          ...prev,
          entries: prev.entries.filter((e) => e.id !== body.id)
        });
      }
      return { prev };
    },
    onSuccess: (_d, body) => {
      clearCheatFloatDirty(body.id);
      void qc.invalidateQueries({ queryKey: queryKeys.cheats });
    },
    onError: (_e, _b, ctx) => {
      if (ctx?.prev) qc.setQueryData(queryKeys.cheats, ctx.prev);
    }
  });
}

export function useCheatAction() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Cheat" },
    mutationFn: (body: Record<string, unknown>) => sendJson("/api/cheats/action", "POST", body),
    onSuccess: (_d, body) => {
      const a = body.action;
      if (a === "reset-all" || a === "reset-group" || a === "clear-field")
        void qc.invalidateQueries({ queryKey: queryKeys.cheats });
    }
  });
}

export function useRunProbePack() {
  return useMutation({
    meta: { entity: "Probe" },
    mutationFn: (body: { packId: string; probeId?: string }) =>
      sendJson<ProbeRunResult>("/api/cheats/probe", "POST", body)
  });
}

export function useEndProbe() {
  return useMutation({
    meta: { entity: "Probe" },
    mutationFn: (body: { probeId?: string; reason?: string } = {}) =>
      sendJson("/api/cheats/probe/end", "POST", body)
  });
}

export function useCreatePlayer() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Player" },
    mutationFn: (name: string) => sendJson<PlayerDto>("/api/players", "POST", { name }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.players });
    }
  });
}

export function useSelectPlayer() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Player" },
    mutationFn: (id: number) => sendJson("/api/players/current", "PUT", { id }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.players });
      void qc.invalidateQueries({ queryKey: queryKeys.runs });
      void qc.invalidateQueries({ queryKey: queryKeys.health });
    }
  });
}

export function useSimCommand() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Simulation" },
    mutationFn: ({ path, body = {} }: { path: string; body?: unknown }) =>
      sendJson("/api/sim" + path, "POST", body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: queryKeys.sim });
      void qc.invalidateQueries({ queryKey: queryKeys.types });
      void qc.invalidateQueries({ queryKey: queryKeys.recipes });
      void qc.invalidateQueries({ queryKey: queryKeys.metrics });
      void qc.invalidateQueries({ queryKey: queryKeys.runs });
      void qc.invalidateQueries({ queryKey: queryKeys.players });
    }
  });
}

export function useResetSim() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Simulation" },
    mutationFn: () => sendJson("/api/test/reset", "POST", {}),
    onSuccess: () => {
      clearLogEvents();
      for (const key of queryKeys.allSnapshots) {
        void qc.invalidateQueries({ queryKey: key });
      }
      void qc.invalidateQueries({ queryKey: ["pvzStats"] });
      void qc.invalidateQueries({ queryKey: ["pvzActivity"] });
      void qc.invalidateQueries({ queryKey: ["pvzActivityFacts"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionSummary"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionStats"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActors"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionLedger"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActor"] });
    }
  });
}

export function useSeedPvzStatsDemo() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "PvzStats" },
    mutationFn: (playerId?: number) =>
      sendJson("/api/test/seed-pvz-stats-demo", "POST", playerId != null ? { playerId } : {}),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["pvzStats"] });
      void qc.invalidateQueries({ queryKey: ["pvzStatsChannel"] });
    }
  });
}

export function useSaveAptitudes() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Aptitudes" },
    mutationFn: (body: { playerId: number; shares: Record<string, number> }) =>
      sendJson<AptitudesState>("/api/aptitudes/allocate", "POST", body),
    onSuccess: (_data, vars) => {
      void qc.invalidateQueries({ queryKey: queryKeys.aptitudes(vars.playerId) });
    }
  });
}

export function useResetPvzStats() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "PvzStats" },
    mutationFn: (playerId: number) =>
      sendJson(`/api/pvz-stats/${playerId}/modifiers/reset`, "POST", {}),
    onSuccess: (_data, playerId) => {
      void qc.invalidateQueries({ queryKey: queryKeys.pvzStats(playerId) });
      void qc.invalidateQueries({ queryKey: ["pvzStatsChannel"] });
    }
  });
}

export function useWithdrawPvzStat() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "PvzStats" },
    mutationFn: (body: {
      playerId: number;
      sourceKind?: string;
      sourceId?: string;
      channel?: string;
      op?: string;
      pluginId?: string;
    }) => {
      const { playerId, ...rest } = body;
      return sendJson(`/api/pvz-stats/${playerId}/modifiers/withdraw`, "POST", rest);
    },
    onSuccess: (_data, vars) => {
      void qc.invalidateQueries({ queryKey: queryKeys.pvzStats(vars.playerId) });
      void qc.invalidateQueries({ queryKey: ["pvzStatsChannel"] });
    }
  });
}

export function useSeedPvzActivityDemo() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "PvzActivity" },
    mutationFn: (playerId?: number) =>
      sendJson("/api/test/seed-pvz-activity-demo", "POST", playerId != null ? { playerId } : {}),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["pvzActivity"] });
      void qc.invalidateQueries({ queryKey: ["pvzActivityFacts"] });
    }
  });
}

export function useSpawnExtraIntent() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Board" },
    mutationFn: (body: {
      typeId: number;
      row?: number;
      col?: number;
      side?: string;
      reason?: string;
      playerId?: number;
      correlationId?: string;
    }) => sendJson("/api/pvz-intent/spawn-extra", "POST", body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["pvzActivity"] });
      void qc.invalidateQueries({ queryKey: ["pvzActivityFacts"] });
    }
  });
}

/**
 * Thin debug POST for lawn inspector (W7). Path is `/api/debug/{path}`.
 * `silent: true` is for the ~1.5s board-stats poll (LawnPage) — a failure
 * still surfaces, but a poll succeeding every tick isn't a "you did
 * something" moment and would spam the toast stack (T11).
 */
export function useLawnDebugPost(options?: { silent?: boolean }) {
  return useMutation({
    meta: { entity: "Board", silent: options?.silent },
    mutationFn: (args: { path: string; body?: Record<string, unknown> }) => {
      const path = args.path.replace(/^\//, "");
      return sendJson(`/api/debug/${path}`, "POST", args.body ?? {});
    }
  });
}

function invalidateUniqueActors(
  qc: ReturnType<typeof useQueryClient>,
  playerId?: number,
  instanceId?: string
) {
  if (playerId != null) {
    void qc.invalidateQueries({ queryKey: queryKeys.uniqueActors(playerId) });
  } else {
    void qc.invalidateQueries({ queryKey: ["uniqueActors"] });
  }
  if (instanceId) {
    void qc.invalidateQueries({ queryKey: queryKeys.uniqueActor(instanceId) });
    void qc.invalidateQueries({ queryKey: queryKeys.uniqueEquipment(instanceId) });
  }
}

export function useCreateUniqueActor() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Creature" },
    mutationFn: (body: { side: string; typeId: number; playerId?: number }) =>
      sendJson<UniqueActorDto>("/api/unique/actors", "POST", body),
    onSuccess: (actor) => {
      invalidateUniqueActors(qc, actor.playerId, actor.instanceId);
    }
  });
}

export function useDeployUniqueActor() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Creature" },
    mutationFn: (args: {
      instanceId: string;
      col?: number;
      row?: number;
      correlationId?: string;
      matchKey?: string;
      playerId?: number;
    }) =>
      sendJson<UniqueActorDeployResultDto>(
        `/api/unique/actors/${encodeURIComponent(args.instanceId)}/deploy`,
        "POST",
        {
          col: args.col,
          row: args.row,
          correlationId: args.correlationId,
          matchKey: args.matchKey
        }
      ),
    onSuccess: (result, args) => {
      const pid = result.actor?.playerId ?? args.playerId;
      invalidateUniqueActors(qc, pid, args.instanceId);
    }
  });
}

export function useRetireUniqueActor() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Creature" },
    mutationFn: (args: { instanceId: string; playerId?: number }) =>
      sendJson<UniqueActorDto>(
        `/api/unique/actors/${encodeURIComponent(args.instanceId)}/retire`,
        "POST",
        {}
      ),
    onSuccess: (actor, args) => {
      invalidateUniqueActors(qc, actor.playerId ?? args.playerId, args.instanceId);
    }
  });
}

export function usePutUniqueEquipment() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Equipment" },
    mutationFn: (args: { instanceId: string; slot: string; itemId: string; playerId?: number }) =>
      sendJson<UniqueEquipmentListDto>(
        `/api/unique/actors/${encodeURIComponent(args.instanceId)}/equipment/${encodeURIComponent(args.slot)}`,
        "PUT",
        { itemId: args.itemId }
      ),
    onSuccess: (eq, args) => {
      invalidateUniqueActors(qc, args.playerId, args.instanceId);
      void qc.invalidateQueries({ queryKey: queryKeys.uniqueEquipment(eq.instanceId) });
    }
  });
}

export function useClearUniqueEquipment() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Equipment" },
    mutationFn: (args: { instanceId: string; slot: string; playerId?: number }) =>
      sendJson<UniqueEquipmentListDto>(
        `/api/unique/actors/${encodeURIComponent(args.instanceId)}/equipment/${encodeURIComponent(args.slot)}`,
        "DELETE"
      ),
    onSuccess: (eq, args) => {
      invalidateUniqueActors(qc, args.playerId, args.instanceId);
      void qc.invalidateQueries({ queryKey: queryKeys.uniqueEquipment(eq.instanceId) });
    }
  });
}

export function useAwardUniqueActorXp() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Creature" },
    mutationFn: (args: { instanceId: string; delta: number; reason?: string; playerId?: number }) =>
      sendJson<UniqueActorDto>(
        `/api/unique/actors/${encodeURIComponent(args.instanceId)}/xp`,
        "POST",
        { delta: args.delta, reason: args.reason }
      ),
    onSuccess: (actor, args) => {
      invalidateUniqueActors(qc, actor.playerId ?? args.playerId, args.instanceId);
    }
  });
}

export function useSeedRpgProgressionDemo() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Progression" },
    mutationFn: (playerId?: number) =>
      sendJson("/api/test/seed-rpg-progression-demo", "POST", playerId != null ? { playerId } : {}),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["rpgProgressionSummary"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionStats"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActors"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionLedger"] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActor"] });
    }
  });
}

export function useClearRpgDemotion() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Progression" },
    mutationFn: (args: { playerId: number; kind: string; typeId: number }) =>
      sendJson(`/api/rpg/progression/${args.playerId}/${args.kind}/${args.typeId}/clear-demotion`, "POST", {}),
    onSuccess: (_data, args) => {
      void qc.invalidateQueries({ queryKey: queryKeys.rpgProgressionSummary(args.playerId) });
      void qc.invalidateQueries({ queryKey: queryKeys.rpgProgressionStats(args.playerId) });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionActors", args.playerId] });
      void qc.invalidateQueries({ queryKey: ["rpgProgressionLedger", args.playerId] });
      void qc.invalidateQueries({
        queryKey: queryKeys.rpgProgressionActor(args.playerId, args.kind, args.typeId)
      });
    }
  });
}

function invalidateStorage(qc: ReturnType<typeof useQueryClient>) {
  void qc.invalidateQueries({ queryKey: queryKeys.storageSummary });
  void qc.invalidateQueries({ queryKey: queryKeys.storageArchives });
  void qc.invalidateQueries({ queryKey: queryKeys.runs });
  void qc.invalidateQueries({ queryKey: ["pvzActivity"] });
  void qc.invalidateQueries({ queryKey: ["pvzActivityFacts"] });
  void qc.invalidateQueries({ queryKey: ["rpgProgressionLedger"] });
}

export function useDeleteArchives() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Storage" },
    mutationFn: (uris: string[]) =>
      sendJson<StoragePurgeResult>("/api/storage/archives/delete", "POST", { uris }),
    onSuccess: () => invalidateStorage(qc)
  });
}

export function usePurgeRunCapture() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Storage" },
    mutationFn: (runIds: number[]) =>
      sendJson<StoragePurgeResult>("/api/storage/runs/purge-capture", "POST", { runIds }),
    onSuccess: () => invalidateStorage(qc)
  });
}

export function useDeleteClosedRuns() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Storage" },
    mutationFn: (runIds: number[]) =>
      sendJson<StoragePurgeResult>("/api/storage/runs/delete", "POST", { runIds }),
    onSuccess: () => invalidateStorage(qc)
  });
}

export function useTrimHotTails() {
  const qc = useQueryClient();
  return useMutation({
    meta: { entity: "Storage" },
    mutationFn: () => sendJson<{ ok: boolean }>("/api/storage/trim-tails", "POST", {}),
    onSuccess: () => invalidateStorage(qc)
  });
}
