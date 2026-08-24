import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { PactsLayer } from "./PactsLayer";

function ControlledPactsLayer() {
  const [open, setOpen] = useState(true);
  return (
    <div>
      <div data-testid="stage-behind">Sanctum content</div>
      <PactsLayer open={open} onOpenChange={setOpen} />
    </div>
  );
}

const mockUsePlayers = vi.fn();
const mockUseDemonRoster = vi.fn();
const mockUseSpeciesIndex = vi.fn();
const mockUseContracts = vi.fn();
const mockUsePatron = vi.fn();
const mockSetPatronMutateAsync = vi.fn();
const mockPerformRitualMutateAsync = vi.fn();
const mockReleaseContractMutateAsync = vi.fn();
const mockBuySlotMutateAsync = vi.fn();

vi.mock("@/lib/bus", () => ({
  usePlayers: () => mockUsePlayers(),
  useDemonRoster: () => mockUseDemonRoster(),
  useSpeciesIndex: () => mockUseSpeciesIndex()
}));

vi.mock("@/lib/bus/demons", () => ({
  newCorrelationId: () => "corr-1"
}));

vi.mock("@/lib/bus/patron", () => ({
  usePatron: () => mockUsePatron(),
  useSetPatron: () => ({ mutateAsync: mockSetPatronMutateAsync })
}));

vi.mock("@/lib/bus/contracts", () => ({
  useContracts: () => mockUseContracts(),
  usePerformRitual: () => ({ mutateAsync: mockPerformRitualMutateAsync }),
  useReleaseContract: () => ({ mutateAsync: mockReleaseContractMutateAsync }),
  useBuyContractSlot: () => ({ mutateAsync: mockBuySlotMutateAsync })
}));

const contentDemon = {
  instanceId: "d1",
  bound: true,
  deployable: true,
  loyalty: 840,
  rank: "trusted",
  personality: "stoic",
  upkeepPerDay: 5
};

const overdueDemon = {
  instanceId: "d2",
  bound: true,
  deployable: false,
  loyalty: 380,
  rank: "insubordinate",
  personality: "cruel",
  upkeepPerDay: 8
};

const roster = {
  items: [
    {
      profile: { instanceId: "d1", speciesId: "sp-imp", rarity: "epic", star: 2, elementPrimary: "fire", nickname: null },
      actor: { level: 20 }
    },
    {
      profile: { instanceId: "d2", speciesId: "sp-wraith", rarity: "rare", star: 0, elementPrimary: "dark", nickname: null },
      actor: { level: 10 }
    }
  ]
};

const speciesIndex = new Map([
  ["sp-imp", { name: "Imp", side: "zombie", gameTypeId: 1 }],
  ["sp-wraith", { name: "Wraith", side: "zombie", gameTypeId: 2 }]
]);

function setup(opts?: { contracts?: unknown[]; patronInstanceId?: string | null }) {
  mockUsePlayers.mockReturnValue({ data: { currentPlayerId: 1 } });
  mockUseDemonRoster.mockReturnValue({ data: roster });
  mockUseSpeciesIndex.mockReturnValue(speciesIndex);
  mockUseContracts.mockReturnValue({
    data: {
      contracts: opts?.contracts ?? [contentDemon, overdueDemon],
      capacity: { used: 2, total: 4, purchasedSlots: 0, nextSlotPrice: 500, canBuy: true, maxSlots: 8 },
      dailyTribute: 13,
      deployFloor: 200,
      loyaltyMax: 1000
    }
  });
  mockUsePatron.mockReturnValue({
    data: { patron: opts?.patronInstanceId ? { instanceId: opts.patronInstanceId } : null, switchCostSouls: 100 }
  });
}

