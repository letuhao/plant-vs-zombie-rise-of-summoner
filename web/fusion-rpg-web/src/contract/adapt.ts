import type { ContractRowDto } from "@/lib/bus/contracts";
import type { DemonProfileDto } from "@/lib/bus/demons";
import type {
  ArmouryPageDto,
  ArmouryRowDto,
  ChannelDeltaDto,
  CombinationRowDto,
  DisplayLineDto,
  ItemAssignmentDto,
  ItemCardDto,
  ItemCompareDto,
  ItemEquipOutcomeDto,
  ItemSurfaceStatusDto,
  WorkbenchCostDto,
  WorkbenchOutcomeDto,
  WorkbenchSocketDto
} from "@/lib/bus/items";
import type { RelicDto, RunItem, UniqueActorDto } from "@/lib/bus/types";
import type {
  WorldEntityDto,
  WorldEntityMemberDto,
  WorldForceDto,
  WorldLaneDto,
  WorldSectorDto,
  WorldSlotDto,
  WorldStateDto,
  WorldTurnEntryDto
} from "@/lib/bus/world";
import { absent, known, pendingWithReason, type Pending } from "./pending";
import type {
  ActorPhase,
  ActorView,
  ArmouryPageView,
  ArmouryRowView,
  ChannelDeltaView,
  CombinationShape,
  CombinationState,
  CombinationView,
  CommanderListRow,
  CommanderListView,
  ComparePayloadView,
  ContainerView,
  ContractView,
  DisplayLine,
  DominanceVerdict,
  EquipAssignmentView,
  EquipOutcomeView,
  ForceView,
  IntelState,
  ItemRoleId,
  ItemSurfaceId,
  ItemSurfaceState,
  LaneView,
  LegionMemberView,
  LegionPosition,
  LegionView,
  Magnitude,
  Rarity,
  RenderStrategy,
  RunResult,
  RunView,
  SectorView,
  SetTierView,
  SetView,
  SlotView,
  SocketCellView,
  SocketsView,
  SourceKind,
  SurfaceStatusView,
  TurnEventView,
  UnitClass,
  UnitClassGroupView,
  WorkbenchCostView,
  WorkbenchOutcomeView,
  WorkbenchSocketView,
  WorkbenchVerb
} from "./types";

/**
 * The DTO→view adapter (T4). Filling a field later touches one file: this
 * one. No component, test fixture shape, or layer changes when a `pending`
 * becomes `known` (game-gui-map.md's contract section).
 *
 * Player-facing pending copy lives in `PLAYER_PENDING` — the UI renders these
 * strings verbatim (`pendingCopyGuard.ts` enforces player vocabulary).
 */
export const PLAYER_PENDING = {
  displayName: "Full name coming soon",
  xpToNext: "Next level isn't shown yet",
  channelSummary: "Stats aren't ready yet",
  elementTyping: "Element typing isn't ready yet",
  shieldStack: "Shield details aren't ready yet",
  equipSlots: "Equipment slots aren't ready yet",
  runSummary: "Run summary isn't ready yet",
  relicImplicit: "Equipping works — the bonus size isn't shown yet",
  contractDisplayName: "Species name isn't ready yet",
  summonerLevel: "Summoner rank isn't tracked yet",
  // world-stage W5 — every reason names the mechanic in the player's own words, not the field.
  loamCapacity: "How much this ground can hold isn't shown yet",
  lifelineInfo: "Ask to see what holding this ground protects",
  wardenBinding: "Whether a warden is bound here isn't shown yet",
  neglectedTurns: "How long this ground has gone untended isn't shown yet",
  gateKey: "What opens this road isn't shown yet",
  constructionProgress: "How much longer this will take isn't shown yet",
  memberRole: "Whether this one carries supply or fights isn't shown yet",
  legionSupply: "How much this legion is carrying isn't shown yet",
  legionCapacity: "How much this legion can carry isn't shown yet",
  legionBurn: "How fast this legion eats through its supply isn't shown yet",
  legionRunway: "How many turns of supply remain isn't shown yet",
  turnEventSentence: "The play-by-play for this turn isn't translated yet",
  // item module 20 — each reason names the thing the player is missing, not the field behind it.
  itemRole: "Which slot this goes in isn't shown yet",
  itemFrame: "Which kind of body this was made for isn't shown yet",
  itemRequirements: "What you need to use this isn't shown yet",
  itemAffixes: "This one's rolled bonuses aren't shown yet",
  itemEnhancement: "How far this has been strengthened isn't shown yet",
  itemSockets: "Its sockets aren't shown yet",
  itemSet: "Whether this belongs to a set isn't shown yet",
  itemGrantedAction: "Any move this teaches isn't shown yet",
  itemFooter: "Roll quality and salvage value aren't shown yet",
  itemBaseStats: "Its base numbers aren't shown yet",
  itemCompare: "Side-by-side numbers for these two aren't shown yet"
} as const;

/**
 * ssot-rarity.md §3.3's ten rungs — ordinal, display name, pip count, and the colour token T7
 * generates from the same table. Three channels for one fact (pips, name, colour), because colour
 * alone is not readable to everyone.
 *
 * A mirror of the ladder, never a second ladder: ids and ordinals are the shipped ones, and the
 * colours are `--color-rarity-*` tokens rather than hexes copied out of the document.
 */
const RARITY_LADDER: Rarity[] = [
  { id: "chaff", ordinal: 10, display: "Chaff", colour: "var(--color-rarity-chaff)", pips: 1 },
  { id: "sprout", ordinal: 20, display: "Sprout", colour: "var(--color-rarity-sprout)", pips: 2 },
  { id: "grafted", ordinal: 30, display: "Grafted", colour: "var(--color-rarity-grafted)", pips: 3 },
  { id: "cultivated", ordinal: 40, display: "Cultivated", colour: "var(--color-rarity-cultivated)", pips: 4 },
  { id: "fused", ordinal: 50, display: "Fused", colour: "var(--color-rarity-fused)", pips: 5 },
  { id: "chimeric", ordinal: 60, display: "Chimeric", colour: "var(--color-rarity-chimeric)", pips: 6 },
  { id: "heirloom", ordinal: 70, display: "Heirloom", colour: "var(--color-rarity-heirloom)", pips: 7 },
  { id: "firstseed", ordinal: 80, display: "Firstseed", colour: "var(--color-rarity-firstseed)", pips: 8 },
  { id: "sunwoven", ordinal: 90, display: "Sunwoven", colour: "var(--color-rarity-sunwoven)", pips: 9 },
  { id: "almanac", ordinal: 100, display: "Almanac", colour: "var(--color-rarity-almanac)", pips: 10 }
];

