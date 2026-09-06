import { describe, expect, it, vi } from "vitest";
import { GridSpecError, makeGridSpec } from "./GridSpec";
import { initialFocus, nextFocus, wireKeyboardNav, type KeySource } from "./keyboardNav";

describe("nextFocus — pure grid navigation", () => {
  const spec = makeGridSpec(5, 9);

  it("moves one cell per direction", () => {
    expect(nextFocus(spec, { row: 2, col: 4 }, "up")).toEqual({ row: 1, col: 4 });
    expect(nextFocus(spec, { row: 2, col: 4 }, "down")).toEqual({ row: 3, col: 4 });
    expect(nextFocus(spec, { row: 2, col: 4 }, "left")).toEqual({ row: 2, col: 3 });
    expect(nextFocus(spec, { row: 2, col: 4 }, "right")).toEqual({ row: 2, col: 5 });
  });

  it("clamps at every edge rather than wrapping or going out of bounds", () => {
    expect(nextFocus(spec, { row: 0, col: 0 }, "up")).toEqual({ row: 0, col: 0 });
    expect(nextFocus(spec, { row: 0, col: 0 }, "left")).toEqual({ row: 0, col: 0 });
    expect(nextFocus(spec, { row: 4, col: 8 }, "down")).toEqual({ row: 4, col: 8 });
    expect(nextFocus(spec, { row: 4, col: 8 }, "right")).toEqual({ row: 4, col: 8 });
  });

  it("throws GridSpecError when current is already outside the spec — never silently coerces", () => {
    expect(() => nextFocus(spec, { row: 5, col: 0 }, "up")).toThrow(GridSpecError);
    expect(() => nextFocus(spec, { row: 0, col: -1 }, "up")).toThrow(GridSpecError);
  });

  it("every cell in the board is reachable from initialFocus() by arrow keys alone", () => {
    const board = makeGridSpec(5, 9);
    const visited = new Set<string>();
    for (let row = 0; row < board.rows; row++) {
      let pos = initialFocus();
      for (let i = 0; i < row; i++) pos = nextFocus(board, pos, "down");
      for (let col = 0; col < board.cols; col++) {
        visited.add(`${pos.row},${pos.col}`);
        pos = nextFocus(board, pos, "right");
      }
    }
    expect(visited.size).toBe(board.rows * board.cols);
  });
});

function fakeKeySource(): KeySource & { fire: (key: string) => void } {
  let handler: ((event: { key: string }) => void) | undefined;
  return {
    on: (_event, h) => {
      handler = h;
    },
    off: vi.fn((_event, h) => {
      if (handler === h) handler = undefined;
    }),
    fire: (key: string) => handler?.({ key })
  };
}

describe("wireKeyboardNav — duck-typed against a KeySource, no Phaser import needed", () => {
  it("an arrow key moves focus by one cell via onFocusChange", () => {
    const keys = fakeKeySource();
    const onFocusChange = vi.fn();
    const onConfirm = vi.fn();
    wireKeyboardNav({
      keys,
      getSpec: () => makeGridSpec(5, 9),
      getFocus: () => ({ row: 2, col: 4 }),
      onFocusChange,
      onConfirm
    });

    keys.fire("ArrowRight");

    expect(onFocusChange).toHaveBeenCalledWith({ row: 2, col: 5 });
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("Enter and Space both confirm the current focus, not the previous one", () => {
    const keys = fakeKeySource();
    const onFocusChange = vi.fn();
    const onConfirm = vi.fn();
    wireKeyboardNav({
      keys,
      getSpec: () => makeGridSpec(5, 9),
      getFocus: () => ({ row: 1, col: 1 }),
      onFocusChange,
      onConfirm
    });

    keys.fire("Enter");
    keys.fire(" ");

    expect(onConfirm).toHaveBeenNthCalledWith(1, { row: 1, col: 1 });
    expect(onConfirm).toHaveBeenNthCalledWith(2, { row: 1, col: 1 });
    expect(onFocusChange).not.toHaveBeenCalled();
  });

  it("an unrelated key triggers neither callback", () => {
    const keys = fakeKeySource();
    const onFocusChange = vi.fn();
    const onConfirm = vi.fn();
    wireKeyboardNav({
      keys,
      getSpec: () => makeGridSpec(5, 9),
      getFocus: () => ({ row: 0, col: 0 }),
      onFocusChange,
      onConfirm
    });

    keys.fire("Tab");

    expect(onFocusChange).not.toHaveBeenCalled();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("the returned disposer removes the listener — no callback fires after unbind", () => {
    const keys = fakeKeySource();
    const onFocusChange = vi.fn();
    const unbind = wireKeyboardNav({
      keys,
      getSpec: () => makeGridSpec(5, 9),
      getFocus: () => ({ row: 0, col: 0 }),
      onFocusChange,
      onConfirm: vi.fn()
    });

    unbind();
    expect(keys.off).toHaveBeenCalledTimes(1);

    keys.fire("ArrowRight");
    expect(onFocusChange).not.toHaveBeenCalled();
  });
});
