import { afterEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { NotifyRail } from "./NotifyRail";
import type { RailItem } from "./notifyRailStore";
import { clearChannelSettingsForTests } from "./channelSettings";

const items: RailItem[] = [
  { id: "a", category: "growth", title: "First", body: "", state: "unread", blocking: false },
  { id: "b", category: "intel.new", title: "Second", body: "", state: "opened", blocking: false }
];

const noop = () => {};

afterEach(() => {
  clearChannelSettingsForTests();
});

describe("NotifyRail — band 1, right-anchored, its own bounded shell (world-stage W87)", () => {
  it("renders every item", () => {
    render(<NotifyRail items={items} onOpen={noop} onDismiss={noop} onUndoDismiss={noop} />);
    expect(screen.getByTestId("rail-item-a")).toBeInTheDocument();
    expect(screen.getByTestId("rail-item-b")).toBeInTheDocument();
  });

  it("declares no z-index of its own", () => {
    render(<NotifyRail items={items} onOpen={noop} onDismiss={noop} onUndoDismiss={noop} />);
    expect(screen.getByTestId("notify-rail").className).not.toMatch(/\bz-\d|\bz-\[/);
  });

  it("scrolls inside its own bounded shell — overflow-y-auto, not the stage", () => {
    render(<NotifyRail items={items} onOpen={noop} onDismiss={noop} onUndoDismiss={noop} />);
    expect(screen.getByTestId("notify-rail").className).toContain("overflow-y-auto");
  });
});