/**
 * By the rung id the wire sends, falling back to the ordinal when the id is unknown to this build —
 * an unrecognised rung renders as its nearest known one rather than as a blank swatch, and a row
 * with neither still renders (as the lowest rung) rather than disappearing from the list.
 */
function rarityFromWire(id: string, ordinal: number): Rarity {
  const byId = RARITY_LADDER.find((r) => r.id === id);
  if (byId) return byId;
  const byOrdinal = [...RARITY_LADDER].reverse().find((r) => ordinal >= r.ordinal);
  return byOrdinal ?? RARITY_LADDER[0]!;
}

function toActorPhase(phase: string): ActorPhase {
  switch (phase) {
    case "ActiveBound":
    case "ActiveUnbound":
    case "Retired":
    case "Idle":
      return phase;
    default:
      return "Idle";
  }
}

export function adaptActor(dto: UniqueActorDto): ActorView {
  return {
    instanceId: dto.instanceId,
    playerId: dto.playerId,
    side: dto.side === "zombie" ? "zombie" : "plant",
    typeId: dto.typeId,
    displayName: pendingWithReason(PLAYER_PENDING.displayName),
    phase: toActorPhase(dto.phase),
    level: dto.level,
    xp: dto.xp,
    xpToNext: pendingWithReason(PLAYER_PENDING.xpToNext),
    revision: dto.revision,
    channelSummary: pendingWithReason(PLAYER_PENDING.channelSummary),
    elementTyping: pendingWithReason(PLAYER_PENDING.elementTyping),
    shieldStack: pendingWithReason(PLAYER_PENDING.shieldStack),
    equipSlots: pendingWithReason(PLAYER_PENDING.equipSlots)
  };
}

function toRunResult(result: string | null | undefined): RunResult {
  switch (result) {
    case "victory":
    case "defeat":
    case "abandoned":
      return result;
    default:
      return "unknown";
  }
}

export function adaptRun(dto: RunItem): RunView {
  return {
    id: dto.id,
    levelName: dto.levelName ? { state: "known", value: dto.levelName } : absent(),
    result: toRunResult(dto.result),
    startedUtc: dto.startedUtc,
    endedUtc: dto.endedUtc ?? undefined,
    zombiesKilled: dto.zombiesKilled === undefined ? absent() : { state: "known", value: dto.zombiesKilled },
    plantsLost: dto.plantsDied === undefined ? absent() : { state: "known", value: dto.plantsDied },
    summary: dto.summary === undefined || dto.summary === null
      ? absent()
      : pendingWithReason(PLAYER_PENDING.runSummary)
  };
}

/**
 * T14's four seed relics use only the first four rungs of the real ten-rung ladder
 * (`docs/architecture/item/ssot-rarity.md` §3.3) — colours and pip counts are the
 * ladder's own, generated into `--color-rarity-*` (T7), not invented here.
 *
 * A slice of `RARITY_LADDER` above rather than a second copy of its first four rows: item module
 * 20 needed all ten, and two tables that agree today are two tables that can disagree later.
 */
const RELIC_RARITY_RUNGS = 4;

function rarityFromRelicTier(tier: number): Rarity {
  const clamped = Math.min(Math.max(Math.trunc(tier), 1), RELIC_RARITY_RUNGS);
  return RARITY_LADDER[clamped - 1]!;
}

function toSlotNoun(slot: string): string {
  return slot.length > 0 ? `Relic · ${slot[0]!.toUpperCase()}${slot.slice(1)}` : "Relic";
}

/**
 * Relics are the Container entity's "item" kind (docs/design/README.md §6) — not a
 * separate rung. Most of `ContainerView`'s richer blocks (affixes, sockets, sets,
 * enhancement) genuinely don't apply to this small, real, seeded catalog (T14's honest
 * scoping note): they're `absent`, not faked. `implicit` is `pending` rather than
 * `absent` — the relic's granted effect is real (verifiable via the equip API's
 * `mods_json`), just not yet expressible as a formatted `DisplayLine` magnitude.
 */
export function adaptRelic(dto: RelicDto): ContainerView {
  return {
    instanceId: dto.id,
    kind: "item",
    header: {
      name: dto.name,
      rarity: rarityFromRelicTier(dto.rarity),
      baseTypeAndClassNoun: toSlotNoun(dto.slot)
    },
    requirements: absent(),
    baseStats: [],
    implicit: pendingWithReason(PLAYER_PENDING.relicImplicit),
    affixes: absent(),
    enhancement: absent(),
    sockets: absent(),
    set: absent(),
    grantedAction: absent(),
    flavour: dto.description,
    footer: absent()
  };
}

export function adaptContract(row: ContractRowDto, profile: DemonProfileDto): ContractView {
  return {
    instanceId: row.instanceId,
    speciesId: profile.speciesId,
    rarity: profile.rarity,
    bound: row.bound,
    loyalty: row.loyalty,
    rank: row.rank,
    personality: row.personality,
    upkeepPerDay: row.upkeepPerDay,
    deployable: row.deployable,
    displayName: pendingWithReason(PLAYER_PENDING.contractDisplayName)
  };
}

/** Every `pending` reason must be non-empty — the check T4's guard proves in tests. */
export function pendingReason<T>(p: Pending<T>): string | null {
  return p.state === "pending" ? p.reason : null;
}

type CommanderListRowDto = {
  id: string;
  displayName: string;
  isDefault: boolean;
  activeAuraId: string | null;
  activeAuraName: string | null;
  locationStub: string | null;
  legionStub: string | null;
};

type CommanderListResponseDto = {
  defaultLawnCommanderId: string;
  commanders: CommanderListRowDto[];
};

