import type { Pending } from "./pending";

/**
 * The FE view contract. Components bind here — never to a REST DTO
 * (`src/lib/bus/*.ts`) directly (game-gui-map.md's contract section, T4's
 * guard). Authored against the eleven-entity ladder in
 * `docs/design/README.md` §6: Atom, Container, Actor, Status, Element,
 * Channel, Resource, Power, Sector, Contract (the SSOT's "Demon + contract"
 * rung), Run.
 *
 * That entity list is itself flagged incomplete (item/ and action/ were
 * missed — docs/design/gap-audit-2026-08-22.md, closed by nine detail-design
 * documents in `docs/design/spec-*.md`). Extension is additive-only
 * (game-gui-map.md's rule): add a field or a variant freely; renaming or
 * narrowing one is a contract version bump + ADR.
 *
 * Fields with no server source today are `Pending<T>` with a player-facing
 * reason, not omitted — "declared, not deferred".
 */

/**
 * v2 (2026-09-04): `SectorView.typeId` narrowed `number` → `string` to match the wire
 * (`WorldSectorDto.TypeId` is `string`, `WorldDtos.cs:66`) — see `decisions.md`'s dated ADR row.
 * A narrowing, not an addition, so it earns the bump rather than riding in free.
 */
export const CONTRACT_VERSION = 2;

// ===========================================================================
// Shared primitives — spec-magnitude-and-units.md §7
// ===========================================================================

/** No overload of `formatMagnitude` (T6) accepts a bare `number` — that omission is the GG-46 guard. */
export type UnitClass =
  | "gameUnits"
  | "gameUnitsPerSecond"
  | "sigmoidPoints"
  | "sigmoidMultiplierPoints"
  | "statusPotencyPoints"
  | "perMilleRatio"
  | "milliseconds"
  | "count"
  | "flag"
  // Ten classes above (spec-magnitude-and-units.md §3); the two below are class-system additions,
  // both authorised 2026-08-26 (spec-primary-stats.md §3.2, spec-unit-class-close.md §3.3/§3.5) —
  // "ladderIndex" itself was already shipped in C# 2026-08-24 but owed here until now (§3's own
  // "Contract change owed" note).
  | "ladderIndex"
  | "aptitudePoints"
  | "reciprocalPoints"
  // loamUnits (world-numbers, owner-authorised 2026-09-04 — see decisions.md's dated ADR row and
  // spec-magnitude-and-units.md's ledger): a whole `long` count of loam, distinct from `gameUnits`
  // because its ledger row needs no `channel` (loam is not a derived channel) and it must not
  // always render signed the way `gameUnits` does (a cost or a stock is a plain count; only a flow
  // wants a sign, and that sign is `LoamFigure`'s own composition, not this unit class's).
  | "loamUnits";

export type ChannelId = string;

export type Magnitude = {
  unit: UnitClass;
  /** The frozen integer the engine holds. Never pre-formatted. */
  value: number;
  /** Required for gameUnits / sigmoid* — carries the arena. */
  channel?: ChannelId;
  /**
   * Required for perMilleRatio. `absolute` (owner-authorised 2026-09-04 — decisions.md) renders the
   * raw per-mille as a multiplier with **no delta convention** — `1400` renders `×1.40` — for a
   * field whose own neutral baseline is 1000, such as `FractureIntensityMilli`. `more` stays for a
   * field that is already a *delta* from zero (a stat modifier's own "+400‰ more" reading).
   */
  op?: "flat" | "increased" | "more" | "absolute";
};

export type ContextRead = {
  reference: "neutral" | { specimenId: string };
  text: string;
};

/** The twelve closed values a rendered line's origin can be (spec-item-card.md §3). */
export type SourceKind =
  | "base"
  | "implicit"
  | "affix-prefix"
  | "affix-suffix"
  | "enhancement"
  | "socket-insert"
  | "resonance"
  | "word"
  | "set-threshold"
  | "granted-action"
  | "unique-identity"
  | "unique-variance";

export type RollPolicy = "fixed" | "onInstantiate" | "onApply";

