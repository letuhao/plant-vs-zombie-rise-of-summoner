import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { LoamGauge } from "./LoamGauge";
import type { LoamComponentSummary, LoamEmpireSummary } from "./worldViewModel";

const component = (overrides: Partial<LoamComponentSummary> = {}): LoamComponentSummary => ({
  componentId: "a",
  production: 50,
  upkeep: 20,
  net: 30,
  stock: 200,
  sectorCount: 2,
  releaseCandidateSectorId: null,
  ...overrides
});

const summary = (components: LoamComponentSummary[]): LoamEmpireSummary => ({
  production: components.reduce((s, c) => s + c.production, 0),
  upkeep: components.reduce((s, c) => s + c.upkeep, 0),
  net: components.reduce((s, c) => s + c.net, 0),
  stock: components.reduce((s, c) => s + c.stock, 0),
  components
});

describe("LoamGauge", () => {
  it("says plainly when there is no territory to read yet", () => {
    render(<LoamGauge summary={summary([])} />);
    expect(screen.getByTestId("loam-gauge-empty")).toBeInTheDocument();
    expect(screen.queryByTestId("loam-gauge")).not.toBeInTheDocument();
  });

  it("shows income, upkeep, net, and stock for the empire", () => {
    render(<LoamGauge summary={summary([component({ production: 50, upkeep: 20, net: 30, stock: 200 })])} />);

    expect(screen.getByTestId("loam-gauge-income")).toHaveTextContent("50");
    expect(screen.getByTestId("loam-gauge-upkeep")).toHaveTextContent("20");
    expect(screen.getByTestId("loam-gauge-net")).toHaveTextContent("+30");
    expect(screen.getByTestId("loam-gauge-stock")).toHaveTextContent("200");
  });

  it("says nothing about a split supply when there is only one component", () => {
    render(<LoamGauge summary={summary([component()])} />);
    expect(screen.queryByTestId("loam-gauge-components")).not.toBeInTheDocument();
    expect(screen.queryByText(/split into/)).not.toBeInTheDocument();
  });

  it("names a split supply, and lists every component, once territory is split", () => {
    render(
      <LoamGauge
        summary={summary([component({ componentId: "a" }), component({ componentId: "b", net: -10 })])}
      />
    );

    expect(screen.getByText("Your supply is split into 2 parts.")).toBeInTheDocument();
    expect(screen.getByTestId("loam-component-a")).toBeInTheDocument();
    expect(screen.getByTestId("loam-component-b")).toBeInTheDocument();
  });

  it("says plainly, in player words, when a component cannot cover its own keep", () => {
    render(
      <LoamGauge
        summary={summary([component({ componentId: "a" }), component({ componentId: "b", net: -10 })])}
      />
    );

    expect(screen.getByTestId("loam-component-warning-b")).toHaveTextContent("can't cover its own keep");
    expect(screen.queryByTestId("loam-component-warning-a")).not.toBeInTheDocument();
  });
});
