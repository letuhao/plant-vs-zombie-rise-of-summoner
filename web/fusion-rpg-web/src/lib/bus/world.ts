import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getJson, sendJson, tryGetJson } from "./rest";

/**
 * The world map's bus layer (spec-world-model.md §Server). Everything the map page does goes through
 * here — no page fetches directly, so caching, invalidation, and the SIM base URL all live in one
 * place.
 *
 * **world-stage W2 (2026-09-04):** the wire DTOs used to live in `features/world/worldTypes.ts` and
 * this file imported them from there — so `contractGuard` (matching only `from "@/lib/bus`) would
 * pass a `stages/world/` component binding straight to a REST DTO. They live here now, where every
 * other domain's DTOs already do; `features/world/worldTypes.ts` re-exports them so the legacy page
 * and its tests keep compiling unchanged until Phase 4 retires that tree. This is a move, not an
 * edit — no field was renamed or narrowed.
 */

export type WorldFactionDto = {
  factionId: string;
  kind: string;
  name: string;
};

export type WorldSlotDto = {
  slotIndex: number;
  slotTypeId: string;
  element: string | null;
  state: string;
  ownerFactionId: string | null;
  guardWaveId: string | null;
  guardState: string;
  /**
   * Found missing 2026-09-04 (world-stage W4) — this is the drift the whole program keeps citing
   * as its example: on the C# DTO since L32 (`WorldDtos.cs:39`, no owner-gating, "as visible as the
   * slot itself"), present in the byte-pinned fixture, and never added here. Verified against both
   * before fixing.
   */
  structureId: string | null;
  /**
   * Found missing 2026-09-04 (world-stage W62) — the same drift class as `structureId` above: on
   * the C# DTO (`WorldDtos.cs:72`), genuinely assigned server-side (`WorldEndpoints.cs:482`, not a
   * stub like `pressureMilli`), present in the byte-pinned fixture (`null` in every current save,
   * since nothing is under construction in either golden world) — and never added to this mirror.
   * `adaptWorldSlot` had compensated by marking it permanently `Pending`, which was the wrong fix
   * for a field that was never actually missing from the wire.
   */
  constructionTurnsRemaining: number | null;
};

/**
 * A force as the viewer believes it to be. `exact` only when they stood on the ground with it — a
 * glimpse from next door reports a band, never a count.
 */
export type WorldForceDto = {
  entityId: string;
  ownerFactionId: string;
  kind: string;
  exact: boolean;
  strength: number;
  bandName: string;
  bandCeiling: number;
};

/** `LoamUpkeepBreakdownDto` (`WorldDtos.cs:102-110`) — the five operands `LoamUpkeep.For` sums, in
 * the exact order its own signature declares them. */
export type WorldLoamUpkeepBreakdownDto = {
  base: number;
  garrison: number;
  development: number;
  danger: number;
  intensityMilli: number;
  handicapMilli: number;
};

/**
 * `WorldCalendarDto` (`WorldDtos.cs:24-33`) — `TurnCalendar.Roll(world.CurrentTurn, seed)`, computed
 * server-side on every poll; the seed itself never reaches the wire. Found missing 2026-09-04
 * (world-stage W53): projected onto `WorldStateDto.Calendar` since `world-wire` W15, never added to
 * this hand-written mirror — the same class of drift every prior wave in this file has already found
 * once (`structureId`, `fractureIntensityMilli`, `upkeepBreakdown`).
 */
export type WorldCalendarDto = {
  daysPerWeek: number;
  weeksPerMonth: number;
  weekBoundary: boolean;
  monthBoundary: boolean;
  specialWeek: boolean;
  specialMonth: boolean;
  plague: boolean;
  /**
   * Meaningful on every turn, never fogged (`TurnCalendar.SeasonOf`'s own doc comment) — unlike
   * the boundary flags above, this is not blank between week boundaries. Added 2026-09-05, wiring
   * the HUD's calendar slot to sector-development's real season now that one exists (superseding
   * §8b.7's "calendar, not a season" premise, made when no season concept did).
   */
  season: number;
};

