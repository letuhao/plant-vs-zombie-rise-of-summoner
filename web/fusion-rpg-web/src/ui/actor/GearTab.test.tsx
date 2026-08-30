import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PLAYER_PENDING } from "@/contract/adapt";
import { known, pendingWithReason } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import { GearTab } from "./GearTab";

function actorWith(equipSlots: ActorView["equipSlots"]): ActorView {
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
    equipSlots
  };
}

describe("GearTab", () => {
  it("renders an honest empty state for today's real pending equipSlots", () => {
    render(<GearTab data={actorWith(pendingWithReason(PLAYER_PENDING.equipSlots))} />);
    const empty = screen.getByTestId("gear-tab-empty");
    expect(empty).toBeInTheDocument();
    expect(empty).toHaveTextContent("No gear slots yet");
    expect(empty).toHaveTextContent("Equipment is coming in a later update.");
  });

  it("does not render a fabricated slot grid", () => {
    render(<GearTab data={actorWith(pendingWithReason(PLAYER_PENDING.equipSlots))} />);
    expect(screen.queryByRole("list")).not.toBeInTheDocument();
  });

  it("does not crash on a future non-pending equipSlots value", () => {
    render(<GearTab data={actorWith(known([]))} />);
    expect(screen.queryByTestId("gear-pending-fallback") ?? screen.getByTestId("gear-tab-empty")).toBeTruthy();
  });
});
