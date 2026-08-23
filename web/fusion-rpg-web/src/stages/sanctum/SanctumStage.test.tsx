import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { SanctumStage } from "./SanctumStage";

const mockUsePlayers = vi.fn();
const mockUseUniqueActors = vi.fn();
const mockUseRuns = vi.fn();
const mockUseSoulBalance = vi.fn();

vi.mock("@/lib/bus", () => ({
  usePlayers: () => mockUsePlayers(),
  useUniqueActors: () => mockUseUniqueActors(),
  useRuns: () => mockUseRuns(),
  useSoulBalance: () => mockUseSoulBalance()
}));

vi.mock("@/lib/bus/contracts", () => ({
  useContracts: () => ({
    data: {
      contracts: [],
      capacity: { used: 0, total: 0, purchasedSlots: 0, nextSlotPrice: 0, canBuy: false, maxSlots: 0 },
      dailyTribute: 0,
      deployFloor: 0,
      loyaltyMax: 0
    }
  })
}));

const noActors = { playerId: 1, items: [] as unknown[] };
const oneActor = {
  playerId: 1,
  items: [{ instanceId: "a1", playerId: 1, side: "plant", typeId: 3, phase: "Roster", level: 5, xp: 10, revision: 1 }]
};

beforeEach(() => {
  mockUsePlayers.mockReturnValue({
    data: { currentPlayerId: 1, items: [{ id: 1, name: "Dave", createdUtc: "2026-01-01" }] }
  });
  mockUseUniqueActors.mockReturnValue({ data: noActors });
  mockUseRuns.mockReturnValue({ data: [] });
  mockUseSoulBalance.mockReturnValue({
    data: { playerId: 1, balance: 250, earnedTotal: 250, spentTotal: 0, revision: 1, updatedUtc: "" }
  });
});

describe("SanctumStage", () => {
  it("renders the HUD and the rail on first paint (GG-2: a playable affordance, not a blank stage)", () => {
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    expect(screen.getByTestId("sanctum-hud")).toBeInTheDocument();
    expect(screen.getByTestId("rail")).toBeInTheDocument();
    expect(screen.getByTestId("rail-sanctum")).toHaveAttribute("data-state", "active");
  });

  it("shows the first-run script when the player has no bound creature", () => {
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    expect(screen.getByTestId("focus-card-first-run")).toBeInTheDocument();
    expect(screen.queryByTestId("focus-card-actor")).not.toBeInTheDocument();
  });

  it("HUD shows identity and souls from real state, and the summoner level honestly as pending", () => {
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    expect(screen.getByTestId("sanctum-hud-identity")).toHaveTextContent("Dave");
    expect(screen.getByTestId("sanctum-hud-souls")).toHaveTextContent("250");
    expect(screen.getByTestId("sanctum-hud-level-pending")).toBeInTheDocument();
  });

  it("locked rail entries name what unlocks them", () => {
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    const relics = screen.getByTestId("rail-relics");
    expect(relics).toBeDisabled();
    expect(relics.getAttribute("title")).toMatch(/item/);
  });

  it("clicking Creatures on the rail opens the real Creatures layer, and Esc closes it", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });

    await user.click(screen.getByTestId("rail-creatures"));
    await waitFor(() => expect(screen.getByTestId("creatures-layer")).toBeInTheDocument());
    expect(screen.getByRole("heading", { name: "Creatures" })).toBeInTheDocument();

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByTestId("creatures-layer")).not.toBeInTheDocument());
  });

  it("clicking an unlocked non-Creatures rail entry opens its placeholder layer, and Esc closes it", async () => {
    mockUseRuns.mockReturnValue({ data: [{ id: 1, startedUtc: "2026-01-01T00:00:00Z" }] });
    const user = userEvent.setup();
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });

    await user.click(screen.getByTestId("rail-almanac"));
    await waitFor(() => expect(screen.getByTestId("sanctum-layer-placeholder")).toBeInTheDocument());
    expect(screen.getByRole("heading", { name: "Almanac" })).toBeInTheDocument();

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByTestId("sanctum-layer-placeholder")).not.toBeInTheDocument());
  });

  it("the focus card's CTA opens the same Creatures layer", async () => {
    const user = userEvent.setup();
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    await user.click(screen.getByTestId("focus-card-cta"));
    await waitFor(() => expect(screen.getByTestId("creatures-layer")).toBeInTheDocument());
  });

  it("Almanac and Chronicle are locked until a run has completed", () => {
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    expect(screen.getByTestId("rail-almanac")).toBeDisabled();
    expect(screen.getByTestId("rail-chronicle")).toBeDisabled();
  });

  it("Almanac and Chronicle unlock once a run exists", () => {
    mockUseRuns.mockReturnValue({ data: [{ id: 1, startedUtc: "2026-01-01T00:00:00Z" }] });
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    expect(screen.getByTestId("rail-almanac")).not.toBeDisabled();
    expect(screen.getByTestId("rail-chronicle")).not.toBeDisabled();
  });
});

describe("SanctumStage — with a bound creature", () => {
  it("shows the actor focus card, not the first-run script", () => {
    mockUseUniqueActors.mockReturnValue({ data: oneActor });
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    expect(screen.getByTestId("focus-card-actor")).toBeInTheDocument();
    expect(screen.queryByTestId("focus-card-first-run")).not.toBeInTheDocument();
    expect(screen.getByTestId("focus-card-count")).toHaveTextContent("1 creature bound");
  });

  it("Fusion unlocks once two actors share a species", () => {
    mockUseUniqueActors.mockReturnValue({
      data: {
        playerId: 1,
        items: [
          { instanceId: "a1", playerId: 1, side: "plant", typeId: 3, phase: "Roster", level: 5, xp: 10, revision: 1 },
          { instanceId: "a2", playerId: 1, side: "plant", typeId: 3, phase: "Roster", level: 2, xp: 0, revision: 1 }
        ]
      }
    });
    renderWithProviders(<SanctumStage />, { withGlobalKeys: true });
    expect(screen.getByTestId("rail-fusion")).not.toBeDisabled();
  });
});
