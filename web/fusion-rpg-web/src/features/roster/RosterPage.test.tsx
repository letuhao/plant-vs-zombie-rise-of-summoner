import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { RosterPage } from "./RosterPage";

const mutateAsync = vi.fn();

const equipmentData = {
  instanceId: "a1",
  phase: "ActiveBound",
  items: [] as { slot: string; itemId: string }[],
  modsJson: "{}"
};

const actorsData = {
  playerId: 1,
  items: [
    {
      instanceId: "a1",
      playerId: 1,
      side: "plant",
      typeId: 3,
      phase: "ActiveBound",
      level: 2,
      xp: 10,
      revision: 1
    }
  ]
};

vi.mock("@/lib/bus", () => ({
  usePlayers: () => ({ data: { currentPlayerId: 1 } }),
  useUniqueActors: () => ({
    data: actorsData,
    isError: false
  }),
  useUniqueEquipment: () => ({
    data: equipmentData,
    isError: false
  }),
  useCreateUniqueActor: () => ({ mutateAsync, isPending: false }),
  useDeployUniqueActor: () => ({ mutateAsync, isPending: false }),
  useRetireUniqueActor: () => ({ mutateAsync, isPending: false }),
  usePutUniqueEquipment: () => ({ mutateAsync, isPending: false }),
  useClearUniqueEquipment: () => ({ mutateAsync, isPending: false }),
  useAwardUniqueActorXp: () => ({ mutateAsync, isPending: false })
}));

describe("RosterPage smoke", () => {
  beforeEach(() => {
    mutateAsync.mockReset();
  });

  it("shows help text and disables equip when not Roster", () => {
    render(
      <MemoryRouter>
        <RosterPage />
      </MemoryRouter>
    );

    expect(screen.getByTestId("page-roster")).toBeInTheDocument();
    fireEvent.click(screen.getByText("a1"));

    expect(screen.getByTestId("roster-equip-help")).toHaveTextContent(
      "Applies on next Deploy (Bound loadout)"
    );
    expect(screen.getByTestId("roster-equip-weapon")).toBeDisabled();
    expect(screen.getByTestId("roster-xp-award")).toBeInTheDocument();
    expect(screen.getByTestId("roster-xp-award")).not.toBeDisabled();
  });
});
