import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, screen } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { getStageMountCount, resetStageMountCounts } from "@/shell/stageHost";
import { handleEscape } from "@/shell/keymap";
import { useLayerStack } from "@/shell/layerStack";
import { WorldStage } from "./WorldStage";

const fakeGame = { scale: { resize: vi.fn() } } as unknown as Phaser.Game;
const createWorldGame = vi.fn(() => fakeGame);
const destroyWorldGame = vi.fn();

vi.mock("@/game/createWorldGame", () => ({
  createWorldGame: (...args: unknown[]) => createWorldGame(...(args as [])),
  destroyWorldGame: (...args: unknown[]) => destroyWorldGame(...(args as []))
}));

describe("WorldStage", () => {
  beforeEach(() => {
    useLayerStack.setState({ layers: [] });
    createWorldGame.mockClear();
    destroyWorldGame.mockClear();
  });

  it("renders under StageHost with the Phaser world-game-host (not SVG)", () => {
    resetStageMountCounts();
    renderWithProviders(<WorldStage />);

    expect(screen.getByTestId("stage-host")).toBeInTheDocument();
    expect(screen.getByTestId("world-game-host")).toBeInTheDocument();
    expect(screen.queryByTestId("world-stage-svg")).not.toBeInTheDocument();
    expect(screen.getByTestId("world-map-fit")).toBeInTheDocument();
    expect(screen.getByTestId("lens-picker")).toBeInTheDocument();
  });

  it("mount count stays at 1 across re-renders — the same guarantee a band-2 layer opening and closing over it relies on (GG-11)", () => {
    resetStageMountCounts();
    const { rerender } = renderWithProviders(<WorldStage />);
    expect(getStageMountCount("world")).toBe(1);

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

    expect(() => act(() => handleEscape())).not.toThrow();
    expect(useLayerStack.getState().layers).toHaveLength(1);
    expect(screen.getByTestId("world-game-host")).toHaveAttribute("data-selected-sector", "");
  });

  it("with a band-2 layer open, Esc closes that layer instead — the stage's own entry and selection survive", () => {
    renderWithProviders(<WorldStage />);
    const closed: string[] = [];
    useLayerStack.getState().push({ id: "fake-layer", band: "panel", close: () => closed.push("fake-layer") });
    expect(useLayerStack.getState().layers).toHaveLength(2);

    handleEscape();

    expect(closed).toEqual(["fake-layer"]);
    expect(useLayerStack.getState().layers.map((l) => l.id)).toEqual(["world-stage", "fake-layer"]);
    expect(screen.getByTestId("world-game-host")).toHaveAttribute("data-selected-sector", "");
  });

  it("right-click on the map pane does exactly what Esc does — one gesture set, no exceptions (§4.4)", () => {
    renderWithProviders(<WorldStage />);
    expect(useLayerStack.getState().layers).toHaveLength(1);

    fireEvent.contextMenu(screen.getByTestId("world-game-host"));

    expect(useLayerStack.getState().layers).toHaveLength(1);
    expect(screen.getByTestId("world-game-host")).toHaveAttribute("data-selected-sector", "");
  });

  it("right-click with a band-2 layer open reaches that layer, not the stage's own selection", () => {
    renderWithProviders(<WorldStage />);
    let fakeClosed = false;
    useLayerStack.getState().push({ id: "fake-layer", band: "panel", close: () => (fakeClosed = true) });

    fireEvent.contextMenu(screen.getByTestId("world-game-host"));

    expect(fakeClosed).toBe(true);
    expect(useLayerStack.getState().layers.map((l) => l.id)).toEqual(["world-stage", "fake-layer"]);
  });
});
