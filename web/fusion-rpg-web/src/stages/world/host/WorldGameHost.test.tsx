import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, act } from "@testing-library/react";
import { worldBusEmit, worldBusOn, worldBusClearAll } from "@/game/EventBus";
import { WorldGameHost } from "./WorldGameHost";
import type { AdaptedWorldState } from "@/contract/adapt";

const fakeGame = {
  scale: { resize: vi.fn() },
  registry: { set: vi.fn(), get: vi.fn() },
  scene: { getScenes: () => [], getScene: () => null },
  destroy: vi.fn()
} as unknown as Phaser.Game;

const createWorldGame = vi.fn(() => fakeGame);
const destroyWorldGame = vi.fn();

vi.mock("@/game/createWorldGame", () => ({
  createWorldGame: (...args: unknown[]) => createWorldGame(...(args as [])),
  destroyWorldGame: (...args: unknown[]) => destroyWorldGame(...(args as []))
}));

const emptyModel: AdaptedWorldState = {
  sectors: [],
  lanes: [],
  slotsBySectorId: {},
  forcesBySectorId: {}
};

describe("WorldGameHost", () => {
  beforeEach(() => {
    worldBusClearAll();
    createWorldGame.mockClear();
    destroyWorldGame.mockClear();
  });

  it("creates a Phaser game on mount and destroys on unmount", () => {
    const { unmount } = render(
      <WorldGameHost model={emptyModel} onSelect={() => {}} />
    );
    expect(createWorldGame).toHaveBeenCalledTimes(1);
    expect(createWorldGame.mock.calls[0]![0]).toMatchObject({ generation: expect.any(Number) });
    unmount();
    expect(destroyWorldGame).toHaveBeenCalledTimes(1);
  });

  it("exposes world-game-host and world-game-canvas testids", () => {
    const { getByTestId } = render(
      <WorldGameHost model={emptyModel} onSelect={() => {}} />
    );
    expect(getByTestId("world-game-host")).toBeInTheDocument();
    expect(getByTestId("world-game-canvas")).toBeInTheDocument();
  });

  it("buffers world:model until world:ready, then emits with monotonic modelSeq", () => {
    const models: Array<{ modelSeq: number }> = [];
    worldBusOn("world:model", (raw) => {
      models.push(raw as { modelSeq: number });
    });

    render(<WorldGameHost model={emptyModel} overlayEpoch={0} onSelect={() => {}} />);
    expect(models).toHaveLength(0);

    const generation = createWorldGame.mock.calls[0]![0].generation as number;
    act(() => {
      worldBusEmit("world:ready", { generation });
    });
    expect(models).toHaveLength(1);
    expect(models[0]!.modelSeq).toBe(1);
  });

  it("does not remount the game when selectedSectorId changes (GG-11)", () => {
    const { rerender } = render(
      <WorldGameHost model={emptyModel} selectedSectorId={null} onSelect={() => {}} />
    );
    expect(createWorldGame).toHaveBeenCalledTimes(1);
    rerender(
      <WorldGameHost model={emptyModel} selectedSectorId="homeworld" onSelect={() => {}} />
    );
    expect(createWorldGame).toHaveBeenCalledTimes(1);
    expect(destroyWorldGame).not.toHaveBeenCalled();
  });
});