/** Maps a commander list row into ActorView for the shared ActorPanel commander role. */
export function adaptCommanderSheet(row: CommanderListRow, playerId: number): ActorView {
  return {
    instanceId: row.id,
    playerId,
    side: "plant",
    typeId: 0,
    displayName: known(row.displayName),
    phase: "Idle",
    level: 1,
    xp: 0,
    xpToNext: pendingWithReason(PLAYER_PENDING.xpToNext),
    revision: 0,
    channelSummary: pendingWithReason(PLAYER_PENDING.channelSummary),
    elementTyping: pendingWithReason(PLAYER_PENDING.elementTyping),
    shieldStack: pendingWithReason(PLAYER_PENDING.shieldStack),
    equipSlots: pendingWithReason(PLAYER_PENDING.equipSlots)
  };
}

export function adaptCommanderList(dto: CommanderListResponseDto): CommanderListView {
  return {
    defaultLawnCommanderId: dto.defaultLawnCommanderId,
    commanders: dto.commanders.map(
      (row): CommanderListRow => ({
        id: row.id,
        displayName: row.displayName,
        isDefault: row.isDefault,
        activeAuraId: row.activeAuraId,
        activeAuraName: row.activeAuraName,
        locationStub: row.locationStub,
        legionStub: row.legionStub
      })
    )
  };
}

// ===========================================================================
// world-stage W5 — adaptWorld* against the byte-pinned fixture (spec-world-contract.md §5).
// Pure functions; no loam number is derived here (spec-loam-fe.md's own rule) — every reading is
// carried straight from the wire, only wrapped with its unit family.
// ===========================================================================

function toIntelState(intel: string): IntelState {
  switch (intel) {
    case "Unknown":
    case "Rumored":
    case "Scouted":
    case "Watched":
      return intel;
    default:
      // Defensive only — the wire is a plain string at the type level. An unrecognised value is
      // treated as the least-informed state rather than thrown, matching fog's own direction: when
      // in doubt, show less, never more.
      return "Unknown";
  }
}

/**
 * `dto.intel` is the only field this adapter treats as meaningful for deciding what a zero *means*
 * — an unseen sector (`WorldEndpoints.cs:271-277`) serialises every other field at its record
 * default, so nothing here infers "unknown" from an empty string or a zero. Every reading below is
 * a straight pass-through of the wire value; the caller reads `intel` to know whether to trust it.
 */
export function adaptWorldSector(
  dto: WorldSectorDto,
  options?: { lifelinesRequested?: boolean }
): SectorView {
  const lifelinesRequested = options?.lifelinesRequested ?? false;
  return {
    sectorId: dto.sectorId,
    typeId: dto.typeId,
    climate: dto.climate,
    ownerFactionId: dto.ownerFactionId,
    intel: toIntelState(dto.intel),
    intelAge: dto.intelAge,
    phase: dto.phase,
    dangerBand: { unit: "count", value: dto.dangerBand },
    developmentLevel: { unit: "count", value: dto.developmentLevel },
    stability: { unit: "perMilleRatio", op: "flat", value: dto.stabilityMilli },
    // Found and fixed 2026-09-04 (world-inspector W63): `pressureMilli` was already on the wire DTO
    // mirror but never read here — `GroundBlock.tsx` had compensated with a hard-coded Pending line.
    pressure: { unit: "perMilleRatio", op: "flat", value: dto.pressureMilli },
    // `op: "absolute"` — see the module comment on SectorView in types.ts. The wire value passes
    // straight through; no delta-from-1000 arithmetic here, which would just move the derived-in-
    // TypeScript problem `spec-loam-fe.md` forbids one line over.
    fractureIntensity: { unit: "perMilleRatio", op: "absolute", value: dto.fractureIntensityMilli },
    habitable: dto.habitable,
    layoutX: dto.layoutX,
    layoutY: dto.layoutY,
    loam: {
      production: { unit: "loamUnits", value: dto.loamProduction },
      upkeep: { unit: "loamUnits", value: dto.loamUpkeep },
      net: { unit: "loamUnits", value: dto.loamNet },
      stock: { unit: "loamUnits", value: dto.loamStock },
      // Found and fixed 2026-09-04 (world-hud W52's own "never projected" premise was stale):
      // `LoamPhases.EffectiveCapacity` is genuinely assigned server-side (`WorldEndpoints.cs:456-458`).
      capacity: known<Magnitude>({ unit: "loamUnits", value: dto.loamCapacity }),
      upkeepBreakdown: {
        base: { unit: "loamUnits", value: dto.upkeepBreakdown.base },
        garrison: { unit: "loamUnits", value: dto.upkeepBreakdown.garrison },
        development: { unit: "loamUnits", value: dto.upkeepBreakdown.development },
        danger: { unit: "loamUnits", value: dto.upkeepBreakdown.danger },
        intensityMilli: { unit: "perMilleRatio", op: "absolute", value: dto.upkeepBreakdown.intensityMilli },
        handicapMilli: { unit: "perMilleRatio", op: "absolute", value: dto.upkeepBreakdown.handicapMilli }
      }
    },
    component: {
      componentId: dto.componentId,
      production: { unit: "loamUnits", value: dto.componentProduction },
      upkeep: { unit: "loamUnits", value: dto.componentUpkeep },
      net: { unit: "loamUnits", value: dto.componentNet },
      stock: { unit: "loamUnits", value: dto.componentStock }
    },
    willReleaseNextTurn: dto.willReleaseNextTurn,
    // Opt-in server sweep: the wire always sends a number/bool (0/false when not requested), which
    // is indistinguishable from "the real answer is zero" — so whether the caller asked for
    // `?lifelines=true` is what decides `known` vs `pending` here, not the value itself.
    // Found and fixed 2026-09-04 (world-numbers W48): `LifelineCost` is a march-cost delta from
    // `ReconnectionCost.For`/`AllPairsCost` (`Topology/ReconnectionCost.cs:36-70`) — the increase in
    // total travel cost across surviving sector pairs if this one is lost — never a loam amount and
    // never a sector count, despite its name reading like the latter. `count` is the honest unit:
    // an abstract magnitude, not one of the twelve/thirteen named classes' own semantics.
    lifelineCost: lifelinesRequested
      ? known<Magnitude>({ unit: "count", value: dto.lifelineCost })
      : pendingWithReason(PLAYER_PENDING.lifelineInfo),
    lifeline: lifelinesRequested ? known(dto.lifeline) : pendingWithReason(PLAYER_PENDING.lifelineInfo),
    // Found and fixed 2026-09-04 (world-inspector W63): both fields are real, owner-gated DTO
    // fields (`WorldEndpoints.cs:451-455`) — no warden-binding mechanic exists yet (`world-confirms`,
    // Phase 4), so `known(null)` today is the honest current answer ("none bound"), not a gap.
    wardenBindingId: known(dto.wardenBindingId),
    neglectedTurns: known<Magnitude>({ unit: "count", value: dto.neglectedTurns })
  };
}

