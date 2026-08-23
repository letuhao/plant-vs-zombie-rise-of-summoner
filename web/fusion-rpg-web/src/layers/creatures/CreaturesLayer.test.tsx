import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { CreaturesLayer } from "./CreaturesLayer";

function ControlledCreaturesLayer() {
  const [open, setOpen] = useState(true);
  return (
    <div>
      <div data-testid="stage-behind">Sanctum content</div>
      <CreaturesLayer open={open} onOpenChange={setOpen} playerId={1} selectedId={null} onSelect={() => {}} />
    </div>
  );
}

const mockUseUniqueActors = vi.fn();
vi.mock("@/lib/bus", () => ({ useUniqueActors: () => mockUseUniqueActors() }));

const actors = [
  { instanceId: "a1", playerId: 1, side: "plant", typeId: 3, phase: "Roster", level: 5, xp: 10, revision: 1 },
  { instanceId: "a2", playerId: 1, side: "zombie", typeId: 7, phase: "ActiveBound", level: 12, xp: 200, revision: 1 }
];

describe("CreaturesLayer (T10)", () => {
  it("shows an empty state with no bound creatures", () => {
    mockUseUniqueActors.mockReturnValue({ data: { playerId: 1, items: [] } });
    renderWithProviders(<CreaturesLayer open onOpenChange={() => {}} playerId={1} selectedId={null} onSelect={() => {}} />);
    expect(screen.getByText("No creatures bound yet")).toBeInTheDocument();
  });

  it("renders a row per bound creature with side, level and no typeId anywhere", () => {
    mockUseUniqueActors.mockReturnValue({ data: { playerId: 1, items: actors } });
    renderWithProviders(<CreaturesLayer open onOpenChange={() => {}} playerId={1} selectedId={null} onSelect={() => {}} />);
    expect(screen.getByTestId("creatures-row-a1")).toBeInTheDocument();
    expect(screen.getByTestId("creatures-row-a2")).toBeInTheDocument();
    expect(screen.getAllByTestId("actor-level")).toHaveLength(2);
    expect(document.body.textContent).not.toMatch(/typeId/i);
  });

  it("selecting a row shows its detail card, and re-clicking deselects", async () => {
    mockUseUniqueActors.mockReturnValue({ data: { playerId: 1, items: actors } });
    const user = userEvent.setup();
    let selected: string | null = null;
    const onSelect = vi.fn((id: string | null) => {
      selected = id;
    });
    const { rerender } = renderWithProviders(
      <CreaturesLayer open onOpenChange={() => {}} playerId={1} selectedId={selected} onSelect={onSelect} />
    );

    await user.click(screen.getByTestId("creatures-row-a1"));
    expect(onSelect).toHaveBeenCalledWith("a1");

    rerender(<CreaturesLayer open onOpenChange={() => {}} playerId={1} selectedId="a1" onSelect={onSelect} />);
    expect(screen.getByTestId("creatures-detail")).toBeInTheDocument();

    await user.click(screen.getByTestId("creatures-row-a1"));
    expect(onSelect).toHaveBeenLastCalledWith(null);
  });

  it("Esc closes it without unmounting whatever is behind it", async () => {
    mockUseUniqueActors.mockReturnValue({ data: { playerId: 1, items: actors } });
    const user = userEvent.setup();
    renderWithProviders(<ControlledCreaturesLayer />, { withGlobalKeys: true });
    expect(screen.getByTestId("creatures-layer")).toBeInTheDocument();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByTestId("creatures-layer")).not.toBeInTheDocument());
    expect(screen.getByTestId("stage-behind")).toBeInTheDocument();
  });
});
