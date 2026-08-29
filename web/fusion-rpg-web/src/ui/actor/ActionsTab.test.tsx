import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ActionsTab } from "./ActionsTab";

describe("ActionsTab", () => {
  it("renders a grid of locked slots, none interactive", () => {
    render(<ActionsTab />);
    const grid = screen.getByTestId("actions-tab");
    expect(grid).toBeInTheDocument();
    expect(screen.queryAllByRole("button").length).toBe(0);
  });

  it("every slot names the real reason the action system isn't built yet", () => {
    render(<ActionsTab />);
    const slots = screen.getByTestId("actions-tab").querySelectorAll("[title]");
    expect(slots.length).toBeGreaterThan(0);
    slots.forEach((slot) => {
      expect(slot.getAttribute("title")).toMatch(/action system/i);
    });
  });
});