export function adaptWorldLane(dto: WorldLaneDto): LaneView {
  return {
    laneId: dto.laneId,
    fromSectorId: dto.fromSectorId,
    toSectorId: dto.toSectorId,
    typeId: dto.typeId,
    length: { unit: "count", value: dto.length },
    width: { unit: "count", value: dto.width },
    hazard: { unit: "perMilleRatio", op: "flat", value: dto.hazardMilli },
    wardLevel: { unit: "count", value: dto.wardLevel },
    state: dto.state,
    gateKeyId: pendingWithReason(PLAYER_PENDING.gateKey)
  };
}

/**
 * `constructionTurnsRemaining`: found and fixed 2026-09-04 (world-stage W62) — genuinely on the
 * wire and assigned server-side (`WorldEndpoints.cs:482`), never actually missing; the earlier
 * `pendingWithReason(PLAYER_PENDING.constructionProgress)` here papered over the fact that
 * `WorldSlotDto`'s TS mirror simply never carried the field, which is the bug this session's own
 * `structureId` finding already named once. `known(...)` now, straight off the wire.
 */
export function adaptWorldSlot(dto: WorldSlotDto): SlotView {
  return {
    slotIndex: dto.slotIndex,
    slotTypeId: dto.slotTypeId,
    element: dto.element,
    state: dto.state,
    ownerFactionId: dto.ownerFactionId,
    guardWaveId: dto.guardWaveId,
    guardState: dto.guardState,
    structureId: dto.structureId,
    constructionTurnsRemaining: known(dto.constructionTurnsRemaining)
  };
}

export function adaptWorldForce(dto: WorldForceDto): ForceView {
  if (dto.exact) {
    return {
      entityId: dto.entityId,
      ownerFactionId: dto.ownerFactionId,
      kind: dto.kind,
      exact: true,
      strength: { unit: "gameUnits", value: dto.strength }
    };
  }
  return {
    entityId: dto.entityId,
    ownerFactionId: dto.ownerFactionId,
    kind: dto.kind,
    exact: false,
    bandName: dto.bandName,
    bandCeiling: { unit: "gameUnits", value: dto.bandCeiling }
  };
}

function toLegionPosition(dto: WorldEntityDto): LegionPosition {
  if (dto.atSectorId != null) {
    return { kind: "sector", sectorId: dto.atSectorId };
  }
  return {
    kind: "lane",
    laneId: dto.onLaneId ?? "",
    towardSectorId: dto.onLaneTowardSectorId ?? "",
    progress: { unit: "perMilleRatio", op: "flat", value: dto.laneProgressMilli }
  };
}

function adaptLegionMember(dto: WorldEntityMemberDto): LegionMemberView {
  return {
    instanceId: dto.instanceId,
    speciesId: dto.speciesId,
    level: { unit: "count", value: dto.level },
    hp: { unit: "gameUnits", value: dto.hp },
    wounds: { unit: "gameUnits", value: dto.wounds },
    role: pendingWithReason(PLAYER_PENDING.memberRole)
  };
}

export function adaptWorldLegion(dto: WorldEntityDto): LegionView {
  return {
    entityId: dto.entityId,
    kind: dto.kind,
    ownerFactionId: dto.ownerFactionId,
    position: toLegionPosition(dto),
    stance: dto.stance,
    // The name says nothing about its unit by itself — MovementPolicy.PointsPerTurn = 1000, so this
    // is per-mille of one turn's march budget, never a raw count.
    movementRemaining: { unit: "perMilleRatio", op: "flat", value: dto.movementRemaining },
    routed: dto.routed,
    members: dto.members.map(adaptLegionMember),
    carriedLoam: pendingWithReason(PLAYER_PENDING.legionSupply),
    capacity: pendingWithReason(PLAYER_PENDING.legionCapacity),
    burn: pendingWithReason(PLAYER_PENDING.legionBurn),
    runway: pendingWithReason(PLAYER_PENDING.legionRunway)
  };
}

export function adaptWorldTurnEvent(dto: WorldTurnEntryDto): TurnEventView {
  return {
    sectorId: dto.sectorId ?? null,
    phase: dto.phase,
    kind: dto.kind,
    subject: dto.subject,
    detail: dto.detail,
    // world-playback owns the one translation table; this adapter never guesses a sentence.
    sentence: pendingWithReason(PLAYER_PENDING.turnEventSentence)
  };
}

/**
 * The whole-state adapter (added closing the scene-composition wiring gap, 2026-09-04) —
 * `contractGuard.ts` bans every `stages/`/`layers/`/`ui/` file from importing a `*Dto` type by
 * name, so `WorldScene.tsx` cannot touch `WorldStateDto` itself even to read `sectors`/`lanes`
 * off it. This is the one function that does, producing only view types on the other side: slots
 * and forces are keyed by their owning sector's id since neither lives on `SectorView` itself
 * (matching `SectorInspectorProps`'s own established shape — `slots`/`forces` are sibling props,
 * never nested fields).
 */
export type AdaptedWorldState = {
  sectors: SectorView[];
  lanes: LaneView[];
  slotsBySectorId: Record<string, SlotView[]>;
  forcesBySectorId: Record<string, ForceView[]>;
};

