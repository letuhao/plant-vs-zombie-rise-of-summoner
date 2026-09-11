import { describe, expect, it } from "vitest";
import { actorSurfaceFixture } from "@/lib/bus/actorSurface";
import { foldAptitudesSurfaceVm } from "./foldAptitudesSurfaceVm";

describe("foldAptitudesSurfaceVm", () => {
  const surface = actorSurfaceFixture();

  it("Mode A unique emits leftover + decision + Force displayName bands", () => {
    const draft: Record<string, number> = {};
    for (const row of surface.aptitudes) draft[row.id] = row.id === "Might" ? 10 : 0;
    const vm = foldAptitudesSurfaceVm({
      mode: "unique",
      surface,
      draftShares: draft,
      budget: 100,
      spent: 10,
      leftover: 90,
      dirty: true,
      withinBudget: true,
      saving: false,
      selectedAptitudeId: "Might",
      theta: 14,
      commanderAddOn: "Commander add-on",
      fedFamiliesByAptitudeId: {
        Might: [{ displayName: "Power" }, { displayName: "Crit" }]
      },
      availability: "ready",
      revision: 2
    });
    expect(vm.mode).toBe("unique");
    expect(vm.leftover.leftover).toBe(90);
    expect(vm.decision.dirty).toBe(true);
    expect(vm.scopeChip.title).toBe("Unique specimen");
    expect(vm.scopeChip.commanderAddOn).toBe("Commander add-on");
    expect(vm.bands[0]?.displayName).toBe("Force");
    expect(vm.inspect.fedFamilies).toEqual([{ displayName: "Power" }, { displayName: "Crit" }]);
    const tile = (vm.bands[0]?.tiles as { icon?: string }[])?.[0];
    expect(tile?.icon).toBeTruthy();
  });

  it("Mode C commander scope fiction differs", () => {
    const draft: Record<string, number> = {};
    for (const row of surface.aptitudes) draft[row.id] = 0;
    const vm = foldAptitudesSurfaceVm({
      mode: "commander",
      surface,
      draftShares: draft,
      budget: 300,
      spent: 0,
      leftover: 300,
      dirty: false,
      withinBudget: true,
      saving: false,
      selectedAptitudeId: null,
      theta: 100,
      availability: "ready"
    });
    expect(vm.scopeChip.title).toBe("Commander");
    expect(vm.speciesChrome).toBeUndefined();
  });

  it("Mode B includes species chrome + price on decision when everRespecced", () => {
    const draft: Record<string, number> = {};
    for (const row of surface.aptitudes) draft[row.id] = 0;
    const vm = foldAptitudesSurfaceVm({
      mode: "species",
      surface,
      draftShares: draft,
      budget: 200,
      spent: 0,
      leftover: 200,
      dirty: true,
      withinBudget: true,
      saving: false,
      selectedAptitudeId: "Agility",
      theta: 20,
      speciesChrome: {
        hasOverride: true,
        priceAmount: 50,
        priceResource: "soul",
        everRespecced: true
      },
      availability: "ready"
    });
    expect(vm.speciesChrome?.hasOverride).toBe(true);
    expect(vm.decision.priceAmount).toBe(50);
    expect(vm.bands.find((b) => b.postureId === "finesse")?.displayName).toBe("Finesse");
  });
});
