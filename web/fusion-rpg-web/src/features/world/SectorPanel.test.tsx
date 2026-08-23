import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import fixture from "./fixtures/first-light.json";
import { SectorPanel } from "./SectorPanel";
import type { WorldStateDto } from "./worldTypes";
import { toGraph, type SectorNodeData } from "./worldViewModel";

const graph = toGraph(fixture as WorldStateDto);
const dataFor = (id: string): SectorNodeData => graph.nodes.find((n) => n.id === id)!.data;

describe("SectorPanel (spec-loam-fe.md: what it earns, what it costs, what it is connected to)", () => {
  it("shows what an owned sector earns, costs, and nets", () => {
    const home = { ...dataFor("homeworld"), loamProduction: 50, loamUpkeep: 16, loamNet: 34 };
    render(<SectorPanel sector={home} />);

    expect(screen.getByTestId("sector-economy")).toBeInTheDocument();
    expect(screen.getByText("50")).toBeInTheDocument();
    expect(screen.getByText("16")).toBeInTheDocument();
    expect(screen.getByText("+34")).toBeInTheDocument();
  });

  it("shows a flat zero net, not a false plus sign, for a sector earning and costing nothing", () => {
    // Found live: a rootbed-less sector exempt from upkeep (G-C) reported "Net +0", which reads as a
    // small positive rather than the flat zero it actually is.
    const flat = { ...dataFor("homeworld"), loamProduction: 0, loamUpkeep: 0, loamNet: 0 };
    render(<SectorPanel sector={flat} />);
    expect(screen.getByText("0", { selector: "dd span" })).toBeInTheDocument();
    expect(screen.queryByText("+0")).not.toBeInTheDocument();
  });

  it("shows nothing for a sector that is not yours — never another faction's economy", () => {
    const enemy = { ...dataFor("ash-waste"), loamProduction: 999 };
    render(<SectorPanel sector={enemy} />);
    expect(screen.queryByTestId("sector-economy")).not.toBeInTheDocument();
  });

  it("still shows the cost of barren ground you own — the ground most worth letting go of", () => {
    const barren = { ...dataFor("homeworld"), habitable: false, loamProduction: 0, loamUpkeep: 12, loamNet: -12 };
    render(<SectorPanel sector={barren} />);

    expect(screen.getByTestId("sector-economy")).toBeInTheDocument();
    expect(screen.getByText("-12")).toBeInTheDocument();
  });

  it("says plainly, in player words, when this sector's territory can't cover its own keep", () => {
    const starving = { ...dataFor("homeworld"), componentNet: -5 };
    render(<SectorPanel sector={starving} />);

    expect(screen.getByTestId("sector-economy-supply-warning")).toHaveTextContent("can't cover its own keep");
  });

  it("says how much is in store when the supply is healthy, not a warning", () => {
    const healthy = { ...dataFor("homeworld"), componentNet: 10, componentStock: 240 };
    render(<SectorPanel sector={healthy} />);

    expect(screen.queryByTestId("sector-economy-supply-warning")).not.toBeInTheDocument();
    expect(screen.getByText("240 in store")).toBeInTheDocument();
  });

  it("marks a sector the engine will release next turn, with the reason, in player words", () => {
    const doomed = { ...dataFor("homeworld"), willReleaseNextTurn: true };
    render(<SectorPanel sector={doomed} />);

    expect(screen.getByTestId("sector-release-warning")).toHaveTextContent("Losing ground next turn");
  });

  it("says nothing extra when nothing is about to be released — silence is the healthy state", () => {
    const safe = { ...dataFor("homeworld"), willReleaseNextTurn: false };
    render(<SectorPanel sector={safe} />);

    expect(screen.queryByTestId("sector-release-warning")).not.toBeInTheDocument();
  });

  it("never uses engine vocabulary in its copy", () => {
    const doomed = { ...dataFor("homeworld"), willReleaseNextTurn: true, componentNet: -5 };
    const { container } = render(<SectorPanel sector={doomed} />);
    const text = container.textContent ?? "";

    expect(text).not.toMatch(/componentId/i);
    expect(text).not.toMatch(/stabilityMilli/i);
    expect(text).not.toMatch(/intensityMilli/i);
  });
});
