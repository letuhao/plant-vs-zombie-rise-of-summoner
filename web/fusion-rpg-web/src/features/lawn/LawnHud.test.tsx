import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { LawnHud } from "./LawnHud";

describe("LawnHud (T28)", () => {
  it("renders real sun and wave numbers, not placeholders, when the economy snapshot is present", () => {
    render(<LawnHud sun={1240} wave={7} maxWave={14} hugeWave={false} deployed={[]} />);
    expect(screen.getByTestId("lawn-hud-sun")).toHaveTextContent("1240");
    expect(screen.getByTestId("lawn-hud-wave")).toHaveTextContent("Wave 7 of 14");
    expect(screen.getByTestId("lawn-hud-wave")).not.toHaveTextContent("huge");
  });

  it("flags a huge wave", () => {
    render(<LawnHud sun={0} wave={10} maxWave={10} hugeWave deployed={[]} />);
    expect(screen.getByTestId("lawn-hud-wave")).toHaveTextContent("huge");
  });

  it("shows an honest placeholder when the economy snapshot hasn't arrived yet, not a fabricated zero", () => {
    render(<LawnHud deployed={[]} />);
    expect(screen.getByTestId("lawn-hud-sun")).toHaveTextContent("—");
    expect(screen.getByTestId("lawn-hud-wave")).toHaveTextContent("Wave —");
  });

  it("renders one chip per deployed creature, and an honest empty state with none", () => {
    const { rerender } = render(<LawnHud deployed={[]} />);
    expect(screen.getByTestId("lawn-hud-deployed-empty")).toBeInTheDocument();

    rerender(
      <LawnHud
        deployed={[
          { ptr: "p1", side: "plant", typeId: 3, typeName: "Emberling" },
          { ptr: "p2", side: "plant", typeId: 4, typeName: null }
        ]}
      />
    );
    expect(screen.queryByTestId("lawn-hud-deployed-empty")).not.toBeInTheDocument();
    expect(screen.getByTestId("lawn-hud-deployed-p1")).toHaveTextContent("Emberling");
    expect(screen.getByTestId("lawn-hud-deployed-p2")).toHaveTextContent("#4"); // honest fallback, no fabricated name
  });

  it("the playback cluster is disabled and states why — no speed/pause control exists in this app", () => {
    render(<LawnHud deployed={[]} />);
    const playback = screen.getByTestId("lawn-hud-playback");
    expect(playback).toHaveAttribute("title", expect.stringContaining("No playback control"));
    for (const label of ["Pause", "Normal speed", "Double speed"]) {
      expect(screen.getByRole("button", { name: label })).toBeDisabled();
    }
  });
});
