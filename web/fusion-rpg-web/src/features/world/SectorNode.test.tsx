import { useState } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import fixture from "./fixtures/first-light.json";
import { SectorNode, type SectorNodeProps } from "./SectorNode";
import { strokeWidthFor } from "./LaneEdge";
import type { WorldStateDto } from "./worldTypes";
import { toGraph, type SectorNodeData } from "./worldViewModel";

/**
 * The mocked `Handle` doubles as a render counter: the card renders two of them, so every time the
 * card's body actually runs, this fires twice. That is what makes the "does not re-render" test a
 * measurement rather than an assertion about `memo` in the abstract.
 */
const { handleRendered } = vi.hoisted(() => ({ handleRendered: vi.fn() }));

vi.mock("@xyflow/react", () => ({
  Handle: () => {
    handleRendered();
    return null;
  },
  Position: { Left: "left", Right: "right" },
  // LaneEdge is imported for `strokeWidthFor`; these keep its module-level imports resolvable.
  BaseEdge: () => null,
  getStraightPath: () => ["", 0, 0]
}));

const graph = toGraph(fixture as WorldStateDto);
const dataFor = (id: string): SectorNodeData => graph.nodes.find((n) => n.id === id)!.data;

/** React Flow passes a wide prop bag; the card reads only these. */
const propsFor = (data: SectorNodeData, selected = false) =>
  ({ id: data.sectorId, data, selected }) as unknown as SectorNodeProps;

/** Module-scope so its identity is stable across a parent's re-renders, as React Flow's is. */
const emberProps = propsFor(dataFor("ember-hollow"));

const cardRenders = () => handleRendered.mock.calls.length / 2;

beforeEach(() => handleRendered.mockClear());

describe("SectorNode", () => {
  it("draws the sector, its slots, and what is standing in it", () => {
    render(<SectorNode {...emberProps} />);

    expect(screen.getByTestId("sector-node-ember-hollow")).toBeInTheDocument();
    expect(screen.getByText("Ember Hollow")).toBeInTheDocument();
    expect(screen.getAllByTestId(/^slot-/)).toHaveLength(4);
    expect(screen.getByTestId("slot-2")).toHaveAttribute("data-guard", "intact");
    expect(screen.getByTestId("slot-0")).toHaveAttribute("data-guard", "none");
  });

  it("shrouds a sector nobody has scouted instead of naming it", () => {
    render(<SectorNode {...propsFor(dataFor("black-gate"))} />);

    expect(screen.queryByText("Black Gate")).not.toBeInTheDocument();
    expect(screen.getByText("?")).toBeInTheDocument();
  });

  it("shows ownership as a data attribute so the styling is assertable", () => {
    render(<SectorNode {...propsFor(dataFor("homeworld"))} />);
    expect(screen.getByTestId("sector-node-homeworld")).toHaveAttribute("data-ownership", "mine");
  });

  it("puts the forces standing in a sector on the card", () => {
    render(<SectorNode {...propsFor(dataFor("ash-waste"))} />);
    expect(screen.getByTestId("force-e-wild-pack-1")).toBeInTheDocument();
  });

  /**
   * The property that makes the map usable: panning and zooming change the canvas transform, never
   * a node's `data`, so the cards must not re-render while the map is dragged. Here the parent's
   * state churns five times with the card's props held identical.
   */
  it("does not re-render while the viewport around it changes", async () => {
    function Harness() {
      const [viewport, setViewport] = useState(0);
      return (
        <div>
          <button onClick={() => setViewport((v) => v + 1)}>pan</button>
          <span data-testid="viewport">{viewport}</span>
          <SectorNode {...emberProps} />
        </div>
      );
    }

    render(<Harness />);
    expect(cardRenders()).toBe(1);

    const user = userEvent.setup();
    for (let i = 0; i < 5; i++) await user.click(screen.getByText("pan"));

    expect(screen.getByTestId("viewport")).toHaveTextContent("5");
    expect(cardRenders()).toBe(1);
  });

  it("does re-render when its own sector actually changes", async () => {
    function Harness() {
      const [taken, setTaken] = useState(false);
      const data = taken
        ? { ...dataFor("ember-hollow"), ownership: "mine" as const, ownerFactionId: "dave" }
        : dataFor("ember-hollow");
      return (
        <div>
          <button onClick={() => setTaken(true)}>claim</button>
          <SectorNode {...propsFor(data)} />
        </div>
      );
    }

    render(<Harness />);
    expect(cardRenders()).toBe(1);

    await userEvent.setup().click(screen.getByText("claim"));

    expect(cardRenders()).toBe(2);
    expect(screen.getByTestId("sector-node-ember-hollow")).toHaveAttribute("data-ownership", "mine");
  });
});

describe("lane stroke", () => {
  it("draws a narrow pass thinner than an open front", () => {
    expect(strokeWidthFor(1000)).toBeGreaterThan(strokeWidthFor(600));
  });

  it("never disappears and never swamps the map", () => {
    expect(strokeWidthFor(0)).toBeGreaterThanOrEqual(1.2);
    expect(strokeWidthFor(100000)).toBeLessThanOrEqual(6);
  });
});
