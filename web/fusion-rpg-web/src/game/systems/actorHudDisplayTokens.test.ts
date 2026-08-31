import { describe, expect, it } from "vitest";
import {
  STATUS_STRIP_MAX,
  statusInitials,
  tierBadgeLetter
} from "./actorHudDisplayTokens";

describe("actorHudDisplayTokens", () => {
  it("STATUS_STRIP_MAX mirrors tuning statusStripMax", () => {
    expect(STATUS_STRIP_MAX).toBe(3);
  });

  it("statusInitials uses two-letter tokens for fold status ids", () => {
    expect(statusInitials("command")).toBe("CO");
    expect(statusInitials("expose")).toBe("EX");
  });

  it("tierBadgeLetter maps tier to canvas badge letter", () => {
    expect(tierBadgeLetter("normal")).toBe("");
    expect(tierBadgeLetter("elite")).toBe("E");
    expect(tierBadgeLetter("boss")).toBe("B");
    expect(tierBadgeLetter("unique")).toBe("U");
  });
});