/** A line binds to `key`/`args`, never a finished sentence — a translator reorders without touching a number. */
export type DisplayLine = {
  key: string;
  args: Record<string, Magnitude | string>;
  unit: UnitClass;
  /** Absent means no context part (no "vs neutral" / "vs <specimen>" clause). */
  context?: ContextRead;
  rollPolicy: RollPolicy;
  /** Only when rollPolicy === "onInstantiate". */
  rollQualityPerMille?: number;
  sourceKind: SourceKind;
  groupOrder: number;
};

export type Rarity = {
  ordinal: number; // 10..100, spaced by 10
  id: "chaff" | "sprout" | "grafted" | "cultivated" | "fused" | "chimeric" | "heirloom" | "firstseed" | "sunwoven" | "almanac";
  display: string;
  colour: string;
  pips: number; // 1..10
};

// ===========================================================================
// 1. Atom — Token · Chip · Row · Card (docs/design/README.md §6)
// ===========================================================================

export type AtomView = {
  id: string;
  line: DisplayLine;
};

// ===========================================================================
// 2. Container — item / trait / skill / species-passive / patron / world-buff
//    Chip · Row · Card · Panel. Eleven blocks (spec-item-card.md §1).
// ===========================================================================

export type ContainerKind = "item" | "trait" | "skill" | "species-passive" | "patron" | "world-buff";

export type ContainerHeader = {
  name: string;
  rarity: Rarity;
  baseTypeAndClassNoun: string;
  frameBadge?: string;
  itemLevel?: number;
  enhancementPrefix?: string; // "+10 "
};

export type RequirementLine = {
  attribute: string;
  composed: number;
  gating: number;
  required: number;
  met: boolean;
};

export type ContainerView = {
  instanceId: string;
  kind: ContainerKind;
  header: ContainerHeader;
  requirements: Pending<RequirementLine[]>;
  baseStats: DisplayLine[];
  implicit: Pending<DisplayLine[]>;
  affixes: Pending<DisplayLine[]>;
  enhancement: Pending<{ tier: number; nextMilestone?: DisplayLine }>;
  sockets: Pending<unknown>; // spec-sockets-and-sets.md
  set: Pending<unknown>; // spec-sockets-and-sets.md
  grantedAction: Pending<DisplayLine>;
  flavour?: string;
  footer: Pending<{ meanRollQualityPerMille?: number; stale: boolean; locked: boolean }>;
};

// ===========================================================================
// 3. Actor / specimen — Token · Chip · Row · Card · Panel
// ===========================================================================

export type ActorPhase = "ActiveBound" | "ActiveUnbound" | "Retired" | "Idle";

export type ActorChannelDetail = {
  channelId: ChannelId;
  value: number;
  unitClass: UnitClass;
  state: DerivedChannelState;
  cap?: { value: number; distance: number };
  composeSentence?: string;
  contributions: Pending<{ source: string; magnitude: Magnitude }[]>;
};

/** aura-skill T18c: one source's own contribution to a derived channel, as
 * `AuraDerivedEndpoints.cs` (T18b) reports it — flat and simple by design, unlike
 * `ActorChannelDetail.contributions`'s richer (still unproduced) `Magnitude`-based shape above. A
 * distinct type rather than forcing a field-name/shape fit onto that one. */
export type DerivedContribution = {
  sourceId: string;
  op: string;
  value: number;
};

export type ActorView = {
  instanceId: string;
  playerId: number;
  side: "plant" | "zombie";
  typeId: number;
  displayName: Pending<string>;
  phase: ActorPhase;
  level: number;
  xp: number;
  xpToNext: Pending<number>;
  revision: number;
  /** Summary rung: 6-8 non-default channels + "see all N" — spec-derived-stat-sheet.md §5.1. */
  channelSummary: Pending<ActorChannelDetail[]>;
  elementTyping: Pending<{ primary: ElementId; secondary?: ElementId }>;
  shieldStack: Pending<unknown>; // spec-shield-and-elements.md §3.1
  equipSlots: Pending<unknown>; // spec-equip-and-paperdoll.md
};

