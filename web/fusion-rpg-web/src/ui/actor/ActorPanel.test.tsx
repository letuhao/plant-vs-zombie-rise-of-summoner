import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { known, pendingWithReason } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import type { ActorRungState } from "./actorRungState";
import { ActorPanel } from "./ActorPanel";

// Matches adaptActor's own real behavior: these four fields are unconditionally "pending" (a real
// reason string), never "absent" — PendingNote only renders for the "pending" state.
function readyState(): ActorRungState {
  const data: ActorView = {
    instanceId: "a1",
    playerId: 1,
    side: "plant",
    typeId: 3,
    displayName: known("Emberling"),
    phase: "ActiveBound",
    level: 14,
    xp: 2140,
    xpToNext: pendingWithReason("no server endpoint yet"),
    revision: 1,
    channelSummary: pendingWithReason("no server endpoint yet"),
    elementTyping: pendingWithReason("no server endpoint yet"),
    shieldStack: pendingWithReason("no server endpoint yet"),
    equipSlots: pendingWithReason("no server endpoint yet")
  };
  return { kind: "ready", data };
}

describe("ActorPanel", () => {
  it("still short-circuits non-ready states to RungStateFallback before any tab bar renders", () => {
    render(<ActorPanel state={{ kind: "loading" }} open onOpenChange={vi.fn()} />);
    expect(screen.getByTestId("actor-panel-loading")).toBeInTheDocument();
    expect(screen.queryByTestId("actor-sheet-tabs")).not.toBeInTheDocument();
  });

  it("renders all six real tabs for a ready actor", () => {
    render(<ActorPanel state={readyState()} open onOpenChange={vi.fn()} />);
    expect(screen.getByTestId("actor-sheet-tab-overview")).toBeInTheDocument();
    expect(screen.getByTestId("actor-sheet-tab-progression")).toBeInTheDocument();
    expect(screen.getByTestId("actor-sheet-tab-derived-stats")).toBeInTheDocument();
    expect(screen.getByTestId("actor-sheet-tab-actions")).toBeInTheDocument();
    expect(screen.getByTestId("actor-sheet-tab-passives")).toBeInTheDocument();
    expect(screen.getByTestId("actor-sheet-tab-gear")).toBeInTheDocument();
  });

  it("defaults to Overview, showing today's real Standing and Element-typing content unchanged", () => {
    render(<ActorPanel state={readyState()} open onOpenChange={vi.fn()} />);
    expect(screen.getByTestId("actor-standing-pending")).toBeInTheDocument();
    expect(screen.getByTestId("actor-element-pending")).toBeInTheDocument();
  });

  it("switching tabs shows only the active tab's own content", async () => {
    const user = userEvent.setup();
    render(<ActorPanel state={readyState()} open onOpenChange={vi.fn()} />);

    await user.click(screen.getByTestId("actor-sheet-tab-gear"));
    expect(screen.queryByTestId("actor-standing-pending")).not.toBeInTheDocument();

    await user.click(screen.getByTestId("actor-sheet-tab-overview"));
    expect(screen.getByTestId("actor-standing-pending")).toBeInTheDocument();
  });

  it("Release and Deploy each close the panel", async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    render(<ActorPanel state={readyState()} open onOpenChange={onOpenChange} />);

    await user.click(screen.getByTestId("actor-panel-release"));
    expect(onOpenChange).toHaveBeenCalledWith(false);

    onOpenChange.mockClear();
    await user.click(screen.getByTestId("actor-panel-deploy"));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
