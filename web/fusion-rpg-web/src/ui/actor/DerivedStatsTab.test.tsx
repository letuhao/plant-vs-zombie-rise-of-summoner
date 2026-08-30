import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { PLAYER_PENDING } from "@/contract/adapt";
import { absent, known, pendingWithReason } from "@/contract/pending";
import type { ActorChannelDetail, ActorView } from "@/contract/types";
import type { ActorDerivedDto } from "@/lib/bus/aura";
import { DerivedStatsTab } from "./DerivedStatsTab";

let derivedQueryResult: { data: ActorDerivedDto | undefined; isLoading: boolean; isError: boolean } = {
  data: undefined,
  isLoading: false,
  isError: false
};

vi.mock("@/lib/bus/aura", () => ({
  useActorDerived: () => derivedQueryResult
}));

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
    xpToNext: pendingWithReason(PLAYER_PENDING.xpToNext),
    revision: 1,
    channelSummary,
    elementTyping: pendingWithReason(PLAYER_PENDING.elementTyping),
    shieldStack: pendingWithReason(PLAYER_PENDING.shieldStack),
    equipSlots: pendingWithReason(PLAYER_PENDING.equipSlots)
  };
}

describe("DerivedStatsTab", () => {
  beforeEach(() => {
    derivedQueryResult = { data: undefined, isLoading: false, isError: false };
  });

  it("renders the honest pending reason today (channelSummary's real, only reachable state)", () => {
    render(<DerivedStatsTab data={actorWith(pendingWithReason(PLAYER_PENDING.channelSummary))} />);
    expect(screen.getByTestId("derived-stats-pending")).toHaveTextContent(PLAYER_PENDING.channelSummary);
    expect(screen.queryByTestId("derived-stats-summary-grid")).not.toBeInTheDocument();
  });

  it("renders the summary grid once channelSummary is known (future-proofing)", () => {
    render(<DerivedStatsTab data={actorWith(known([channel("combat.attack", 140)]))} />);
    expect(screen.getByTestId("derived-stats-summary-grid")).toBeInTheDocument();
    expect(screen.getByText("combat.attack")).toBeInTheDocument();
  });

  it("the doorway button is always disabled with the real reason, never a dead link", () => {
    render(<DerivedStatsTab data={actorWith(pendingWithReason(PLAYER_PENDING.channelSummary))} />);
    const button = screen.getByTestId("derived-stats-open-full");
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("title", "Full stat sheet coming soon");
  });

  it("live section: loading state before live data arrives, not a fabricated grid", () => {
    derivedQueryResult = { data: undefined, isLoading: true, isError: false };
    render(<DerivedStatsTab data={actorWith(pendingWithReason(PLAYER_PENDING.channelSummary))} />);
    expect(screen.getByTestId("derived-stats-live-loading")).toBeInTheDocument();
  });

  it("live section: an honest empty note when no stat changes are available yet", () => {
    derivedQueryResult = {
      data: { instanceId: "a1", channels: [{ channelId: "combat.absorption.fire", value: 0, contributions: [] }] },
      isLoading: false,
      isError: false
    };
    render(<DerivedStatsTab data={actorWith(pendingWithReason(PLAYER_PENDING.channelSummary))} />);
    expect(screen.getByTestId("derived-stats-live-empty")).toHaveTextContent("No stat changes to show yet.");
  });

  it("GG-49 non-vacuously: a real channel with a real contributor shows the contribution", () => {
    derivedQueryResult = {
      data: {
        instanceId: "a1",
        channels: [
          { channelId: "progression.power", value: 12, contributions: [{ sourceId: "rpg.progression", op: "Replace", value: 12 }] }
        ]
      },
      isLoading: false,
      isError: false
    };
    render(<DerivedStatsTab data={actorWith(pendingWithReason(PLAYER_PENDING.channelSummary))} />);
    expect(screen.getByTestId("derived-live-channel-progression.power")).toBeInTheDocument();
    expect(screen.getByTestId("channel-contribution-rpg.progression")).toHaveTextContent("+12");
  });
});
