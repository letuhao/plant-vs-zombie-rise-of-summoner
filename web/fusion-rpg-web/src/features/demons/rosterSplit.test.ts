import { describe, expect, it } from "vitest";
import { ACTIVE_CAP, displayName, pityLine, splitRoster } from "./rosterSplit";
import type { DemonSpecimenDto } from "@/lib/bus/demons";

function specimen(
  id: string,
  speciesId: string,
  rarity: string,
  locked = false,
  createdUtc = "2026-08-21T00:00:00Z"
): DemonSpecimenDto {
  return {
    actor: { instanceId: id } as DemonSpecimenDto["actor"],
    profile: {
      instanceId: id,
      speciesId,
      rarity,
      variant: "normal",
      elementPrimary: "fire",
      traitIds: [],
      origin: "summon",
      locked,
      createdUtc,
      revision: 0
    }
  };
}

describe("splitRoster", () => {
  it("keeps everything active under the cap", () => {
    const items = [specimen("a", "imp", "common"), specimen("b", "hound", "rare")];
    const { active, reserve } = splitRoster(items);
    expect(active).toHaveLength(2);
    expect(reserve).toHaveLength(0);
  });

  it("locked specimens always sort into active first", () => {
    const items = [
      ...Array.from({ length: 30 }, (_, i) => specimen(`c${i}`, "imp", "common")),
      specimen("locked-common", "imp", "common", true)
    ];
    const { active } = splitRoster(items);
    expect(active[0].profile.instanceId).toBe("locked-common");
    expect(active).toHaveLength(ACTIVE_CAP);
  });

  it("rarity beats age for unlocked specimens", () => {
    const items = [
      specimen("old-common", "imp", "common", false, "2026-01-01T00:00:00Z"),
      specimen("new-legendary", "dragon", "legendary", false, "2026-08-01T00:00:00Z")
    ];
    const { active } = splitRoster(items);
    expect(active[0].profile.instanceId).toBe("new-legendary");
  });

  it("reserve stacks by species with counts, largest first", () => {
    const items = [
      ...Array.from({ length: ACTIVE_CAP }, (_, i) => specimen(`keep${i}`, "elite", "epic")),
      ...Array.from({ length: 7 }, (_, i) => specimen(`imp${i}`, "imp", "common")),
      ...Array.from({ length: 3 }, (_, i) => specimen(`wisp${i}`, "wisp", "common"))
    ];
    const { active, reserve } = splitRoster(items);
    expect(active).toHaveLength(ACTIVE_CAP);
    expect(reserve.map((r) => [r.speciesId, r.count])).toEqual([
      ["imp", 7],
      ["wisp", 3]
    ]);
  });
});

describe("pityLine", () => {
  it("renders both counters", () => {
    expect(pityLine(12, 25, 31, 55)).toBe("12/25 to guaranteed Epic · 31/55 to guaranteed Legendary");
  });
});

describe("displayName", () => {
  it("prefers nickname, then species name, then id", () => {
    const s = specimen("x", "hell-hound", "rare");
    expect(displayName(s, "Hell Hound")).toBe("Hell Hound");
    s.profile.nickname = "Ragnar";
    expect(displayName(s, "Hell Hound")).toBe("Ragnar");
    expect(displayName(specimen("y", "unknown-species", "common"))).toBe("unknown-species");
  });
});
