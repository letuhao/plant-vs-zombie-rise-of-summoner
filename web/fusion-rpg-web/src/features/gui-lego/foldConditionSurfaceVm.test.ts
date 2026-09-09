import { describe, expect, it } from "vitest";
import { known, pendingWithReason } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import { PLAYER_PENDING } from "@/contract/adapt";
import { actorSurfaceFixture } from "@/lib/bus/actorSurface";
import { foldConditionSurfaceVm } from "./foldConditionSurfaceVm";

function actor(): ActorView {
  return {
    instanceId: "a1",
    playerId: 1,
    side: "plant",
    typeId: 3,
    displayName: known("Emberling"),
    phase: "ActiveBound",
    level: 14,
    xp: 2140,
    xpToNext: pendingWithReason(PLAYER_PENDING.xpToNext),
    revision: 1,
    channelSummary: pendingWithReason(PLAYER_PENDING.channelSummary),
    elementTyping: pendingWithReason(PLAYER_PENDING.elementTyping),
    shieldStack: pendingWithReason(PLAYER_PENDING.shieldStack),
    equipSlots: pendingWithReason(PLAYER_PENDING.equipSlots)
  };
}

function baseSheet() {
  return {
    instanceId: "a1",
    playerId: 1,
    side: "plant" as const,
    typeId: 3,
    displayName: "Emberling",
    speciesName: "Sunflower",
    phase: "ActiveBound",
    level: 14,
    xp: 2140,
    xpToNext: 3400,
    elementTyping: { primary: "fire", secondary: "light" },
    standing: {
      offense: 77,
      survivability: 58,
      control: 36,
      utility: 26,
      economy: 49
    },
    resourcePools: [
      { resourceId: "hp", current: 1240, max: 1800 },
      { resourceId: "stamina", current: 44, max: 60 },
      { resourceId: "hunger", current: 12, max: 40 },
      { resourceId: "spirit", current: 22, max: 40 },
      { resourceId: "qi", current: 8, max: 24 },
      { resourceId: "poise", current: 18, max: 20 }
    ],
    liveStatuses: [] as { statusId: string }[],
    derived: [],
    primary: []
  };
}

describe("foldConditionSurfaceVm", () => {
  it("emits progression, identity, hero, stand in grid order", () => {
    const surface = actorSurfaceFixture();
    const vm = foldConditionSurfaceVm({
      data: actor(),
      sheet: null,
      surface,
      selectedPoolId: "hp",
      availability: "ready",
      revision: 1
    });
    expect(vm.main.map((m) => m.piece)).toEqual([
      "progression-gauge",
      "actor-identity",
      "cond-hero",
      "stand-row"
    ]);
    const hero = vm.main[2] as { meters?: { poolId: string }[] };
    expect(hero.meters).toHaveLength(surface.resources.length);
  });

  it("uses /sheet xpToNext and standing when available", () => {
    const vm = foldConditionSurfaceVm({
      data: actor(),
      sheet: baseSheet(),
      surface: actorSurfaceFixture(),
      selectedPoolId: "hp",
      availability: "ready",
      revision: 1
    });
    const prog = vm.main[0] as { fillPct?: number | null; message?: string | null; phase?: string };
    expect(prog.fillPct).toBeGreaterThan(0);
    expect(prog.message).toBeNull();
    expect(prog.phase).toBe("ready");

    const identity = vm.main[1] as { speciesName?: string; elements?: string[] };
    expect(identity.speciesName).toBe("Sunflower");
    expect(identity.elements).toEqual(["fire", "light"]);

    const stand = vm.main[3] as {
      bars?: { phase?: string; axes?: { id: string; value: number; fillPct: number }[] };
      statusStrip?: { phase?: string };
    };
    expect(stand.bars?.phase).toBe("ready");
    expect(stand.bars?.axes?.find((a) => a.id === "offense")?.value).toBe(77);
    expect(stand.bars?.axes?.find((a) => a.id === "offense")?.fillPct).toBe(100);
    expect(stand.statusStrip?.phase).toBe("empty");

    const hero = vm.main[2] as { radial?: { hpPct?: number | null }; meters?: { fillPct?: number | null }[] };
    expect(hero.radial?.hpPct).toBeGreaterThan(0);
    expect(hero.meters?.every((m) => m.fillPct != null)).toBe(true);
  });

  it("Standing pending never fabricates zero axes", () => {
    const vm = foldConditionSurfaceVm({
      data: actor(),
      sheet: null,
      surface: actorSurfaceFixture(),
      selectedPoolId: "hp",
      availability: "ready",
      revision: 1
    });
    const standing = vm.main[3] as {
      radar?: { phase?: string; axes?: unknown[]; message?: string };
      bars?: { phase?: string; axes?: unknown[] };
    };
    expect(standing.radar?.phase).toBe("pending");
    expect(standing.bars?.phase).toBe("pending");
    expect(standing.radar?.axes).toBeUndefined();
    expect(standing.bars?.axes).toBeUndefined();
  });

  it("sheet present with liveStatuses empty is honest empty on nested strip", () => {
    const vm = foldConditionSurfaceVm({
      data: actor(),
      sheet: { ...baseSheet(), liveStatuses: [] },
      surface: actorSurfaceFixture(),
      selectedPoolId: "hp",
      availability: "ready",
      revision: 1
    });
    const strip = (vm.main[3] as { statusStrip?: { phase?: string; message?: string } }).statusStrip;
    expect(strip?.phase).toBe("empty");
    expect(strip?.message).toMatch(/No live effects/i);
  });

  it("sheet null marks statuses pending/unwired", () => {
    const vm = foldConditionSurfaceVm({
      data: actor(),
      sheet: null,
      surface: actorSurfaceFixture(),
      selectedPoolId: "hp",
      availability: "ready",
      revision: 1
    });
    const strip = (vm.main[3] as { statusStrip?: { phase?: string; message?: string } }).statusStrip;
    expect(strip?.phase).toBe("pending");
    expect(strip?.message).toMatch(/not available yet/i);
  });
});