// ===========================================================================
// 4. Status — Token · Chip · Row (spec-derived-stat-sheet.md §6)
// ===========================================================================

export type StatusFamily = "power" | "resist" | "immune" | "immuneReduction" | "expose";

export type StatusView = {
  id: string; // e.g. "status.resist.dot"
  family: StatusFamily;
  name: Pending<string>;
  channel: Pending<ActorChannelDetail>;
};

// ===========================================================================
// 5. Element — Token · Chip. 6 concrete + omni (ActorElementTypes.cs).
// ===========================================================================

export type ElementId = "fire" | "ice" | "air" | "earth" | "light" | "dark";
export type ElementSlot = ElementId | "omni";

export type ElementMatchupCell = {
  attacker: ElementSlot;
  defender: ElementSlot;
  multiplierPerMille: number;
};

export type ElementMatrixView = {
  mode: "combat" | "shield" | "diff";
  cells: Pending<ElementMatchupCell[]>;
};

// ===========================================================================
// 6. Channel — Token · Chip · Row. Six-state model (spec-derived-stat-sheet.md §3).
// ===========================================================================

export type DerivedChannelState = "active" | "default" | "capped" | "stub" | "no-producer" | "unregistered";

export type ChannelView = {
  channelId: ChannelId;
  unitClass: UnitClass;
  state: DerivedChannelState;
};

// ===========================================================================
// 7. Resource — Token · Meter · Row. Five locked ids (resource-hub-ssot.md).
// ===========================================================================

export type ResourceId = "hp" | "stamina" | "hunger" | "spirit" | "qi";

export type ResourceView = {
  id: ResourceId;
  label: string; // faction-resolved (hunger -> Sun/Hunger, qi -> Yang/Yin)
  value: number;
  max: number;
  polarity: "positive" | "negative";
  exhausted: boolean;
};

// ===========================================================================
// 8. Power vector — Token · Chip · Card. definitions.md §7 — unbuilt server-side.
// ===========================================================================

export type PowerCategory = "offense" | "defense" | "utility" | "mobility" | "sustain";

export type PowerView = {
  scalar: Pending<number>; // rendered "≈ 1,300 (±25%)" — two sig figs + band (spec-comparison.md §2)
  categories: Pending<Record<PowerCategory, number>>;
};

// ===========================================================================
// 9. Sector / lane / legion — Token · Chip · Card · Panel (world-stage program).
//
// world-stage W4 (2026-09-04): the six views spec-world-contract.md §1 calls for, replacing the
// six-field vocabulary-only stub. Every magnitude a player would read as a quantity carries its
// `UnitClass` in the type (GG-46) — never a bare `number` for one of those. Identifiers, layout
// coordinates and booleans are not magnitudes and stay plain.
//
// **`loamUnits` — owner-authorised 2026-09-04 (world-numbers W37/W38, decisions.md's dated ADR
// row).** Every loam/component reading below carries `unit: "loamUnits"`, not `gameUnits` — see
// `adaptSector`/`adaptWorldForce` in `adapt.ts` for the one place these are actually constructed.
//
// **The `…Milli` fracture reading — `Magnitude.op` gains `absolute` (same authorisation), not a
// divide-at-the-adapter.** `FractureIntensityMilli`'s neutral baseline is 1000
// (`WorldDtos.cs:80`); the verified defect (a raw 1400 rendering ×2.40 instead of ×1.40) is fixed by
// `formatPerMille`'s own new `absolute` arm (`×(value/1000)`, no delta), so the adapter passes the
// wire value straight through — never subtracting 1000 first, which would just move the same
// derived-in-TypeScript problem `spec-loam-fe.md` forbids one line over.
// ===========================================================================

/** `dave` sees the whole picture; anyone else sees exactly this ladder — `IntelLadder.StateOf`. */
export type IntelState = "Unknown" | "Rumored" | "Scouted" | "Watched";

