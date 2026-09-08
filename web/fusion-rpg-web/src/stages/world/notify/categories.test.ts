import { describe, expect, it } from "vitest";
import { ALL_CATEGORIES, CATEGORY_DEFAULT_CHANNEL, TOAST_TIER } from "./categories";

describe("categories — the closed list and its default channels (world-stage W85)", () => {
  it("every category has a default channel", () => {
    for (const category of ALL_CATEGORIES) {
      expect(CATEGORY_DEFAULT_CHANNEL[category]).toBeDefined();
    }
    expect(ALL_CATEGORIES.length).toBeGreaterThan(0);
  });

  it("no category defaults to Toast unless it is in the declared top tier", () => {
    for (const category of ALL_CATEGORIES) {
      if (CATEGORY_DEFAULT_CHANNEL[category] === "toast") {
        expect(TOAST_TIER).toContain(category);
      }
    }
  });

  it("battle results default to the rail — the ES2 retraction", () => {
    expect(CATEGORY_DEFAULT_CHANNEL["battle.result"]).toBe("rail");
  });

  it("ground releasing next turn defaults to Toast", () => {
    expect(CATEGORY_DEFAULT_CHANNEL["loam.release"]).toBe("toast");
  });
});
