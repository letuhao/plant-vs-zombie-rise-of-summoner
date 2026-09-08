import { describe, expect, it } from "vitest";
import { actorSurfaceFixture, derivedSurfaceFromFixture } from "@/lib/bus/actorSurface";
import { formatDerivedMagnitude } from "./cook/formatDerivedMagnitude";
import { foldDerivedSurfaceVm } from "./foldDerivedSurfaceVm";

describe("formatDerivedMagnitude", () => {
  it("formats channel totals without a leading +", () => {
    const m = formatDerivedMagnitude(2847, "GameUnits", { role: "total" });
    expect(m.valueText).toBe("2,847");
    expect(m.valueText.startsWith("+")).toBe(false);
  });

  it("keeps signed delta for contribution lines", () => {
    const m = formatDerivedMagnitude(980, "GameUnits", { role: "delta" });
    expect(m.valueText).toMatch(/^\+9,?80$/);
  });
});

describe("foldDerivedSurfaceVm", () => {
  it("builds chips, families, and unsigned total valueText", () => {
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
    expect(vm.primaryRail.chips.map((c) => c.id)).toEqual([
      "elements",
      "status",
      "resources",
      "other"
    ]);
    expect(vm.primaryRail.chips.some((c) => c.id === "elements" && c.selected)).toBe(true);
    expect(vm.variantRail.chips.some((c) => c.id === "fire")).toBe(true);
    const fireChip = vm.variantRail.chips.find((c) => c.id === "fire");
    expect(fireChip?.themeResolved?.glyphDefault).toBe("flame");
    expect(fireChip?.themeResolved?.vfx?.select).toBe("vfx.ember-pulse");
    expect((vm as { identity?: unknown }).identity).toBeUndefined();
    const row = vm.families
      .flatMap((f) => f.rows as { channelId: string; valueText: string; selected: boolean }[])
      .find((r) => r.channelId === "combat.power.fire");
    expect(row?.selected).toBe(true);
    expect(row?.valueText).toBe("2,847");
    expect(vm.inspect.hero.valueText).toBe("2,847");
    const bars = vm.inspect.stack.bars as { valueText: string }[];
    expect(bars[0]?.valueText.startsWith("+")).toBe(true);
    expect(vm.inspect.donut.slices).toBeTruthy();
    expect((vm.inspect.donut.slices as { paint: string }[])[0]?.paint).toMatch(/^#/);
    expect(vm.familyList.count).toBeGreaterThan(0);
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
    expect(vm.phasePayload.message).toMatch(/Loading/);
  });

  it("maps error availability to phase error with retry", () => {
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
      availability: "error"
    });
    expect(vm.phase).toBe("error");
    expect(vm.phasePayload.canRetry).toBe(true);
    expect(vm.phasePayload.retryLabel).toBe("Retry");
  });

  it("zero visible rows → empty phase and familyList.count 0", () => {
    const surface = actorSurfaceFixture();
    const cook = derivedSurfaceFromFixture(surface);
    const vm = foldDerivedSurfaceVm({
      identity: { displayName: "X", level: 1, side: "plant" },
      cookTabs: cook.tabs,
      elements: surface.elements,
      sheetChannels: [],
      ui: {
        tabId: "elements",
        variantId: "fire",
        query: "zzz-no-match",
        showUnchanged: false,
        selectedChannelId: null
      },
      availability: "ready"
    });
    expect(vm.phase).toBe("empty");
    expect(vm.familyList.count).toBe(0);
    expect(vm.families).toHaveLength(0);
  });

  it("resource variants resolve resource theme packs", () => {
    const surface = actorSurfaceFixture();
    const cook = derivedSurfaceFromFixture(surface);
    const vm = foldDerivedSurfaceVm({
      identity: { displayName: "X", level: 1, side: "plant" },
      cookTabs: cook.tabs,
      elements: surface.elements,
      ui: {
        tabId: "resources",
        variantId: "hp",
        query: "",
        showUnchanged: true,
        selectedChannelId: null
      },
      availability: "ready"
    });
    const hp = vm.variantRail.chips.find((c) => c.id === "hp");
    expect(hp?.themeResolved?.themeId).toBe("resource.hp");
    expect(hp?.themeResolved?.glyphDefault).toBe("heart");
    expect(vm.themeResolved?.themeId).toBe("resource.hp");
  });

  it("status rail cooks Omni + every status-catalog chip", () => {
    const surface = actorSurfaceFixture();
    const cook = derivedSurfaceFromFixture(surface);
    const butterRow = surface.statuses.find((s) => s.id === "butter")!;
    const vm = foldDerivedSurfaceVm({
      identity: { displayName: "X", level: 1, side: "plant" },
      cookTabs: cook.tabs,
      elements: surface.elements,
      statuses: surface.statuses,
      ui: {
        tabId: "status",
        variantId: "butter",
        query: "",
        showUnchanged: true,
        selectedChannelId: null
      },
      availability: "ready"
    });
    const ids = vm.variantRail.chips.map((c) => c.id);
    expect(ids[0]).toBe("omni");
    expect(ids).toHaveLength(25);
    expect(ids).toContain("butter");
    expect(ids).toContain("nerve.afflicted");
    expect(vm.variantRail.ariaLabel).toBe("Status catalog variants");
    const butter = vm.variantRail.chips.find((c) => c.id === "butter");
    expect(butter?.themeResolved?.glyphDefault).toBe("ban");
    expect(butter?.themeResolved?.paint.accent).toBe(butterRow.color);
    expect((butter as { glyphRef?: { hudToken?: string } })?.glyphRef?.hudToken).toBe(
      butterRow.hudToken
    );
  });
});
