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
 *
 * v3 (2026-09-06): `ContainerView.sockets` and `ContainerView.set` narrowed `Pending<unknown>` →
 * `Pending<SocketsView>` / `Pending<SetView>`. Both were declared placeholders waiting for exactly
 * this — item module 20's own build — and nothing has ever produced a value for either, so no
 * caller narrows underneath. It is still a narrowing rather than an addition, so it takes the bump
 * on the same terms v2 did. ⚠ The matching dated row in `decisions.md` is owed and is the owner's
 * to write; this note is not a substitute for it.
 *
 * v4 (2026-09-06): `DisplayLine` and two `ContainerView` fields reshaped the day
 * `GET /api/items/{instanceId}/card` started serving module 10's real `DisplayModel`, because three
 * of the placeholder shapes turned out to be wrong about what a rendered line IS:
 *
 * - `DisplayLine.unit` / `.sourceKind` widened to `| null`. **The null is load-bearing**, not a gap:
 *   a STRUCTURAL line (the header, a requirement clause, the footer) carries no magnitude, so it has
 *   no unit and declares no source kind. Naming one anyway is the exact lie SC4 exists to prevent,
 *   and it is why the C# record made both nullable in the first place.
 * - `DisplayLine.rollPolicy` → `rollBarSegments?`. The bar decision is `ItemDisplayRenderer.BarFor`'s
 *   and it is already made by the time a line exists — re-deriving it here from a policy would be a
 *   second implementation of the one rule that says `Fixed` has no luck to show and `OnApply` shows a
 *   band. `rendered` was added for the same reason: the renderer composes the sentence, the browser
 *   does not.
 * - `ContainerView.requirements` and `.grantedAction` became `Pending<DisplayLine[]>`. Both were
 *   authored as bespoke shapes (`RequirementLine[]`, ONE line) before the card existed; the card
 *   emits `{key, args}` leaves for both, and an item may grant more than one action.
 *
 * `RequirementLine` is retired with this bump — it had no producer and now has no consumer. ⚠ The
 * matching dated row in `decisions.md` is owed and is the owner's to write.
 */
export const CONTRACT_VERSION = 4;

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
  /**
   * `null` for a STRUCTURAL line — the header, a requirement clause, a set name, the footer. Those
   * carry no magnitude, so they have no unit, and naming one would be the lie SC4 exists to prevent.
   */
  unit: UnitClass | null;
  /** Absent means no context part (no "vs neutral" / "vs <specimen>" clause). */
  context?: ContextRead;
  /**
   * The roll-quality bar, already decided by `ItemDisplayRenderer.BarFor`: absent when the line has
   * no luck to show (a `fixed` value never rolled; an `onApply` value shows a band the hit rolls).
   * Re-deriving it here from a roll policy would be a second implementation of that one rule.
   */
  rollBarSegments?: number;
  /** Only for a value the ITEM rolled — absent for `fixed` and for an `onApply` band. */
  rollQualityPerMille?: number;
  /** `null` for the same reason `unit` is: a structural line is not an atom line. */
  sourceKind: SourceKind | null;
  groupOrder: number;
  /**
   * The renderer's own finished sentence for this line, composed from the display template and the
   * frozen magnitude. Present whenever the line came from a template; absent for a structural line,
   * which has no template. ⛔ **The browser never composes this** — that is the whole reason a
   * template lives in `item_display_template` and the magnitude formatting lives in
   * `ItemDisplayRenderer`.
   */
  rendered?: string;
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

export type ContainerView = {
  instanceId: string;
  kind: ContainerKind;
  header: ContainerHeader;
  /**
   * The level clause, each attribute clause, and the gate's refusal when there is one — all as
   * rendered lines, because that is what the card emits. I11's rule that a clause names WHICH number
   * gates lives in the line's own args (`need` / `unassisted` / `bonus` / `gates`), not in a shape
   * this file re-derives.
   */
  requirements: Pending<DisplayLine[]>;
  baseStats: DisplayLine[];
  implicit: Pending<DisplayLine[]>;
  affixes: Pending<DisplayLine[]>;
  enhancement: Pending<{ tier: number; nextMilestone?: DisplayLine }>;
  sockets: Pending<SocketsView>;
  set: Pending<SetView>;
  /** Plural: an item may carry more than one grant, and block 9 renders every one of them. */
  grantedAction: Pending<DisplayLine[]>;
  flavour?: string;
  /**
   * `meanRollQuality` is the renderer's own formatted percentage (`ItemDisplayRenderer.FormatPerMille`),
   * not a raw per-mille: block 11 computes the mean from the same per-atom read the bars above it use
   * and formats it there, and parsing that string back into a number here to re-format it would be
   * the second implementation the one-producer rule forbids. Absent when nothing on the item rolled.
   */
  footer: Pending<{ meanRollQuality?: string; stale: boolean; locked: boolean }>;
};

