/**
 * The rail is identical on every stage (information-architecture.md §3):
 * Sanctum (the travel-home affordance) then the seven layers. It renders
 * from state — never a constant list (GG-44) — so this module is the one
 * place unlock derivation happens; the component just renders what this
 * returns.
 */
export type RailLayerId =
  | "creatures"
  | "relics"
  | "fusion"
  | "pacts"
  | "expeditions"
  | "almanac"
  | "chronicle"
  | "aptitudes";

export type RailEntryState = "active" | "available" | "badged" | "locked";

export type RailEntry = {
  id: "sanctum" | RailLayerId;
  label: string;
  key: string;
  state: RailEntryState;
  /** Required when state is "locked" — GG-17: say what unlocks it. */
  lockedReason?: string;
  badgeCount?: number;
};

export type RailUnlockInputs = {
  currentStageId: "sanctum" | "world" | "lawn" | "battle";
  hasCompletedARun: boolean;
  /** T15: fusion is real demon fusion (spec-demon-fusion.md), not creature fusion — star merge
   * and promotion both need at least one demon in the roster (recipe fusion needs two, but the
   * lab itself is reachable with one so a player can see what it needs). */
  hasAnyDemon: boolean;
  hasAnyContract: boolean;
  /** T14: the relic catalog is real but has no acquisition system yet, so every player holds
   * it in full — this is always true today, but still threaded through real query data rather
   * than hardcoded, so it stays correct once holding a relic becomes an earned event. */
  hasAnyRelic: boolean;
  /** T17: expeditions field demons, not creatures — real requirement is a bound demon (World's
   * "held sector" never applied; the previous condition was wrong-domain, not just hard to
   * live-demo, and has been replaced). */
  hasAnyBoundDemon: boolean;
  /** T17: dispatched expeditions whose due time has passed but aren't collected yet — GG-53's
   * rail badge. */
  returnedExpeditionCount: number;
  unreadResultCount: number;
};

const UNLOCK_LADDER: Record<RailLayerId, { label: string; key: string; reason: string }> = {
  creatures: { label: "Creatures", key: "C", reason: "Unlocks at session start" },
  relics: { label: "Relics", key: "R", reason: "Unlocks when you hold your first item" },
  fusion: { label: "Fusion", key: "F", reason: "Unlocks once you have a demon to fuse" },
  pacts: { label: "Pacts", key: "P", reason: "Unlocks when a contract is first offered" },
  expeditions: { label: "Expeditions", key: "E", reason: "Unlocks once you have a bound demon to field" },
  almanac: { label: "Almanac", key: "A", reason: "Unlocks after your first run" },
  chronicle: { label: "Chronicle", key: "H", reason: "Unlocks after your first run" },
  aptitudes: { label: "Primary Stats", key: "S", reason: "Unlocks at session start" }
};

function isUnlocked(id: RailLayerId, inputs: RailUnlockInputs): boolean {
  switch (id) {
    case "creatures":
      return true; // GG-44: unlocks at session start, same as Sanctum
    case "aptitudes":
      return true; // same reasoning as creatures -- a foundational system, not an earned one
    case "relics":
      return inputs.hasAnyRelic;
    case "fusion":
      return inputs.hasAnyDemon;
    case "pacts":
      return inputs.hasAnyContract;
    case "expeditions":
      return inputs.hasAnyBoundDemon;
    case "almanac":
    case "chronicle":
      return inputs.hasCompletedARun;
  }
}

export function deriveRailEntries(inputs: RailUnlockInputs): RailEntry[] {
  const sanctum: RailEntry = {
    id: "sanctum",
    label: "Sanctum",
    key: "M",
    state: inputs.currentStageId === "sanctum" ? "active" : "available"
  };

  const layers: RailEntry[] = (Object.keys(UNLOCK_LADDER) as RailLayerId[]).map((id) => {
    const { label, key, reason } = UNLOCK_LADDER[id];
    if (!isUnlocked(id, inputs)) {
      return { id, label, key, state: "locked", lockedReason: reason };
    }
    const badgeCount =
      id === "chronicle" ? inputs.unreadResultCount : id === "expeditions" ? inputs.returnedExpeditionCount : 0;
    if (badgeCount > 0) {
      return { id, label, key, state: "badged", badgeCount };
    }
    return { id, label, key, state: "available" };
  });

  return [sanctum, ...layers];
}
