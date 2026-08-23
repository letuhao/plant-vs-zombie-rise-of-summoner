import { describe, expect, it } from "vitest";
import { deriveRailEntries, type RailUnlockInputs } from "./railState";

const allLocked: RailUnlockInputs = {
  currentStageId: "sanctum",
  hasCompletedARun: false,
  hasAnyDemon: false,
  hasAnyContract: false,
  hasAnyRelic: false,
  hasAnyBoundDemon: false,
  returnedExpeditionCount: 0,
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

  it("Relics unlocks once the player holds at least one relic (T14)", () => {
    expect(deriveRailEntries(allLocked).find((e) => e.id === "relics")!.state).toBe("locked");
    expect(deriveRailEntries({ ...allLocked, hasAnyRelic: true }).find((e) => e.id === "relics")!.state).toBe(
      "available"
    );
  });

  it("Fusion unlocks once the player has a demon to fuse (T15)", () => {
    expect(deriveRailEntries(allLocked).find((e) => e.id === "fusion")!.state).toBe("locked");
    const entries = deriveRailEntries({ ...allLocked, hasAnyDemon: true });
    expect(entries.find((e) => e.id === "fusion")!.state).toBe("available");
  });

  it("Pacts unlocks on any contract existing", () => {
    const entries = deriveRailEntries({ ...allLocked, hasAnyContract: true });
    expect(entries.find((e) => e.id === "pacts")!.state).toBe("available");
  });

  it("Expeditions unlocks once the player has a bound demon to field (T17)", () => {
    expect(deriveRailEntries(allLocked).find((e) => e.id === "expeditions")!.state).toBe("locked");
    const entries = deriveRailEntries({ ...allLocked, hasAnyBoundDemon: true });
    expect(entries.find((e) => e.id === "expeditions")!.state).toBe("available");
  });

  it("Expeditions badges with the returned-but-uncollected count once unlocked", () => {
    const entries = deriveRailEntries({ ...allLocked, hasAnyBoundDemon: true, returnedExpeditionCount: 2 });
    const expeditions = entries.find((e) => e.id === "expeditions")!;
    expect(expeditions.state).toBe("badged");
    expect(expeditions.badgeCount).toBe(2);
  });

  it("a returned-expedition count does not badge a still-locked Expeditions", () => {
    const entries = deriveRailEntries({ ...allLocked, returnedExpeditionCount: 2 });
    expect(entries.find((e) => e.id === "expeditions")!.state).toBe("locked");
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