// ===========================================================================
// 2b. Item surfaces — the six surfaces of `spec-item-surfaces.md` (item module
//     20), over the read-only `GET /api/items/*` routes.
//
// Every number here is the server's. Nothing in this section is derived in
// TypeScript: distances come from module 16's evaluator through
// `CombinationDistance`, verdicts and unit-class grouping from
// `DominancePresentation`, per-piece set truth from `SetDisclosure`. A view
// type that carried a formula instead of a value would be the second
// implementation those modules exist to prevent.
// ===========================================================================

/** `SurfaceCatalog.Id` — closed, and a seventh needs a declared unlock before it can exist. */
export type ItemSurfaceId =
  | "armoury"
  | "equipScreen"
  | "itemCard"
  | "comparison"
  | "socketBench"
  | "compendium";

/**
 * GG-17's four designed states plus the named populated case. `ready` is named rather than implied
 * so a surface cannot be written with three states and a fourth nobody handled.
 */
export type ItemSurfaceState = "locked" | "loading" | "empty" | "error" | "ready";

export type SurfaceStatusView = {
  surface: ItemSurfaceId;
  state: ItemSurfaceState;
  /** Always present, so a locked surface can always say what unlocks it (GG-44). */
  unlockKey: string;
};

/** GG-50's three bands. No band refuses a row — they differ only in how much reaches the DOM. */
export type RenderStrategy = "renderAll" | "virtualize" | "searchFirst";

export type ArmouryRowView = {
  instanceId: string;
  containerId: string;
  rarity: Rarity;
  /**
   * Both live on the item's BASE TYPE, and that table does not exist yet — so they are `pending`
   * with a player-facing reason rather than answered from the container's slot, which is a
   * different axis and would be a plausible wrong answer.
   */
  role: Pending<string>;
  frame: Pending<string>;
  assigned: boolean;
  locked: boolean;
  unseen: boolean;
  stale: boolean;
  acquiredUtc: string;
  /**
   * item-content `granted-action-text` (T15) — card block 9's compact-line half
   * (`ssot-presentation.md` §9.14): the item grants a battle-only action, so it is inert on the lawn.
   * A plain answer, not `Pending`: the grant rows are on the wire, unlike `role`/`frame` above.
   */
  battleOnly: boolean;
};

/** The inbox is counted over the WHOLE armoury, never the page. */
export type ArmouryInboxView = {
  unseen: Magnitude; // count
  total: Magnitude; // count
  /** A watch number, never a refusal — no row is hidden when it fires. */
  overReviewPressure: boolean;
};

export type ArmouryPageView = {
  inbox: ArmouryInboxView;
  strategy: RenderStrategy;
  rows: ArmouryRowView[];
};

/** `ArmourySortKey` — module 2's own sort axes, minus the ones whose column is not on the wire. */
export type ArmourySortKey = "acquired" | "rarity" | "assigned" | "locked" | "unseen";

/**
 * The loot filter is a client-side VIEW rule over an already-materialised row list — never a
 * server-side throttle on generation, and never a bag cap wearing a layout name. A `locked` row is
 * never hidden by any combination of these.
 */
export type ArmouryFilterState = {
  rarityMin: number | null;
  rarityMax: number | null;
  unseenOnly: boolean;
  hideAssigned: boolean;
  hideStale: boolean;
  sort: ArmourySortKey;
};

export type CombinationShape = "strain" | "splice" | "pure" | "ring" | "eclipse" | "diversity";

/** The four closed states, in the order the compendium renders them. */
export type CombinationState = "active" | "one-away" | "known-inactive" | "undiscovered";