export function adaptWorldState(
  dto: WorldStateDto,
  options?: { lifelinesRequested?: boolean }
): AdaptedWorldState {
  const slotsBySectorId: Record<string, SlotView[]> = {};
  const forcesBySectorId: Record<string, ForceView[]> = {};
  for (const sector of dto.sectors) {
    slotsBySectorId[sector.sectorId] = sector.slots.map(adaptWorldSlot);
    forcesBySectorId[sector.sectorId] = sector.forces.map(adaptWorldForce);
  }

  return {
    sectors: dto.sectors.map((s) => adaptWorldSector(s, options)),
    lanes: dto.lanes.map(adaptWorldLane),
    slotsBySectorId,
    forcesBySectorId
  };
}

// ===========================================================================
// Item surfaces — item module 20 (`spec-item-surfaces.md`), over the read-only
// `GET /api/items/*` routes.
//
// ⛔ These adapters RENAME and RESHAPE. They compute nothing: no distance is
// derived here, no verdict decided, no magnitude recombined. Every number
// below arrives on the wire already resolved by the module that owns it.
// ===========================================================================

const ITEM_SURFACE_IDS: ItemSurfaceId[] = [
  "armoury",
  "equipScreen",
  "itemCard",
  "comparison",
  "socketBench",
  "compendium"
];

function toItemSurfaceId(wire: string): ItemSurfaceId | null {
  return ITEM_SURFACE_IDS.find((id) => id === wire) ?? null;
}

/**
 * The C# enum name, lower-cased. Precedence is the server's — locked before loading before error
 * before empty — so this is a rename, never a re-decision.
 *
 * An unrecognised state resolves to `error` rather than `ready`: a surface we cannot describe must
 * not claim to be showing everything.
 */
function toItemSurfaceState(wire: string): ItemSurfaceState {
  switch (wire) {
    case "Locked":
      return "locked";
    case "Loading":
      return "loading";
    case "Empty":
      return "empty";
    case "Ready":
      return "ready";
    case "Error":
    default:
      return "error";
  }
}

export function adaptItemSurfaces(dtos: ItemSurfaceStatusDto[]): SurfaceStatusView[] {
  const views: SurfaceStatusView[] = [];
  for (const dto of dtos) {
    const surface = toItemSurfaceId(dto.surface);
    if (!surface) continue;
    views.push({ surface, state: toItemSurfaceState(dto.state), unlockKey: dto.unlockKey });
  }
  return views;
}

function toRenderStrategy(wire: string): RenderStrategy {
  switch (wire) {
    case "Virtualize":
      return "virtualize";
    case "SearchFirst":
      return "searchFirst";
    case "RenderAll":
    default:
      return "renderAll";
  }
}

export function adaptArmouryRow(dto: ArmouryRowDto): ArmouryRowView {
  return {
    instanceId: dto.instanceId,
    containerId: dto.containerId,
    rarity: rarityFromWire(dto.rarity, dto.rarityOrdinal),
    role: pendingWithReason(PLAYER_PENDING.itemRole),
    frame: pendingWithReason(PLAYER_PENDING.itemFrame),
    assigned: dto.assigned,
    locked: dto.locked,
    unseen: dto.unseen,
    stale: dto.stale,
    acquiredUtc: dto.acquiredUtc,
    // item-content `granted-action-text` (T15). `?? false` covers a server older than the field
    // rather than letting `undefined` reach a boolean slot — absent is "we do not know of one".
    battleOnly: dto.battleOnly ?? false
  };
}

export function adaptArmouryPage(dto: ArmouryPageDto): ArmouryPageView {
  return {
    inbox: {
      unseen: { unit: "count", value: dto.unseen },
      total: { unit: "count", value: dto.total },
      overReviewPressure: dto.overReviewPressure
    },
    strategy: toRenderStrategy(dto.renderStrategy),
    rows: dto.rows.map(adaptArmouryRow)
  };
}

const COMBINATION_SHAPES: CombinationShape[] = [
  "strain",
  "splice",
  "pure",
  "ring",
  "eclipse",
  "diversity"
];

/**
 * `Undiscovered` never reaches the wire — the reveal pass drops it before serialising — so an
 * unrecognised state maps there rather than to a state that would render. Showing a row we cannot
 * classify is how a combination the player has not earned appears in the list.
 */
function toCombinationState(wire: string): CombinationState {
  switch (wire) {
    case "Active":
      return "active";
    case "OneAway":
      return "one-away";
    case "KnownInactive":
      return "known-inactive";
    default:
      return "undiscovered";
  }
}

export function adaptCombination(dto: CombinationRowDto): CombinationView {
  return {
    comboId: dto.comboId,
    shape: COMBINATION_SHAPES.find((s) => s === dto.shape) ?? "pure",
    state: toCombinationState(dto.state),
    distance: dto.distance,
    missingFamilies: dto.missingFamilies,
    missingElements: dto.missingElements,
    grantedTier: dto.grantedTier
  };
}

export function adaptCombinations(dtos: CombinationRowDto[]): CombinationView[] {
  return dtos.map(adaptCombination).filter((c) => c.state !== "undiscovered");
}

/**
 * An armoury row as the eleven-block container the item card renders.
 *
 * ⚠ Most blocks come back `pending`, and that is the honest state rather than a gap being papered
 * over: the armoury route serves ownership and flags, and no route serves module 10's rendered
 * card yet. `sockets` is filled separately by the caller that has already read the combination
 * route for this instance, so the card never triggers a second read of its own.
 *
 * `relicName` is the display name where the seeded relic catalog knows this container; the id is
 * the honest fallback, never an invented name.
 */
export function adaptArmouryItem(row: ArmouryRowView, relicName?: string): ContainerView {
  return {
    instanceId: row.instanceId,
    kind: "item",
    header: {
      name: relicName ?? row.containerId,
      rarity: row.rarity,
      baseTypeAndClassNoun: row.role.state === "known" ? row.role.value : "Item"
    },
    requirements: pendingWithReason(PLAYER_PENDING.itemRequirements),
    baseStats: [],
    implicit: pendingWithReason(PLAYER_PENDING.itemBaseStats),
    affixes: pendingWithReason(PLAYER_PENDING.itemAffixes),
    enhancement: pendingWithReason(PLAYER_PENDING.itemEnhancement),
    sockets: pendingWithReason(PLAYER_PENDING.itemSockets),
    set: pendingWithReason(PLAYER_PENDING.itemSet),
    grantedAction: pendingWithReason(PLAYER_PENDING.itemGrantedAction),
    footer: known({ stale: row.stale, locked: row.locked })
  };
}

