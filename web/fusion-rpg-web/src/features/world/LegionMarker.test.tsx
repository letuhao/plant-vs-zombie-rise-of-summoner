import { act } from "react";
import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LegionMarker } from "./LegionMarker";

/**
 * jsdom has no SVG geometry, so the lane path is stubbed: 1000 units long, laid out along x. That is
 * enough to prove the two things that matter — the marker walks the lane, and it does so without the
 * React tree re-rendering once.
 */
function stubLanePath(id: string) {
  const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
  path.id = id;
  Object.assign(path, {
    getTotalLength: () => 1000,
    getPointAtLength: (len: number) => ({ x: len, y: 0 })
  });
  document.body.appendChild(path);
  return path;
}

let frames: FrameRequestCallback[] = [];

beforeEach(() => {
  frames = [];
  vi.stubGlobal("requestAnimationFrame", (cb: FrameRequestCallback) => {
    frames.push(cb);
    return frames.length;
  });
  vi.stubGlobal("cancelAnimationFrame", () => {});
});

afterEach(() => {
  vi.unstubAllGlobals();
  document.body.innerHTML = "";
});

/** Runs whatever frames are pending; each may schedule the next. */
function pump(times: number) {
  for (let i = 0; i < times; i++) {
    const pending = frames;
    frames = [];
    act(() => pending.forEach((cb) => cb(0)));
  }
}

describe("LegionMarker", () => {
  it("starts where the force was and ends where it stopped", () => {
    stubLanePath("l-home-ember");
    let clock = 0;

    render(
      <svg>
        <LegionMarker
          pathId="l-home-ember"
          entityId="e-dave-legion-1"
          fromMilli={200}
          toMilli={800}
          durationMs={100}
          color="#34d399"
          now={() => clock}
        />
      </svg>
    );

    const marker = screen.getByTestId("legion-marker-e-dave-legion-1");
    expect(marker).toHaveAttribute("transform", "translate(200, 0)");

    clock = 50;
    pump(1);
    expect(marker).toHaveAttribute("transform", "translate(500, 0)");

    clock = 100;
    pump(1);
    expect(marker).toHaveAttribute("transform", "translate(800, 0)");
  });

  it("stops scheduling frames once it has arrived", () => {
    stubLanePath("l-home-ember");
    let clock = 0;

    render(
      <svg>
        <LegionMarker
          pathId="l-home-ember"
          entityId="e-a"
          fromMilli={0}
          toMilli={1000}
          durationMs={100}
          color="#34d399"
          now={() => clock}
        />
      </svg>
    );

    clock = 500;
    pump(1);
    expect(frames).toHaveLength(0);
  });

  /** The whole point of the ref-and-rAF approach. */
  it("never re-renders React while it animates", () => {
    stubLanePath("l-home-ember");
    const renders = vi.fn();
    let clock = 0;

    function Counting() {
      renders();
      return (
        <svg>
          <LegionMarker
            pathId="l-home-ember"
            entityId="e-a"
            fromMilli={0}
            toMilli={1000}
            durationMs={1000}
            color="#34d399"
            now={() => clock}
          />
        </svg>
      );
    }

    render(<Counting />);
    expect(renders).toHaveBeenCalledTimes(1);

    for (let i = 1; i <= 8; i++) {
      clock = i * 100;
      pump(1);
    }

    expect(screen.getByTestId("legion-marker-e-a")).toHaveAttribute("transform", "translate(800, 0)");
    expect(renders).toHaveBeenCalledTimes(1);
  });

  /**
   * A marker that is re-rendered for an unrelated reason — a force changing sides, say — must carry
   * on from where it is. Restarting the march every time the component happens to render would make
   * a legion stutter back to the start of the lane for no reason a player could see.
   */
  it("carries on across a re-render instead of snapping back to the start", () => {
    stubLanePath("l-home-ember");
    let clock = 0;

    const marker = (color: string) => (
      <svg>
        <LegionMarker
          pathId="l-home-ember"
          entityId="e-a"
          fromMilli={0}
          toMilli={1000}
          durationMs={1000}
          color={color}
          now={() => clock}
        />
      </svg>
    );

    const { rerender } = render(marker("#34d399"));

    clock = 500;
    pump(1);
    expect(screen.getByTestId("legion-marker-e-a")).toHaveAttribute("transform", "translate(500, 0)");

    // Same march, different colour. Nothing about where it is has changed.
    rerender(marker("#fb7185"));

    expect(screen.getByTestId("legion-marker-e-a")).toHaveAttribute("transform", "translate(500, 0)");
  });

  it("does nothing at all when the lane it was told about is not on screen", () => {
    render(
      <svg>
        <LegionMarker
          pathId="missing-lane"
          entityId="e-a"
          fromMilli={0}
          toMilli={1000}
          durationMs={100}
          color="#34d399"
        />
      </svg>
    );

    expect(screen.getByTestId("legion-marker-e-a")).not.toHaveAttribute("transform");
    expect(frames).toHaveLength(0);
  });
});