export type CombinationView = {
  comboId: string;
  shape: CombinationShape;
  state: CombinationState;
  /** `null` is ∞ — unreachable on this item, which is `undiscovered` and never `one-away`. */
  distance: number | null;
  /** Named families for an authored recipe; empty for a generated resonance. */
  missingFamilies: string[];
  /** The other half: which elements a generated resonance's fill still lacks. */
  missingElements: string[];
  grantedTier: number;
};

export type SocketCellView = {
  index: number;
  affinity: string | null;
  /** `null` is an empty cell — room, not an ingredient. */
  insertName: string | null;
  /** An omni insert counts toward Diversity only, and the cell has to say so. */
  omniCountsDiversityOnly: boolean;
};

export type SocketsView = {
  cells: SocketCellView[];
  combinations: CombinationView[];
};

export type SetTierView = {
  /** A threshold names the piece count it needs, never "next". */
  piecesRequired: number;
  active: boolean;
  isCapability: boolean;
};

export type SetView = {
  setId: string;
  name: string;
  count: number;
  total: number;
  /** The whole ladder always renders — inactive thresholds are the goal. */
  ladder: SetTierView[];
  /** Sets this piece advances, and sets where an earlier piece already claimed its role. */
  advances: string[];
  redundantIn: string[];
};

/**
 * Which sets one worn piece advances, and which it is silently redundant in. A disclosure, never a
 * refusal — wearing the second copy stays legal, and the point is that the card says why the
 * fourth piece did not count. One shipped item routinely advances more than one set.
 */
export type PieceSetDisclosureView = {
  instanceId: string;
  itemName: string;
  advances: string[];
  redundantIn: string[];
};

/**
 * The six verbs the workbench executes (item modules 14/15/16), as
 * `POST /api/items/workbench/{verb}` names them. A closed list: `forge`, `reroll` and `transfer`
 * are deliberately absent because no route serves them — see `Workbench.tsx`'s unavailable list,
 * which names each one's real reason rather than showing a control that could only fail.
 */
export type WorkbenchVerb =
  | "salvage"
  | "upcycle"
  | "enhance"
  | "socket-add"
  | "socket-insert"
  | "socket-imbue";

/** One resolved cost or yield line. The quantity is the server's own resolved price, never a guess. */
export type WorkbenchCostView = {
  /** `Substrate` | `Catalyst` | `Reagent` — the material class the server named. */
  materialClass: string;
  materialId: string;
  qty: Magnitude; // count
};

/**
 * One socket after the operation, exactly as the workbench reported it. Deliberately NOT
 * `SocketCellView`: that view carries `omniCountsDiversityOnly`, which this payload does not
 * answer, and inventing a value for it would be the fabricated-field defect the contract exists to
 * prevent.
 */
export type WorkbenchSocketView = {
  index: number;
  /** `null` when the socket declares no element affinity. */
  affinity: string | null;
  crafted: boolean;
  /** `null` is an empty socket — room, not an ingredient. */
  insertContainerId: string | null;
};

/**
 * What one workbench operation did. ⛔ **Every magnitude here is the server's**: the spend, the
 * yield, the level and the roll chance all arrive resolved, and nothing in the client composes one.
 *
 * ⚠ `ok` is about whether the operation RAN, never about whether it went the player's way — a
 * failed enhance is `ok: true` with `outcome: "failure"`, because the materials were still spent
 * and the pity counter still moved. Rendering those two the same way is the defect the split
 * between `ok` and `outcome` exists to stop.
 */
export type WorkbenchOutcomeView = {
  ok: boolean;
  verb: WorkbenchVerb;
  /** The server's named rule when it refused; empty otherwise. Never rewritten in the client. */
  reason: string;
  instanceId: string;
  recipeId: string;
  opSeq: number;
  /** A retried correlation returns the RECORDED outcome; nothing is re-priced or re-rolled. */
  replayed: boolean;
  /** `success` | `failure` | `failure-downgrade` | `salvaged` | `upcycled` | the verb, for sockets. */
  outcome: string;
  enhanceLevel: Magnitude; // count
  pityCounter: Magnitude; // count
  /** `null` for every verb that rolls nothing — an absent chance, never a zero one. */
  successChance: Magnitude | null; // perMilleRatio
  spent: WorkbenchCostView[];
  granted: WorkbenchCostView[];
  sockets: WorkbenchSocketView[];
};

export type DominanceVerdict = "strictly-better" | "strictly-worse" | "sidegrade" | "incomparable";

