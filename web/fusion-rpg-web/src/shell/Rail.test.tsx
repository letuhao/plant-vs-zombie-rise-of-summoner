import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Rail } from "./Rail";
import { deriveRailEntries, type RailUnlockInputs } from "./railState";

const baseInputs: RailUnlockInputs = {
  currentStageId: "sanctum",
  hasCompletedARun: true,
  hasAnyDemon: false,
  hasAnyContract: false,
  hasAnyRelic: false,
  hasAnyBoundDemon: false,
  returnedExpeditionCount: 0,
  unreadResultCount: 2
};

describe("Rail", () => {
  it("renders all eight entries", () => {
    render(<Rail entries={deriveRailEntries(baseInputs)} onSelect={() => {}} />);
    for (const id of ["sanctum", "creatures", "relics", "fusion", "pacts", "expeditions", "almanac", "chronicle"]) {
      expect(screen.getByTestId(`rail-${id}`)).toBeInTheDocument();
    }
  });

  it("a locked entry is disabled and carries its reason as a title", () => {
    render(<Rail entries={deriveRailEntries(baseInputs)} onSelect={() => {}} />);
    const relics = screen.getByTestId("rail-relics");
    expect(relics).toBeDisabled();
    expect(relics).toHaveAttribute("title", expect.stringContaining("item"));
  });

  it("a badged entry shows its count", () => {
    render(<Rail entries={deriveRailEntries(baseInputs)} onSelect={() => {}} />);
    expect(screen.getByTestId("rail-chronicle-badge")).toHaveTextContent("2");
  });

  it("clicking an available entry calls onSelect with its id", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Rail entries={deriveRailEntries(baseInputs)} onSelect={onSelect} />);
    await user.click(screen.getByTestId("rail-creatures"));
    expect(onSelect).toHaveBeenCalledWith("creatures");
  });

  it("clicking a locked entry never calls onSelect", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Rail entries={deriveRailEntries(baseInputs)} onSelect={onSelect} />);
    await user.click(screen.getByTestId("rail-relics"));
    expect(onSelect).not.toHaveBeenCalled();
  });
});
