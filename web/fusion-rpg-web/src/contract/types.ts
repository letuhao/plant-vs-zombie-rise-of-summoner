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

export const CONTRACT_VERSION = 1;

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
  | "reciprocalPoints";

export type ChannelId = string;

export type Magnitude = {
  unit: UnitClass;
  /** The frozen integer the engine holds. Never pre-formatted. */
  value: number;
  /** Required for gameUnits / sigmoid* — carries the arena. */
  channel?: ChannelId;
  /** Required for perMilleRatio. */
  op?: "flat" | "increased" | "more";
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
// 9. Sector / lane / legion — Token · Chip · Card · Panel.
//    World stage excluded this phase (T16) — type declared for vocabulary
//    completeness; no adapter wired yet.
// ===========================================================================

export type SectorView = {
  sectorId: string;
  typeId: number;
  ownerFactionId: Pending<string>;
  stabilityPerMille: number;
  developmentLevel: number;
  habitable: boolean;
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
