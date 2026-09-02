export type StatMod = {
  hpPercent: number;
  hpFlat: number;
  attackPercent: number;
  attackFlat: number;
  defensePercent: number;
  defenseFlat: number;
};

export type StatsConfig = {
  plants: StatMod;
  zombies: StatMod;
  logDamage: boolean;
  applyStats: boolean;
};

export type HealthDto = {
  ok: boolean;
  injectorConnected: boolean;
  lastHeartbeatUtc: string | null;
  simEnabled?: boolean;
  source?: string;
  currentPlayerId?: number;
  ingestQueued?: number;
  lastFlushMs?: number;
};

export type PlayerDto = {
  id: number;
  name: string;
  createdUtc: string;
  /** "The whole save"'s own root (spec-world-seed.md, T5.1) — display only. The server's C# `long`
   * can exceed Number.MAX_SAFE_INTEGER, so this may round; nothing client-side derives a roll from
   * it — every real derivation (`WorldSeed.DeriveRollSeed`) stays server-side, exact. */
  worldSeed: number;
};

export type PlayersListDto = {
  items: PlayerDto[];
  currentPlayerId: number;
};

export type SimEntity = {
  side: string;
  ptr: string;
  type: number;
  hp: number;
  maxHp: number;
  attack: number;
  armor: number;
  armorMax: number;
  col: number;
  row: number;
};

export type SimMower = {
  ptr: string;
  type: number;
  typeName: string;
  row: number;
  started: boolean;
  dead: boolean;
};

export type SimState = {
  levelName?: string | null;
  matchKey?: string | null;
  wave?: number;
  maxWave?: number;
  nextPlant: number;
  nextZombie: number;
  plants: SimEntity[];
  zombies: SimEntity[];
  mowers?: SimMower[];
  applied: string[];
};

export type TypeItem = {
  game?: string;
  side: string;
  type: number;
  typeName?: string | null;
  displayName?: string | null;
  sampleJson?: string | null;
  hpBase?: number | null;
  maxHpBase?: number | null;
  attackBase?: number | null;
  armorBase?: number | null;
  armorMaxBase?: number | null;
  seenCount: number;
  killedCount: number;
  firstSeenUtc?: string | null;
  lastSeenUtc?: string | null;
};

export type RecipeItem = {
  parentA: number;
  parentAName?: string | null;
  parentB: number;
  parentBName?: string | null;
  result: number;
  resultName?: string | null;
};

export type SpawnStatItem = {
  id: number;
  runId: number;
  ptr: string;
  side: string;
  type: number;
  source: string;
  capturedUtc: string;
  stats?: unknown;
};

export type EventEnvelope = {
  id?: number;
  t: string;
  game: string;
  kind: string;
  matchKey?: string | null;
  playerId?: number | null;
  runId?: number | null;
  payload?: unknown;
};

export type MetricItem = { name: string; value: number; ts: string };

export type RunItem = {
  id: number;
  playerId?: number;
  matchKey?: string | null;
  startedUtc: string;
  endedUtc?: string | null;
  levelName?: string | null;
  result?: string | null;
  mowersUsed?: number;
  plantsPlanted?: number;
  plantsDied?: number;
  zombiesKilled?: number;
  summary?: unknown;
  levelType?: string | null;
  boardLevel?: number | null;
  modifiers?: unknown;
  archiveUri?: string | null;
};

export type HubStatus = "off" | "on" | "err";

export type CheatEntry = {
  id: string;
  kind: string;
  enabled: boolean;
  /** Absent when unset — do not invent 0. */
  floatValue?: number;
  isSet?: boolean;
};

export type CheatCatalogItem = {
  type: number;
  typeName?: string;
  displayName?: string;
  spawnOk?: boolean | null;
};

export type CheatSnapshot = {
  menuEnabled?: boolean;
  revision?: number;
  updatedAt?: string;
  persist?: boolean;
  emitProof?: boolean;
  localOverride?: boolean;
  selectedPtr?: string;
  selectedSide?: string;
  spawnCol?: number;
  spawnRow?: number;
  catalogPlants?: number;
  catalogZombies?: number;
  note?: string;
  activeProbeId?: string;
  activePackId?: string;
  entries?: CheatEntry[];
  mods?: { id: string; channel?: string; op?: string; value?: number; enabled?: boolean }[];
  catalog?: {
    plants?: CheatCatalogItem[];
    zombies?: CheatCatalogItem[];
  };
};

export type CheatSchemaField = {
  id: string;
  role: string;
  kind: string;
  channel: string;
  op: string;
  displayDefault: number;
  toggleDefault: boolean;
  groupPrefix: string;
};

export type CheatSchemaDto = {
  fields: CheatSchemaField[];
};

export type PvzStatContribution = {
  channel: string;
  pluginId: string;
  sourceKind: string;
  sourceId: string;
  op: string;
  value: number;
  priority: number;
  detailJson?: string | null;
};

export type PvzStatsChannelSummary = {
  channel: string;
  final: number;
  sourceCount: number;
};

export type PvzStatsSheet = {
  playerId: number;
  revision: number;
  updatedAt?: string;
  channels: PvzStatsChannelSummary[];
};

