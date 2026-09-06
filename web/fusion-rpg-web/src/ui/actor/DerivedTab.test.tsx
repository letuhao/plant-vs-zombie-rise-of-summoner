import { describe, expect, it } from "vitest";
import { actorSurfaceFixture } from "@/lib/bus/actorSurface";
import { expandDerivedFamily } from "./DerivedTab";

describe("expandDerivedFamily", () => {
  const surface = actorSurfaceFixture();

  it("expands combat families over omni + every concrete element", () => {
    const family = surface.families.find((row) => row.family === "combat.power")!;
    const expanded = expandDerivedFamily(family, surface.elements);
    expect(expanded.map((row) => row.channelId)).toEqual([
      "combat.power.omni",
      "combat.power.fire",
      "combat.power.ice",
      "combat.power.air",
      "combat.power.earth",
      "combat.power.light",
      "combat.power.dark"
    ]);
  });

  it("leaves non-combat families as a single channel id", () => {
    const family = surface.families.find((row) => !row.family.startsWith("combat."))!;
    const expanded = expandDerivedFamily(family, surface.elements);
    expect(expanded).toEqual([{ channelId: family.family, element: null }]);
  });
});
