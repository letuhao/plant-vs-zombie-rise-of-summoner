import type { ContractRowDto } from "@/lib/bus/contracts";
import type { DemonProfileDto } from "@/lib/bus/demons";
import type { RelicDto, RunItem, UniqueActorDto } from "@/lib/bus/types";
import { absent, pendingWithReason, type Pending } from "./pending";
import type { ActorPhase, ActorView, ContainerView, ContractView, Rarity, RunResult, RunView } from "./types";

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
  summonerLevel: "Summoner rank isn't tracked yet"
} as const;

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
 */
const RELIC_RARITY_LADDER: { id: Rarity["id"]; ordinal: number; display: string; colour: string; pips: number }[] = [
  { id: "chaff", ordinal: 10, display: "Chaff", colour: "var(--color-rarity-chaff)", pips: 1 },
  { id: "sprout", ordinal: 20, display: "Sprout", colour: "var(--color-rarity-sprout)", pips: 2 },
  { id: "grafted", ordinal: 30, display: "Grafted", colour: "var(--color-rarity-grafted)", pips: 3 },
  { id: "cultivated", ordinal: 40, display: "Cultivated", colour: "var(--color-rarity-cultivated)", pips: 4 }
];

function rarityFromRelicTier(tier: number): Rarity {
  const clamped = Math.min(Math.max(Math.trunc(tier), 1), RELIC_RARITY_LADDER.length);
  return RELIC_RARITY_LADDER[clamped - 1]!;
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
