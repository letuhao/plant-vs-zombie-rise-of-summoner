import { describe, expect, it } from "vitest";
import { actorSurfaceFixture } from "@/lib/bus/actorSurface";
import { expandDerivedFamily } from "./DerivedTab";

describe("expandDerivedFamily", () => {
  const surface = actorSurfaceFixture();

  it("expands element families over omni + every concrete element", () => {
    const family = surface.families.find((row) => row.family === "combat.power")!;
    expect(family.expand).toBe("element");
    const expanded = expandDerivedFamily(family, surface.elements, surface.resources);
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

  it("expands status-category families over omni/dot/cc/contagion", () => {
    const family = surface.families.find((row) => row.family === "status.resist")!;
    expect(family.expand).toBe("status-category");
    const expanded = expandDerivedFamily(family, surface.elements, surface.resources);
    expect(expanded.map((row) => row.channelId)).toEqual([
      "status.resist.omni",
      "status.resist.dot",
      "status.resist.cc",
      "status.resist.contagion"
    ]);
  });

  it("expands resource families over resource-catalog ids", () => {
    const family = surface.families.find((row) => row.family === "resource.max")!;
    expect(family.expand).toBe("resource");
    const expanded = expandDerivedFamily(family, surface.elements, surface.resources);
    expect(expanded.map((row) => row.channelId)).toEqual(
      surface.resources.map((r) => `resource.max.${r.id}`)
    );
  });

  it("leaves expand:none families as a single channel id", () => {
    const family = surface.families.find((row) => row.family === "progression.power")!;
    expect(family.expand).toBe("none");
    const expanded = expandDerivedFamily(family, surface.elements, surface.resources);
    expect(expanded).toEqual([{ channelId: "progression.power", element: null }]);
  });
});
