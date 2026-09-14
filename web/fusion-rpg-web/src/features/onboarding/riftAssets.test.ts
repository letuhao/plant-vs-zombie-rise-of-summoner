import { describe, expect, it } from "vitest";
import { RIFT_ASSETS, RIFT_ASSET_VERSION } from "./riftAssets";

describe("Rift asset manifest", () => {
  it("keeps semantic roles stable and filename-independent", () => {
    expect(RIFT_ASSET_VERSION).toBe(1);
    expect(RIFT_ASSETS.storySprite.role).toBe("storySprite");
    expect(RIFT_ASSETS.icon.role).toBe("icon");
    expect(RIFT_ASSETS.storySprite.src).toContain("rift-portal-pvz-style.png");
    expect(RIFT_ASSETS.icon.src).toContain("rift-icon-pvz-style-64.png");
    expect(RIFT_ASSETS.storySprite.alt).toBeTruthy();
    expect(RIFT_ASSETS.storySprite.fallbackLabel).toBeTruthy();
  });
});