/** A word AND a shape, never a colour alone — and the badge carries no colour to fall back on. */
export type VerdictBadgeView = {
  verdict: DominanceVerdict;
  label: string;
  shape: string;
};

export type ChannelDeltaView = {
  channel: ChannelId;
  incumbent: Magnitude;
  candidate: Magnitude;
  delta: Magnitude;
};

/** The unit lives in the GROUP HEADER, never in the column. An unresolvable unit gets its own group. */
export type UnitClassGroupView = {
  unit: UnitClass | null;
  deltas: ChannelDeltaView[];
};

export type SidegradeTradeView = {
  youGain: ChannelDeltaView[];
  youGiveUp: ChannelDeltaView[];
};

export type ComparePayloadView = {
  badge: VerdictBadgeView;
  groups: UnitClassGroupView[];
  /** Non-null only for a sidegrade: the verdict word alone says it is a trade, not which trade. */
  trade: SidegradeTradeView | null;
  /** Non-null only for an incomparable verdict — one with no explanation reads as a bug. */
  incomparableReason: string | null;
  meanRollQualityPerMille: number | null;
};

/** `core.v1.json`'s fifteen body roles plus the reserved commander slot. Closed, append-only. */
export type ItemRoleId =
  | "armament-primary"
  | "core-guard"
  | "ward-array"
  | "armament-secondary"
  | "jewel-major"
  | "manipulator"
  | "mantle"
  | "head-guard"
  | "girdle"
  | "sense"
  | "footing"
  | "infusion"
  | "retinue"
  | "jewel-minor-a"
  | "jewel-minor-b"
  | "standard";

export type PaperdollCellView = {
  role: ItemRoleId;
  /** The registry's own two frame nouns, both shown while no frame reaches the wire. */
  humanoidName: string;
  plantName: string;
  hybridEligible: boolean;
  /** `null` is an empty role, which is a designed state and not a blank. */
  instanceId: string | null;
  itemName: string | null;
  rarity: Rarity | null;
  /**
   * Which flow put this piece here — `"item"` for an owned instance assigned through
   * `POST /api/items/equip`, `"relic"` for one of the four hand-authored relics put there by
   * `PUT /api/unique/actors/{id}/equipment/{slot}`. `null` on an empty cell.
   *
   * ⛔ Not decoration: only an item cell may be taken off from the equip screen. A relic's write
   * path also rebuilds `mods_json` and its atom bindings, so unequipping one anywhere else would
   * leave both standing.
   */
  source: "item" | "relic" | null;
};

/**
 * One durable equip decision, as the equip screen reads it. The role is the fifteen-role vocabulary,
 * not the relic layer's three legacy slot words.
 */
export type EquipAssignmentView = {
  role: ItemRoleId;
  /** `"item"` is a rolled instance the player owns; `"relic"` is a catalog id from the older flow. */
  source: "item" | "relic";
  /** An `instance_id` for an item, a `container_id` for a relic. */
  refId: string;
  assignedUtc: string;
};

/**
 * What one equip or unequip did.
 *
 * ⚠ Unlike {@link WorkbenchOutcomeView} there is no second `outcome` axis, because equipping rolls
 * nothing: `ok` is the whole answer, and `reason` carries the server's named rule when it is false.
 */
export type EquipOutcomeView = {
  ok: boolean;
  verb: "equip" | "unequip";
  /** The server's named rule when it refused; empty otherwise. Never rewritten in the client. */
  reason: string;
  specimenId: string;
  role: ItemRoleId | null;
  refId: string;
  /** The piece this write displaced — swapped out on an equip, taken off on an unequip. */
  replaced: EquipAssignmentView | null;
  assignments: EquipAssignmentView[];
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

// ===========================================================================
// 11. Battle turn order — forecast-rail (battle-tempo FR2/FR3). Mirrors
//     FusionRpg.Core.Battle.Timeline.TurnOrderEntry field-for-field (parity
//     guarded — see ClassSystem/TurnOrderRecordContractParityTests.cs). A
//     RECORD of an already-resolved battle, never a live forecast (spec
//     spec-forecast-rail.md §2.0) — round + a display name only. No
//     actorKey, no tick numbers, no TurnState: engine vocabulary never
//     reaches this type (§2.4).
// ===========================================================================

export type TurnOrderEntry = {
  round: number;
  displayName: string;
};
