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

describe("foldConditionSurfaceVm", () => {
  it("always emits one meter per catalog resource and a progression block", () => {
    const surface = actorSurfaceFixture();
    const vm = foldConditionSurfaceVm({
      data: actor(),
      sheet: null,
      surface,
      selectedPoolId: "hp",
      availability: "ready",
      revision: 1
    });
    expect(vm.phase).toBe("ready");
    expect(vm.main[0]?.piece).toBe("progression-gauge");
    const hero = vm.main[1] as { meters?: { poolId: string }[] };
    expect(hero.meters).toHaveLength(surface.resources.length);
    expect(hero.meters?.some((m) => m.poolId === "poise")).toBe(true);
  });

  it("uses /sheet xpToNext when available", () => {
    const vm = foldConditionSurfaceVm({
      data: actor(),
      sheet: {
        instanceId: "a1",
        playerId: 1,
        side: "plant",
        typeId: 3,
        displayName: "Emberling",
        level: 14,
        xp: 2140,
        xpToNext: 3400,
        derived: [],
        primary: []
      },
      surface: actorSurfaceFixture(),
      selectedPoolId: "hp",
      availability: "ready",
      revision: 1
    });
    const prog = vm.main[0] as { fillPct?: number | null; message?: string | null };
    expect(prog.fillPct).toBeGreaterThan(0);
    expect(prog.message).toBeNull();
  });
});
