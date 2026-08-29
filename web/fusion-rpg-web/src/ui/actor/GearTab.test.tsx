import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
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
    xpToNext: pendingWithReason("no server endpoint yet"),
    revision: 1,
    channelSummary: pendingWithReason("no server endpoint yet"),
    elementTyping: pendingWithReason("no server endpoint yet"),
    shieldStack: pendingWithReason("no server endpoint yet"),
    equipSlots
  };
}

describe("GearTab", () => {
  it("renders an honest empty state for today's real pending equipSlots", () => {
    render(<GearTab data={actorWith(pendingWithReason("no server endpoint yet"))} />);
    const empty = screen.getByTestId("gear-tab-empty");
    expect(empty).toBeInTheDocument();
    expect(empty).toHaveTextContent("No gear slots wired yet");
    expect(empty).toHaveTextContent("spec-equip-and-paperdoll.md");
  });

  it("does not render a fabricated slot grid", () => {
    render(<GearTab data={actorWith(pendingWithReason("no server endpoint yet"))} />);
    expect(screen.queryByRole("list")).not.toBeInTheDocument();
  });

  it("does not crash on a future non-pending equipSlots value", () => {
    render(<GearTab data={actorWith(known([]))} />);
    expect(screen.queryByTestId("gear-pending-fallback") ?? screen.getByTestId("gear-tab-empty")).toBeTruthy();
  });
});
