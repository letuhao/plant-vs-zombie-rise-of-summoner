import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Toasts } from "@/shell/Toasts";
import { useToastStack } from "@/shell/toastStack";
import { NotifyRail } from "./NotifyRail";
import { onCommit, type RailItem } from "./notifyRailStore";
import { ChannelControl } from "./ChannelControl";
import { clearChannelSettingsForTests } from "./channelSettings";

/**
 * world-stage W89 (spec-world-notify.md §7) — the per-turn click budget, counted rather than
 * asserted in prose, against Endless Legend's own audited four-clicks-per-notification. Each row
 * exercises the pieces W84-88 already built directly (`NotifyRail`/`Toasts`/`ChannelControl`) —
 * there is no keyframe→category translator yet (that is `world-playback`'s own table, unmodified by
 * this program so far), so the fixtures here are hand-built rail items and toasts standing in for
 * what a real turn would eventually produce, not a live-translated report.
 */

// This environment's default window.localStorage is incomplete — stub a real in-memory Storage.
beforeEach(() => {
  const mem: Record<string, string> = {};
  const ls = {
    getItem: (k: string) => mem[k] ?? null,
    setItem: (k: string, v: string) => {
      mem[k] = v;
    },
    removeItem: (k: string) => {
      delete mem[k];
    },
    clear: () => {
      for (const key of Object.keys(mem)) delete mem[key];
    },
    key: (i: number) => Object.keys(mem)[i] ?? null,
    get length() {
      return Object.keys(mem).length;
    }
  };
  Object.defineProperty(window, "localStorage", { configurable: true, value: ls });
  useToastStack.getState().clear();
});

afterEach(() => {
  clearChannelSettingsForTests();
});

const noop = () => {};

describe("The per-turn click budget (world-stage W89, spec §7)", () => {
  it("row 1: acknowledging one routine event costs 0 clicks — it flushes with the turn", () => {
    const items: RailItem[] = [
      { id: "r-1", category: "growth", title: "Ash Waste grew a rootbed", body: "", state: "unread", blocking: false }
    ];
    // The turn ending is the one action the whole feed rides on — no per-item interaction at all.
    const afterCommit = onCommit(items, true);
    expect(afterCommit).toEqual([]);
  });

  it("row 2: acting on one important event costs 1 click — the toast's own action button", async () => {
    const user = userEvent.setup();
    const run = vi.fn();
    useToastStack.getState().push({
      tone: "warn",
      title: "Ash Waste will release next turn",
      category: "loam.release",
      action: { label: "View sector", run }
    });
    render(<Toasts />);

    await user.click(screen.getByTestId("toast-action")); // exactly one interaction
    expect(run).toHaveBeenCalledTimes(1);
    expect(useToastStack.getState().toasts).toHaveLength(0);
  });

  it("row 3: clearing a feed of several items costs 0 per-item clicks — End Turn flushes all of them at once", () => {
    const items: RailItem[] = [
      { id: "a", category: "growth", title: "A", body: "", state: "unread", blocking: false },
      { id: "b", category: "intel.new", title: "B", body: "", state: "opened", blocking: false },
      { id: "c", category: "supply.change", title: "C", body: "", state: "minimized", blocking: false }
    ];
    const afterCommit = onCommit(items, true);
    expect(afterCommit).toEqual([]); // zero calls to `dismiss` were needed for any of the three
  });

  it("row 4: changing how a category notifies costs 1 click — on the notification that annoyed you", async () => {
    const user = userEvent.setup();
    render(
      <>
        <NotifyRail
          items={[{ id: "r-1", category: "battle.result", title: "A skirmish resolved", body: "", state: "opened", blocking: false }]}
          onOpen={noop}
          onDismiss={noop}
          onUndoDismiss={noop}
        />
        {/* Settings-list stand-in, reading the same store as the notification above. */}
        <ChannelControl category="battle.result" />
      </>
    );

    await user.click(screen.getAllByRole("button", { name: "Toast" })[0]!); // exactly one interaction

    for (const button of screen.getAllByRole("button", { name: "Toast" })) {
      expect(button).toHaveAttribute("aria-pressed", "true");
    }
  });
});
