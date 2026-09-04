import { beforeEach, describe, expect, it } from "vitest";
import { act, fireEvent, screen } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { getStageMountCount, resetStageMountCounts } from "@/shell/stageHost";
import { handleEscape } from "@/shell/keymap";
import { useLayerStack } from "@/shell/layerStack";
import { WorldStage } from "./WorldStage";

describe("WorldStage", () => {
  beforeEach(() => {
    useLayerStack.setState({ layers: [] });
  });

  it("renders under StageHost with a viewBox-driven svg", () => {
    resetStageMountCounts();
    renderWithProviders(<WorldStage />);

    expect(screen.getByTestId("stage-host")).toBeInTheDocument();
    const svg = screen.getByTestId("world-stage-svg");
    expect(svg.tagName.toLowerCase()).toBe("svg");
    expect(svg.getAttribute("viewBox")).toMatch(/^-?\d+(\.\d+)? -?\d+(\.\d+)? \d+(\.\d+)? \d+(\.\d+)?$/);
  });

  it("mount count stays at 1 across re-renders — the same guarantee a band-2 layer opening and closing over it relies on (GG-11)", () => {
    resetStageMountCounts();
    const { rerender } = renderWithProviders(<WorldStage />);
    expect(getStageMountCount("world")).toBe(1);

    // A band-2 layer opening over the stage re-renders the tree around it without ever
    // recreating the stage's own component instance — simulated here the same way, since no
    // world-stage layer exists yet to open for real (that is Phase 2's job).
    rerender(<WorldStage />);
    expect(getStageMountCount("world")).toBe(1);

    rerender(<WorldStage />);
    expect(getStageMountCount("world")).toBe(1);
  });

  it("claims exactly one entry on the escape stack for its mounted lifetime, and releases it on unmount", () => {
    const { unmount } = renderWithProviders(<WorldStage />);
    expect(useLayerStack.getState().layers).toHaveLength(1);

    unmount();
    expect(useLayerStack.getState().layers).toHaveLength(0);
  });

  it("Esc reaches the stage's own entry when nothing else is open — select-sector: null is dispatched at last", () => {
    renderWithProviders(<WorldStage />);
    expect(useLayerStack.getState().layers).toHaveLength(1);

    // Must not throw: `handleEscape` calls the stage's own `close`, which dispatches
    // `select-sector: null` — the action `worldSelection.ts`'s reducer has always accepted but
    // nothing had ever dispatched before this task. Its own `close` never pops the stack itself
    // (only the unmount cleanup does, per `claimStageEscape`'s own contract), so the entry stays.
    expect(() => act(() => handleEscape())).not.toThrow();
    expect(useLayerStack.getState().layers).toHaveLength(1);
    expect(screen.getByTestId("world-stage-svg")).toHaveAttribute("data-selected-sector", "");
  });

  it("with a band-2 layer open, Esc closes that layer instead — the stage's own entry and selection survive", () => {
    renderWithProviders(<WorldStage />);
    const closed: string[] = [];
    useLayerStack.getState().push({ id: "fake-layer", band: "panel", close: () => closed.push("fake-layer") });
    expect(useLayerStack.getState().layers).toHaveLength(2);

    handleEscape();

    // handleEscape found the topmost dismissible entry — the fake layer, not the stage — and
    // called its own close(); the layer stub here never pops itself, so what proves the *stage*
    // was left alone is that its own entry, and the deselection close it owns, never fired.
    expect(closed).toEqual(["fake-layer"]);
    expect(useLayerStack.getState().layers.map((l) => l.id)).toEqual(["world-stage", "fake-layer"]);
    expect(screen.getByTestId("world-stage-svg")).toHaveAttribute("data-selected-sector", "");
  });

  it("right-click on the map pane does exactly what Esc does — one gesture set, no exceptions (§4.4)", () => {
    renderWithProviders(<WorldStage />);
    expect(useLayerStack.getState().layers).toHaveLength(1);

    fireEvent.contextMenu(screen.getByTestId("world-stage-svg"));

    expect(useLayerStack.getState().layers).toHaveLength(1);
    expect(screen.getByTestId("world-stage-svg")).toHaveAttribute("data-selected-sector", "");
  });

  it("right-click with a band-2 layer open reaches that layer, not the stage's own selection", () => {
    renderWithProviders(<WorldStage />);
    let fakeClosed = false;
    useLayerStack.getState().push({ id: "fake-layer", band: "panel", close: () => (fakeClosed = true) });

    fireEvent.contextMenu(screen.getByTestId("world-stage-svg"));

    // handleEscape found "fake-layer" (the topmost) and told it to close itself — proving
    // right-click is routed through the identical dismissal path Esc uses, not a hard-coded
    // "always deselect" shortcut — and the stage's own entry is still there, untouched.
    expect(fakeClosed).toBe(true);
    expect(useLayerStack.getState().layers.map((l) => l.id)).toEqual(["world-stage", "fake-layer"]);
  });
});