// ===========================================================================
// item module 10 — the rendered card, and the comparison built on two of them
//
// ⛔ Nothing below composes a sentence, formats a magnitude, decides a verdict
// or groups a delta. All four already happened in `ItemCardRenderer`,
// `ArmouryCompare` and `DominancePresentation`; this section renames their
// output into view types and does no arithmetic at all.
// ===========================================================================

/** `item.card.header` etc — the block keys `CardBlocks` declares, in the order it declares them. */
const CARD_BLOCK = {
  header: "item.card.header",
  requirements: "item.card.requirements",
  baseStats: "item.card.base-stats",
  implicit: "item.card.implicit",
  affixes: "item.card.affixes",
  enhancement: "item.card.enhancement",
  sockets: "item.card.sockets",
  set: "item.card.set",
  grantedAction: "item.card.granted-action",
  flavour: "item.card.flavour",
  footer: "item.card.footer"
} as const;

/** C# `UnitClass` member names → the contract's own camelCase vocabulary. Closed, both ends. */
const UNIT_CLASS_BY_WIRE: Record<string, UnitClass> = {
  GameUnits: "gameUnits",
  GameUnitsPerSecond: "gameUnitsPerSecond",
  SigmoidPoints: "sigmoidPoints",
  SigmoidMultiplierPoints: "sigmoidMultiplierPoints",
  StatusPotencyPoints: "statusPotencyPoints",
  PerMilleRatio: "perMilleRatio",
  Milliseconds: "milliseconds",
  Count: "count",
  Flag: "flag",
  LadderIndex: "ladderIndex",
  AptitudePoints: "aptitudePoints",
  ReciprocalPoints: "reciprocalPoints",
  LoamUnits: "loamUnits"
};

/** C# `SourceKind` member names → §4.4's twelve kebab-case values. */
const SOURCE_KIND_BY_WIRE: Record<string, SourceKind> = {
  Base: "base",
  Implicit: "implicit",
  AffixPrefix: "affix-prefix",
  AffixSuffix: "affix-suffix",
  Enhancement: "enhancement",
  SocketInsert: "socket-insert",
  Resonance: "resonance",
  Word: "word",
  SetThreshold: "set-threshold",
  GrantedAction: "granted-action",
  UniqueIdentity: "unique-identity",
  UniqueVariance: "unique-variance"
};

const DOMINANCE_BY_WIRE: Record<string, DominanceVerdict> = {
  StrictlyBetter: "strictly-better",
  StrictlyWorse: "strictly-worse",
  Sidegrade: "sidegrade",
  Incomparable: "incomparable"
};

/**
 * A display key's last segment, shown where the key itself would be.
 *
 * ⚠ **A placement, never a translation.** `content/display/en.json` still carries no row for
 * `class.*` / `rarity.*` / `combo.*` / `item.compare.*` — a named wiring gap in the string corpus,
 * owned by the content side — so for those the honest thing to show is the key's own tail rather than
 * an invented English word. The same rule `ItemCard`'s `LineRow` already applies to a line's label.
 *
 * ⛔ **Flavour no longer comes through here** (item-content T6, 2026-09-06). The catalog now carries a
 * real row for all 118 authored unique and set sentences, the server resolves it, and the card's
 * flavour line arrives with the finished sentence in `__rendered` — see `adaptItemCard`. A key with no
 * catalog row renders no flavour at all, which is the honest answer; `keyTail` on a flavour key would
 * turn `flavor.unique.carrion-spitter` into "carrion spitter" and call it prose.
 */
function keyTail(key: string | undefined): string {
  if (!key) return "";
  return key.split(".").slice(-1)[0]!.replace(/-/g, " ");
}

function adaptDisplayLine(dto: DisplayLineDto): DisplayLine {
  const { __rendered: rendered, ...args } = dto.args;
  const line: DisplayLine = {
    key: dto.key,
    // Every arg stays a STRING: the renderer already formatted each magnitude into the sentence it
    // composed, and re-typing one as a `Magnitude` here would invite a second formatting pass.
    args,
    unit: dto.unit ? UNIT_CLASS_BY_WIRE[dto.unit] ?? null : null,
    sourceKind: dto.sourceKind ? SOURCE_KIND_BY_WIRE[dto.sourceKind] ?? null : null,
    groupOrder: dto.groupOrder
  };
  if (rendered) line.rendered = rendered;
  if (dto.rollBarSegments !== null) line.rollBarSegments = dto.rollBarSegments;
  if (dto.rollQualityPerMille !== null) line.rollQualityPerMille = dto.rollQualityPerMille;
  if (dto.contextRead) line.context = { reference: "neutral", text: dto.contextRead };
  return line;
}

function blockLines(dto: ItemCardDto, blockKey: string): DisplayLineDto[] {
  return dto.blocks.find((b) => b.blockKey === blockKey)?.lines ?? [];
}

function linesOf(dto: ItemCardDto, blockKey: string): DisplayLine[] {
  return blockLines(dto, blockKey).map(adaptDisplayLine);
}

/** `known` when the block rendered something, `absent` when it rendered nothing. Never `pending`:
 * the route answered, so "not shown yet" is no longer one of the possibilities. */
function blockPending(dto: ItemCardDto, blockKey: string): Pending<DisplayLine[]> {
  const lines = linesOf(dto, blockKey);
  return lines.length > 0 ? known(lines) : absent<DisplayLine[]>();
}

function rarityFromCard(args: Record<string, string>): Rarity {
  const id = (args.rungKey ?? "").replace(/^rarity\./, "");
  const rung = RARITY_LADDER.find((r) => r.id === id);
  if (rung) return rung;
  // A rung the shipped ladder does not carry: the card still states all three redundant channels
  // itself, so those are used rather than a different rung's.
  return {
    ...RARITY_LADDER[0]!,
    display: keyTail(args.rungKey),
    colour: args.colorHex ?? RARITY_LADDER[0]!.colour,
    pips: Number(args.pips ?? "0")
  };
}

