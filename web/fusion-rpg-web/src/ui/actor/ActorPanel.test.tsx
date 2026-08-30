import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/render";
import { PLAYER_PENDING, adaptCommanderSheet } from "@/contract/adapt";
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
    xpToNext: pendingWithReason(PLAYER_PENDING.xpToNext),
    revision: 1,
    channelSummary: pendingWithReason(PLAYER_PENDING.channelSummary),
    elementTyping: pendingWithReason(PLAYER_PENDING.elementTyping),
    shieldStack: pendingWithReason(PLAYER_PENDING.shieldStack),
    equipSlots: pendingWithReason(PLAYER_PENDING.equipSlots)
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

  it("commander role shows CommanderSheetFooter and hides Deploy/Release", () => {
    const state: ActorRungState = {
      kind: "ready",
      data: adaptCommanderSheet(
        {
          id: "commander:dave",
          displayName: "Crazy Dave",
          isDefault: true,
          activeAuraId: "Might",
          activeAuraName: "Might",
          locationStub: null,
          legionStub: null
        },
        1
      )
    };
    render(
      <ActorPanel
        state={state}
        open
        onOpenChange={vi.fn()}
        role="commander"
        commanderMeta={{
          isDefault: true,
          activeAuraName: "Might",
          locationStub: null,
          legionStub: null
        }}
        onSetDefault={vi.fn()}
        onDefendLawn={vi.fn()}
        onOpenCommandersList={vi.fn()}
      />
    );
    expect(screen.getByTestId("commander-sheet-set-default")).toBeInTheDocument();
    expect(screen.getByTestId("commander-sheet-defend")).toBeInTheDocument();
    expect(screen.queryByTestId("actor-panel-deploy")).not.toBeInTheDocument();
    expect(screen.queryByTestId("actor-panel-release")).not.toBeInTheDocument();
    expect(screen.getByTestId("commander-sheet-overview-default")).toBeInTheDocument();
  });

  it("commander role wires Set default and Defend callbacks from the footer", async () => {
    const user = userEvent.setup();
    const onSetDefault = vi.fn();
    const onDefendLawn = vi.fn();
    const state: ActorRungState = {
      kind: "ready",
      data: adaptCommanderSheet(
        {
          id: "commander:penny",
          displayName: "Penny",
          isDefault: false,
          activeAuraId: null,
          activeAuraName: null,
          locationStub: null,
          legionStub: null
        },
        1
      )
    };
    render(
      <ActorPanel
        state={state}
        open
        onOpenChange={vi.fn()}
        role="commander"
        commanderMeta={{
          isDefault: false,
          activeAuraName: null,
          locationStub: null,
          legionStub: null
        }}
        onSetDefault={onSetDefault}
        onDefendLawn={onDefendLawn}
        onOpenCommandersList={vi.fn()}
      />
    );
    await user.click(screen.getByTestId("commander-sheet-set-default"));
    await user.click(screen.getByTestId("commander-sheet-defend"));
    expect(onSetDefault).toHaveBeenCalledTimes(1);
    expect(onDefendLawn).toHaveBeenCalledTimes(1);
  });

  it("commander role can switch to Progression without Deploy or Release", async () => {
    const user = userEvent.setup();
    const state: ActorRungState = {
      kind: "ready",
      data: adaptCommanderSheet(
        {
          id: "commander:dave",
          displayName: "Crazy Dave",
          isDefault: true,
          activeAuraId: "Might",
          activeAuraName: "Might",
          locationStub: null,
          legionStub: null
        },
        1
      )
    };
    renderWithProviders(
      <ActorPanel
        state={state}
        open
        onOpenChange={vi.fn()}
        role="commander"
        commanderMeta={{
          isDefault: true,
          activeAuraName: "Might",
          locationStub: null,
          legionStub: null
        }}
        onSetDefault={vi.fn()}
        onDefendLawn={vi.fn()}
        onOpenCommandersList={vi.fn()}
      />
    );
    await user.click(screen.getByTestId("actor-sheet-tab-progression"));
    expect(screen.getByTestId("actor-panel")).toBeInTheDocument();
    expect(screen.queryByTestId("actor-panel-deploy")).not.toBeInTheDocument();
    expect(screen.queryByTestId("actor-panel-release")).not.toBeInTheDocument();
  });

  it("shows Set default (next run) when matchBanner is set", () => {
    const state: ActorRungState = {
      kind: "ready",
      data: adaptCommanderSheet(
        {
          id: "commander:dave",
          displayName: "Crazy Dave",
          isDefault: true,
          activeAuraId: "Might",
          activeAuraName: "Might",
          locationStub: null,
          legionStub: null
        },
        1
      )
    };
    render(
      <ActorPanel
        state={state}
        open
        onOpenChange={vi.fn()}
        role="commander"
        matchBanner={{ displayName: "Crazy Dave", auraDisplayName: "Might" }}
        commanderMeta={{
          isDefault: true,
          activeAuraName: "Might",
          locationStub: null,
          legionStub: null
        }}
        onSetDefault={vi.fn()}
        onDefendLawn={vi.fn()}
        onOpenCommandersList={vi.fn()}
      />
    );
    expect(screen.getByTestId("commander-sheet-match-banner")).toHaveTextContent("This match: Crazy Dave · Might");
    expect(screen.getByTestId("commander-sheet-set-default")).toHaveTextContent("Set default (next run)");
  });

  it("matchBanner without aura omits the separator", () => {
    const state: ActorRungState = {
      kind: "ready",
      data: adaptCommanderSheet(
        {
          id: "commander:dave",
          displayName: "Dave",
          isDefault: true,
          activeAuraId: null,
          activeAuraName: null,
          locationStub: null,
          legionStub: null
        },
        1
      )
    };
    render(
      <ActorPanel
        state={state}
        open
        onOpenChange={vi.fn()}
        role="commander"
        matchBanner={{ displayName: "Dave", auraDisplayName: null }}
        commanderMeta={{
          isDefault: true,
          activeAuraName: null,
          locationStub: null,
          legionStub: null
        }}
        onSetDefault={vi.fn()}
        onDefendLawn={vi.fn()}
        onOpenCommandersList={vi.fn()}
      />
    );
    expect(screen.getByTestId("commander-sheet-match-banner")).toHaveTextContent("This match: Dave");
    expect(screen.getByTestId("commander-sheet-match-banner").textContent).not.toContain(" · ");
  });
});