describe("PactsLayer (T17)", () => {
  it("shows a loading state while queries are in flight, distinct from empty (GG-17)", () => {
    mockUsePlayers.mockReturnValue({ data: { currentPlayerId: 1 } });
    mockUseSpeciesIndex.mockReturnValue(speciesIndex);
    mockUseDemonRoster.mockReturnValue({ isLoading: true, data: undefined });
    mockUseContracts.mockReturnValue({ isLoading: true, data: undefined });
    mockUsePatron.mockReturnValue({ data: { patron: null, switchCostSouls: 100 } });
    renderWithProviders(<PactsLayer open onOpenChange={() => {}} />);
    expect(screen.getByTestId("pacts-loading")).toBeInTheDocument();
    expect(screen.queryByText("No pacts yet")).not.toBeInTheDocument();
  });

  it("shows an error state with a retry, distinct from empty (GG-17)", async () => {
    const refetchContracts = vi.fn();
    const refetchRoster = vi.fn();
    mockUsePlayers.mockReturnValue({ data: { currentPlayerId: 1 } });
    mockUseSpeciesIndex.mockReturnValue(speciesIndex);
    mockUseDemonRoster.mockReturnValue({ data: roster, refetch: refetchRoster });
    mockUseContracts.mockReturnValue({ isError: true, data: undefined, refetch: refetchContracts });
    mockUsePatron.mockReturnValue({ data: { patron: null, switchCostSouls: 100 } });
    const user = userEvent.setup();
    renderWithProviders(<PactsLayer open onOpenChange={() => {}} />);
    expect(screen.getByTestId("pacts-error")).toBeInTheDocument();

    await user.click(screen.getByText("Retry"));
    expect(refetchContracts).toHaveBeenCalled();
    expect(refetchRoster).toHaveBeenCalled();
  });

  it("shows an empty state with no bound pacts", () => {
    setup({ contracts: [] });
    renderWithProviders(<PactsLayer open onOpenChange={() => {}} />);
    expect(screen.getByText("No pacts yet")).toBeInTheDocument();
  });

  it("a content pact offers Release, an overdue pact disables Renegotiate with its reason inline and offers Ritual instead", () => {
    setup();
    renderWithProviders(<PactsLayer open onOpenChange={() => {}} />);

    expect(screen.getByTestId("pact-release-d1")).toBeInTheDocument();
    expect(screen.queryByTestId("pact-renegotiate-d1")).not.toBeInTheDocument();

    const renegotiate = screen.getByTestId("pact-renegotiate-d2");
    expect(renegotiate).toBeDisabled();
    expect(screen.getByTestId("pact-renegotiate-reason-d2")).toHaveTextContent("Insubordinate — perform a pact ritual");
    expect(screen.getByTestId("pact-ritual-d2")).toBeInTheDocument();
  });

  it("Ritual calls the real mutation for the overdue pact's specimen", async () => {
    setup();
    const user = userEvent.setup();
    renderWithProviders(<PactsLayer open onOpenChange={() => {}} />);
    await user.click(screen.getByTestId("pact-ritual-d2"));
    expect(mockPerformRitualMutateAsync).toHaveBeenCalledWith({ playerId: 1, instanceId: "d2", correlationId: "corr-1" });
  });

  it("shows the patron's real aura benefit, and only a non-patron content pact offers Make patron", () => {
    setup({ patronInstanceId: "d1" });
    renderWithProviders(<PactsLayer open onOpenChange={() => {}} />);
    expect(screen.getByTestId("pact-aura-d1")).toHaveTextContent("fire power");
    expect(screen.queryByTestId("pact-make-patron-d1")).not.toBeInTheDocument();
  });

  it("Esc closes it without unmounting whatever is behind it", async () => {
    setup();
    const user = userEvent.setup();
    renderWithProviders(<ControlledPactsLayer />, { withGlobalKeys: true });
    expect(screen.getByTestId("pacts-layer")).toBeInTheDocument();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByTestId("pacts-layer")).not.toBeInTheDocument());
    expect(screen.getByTestId("stage-behind")).toBeInTheDocument();
  });

  // T30 (plate 03 §D): side-by-side cards with a colour-tinted portrait, not the earlier vertical
  // stack with no portrait at all.
  it("renders pacts side by side, not stacked, each with a rarity-tinted portrait", () => {
    setup();
    renderWithProviders(<PactsLayer open onOpenChange={() => {}} />);
    expect(screen.getByTestId("pacts-grid")).toHaveClass("grid-cols-2");

    const portraits = screen.getAllByTestId("pact-portrait");
    expect(portraits).toHaveLength(2);
    // d1 is epic (Imp), d2 is rare (Wraith) — real profile.rarity, not a fabricated palette.
    expect(portraits[0]).toHaveClass("border-rarity-4");
    expect(portraits[1]).toHaveClass("border-rarity-3");
    expect(portraits[0]).toHaveTextContent("I"); // first letter of the real display name
  });
});
