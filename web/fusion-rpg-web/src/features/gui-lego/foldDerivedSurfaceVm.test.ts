import { describe, expect, it } from "vitest";
import { actorSurfaceFixture, derivedSurfaceFromFixture } from "@/lib/bus/actorSurface";
import { foldDerivedSurfaceVm } from "./foldDerivedSurfaceVm";

describe("foldDerivedSurfaceVm", () => {
  it("builds chips, families, and magnitude valueText via formatMagnitude", () => {
    const surface = actorSurfaceFixture();
    const cook = derivedSurfaceFromFixture(surface);
    const vm = foldDerivedSurfaceVm({
      identity: { displayName: "Emberling", level: 24, side: "plant" },
      cookTabs: cook.tabs,
      elements: surface.elements,
      sheetChannels: [
        {
          channelId: "combat.power.fire",
          displayName: "Power",
          reading: "Fire power",
          composeKind: "FlatSum",
          value: 2847,
          contributions: [
            { sourceId: "equip:muzzle:ember", label: "Equip", op: "Flat", value: 980 },
            { sourceId: "aptitude.Might", label: "Might", op: "Flat", value: 185 }
          ]
        }
      ],
      ui: {
        tabId: "elements",
        variantId: "fire",
        query: "",
        showUnchanged: true,
        selectedChannelId: "combat.power.fire"
      },
      availability: "ready",
      revision: 1
    });

    expect(vm.phase).toBe("ready");
    expect(vm.primaryRail.chips.some((c) => c.id === "elements" && c.selected)).toBe(true);
    expect(vm.variantRail.chips.some((c) => c.id === "fire")).toBe(true);
    const row = vm.families
      .flatMap((f) => f.rows as { channelId: string; valueText: string; selected: boolean }[])
      .find((r) => r.channelId === "combat.power.fire");
    expect(row?.selected).toBe(true);
    expect(row?.valueText).toMatch(/\+?2,?847/);
    expect(vm.inspect.donut.slices).toBeTruthy();
    expect((vm.inspect.donut.slices as { paint: string }[])[0]?.paint).toMatch(/^#/);
  });

  it("maps loading availability to phase loading", () => {
    const surface = actorSurfaceFixture();
    const cook = derivedSurfaceFromFixture(surface);
    const vm = foldDerivedSurfaceVm({
      identity: { displayName: "X", level: 1, side: "plant" },
      cookTabs: cook.tabs,
      elements: surface.elements,
      ui: {
        tabId: "elements",
        variantId: "fire",
        query: "",
        showUnchanged: true,
        selectedChannelId: null
      },
      availability: "loading"
    });
    expect(vm.phase).toBe("loading");
  });
});