function adaptSockets(dto: ItemCardDto, combinations: CombinationView[]): Pending<SocketsView> {
  const cells: SocketCellView[] = blockLines(dto, CARD_BLOCK.sockets)
    .filter((l) => l.key === "item.card.socket.cell")
    .map((l) => ({
      index: Number(l.args.index ?? "0"),
      affinity: l.args.affinityKey ? keyTail(l.args.affinityKey) : null,
      insertName: l.args.empty === "1" ? null : keyTail(l.args.insertKey),
      omniCountsDiversityOnly: l.args.omniDiversityOnly === "1"
    }));

  if (cells.length === 0 && combinations.length === 0) return absent<SocketsView>();
  return known({ cells, combinations });
}

function adaptSet(dto: ItemCardDto): Pending<SetView> {
  const lines = blockLines(dto, CARD_BLOCK.set);
  const header = lines.find((l) => l.key === "item.card.set.header");
  if (!header) return absent<SetView>();

  const name = keyTail(header.args.nameKey);
  const ladder: SetTierView[] = lines
    .filter((l) => l.key === "item.card.set.threshold")
    .map((l) => ({
      piecesRequired: Number(l.args.pieces ?? "0"),
      active: l.args.active === "1",
      isCapability: l.args.capability === "1"
    }));

  const redundant = header.args.redundant === "1";
  return known({
    setId: (header.args.nameKey ?? "").replace(/^set\./, ""),
    name,
    count: Number(header.args.count ?? "0"),
    total: Number(header.args.total ?? "0"),
    ladder,
    // The card's set block is the set THIS piece touches, and module 12's disclosure flag says which
    // of the two ways it touches it. One name either way — never both, never neither.
    advances: redundant ? [] : [name],
    redundantIn: redundant ? [name] : []
  });
}

/**
 * ⭐ One rendered card, as the eleven-block container the item card draws — the adapter for
 * `GET /api/items/{instanceId}/card`.
 *
 * `combinations` comes from the caller that has already read `GET /api/items/{id}/combinations`: the
 * card's own socket block renders the combination SENTENCES, but `CombinationView` also carries the
 * shape and the granted tier, which only that route reports. Passing them in keeps the card from
 * triggering a second read of its own, and leaving them out is honest rather than empty-by-guess.
 */
export function adaptItemCard(dto: ItemCardDto, combinations: CombinationView[] = []): ContainerView {
  const header = blockLines(dto, CARD_BLOCK.header)[0]?.args ?? {};
  const enhancement = blockLines(dto, CARD_BLOCK.enhancement)[0];
  const footer = blockLines(dto, CARD_BLOCK.footer)[0]?.args ?? {};
  // Block 10 can carry a unique's line and a set's (a unique may not be a set member, so in practice
  // one). Only lines the string catalog actually resolved are shown — a key with no row contributes
  // nothing rather than its own tail dressed up as a sentence.
  const flavour = blockLines(dto, CARD_BLOCK.flavour)
    .map((l) => l.args.__rendered)
    .filter((s): s is string => typeof s === "string" && s.length > 0);

  const view: ContainerView = {
    instanceId: dto.instanceId,
    kind: "item",
    header: {
      name: keyTail(header.name),
      rarity: rarityFromCard(header),
      baseTypeAndClassNoun: [keyTail(header.baseNameKey), keyTail(header.classNounKey)]
        .filter((s) => s.length > 0)
        .join(" · ")
    },
    requirements: blockPending(dto, CARD_BLOCK.requirements),
    baseStats: linesOf(dto, CARD_BLOCK.baseStats),
    implicit: blockPending(dto, CARD_BLOCK.implicit),
    affixes: blockPending(dto, CARD_BLOCK.affixes),
    enhancement: enhancement
      ? known({
          tier: Number(enhancement.args.level ?? "0"),
          nextMilestone: adaptDisplayLine(enhancement)
        })
      : absent(),
    sockets: adaptSockets(dto, combinations),
    set: adaptSet(dto),
    grantedAction: blockPending(dto, CARD_BLOCK.grantedAction),
    footer: known({
      // `""` is block 11's own "nothing on this item rolled", not a missing read.
      ...(footer.meanRollQuality ? { meanRollQuality: footer.meanRollQuality } : {}),
      stale: footer.stale === "1",
      locked: footer.locked === "1"
    })
  };

  if (header.frame) view.header.frameBadge = header.frame;
  if (header.ilvl) view.header.itemLevel = Number(header.ilvl);
  // Absent at +0 rather than present as "+0": a zero enhancement is not a thing the item has.
  if (header.enhance) view.header.enhancementPrefix = header.enhance + " ";
  if (flavour.length > 0) view.flavour = flavour.join("\n\n");

  return view;
}

function adaptChannelDelta(dto: ChannelDeltaDto): ChannelDeltaView {
  // ONE unit vocabulary, shared with the group header above. Until 2026-09-06 the delta carried a
  // second one of its own (`game-units` / `per-mille`, derived server-side from the atom's OP) and
  // could disagree with the group it sat in — `maxHp` arrived as `per-mille` inside a `GameUnits`
  // header. The server now labels the CHANNEL, which is the unit ledger's own rule.
  const unit = dto.unit ? UNIT_CLASS_BY_WIRE[dto.unit] ?? "gameUnits" : "gameUnits";
  // `more` is the per-mille reading for a value that is already a delta from zero — a stat
  // modifier's own "+400‰ more". `flat` would render a proportion as if it were an absolute.
  const op = unit === "perMilleRatio" ? ("more" as const) : undefined;
  const magnitude = (value: number): Magnitude =>
    op ? { unit, value, channel: dto.channel, op } : { unit, value, channel: dto.channel };

  return {
    channel: dto.channel,
    incumbent: magnitude(dto.incumbent),
    candidate: magnitude(dto.candidate),
    delta: magnitude(dto.delta)
  };
}

/**
 * ⭐ The comparison payload — `DominancePresentation`'s verdict, trade and unit-class grouping, plus
 * module 13's deltas, exactly as the server decided them.
 *
 * ⛔ **No synthesized scalar reaches this view, and there is no field one could hide in.** The
 * footnote that says why is the server's own constant key.
 */