/**
 * The five operands of `LoamUpkeep.For(garrisonMembers, developmentLevel, dangerBand,
 * intensityMilli, handicapMilli)` (`LoamUpkeep.cs:40`) — **not a design choice**, this is that
 * signature's own argument list, in that order. `ModifierLedger`'s whole job is reproducing
 * `sum × intensityMilli × handicapMilli ÷ 1_000_000` exactly, one division, never two roundings.
 */
export type UpkeepBreakdownView = {
  base: Magnitude; // loamUnits
  garrison: Magnitude; // loamUnits
  development: Magnitude; // loamUnits
  danger: Magnitude; // loamUnits
  intensityMilli: Magnitude; // perMilleRatio, op "absolute"
  handicapMilli: Magnitude; // perMilleRatio, op "absolute"
};

export type SectorView = {
  sectorId: string;
  /** `string` since v2 — the wire is `WorldSectorDto.TypeId: string` (.NET-cased id), not a number. */
  typeId: string;
  climate: string | null;
  ownerFactionId: string | null;
  /**
   * The rendering must branch on `intel`, never on emptiness — an unseen sector serialises every
   * other field at its record default (`WorldEndpoints.cs:271-277`) and is indistinguishable from a
   * zeroed known one except by this field.
   */
  intel: IntelState;
  /** Turns since last seen; 0 while `intel === "Watched"`. */
  intelAge: number;
  phase: string;
  dangerBand: Magnitude; // count
  developmentLevel: Magnitude; // count
  stability: Magnitude; // perMilleRatio, op "flat" — 0..1000 is 0..100%
  /**
   * Found missing 2026-09-04 (world-inspector W63, the third stale "never assigned" premise found
   * this task alone): `LoamPhases.NextPressure` writes this every turn from fade contagion
   * (`LoamPhases.cs:190,203,266-291`) — real, live state, not a stub. `pressureMilli` was already on
   * the wire DTO mirror (`lib/bus/world.ts`) but never reached this view contract at all.
   */
  pressure: Magnitude; // perMilleRatio, op "flat"
  /** `op: "absolute"` — see the module comment above. Always present; 1000 = neutral. */
  fractureIntensity: Magnitude; // perMilleRatio, op "absolute"
  habitable: boolean;
  layoutX: number;
  layoutY: number;
  loam: {
    production: Magnitude; // loamUnits
    upkeep: Magnitude; // loamUnits
    net: Magnitude; // loamUnits
    stock: Magnitude; // loamUnits
    /** `world-wire`'s `EffectiveCapacity` projection — the stock figure's own denominator. */
    capacity: Pending<Magnitude>;
    /**
     * The five operands `LoamUpkeep.For` sums, in that function's own signature order
     * (`LoamUpkeep.cs:40`) — `world-numbers` W41's `ModifierLedger` reads this, never re-derives it.
     * Found missing from the wire mirror 2026-09-04, projected server-side since `WorldEndpoints.cs
     * :490-497` (world-stage W10) — genuinely on the wire, not a `Pending` gap.
     */
    upkeepBreakdown: UpkeepBreakdownView;
  };
  /** Non-null only for a sector inside a connected territory component; totals are pooled. */
  component: {
    componentId: string | null;
    production: Magnitude; // loamUnits
    upkeep: Magnitude; // loamUnits
    net: Magnitude; // loamUnits
    stock: Magnitude; // loamUnits
  };
  willReleaseNextTurn: boolean;
  /** Opt-in server sweep (`?lifelines=true`) — absent, not merely zero, when the caller didn't ask. */
  lifelineCost: Pending<Magnitude>;
  lifeline: Pending<boolean>;
  wardenBindingId: Pending<string | null>;
  neglectedTurns: Pending<Magnitude>; // count
};

export type LaneView = {
  laneId: string;
  fromSectorId: string;
  toSectorId: string;
  typeId: string;
  length: Magnitude; // count — a march-turn measure, not a per-mille ratio
  width: Magnitude; // count
  hazard: Magnitude; // perMilleRatio, op "flat"
  wardLevel: Magnitude; // count
  state: string;
  /** Real gap, not merely unwired — `GateKeyId` does not exist on the C# contract yet. */
  gateKeyId: Pending<string | null>;
};