export type WorldSectorDto = {
  sectorId: string;
  typeId: string;
  climate: string | null;
  dangerBand: number;
  phase: string;
  ownerFactionId: string | null;
  stabilityMilli: number;
  pressureMilli: number;
  depletionMilli: number;
  /**
   * Found missing 2026-09-04 (world-stage W4): projected server-side since
   * `WorldEndpoints.cs:298` and present in the byte-pinned fixture (default 1000 = neutral), but
   * never added to this hand-written mirror — the same class of drift that lost `structureId` for
   * two waves. A renderer reading `sector.fractureIntensityMilli` got `undefined` until this line.
   */
  fractureIntensityMilli: number;
  developmentLevel: number;
  intel: string;
  lastSeenTurn: number;
  /** Turns since this was last seen. Zero while it is in sight. */
  intelAge: number;
  layoutX: number;
  layoutY: number;
  slots: WorldSlotDto[];
  forces: WorldForceDto[];
  /** What losing this would cost your own empire; zero for anything you do not hold. */
  lifelineCost: number;
  /** True when losing it would cut your territory in two. */
  lifeline: boolean;
  /** Whether this ground can be kept at all — terrain, visible once scouted, not only to the owner. */
  habitable: boolean;
  /** What this sector earns this turn. Owner-only; zero for anything you do not hold. */
  loamProduction: number;
  /** What this sector costs this turn. Owner-only. */
  loamUpkeep: number;
  /**
   * Found missing 2026-09-04 (world-numbers W41): projected server-side since
   * `WorldEndpoints.cs:490-497` (world-stage W10) and read by `ComputeLoamReading`'s own
   * `LoamUpkeepBreakdownBySector`, but never added to this hand-written mirror — the same class of
   * drift `fractureIntensityMilli` was found missing to above. Owner-only; every field defaults to
   * zero (`HandicapMilli` to 1000) for a sector you do not hold.
   */
  upkeepBreakdown: WorldLoamUpkeepBreakdownDto;
  /** The number an abandonment decision is actually about. Owner-only. */
  loamNet: number;
  /** The connected block of your territory this sector pools with. Owner-only; null otherwise. */
  componentId: string | null;
  componentProduction: number;
  componentUpkeep: number;
  componentNet: number;
  /** Raw stock. Owner-only; zero for anything you do not hold. */
  loamStock: number;
  /**
   * Found missing 2026-09-04 (world-hud W52's own stated premise — "never projected" — was stale):
   * `LoamPhases.EffectiveCapacity(sector)` is genuinely computed and assigned server-side
   * (`WorldEndpoints.cs:456-458`), owner-gated the same way as `wardenBindingId` below, never
   * mirrored here. `adaptWorldSector` had compensated by marking the stock denominator permanently
   * `Pending`, hiding a real, wired value.
   */
  loamCapacity: number;
  /** The pooled stock of this sector's whole component. Owner-only. */
  componentStock: number;
  /** True when, if nothing changes, the engine releases this ground outright next turn. Owner-only. */
  willReleaseNextTurn: boolean;
  /**
   * Found missing 2026-09-04 (world-stage W63) — the same drift class as `structureId`/
   * `constructionTurnsRemaining`: real on the C# DTO (`WorldDtos.cs:190`), genuinely assigned and
   * already owner-gated server-side (`WorldEndpoints.cs:451-452`, `null` unless this faction owns
   * the sector), never added to this mirror. `adaptWorldSector` had compensated by marking it
   * permanently `Pending`, hiding a real, wired value.
   */
  wardenBindingId: string | null;
  /**
   * Found missing 2026-09-04 (world-stage W63), identical drift: real (`WorldDtos.cs:197`),
   * owner-gated the same way as `wardenBindingId` (`WorldEndpoints.cs:454-455`), never mirrored.
   */
  neglectedTurns: number;
};

export type WorldLaneDto = {
  laneId: string;
  fromSectorId: string;
  toSectorId: string;
  typeId: string;
  length: number;
  width: number;
  hazardMilli: number;
  wardLevel: number;
  state: string;
};

export type WorldEntityMemberDto = {
  instanceId: string | null;
  speciesId: string;
  level: number;
  hp: number;
  wounds: number;
};

export type WorldEntityDto = {
  entityId: string;
  kind: string;
  ownerFactionId: string;
  atSectorId: string | null;
  onLaneId: string | null;
  onLaneTowardSectorId: string | null;
  laneProgressMilli: number;
  stance: string;
  movementRemaining: number;
  routed: boolean;
  members: WorldEntityMemberDto[];
};

