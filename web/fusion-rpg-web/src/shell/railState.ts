/**
 * The rail is identical on every stage (information-architecture.md §3):
 * Sanctum (the travel-home affordance) then the seven layers. It renders
 * from state — never a constant list (GG-44) — so this module is the one
 * place unlock derivation happens; the component just renders what this
 * returns.
 */
export type RailLayerId = "creatures" | "relics" | "fusion" | "pacts" | "expeditions" | "almanac" | "chronicle";

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
  hasDuplicateSpecies: boolean;
  hasAnyContract: boolean;
  /** Not derivable without World (T16, excluded this phase) — always locked honestly until then. */
  hasHeldASector: boolean;
  unreadResultCount: number;
};

const UNLOCK_LADDER: Record<RailLayerId, { label: string; key: string; reason: string }> = {
  creatures: { label: "Creatures", key: "C", reason: "Unlocks at session start" },
  relics: { label: "Relics", key: "R", reason: "Unlocks when you hold your first item" },
  fusion: { label: "Fusion", key: "F", reason: "Unlocks with a second creature of one species" },
  pacts: { label: "Pacts", key: "P", reason: "Unlocks when a contract is first offered" },
  expeditions: { label: "Expeditions", key: "E", reason: "Unlocks when you hold your first sector" },
  almanac: { label: "Almanac", key: "A", reason: "Unlocks after your first run" },
  chronicle: { label: "Chronicle", key: "H", reason: "Unlocks after your first run" }
};

function isUnlocked(id: RailLayerId, inputs: RailUnlockInputs): boolean {
  switch (id) {
    case "creatures":
      return true; // GG-44: unlocks at session start, same as Sanctum
    case "relics":
      return false; // no container/inventory endpoint yet — see game-gui-map.md's contract gaps
    case "fusion":
      return inputs.hasDuplicateSpecies;
    case "pacts":
      return inputs.hasAnyContract;
    case "expeditions":
      return inputs.hasHeldASector;
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
    if (id === "chronicle" && inputs.unreadResultCount > 0) {
      return { id, label, key, state: "badged", badgeCount: inputs.unreadResultCount };
    }
    return { id, label, key, state: "available" };
  });

  return [sanctum, ...layers];
}
