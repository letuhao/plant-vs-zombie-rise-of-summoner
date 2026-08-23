/**
 * Wire shapes for the world map — the TypeScript mirror of `FusionRpg.Contracts/WorldDtos.cs`.
 *
 * The checked-in fixture is asserted against the live DTO by an E2E test, so if these drift the
 * build that catches it is the server's, not a runtime surprise in the browser.
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
  /** The number an abandonment decision is actually about. Owner-only. */
  loamNet: number;
  /** The connected block of your territory this sector pools with. Owner-only; null otherwise. */
  componentId: string | null;
  componentProduction: number;
  componentUpkeep: number;
  componentNet: number;
  /** Raw stock. Owner-only; zero for anything you do not hold. */
  loamStock: number;
  /** The pooled stock of this sector's whole component. Owner-only. */
  componentStock: number;
  /** True when, if nothing changes, the engine releases this ground outright next turn. Owner-only. */
  willReleaseNextTurn: boolean;
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
