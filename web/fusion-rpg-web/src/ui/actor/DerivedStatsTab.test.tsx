import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { absent, known, pendingWithReason } from "@/contract/pending";
import type { ActorChannelDetail, ActorView } from "@/contract/types";
import { DerivedStatsTab } from "./DerivedStatsTab";

function channel(channelId: string, value: number): ActorChannelDetail {
  return { channelId, value, unitClass: "gameUnits", state: "active", contributions: absent() };
}

function actorWith(channelSummary: ActorView["channelSummary"]): ActorView {
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
    channelSummary,
    elementTyping: pendingWithReason("no server endpoint yet"),
    shieldStack: pendingWithReason("no server endpoint yet"),
    equipSlots: pendingWithReason("no server endpoint yet")
  };
}

describe("DerivedStatsTab", () => {
  it("renders the honest pending reason today (channelSummary's real, only reachable state)", () => {
    render(<DerivedStatsTab data={actorWith(pendingWithReason("The derived-stat snapshot has no server endpoint yet"))} />);
    expect(screen.getByTestId("derived-stats-pending")).toHaveTextContent("no server endpoint yet");
    expect(screen.queryByTestId("derived-stats-summary-grid")).not.toBeInTheDocument();
  });

  it("renders the summary grid once channelSummary is known (future-proofing)", () => {
    render(<DerivedStatsTab data={actorWith(known([channel("combat.attack", 140)]))} />);
    expect(screen.getByTestId("derived-stats-summary-grid")).toBeInTheDocument();
    expect(screen.getByText("combat.attack")).toBeInTheDocument();
  });

  it("the doorway button is always disabled with the real reason, never a dead link", () => {
    render(<DerivedStatsTab data={actorWith(pendingWithReason("no server endpoint yet"))} />);
    const button = screen.getByTestId("derived-stats-open-full");
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("title", expect.stringContaining("not built yet"));
  });
});