export type WorldStateDto = {
  worldId: string;
  templateId: string;
  currentTurn: number;
  factions: WorldFactionDto[];
  sectors: WorldSectorDto[];
  lanes: WorldLaneDto[];
  entities: WorldEntityDto[];
  calendar: WorldCalendarDto;
  /**
   * Found missing 2026-09-04 (alongside `calendar`, same drift class): projected since `world-wire`
   * W16 (`WorldDtos.cs:338`), never added to this hand-written mirror. Every sector a dowser has
   * confirmed holds a loam source this turn — deliberately separate from a sector's own `intel`.
   */
  prospectedSectorIds: string[];
};

/** One line of a turn report, as the map plays it back. */
export type WorldTurnEntryDto = {
  /** Where it happened, when it happened anywhere. Absent means "nowhere in particular". */
  sectorId?: string | null;

  phase: string;
  kind: string;
  subject: string;
  detail: string;
};

export type WorldTurnCommandDto = {
  commanderId: string;
  commandId: string;
  kind: string;
  entityId?: string | null;
  sectorId?: string | null;
  /** Null for anything a person filed — the player never explains themselves. */
  reason?: string | null;
};

export type WorldTurnReportDto = {
  turn: number;
  stateHash: string;
  phases: string[];
  entries: WorldTurnEntryDto[];
  /** Survives a trim that empties `entries`: commands are the save and are never trimmed. */
  commands?: WorldTurnCommandDto[];
};

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
  /**
   * Found missing 2026-09-04 (world-stage W66) — real on the C# DTO since world-commands' own W22
   * (`WorldDtos.cs:420`, `WorldCommandRequest.Stance`), never mirrored here. The posture a `stance`
   * order asks for: march, scout, hold, or (since `world-commands` W30) dowse.
   */
  stance?: string | null;
  /** Found missing alongside `stance` (`WorldDtos.cs:426`) — a `sustain` order's whole-loam spend,
   * `long` end to end per W22, never `int`. */
  amount?: number | null;
  /** Found missing alongside `stance` (`WorldDtos.cs:429`) — a `build` order's structure choice. */
  structureId?: string | null;
};

export type WorldCommandResultDto = {
  commandId: string;
  ok: boolean;
  reason: string;
  replayed: boolean;
};

/**
 * Rules, not state — no world id, no viewer, no fog (world-stage W17). Found missing entirely from
 * this mirror 2026-09-04 (world-stage W69): `GET /api/world/catalog` has existed since W17, but
 * nothing on the TS side ever read it.
 */
export type WorldStructureDto = {
  structureId: string;
  name: string;
  kind: string;
  requiredSlotKind: string;
  /** Whole loam units, despite `StructureDef.CostMilli`'s own name server-side — named `cost` here
   * on purpose, matching the DTO's own `Cost` field. */
  cost: number;
  yieldMultiplierMilli: number;
  buildTurns: number;
  capacityBonus: number;
};

export type WorldSlotTypeDto = {
  slotTypeId: string;
  name: string;
  kind: string;
  buildable: boolean;
  yields: boolean;
};

export type WorldStrengthBandDto = {
  index: number;
  name: string;
  floor: number;
  ceiling: number;
  midpoint: number;
};

export type WorldLaneTypeDto = {
  laneTypeId: string;
  name: string;
  costMultiplierMilli: number;
  carriesSupply: boolean;
  carriesPressure: boolean;
  oneWay: boolean;
  gated: boolean;
  ley: boolean;
};

export type WorldCatalogDto = {
  structures: WorldStructureDto[];
  slotTypes: WorldSlotTypeDto[];
  strengthBands: WorldStrengthBandDto[];
  laneTypes: WorldLaneTypeDto[];
  /** `LoamPolicy.WaystationRangeHops` (world-stage W69) — read from the tuning row, never a
   * literal on this side of the wire either. */
  waystationRangeHops: number;
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
  turn: (worldId: string, turn: number) => ["world", "turn", worldId, turn] as const,
  catalog: () => ["world", "catalog"] as const
};

/** Rules, not state — no world id needed, so this never gates on one being selected. */
export function useWorldCatalog() {
  return useQuery({
    queryKey: worldKeys.catalog(),
    queryFn: () => getJson<WorldCatalogDto>("/api/world/catalog")
  });
}

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
