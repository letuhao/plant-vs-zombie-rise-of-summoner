import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import fixture from "./fixtures/first-light.json";
import { SectorNode, type SectorNodeProps } from "./SectorNode";
import type { WorldStateDto } from "./worldTypes";
import { toGraph, type SectorNodeData } from "./worldViewModel";

/**
 * W24 (spec-world-intel.md §On the map): three treatments, and a card that never implies more
 * certainty than the viewer has.
 */
vi.mock("@xyflow/react", () => ({
  Handle: () => null,
  Position: { Left: "left", Right: "right" }
}));

const graph = toGraph(fixture as WorldStateDto);
const dataFor = (id: string): SectorNodeData => graph.nodes.find((n) => n.id === id)!.data;

const props = (data: SectorNodeData, showLifelines = false) =>
  ({ id: data.sectorId, data, selected: false, showLifelines }) as unknown as SectorNodeProps;

describe("fog on the map", () => {
  it("draws a sector nobody has seen without naming it", () => {
    render(<SectorNode {...props(dataFor("black-gate"))} />);

    expect(screen.queryByText("Black Gate")).not.toBeInTheDocument();
    expect(screen.getByTestId("sector-status")).toHaveTextContent("unscouted");
    expect(screen.queryAllByTestId(/^slot-/)).toHaveLength(0);
  });

  it("stamps a remembered sector with how old the memory is", () => {
    // Hiding the date would create note-taking, not tension — the interesting uncertainty is what
    // changed, never how long ago you looked.
    const remembered: SectorNodeData = { ...dataFor("ash-waste"), remembered: true, age: 6, unknown: false };
    render(<SectorNode {...props(remembered)} />);

    expect(screen.getByTestId("sector-status")).toHaveTextContent("seen 6 turns ago");
  });

  it("gets the singular right, because it will be read a thousand times", () => {
    const remembered: SectorNodeData = { ...dataFor("ash-waste"), remembered: true, age: 1, unknown: false };
    render(<SectorNode {...props(remembered)} />);

    expect(screen.getByTestId("sector-status")).toHaveTextContent("seen 1 turn ago");
  });

  it("shows a sector in sight as what it is doing, not as a memory", () => {
    render(<SectorNode {...props(dataFor("homeworld"))} />);
    expect(screen.getByTestId("sector-status")).toHaveTextContent("held");
  });

  it("names a force it could not count and numbers one it could", () => {
    render(<SectorNode {...props(dataFor("homeworld"))} />);
    const mine = screen.getByTestId("force-e-dave-legion-1");
    expect(mine.textContent).toMatch(/^\d+$/);

    render(<SectorNode {...props(dataFor("ash-waste"))} />);
    const theirs = screen.getByTestId("force-e-wild-pack-1");
    expect(theirs.textContent).not.toMatch(/^\d+$/);
    expect(theirs.textContent).toBeTruthy();
  });

  it("never calls a sector claimable on the strength of slots it has not seen", () => {
    // A glimpse reports no slots at all. "No slots left to clear" must not read as "clear".
    const glimpsed = dataFor("frost-mire");
    expect(glimpsed.slots).toHaveLength(0);
    expect(glimpsed.claimable).toBe(false);
  });
});

describe("the lifeline overlay", () => {
  const junction: SectorNodeData = { ...dataFor("homeworld"), lifeline: true, lifelineCost: 5_000 };
  const spur: SectorNodeData = { ...dataFor("homeworld"), lifeline: false, lifelineCost: 900 };

  it("says nothing until you ask for it", () => {
    render(<SectorNode {...props(junction, false)} />);
    expect(screen.queryByTestId("sector-lifeline")).not.toBeInTheDocument();
  });

  it("marks a sector whose loss would split your territory", () => {
    render(<SectorNode {...props(junction, true)} />);
    expect(screen.getByTestId("sector-lifeline")).toHaveTextContent("lifeline");
  });

  it("distinguishes a route from a lifeline", () => {
    render(<SectorNode {...props(spur, true)} />);
    expect(screen.getByTestId("sector-lifeline")).toHaveTextContent("route");
  });

  it("says nothing about ground you do not hold", () => {
    // Reconnection cost is computed over your own holdings, so anything else scores zero and the
    // overlay stays quiet — it tells you about your territory, never anyone else's.
    const theirs: SectorNodeData = { ...dataFor("ash-waste"), lifeline: false, lifelineCost: 0 };
    render(<SectorNode {...props(theirs, true)} />);

    expect(screen.queryByTestId("sector-lifeline")).not.toBeInTheDocument();
  });
});
