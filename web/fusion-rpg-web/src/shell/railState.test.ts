import { describe, expect, it } from "vitest";
import { deriveRailEntries, type RailUnlockInputs } from "./railState";

const allLocked: RailUnlockInputs = {
  currentStageId: "sanctum",
  hasCompletedARun: false,
  hasDuplicateSpecies: false,
  hasAnyContract: false,
  hasHeldASector: false,
  unreadResultCount: 0
};

describe("deriveRailEntries — GG-44, renders from state", () => {
  it("returns Sanctum first, then the seven layers, in the fixed order", () => {
    const entries = deriveRailEntries(allLocked);
    expect(entries.map((e) => e.id)).toEqual([
      "sanctum",
      "creatures",
      "relics",
      "fusion",
      "pacts",
      "expeditions",
      "almanac",
      "chronicle"
    ]);
  });

  it("Sanctum is active on the sanctum stage and available elsewhere", () => {
    expect(deriveRailEntries(allLocked).find((e) => e.id === "sanctum")!.state).toBe("active");
    expect(
      deriveRailEntries({ ...allLocked, currentStageId: "lawn" }).find((e) => e.id === "sanctum")!.state
    ).toBe("available");
  });

  it("Creatures is unlocked from session start regardless of any other state", () => {
    expect(deriveRailEntries(allLocked).find((e) => e.id === "creatures")!.state).toBe("available");
  });

  it("every locked entry carries a non-empty reason (GG-17)", () => {
    const locked = deriveRailEntries(allLocked).filter((e) => e.state === "locked");
    expect(locked.length).toBeGreaterThan(0);
    for (const entry of locked) {
      expect(entry.lockedReason).toBeTruthy();
    }
  });

  it("Relics has no server-backed unlock signal yet and stays honestly locked", () => {
    const fullyPlayed: RailUnlockInputs = {
      currentStageId: "sanctum",
      hasCompletedARun: true,
      hasDuplicateSpecies: true,
      hasAnyContract: true,
      hasHeldASector: true,
      unreadResultCount: 0
    };
    expect(deriveRailEntries(fullyPlayed).find((e) => e.id === "relics")!.state).toBe("locked");
  });

  it("Fusion unlocks on a duplicate species", () => {
    const entries = deriveRailEntries({ ...allLocked, hasDuplicateSpecies: true });
    expect(entries.find((e) => e.id === "fusion")!.state).toBe("available");
  });

  it("Pacts unlocks on any contract existing", () => {
    const entries = deriveRailEntries({ ...allLocked, hasAnyContract: true });
    expect(entries.find((e) => e.id === "pacts")!.state).toBe("available");
  });

  it("Expeditions stays locked without a held sector (World excluded this phase)", () => {
    expect(deriveRailEntries(allLocked).find((e) => e.id === "expeditions")!.state).toBe("locked");
  });

  it("Almanac and Chronicle unlock together on the first completed run", () => {
    const entries = deriveRailEntries({ ...allLocked, hasCompletedARun: true });
    expect(entries.find((e) => e.id === "almanac")!.state).toBe("available");
    expect(entries.find((e) => e.id === "chronicle")!.state).toBe("available");
  });

  it("Chronicle badges with the unread result count once unlocked", () => {
    const entries = deriveRailEntries({ ...allLocked, hasCompletedARun: true, unreadResultCount: 3 });
    const chronicle = entries.find((e) => e.id === "chronicle")!;
    expect(chronicle.state).toBe("badged");
    expect(chronicle.badgeCount).toBe(3);
  });

  it("an unread count does not badge a still-locked Chronicle", () => {
    const entries = deriveRailEntries({ ...allLocked, unreadResultCount: 3 });
    expect(entries.find((e) => e.id === "chronicle")!.state).toBe("locked");
  });
});