export function adaptItemCompare(dto: ItemCompareDto): ComparePayloadView {
  const verdict = DOMINANCE_BY_WIRE[dto.dominance] ?? "incomparable";
  const groups: UnitClassGroupView[] = dto.unitGroups.map((g) => ({
    // An unresolvable unit keeps its OWN group; folding it into `gameUnits` is the exact guess SC4
    // forbids, and the server already made the same choice on the way out.
    unit: g.unit ? UNIT_CLASS_BY_WIRE[g.unit] ?? null : null,
    deltas: g.deltas.map(adaptChannelDelta)
  }));

  return {
    badge: { verdict, label: keyTail(dto.badge.labelKey), shape: dto.badge.shape },
    groups,
    // Only a sidegrade is a trade. For every other verdict the split is real but says nothing the
    // verdict word has not already said, so it is not drawn.
    trade:
      verdict === "sidegrade"
        ? {
            youGain: dto.trade.youGain.map(adaptChannelDelta),
            youGiveUp: dto.trade.youGiveUp.map(adaptChannelDelta)
          }
        : null,
    incomparableReason: dto.incomparableReasonKey ? keyTail(dto.incomparableReasonKey) : null,
    meanRollQualityPerMille: dto.meanRollQualityMilliCandidate
  };
}

const WORKBENCH_VERBS: WorkbenchVerb[] = [
  "salvage",
  "upcycle",
  "enhance",
  "socket-add",
  "socket-insert",
  "socket-imbue"
];

function adaptWorkbenchCost(dto: WorkbenchCostDto): WorkbenchCostView {
  return {
    materialClass: dto.class,
    materialId: dto.materialId,
    qty: { unit: "count", value: dto.qty }
  };
}

/** `""` is "no affinity declared" on the wire; `null` is that same fact in the view. */
function adaptWorkbenchSocket(dto: WorkbenchSocketDto): WorkbenchSocketView {
  return {
    index: dto.index,
    affinity: dto.affinity.length > 0 ? dto.affinity : null,
    crafted: dto.crafted,
    insertContainerId: dto.insert && dto.insert.length > 0 ? dto.insert : null
  };
}

/**
 * The workbench's own reply, in view terms.
 *
 * ⛔ **Nothing is composed here.** Every quantity, the level, the pity counter and the roll chance
 * are the server's resolved values; this function labels their unit class and stops.
 *
 * `successMilli` is `0` on every verb that rolls nothing, and a zero *chance* and an absent one are
 * different sentences — so it becomes `null` for those verbs rather than a 0‰ the bench would
 * render as "0.0% to succeed" beside a socket that never rolled.
 */
export function adaptWorkbenchOutcome(dto: WorkbenchOutcomeDto): WorkbenchOutcomeView {
  const rolls = dto.verb === "enhance";
  return {
    ok: dto.ok,
    verb: WORKBENCH_VERBS.find((v) => v === dto.verb) ?? "salvage",
    reason: dto.reason,
    instanceId: dto.instanceId,
    recipeId: dto.recipeId,
    opSeq: dto.opSeq,
    replayed: dto.replayed,
    outcome: dto.outcome,
    enhanceLevel: { unit: "count", value: dto.enhanceLevel },
    pityCounter: { unit: "count", value: dto.pityCounter },
    successChance: rolls ? { unit: "perMilleRatio", value: dto.successMilli, op: "flat" } : null,
    spent: dto.spent.map(adaptWorkbenchCost),
    granted: dto.granted.map(adaptWorkbenchCost),
    sockets: dto.sockets.map(adaptWorkbenchSocket)
  };
}

// ---------------------------------------------------------------------------
// item module 4 (`equip-assign`) — equip / unequip
// ---------------------------------------------------------------------------

/**
 * `core.v1.json`'s sixteen role ids, closed. The registry is append-only, so a role the server
 * sends that is not here means the registry grew and this list is stale — the adapter says so by
 * returning `null` rather than widening the union at runtime.
 */
const ITEM_ROLE_IDS: ItemRoleId[] = [
  "armament-primary",
  "core-guard",
  "ward-array",
  "armament-secondary",
  "jewel-major",
  "manipulator",
  "mantle",
  "head-guard",
  "girdle",
  "sense",
  "footing",
  "infusion",
  "retinue",
  "jewel-minor-a",
  "jewel-minor-b",
  "standard"
];

export function asItemRoleId(role: string): ItemRoleId | null {
  return ITEM_ROLE_IDS.find((r) => r === role) ?? null;
}

/**
 * `ref_kind` names the WRITE PATH, not the shape of the thing worn, which is why it becomes a word
 * the surface can act on: `rolled` is an item instance this flow owns and may take off, anything
 * else is the relic flow's row and is read-only from here.
 */
function equipSource(refKind: string): "item" | "relic" {
  return refKind === "rolled" ? "item" : "relic";
}

/**
 * One assignment row. A row whose role this build does not know is dropped rather than rendered
 * under an invented name — the same rule `ListAssignments` applies on the server side.
 */
export function adaptEquipAssignments(dtos: ItemAssignmentDto[]): EquipAssignmentView[] {
  const views: EquipAssignmentView[] = [];
  for (const dto of dtos) {
    const role = asItemRoleId(dto.role);
    if (role === null) continue;
    views.push({ role, source: equipSource(dto.refKind), refId: dto.refId, assignedUtc: dto.assignedUtc });
  }
  return views;
}

/**
 * The equip executor's own reply, in view terms. Nothing is composed: `ok` and `reason` are the
 * server's, and `assignments` is the list it read back after the write.
 */
export function adaptEquipOutcome(dto: ItemEquipOutcomeDto): EquipOutcomeView {
  return {
    ok: dto.ok,
    verb: dto.verb === "unequip" ? "unequip" : "equip",
    reason: dto.reason,
    specimenId: dto.specimenId,
    role: asItemRoleId(dto.role),
    refId: dto.refId,
    replaced: dto.replaced ? (adaptEquipAssignments([dto.replaced])[0] ?? null) : null,
    assignments: adaptEquipAssignments(dto.assignments)
  };
}
