import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PLAYER_PENDING } from "@/contract/adapt";
import { known, pendingWithReason } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import { actorSurfaceFixture } from "@/lib/bus/actorSurface";
import { ConditionTab } from "./ConditionTab";

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

describe("ConditionTab", () => {
  it("renders one meter slot per resource-catalog entry including poise", () => {
    const surface = actorSurfaceFixture();
    render(<ConditionTab data={actor()} surface={surface} />);
    for (const resource of surface.resources) {
      expect(screen.getByTestId(`condition-resource-${resource.id}`)).toBeInTheDocument();
    }
    expect(screen.getByTestId("condition-resource-hunger")).toHaveTextContent("Sun");
  });

  it("keeps Standing and xpToNext honest pending", () => {
    render(<ConditionTab data={actor()} surface={actorSurfaceFixture()} />);
    expect(screen.getByTestId("actor-standing-pending")).toBeInTheDocument();
    expect(screen.getByTestId("condition-xp-pending")).toBeInTheDocument();
  });
});
