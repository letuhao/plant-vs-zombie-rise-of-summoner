import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PLAYER_PENDING } from "@/contract/adapt";
import { known, pendingWithReason } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import { actorSurfaceFixture } from "@/lib/bus/actorSurface";
import { AptitudesTab } from "./AptitudesTab";

const mutateAsync = vi.fn();
const aptitudesData = {
  theta: 100,
  budget: 300,
  shares: { Might: 12, Fortitude: 8, Agility: 5 }
};

vi.mock("@/lib/bus", () => ({
  usePlayers: () => ({ data: { currentPlayerId: 1 } }),
  useAptitudes: () => ({ data: aptitudesData, isLoading: false }),
  useSaveAptitudes: () => ({ mutateAsync, isPending: false })
}));

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

describe("AptitudesTab", () => {
  beforeEach(() => {
    mutateAsync.mockReset();
  });

  it("renders catalog displayNames as tiles", () => {
    const surface = actorSurfaceFixture();
    render(<AptitudesTab data={actor()} surface={surface} />);
    expect(screen.getByTestId("aptitudes-tab")).toBeInTheDocument();
    expect(screen.getByTestId("aptitude-tile-Might")).toHaveTextContent("Might");
    expect(screen.getByTestId("aptitudes-scope-chip")).toHaveTextContent("Commander");
  });

  it("reports draft leftover upward when a tile increments", async () => {
    const user = userEvent.setup();
    const onDraftState = vi.fn();
    const surface = actorSurfaceFixture();
    render(<AptitudesTab data={actor()} surface={surface} onDraftState={onDraftState} />);
    await user.click(screen.getByTestId("aptitude-inc-Might"));
    const last = onDraftState.mock.calls.at(-1)?.[0];
    expect(last?.dirty).toBe(true);
    expect(last?.spent).toBeGreaterThan(12 + 8 + 5);
  });
});
