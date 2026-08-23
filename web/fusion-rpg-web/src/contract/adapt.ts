import type { ContractRowDto } from "@/lib/bus/contracts";
import type { DemonProfileDto } from "@/lib/bus/demons";
import type { RunItem, UniqueActorDto } from "@/lib/bus/types";
import { absent, pendingWithReason, type Pending } from "./pending";
import type { ActorPhase, ActorView, ContractView, RunResult, RunView } from "./types";

/**
 * The DTO→view adapter (T4). Filling a field later touches one file: this
 * one. No component, test fixture shape, or layer changes when a `pending`
 * becomes `known` (game-gui-map.md's contract section).
 */

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
    displayName: pendingWithReason("Names resolve from the almanac catalog, not wired to this reader yet"),
    phase: toActorPhase(dto.phase),
    level: dto.level,
    xp: dto.xp,
    xpToNext: pendingWithReason("The XP curve endpoint isn't joined to this reader yet"),
    revision: dto.revision,
    channelSummary: pendingWithReason("The derived-stat snapshot has no server endpoint yet (spec-derived-stat-sheet.md)"),
    elementTyping: pendingWithReason("Element typing isn't exposed on UniqueActorDto yet"),
    shieldStack: pendingWithReason("Shield instances aren't exposed on UniqueActorDto yet"),
    equipSlots: pendingWithReason("Equip-slot unlock state isn't exposed on UniqueActorDto yet")
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
      : pendingWithReason("Run summary is an opaque payload — its shape isn't specced yet (match-runtime.md)")
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
    displayName: pendingWithReason("Species display name is in the catalog endpoint, not joined here yet")
  };
}

/** Every `pending` reason must be non-empty — the check T4's guard proves in tests. */
export function pendingReason<T>(p: Pending<T>): string | null {
  return p.state === "pending" ? p.reason : null;
}
