import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PassivesTab } from "./PassivesTab";

describe("PassivesTab", () => {
  it("renders a grid of locked slots, none interactive", () => {
    render(<PassivesTab />);
    const grid = screen.getByTestId("passives-tab");
    expect(grid).toBeInTheDocument();
    expect(screen.queryAllByRole("button").length).toBe(0);
  });

  it("every slot names the real reason passives are deferred", () => {
    render(<PassivesTab />);
    const slots = screen.getByTestId("passives-tab").querySelectorAll("[title]");
    expect(slots.length).toBeGreaterThan(0);
    slots.forEach((slot) => {
      expect(slot.getAttribute("title")).toMatch(/reserved|deferred|no target date/i);
    });
  });
});
