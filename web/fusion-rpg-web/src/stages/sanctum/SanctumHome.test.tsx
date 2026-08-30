import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { SanctumHome } from "./SanctumHome";

const listView = {
  defaultLawnCommanderId: "commander:dave",
  commanders: [
    {
      id: "commander:dave",
      displayName: "Crazy Dave",
      isDefault: true,
      activeAuraId: "Might",
      activeAuraName: "Might",
      locationStub: null,
      legionStub: null
    }
  ]
};

const mockUseCommanders = vi.fn();
vi.mock("@/lib/bus", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/bus")>();
  return { ...actual, useCommanders: () => mockUseCommanders() };
});

const mockNavigate = vi.fn();
vi.mock("react-router-dom", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router-dom")>();
  return { ...actual, useNavigate: () => mockNavigate };
});

describe("SanctumHome commander readout", () => {
  it("shows Leading line and Change commander opens the layer callback", async () => {
    mockUseCommanders.mockReturnValue({ data: listView });
    const onOpenCommanders = vi.fn();
    const user = userEvent.setup();
    render(
      <SanctumHome
        playerId={1}
        actorStates={[]}
        onOpenCreatures={() => {}}
        onOpenCommanders={onOpenCommanders}
        returnedExpeditionCount={0}
        onOpenExpeditions={() => {}}
      />
    );
    expect(screen.getByTestId("sanctum-home-leading-line")).toHaveTextContent("Leading: Crazy Dave · Might");
    await user.click(screen.getByTestId("sanctum-home-change-commander"));
    expect(onOpenCommanders).toHaveBeenCalled();
  });

  it("shows error state instead of faking Dave when the list query fails", () => {
    mockUseCommanders.mockReturnValue({ isError: true, refetch: vi.fn() });
    render(
      <SanctumHome
        playerId={1}
        actorStates={[]}
        onOpenCreatures={() => {}}
        onOpenCommanders={() => {}}
        returnedExpeditionCount={0}
        onOpenExpeditions={() => {}}
      />
    );
    expect(screen.getByTestId("sanctum-home-leading-error")).toBeInTheDocument();
    expect(screen.queryByText(/Leading: Crazy Dave/)).not.toBeInTheDocument();
  });
});