export type LegionMemberRole = "Fighter" | "Bearer";

export type LegionMemberView = {
  instanceId: string | null;
  speciesId: string;
  level: Magnitude; // count
  hp: Magnitude; // gameUnits
  wounds: Magnitude; // gameUnits
  /** Bearer count is the only input to a legion's carrying capacity — not on the wire yet. */
  role: Pending<LegionMemberRole>;
};

/** Exactly one of `atSectorId` or the `onLane*` triple is set — never both, never neither. */
export type LegionPosition =
  | { kind: "sector"; sectorId: string }
  | { kind: "lane"; laneId: string; towardSectorId: string; progress: Magnitude /* perMilleRatio, flat */ };

export type LegionView = {
  entityId: string;
  kind: string;
  ownerFactionId: string;
  position: LegionPosition;
  stance: string;
  /** The name says nothing about its unit by itself — it is per-mille of one turn's march budget. */
  movementRemaining: Magnitude; // perMilleRatio, op "flat"
  routed: boolean;
  members: LegionMemberView[];
  carriedLoam: Pending<Magnitude>;
  capacity: Pending<Magnitude>;
  burn: Pending<Magnitude>;
  /** Turns until this legion runs dry at its current burn; `null` when burn is not positive. */
  runway: Pending<number | null>;
};

export type SlotView = {
  slotIndex: number;
  slotTypeId: string;
  element: string | null;
  state: string;
  ownerFactionId: string | null;
  guardWaveId: string | null;
  guardState: string;
  structureId: string | null;
  constructionTurnsRemaining: Pending<number | null>;
};

/**
 * A band is a fog artefact, never a stand-in for a real count — the discriminated union makes
 * reading `strength` off an inexact force a compile error rather than a UI that quietly lies.
 */
export type ForceView =
  | { entityId: string; ownerFactionId: string; kind: string; exact: true; strength: Magnitude }
  | { entityId: string; ownerFactionId: string; kind: string; exact: false; bandName: string; bandCeiling: Magnitude };

export type TurnEventView = {
  sectorId: string | null;
  phase: string;
  kind: string;
  subject: string;
  detail: string;
  /** `world-playback`'s one translation table owns filling this; never a second copy. */
  sentence: Pending<string>;
};

// ===========================================================================
// 10. Contract — the SSOT's "Demon + contract" rung. Chip · Row · Card · Panel.
//     DTO-grounded already (ContractRowDto + DemonProfileDto joined by
//     instanceId) — the gap here is compositional, not missing fields.
// ===========================================================================

export type ContractView = {
  instanceId: string;
  speciesId: string;
  rarity: string;
  bound: boolean;
  loyalty: number;
  rank: string;
  personality: string;
  upkeepPerDay: number;
  deployable: boolean;
  displayName: Pending<string>; // species catalog name, joined separately (DemonCatalogDto)
};

// ===========================================================================
// 11. Run / wave — Row · Card.
// ===========================================================================

export type RunResult = "victory" | "defeat" | "abandoned" | "unknown";

export type RunView = {
  id: number;
  levelName: Pending<string>;
  result: RunResult;
  startedUtc: string;
  endedUtc?: string;
  zombiesKilled: Pending<number>;
  plantsLost: Pending<number>;
  /** `RunItem.summary`/`.modifiers` are typed `unknown` server-side today — prime Pending candidates. */
  summary: Pending<unknown>;
};

// ===========================================================================
// Commander list — GET /api/commanders/{playerId} (commander-surface P1)
// ===========================================================================

export type CommanderListRow = {
  id: string;
  displayName: string;
  isDefault: boolean;
  activeAuraId: string | null;
  activeAuraName: string | null;
  locationStub: string | null;
  legionStub: string | null;
};

export type CommanderListView = {
  defaultLawnCommanderId: string;
  commanders: CommanderListRow[];
};

/** Commander drill-in context for ActorPanel — not part of ActorView (commander-sheet-role). */
export type CommanderSheetMeta = {
  isDefault: boolean;
  activeAuraName: string | null;
  locationStub: string | null;
  legionStub: string | null;
};
