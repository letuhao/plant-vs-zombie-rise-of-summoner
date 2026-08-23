import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { getStageMountCount, resetStageMountCounts } from "@/shell/stageHost";
import { LawnStage } from "./LawnStage";

const mutateAsync = vi.fn();
const mutate = vi.fn();

vi.mock("@/lib/bus", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/bus")>();
  return {
    ...actual,
    useSpawnExtraIntent: () => ({ mutateAsync, mutate, isPending: false }),
    useLawnDebugPost: () => ({ mutateAsync, mutate, isPending: false }),
    useUniqueActor: () => ({ data: undefined, isError: false, isLoading: false })
  };
});

const fakeGame = { scale: { resize: vi.fn() } } as unknown as Phaser.Game;
const createLawnGame = vi.fn(() => fakeGame);
const destroyLawnGame = vi.fn();

vi.mock("@/game/createLawnGame", () => ({
  createLawnGame: (...args: unknown[]) => createLawnGame(...(args as [])),
  destroyLawnGame: (...args: unknown[]) => destroyLawnGame(...(args as []))
}));

describe("LawnStage — GG-11 keystone proof", () => {
  beforeEach(() => {
    createLawnGame.mockClear();
    destroyLawnGame.mockClear();
    resetStageMountCounts();
  });

  it("opening and closing a panel leaves the Phaser Game instance identical and never unmounts the stage", async () => {
    const user = userEvent.setup();
    renderWithProviders(<LawnStage />);

    await waitFor(() => expect(createLawnGame).toHaveBeenCalledTimes(1));
    const gameBeforeOpen = createLawnGame.mock.results[0]!.value;
    expect(getStageMountCount("lawn")).toBe(1);

    await user.click(screen.getByTestId("lawn-stage-open-panel"));
    await waitFor(() => expect(screen.getByTestId("lawn-stage-panel")).toBeInTheDocument());

    // The board host never left the tree, the Phaser Game was never
    // recreated or destroyed, and the stage component was never remounted.
    expect(screen.getByTestId("lawn-game-host")).toBeInTheDocument();
    expect(createLawnGame).toHaveBeenCalledTimes(1);
    expect(createLawnGame.mock.results[0]!.value).toBe(gameBeforeOpen);
    expect(destroyLawnGame).not.toHaveBeenCalled();
    expect(getStageMountCount("lawn")).toBe(1);

    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByTestId("lawn-stage-panel")).not.toBeInTheDocument());

    expect(screen.getByTestId("lawn-game-host")).toBeInTheDocument();
    expect(createLawnGame).toHaveBeenCalledTimes(1);
    expect(destroyLawnGame).not.toHaveBeenCalled();
    expect(getStageMountCount("lawn")).toBe(1);
  });

  it("leaving the stage entirely still runs the full destroy checklist", async () => {
    const { unmount } = renderWithProviders(<LawnStage />);
    await waitFor(() => expect(createLawnGame).toHaveBeenCalledTimes(1));

    unmount();

    expect(destroyLawnGame).toHaveBeenCalledTimes(1);
  });
});