export type PvzStatsChannelDetail = {
  playerId: number;
  revision: number;
  channel: string;
  final: number;
  contributions: PvzStatContribution[];
};

export type PvzActivityRollup = {
  playerId: number;
  revision: number;
  updatedAt?: string;
  matchesStarted: number;
  matchesEnded: number;
  victories: number;
  defeats: number;
  zombiesKilled: number;
  plantsLost: number;
  plantsPlaced: number;
  extraSpawnsFired: number;
};

export type PvzActivityFact = {
  id: number;
  playerId: number;
  runId?: number | null;
  t: string;
  kind: string;
  pluginId: string;
  sourceKind: string;
  sourceId: string;
  payloadJson?: string | null;
  matchKey?: string | null;
  dedupeKey?: string | null;
};

export type PvzActivityFactsPage = {
  playerId: number;
  revision: number;
  items: PvzActivityFact[];
};

export type RpgActorProgression = {
  playerId: number;
  kind: string;
  typeId: number;
  typeName?: string | null;
  displayName?: string | null;
  /** Promoted PlantInfo/ZombieInfo.info */
  almanacInfo?: string | null;
  /** Promoted ZombieInfo.introduce */
  almanacIntroduce?: string | null;
  /** Promoted PlantInfo.cost */
  almanacCost?: string | null;
  level: number;
  xp: number;
  xpToNext: number;
  highestLevel: number;
  demotionCount: number;
  revision: number;
  updatedAt: string;
  curveFirst: number;
  curveStep: number;
};

export type RpgProgressionSummary = {
  playerId: number;
  player: RpgActorProgression | null;
  plantActorCount: number;
  zombieActorCount: number;
  highestPlantLevel: number;
  highestZombieLevel: number;
  topPlants: RpgActorProgression[];
  topZombies: RpgActorProgression[];
};

export type RpgProgressionList = {
  playerId: number;
  items: RpgActorProgression[];
  total: number;
  limit: number;
  offset: number;
};

export type RpgXpLedgerEntry = {
  id: number;
  playerId: number;
  kind: string;
  typeId: number;
  typeName?: string | null;
  runId: number;
  t: string;
  delta: number;
  reason: string;
  activityFactId?: number | null;
  levelBefore: number;
  xpBefore: number;
  levelAfter: number;
  xpAfter: number;
  demotionBefore: number;
  demotionAfter: number;
  payloadJson?: string | null;
};

export type RpgXpLedgerPage = {
  playerId: number;
  items: RpgXpLedgerEntry[];
  limit: number;
  nextAfterId?: number | null;
};

export type RpgProgressionStats = {
  playerId: number;
  xpByReason: { reason: string; sumDelta: number; count: number }[];
  plantLevels: { level: number; count: number }[];
  zombieLevels: { level: number; count: number }[];
  recentDeltas: { t: string; delta: number; reason: string }[];
};

export type ProbePackDto = {
  id: string;
  label: string;
  hint: string;
  expectedKinds: string[];
  steps?: unknown[];
};

export type ProbeRunResult = {
  probeId: string;
  packId: string;
  label?: string;
  hint?: string;
  expectedKinds: string[];
  steps?: number;
};

export type StorageSummary = {
  archiveCount: number;
  closedRunsStillHot: number;
  openRuns: number;
  activityOverTail: boolean;
  xpOverTail: boolean;
};

export type StorageArchiveItem = {
  uri: string;
  kind: string;
  runId?: number | null;
  createdUtc: string;
};

export type StoragePurgeResult = {
  deleted: number;
  refused: number;
};

/** Cold UniqueActor specimen (W4+; lawn inspector observe W7-B). */
export type UniqueActorDto = {
  instanceId: string;
  playerId: number;
  side: string;
  typeId: number;
  phase: string;
  level: number;
  xp: number;
  matchKey?: string | null;
  lastPtr?: string | null;
  deployCorrelationId?: string | null;
  revision: number;
  createdAt?: string;
  updatedAt?: string;
};

export type UniqueActorListDto = {
  playerId: number;
  items: UniqueActorDto[];
};

export type UniqueActorDeployResultDto = {
  ok: boolean;
  reason: string;
  queued: boolean;
  correlationId: string;
  actor?: UniqueActorDto | null;
};

export type UniqueEquipmentSlotDto = {
  slot: string;
  itemId: string;
};

export type UniqueEquipmentListDto = {
  instanceId: string;
  phase: string;
  items: UniqueEquipmentSlotDto[];
  modsJson: string;
};

export type RelicDto = {
  id: string;
  name: string;
  rarity: number;
  slot: string;
  description: string;
  effectId: string;
};

export type RelicCatalogListDto = {
  items: RelicDto[];
};

export const emptyMod = (): StatMod => ({
  hpPercent: 1,
  hpFlat: 0,
  attackPercent: 1,
  attackFlat: 0,
  defensePercent: 1,
  defenseFlat: 0
});

/** spec-aptitude-allocation-surface.md — GET/POST /api/aptitudes. Commander scope only; `shares` always
 * carries all twelve aptitude ids (zero if unset, never omitted). */
export type AptitudesState = {
  theta: number;
  budget: number;
  spent: number;
  withinBudget: boolean;
  shares: Record<string, number>;
};
