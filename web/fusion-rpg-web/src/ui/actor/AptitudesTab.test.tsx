import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PLAYER_PENDING } from "@/contract/adapt";
import { known, pendingWithReason } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import { actorSurfaceFixture } from "@/lib/bus/actorSurface";
import { AptitudesTab } from "./AptitudesTab";
import { resetAptitudeObsForTests } from "./aptitudeObs";

const mutateCommander = vi.fn();
const mutateUnique = vi.fn();
const commanderData = {
  theta: 100,
  budget: 300,
  shares: { Might: 12, Fortitude: 8, Agility: 5 }
};
const uniqueData = {
  instanceId: "a1",
  playerId: 1,
  specimenLevel: 14,
  budget: 200,
  spent: 20,
  leftover: 180,
  withinBudget: true,
  shares: { Might: 10, Fortitude: 5, Agility: 5 },
  theta: 80
};

vi.mock("@/lib/bus", () => ({
  usePlayers: () => ({ data: { currentPlayerId: 1 } }),
  useAptitudes: (playerId: number | null | undefined) =>
    playerId
      ? { data: commanderData, isLoading: false }
      : { data: undefined, isLoading: false },
  useUniqueAptitudes: (instanceId: string | null | undefined) =>
    instanceId
      ? { data: uniqueData, isLoading: false }
      : { data: undefined, isLoading: false },
  useSaveAptitudes: () => ({ mutateAsync: mutateCommander, isPending: false }),
  useSaveUniqueAptitudes: () => ({ mutateAsync: mutateUnique, isPending: false })
}));

vi.mock("@/lib/bus/aptitudePresets", () => ({
  probeAptitudePresetsApi: vi.fn(async () => true),
  useAptitudePresetActive: () => ({ data: { presetId: null }, isLoading: false })
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
    mutateCommander.mockReset();
    mutateUnique.mockReset();
    resetAptitudeObsForTests();
  });

  it("Mode A creature uses UniqueCreature scope and catalog icons", () => {
    const surface = actorSurfaceFixture();
    render(<AptitudesTab data={actor()} surface={surface} role="creature" />);
    expect(screen.getByTestId("aptitudes-tab")).toHaveAttribute("data-mode", "unique");
    expect(screen.getByTestId("aptitudes-scope-chip")).toHaveTextContent("Unique specimen");
    expect(screen.getByTestId("aptitude-tile-Might")).toHaveTextContent("Might");
    expect(screen.getByTestId("aptitude-icon-Might")).toBeInTheDocument();
  });

  it("Mode C commander chip fiction differs from UniqueCreature", () => {
    const surface = actorSurfaceFixture();
    render(<AptitudesTab data={actor()} surface={surface} role="commander" />);
    expect(screen.getByTestId("aptitudes-tab")).toHaveAttribute("data-mode", "commander");
    expect(screen.getByTestId("aptitudes-scope-chip")).toHaveTextContent("Commander");
  });

  it("reports draft leftover upward when a tile increments", async () => {
    const user = userEvent.setup();
    const onDraftState = vi.fn();
    const surface = actorSurfaceFixture();
    render(
      <AptitudesTab data={actor()} surface={surface} role="creature" onDraftState={onDraftState} />
    );
    await user.click(screen.getByTestId("aptitude-inc-Might"));
    const last = onDraftState.mock.calls.at(-1)?.[0];
    expect(last?.dirty).toBe(true);
    expect(last?.mode).toBe("unique");
    expect(last?.spent).toBeGreaterThan(20);
  });

  it("Confirm save posts UniqueCreature allocate for Mode A", async () => {
    mutateUnique.mockResolvedValue(uniqueData);
    const onDraftState = vi.fn();
    const user = userEvent.setup();
    const surface = actorSurfaceFixture();
    render(
      <AptitudesTab data={actor()} surface={surface} role="creature" onDraftState={onDraftState} />
    );
    await user.click(screen.getByTestId("aptitude-inc-Might"));
    const draft = onDraftState.mock.calls.at(-1)?.[0];
    await draft.save();
    expect(mutateUnique).toHaveBeenCalled();
    expect(mutateCommander).not.toHaveBeenCalled();
  });
});
