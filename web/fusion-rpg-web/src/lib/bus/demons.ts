import { useMemo } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getJson, sendJson } from "./rest";
import type { UniqueActorDto } from "./types";

// ---- DTOs (spec-demon-core / spec-demon-summoning wire shapes) ----

export type DemonSpeciesCatalogItem = {
  speciesId: string;
  name: string;
  side: "plant" | "zombie";
  gameTypeId: number;
  demonTypeId: number;
  elementPrimary: string;
  elementSecondary?: string | null;
  rarity: "common" | "rare" | "epic" | "legendary";
  deployMode: "plant-avatar" | "hypno-ally";
  summonable: boolean;
  captureOnly: boolean;
  variants: string[];
  traitPool: string[];
};

export type DemonTraitCatalogItem = { traitId: string; name: string; blurb: string };

export type DemonCatalogDto = {
  species: DemonSpeciesCatalogItem[];
  traits: DemonTraitCatalogItem[];
};

export type DemonProfileDto = {
  instanceId: string;
  speciesId: string;
  rarity: string;
  variant: string;
  elementPrimary: string;
  elementSecondary?: string | null;
  traitIds: string[];
  origin: string;
  nickname?: string | null;
  locked: boolean;
  star: number;
  promoted: boolean;
  createdUtc: string;
  revision: number;
};

export type DemonSpecimenDto = { actor: UniqueActorDto; profile: DemonProfileDto };
export type DemonRosterDto = { playerId: number; items: DemonSpecimenDto[] };
export type DemonCodexEntryDto = { speciesId: string; state: "seen" | "discovered"; firstUtc: string; updatedUtc: string };
export type DemonCodexDto = { playerId: number; entries: DemonCodexEntryDto[] };

export type SoulBalanceDto = {
  playerId: number;
  balance: number;
  earnedTotal: number;
  spentTotal: number;
  revision: number;
  updatedUtc: string;
};

export type SummonStateDto = {
  banners: { bannerId: string; costPerPull: number; costPerTen: number; focusElement?: string | null }[];
  pity: { pullsSinceEpic: number; pullsSinceLegendary: number; epicGuaranteeAt: number; legendaryGuaranteeAt: number };
  balance: SoulBalanceDto;
};

export type SummonOutcomeDto = {
  replayed: boolean;
  specimens: DemonSpecimenDto[];
  pity: { pullsSinceEpic: number; pullsSinceLegendary: number };
  balance: SoulBalanceDto;
  discoverySouls: number;
};

// ---- Keys ----

export const demonKeys = {
  catalog: ["demonCatalog"] as const,
  roster: (playerId: number) => ["demonRoster", playerId] as const,
  codex: (playerId: number) => ["demonCodex", playerId] as const,
  summonState: (playerId: number) => ["demonSummonState", playerId] as const,
  souls: (playerId: number) => ["souls", playerId] as const
};

// ---- Queries ----

export function useDemonCatalog() {
  return useQuery({
    queryKey: demonKeys.catalog,
    queryFn: () => getJson<DemonCatalogDto>("/api/demons/catalog"),
    staleTime: Infinity // code-authored catalog: changes only on redeploy
  });
}

export type SpeciesIndexEntry = { name: string; side: "plant" | "zombie"; gameTypeId: number };

/** Shared species lookup — one memoized index instead of a copy-pasted map per page. */
export function useSpeciesIndex(): Map<string, SpeciesIndexEntry> {
  const catalog = useDemonCatalog();
  return useMemo(() => {
    const map = new Map<string, SpeciesIndexEntry>();
    for (const s of catalog.data?.species ?? [])
      map.set(s.speciesId, { name: s.name, side: s.side, gameTypeId: s.gameTypeId });
    return map;
  }, [catalog.data]);
}

export function useDemonRoster(playerId: number) {
  return useQuery({
    queryKey: demonKeys.roster(playerId),
    queryFn: () => getJson<DemonRosterDto>(`/api/demons/${playerId}`),
    enabled: playerId > 0
  });
}

export function useDemonCodex(playerId: number) {
  return useQuery({
    queryKey: demonKeys.codex(playerId),
    queryFn: () => getJson<DemonCodexDto>(`/api/demons/${playerId}/codex`),
    enabled: playerId > 0
  });
}

export function useSummonState(playerId: number) {
  return useQuery({
    queryKey: demonKeys.summonState(playerId),
    queryFn: () => getJson<SummonStateDto>(`/api/demons/${playerId}/summon-state`),
    enabled: playerId > 0
  });
}

export function useSoulBalance(playerId: number) {
  return useQuery({
    queryKey: demonKeys.souls(playerId),
    queryFn: () => getJson<SoulBalanceDto>(`/api/souls/${playerId}`),
    enabled: playerId > 0
  });
}

/** UUID v4 with a getRandomValues fallback — crypto.randomUUID is absent on non-secure origins (LAN rebind). */
export function newCorrelationId(): string {
  if (typeof crypto.randomUUID === "function") return crypto.randomUUID();
  const b = crypto.getRandomValues(new Uint8Array(16));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = [...b].map((x) => x.toString(16).padStart(2, "0")).join("");
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

// ---- Mutations ----

export function invalidateDemonQueries(qc: ReturnType<typeof useQueryClient>) {
  // Prefix keys match every player's variant of each query.
  void qc.invalidateQueries({ queryKey: ["demonRoster"] });
  void qc.invalidateQueries({ queryKey: ["demonCodex"] });
  void qc.invalidateQueries({ queryKey: ["demonSummonState"] });
  void qc.invalidateQueries({ queryKey: ["souls"] });
}

export function useSummon() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: { playerId?: number; bannerId: string; count: 1 | 10; correlationId: string }) =>
      sendJson<SummonOutcomeDto>("/api/demons/summon", "POST", req),
    onSuccess: () => invalidateDemonQueries(qc)
  });
}

export function useSetDemonNickname() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: { instanceId: string; nickname: string | null }) =>
      sendJson<DemonProfileDto>(`/api/demons/specimen/${req.instanceId}/nickname`, "POST", {
        nickname: req.nickname
      }),
    // Rename touches only the roster; codex/souls/pity can't change (the hub echo covers other tabs).
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["demonRoster"] })
  });
}

export function useSetDemonLocked() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: { instanceId: string; locked: boolean }) =>
      sendJson<DemonProfileDto>(`/api/demons/specimen/${req.instanceId}/lock`, "POST", {
        locked: req.locked
      }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["demonRoster"] })
  });
}
