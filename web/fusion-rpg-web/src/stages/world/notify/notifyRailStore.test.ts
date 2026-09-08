import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { dismiss, flush, minimizeCategory, onCommit, open, type RailItem } from "./notifyRailStore";

function item(overrides: Partial<RailItem> = {}): RailItem {
  return {
    id: "r-1",
    category: "growth",
    title: "Ash Waste grew a rootbed",
    body: "",
    state: "unread",
    blocking: false,
    ...overrides
  };
}

describe("notifyRail — the flush rule (world-stage W86)", () => {
  it("a commit with a mixed feed leaves only blockers", () => {
    const items = [item({ id: "a", blocking: false }), item({ id: "b", blocking: true, state: "blocking" })];
    expect(onCommit(items, true).map((i) => i.id)).toEqual(["b"]);
  });

  it("a commit with advanced === false leaves the rail untouched", () => {
    const items = [item({ id: "a" }), item({ id: "b", blocking: true, state: "blocking" })];
    expect(onCommit(items, false)).toEqual(items);
  });

  it("dismissing marks an item dismissed rather than erasing it — removed from the feed, not the record", () => {
    const items = [item({ id: "a" })];
    const after = dismiss(items, "a");
    expect(after).toHaveLength(1);
    expect(after[0]!.state).toBe("dismissed");
  });

  it("a blocker cannot be dismissed", () => {
    const items = [item({ id: "a", blocking: true, state: "blocking" })];
    const after = dismiss(items, "a");
    expect(after[0]!.state).toBe("blocking");
  });

  it("opening clears unread without dismissing", () => {
    const items = [item({ id: "a", state: "unread" })];
    const after = open(items, "a");
    expect(after[0]!.state).toBe("opened");
  });

  it("minimizeCategory routes a whole category, never touching a blocker in it", () => {
    const items = [
      item({ id: "a", category: "growth", state: "unread" }),
      item({ id: "b", category: "growth", blocking: true, state: "blocking" }),
      item({ id: "c", category: "intel.new", state: "unread" })
    ];
    const after = minimizeCategory(items, "growth");
    expect(after.find((i) => i.id === "a")!.state).toBe("minimized");
    expect(after.find((i) => i.id === "b")!.state).toBe("blocking");
    expect(after.find((i) => i.id === "c")!.state).toBe("unread");
  });

  it("the store is pure — no React import, no fetch", () => {
    const source = readFileSync(join(__dirname, "notifyRailStore.ts"), "utf8");
    expect(source).not.toMatch(/from\s+["']react["']/i);
    expect(source).not.toMatch(/\bfetch\s*\(/);
  });
});
